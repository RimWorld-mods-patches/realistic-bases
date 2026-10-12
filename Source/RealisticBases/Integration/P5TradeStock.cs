using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RealisticBases.Core;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Integration
{
    public sealed class P5StockRecord : IExposable
    {
        public int settlementId = -1;
        public P5StockStatus status;
        public int epoch;
        public int generatedTick = -1;
        public P5StockEpoch Policy(bool encounter) => new P5StockEpoch(status, epoch, generatedTick, encounter);
        public void Apply(P5StockEpoch state)
        { status = state.Status; epoch = state.Epoch; generatedTick = state.GeneratedTick; }
        public void ExposeData()
        {
            Scribe_Values.Look(ref settlementId, "settlementId", -1);
            Scribe_Values.Look(ref status, "status", P5StockStatus.Uninitialized);
            Scribe_Values.Look(ref epoch, "epoch", 0);
            Scribe_Values.Look(ref generatedTick, "generatedTick", -1);
        }
    }

    // Native tracker remains the sole owner at this spike checkpoint. No map
    // materialization or quantity copy is enabled until the adapter is tested.
    public sealed class P5WorldStock : GameComponent
    {
        private List<P5StockRecord> records = new List<P5StockRecord>();
        public P5WorldStock(Game game) { }
        public P5StockRecord Find(int id) => records.FirstOrDefault(r => r.settlementId == id);
        public P5StockRecord GetOrCreate(int id)
        {
            P5StockRecord record = Find(id);
            if (record == null) records.Add(record = new P5StockRecord { settlementId = id });
            return record;
        }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref records, "p5StockEpochs", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && records == null) records = new List<P5StockRecord>();
        }
        public override void LoadedGame()
        {
            foreach (Settlement settlement in Verse.Find.WorldObjects.AllWorldObjects.OfType<Settlement>())
                if (Find(settlement.ID) != null && P5TradeStock.Supported(settlement))
                    P5TradeStock.RestoreHolderOwner(settlement.trader);
        }
    }

    internal static class P5TradeStock
    {
        private static readonly FieldInfo GeneratedTick = AccessTools.Field(typeof(Settlement_TraderTracker), "lastStockGenerationTicks");
        private static readonly MethodInfo Generate = AccessTools.Method(typeof(Settlement_TraderTracker), "RegenerateStock");
        private static readonly FieldInfo HolderOwner = AccessTools.Field(typeof(ThingOwner), "owner");
        internal static bool Supported(Settlement settlement) => settlement != null &&
            settlement.GetType() == typeof(Settlement) && settlement.trader?.GetType() == typeof(Settlement_TraderTracker) &&
            settlement.Faction != null && !settlement.Faction.IsPlayer && settlement.Faction.def.humanlikeFaction &&
            settlement.trader.TraderKind != null && GeneratedTick != null && Generate != null;
        internal static P5StockRecord Record(Settlement_TraderTracker tracker)
            => tracker?.settlement == null ? null : Current.Game?.GetComponent<P5WorldStock>()?.Find(tracker.settlement.ID);
        internal static ThingOwner<Thing> Holder(Settlement_TraderTracker tracker) => tracker.GetDirectlyHeldThings() as ThingOwner<Thing>;
        internal static int NativeTick(Settlement_TraderTracker tracker) => (int)GeneratedTick.GetValue(tracker);
        internal static List<Thing> Offer(Settlement_TraderTracker tracker)
        {
            var native = Holder(tracker)?.InnerListForReading ?? new List<Thing>();
            var physical = P5PhysicalSaleStock.For(tracker);
            if (physical == null) return native;
            var combined = native.Concat(physical.Goods).ToList();
            if (combined.GroupBy(t => t.thingIDNumber).Any(g => g.Count() != 1))
                throw new InvalidOperationException("Duplicate canonical physical/native stock identity");
            return combined;
        }
        internal static void RestoreHolderOwner(Settlement_TraderTracker tracker)
        {
            ThingOwner holder = Holder(tracker);
            if (holder == null) return;
            // Native tracker deep-loads stock without its constructor owner.
            // Rebind only a verified, otherwise ownerless, canonical holder.
            if (holder.Owner == null)
            {
                HolderOwner.SetValue(holder, tracker);
                P0Log.Message("P5_STOCK_HOLDER_REBOUND", "settlement=" + tracker.settlement.ID + " stacks=" + holder.Count);
            }
            if (holder.Owner != tracker) throw new InvalidOperationException("Native stock has a different owner");
        }

        // Explicit mutable integration boundary. Planner callers only receive Snapshot.
        internal static P5StockSnapshot Acquire(Settlement settlement)
        {
            if (!Supported(settlement)) throw new InvalidOperationException("Unsupported native trader/settlement contract");
            P5StockRecord record = Current.Game.GetComponent<P5WorldStock>().GetOrCreate(settlement.ID);
            var decision = record.Policy(settlement.HasMap).Decide(P5StockRequest.Acquire, true,
                Holder(settlement.trader) != null, settlement.trader.EverVisited, NativeTick(settlement.trader), GenTicks.TicksGame);
            if (decision.Action == P5StockAction.GenerateOnce)
                Generate.Invoke(settlement.trader, null);
            else if (decision.Action == P5StockAction.Inconsistent || decision.Action == P5StockAction.SuppressExpiry)
                throw new InvalidOperationException("Stock acquisition rejected: " + decision.Reason);
            record.Apply(decision.State);
            RestoreHolderOwner(settlement.trader);
            P0Log.Message("P5_STOCK_ACQUIRE", "settlement=" + settlement.ID + " action=" + decision.Action +
                " reason=" + decision.Reason + " epoch=" + record.epoch + " tick=" + record.generatedTick);
            return Snapshot(settlement.trader, out _);
        }

        internal static P5StockSnapshot Snapshot(Settlement_TraderTracker tracker, out List<string> unsupported)
        {
            unsupported = new List<string>();
            List<P5ItemFacts> items = new List<P5ItemFacts>();
            foreach (Thing thing in Offer(tracker))
                {
                    string reason = Unsupported(thing);
                    if (reason != null) { unsupported.Add(thing.ThingID + ":" + reason); continue; }
                    QualityCategory quality;
                    int grade = thing.TryGetQuality(out quality) ? (int)quality : -1;
                    items.Add(new P5ItemFacts(thing.thingIDNumber, thing.def.defName, thing.stackCount,
                        thing.Stuff?.defName, grade, thing.def.useHitPoints ? thing.HitPoints : -1,
                        thing.def.useHitPoints ? thing.MaxHitPoints : -1, stackLimit: Math.Max(1, thing.def.stackLimit),
                        marketValue: thing.MarketValue, storage: thing.TryGetComp<CompExplosive>() != null ?
                            P5StorageKind.Hazardous : thing.TryGetComp<CompRottable>() != null ?
                            P5StorageKind.Perishable : P5StorageKind.Ordinary));
                }
            return new P5StockSnapshot(items);
        }

        internal static string Unsupported(Thing thing)
        {
            if (thing == null || thing.Destroyed) return "invalid_thing";
            if (thing is Pawn) return "native_pawn_ownership";
            if (thing.def.category != ThingCategory.Item) return "not_ordinary_item";
            if (thing is IThingHolder || thing.def.ThisOrAnyCompIsThingHolder()) return "nested_container";
            if (thing.GetType().Assembly != typeof(Thing).Assembly ||
                thing is ThingWithComps comps && comps.AllComps.Any(c => c.GetType().Assembly != typeof(Thing).Assembly))
                return "unknown_item_behavior";
            return null;
        }
    }

    [HarmonyPatch(typeof(Settlement_TraderTracker), nameof(Settlement_TraderTracker.StockListForReading), MethodType.Getter)]
    internal static class P5EmptyStockReadPatch
    {
        private static bool Prefix(Settlement_TraderTracker __instance, ref List<Thing> __result)
        {
            P5StockRecord record = P5TradeStock.Record(__instance);
            if (record == null || !P5TradeStock.Supported(__instance.settlement)) return true;
            if (record.status == P5StockStatus.Canonical || record.status == P5StockStatus.Claimed)
            {
                // A depleted native holder is still this epoch. Missing canonical
                // ownership fails closed instead of rerolling an observed offer.
                __result = P5TradeStock.Offer(__instance);
                return false;
            }
            if (__instance.settlement.HasMap && record.status == P5StockStatus.Expired)
            { __result = new List<Thing>(); return false; }
            return true;
        }
        private static void Postfix(Settlement_TraderTracker __instance)
        {
            P5StockRecord record = P5TradeStock.Record(__instance);
            if (record == null || record.status != P5StockStatus.Expired || !P5TradeStock.Supported(__instance.settlement)) return;
            if (P5TradeStock.Holder(__instance) != null)
                record.Apply(new P5StockEpoch(P5StockStatus.Canonical, checked(record.epoch + 1), P5TradeStock.NativeTick(__instance)));
        }
    }

    [HarmonyPatch(typeof(Settlement_TraderTracker), nameof(Settlement_TraderTracker.TraderTrackerTick))]
    internal static class P5StockExpiryPatch
    {
        private static bool Prefix(Settlement_TraderTracker __instance)
        {
            P5StockRecord record = P5TradeStock.Record(__instance);
            if (record == null || !P5TradeStock.Supported(__instance.settlement)) return true;
            var decision = record.Policy(__instance.settlement.HasMap).Decide(P5StockRequest.ScheduledExpiry, true,
                P5TradeStock.Holder(__instance) != null, __instance.EverVisited, P5TradeStock.NativeTick(__instance), GenTicks.TicksGame);
            return decision.Action != P5StockAction.SuppressExpiry;
        }
        private static void Postfix(Settlement_TraderTracker __instance)
        {
            P5StockRecord record = P5TradeStock.Record(__instance);
            if (record == null || !P5TradeStock.Supported(__instance.settlement) || P5TradeStock.Holder(__instance) != null) return;
            var decision = record.Policy(__instance.settlement.HasMap).Decide(P5StockRequest.ScheduledExpiry, true,
                false, __instance.EverVisited, P5TradeStock.NativeTick(__instance), GenTicks.TicksGame);
            if (decision.Action == P5StockAction.ExpireOffMap) record.Apply(decision.State);
        }
    }
}
