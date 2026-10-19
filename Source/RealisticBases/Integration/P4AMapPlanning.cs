using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealisticBases.Persistence;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Integration
{
    public sealed class P4AMapPlanningRun
    {
        public P4AMapPlanningRun(ProceduralLayoutInput input, ProceduralLayoutResult result, string profileId,
            P5StockSnapshot saleStock = null, long captureMilliseconds = 0, long planningMilliseconds = 0,
            ProceduralLayoutStageTimings stageTimings = null)
        {
            Input = input;
            Result = result;
            ProfileId = profileId;
            SaleStock = saleStock;
            CaptureMilliseconds = captureMilliseconds;
            PlanningMilliseconds = planningMilliseconds;
            StageTimings = stageTimings ?? result?.StageTimings ?? new ProceduralLayoutStageTimings();
        }

        public ProceduralLayoutInput Input { get; }
        public ProceduralLayoutResult Result { get; }
        public string ProfileId { get; }
        // P5: supported ordinary goods of the settlement's canonical native
        // offer, acquired once before planning and sized into storage. Null
        // when the settlement keeps native-only trade stock.
        public P5StockSnapshot SaleStock { get; }
        public long CaptureMilliseconds { get; }
        public long PlanningMilliseconds { get; }
        public ProceduralLayoutStageTimings StageTimings { get; }
    }

    public static class P4AMapPlanner
    {
        private static TechCatalog cachedCatalog;
        private static float cachedMaintenanceFactor = float.NaN;
        // Developer-action override for one disposable generation only. The
        // faction's persisted stage remains unchanged for ordinary maps.
        public static SettlementTechStageLevel? DebugStageNext;
        public static int? DebugWealthNext;
        public static int? DebugMortarBiasNext;
        public static int? DebugPlannerDegreeNext;

        public static P4AMapPlanningRun Run(Map map, Settlement settlement, int requestedWallLayers)
        {
            if (map == null || settlement?.Faction == null) throw new ArgumentNullException(nameof(settlement));
            Stopwatch captureWatch = Stopwatch.StartNew();
            PhysicalSettlementProfile profile = P2MapPlanner.SelectProfile(settlement.Faction);
            string worldSeed = Find.World?.info?.seedString ?? string.Empty;
            P0WorldState worldState = Current.Game.GetComponent<P0WorldState>();
            SettlementTechStage stage;
            string diagnostic;
            if (!worldState.TryGetOrCreateFactionStage(settlement.Faction, worldSeed, out stage, out diagnostic))
                throw new InvalidOperationException(diagnostic);
            string stageMode = "faction";
            if (DebugStageNext.HasValue)
            {
                stage = TechStageModel.Create(DebugStageNext.Value);
                DebugStageNext = null;
                stageMode = "debug-once";
            }
            ulong seed = worldState.GetOrCreateProceduralSeed(settlement, worldSeed, profile.Id, profile.Version);
            TerrainSnapshot terrain = P1MapPlanner.Capture(map);
            LayoutLocalResources resources = CaptureResources(map);
            LayoutClimate climate = CaptureClimate(map);
            int wealthPercent = worldState.GetOrCreateOrdinaryWealthPercent(settlement, seed);
            if (DebugWealthNext.HasValue)
            {
                if (!RealisticBases.Diagnostics.P4DQuestDebugActions.Disposable)
                    throw new InvalidOperationException("P4E wealth override requires disposable profile");
                wealthPercent = DebugWealthNext.Value;
                DebugWealthNext = null;
                worldState.GetOrCreate(settlement.ID, settlement.Faction).ordinaryWealthPercent = wealthPercent;
                stageMode += "/debug-funded-wealth";
            }
            OrdinarySettlementBudget populationBudget = wealthPercent == 0 ? null :
                OrdinarySettlementPopulation.Create(profile, stage, climate, wealthPercent);
            P5StockSnapshot saleStock = populationBudget == null ? null : P5PhysicalSaleStock.AcquireForGeneration(settlement);
            var retained = populationBudget == null ? new List<Thing>() :
                worldState.FindExisting(settlement.ID).allocatedReserves.ToList();
            int retainedOrdinary = retained.Where(t => t.TryGetComp<CompRottable>() == null)
                .Sum(t => checked((int)(((long)t.stackCount + t.def.stackLimit - 1) / t.def.stackLimit)));
            int retainedPerishable = retained.Where(t => t.TryGetComp<CompRottable>() != null)
                .Sum(t => checked((int)(((long)t.stackCount + t.def.stackLimit - 1) / t.def.stackLimit)));
            // P5.4: keep the revisited layout unless its storage demand grew.
            P0SettlementState planned = populationBudget == null ? null : worldState.FindExisting(settlement.ID);
            LayoutRequirements requirements = LayoutRequirements.FromProfile(profile, climate, stage: stage,
                populationBudget: populationBudget, mortarBiasPercent: DebugMortarBiasNext,
                saleStorageStacks: P5RevisitDemand.Stacks(saleStock == null ? 0 :
                    saleStock.StorageCells(P5StorageKind.Ordinary) + saleStock.StorageCells(P5StorageKind.Hazardous),
                    planned?.plannedSaleStacks ?? -1),
                salePerishableStacks: P5RevisitDemand.Stacks(saleStock?.StorageCells(P5StorageKind.Perishable) ?? 0,
                    planned?.plannedSalePerishableStacks ?? -1),
                retainedStorageStacks: P5RevisitDemand.Stacks(retainedOrdinary, planned?.plannedRetainedStacks ?? -1),
                retainedPerishableStacks: P5RevisitDemand.Stacks(retainedPerishable, planned?.plannedRetainedPerishableStacks ?? -1));
            DebugMortarBiasNext = null;
            int targetLayers = requestedWallLayers >= 1 && requestedWallLayers <= 3 ? requestedWallLayers :
                P4BLayerPolicy.Select(seed, stage.Level, requirements.EquipmentValueLimit,
                    requirements.ImportValueLimit, requirements.Residents);
            float maintenanceFactor = Find.Storyteller?.difficulty?.maintenanceCostFactor ?? 1f;
            if (cachedCatalog == null || cachedMaintenanceFactor != maintenanceFactor)
            {
                cachedCatalog = P4ARuntimeCatalog.Capture();
                cachedMaintenanceFactor = maintenanceFactor;
            }
            TechCatalog catalog = cachedCatalog;
            int plannerDegree = DebugPlannerDegreeNext ?? ProceduralLayoutInput.AutomaticPlannerDegree;
            DebugPlannerDegreeNext = null;
            ProceduralLayoutInput input = null;
            ProceduralLayoutResult result = null;
            int wallLayers = targetLayers;
            string layerFallback = "none";
            captureWatch.Stop();
            Stopwatch planningWatch = Stopwatch.StartNew();
            ProceduralLayoutStageTimings totalStageTimings = new ProceduralLayoutStageTimings();
            for (wallLayers = targetLayers; wallLayers >= 1; wallLayers--)
            {
                input = new ProceduralLayoutInput(requirements, terrain, seed, catalog,
                    new LayoutFortificationReservation(wallLayers), resources: resources,
                    plannerDegree: plannerDegree);
                result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
                AddTimings(totalStageTimings, result.StageTimings);
                if (result.Success && P4BPerimeterDepth.Minimum(terrain, result.Layout) == wallLayers) break;
                if (wallLayers > 1) layerFallback = result.Success ? "insufficient_perimeter_breach_depth" : result.RejectionCode ?? "plan_rejected";
            }
            planningWatch.Stop();
            string terrainStats = result.Success ? string.Empty : " terrainCells=" + terrain.Width * terrain.Height +
                " standable=" + terrain.Points().Count(c => terrain[c].Has(TerrainTraits.Standable)) +
                " buildable=" + terrain.Points().Count(c => terrain[c].Has(TerrainTraits.Buildable)) +
                " clearablePlant=" + terrain.Points().Count(c => terrain[c].Has(TerrainTraits.ClearablePlant)) +
                " plantBlocked=" + terrain.Points().Count(c => terrain[c].Has(TerrainTraits.ClearablePlant) &&
                    !terrain[c].Has(TerrainTraits.Standable)) +
                " occupied=" + terrain.Points().Count(c => terrain[c].Has(TerrainTraits.Occupied)) +
                " naturalRock=" + terrain.Points().Count(c => terrain[c].Has(TerrainTraits.NaturalRock)) +
                " water=" + terrain.Points().Count(c => terrain[c].Has(TerrainTraits.Water));
            P2Log.Message("PROCEDURAL_PLAN", "map=" + map.uniqueID + " profile=" + profile.Id +
                " stage=" + stage.Level + " stageMode=" + stageMode + " seed=" + StableHash.Hex(seed) + " targetLayers=" + targetLayers +
                " residents=" + requirements.Residents + " populationMode=" + (populationBudget == null ? "legacy-profile" : OrdinarySettlementPopulation.Version) +
                " wealthPercent=" + wealthPercent + " equipmentBudget=" + requirements.EquipmentValueLimit +
                " importBudget=" + requirements.ImportValueLimit +
                " baseImportBudget=" + (populationBudget?.BaseImportValue ?? requirements.ImportValueLimit) +
                " defenseAllowancePercent=" + (populationBudget?.DefenseAllowancePercent ?? 100) +
                " defenseReserveWatts=" + requirements.DefenseReserveWatts +
                " engine=" + input.Engine.Id + " saleStacks=" + requirements.SaleStorageStacks + "+" + requirements.SalePerishableStacks +
                " retainedStacks=" + retainedOrdinary + "+" + retainedPerishable +
                " effectiveLayers=" + input.Fortification.WallLayers + " layerFallback=" + layerFallback +
                " layerMode=" + (requestedWallLayers == 0 ? "tech-budget-weighted" : "debug-override") +
                " catalogDefs=" + catalog.Defs.Count + " catalogResearch=" + catalog.Research.Count +
                " wood=" + resources.LocalWoodYield + " stone=" + resources.LocalStoneStuffs.Count +
                " river=" + resources.RiverCells.Count + " geysers=" + resources.SteamGeyserCells.Count +
                " climate=" + climate + " success=" + result.Success + " input=" + input.Digest +
                " layout=" + (result.Layout?.Digest ?? "none") + " stats=" + result.Stats +
                " turret=" + (result.Layout?.Turret?.DefName ?? "none") +
                " turretDecision=" + P1MapPlanner.Compact(result.Diagnostics.Where(d => d.Stage == "turret")
                    .Select(d => d.ToString()).Take(2).ToArray()) +
                terrainStats + " rejectionCode=" + (result.RejectionCode ?? "none") +
                " rejected=" + RejectionSummary(result));
            return new P4AMapPlanningRun(input, result, profile.Id, saleStock,
                captureWatch.ElapsedMilliseconds, planningWatch.ElapsedMilliseconds, totalStageTimings);
        }

        private static void AddTimings(ProceduralLayoutStageTimings total, ProceduralLayoutStageTimings part)
        {
            total.SiteCandidateSearchMs += part.SiteAndCandidateSearchMilliseconds;
            total.FieldWallMs += part.FieldWallMilliseconds;
            total.FitDefenseAccessMs += part.FitDefenseAccessMilliseconds;
            total.ApproachDefenseMs += part.ApproachDefenseMilliseconds;
            total.TurretPlanningMs += part.TurretPlanningMilliseconds;
            total.PowerTrimMs += part.PowerTrimMilliseconds;
            total.ValidationMs += part.ValidationMilliseconds;
            total.ValidationCalls += part.ValidationCallCount;
            total.ScoringMs += part.ScoringMilliseconds;
        }

        public static string RejectionSummary(ProceduralLayoutResult result)
        {
            if (result == null || result.Success) return "none";
            string[] failures = result.Diagnostics
                .Where(d => d.Stage == "attempt" || d.Stage == "result" || d.Stage == "site")
                .Reverse().Take(6).Reverse().Select(d => d.ToString()).ToArray();
            return P1MapPlanner.Compact(failures);
        }

        public static LayoutLocalResources CaptureResources(Map map)
        {
            int wood = 0;
            foreach (Plant plant in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>())
            {
                if (plant.def.plant?.harvestedThingDef != ThingDefOf.WoodLog) continue;
                float yield = Math.Max(0, plant.def.plant.harvestYield);
                wood += (int)Math.Floor(yield * plant.Growth);
            }
            List<string> stones = P2MapPlanner.DiscoverLocalMaterials(map)
                .Where(s => s.StartsWith("Blocks", StringComparison.Ordinal)).ToList();
            List<GridPoint> river = new List<GridPoint>();
            foreach (IntVec3 cell in map.AllCells)
            {
                string name = cell.GetTerrain(map)?.defName ?? string.Empty;
                if (name.IndexOf("WaterMoving", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("River", StringComparison.OrdinalIgnoreCase) >= 0)
                    river.Add(new GridPoint(cell.x, cell.z));
            }
            List<GridPoint> geysers = map.listerThings.AllThings
                .Where(t => t.def?.defName == "SteamGeyser")
                .Select(t => new GridPoint(t.Position.x, t.Position.z)).ToList();
            return new LayoutLocalResources(wood, stones, river, geysers);
        }

        public static LayoutClimate CaptureClimate(Map map)
        {
            // Tile temperature is stable across visits; current weather is not.
            object tile = Find.WorldGrid[map.Tile];
            double average = ReadNumber(tile, "temperature", 18);
            return new LayoutClimate((int)Math.Floor(average - 20), (int)Math.Ceiling(average + 20));
        }

        private static double ReadNumber(object source, string name, double fallback)
        {
            if (source == null) return fallback;
            Type type = source.GetType();
            object value = type.GetField(name)?.GetValue(source) ?? type.GetProperty(name)?.GetValue(source, null);
            return value == null ? fallback : Convert.ToDouble(value);
        }
    }
}
