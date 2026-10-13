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
            foreach (Building building in map.listerBuildings.allBuildingsNonColonist.Where(b => b.Faction == owner.Faction))
            {
                P5AssetSnapshot snapshot = Snapshot(building);
                if (snapshot != null) assets.Add(P5AssetState.From(snapshot));
            }
            state.assetArchive = assets;
            state.assetArchiveLayout = state.proceduralLayoutDigest;
            P0Log.Message("P5_ASSET_ARCHIVE", "map=" + map.uniqueID + " settlement=" + owner.ID +
                " layout=" + state.assetArchiveLayout + " assets=" + assets.Count +
                " damaged=" + assets.Count(a => a.hitPoints >= 0) +
                " fuel=" + assets.Where(a => a.fuel >= 0).Sum(a => a.fuel).ToString("F4") +
                " energy=" + assets.Where(a => a.storedEnergy >= 0).Sum(a => a.storedEnergy).ToString("F2") +
                " loadedShells=" + string.Join(",", assets.Where(a => a.shellDef != null).Select(a => a.shellDef + "x" + a.shellCount)));
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

        // The archive applies only to the layout it was recorded for.
        internal static Dictionary<string, P5AssetSnapshot> Assets(P0SettlementState state, string layoutDigest)
        {
            if (state == null || state.assetArchive.Count == 0 && state.assetArchiveLayout == null) return null;
            if (state.assetArchiveLayout != layoutDigest)
            {
                P0Log.Message("P5_ASSET_RESTORE_SKIPPED", "settlement=" + state.settlementId +
                    " archivedLayout=" + state.assetArchiveLayout + " layout=" + layoutDigest);
                return null;
            }
            return state.assetArchive.Select(a => a.Snapshot()).GroupBy(a => a.Key).ToDictionary(g => g.Key, g => g.First());
        }

        internal static P5AssetSnapshot For(Dictionary<string, P5AssetSnapshot> assets, Thing thing)
        {
            if (assets == null) return null;
            return assets.TryGetValue(P5AssetSnapshot.KeyFor(thing.def.defName, thing.Position.x, thing.Position.z), out var s) ? s : null;
        }

        internal static void SetFuel(CompRefuelable refuelable, float amount)
        {
            FuelRef(refuelable) = amount;
            refuelable.parent.BroadcastCompSignal("Refueled");
        }

        internal static void ApplyAssets(Map map, Settlement settlement, IEnumerable<Thing> owned, Dictionary<string, P5AssetSnapshot> assets)
        {
            if (assets == null) return;
            int applied = 0, damaged = 0;
            float fuel = 0, energy = 0;
            var shells = new List<string>();
            var matched = new HashSet<string>();
            foreach (Building building in owned.OfType<Building>().Where(b => b.Spawned && b.Faction == settlement.Faction))
            {
                P5AssetSnapshot s = For(assets, building);
                if (s == null) continue;
                matched.Add(s.Key);
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
                " applied=" + applied + "/" + assets.Count + " unmatched=" + (assets.Count - matched.Count) +
                " damaged=" + damaged + " fuel=" + fuel.ToString("F4") + " energy=" + energy.ToString("F2") +
                " loadedShells=" + (shells.Count == 0 ? "none" : string.Join(",", shells)) + " granted=0");
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
