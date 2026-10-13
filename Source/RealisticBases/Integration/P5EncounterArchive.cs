using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RealisticBases.Core;
using RealisticBases.Generation;
using RealisticBases.Persistence;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Integration
{
    // P5.4 supported encounter archive/restore for ordinary settlements.
    // Residents stay native world pawns between visits: unload pins them
    // against discard, re-entry takes the same pawns back with their own gear,
    // inventory, needs and injuries. Asset state is keyed by the deterministic
    // layout and reapplied to the regenerated buildings. Nothing is granted.
    internal static class P5EncounterArchive
    {
        private static readonly AccessTools.FieldRef<CompRefuelable, float> FuelRef =
            AccessTools.FieldRefAccess<CompRefuelable, float>("fuel");

        // Before native teardown, while the residents are still spawned.
        internal static List<Pawn> ArchiveResidents(Map map)
        {
            Settlement owner = map.GetComponent<P5ReserveState>()?.Owner;
            P0SettlementState state = owner == null ? null : Current.Game.GetComponent<P0WorldState>()?.FindExisting(owner.ID);
            var controller = map.GetComponent<AI.P3GarrisonController>();
            if (state == null || controller == null) return null;
            List<Pawn> residents = controller.P5Residents
                .Where(p => p.RaceProps.Humanlike && !p.IsPrisoner && p.HostFaction == null)
                .OrderBy(p => p.thingIDNumber).ToList();
            int moved = 0;
            foreach (Pawn pawn in residents)
            {
                // Native despawn drops a carried stack on the closing map. Keep it
                // with the resident, like the rest of its inventory.
                Thing carried = pawn.carryTracker?.CarriedThing;
                if (carried != null && pawn.inventory != null &&
                    pawn.carryTracker.innerContainer.TryTransferToContainer(carried, pawn.inventory.innerContainer)) moved++;
            }
            state.residentArchive = residents.Select(p => p.thingIDNumber).ToList();
            state.residentArchiveTick = GenTicks.TicksGame;
            P0Log.Message("P5_RESIDENT_ARCHIVE", "map=" + map.uniqueID + " settlement=" + owner.ID +
                " residents=" + string.Join(",", state.residentArchive) + " carriedToInventory=" + moved +
                " inventoryUnits=" + residents.Sum(p => p.inventory?.innerContainer.Sum(t => t.stackCount) ?? 0));
            return residents;
        }

        // After native teardown passed them to the world.
        internal static void PinResidents(List<Pawn> residents)
        {
            if (residents == null) return;
            int pinned = 0;
            foreach (Pawn pawn in residents)
                if (!pawn.Dead && !pawn.Spawned && Find.WorldPawns.Contains(pawn) && Find.WorldPawns.ForcefullyKeptPawns.Add(pawn))
                    pinned++;
            P0Log.Message("P5_RESIDENT_PIN", "pinned=" + pinned + "/" + residents.Count);
        }

        internal static void ArchiveAssets(Map map)
        {
            Settlement owner = map.GetComponent<P5ReserveState>()?.Owner;
            P0SettlementState state = owner == null ? null : Current.Game.GetComponent<P0WorldState>()?.FindExisting(owner.ID);
            if (state == null) return;
            var assets = new List<P5AssetState>();
            var present = new HashSet<string>();
            foreach (Building building in map.listerBuildings.allBuildingsNonColonist.Where(b => b.Faction == owner.Faction))
            {
                present.Add(Key(building));
                P5AssetSnapshot snapshot = Snapshot(building);
                if (snapshot != null) assets.Add(P5AssetState.From(snapshot));
            }
            state.assetArchive = assets;
            state.assetArchiveLayout = state.proceduralLayoutDigest;
            state.destroyedAssets = map.GetComponent<P5ReserveState>().BuiltAssets.Where(k => !present.Contains(k)).ToList();
            P0Log.Message("P5_ASSET_ARCHIVE", "map=" + map.uniqueID + " settlement=" + owner.ID +
                " layout=" + state.assetArchiveLayout + " assets=" + assets.Count +
                " damaged=" + assets.Count(a => a.hitPoints >= 0) + " destroyed=" + state.destroyedAssets.Count +
                " fuel=" + assets.Where(a => a.fuel >= 0).Sum(a => a.fuel).ToString("F4") +
                " energy=" + assets.Where(a => a.storedEnergy >= 0).Sum(a => a.storedEnergy).ToString("F2") +
                " loadedShells=" + string.Join(",", assets.Where(a => a.shellDef != null).Select(a => a.shellDef + "x" + a.shellCount)));
        }

        internal static string Key(Thing thing) => P5AssetSnapshot.KeyFor(thing.def.defName, thing.Position.x, thing.Position.z);

        // The archive's destroyed buildings, when it belongs to this layout.
        internal static HashSet<string> Destroyed(P0SettlementState state, string layoutDigest) =>
            state == null || state.assetArchiveLayout != layoutDigest || state.destroyedAssets.Count == 0
                ? new HashSet<string>() : new HashSet<string>(state.destroyedAssets);

        // After commit, like a loss in combat: the full layout was realized and
        // audited, then buildings missing at the last unload are removed again.
        // Their roofs already fell then; drop newly unsupported thin roofs
        // silently instead of collapsing them onto the residents.
        internal static void RemoveDestroyed(Map map, Settlement settlement, IEnumerable<Thing> owned, HashSet<string> destroyed)
        {
            if (destroyed.Count == 0) return;
            List<IntVec3> marked = map.roofCollapseBuffer.CellsMarkedToCollapse;
            var queued = new HashSet<IntVec3>(marked);
            var removed = new List<string>();
            foreach (Building building in owned.OfType<Building>().Where(b => b.Spawned && b.Faction == settlement.Faction &&
                destroyed.Contains(Key(b))).ToList())
            {
                removed.Add(Key(building));
                building.Destroy(DestroyMode.Vanish);
            }
            int roofs = 0;
            foreach (IntVec3 cell in marked.Where(c => !queued.Contains(c)).ToList())
            {
                marked.Remove(cell);
                if (map.roofGrid.RoofAt(cell)?.isThickRoof == false) { map.roofGrid.SetRoof(cell, null); roofs++; }
            }
            P0Log.Message("P5_DESTROYED_RESTORE", "map=" + map.uniqueID + " settlement=" + settlement.ID +
                " removed=" + removed.Count + "/" + destroyed.Count + " roofsAlreadyFallen=" + roofs +
                " defs=" + string.Join(",", removed.Select(k => k.Split('@')[0]).GroupBy(d => d).Select(g => g.Key + ":" + g.Count())) +
                " rebuilt=0");
        }

        private static P5AssetSnapshot Snapshot(Building building)
        {
            int hp = building.def.useHitPoints && building.HitPoints < building.MaxHitPoints ? building.HitPoints : -1;
            CompRefuelable refuelable = building.TryGetComp<CompRefuelable>();
            CompPowerBattery battery = building.TryGetComp<CompPowerBattery>();
            CompChangeableProjectile shell = (building as Building_TurretGun)?.gun?.TryGetComp<CompChangeableProjectile>();
            if (hp < 0 && refuelable == null && battery == null && shell == null) return null;
            return new P5AssetSnapshot(P5AssetSnapshot.KeyFor(building.def.defName, building.Position.x, building.Position.z), hp,
                refuelable?.Fuel ?? -1, battery?.StoredEnergy ?? -1, shell?.LoadedShell?.defName, shell?.loadedCount ?? 0);
        }

        // Archived state paired with the regenerated buildings: exact on the
        // same layout, by definition (consumables only) on a changed one.
        internal static Dictionary<Thing, P5AssetSnapshot> PlanAssets(P0SettlementState state, string layoutDigest,
            IEnumerable<Thing> owned, Faction faction, out bool exact, out List<P5AssetSnapshot> unmatched)
        {
            exact = state != null && state.assetArchiveLayout == layoutDigest;
            unmatched = new List<P5AssetSnapshot>();
            if (state == null || state.assetArchive.Count == 0) return null;
            var buildings = owned.OfType<Building>().Where(b => b.Spawned && b.Faction == faction)
                .GroupBy(Key).ToDictionary(g => g.Key, g => (Thing)g.First());
            var matches = P5AssetSnapshot.Match(state.assetArchive.Select(a => a.Snapshot()), buildings.Keys, exact);
            var plan = matches.ToDictionary(p => buildings[p.Key], p => p.Value);
            var used = new HashSet<string>(matches.Select(p => p.Value.Key));
            unmatched = state.assetArchive.Select(a => a.Snapshot()).Where(a => !used.Contains(a.Key)).ToList();
            if (!exact)
                P0Log.Message("P5_ASSET_RESTORE_BY_DEF", "settlement=" + state.settlementId + " archivedLayout=" +
                    state.assetArchiveLayout + " layout=" + layoutDigest + " matched=" + plan.Count + "/" + state.assetArchive.Count +
                    " damageAndDestroyedNotTransferable=True defs=" + string.Join(",", state.assetArchive.Select(a => a.Snapshot())
                        .GroupBy(a => a.DefName).Select(g => g.Key + ":" + g.Count() + "/" +
                            buildings.Keys.Count(k => k.StartsWith(g.Key + "@", StringComparison.Ordinal)) + "/" +
                            plan.Values.Count(v => v.DefName == g.Key) + ":fuel" + g.Where(a => a.Fuel > 0).Sum(a => a.Fuel).ToString("F0"))));
            return plan;
        }

        // Physical goods held by archived buildings that did not come back:
        // loaded shells return as loose shells, unburnt fuel and turret
        // magazines as whole units of their own fuel item (floored).
        internal static IEnumerable<KeyValuePair<ThingDef, int>> LeftoverFuel(IEnumerable<P5AssetSnapshot> unmatched)
        {
            var units = new Dictionary<ThingDef, double>();
            foreach (P5AssetSnapshot s in unmatched.Where(s => s.Fuel > 0))
            {
                var props = DefDatabase<ThingDef>.GetNamedSilentFail(s.DefName)?.GetCompProperties<CompProperties_Refuelable>();
                ThingDef fuel = props?.fuelFilter?.AnyAllowedDef;
                if (fuel == null || fuel.category != ThingCategory.Item) continue;
                units.TryGetValue(fuel, out double n);
                units[fuel] = n + s.Fuel / Math.Max(0.0001f, props.FuelMultiplierCurrentDifficulty);
            }
            return units.Select(p => new KeyValuePair<ThingDef, int>(p.Key, (int)Math.Floor(p.Value + 1e-6))).Where(p => p.Value > 0);
        }

        internal static IEnumerable<KeyValuePair<ThingDef, int>> LeftoverShells(IEnumerable<P5AssetSnapshot> unmatched) =>
            unmatched.Where(s => s.ShellDef != null && s.ShellCount > 0).GroupBy(s => s.ShellDef)
                .Select(g => new KeyValuePair<ThingDef, int>(DefDatabase<ThingDef>.GetNamedSilentFail(g.Key), g.Sum(s => s.ShellCount)))
                .Where(p => p.Key != null);

        internal static P5AssetSnapshot For(Dictionary<Thing, P5AssetSnapshot> plan, Thing thing) =>
            plan != null && plan.TryGetValue(thing, out var s) ? s : null;

        internal static void SetFuel(CompRefuelable refuelable, float amount)
        {
            FuelRef(refuelable) = amount;
            refuelable.parent.BroadcastCompSignal("Refueled");
        }

        internal static void ApplyAssets(Map map, Settlement settlement, Dictionary<Thing, P5AssetSnapshot> assets)
        {
            if (assets == null) return;
            int applied = 0, damaged = 0;
            float fuel = 0, energy = 0;
            var shells = new List<string>();
            foreach (var pair in assets)
            {
                if (!(pair.Key is Building building) || !building.Spawned) continue;
                P5AssetSnapshot s = pair.Value;
                applied++;
                if (s.HitPoints >= 0 && building.def.useHitPoints)
                {
                    building.HitPoints = s.RestoredHitPoints(building.MaxHitPoints);
                    damaged++;
                }
                CompRefuelable refuelable = building.TryGetComp<CompRefuelable>();
                if (refuelable != null && s.Fuel >= 0)
                {
                    SetFuel(refuelable, s.RestoredFuel(refuelable.Props.fuelCapacity));
                    fuel += refuelable.Fuel;
                }
                CompPowerBattery battery = building.TryGetComp<CompPowerBattery>();
                if (battery != null && s.StoredEnergy >= 0)
                {
                    battery.SetStoredEnergyPct(s.RestoredEnergy(battery.Props.storedEnergyMax) / battery.Props.storedEnergyMax);
                    energy += battery.StoredEnergy;
                }
                CompChangeableProjectile shell = (building as Building_TurretGun)?.gun?.TryGetComp<CompChangeableProjectile>();
                ThingDef shellDef = s.ShellDef == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(s.ShellDef);
                if (shell != null && shellDef != null && shell.LoadedShell == null)
                {
                    shell.LoadShell(shellDef, s.ShellCount);
                    shells.Add(shellDef.defName + "x" + s.ShellCount);
                }
            }
            P0Log.Message("P5_ASSET_RESTORE", "map=" + map.uniqueID + " settlement=" + settlement.ID +
                " applied=" + applied + "/" + assets.Count +
                " damaged=" + damaged + " fuel=" + fuel.ToString("F4") + " energy=" + energy.ToString("F2") +
                " loadedShells=" + (shells.Count == 0 ? "none" : string.Join(",", shells)) + " granted=0");
        }

        // A settlement removed while off-map (native defeat, quest or faction
        // loss) no longer owns its archive: unpin the residents for native
        // world-pawn handling and discard the asset state and archived goods.
        internal static void ReleaseOrphaned(IEnumerable<P0SettlementState> states)
        {
            foreach (P0SettlementState state in states.Where(s => s.residentArchiveTick >= 0 || s.assetArchive.Count > 0 ||
                s.allocatedReserves.Count > 0))
            {
                Settlement settlement = Find.WorldObjects.Settlements.FirstOrDefault(s => s.ID == state.settlementId);
                if (settlement != null && settlement.Faction != null && !settlement.Faction.IsPlayer &&
                    (state.factionDefName == null || settlement.Faction.def.defName == state.factionDefName)) continue;
                int unpinned = 0;
                foreach (Pawn pawn in Find.WorldPawns.AllPawnsAliveOrDead.Where(p => state.residentArchive.Contains(p.thingIDNumber)).ToList())
                    if (Find.WorldPawns.ForcefullyKeptPawns.Remove(pawn)) unpinned++;
                int goods = state.allocatedReserves.Sum(t => t.stackCount);
                state.allocatedReserves.ClearAndDestroyContents();
                state.residentArchive.Clear();
                state.residentArchiveTick = -1;
                state.assetArchive.Clear();
                state.assetArchiveLayout = null;
                state.defeated = true;
                P0Log.Message("P5_ARCHIVE_RELEASED", "settlement=" + state.settlementId +
                    " reason=" + (settlement == null ? "settlement_removed" : "owner_changed") +
                    " unpinnedResidents=" + unpinned + " discardedArchivedUnits=" + goods);
            }
        }

        // The archived survivors, taken back from the world, or null when this
        // settlement has no archive yet (first visit or a pre-P5.4 save).
        internal static List<Pawn> TakeSurvivors(P4SpawnTransaction tx, Settlement settlement, Map map, IList<IntVec3> homes)
        {
            P0SettlementState state = Current.Game.GetComponent<P0WorldState>()?.FindExisting(settlement.ID);
            if (state == null || state.residentArchiveTick < 0) return null;
            Dictionary<int, Pawn> world = Find.WorldPawns.AllPawnsAliveOrDead
                .Where(p => state.residentArchive.Contains(p.thingIDNumber)).GroupBy(p => p.thingIDNumber)
                .ToDictionary(g => g.Key, g => g.First());
            var fates = P5RosterRestore.Resolve(state.residentArchive, id => world.TryGetValue(id, out Pawn p)
                ? new P5ResidentStatus(true, p.Dead || p.Destroyed, p.Faction == settlement.Faction,
                    p.IsPrisoner || p.HostFaction != null)
                : new P5ResidentStatus(false, false, false, false));
            List<Pawn> survivors = fates.Where(f => f.Value == P5ResidentFate.Restored).Select(f => world[f.Key]).ToList();
            if (survivors.Count == 0)
                throw new InvalidOperationException("p5_no_surviving_residents archived=" + state.residentArchive.Count);
            if (survivors.Count > homes.Count) throw new InvalidOperationException("No resident spawn cells remain");
            for (int i = 0; i < survivors.Count; i++)
            {
                Pawn pawn = survivors[i];
                Find.WorldPawns.RemovePawn(pawn);
                tx.OnRollback(() =>
                {
                    if (pawn.Spawned) pawn.DeSpawn();
                    if (!pawn.Dead && !Find.WorldPawns.Contains(pawn)) Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                });
                GenSpawn.Spawn(pawn, homes[i], map);
            }
            P0Log.Message("P5_RESIDENT_RESTORE", "map=" + map.uniqueID + " settlement=" + settlement.ID +
                " archivedTick=" + state.residentArchiveTick + " restored=" + string.Join(",", survivors.Select(p => p.thingIDNumber)) +
                " fates=" + string.Join(",", fates.Select(f => f.Key + ":" + f.Value)) +
                " inventoryUnits=" + survivors.Sum(p => p.inventory?.innerContainer.Sum(t => t.stackCount) ?? 0) +
                " generated=0 replaced=0");
            return survivors;
        }
    }
}
