using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RealisticBases.AI;
using RealisticBases.Core;
using RealisticBases.Diagnostics;
using RealisticBases.Generation;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RealisticBases.Integration
{
    // Generation is synchronous. Hold signal publication only inside the quest
    // mutation boundary; failed provisional spawn/despawn never reaches quests.
    public sealed class P4DProvisionalSignals : IDisposable
    {
        private static P4DProvisionalSignals active;
        private readonly List<Signal> pending = new List<Signal>();
        public P4DProvisionalSignals()
        {
            if (active != null) throw new InvalidOperationException("Nested quest mutation boundary");
            active = this;
        }
        public static bool PublishOrDefer(Signal signal)
        {
            if (active == null) return true;
            active.pending.Add(signal);
            return false;
        }
        public void Commit()
        {
            active = null;
            foreach (Signal signal in pending) Find.SignalManager.SendSignal(signal);
            pending.Clear();
        }
        public void Dispose() { if (active == this) active = null; pending.Clear(); }
    }

    [HarmonyPatch(typeof(SignalManager), nameof(SignalManager.SendSignal))]
    internal static class P4DProvisionalSignalPatch
    {
        private static bool Prefix(Signal signal) => P4DProvisionalSignals.PublishOrDefer(signal);
    }

    public enum P4DForcedFailure { None, BeforeMutation, AfterBase, AfterTransfer }

    public sealed class P4DQuestSpawnContext : IThingHolder
    {
        private readonly ThingOwner prepared;
        public P4DQuestSpawnContext(Map map, Site site, SitePart force)
        {
            Map = map; Site = site; Force = force;
            prepared = new ThingOwner<Thing>(this);
        }
        public Map Map { get; }
        public Site Site { get; }
        public SitePart Force { get; }
        public List<Pawn> Roster { get; } = new List<Pawn>();
        public List<Thing> Loot { get; } = new List<Thing>();
        public SitePart PrisonerPart { get; private set; }
        public Pawn Prisoner { get; private set; }
        public CellRect PrisonBounds { get; private set; }
        public P4DQuestContract Contract { get; private set; }
        public bool Committed { get; private set; }
        public bool NativeFallback { get; set; }
        public bool RecoveryVerified { get; set; } = true;
        public bool RosterPrepared { get; private set; }
        public bool LootPrepared { get; private set; }
        private bool rosterReleased, lootReleased;
        public IThingHolder ParentHolder => null;
        public ThingOwner GetDirectlyHeldThings() => prepared;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, prepared);

        public void Prepare()
        {
            // The native group kind/points/inhabitants/seed own the entire roster.
            // A purpose scope isolates layout and loot RNG from its generation.
            Rand.PushState(Gen.HashCombineInt(Force.parms.randomValue, Site.ID));
            try
            {
                foreach (Pawn pawn in PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
                {
                    tile = Map.Tile, faction = Site.Faction, groupKind = PawnGroupKindDefOf.Settlement,
                    points = Force.parms.threatPoints, inhabitants = true,
                    seed = OutpostSitePartUtility.GetPawnGroupMakerSeed(Force.parms)
                }))
                {
                    Roster.Add(pawn);
                    if (!prepared.TryAdd(pawn, false)) throw new InvalidOperationException("Cannot stage native roster");
                }
                RosterPrepared = true;
                if (Force.parms.lootMarketValue > 0)
                {
                    Loot.AddRange(ThingSetMakerDefOf.MapGen_DefaultStockpile.root.Generate(new ThingSetMakerParams
                    {
                        countRange = new IntRange(3, 10), techLevel = Site.Faction.def.techLevel,
                        makingFaction = Site.Faction,
                        totalMarketValueRange = new FloatRange(Force.parms.lootMarketValue, Force.parms.lootMarketValue)
                    }));
                    foreach (Thing item in Loot)
                        if (!prepared.TryAdd(item, false)) throw new InvalidOperationException("Cannot stage native loot");
                }
                LootPrepared = true;
            }
            finally { Rand.PopState(); }
            PrisonerPart = Site.parts.FirstOrDefault(p => p.def.defName == "PrisonerWillingToJoin");
            Prisoner = PrisonerPart?.things?.OfType<Pawn>().SingleOrDefault();
            QuestSiteFacts facts = new QuestSiteFacts(Site.ID, Site.Faction.def.defName, Map.generatorDef.defName,
                Site.parts.Select(p => new QuestPartFacts(p.def.defName, p.hidden)), Force.parms.threatPoints,
                Force.parms.randomValue, Force.parms.lootMarketValue, Force.expectedEnemyCount,
                Roster.Select(Describe), Loot.Count, Prisoner?.ThingID, Prisoner?.holdingOwner == PrisonerPart?.things);
            if (!P4DQuestContract.TryCreate(facts, out P4DQuestContract contract, out string reason))
                throw new InvalidOperationException(reason);
            if (Roster.Any(p => p.IsPrisoner || p.IsSlave || p.Faction != Site.Faction || !p.RaceProps.Humanlike ||
                !p.questTags.NullOrEmpty())) throw new InvalidOperationException("unsupported_scripted_native_occupant");
            Contract = contract;
        }

        private void PlacePrisoner(P4SpawnTransaction tx, ProceduralLayout layout)
        {
            if (Contract.Strategy.PrisonerCapacity == 0) return;
            if (Prisoner == null || Prisoner.holdingOwner != PrisonerPart.things || !Prisoner.IsPrisoner ||
                Prisoner.Faction != Site.Faction || Prisoner.guest.HostFaction != Site.Faction || Prisoner.GetLord() != null)
                throw new InvalidOperationException("unsupported_original_prisoner_custody");
            var jail = layout.Rooms.Single(r => r.Kind == LayoutRoomKind.Jail);
            PrisonBounds = new CellRect(jail.Bounds.MinX, jail.Bounds.MinZ, jail.Bounds.Width, jail.Bounds.Height);
            var bed = Map.listerThings.AllThings.OfType<Building_Bed>().Single(b => b.ForPrisoners && PrisonBounds.Contains(b.Position));
            IntVec3 cell = jail.Interior.Points().Select(Cell).FirstOrDefault(c => c.Standable(Map) && c.GetEdifice(Map) == null);
            if (!cell.IsValid || !PrisonBounds.ContractedBy(1).Contains(cell) ||
                !Map.reachability.CanReachMapEdge(cell, TraverseParms.For(TraverseMode.PassDoors)))
                throw new InvalidOperationException("unreachable_planned_prison");
            Faction faction = Prisoner.Faction, host = Prisoner.guest.HostFaction;
            float health = Prisoner.health.summaryHealth.SummaryHealthPercent;
            string name = Prisoner.Name.ToStringFull, tags = string.Join(",", Prisoner.questTags ?? new List<string>());
            tx.Borrow(Prisoner, PrisonerPart.things);
            GenSpawn.Spawn(Prisoner, cell, Map);
            Prisoner.mindState.WillJoinColonyIfRescued = true;
            if (!Prisoner.IsPrisoner || Prisoner.Faction != faction || Prisoner.guest.HostFaction != host ||
                Prisoner.health.summaryHealth.SummaryHealthPercent != health || Prisoner.Name.ToStringFull != name ||
                string.Join(",", Prisoner.questTags ?? new List<string>()) != tags || Prisoner.GetLord() != null ||
                bed.GetRoom() == null || bed.GetRoom().TouchesMapEdge || bed.GetRoom().OpenRoofCount != 0 ||
                Prisoner.GetRoom() != bed.GetRoom()) throw new InvalidOperationException("original_prisoner_state_changed");
            tx.OnRollback(() => P4DQuestDebugActions.Log("PRISONER_ROLLBACK_PENDING", "site=" + Site.ID +
                " pawn=" + Prisoner.ThingID + " returnOwner=originalSitePart"));
            P4DQuestDebugActions.Log("PRISONER_PLACED", "site=" + Site.ID + " pawn=" + Prisoner.ThingID +
                " name=" + name + " health=" + health + " host=" + host.def.defName + " room=" + PrisonBounds +
                " bed=" + bed.ThingID + " holderEmpty=" + (PrisonerPart.things.Count == 0) + " p3Controlled=false tags=" + tags);
        }

        private static QuestResidentFacts Describe(Pawn pawn)
        {
            Thing weapon = pawn.equipment?.Primary;
            LoadoutRole role = weapon == null ? LoadoutRole.Support : weapon.def.IsMeleeWeapon
                ? LoadoutRole.MeleeReserve : (weapon.def.Verbs.FirstOrDefault()?.range ?? 0) >= 40
                    ? LoadoutRole.Marksman : LoadoutRole.RangedLine;
            return new QuestResidentFacts(pawn.ThingID, pawn.kindDef.defName, role, Item(weapon),
                pawn.apparel.WornApparel.Select(Item));
        }

        private static LoadoutItem Item(Thing thing)
        {
            if (thing == null) return null;
            thing.TryGetQuality(out QualityCategory quality);
            return new LoadoutItem(thing.def.defName, thing.Stuff?.defName, (LoadoutQuality)(int)quality,
                thing.stackCount, (int)Math.Ceiling(thing.MarketValue));
        }

        public void SpawnRoster(P4SpawnTransaction tx, IReadOnlyList<GridPoint> homes)
        {
            if (homes.Count < Roster.Count) throw new InvalidOperationException("native_roster_spawn_capacity");
            for (int i = 0; i < Roster.Count; i++)
            {
                tx.Borrow(Roster[i], prepared);
                GenSpawn.Spawn(Roster[i], Cell(homes[i]), Map);
            }
        }

        public void PlaceLoot(P4SpawnTransaction tx, IReadOnlyList<GridPoint> storage)
        {
            foreach (Thing item in Loot)
            {
                GridPoint? selected = storage.Where(c => Cell(c).Standable(Map) &&
                    !Cell(c).GetThingList(Map).Any(t => t.def.category == ThingCategory.Item)).Cast<GridPoint?>().FirstOrDefault();
                if (!selected.HasValue) throw new InvalidOperationException("native_loot_stock_capacity");
                tx.Borrow(item, prepared);
                GenSpawn.Spawn(item, Cell(selected.Value), Map);
            }
        }

        public void BeforeCommit(P4SpawnTransaction tx, ProceduralLayout layout, string finish,
            EncounterPlan encounter, IEnumerable<Thing> combatStocks, int startupFuel)
        {
            if (P4DQuestGeneration.ForcedFailure == P4DForcedFailure.AfterBase)
                throw new InvalidOperationException("forced_quest_failure_after_base");
            double operatingValue = tx.OwnedThings.Where(t => t.def.category == ThingCategory.Item)
                .Sum(t => t.MarketValue * t.stackCount) + startupFuel *
                (layout.Power == null ? 0 : DefDatabase<ThingDef>.GetNamedSilentFail(layout.Power.FuelDef)?.BaseMarketValue ?? 0);
            if (operatingValue > Contract.OperatingValueAllowance + 0.01)
                throw new InvalidOperationException("quest_operating_allowance:" + operatingValue + "/" + Contract.OperatingValueAllowance);
            PlacePrisoner(tx, layout);
            PlaceLoot(tx, LayoutStockCells.ForRooms(layout.Rooms, LayoutRoomKind.Storage));
            P3GarrisonController controller = Map.GetComponent<P3GarrisonController>();
            Lord lord = null;
            tx.OnRollback(() =>
            {
                if (lord != null) Map.lordManager.RemoveLord(lord);
                controller.ClearProvisionalQuest();
                Map.GetComponent<P4DMapState>().Clear();
            });
            controller.Initialize(Site, layout.Gates.Select(g => g.Cell)
                .Concat(layout.ApproachDefense?.SallyPorts.Select(p => p.Cell) ?? new GridPoint[0]),
                encounter, Roster, tx.OwnedThings.Concat(Roster).Concat(Loot), null, combatStocks,
                P4ProceduralSpawner.PerimeterSegments(layout, encounter.SupportCenter),
                layout.Enclosure, compoundRegion: layout.Enclosure);
            lord = LordMaker.MakeNewLord(Site.Faction, new LordJob_P3Garrison(Site.ID), Map, Roster);
            // Metadata is part of provisional ownership, never ordinary wealth state.
            var data = AccessTools.Field(typeof(MapGenerator), "data").GetValue(null) as Dictionary<string, object>;
            bool hadRects = data.ContainsKey("UsedRects"), hadInterest = data.TryGetValue("RectOfInterest", out object oldInterest);
            List<CellRect> used = MapGenerator.UsedRects;
            var oldRects = used.ToList();
            int rootsCount = MapGenerator.rootsToUnfog.Count;
            tx.OnRollback(() =>
            {
                used.Clear(); used.AddRange(oldRects);
                if (!hadRects) data.Remove("UsedRects");
                if (hadInterest) data["RectOfInterest"] = oldInterest; else data.Remove("RectOfInterest");
                MapGenerator.rootsToUnfog.RemoveRange(rootsCount, MapGenerator.rootsToUnfog.Count - rootsCount);
                P4DQuestDebugActions.Log("QUEST_METADATA_RESTORED", "site=" + Site.ID +
                    " usedRects=" + used.Count + " roots=" + MapGenerator.rootsToUnfog.Count +
                    " hadInterest=" + hadInterest);
            });
            GridPoint[] footprint = layout.Enclosure.Concat(layout.BuiltWalls).Concat(layout.NaturalWalls)
                .Concat(layout.ApproachDefense?.TransitCells ?? new GridPoint[0])
                .Concat(layout.ApproachDefense?.CoverCells ?? new GridPoint[0]).ToArray();
            CellRect rect = new CellRect(footprint.Min(c => c.X), footprint.Min(c => c.Z),
                footprint.Max(c => c.X) - footprint.Min(c => c.X) + 1,
                footprint.Max(c => c.Z) - footprint.Min(c => c.Z) + 1);
            used.Add(rect);
            MapGenerator.SetVar("RectOfInterest", rect);
            if (Prisoner != null) MapGenerator.rootsToUnfog.Add(Prisoner.Position);
            Map.GetComponent<P4DMapState>().Initialize(Site, Contract, layout.Digest, finish, Roster, Loot, Prisoner);
            if (P4DQuestGeneration.ForcedFailure == P4DForcedFailure.AfterTransfer)
                throw new InvalidOperationException("forced_quest_failure_after_transfer_metadata");
            P4DQuestDebugActions.Log("QUEST_CONTENT_LEDGER", "site=" + Site.ID + " defenders=" + Roster.Count +
                " threat=" + Force.parms.threatPoints + " groupSeed=" + Force.parms.randomValue +
                " staticThreat=0 gearValue=" + Contract.EquipmentValue + " operating=" + operatingValue +
                "/" + Contract.OperatingValueAllowance + " lootAllowance=" + Force.parms.lootMarketValue +
                " lootValue=" + Loot.Sum(t => t.MarketValue * t.stackCount) + " lootIDs=" + string.Join(",", Loot.Select(t => t.ThingID)));
        }

        public void MarkCommitted() { Committed = true; }
        public IEnumerable<Pawn> NativeRoster()
        {
            if (rosterReleased) throw new InvalidOperationException("Native quest roster consumed twice");
            rosterReleased = true;
            foreach (Pawn pawn in Roster) { prepared.Remove(pawn); yield return pawn; }
        }
        public List<Thing> NativeLoot()
        {
            if (lootReleased) throw new InvalidOperationException("Native quest loot consumed twice");
            lootReleased = true;
            foreach (Thing item in Loot) prepared.Remove(item);
            return Loot;
        }
        private static IntVec3 Cell(GridPoint p) => new IntVec3(p.X, 0, p.Z);
    }

    public static class P4DQuestGeneration
    {
        private static readonly ConditionalWeakTable<Map, P4DQuestSpawnContext> Runs = new ConditionalWeakTable<Map, P4DQuestSpawnContext>();
        private static readonly ConditionalWeakTable<Map, List<GenStepWithParams>> Steps = new ConditionalWeakTable<Map, List<GenStepWithParams>>();
        internal static P4DQuestSpawnContext NativeActive;
        internal static Site CaravanEntrySite;
        public static P4DForcedFailure ForcedFailure;

        internal static void Capture(Map map, IEnumerable<GenStepWithParams> steps)
        { Steps.Remove(map); Steps.Add(map, steps.ToList()); }

        internal static bool TryGenerate(GenStep_Outpost step, Map map, GenStepParams parms)
        {
            if (!Eligible(step, map, parms, out string reason))
            {
                if (map.Parent is Site) P4DQuestDebugActions.Log("QUEST_NATIVE", "site=" + map.Parent.ID + " reason=" + reason);
                return true;
            }
            if (ForcedFailure == P4DForcedFailure.BeforeMutation)
            {
                ForcedFailure = P4DForcedFailure.None;
                P4DQuestDebugActions.Log("QUEST_PREFLIGHT_REJECT", "site=" + map.Parent.ID + " mutationCount=0 nativeRuns=1");
                return true;
            }
            var context = new P4DQuestSpawnContext(map, (Site)map.Parent, parms.sitePart);
            Runs.Add(map, context);
            try
            {
                context.Prepare();
                var stage = TechStageModel.Create(map.ParentFaction.def.techLevel >= TechLevel.Industrial
                    ? SettlementTechStageLevel.IndustrialMid : SettlementTechStageLevel.Neolithic);
                var input = new ProceduralLayoutInput(context.Contract.Requirements(stage, P4AMapPlanner.CaptureClimate(map)),
                    P1MapPlanner.Capture(map), context.Contract.Seed, P4ARuntimeCatalog.Capture(),
                    resources: P4AMapPlanner.CaptureResources(map), questContract: context.Contract);
                var result = ProceduralLayoutPlanner.Plan(input);
                if (!result.Success) throw new InvalidOperationException("quest_layout:" + P4AMapPlanner.RejectionSummary(result));
                if (!P4ProceduralSpawner.TrySpawn(map, context.Site, new P4AMapPlanningRun(input, result, context.Contract.Strategy.Id),
                    out string failure, quest: context)) throw new InvalidOperationException(failure);
                P4DQuestDebugActions.Log("QUEST_COMMIT", "site=" + context.Site.ID + " strategy=" + context.Contract.Strategy.Id +
                    " layout=" + result.Layout.Digest + " defenders=" + context.Roster.Count + " nativeOutpostSuppressed=true");
                return false;
            }
            catch (Exception ex)
            {
                if (!context.RosterPrepared || !context.LootPrepared || context.Committed || !context.RecoveryVerified || context.Roster.Any(p => p.Spawned || p.Destroyed || p.holdingOwner != context.GetDirectlyHeldThings()) ||
                    context.Loot.Any(t => t.Spawned || t.Destroyed || t.holdingOwner != context.GetDirectlyHeldThings()) ||
                    (context.Prisoner != null && (context.Prisoner.Spawned || context.Prisoner.Destroyed || context.Prisoner.holdingOwner != context.PrisonerPart.things)))
                    throw new InvalidOperationException("Unsafe quest rollback; native fallback disabled", ex);
                if (context.Prisoner != null) P4DQuestDebugActions.Log("PRISONER_RETURNED", "site=" + context.Site.ID +
                    " pawn=" + context.Prisoner.ThingID + " originalOwner=true join=" + context.Prisoner.mindState.WillJoinColonyIfRescued +
                    " health=" + context.Prisoner.health.summaryHealth.SummaryHealthPercent);
                context.NativeFallback = true;
                NativeActive = context;
                P4DQuestDebugActions.Log("QUEST_FALLBACK", "site=" + context.Site.ID + " reason=" + ex.Message +
                    " preparedRoster=" + context.Roster.Count + " nativeRuns=1");
                return true;
            }
            finally { ForcedFailure = P4DForcedFailure.None; }
        }

        private static bool Eligible(GenStep_Outpost step, Map map, GenStepParams parms, out string reason)
        {
            reason = "unsupported_contract";
            if (!RealisticBasesMod.Settings.enableP0Generator) { reason = "disabled"; return false; }
            if (map.Parent?.GetType() != typeof(Site) || !(map.Parent is Site site) || CaravanEntrySite != site ||
                map.generatorDef != MapGeneratorDefOf.Encounter || map.wasSpawnedViaGravShipLanding ||
                map.TileInfo.Layer.Def.isSpace || parms.gravship != null || site.Faction == null ||
                !site.Faction.HostileTo(Faction.OfPlayer) ||
                !new[] { "Pirate", "OutlanderRough", "TribeRough", "TribeSavage" }.Contains(site.Faction.def.defName) ||
                site.parts.Any(p => p.hidden || p.def.modContentPack?.PackageIdPlayerFacing != "Ludeon.RimWorld")) return false;
            bool bandit = site.parts.Select(p => p.def.defName).SequenceEqual(new[] { "BanditCamp" });
            bool rescue = site.parts.Select(p => p.def.defName).SequenceEqual(new[]
                { "PrisonerWillingToJoin", "Outpost", "PossibleUnknownThreatMarker" });
            if (!(bandit || rescue) || site.parts[bandit ? 0 : 1] != parms.sitePart || site.def.mapGenerator != null ||
                !Find.QuestManager.QuestsListForReading.Any(q => q.State == QuestState.Ongoing &&
                    q.root?.defName == (bandit ? "OpportunitySite_BanditCamp" : "OpportunitySite_PrisonerWillingToJoin") && q.PartsListForReading.OfType<QuestPart_SpawnWorldObject>()
                        .Any(p => p.worldObject == site))) return false;
            if (rescue && (site.parts[0].def.Worker.GetType() != typeof(SitePartWorker_PrisonerWillingToJoin) ||
                site.parts[0].things?.Count != 1 || !(site.parts[0].things[0] is Pawn original) || !original.IsPrisoner ||
                site.parts[0].def.ExtraGenSteps.Count != 1 || site.parts[0].def.ExtraGenSteps[0].genStep.GetType() != typeof(GenStep_PrisonerWillingToJoin) ||
                site.parts[2].def.Worker.GetType() != typeof(SitePartWorker_UnknownThreatMarker) ||
                site.parts[2].def.ExtraGenSteps.Any() || site.parts[2].things?.Any == true)) return false;
            if (step.GetType() != typeof(GenStep_Outpost) || step.overrideFaction != null || step.forcedRect != null ||
                step.settlementDontGeneratePawns || !step.generateLoot || step.unfogged || step.attackWhenPlayerBecameEnemy ||
                step.requiredGravcoreRooms != 0 || step.requiredWorshippedTerminalRooms != 0 ||
                step.postProcessSettlementParams != null || step.pawnGroupKindDef != null) return false;
            if (!Steps.TryGetValue(map, out var actual)) return false;
            var expected = map.generatorDef.genSteps.Concat(site.parts.SelectMany(p => p.def.ExtraGenSteps)).ToList();
            if (actual.Count != expected.Count || expected.Distinct().Any(d =>
                    actual.Count(s => s.def == d) != expected.Count(e => e == d)) ||
                actual.Count(s => s.def.genStep is GenStep_Outpost) != 1 ||
                actual.Count(s => s.def.genStep is GenStep_Power) != 1 ||
                parms.sitePart.def.Worker.GetType() != typeof(SitePartWorker_Outpost) ||
                parms.sitePart.def.ExtraGenSteps.Any(s => s.genStep.GetType() != typeof(GenStep_Outpost) &&
                    s.genStep.GetType() != typeof(GenStep_Power))) return false;
            reason = null; return true;
        }

        internal static bool Committed(Map map) => Runs.TryGetValue(map, out var c) && c.Committed;
        internal static bool FinishRescue(Map map, GenStepParams parms)
        {
            if (!Runs.TryGetValue(map, out var c) || !c.Committed || c.PrisonerPart != parms.sitePart) return true;
            if (c.Prisoner == null || !c.Prisoner.Spawned || c.Prisoner.Map != map || c.Prisoner.GetLord() != null)
                throw new InvalidOperationException("Committed quest target missing before native prison step");
            // Native prison scatterer runs after Fog1500; keep its exact fog and
            // interest obligations but do not generate a second prison/target.
            MapGenerator.SetVar("RectOfInterest", c.PrisonBounds);
            map.fogGrid.Refog(c.PrisonBounds.ContractedBy(1));
            P4DQuestDebugActions.Log("PRISON_STEP_SUPPRESSED", "site=" + c.Site.ID +
                " pawn=" + c.Prisoner.ThingID + " nativeFogApplied=true duplicatePrisons=0");
            return false;
        }
        internal static void FinishNative(Map map)
        {
            if (NativeActive?.Map != map) return;
            var c = NativeActive;
            NativeActive = null;
            P4DQuestDebugActions.Log("QUEST_NATIVE_FALLBACK_COMPLETE", "site=" + c.Site.ID +
                " rosterSpawned=" + c.Roster.Count(p => p.Spawned && p.Map == map) + "/" + c.Roster.Count +
                " preparedRemaining=" + c.GetDirectlyHeldThings().Count);
        }
    }

    // Arrived queues a long event, so scope the synchronous private DoEnter,
    // not Arrived itself. Pods, transporters and debug-map generation stay native.
    [HarmonyPatch(typeof(CaravanArrivalAction_VisitSite), "DoEnter")]
    internal static class P4DCaravanEntryPatch
    {
        private static void Prefix(Site site, ref Site __state)
        { __state = P4DQuestGeneration.CaravanEntrySite; P4DQuestGeneration.CaravanEntrySite = site; }
        private static Exception Finalizer(Exception __exception, Site __state)
        { P4DQuestGeneration.CaravanEntrySite = __state; return __exception; }
    }

    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateContentsIntoMap))]
    internal static class P4DContentsInventory
    {
        private static void Prefix(IEnumerable<GenStepWithParams> genStepDefs, Map map) => P4DQuestGeneration.Capture(map, genStepDefs);
    }
    [HarmonyPatch(typeof(GenStep_Outpost), nameof(GenStep_Outpost.Generate))]
    internal static class P4DOutpostPatch
    {
        private static bool Prefix(GenStep_Outpost __instance, Map map, GenStepParams parms) => P4DQuestGeneration.TryGenerate(__instance, map, parms);
        private static void Postfix(Map map) => P4DQuestGeneration.FinishNative(map);
        private static Exception Finalizer(Exception __exception, Map map)
        { P4DQuestGeneration.FinishNative(map); return __exception; }
    }
    [HarmonyPatch(typeof(GenStep_Power), nameof(GenStep_Power.Generate))]
    internal static class P4DNativePowerPatch
    {
        private static bool Prefix(Map map, GenStepParams parms) => !P4DQuestGeneration.Committed(map);
    }
    [HarmonyPatch(typeof(GenStep_Scatterer), nameof(GenStep_Scatterer.Generate))]
    internal static class P4DNativePrisonPatch
    {
        private static bool Prefix(GenStep __instance, Map map, GenStepParams parms) =>
            __instance.GetType() != typeof(GenStep_PrisonerWillingToJoin) || P4DQuestGeneration.FinishRescue(map, parms);
    }
    [HarmonyPatch(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.GeneratePawns))]
    internal static class P4DFallbackRosterPatch
    {
        private static bool Prefix(PawnGroupMakerParms parms, ref IEnumerable<Pawn> __result)
        {
            var c = P4DQuestGeneration.NativeActive;
            if (c == null || parms.faction != c.Site.Faction || parms.groupKind != PawnGroupKindDefOf.Settlement ||
                parms.seed != OutpostSitePartUtility.GetPawnGroupMakerSeed(c.Force.parms) || !c.RosterPrepared) return true;
            __result = c.NativeRoster(); return false;
        }
    }
    [HarmonyPatch(typeof(SymbolResolver_LootScatter), nameof(SymbolResolver_LootScatter.Resolve))]
    internal static class P4DFallbackLootPatch
    {
        private static void Prefix(ref ResolveParams rp)
        {
            var c = P4DQuestGeneration.NativeActive;
            if (c != null && c.LootPrepared && BaseGen.globalSettings.map == c.Map && rp.faction == c.Site.Faction)
                rp.lootConcereteContents = c.NativeLoot();
        }
    }

    public sealed class P4DMapState : MapComponent
    {
        private Site site;
        private string strategy, contract, seed, layout, finish;
        private List<Pawn> roster = new List<Pawn>();
        private List<Thing> loot = new List<Thing>();
        private Pawn prisoner;
        public Pawn OriginalPrisoner => prisoner;
        public P4DMapState(Map map) : base(map) { }
        public void Initialize(Site parent, P4DQuestContract input, string geometry, string finishes,
            IEnumerable<Pawn> occupants, IEnumerable<Thing> items, Pawn target = null)
        {
            site = parent; strategy = input.Strategy.Id; contract = input.Digest;
            seed = StableHash.Hex(input.Seed); layout = geometry; finish = finishes;
            roster = occupants.ToList(); loot = items.ToList(); prisoner = target;
        }
        public void Clear() { site = null; prisoner = null; strategy = contract = seed = layout = finish = null; roster.Clear(); loot.Clear(); }
        public override void ExposeData()
        {
            Scribe_References.Look(ref site, "p4dSite");
            Scribe_References.Look(ref prisoner, "p4dPrisoner");
            Scribe_Values.Look(ref strategy, "p4dStrategy"); Scribe_Values.Look(ref contract, "p4dContract");
            Scribe_Values.Look(ref seed, "p4dSeed"); Scribe_Values.Look(ref layout, "p4dLayout");
            Scribe_Values.Look(ref finish, "p4dFinish");
            Scribe_Collections.Look(ref roster, "p4dRoster", LookMode.Reference);
            Scribe_Collections.Look(ref loot, "p4dLoot", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                roster = roster ?? new List<Pawn>();
                loot = loot ?? new List<Thing>();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && site != null)
                P4DQuestDebugActions.Log("QUEST_RELOADED", "site=" + site.ID + " strategy=" + strategy +
                    " contract=" + contract + " seed=" + seed + " layout=" + layout + " finish=" + finish +
                    " roster=" + string.Join(",", roster.Where(p => p != null).Select(p => p.ThingID)) +
                    " loot=" + string.Join(",", loot.Where(t => t != null).Select(t => t.ThingID)) + " prisoner=" + prisoner?.ThingID);
        }
    }
}
