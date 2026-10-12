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

        public P5ReserveState(Map map) : base(map) { }

        internal bool Active => owner != null && owner.Faction?.loadID == factionId && !owner.Faction.IsPlayer && map.Parent == owner;

        internal void Begin(Settlement settlement, P4SpawnTransaction tx)
        {
            owner = settlement;
            factionId = settlement.Faction.loadID;
            keys.Clear();
            planned.Clear();
            tx.OnRollback(() => { owner = null; factionId = -1; keys.Clear(); planned.Clear(); });
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

        public override void ExposeData()
        {
            Scribe_References.Look(ref owner, "p5ReserveOwner");
            Scribe_Values.Look(ref factionId, "p5ReserveFaction", -1);
            Scribe_Collections.Look(ref keys, "p5ReserveKeys", LookMode.Value);
            Scribe_Collections.Look(ref planned, "p5ReservePlanned", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                keys = keys ?? new List<string>();
                planned = planned ?? new List<int>();
            }
        }

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
                P5PhysicalSaleStock.Tracked(t) == null;
            long units = map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && match(t)).Sum(t => (long)t.stackCount);
            foreach (Pawn pawn in map.mapPawns.SpawnedPawnsInFaction(owner.Faction))
                if (pawn.inventory?.innerContainer != null)
                    units += pawn.inventory.innerContainer.Where(match).Sum(t => (long)t.stackCount);
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
                    .Select(e => e.Key + ":" + e.Value)));
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
            try { map?.GetComponent<P5ReserveState>()?.PersistOnUnload(); }
            catch (Exception ex) { P0Log.Error("P5_RESERVE_LEDGER_FAILED", "map=" + map?.uniqueID + " " + ex); }
        }
    }
}
