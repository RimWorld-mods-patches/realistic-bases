using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RealisticBases.AI;
using RealisticBases.Core;
using RealisticBases.Generation;
using RealisticBases.Integration;
using RealisticBases.Planning;
using RimWorld;
using Verse;

namespace RealisticBases.Diagnostics
{
    public static class P5PhysicalStockProbe
    {
        [DebugAction("Realistic Bases P5", "P5 test physical stock late rollback", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Rollback() => Run(true);

        [DebugAction("Realistic Bases P5", "P5 prototype inert physical sale stock", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Commit() => Run(false);

        private static void Run(bool rollback)
        {
            var settlement = P5DebugActions.CurrentSettlement();
            Map map = settlement.Map;
            P5TradeStock.Acquire(settlement);
            var holder = P5TradeStock.Holder(settlement.trader);
            var before = P5TradeStock.Snapshot(settlement.trader, out _);
            var original = holder.ToDictionary(t => t, t => t.stackCount);
            var selected = holder.Where(t => t.def == ThingDefOf.Steel || t.def == ThingDefOf.MedicineIndustrial).ToArray();
            if (selected.Length < 2) throw new InvalidOperationException("Require original native steel and medicine stocks");
            var shelves = map.GetComponent<P3GarrisonController>().P5OwnedThings.OfType<Building_Storage>()
                .Where(s => s.Spawned && s.Faction == settlement.Faction).SelectMany(s => s.OccupiedRect().Cells).ToList();
            var tx = new P4SpawnTransaction(map, new TerrainMutationFootprint(null, null, null, null));
            List<Thing> placed;
            bool committed = false;
            try
            {
                placed = map.GetComponent<P5PhysicalSaleStock>().Transfer(tx, settlement, selected, shelves);
                if (rollback) throw new InvalidOperationException("forced P5 failure after borrowed ownership and splits");
                var after = P5TradeStock.Snapshot(settlement.trader, out _);
                if (Aggregate(before) != Aggregate(after) || selected.Any(t => holder.Contains(t)) ||
                    placed.Any(t => !t.Spawned || t.Map != map || t.stackCount > t.def.stackLimit || !(t.Position.GetEdifice(map) is Building_Storage)))
                    throw new InvalidOperationException("Physical stock conservation/storage invariant failed");
                tx.Commit();
                committed = true;
                P0Log.Message("P5_PHYSICAL_STOCK_COMMIT", "map=" + map.uniqueID + " passed=True originals=" + selected.Length +
                    " physicalStacks=" + placed.Count + " ordinaryUnits=" + after.Items.Sum(t => (long)t.Count) +
                    " aggregate=" + Aggregate(after) + " nativeCopiesRetained=0 prototypeSlice=True fullOfferMaterialized=False");
                Audit();
            }
            catch (Exception ex)
            {
                if (committed) throw;
                bool restored = tx.Rollback();
                bool exact = holder.Count == original.Count && original.All(p => holder.Contains(p.Key) &&
                    p.Key.stackCount == p.Value && p.Key.holdingOwner == holder && !p.Key.Spawned);
                bool ledger = Aggregate(before) == Aggregate(P5TradeStock.Snapshot(settlement.trader, out _));
                P0Log.Message("P5_PHYSICAL_STOCK_ROLLBACK", "map=" + map.uniqueID + " forced=" + rollback +
                    " recovered=" + restored + " exactNativeThings=" + exact + " quantitiesAttributes=" + ledger +
                    " itemLedgerRestored=" + tx.ItemLedgerRestored + " provisionalRoofCollapsesDiscarded=" + tx.DiscardedProvisionalRoofCollapses +
                    " reason=" + ex.Message);
                if (!rollback || !restored || !exact || !ledger) throw;
            }
        }
        [DebugAction("Realistic Bases P5", "P5 test sale reserve merge separation", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void MergeSeparation()
        {
            var settlement = P5DebugActions.CurrentSettlement();
            Map map = settlement.Map;
            Thing sale = map.GetComponent<P5PhysicalSaleStock>().Goods.First(t => t.def == ThingDefOf.Steel);
            Thing reserve = map.GetComponent<P3GarrisonController>().P5OwnedThings.First(t => t.Spawned && t.def == sale.def &&
                P5PhysicalSaleStock.Tracked(t) == null);
            int saleBefore = sale.stackCount, reserveBefore = reserve.stackCount;
            bool merged = reserve.TryAbsorbStack(sale, respectStackLimit: false);
            bool passed = !merged && !sale.Destroyed && sale.stackCount == saleBefore && reserve.stackCount == reserveBefore;
            P0Log.Message("P5_SALE_RESERVE_MERGE", "map=" + map.uniqueID + " passed=" + passed + " nativeMerged=" + merged +
                " sale=" + sale.ThingID + ":" + sale.stackCount + " reserve=" + reserve.ThingID + ":" + reserve.stackCount);
            if (!passed) throw new InvalidOperationException("Sale allocation merged into an operating reserve");
        }
        [DebugAction("Realistic Bases P5", "Audit P5 physical sale stock", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Audit()
        {
            var settlement = P5DebugActions.CurrentSettlement();
            var component = settlement.Map.GetComponent<P5PhysicalSaleStock>();
            var native = P5TradeStock.Holder(settlement.trader);
            var physical = component.Goods.ToList();
            var snapshot = P5TradeStock.Snapshot(settlement.trader, out var unsupported);
            string ids = string.Join("|", physical.OrderBy(t => t.thingIDNumber).Select(t => t.ThingID + ":" + t.stackCount +
                ":" + t.Stuff?.defName + ":" + t.HitPoints + "@" + t.Position));
            P0Log.Message("P5_PHYSICAL_STOCK_AUDIT", "map=" + settlement.Map.uniqueID + " active=" + component.Active +
                " mappedStacks=" + physical.Count + " mappedUnits=" + physical.Sum(t => t.stackCount) +
                " nativeStacks=" + native.Count + " ordinaryUnits=" + snapshot.Items.Sum(t => (long)t.Count) +
                " aggregate=" + Aggregate(snapshot) + " duplicatePhysicalNative=" + physical.Any(native.Contains) +
                " unsupportedNative=" + unsupported.Count + " mapped=" + ids);
        }
        private static string Aggregate(P5StockSnapshot stock)
        {
            var groups = stock.Items.GroupBy(i => i.Def + ":" + i.Stuff + ":" + i.Quality + ":" + i.HitPoints + ":" + i.MaxHitPoints)
                .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + "=" + g.Sum(i => (long)i.Count));
            return StableHash.Add(StableHash.Start, string.Join("|", groups)).ToString("X16");
        }
    }
}
