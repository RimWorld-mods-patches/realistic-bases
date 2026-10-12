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
    // P5 stock persistence on the loaded encounter map: which operating
    // reserves generation planned, so that unload can record their remainder.
    public sealed class P5ReserveState : MapComponent
    {
        private Settlement owner;
        private int factionId = -1;
        private List<string> keys = new List<string>();
        private List<int> planned = new List<int>();
        private List<Thing> allocated = new List<Thing>();
        private static Game allocationGame;
        private static readonly Dictionary<Thing, P5ReserveState> allocations = new Dictionary<Thing, P5ReserveState>();

        public P5ReserveState(Map map) : base(map) { }

        internal bool Active => owner != null && owner.Faction?.loadID == factionId && !owner.Faction.IsPlayer && map.Parent == owner;

        internal void Begin(Settlement settlement, P4SpawnTransaction tx)
        {
            owner = settlement;
            factionId = settlement.Faction.loadID;
            keys.Clear();
            planned.Clear();
            tx.OnRollback(() => { foreach (Thing t in allocated) allocations.Remove(t); allocated.Clear();
                owner = null; factionId = -1; keys.Clear(); planned.Clear(); });
        }

        // Records what this generation actually granted, including zero: an
        // exhausted reserve must stay in the ledger, or a later visit would
        // treat it as never generated and spawn it in full again.
        internal void Record(string key, int count)
        {
            if (owner == null || count < 0) return;
            keys.Add(key);
            planned.Add(count);
        }

        internal IEnumerable<string> Defs => keys.Select(k => k.Split('|')[0]).Distinct();
        internal IEnumerable<string> Keys => keys.Distinct();

        internal static P5ReserveState AllocationFor(Thing thing)
        {
            if (thing == null) return null;
            if (!ReferenceEquals(allocationGame, Current.Game))
            {
                allocationGame = Current.Game; allocations.Clear();
                if (Current.Game != null)
                    foreach (Map current in Find.Maps)
                        foreach (Thing t in (current.GetComponent<P5ReserveState>().allocated ?? new List<Thing>()).Where(t => t != null && !t.Destroyed))
                            allocations[t] = current.GetComponent<P5ReserveState>();
            }
            return allocations.TryGetValue(thing, out var state) && state.Active ? state : null;
        }

        internal void RegisterAllocation(Thing thing)
        {
            AllocationFor(thing); // Initialize the per-game reference cache first.
            if (!allocated.Contains(thing)) allocated.Add(thing);
            allocations[thing] = this;
        }
        internal void ForgetAllocation(Thing thing) { allocated.Remove(thing); allocations.Remove(thing); }
        private bool StillOwned(Thing thing)
        {
            if (thing == null || thing.Destroyed) return false;
            if (thing.Spawned) return thing.Map == map;
            if (thing.ParentHolder is Pawn_InventoryTracker inventory)
                return inventory.pawn.Map == map && inventory.pawn.Faction == owner.Faction && !inventory.pawn.IsPrisoner;
            if (thing.ParentHolder is Pawn_CarryTracker carry)
                return carry.pawn.Map == map && carry.pawn.Faction == owner.Faction && !carry.pawn.IsPrisoner;
            return false;
        }
        internal IEnumerable<Thing> Allocated => Active ? allocated.Where(StillOwned) : Enumerable.Empty<Thing>();
        public override void MapComponentTick()
        {
            if (owner == null || GenTicks.TicksGame % 250 != 0) return;
            foreach (Thing t in allocated.Where(t => !Active || !StillOwned(t)).ToArray())
            {
                ForgetAllocation(t);
                map.GetComponent<AI.P3GarrisonController>().P5UnregisterOperatingStock(t);
            }
        }
        internal void ArchiveAllocated()
        {
            if (!Active) return;
            var state = Current.Game.GetComponent<P0WorldState>().FindExisting(owner.ID)
                ?? throw new InvalidOperationException("No persisted operating owner");
            int count = 0, units = 0;
            foreach (Thing thing in Allocated.ToArray())
            {
                if (thing.Spawned) thing.DeSpawn(); else thing.holdingOwner?.Remove(thing);
                if (!state.allocatedReserves.TryAdd(thing, canMergeWithExistingStacks: false))
                    throw new InvalidOperationException("Operating archive refused " + thing.ThingID);
                ForgetAllocation(thing); count++; units += thing.stackCount;
            }
            P0Log.Message("P5_ALLOCATED_ARCHIVE", "map=" + map.uniqueID + " settlement=" + owner.ID +
                " stacks=" + count + " units=" + units + " copies=0");
        }
        internal List<Thing> RestoreAllocated(P4SpawnTransaction tx, IEnumerable<IntVec3> storage, IEnumerable<IntVec3> food)
        {
            var state = Current.Game.GetComponent<P0WorldState>().FindExisting(owner.ID);
            var holder = state?.allocatedReserves;
            var originals = holder?.ToList() ?? new List<Thing>();
            if (originals.Count == 0) return originals;
            if (originals.Any(t => t.stackCount > t.def.stackLimit))
                throw new InvalidOperationException("Oversized retained operating stack requires explicit split recovery");
            var places = P5PhysicalSaleStock.StorageLocations(map, owner, originals,
                t => t.TryGetComp<CompRottable>() == null ? storage : food);
            foreach (Thing thing in originals)
            {
                tx.Borrow(thing, holder);
                tx.OnRollback(() => ForgetAllocation(thing));
                GenSpawn.Spawn(thing, places[thing].Single(), map);
                RegisterAllocation(thing);
            }
            P0Log.Message("P5_ALLOCATED_RESTORE", "map=" + map.uniqueID + " settlement=" + owner.ID +
                " stacks=" + originals.Count + " units=" + originals.Sum(t => t.stackCount) + " recreated=0" +
                " items=" + string.Join(",", originals.Select(t => t.ThingID + ":" + t.stackCount)));
            return originals;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref owner, "p5ReserveOwner");
            Scribe_Values.Look(ref factionId, "p5ReserveFaction", -1);
            Scribe_Collections.Look(ref keys, "p5ReserveKeys", LookMode.Value);
            Scribe_Collections.Look(ref planned, "p5ReservePlanned", LookMode.Value);
            Scribe_Collections.Look(ref allocated, "p5AllocatedOperating", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                keys = keys ?? new List<string>();
                planned = planned ?? new List<int>();
                allocated = allocated?.Where(t => t != null && !t.Destroyed).ToList() ?? new List<Thing>();
            }
        }
        public override void FinalizeInit() { foreach (Thing t in allocated.ToArray()) RegisterAllocation(t); }

        // Remaining units of one reserve key: loose items on the map that are
        // not sale goods, items carried by the settlement's own pawns, and
        // fuel already loaded into its generators (they are refilled from the
        // same reserve on re-entry). Turret and mortar loads are not counted.
        internal int Observe(string key)
        {
            string[] parts = key.Split('|');
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(parts[0]);
            if (def == null) return 0;
            ThingDef stuff = parts.Length > 1 ? DefDatabase<ThingDef>.GetNamedSilentFail(parts[1]) : null;
            Func<Thing, bool> match = t => t.def == def && (parts.Length == 1 || t.Stuff == stuff) &&
                P5PhysicalSaleStock.Tracked(t) == null && AllocationFor(t) == null;
            long units = map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && match(t)).Sum(t => (long)t.stackCount);
            foreach (Pawn pawn in map.mapPawns.SpawnedPawnsInFaction(owner.Faction).Where(p => !p.IsPrisoner))
            {
                if (pawn.inventory?.innerContainer != null)
                    units += pawn.inventory.innerContainer.Where(match).Sum(t => (long)t.stackCount);
                Thing carried = pawn.carryTracker?.CarriedThing;
                if (carried != null && match(carried)) units += carried.stackCount;
            }
            foreach (Building building in map.listerBuildings.allBuildingsNonColonist.Where(b => b.Faction == owner.Faction &&
                b.TryGetComp<CompPowerPlant>() != null))
            {
                CompRefuelable fuel = building.TryGetComp<CompRefuelable>();
                if (fuel != null && fuel.Props.fuelFilter.Allows(def))
                    units += (long)Math.Floor(fuel.Fuel / Math.Max(0.0001f, fuel.Props.FuelMultiplierCurrentDifficulty));
            }
            return (int)Math.Min(int.MaxValue, units);
        }

        internal void PersistOnUnload()
        {
            if (!Active || keys.Count == 0) return;
            P0SettlementState state = Current.Game.GetComponent<P0WorldState>()?.FindExisting(owner.ID);
            if (state == null) return;
            P5ReserveLedger ledger = P5ReserveLedger.Observe(keys.Zip(planned, (k, v) => new KeyValuePair<string, int>(k, v)), Observe);
            state.StoreReserveLedger(ledger, GenTicks.TicksGame);
            P0Log.Message("P5_RESERVE_LEDGER", "map=" + map.uniqueID + " settlement=" + owner.ID + " tick=" + GenTicks.TicksGame +
                " remaining=" + string.Join(",", ledger.Remaining.OrderBy(e => e.Key, StringComparer.Ordinal)
                    .Select(e => e.Key + ":" + e.Value)) + " allocatedArchived=" + state.allocatedReserves.Sum(t => t.stackCount));
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    internal static class P5OperatingSplitPatch
    {
        private static void Prefix(Thing __instance, out P5ReserveState __state) => __state = P5ReserveState.AllocationFor(__instance);
        private static void Postfix(Thing __instance, Thing __result, P5ReserveState __state)
        {
            if (__state == null || __result == null || __result == __instance) return;
            __state.RegisterAllocation(__result);
            (__instance.MapHeld ?? __result.MapHeld)?.GetComponent<AI.P3GarrisonController>().P5RegisterOperatingStock(__result);
        }
    }

    // Pre-teardown boundary: runs before MapDeiniter passes pawns to the world,
    // so sale goods and reserves are still on the map with their owners.
    [HarmonyPatch(typeof(MapDeiniter), nameof(MapDeiniter.Deinit))]
    internal static class P5MapUnloadPatch
    {
        private static void Prefix(Map map)
        {
            try { map?.GetComponent<P5PhysicalSaleStock>()?.ReturnToNative(); }
            catch (Exception ex) { P0Log.Error("P5_SALE_RETURN_FAILED", "map=" + map?.uniqueID + " " + ex); }
            try { map?.GetComponent<P5ReserveState>()?.ArchiveAllocated(); }
            catch (Exception ex) { P0Log.Error("P5_ALLOCATED_ARCHIVE_FAILED", "map=" + map?.uniqueID + " " + ex); }
            try { map?.GetComponent<P5ReserveState>()?.PersistOnUnload(); }
            catch (Exception ex) { P0Log.Error("P5_RESERVE_LEDGER_FAILED", "map=" + map?.uniqueID + " " + ex); }
        }
    }
}
