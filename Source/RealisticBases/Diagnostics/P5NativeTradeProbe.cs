using System;
using System.Linq;
using LudeonTK;
using RealisticBases.Core;
using RealisticBases.Integration;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Diagnostics
{
    // A native TradeDeal spike, not an alternative economy or free-price API.
    // Requires a reset disposable colony with an existing funded negotiator.
    public static class P5NativeTradeProbe
    {
        [DebugAction("Realistic Bases P5", "P5 prototype native partial buy and sell", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void Run()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("Disposable profile only");
            if (Find.Maps.Any(m => m.Parent is Settlement && m.GetComponent<RealisticBases.AI.P3GarrisonController>().P5Residents.Any()))
                throw new InvalidOperationException("Restore the untouched colony before this independent trade spike");
            Map home = Find.Maps.Single(m => m.IsPlayerHome);
            Settlement target = Find.WorldObjects.Settlements.Where(s => P5TradeStock.Supported(s) && !s.HasMap &&
                s.Faction.def.techLevel >= TechLevel.Industrial && !s.Faction.HostileTo(Faction.OfPlayer) &&
                !s.Faction.def.permanentEnemy).OrderBy(s => s.ID).FirstOrDefault()
                ?? throw new InvalidOperationException("No supported ordinary friendly settlement");
            Pawn negotiator = home.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && !p.InMentalState &&
                !p.skills.GetSkill(SkillDefOf.Social).TotallyDisabled && !StatDefOf.TradePriceImprovement.Worker.IsDisabledFor(p) &&
                p.CanTradeWith(target.Faction, target.TraderKind).Accepted)
                .OrderByDescending(p => p.GetStatValue(StatDefOf.TradePriceImprovement)).ThenBy(p => p.thingIDNumber).FirstOrDefault()
                ?? throw new InvalidOperationException("No existing pawn passes native trade permissions/stat/social checks");
            Thing silver = home.listerThings.ThingsOfDef(ThingDefOf.Silver).OrderByDescending(t => t.stackCount).FirstOrDefault();
            int heldSilver = negotiator.inventory.innerContainer.Where(t => t.def == ThingDefOf.Silver).Sum(t => t.stackCount);
            if (heldSilver < 100 && (silver == null || silver.stackCount < 100))
                throw new InvalidOperationException("Require existing test currency; no silver will be manufactured");
            GameDataSaveLoader.SaveGame("RealisticBases-P5-before-trade-spike");
            if (heldSilver < 100)
            {
                Thing currency = silver.SplitOff(Math.Min(500, silver.stackCount));
                if (!negotiator.inventory.innerContainer.TryAdd(currency)) throw new InvalidOperationException("Existing currency transfer failed");
            }
            negotiator.jobs.StopAll();
            if (negotiator.drafter != null) negotiator.drafter.Drafted = false;
            negotiator.DeSpawn();
            Caravan caravan = CaravanMaker.MakeCaravan(new[] { negotiator }, Faction.OfPlayer, target.Tile, true);
            if (!CaravanArrivalAction_Trade.CanTradeWith(caravan, target))
            {
                P0Log.Message("P5_NATIVE_TRADE_SETUP_REJECT", "settlement=" + target.ID + " spawned=" + target.Spawned +
                    " hasMap=" + target.HasMap + " hostile=" + target.Faction.HostileTo(Faction.OfPlayer) +
                    " canTradeNow=" + target.CanTradeNow + " negotiator=" + negotiator.ThingID +
                    " isOwner=" + caravan.IsOwner(negotiator) + " socialDisabled=" + negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled);
                throw new InvalidOperationException("Native ordinary trade permission rejected");
            }
            var initial = P5TradeStock.Acquire(target);
            var holder = P5TradeStock.Holder(target.trader);
            int epoch = P5TradeStock.Record(target.trader).epoch;
            int generationTick = P5TradeStock.NativeTick(target.trader);
            int initialSteel = holder.Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount);
            int playerSteel = negotiator.inventory.innerContainer.Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount);
            long totalSilver = Silver(target, negotiator);
            try
            {
                TradeSession.SetupWith(target, negotiator, false);
                Tradeable buy = TradeSession.deal.AllTradeables.FirstOrDefault(t => t.ThingDef == ThingDefOf.Steel &&
                    t.CountHeldBy(Transactor.Trader) >= 7) ?? throw new InvalidOperationException("Native offer lacks seven steel");
                buy.ForceToSource(7);
                Execute("buy7");
                int afterBuy = holder.Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount);
                if (afterBuy != initialSteel - 7 || Silver(target, negotiator) != totalSilver)
                    throw new InvalidOperationException("Native purchase conservation failed");
                TradeSession.SetupWith(target, negotiator, false);
                Tradeable sell = TradeSession.deal.AllTradeables.First(t => t.ThingDef == ThingDefOf.Steel &&
                    t.CountHeldBy(Transactor.Colony) >= 3);
                sell.ForceToDestination(3);
                Execute("sell3");
                var final = P5TradeStock.Snapshot(target.trader, out var unsupported);
                int finalPlayerSteel = negotiator.inventory.innerContainer.Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount);
                bool passed = final.Count("Steel", RealisticBases.Planning.P5Allocation.Sale) == initialSteel - 4 &&
                    finalPlayerSteel == playerSteel + 4 && Silver(target, negotiator) == totalSilver &&
                    P5TradeStock.Record(target.trader).epoch == epoch && P5TradeStock.NativeTick(target.trader) == generationTick &&
                    holder.All(t => t.holdingOwner == holder && !t.Spawned);
                P0Log.Message("P5_NATIVE_TRADE_SPIKE_COMPLETE", "settlement=" + target.ID + " caravan=" + caravan.ID +
                    " negotiator=" + negotiator.ThingID + " passed=" + passed + " bought=7 sold=3 initialSteel=" + initialSteel +
                    " remainingSteel=" + final.Count("Steel", RealisticBases.Planning.P5Allocation.Sale) +
                    " playerSteelDelta=" + (finalPlayerSteel - playerSteel) + " silverConserved=" + (Silver(target, negotiator) == totalSilver) +
                    " epoch=" + epoch + " initialSupportedStacks=" + initial.Items.Count + " finalSupportedStacks=" + final.Items.Count +
                    " unsupportedNative=" + unsupported.Count + " testCurrencyAdded=0 npcSuppliesAdded=0 attackNotYetTested=True");
                if (!passed) throw new InvalidOperationException("Native partial trade spike failed");
                Audit();
                GameDataSaveLoader.SaveGame("RealisticBases-P5-native-partial-trade");
            }
            finally { TradeSession.Close(); }
        }
        [DebugAction("Realistic Bases P5", "Audit P5 native partial trade result", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void Audit()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("Disposable profile only");
            var targets = Find.WorldObjects.Settlements.Where(s => P5TradeStock.Supported(s) &&
                P5TradeStock.Record(s.trader) != null).ToList();
            if (targets.Count != 1) throw new InvalidOperationException("Require one managed native-trade target");
            Settlement target = targets[0];
            Caravan caravan = Find.WorldObjects.Caravans.Single(c => c.Faction == Faction.OfPlayer && c.Tile == target.Tile);
            var snapshot = P5TradeStock.Snapshot(target.trader, out var unsupported);
            var holder = P5TradeStock.Holder(target.trader);
            var items = CaravanInventoryUtility.AllInventoryItems(caravan).ToList();
            string facts = string.Join("|", snapshot.Items.Select(i => i.Id + ":" + i.Def + ":" + i.Count + ":" +
                i.Stuff + ":" + i.Quality + ":" + i.HitPoints + ":" + i.MaxHitPoints));
            var record = P5TradeStock.Record(target.trader);
            P0Log.Message("P5_NATIVE_TRADE_AUDIT", "settlement=" + target.ID + " caravan=" + caravan.ID +
                " epoch=" + record.epoch + " generated=" + record.generatedTick + " ordinaryStacks=" + snapshot.Items.Count +
                " ordinaryFingerprint=" + RealisticBases.Planning.StableHash.Add(RealisticBases.Planning.StableHash.Start, facts).ToString("X16") +
                " nativeOwnership=" + holder.All(t => t.holdingOwner == holder && t.ParentHolder == target.trader && !t.Spawned) +
                " steel=" + snapshot.Count("Steel", RealisticBases.Planning.P5Allocation.Sale) +
                " playerSteel=" + items.Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount) +
                " totalSilver=" + (holder.Where(t => t.def == ThingDefOf.Silver).Sum(t => t.stackCount) +
                    items.Where(t => t.def == ThingDefOf.Silver).Sum(t => t.stackCount)) +
                " unsupportedNative=" + unsupported.Count + " pawnIds=" + string.Join(",", caravan.PawnsListForReading.Select(p => p.thingIDNumber)) +
                " readOnly=True");
        }

        [DebugAction("Realistic Bases P5", "P5 attack native partial-trade settlement", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void AttackTradedSettlement()
        {
            if (!P4DQuestDebugActions.Disposable || !GeneratorSwitch.Enabled)
                throw new InvalidOperationException("Requires disposable profile and explicitly enabled generator");
            var targets = Find.WorldObjects.Settlements.Where(s => P5TradeStock.Supported(s) && !s.HasMap &&
                P5TradeStock.Record(s.trader) != null).ToList();
            if (targets.Count != 1) throw new InvalidOperationException("Require one previously traded native settlement");
            Settlement target = targets[0];
            Caravan caravan = Find.WorldObjects.Caravans.Single(c => c.Faction == Faction.OfPlayer && c.Tile == target.Tile);
            if (!CaravanArrivalAction_AttackSettlement.CanAttack(caravan, target))
                throw new InvalidOperationException("Native attack authorization rejected");
            Pawn negotiator = caravan.PawnsListForReading.Single(p => p.Faction == Faction.OfPlayer);
            var before = P5TradeStock.Snapshot(target.trader, out _);
            string aggregate = P5PhysicalStockProbe.Aggregate(before);
            int playerSteel = negotiator.inventory.innerContainer.Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount);
            int epoch = P5TradeStock.Record(target.trader).epoch;
            GameDataSaveLoader.SaveGame("RealisticBases-P5-before-native-trade-attack");
            P0Log.Message("P5_NATIVE_TRADE_ATTACK_START", "settlement=" + target.ID + " epoch=" + epoch +
                " offerUnits=" + before.Items.Sum(t => (long)t.Count) + " playerSteel=" + playerSteel + " suppliesAdded=0");
            new CaravanArrivalAction_AttackSettlement(target).Arrived(caravan);
            // ExecuteWhenFinished can run immediately while a newly queued
            // arrival is still waiting. Queue verification behind that arrival.
            LongEventHandler.QueueLongEvent(() =>
            {
                var after = P5TradeStock.Snapshot(target.trader, out _);
                bool passed = target.Map?.GetComponent<RealisticBases.Persistence.P0MapState>().Active == true &&
                    target.Faction.HostileTo(Faction.OfPlayer) && negotiator.Spawned && negotiator.Map == target.Map &&
                    aggregate == P5PhysicalStockProbe.Aggregate(after) && P5TradeStock.Record(target.trader).epoch == epoch &&
                    negotiator.inventory.innerContainer.Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount) == playerSteel;
                P0Log.Message("P5_NATIVE_TRADE_ATTACK_COMPLETE", "settlement=" + target.ID + " map=" + target.Map?.uniqueID +
                    " passed=" + passed + " sameOffer=" + (aggregate == P5PhysicalStockProbe.Aggregate(after)) +
                    " epoch=" + P5TradeStock.Record(target.trader).epoch + " offerUnits=" + after.Items.Sum(t => (long)t.Count) +
                    " saleSteel=" + after.Count("Steel", RealisticBases.Planning.P5Allocation.Sale) +
                    " playerSteel=" + playerSteel + " player=" + negotiator.ThingID + " suppliesAdded=0 nativeHostility=True");
                Find.TickManager.Pause();
                if (target.Map != null) P5DebugActions.AuditPhysical();
                GameDataSaveLoader.SaveGame("RealisticBases-P5-native-trade-attack-result");
            }, "Verifying P5 native trade attack", doAsynchronously: false, null);
        }
        private static long Silver(Settlement settlement, Pawn negotiator) =>
            P5TradeStock.Holder(settlement.trader).Where(t => t.def == ThingDefOf.Silver).Sum(t => (long)t.stackCount) +
            negotiator.inventory.innerContainer.Where(t => t.def == ThingDefOf.Silver).Sum(t => (long)t.stackCount);
        private static void Execute(string phase)
        {
            TradeSession.deal.UpdateCurrencyCount();
            if (!TradeSession.deal.DoesTraderHaveEnoughSilver() || !TradeSession.deal.TryExecute(out bool traded) || !traded)
                throw new InvalidOperationException("Native transaction failed: " + phase);
        }
    }
}
