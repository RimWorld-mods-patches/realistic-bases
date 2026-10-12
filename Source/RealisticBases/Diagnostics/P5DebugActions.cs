using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RealisticBases.Core;
using RealisticBases.Integration;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Diagnostics
{
    public static class P5DebugActions
    {
        private const string Category = "Realistic Bases P5";
        internal static bool FailNextFullStock, ForceNextSaleOverflow;
        [DebugAction(Category, "P5 fail next spawn after full stock transfer", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void ArmFullStockFailure()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("Disposable profile only");
            FailNextFullStock = true;
            P0Log.Message("P5_FULL_STOCK_FAILURE_ARMED", "suppliesAdded=0");
        }
        [DebugAction(Category, "P5 force next sale storage overflow", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void ArmSaleOverflow()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("Disposable profile only");
            ForceNextSaleOverflow = true;
            P0Log.Message("P5_SALE_OVERFLOW_ARMED", "forcedNoSaleSlots=True suppliesAdded=0");
        }
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
            int mappedCount = settlement.Map.GetComponent<P5PhysicalSaleStock>().Goods.Count();
            try
            {
                // Temporarily detach, never clone or destroy. No ticks/save occur
                // while borrowed; finally restores the exact original Things.
                foreach (Thing thing in original) holder.Remove(thing);
                if (tracker.StockListForReading.Count != mappedCount || tracker.StockListForReading.Count != mappedCount ||
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
                " epoch=" + epoch + " tickRestored=" + (now == GenTicks.TicksGame) + " mappedRetained=" + mappedCount + " physicalCopiesAdded=0");
            Audit();
        }
        // Supported ordinary goods of the whole canonical offer (native holder
        // plus physical map objects): def|stuff|quality -> units.
        internal static string OfferFingerprint(Settlement settlement, out long units)
        {
            var snapshot = P5TradeStock.Snapshot(settlement.trader, out _);
            units = snapshot.Items.Sum(i => (long)i.Count);
            return string.Join(",", snapshot.Items.GroupBy(i => i.Def + "|" + i.Stuff + "|q" + i.Quality)
                .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + "=" + g.Sum(i => (long)i.Count)));
        }

        [DebugAction(Category, "Audit P5 physical sale stock and reserves", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void AuditPhysical()
        {
            Settlement settlement = CurrentSettlement();
            Map map = settlement.Map;
            var physical = map.GetComponent<P5PhysicalSaleStock>();
            var holder = P5TradeStock.Holder(settlement.trader);
            var goods = physical.Goods.ToList();
            string fingerprint = OfferFingerprint(settlement, out long units);
            bool duplicate = holder != null && goods.Any(holder.Contains);
            var reserves = map.GetComponent<P5ReserveState>();
            var state = Current.Game.GetComponent<RealisticBases.Persistence.P0WorldState>()?.FindExisting(settlement.ID);
            P0Log.Message("P5_PHYSICAL_AUDIT", "map=" + map.uniqueID + " settlement=" + settlement.ID +
                " physicalActive=" + physical.Active + " physicalStacks=" + goods.Count + " physicalUnits=" + goods.Sum(t => (long)t.stackCount) +
                " spawned=" + goods.Count(t => t.Spawned) + " nativeStacks=" + (holder?.Count ?? 0) +
                " nativeUnits=" + (holder?.Sum(t => (long)t.stackCount) ?? 0) + " offerUnits=" + units + " duplicateIdentity=" + duplicate +
                " fingerprint=" + fingerprint);
            P0Log.Message("P5_RESERVE_AUDIT", "map=" + map.uniqueID + " settlement=" + settlement.ID +
                " current=" + string.Join(",", reserves.Keys.Select(k => k + ":" + reserves.Observe(k))) +
                " ledgerTick=" + (state?.reserveLedgerTick ?? -1) + " ledger=" + (state == null ? "none" :
                    string.Join(",", state.reserveKeys.Zip(state.reserveRemaining, (k, v) => k + ":" + v))));
            var allocated = reserves.Allocated.OrderBy(t => t.thingIDNumber).ToList();
            P0Log.Message("P5_ALLOCATED_AUDIT", "map=" + map.uniqueID + " settlement=" + settlement.ID +
                " mappedStacks=" + allocated.Count + " mappedUnits=" + allocated.Sum(t => t.stackCount) +
                " archivedStacks=" + (state?.allocatedReserves.Count ?? 0) +
                " archivedUnits=" + (state?.allocatedReserves.Sum(t => t.stackCount) ?? 0) +
                " items=" + string.Join("|", allocated.Select(t => t.ThingID + ":" + t.stackCount +
                    ":" + t.Stuff?.defName + ":hp" + t.HitPoints + ":rot" + t.TryGetComp<CompRottable>()?.RotProgress + "@" + t.Position)));
        }

        [DebugAction(Category, "P5 attach existing test visitor", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void AttachVisitor()
        {
            Settlement settlement = CurrentSettlement();
            Map map = settlement.Map;
            if (map.mapPawns.FreeColonistsSpawned.Any()) throw new InvalidOperationException("Test visitor already present");
            Pawn visitor = Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.FreeColonistsSpawned)
                .Where(p => p.HostFaction == null && p.questTags.NullOrEmpty()).OrderBy(p => p.thingIDNumber).FirstOrDefault()
                ?? throw new InvalidOperationException("No existing disposable visitor");
            IntVec3 edge = map.AllCells.Where(c => c.OnEdge(map) && c.Standable(map))
                .OrderByDescending(c => c.DistanceToSquared(map.GetComponent<RealisticBases.AI.P3GarrisonController>().SupportCenter)).First();
            visitor.jobs.StopAll(); visitor.DeSpawn(); GenSpawn.Spawn(visitor, edge, map);
            visitor.drafter.Drafted = true;
            Current.Game.CurrentMap = map;
            P0Log.Message("P5_VISITOR_ATTACHED", "map=" + map.uniqueID + " pawn=" + visitor.ThingID + " cell=" + edge + " pawnsAdded=0");
        }

        [DebugAction(Category, "P5 exhaust original spare barrels", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ExhaustOriginalBarrels()
        {
            Settlement settlement = CurrentSettlement();
            var stocks = settlement.Map.GetComponent<RealisticBases.AI.P3GarrisonController>().P5OwnedThings
                .Where(t => t.def.defName == "ReinforcedBarrel" && P5ReserveState.AllocationFor(t) == null).ToArray();
            int units = stocks.Sum(t => t.stackCount);
            foreach (Thing t in stocks) t.Destroy(DestroyMode.Vanish);
            P0Log.Message("P5_ORIGINAL_BARRELS_EXHAUSTED", "map=" + settlement.Map.uniqueID + " removed=" + units + " suppliesAdded=0");
        }

        [DebugAction(Category, "P5 exhaust operating food for allocation test", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ExhaustFood()
        {
            Settlement settlement = CurrentSettlement();
            var c = settlement.Map.GetComponent<RealisticBases.AI.P3GarrisonController>();
            var food = c.P5OwnedThings.Concat(c.P5Residents.SelectMany(p => p.inventory.innerContainer))
                .Where(t => t.def.category == ThingCategory.Item && t.def.IsNutritionGivingIngestible &&
                    t.def.ingestible.HumanEdible && !t.def.IsDrug && P5PhysicalSaleStock.Tracked(t) == null)
                .Distinct().ToArray();
            int count = food.Sum(t => t.stackCount);
            foreach (Thing t in food) t.Destroy(DestroyMode.Vanish);
            P0Log.Message("P5_TEST_FOOD_EXHAUSTED", "map=" + settlement.Map.uniqueID + " unitsRemoved=" + count + " suppliesAdded=0");
        }

        [DebugAction(Category, "P5 damage allocated ingredient once", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void DamageAllocatedIngredient()
        {
            Settlement settlement = CurrentSettlement();
            Thing ingredient = settlement.Map.GetComponent<P5ReserveState>().Allocated
                .FirstOrDefault(t => t.TryGetComp<CompRottable>() != null) ?? throw new InvalidOperationException("No retained ingredient");
            int before = ingredient.HitPoints;
            ingredient.HitPoints = Math.Max(1, before - 7);
            P0Log.Message("P5_TEST_INGREDIENT_DAMAGED", "map=" + settlement.Map.uniqueID + " thing=" + ingredient.ThingID +
                " beforeHp=" + before + " hp=" + ingredient.HitPoints + " rot=" + ingredient.TryGetComp<CompRottable>().RotProgress);
        }

        [DebugAction(Category, "P5 test emergency transfer rollback", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void EmergencyRollback()
        {
            Settlement settlement = CurrentSettlement();
            var physical = settlement.Map.GetComponent<P5PhysicalSaleStock>();
            var reserves = settlement.Map.GetComponent<P5ReserveState>();
            var controller = settlement.Map.GetComponent<RealisticBases.AI.P3GarrisonController>();
            var saleBefore = physical.Goods.ToDictionary(t => t, t => t.stackCount);
            var allocatedBefore = reserves.Allocated.ToList();
            var ownedBefore = controller.P5OwnedThings.ToList();
            bool forced = false;
            try { physical.EmergencyTransfer(forceRollback: true); }
            catch (InvalidOperationException ex) when (ex.Message == "forced emergency allocation rollback") { forced = true; }
            var saleAfter = physical.Goods.ToDictionary(t => t, t => t.stackCount);
            bool passed = forced && saleBefore.Count == saleAfter.Count &&
                saleBefore.All(p => saleAfter.TryGetValue(p.Key, out int count) && count == p.Value) &&
                allocatedBefore.SequenceEqual(reserves.Allocated) && ownedBefore.SequenceEqual(controller.P5OwnedThings);
            P0Log.Message("P5_EMERGENCY_ROLLBACK_COMPLETE", "map=" + settlement.Map.uniqueID + " passed=" + passed +
                " exactSaleThings=" + saleBefore.Count + " operatingReferencesRestored=True copies=0 suppliesAdded=0");
            if (!passed) throw new InvalidOperationException("Emergency allocation rollback fixture failed");
        }

        [DebugAction(Category, "P5 run emergency allocation transfer now", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void EmergencyTransfer()
        {
            Settlement settlement = CurrentSettlement();
            int moved = settlement.Map.GetComponent<P5PhysicalSaleStock>().EmergencyTransfer();
            P0Log.Message("P5_EMERGENCY_TRANSFER_CHECK", "settlement=" + settlement.ID + " transferred=" + moved);
            AuditPhysical();
        }

        [DebugAction(Category, "P5 test carried medicine protects sale allocation", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void CarriedMedicineGuard()
        {
            Settlement settlement = CurrentSettlement();
            Map map = settlement.Map;
            var physical = map.GetComponent<P5PhysicalSaleStock>();
            Func<Thing, bool> operatingMedicine = t => t != null && !t.Destroyed && t.def.IsMedicine &&
                P5PhysicalSaleStock.Tracked(t) == null;
            int carried = map.GetComponent<RealisticBases.AI.P3GarrisonController>().P5Residents.Sum(p =>
                (p.inventory?.innerContainer.Where(operatingMedicine).Sum(t => t.stackCount) ?? 0) +
                (operatingMedicine(p.carryTracker?.CarriedThing) ? p.carryTracker.CarriedThing.stackCount : 0));
            var before = physical.Goods.Where(t => t.def.IsMedicine).ToDictionary(t => t, t => t.stackCount);
            if (carried <= 0 || before.Count == 0 || map.listerThings.AllThings.Any(t => t.Spawned && operatingMedicine(t)))
                throw new InvalidOperationException("Require existing carried operating medicine, no loose operating medicine, and sale medicine");
            physical.EmergencyTransfer();
            var after = physical.Goods.Where(t => t.def.IsMedicine).ToDictionary(t => t, t => t.stackCount);
            bool passed = before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out int count) && count == p.Value);
            P0Log.Message("P5_CARRIED_RESERVE_GUARD_COMPLETE", "map=" + map.uniqueID + " passed=" + passed +
                " carriedMedicine=" + carried + " saleMedicineBefore=" + before.Values.Sum() +
                " saleMedicineAfter=" + after.Values.Sum() + " suppliesAdded=0 npcOrdersIssued=0");
            if (!passed) throw new InvalidOperationException("Carried operating medicine failed to protect sale allocation");
        }

        // Real native removal (DeinitAndRemoveMap) and regeneration of the same
        // settlement. Disposable profile only; refuses while player pawns are
        // on the map so no player asset is passed to the world by the test.
        [DebugAction(Category, "P5 unload and re-enter current settlement", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void UnloadAndReenter()
            => UnloadAndReenter(true);

        [DebugAction(Category, "P5 unload current settlement only", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void UnloadOnly() => UnloadAndReenter(false);

        private static void UnloadAndReenter(bool reopen)
        {
            Settlement settlement = CurrentSettlement();
            Map map = settlement.Map;
            if (map.mapPawns.FreeColonistsSpawned.Any() || map.mapPawns.AllPawnsSpawned.Any(p => p.Faction != null && p.Faction.IsPlayer))
                throw new InvalidOperationException("Move player pawns off the encounter map first");
            string before = OfferFingerprint(settlement, out long beforeUnits);
            P0Log.Message("P5_REENTRY_BEFORE", "settlement=" + settlement.ID + " map=" + map.uniqueID +
                " offerUnits=" + beforeUnits + " fingerprint=" + before);
            Map home = Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
            Current.Game.DeinitAndRemoveMap(map, false);
            string unloaded = OfferFingerprint(settlement, out long unloadedUnits);
            P0Log.Message("P5_REENTRY_UNLOADED", "settlement=" + settlement.ID + " offerUnits=" + unloadedUnits +
                " sameOffer=" + (unloaded == before) + " hasMap=" + settlement.HasMap);
            if (!reopen)
            {
                if (home != null) Current.Game.CurrentMap = home;
                return;
            }
            Map reopened = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, Find.World.info.initialMapSize, null);
            if (home != null) Current.Game.CurrentMap = home;
            string after = OfferFingerprint(settlement, out long afterUnits);
            P0Log.Message("P5_REENTRY_AFTER", "settlement=" + settlement.ID + " map=" + reopened.uniqueID +
                " offerUnits=" + afterUnits + " sameOffer=" + (after == before) +
                " physicalStacks=" + reopened.GetComponent<P5PhysicalSaleStock>().Goods.Count());
        }

        [DebugAction(Category, "P5 reopen saved allocated-stock settlement", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void ReopenAllocated()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("Disposable profile only");
            var candidates = Find.WorldObjects.Settlements.Where(s => P5TradeStock.Supported(s) && !s.HasMap &&
                Current.Game.GetComponent<RealisticBases.Persistence.P0WorldState>().FindExisting(s.ID)?.allocatedReserves.Count > 0).ToList();
            if (candidates.Count != 1) throw new InvalidOperationException("Require one saved allocated-stock settlement");
            Settlement settlement = candidates[0];
            string before = OfferFingerprint(settlement, out long units);
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, Find.World.info.initialMapSize, null);
            P0Log.Message("P5_REENTRY_AFTER", "settlement=" + settlement.ID + " map=" + map.uniqueID +
                " offerUnits=" + units + " sameOffer=" + (before == OfferFingerprint(settlement, out _)) +
                " physicalStacks=" + map.GetComponent<P5PhysicalSaleStock>().Goods.Count());
            AuditPhysical();
        }

        [DebugAction(Category, "P5 consume one allocated barrel", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ConsumeAllocatedBarrel()
        {
            Settlement settlement = CurrentSettlement();
            Thing barrel = settlement.Map.GetComponent<P5ReserveState>().Allocated
                .FirstOrDefault(t => t.def.defName == "ReinforcedBarrel") ?? throw new InvalidOperationException("No existing allocated barrel");
            int before = barrel.stackCount;
            barrel.SplitOff(1).Destroy(DestroyMode.Vanish);
            P0Log.Message("P5_ALLOCATED_CONSUMED", "map=" + settlement.Map.uniqueID + " original=" + barrel.ThingID +
                " before=" + before + " remaining=" + (barrel.Destroyed ? 0 : barrel.stackCount) + " removed=1 suppliesAdded=0");
        }

        [DebugAction(Category, "Audit P5 native stock ownership", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Audit()
        {
            Settlement settlement = CurrentSettlement();
            var snapshot = P5TradeStock.Snapshot(settlement.trader, out var unsupported);
            var holder = P5TradeStock.Holder(settlement.trader);
            var record = P5TradeStock.Record(settlement.trader);
            bool mapped = settlement.Map.GetComponent<P5PhysicalSaleStock>().Active;
            P0Log.Message("P5_STOCK_AUDIT", "map=" + settlement.Map.uniqueID + " settlement=" + settlement.ID +
                " holder=" + (holder != null) + " epoch=" + (record?.epoch ?? 0) + " status=" + record?.status +
                " generated=" + P5TradeStock.NativeTick(settlement.trader) + " nextRestock=" + settlement.NextRestockTick +
                " stacks=" + snapshot.Items.Count + " units=" + snapshot.Items.Sum(i => (long)i.Count) +
                " nativeOwnership=" + (!mapped && (holder == null || holder.All(t => t.ParentHolder == settlement.trader && !t.Spawned))) +
                " physicalMapping=" + mapped +
                " unsupported=" + string.Join("|", unsupported) + " items=" + string.Join("|", snapshot.Items.Select(i =>
                    i.Id + ":" + i.Def + ":" + i.Count + ":" + i.Stuff + ":q" + i.Quality + ":hp" + i.HitPoints + "/" + i.MaxHitPoints)));
        }
    }
}
