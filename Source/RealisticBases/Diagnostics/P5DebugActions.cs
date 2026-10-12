using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RealisticBases.Core;
using RealisticBases.Integration;
using RealisticBases.Planning;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Diagnostics
{
    public static class P5DebugActions
    {
        private const string Category = "Realistic Bases P5";
        [DebugAction(Category, "Audit P5 finite encounter readiness", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Readiness() => P5ReadinessAudit.Audit(CurrentSettlement().Map);
        [DebugAction(Category, "Start P5 three-day routine probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Routine() => CurrentSettlement().Map.GetComponent<P5RoutineProbe>().Start();
        internal static Settlement CurrentSettlement()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("Disposable profile only");
            var settlement = Find.CurrentMap?.Parent as Settlement;
            if (!P5TradeStock.Supported(settlement))
            {
                var candidates = Find.Maps.Where(m => P5TradeStock.Supported(m.Parent as Settlement) &&
                    m.GetComponent<RealisticBases.AI.P3GarrisonController>().P5Residents.Any()).ToList();
                if (candidates.Count != 1) throw new InvalidOperationException("No unique supported ordinary encounter map");
                settlement = (Settlement)candidates[0].Parent;
                P0Log.Message("P5_TEST_TARGET_RESOLVED", "view=" + Find.CurrentMap?.uniqueID + " target=" + settlement.Map.uniqueID);
            }
            return settlement;
        }
        [DebugAction(Category, "P5 acquire current ordinary offer once", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Acquire()
        {
            Settlement settlement = CurrentSettlement();
            P5TradeStock.Acquire(settlement);
            Audit();
        }
        [DebugAction(Category, "P5 test native empty offer and loaded expiry", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void EmptyAndExpiry()
        {
            Settlement settlement = CurrentSettlement();
            P5TradeStock.Acquire(settlement);
            var tracker = settlement.trader;
            var holder = P5TradeStock.Holder(tracker);
            List<Thing> original = holder.ToList();
            var counts = original.ToDictionary(t => t, t => t.stackCount);
            int epoch = P5TradeStock.Record(tracker).epoch;
            int nativeTick = P5TradeStock.NativeTick(tracker), now = GenTicks.TicksGame;
            try
            {
                // Temporarily detach, never clone or destroy. No ticks/save occur
                // while borrowed; finally restores the exact original Things.
                foreach (Thing thing in original) holder.Remove(thing);
                if (tracker.StockListForReading.Count != 0 || tracker.StockListForReading.Count != 0 ||
                    P5TradeStock.Holder(tracker) != holder || P5TradeStock.NativeTick(tracker) != nativeTick)
                    throw new InvalidOperationException("Empty getter regenerated canonical stock");
            }
            finally
            {
                foreach (Thing thing in original)
                    if (!holder.Contains(thing) && !holder.TryAdd(thing, canMergeWithExistingStacks: false))
                        throw new InvalidOperationException("Borrowed native stock recovery failed: " + thing);
            }
            try
            {
                // Synchronous clock boundary, not a measured 30-day encounter.
                Find.TickManager.DebugSetTicksGame(checked(nativeTick + P5StockEpoch.RestockTicks + 1));
                tracker.TraderTrackerTick();
            }
            finally { Find.TickManager.DebugSetTicksGame(now); }
            if (holder != P5TradeStock.Holder(tracker) || counts.Any(p => !holder.Contains(p.Key) ||
                p.Key.stackCount != p.Value) || holder.Count != original.Count || P5TradeStock.Record(tracker).epoch != epoch)
                throw new InvalidOperationException("Loaded expiry changed native stock");
            P0Log.Message("P5_STOCK_SPIKE_COMPLETE", "map=" + settlement.Map.uniqueID + " settlement=" + settlement.ID +
                " passed=True emptyReads=2 loadedExpirySuppressed=True exactThingsRestored=" + original.Count +
                " epoch=" + epoch + " tickRestored=" + (now == GenTicks.TicksGame) + " physicalCopiesAdded=0");
            Audit();
        }
        [DebugAction(Category, "Audit P5 native stock ownership", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Audit()
        {
            Settlement settlement = CurrentSettlement();
            var snapshot = P5TradeStock.Snapshot(settlement.trader, out var unsupported);
            var holder = P5TradeStock.Holder(settlement.trader);
            var record = P5TradeStock.Record(settlement.trader);
            P0Log.Message("P5_STOCK_AUDIT", "map=" + settlement.Map.uniqueID + " settlement=" + settlement.ID +
                " holder=" + (holder != null) + " epoch=" + (record?.epoch ?? 0) + " status=" + record?.status +
                " generated=" + P5TradeStock.NativeTick(settlement.trader) + " nextRestock=" + settlement.NextRestockTick +
                " stacks=" + snapshot.Items.Count + " units=" + snapshot.Items.Sum(i => (long)i.Count) +
                " nativeOwnership=" + (holder == null || holder.All(t => t.ParentHolder == settlement.trader && !t.Spawned)) +
                " unsupported=" + string.Join("|", unsupported) + " items=" + string.Join("|", snapshot.Items.Select(i =>
                    i.Id + ":" + i.Def + ":" + i.Count + ":" + i.Stuff + ":q" + i.Quality + ":hp" + i.HitPoints + "/" + i.MaxHitPoints)));
        }
    }
}
