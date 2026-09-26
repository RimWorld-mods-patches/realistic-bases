using System;
using System.Collections.Generic;
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
        public P4AMapPlanningRun(ProceduralLayoutInput input, ProceduralLayoutResult result, string profileId)
        { Input = input; Result = result; ProfileId = profileId; }

        public ProceduralLayoutInput Input { get; }
        public ProceduralLayoutResult Result { get; }
        public string ProfileId { get; }
    }

    public static class P4AMapPlanner
    {
        private static TechCatalog cachedCatalog;

        public static P4AMapPlanningRun Run(Map map, Settlement settlement, int requestedWallLayers)
        {
            if (map == null || settlement?.Faction == null) throw new ArgumentNullException(nameof(settlement));
            PhysicalSettlementProfile profile = P2MapPlanner.SelectProfile(settlement.Faction);
            string worldSeed = Find.World?.info?.seedString ?? string.Empty;
            P0WorldState worldState = Current.Game.GetComponent<P0WorldState>();
            SettlementTechStage stage;
            string diagnostic;
            if (!worldState.TryGetOrCreateFactionStage(settlement.Faction, worldSeed, out stage, out diagnostic))
                throw new InvalidOperationException(diagnostic);
            ulong seed = worldState.GetOrCreateProceduralSeed(settlement, worldSeed, profile.Id, profile.Version);
            TerrainSnapshot terrain = P1MapPlanner.Capture(map);
            LayoutLocalResources resources = CaptureResources(map);
            LayoutClimate climate = CaptureClimate(map);
            LayoutRequirements requirements = LayoutRequirements.FromProfile(profile, climate, stage: stage);
            int targetLayers = requestedWallLayers >= 1 && requestedWallLayers <= 3 ? requestedWallLayers :
                P4BLayerPolicy.Select(seed, stage.Level, requirements.EquipmentValueLimit,
                    requirements.ImportValueLimit, requirements.Residents);
            TechCatalog catalog = cachedCatalog ?? (cachedCatalog = P4ARuntimeCatalog.Capture());
            ProceduralLayoutInput input = null;
            ProceduralLayoutResult result = null;
            int wallLayers = targetLayers;
            string layerFallback = "none";
            for (; wallLayers >= 1; wallLayers--)
            {
                input = new ProceduralLayoutInput(requirements, terrain, seed, catalog,
                    new LayoutFortificationReservation(wallLayers), resources: resources);
                result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
                if (result.Success && (wallLayers == 1 || result.Layout.OuterSections.Any(s => s.Layer == wallLayers - 1))) break;
                if (wallLayers > 1) layerFallback = result.Success ? "no_eligible_outer_section" : result.RejectionCode ?? "plan_rejected";
            }
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
                " stage=" + stage.Level + " seed=" + StableHash.Hex(seed) + " targetLayers=" + targetLayers +
                " effectiveLayers=" + input.Fortification.WallLayers + " layerFallback=" + layerFallback +
                " layerMode=" + (requestedWallLayers == 0 ? "tech-budget-weighted" : "debug-override") +
                " catalogDefs=" + catalog.Defs.Count + " catalogResearch=" + catalog.Research.Count +
                " wood=" + resources.LocalWoodYield + " stone=" + resources.LocalStoneStuffs.Count +
                " river=" + resources.RiverCells.Count + " geysers=" + resources.SteamGeyserCells.Count +
                " climate=" + climate + " success=" + result.Success + " input=" + input.Digest +
                " layout=" + (result.Layout?.Digest ?? "none") + " stats=" + result.Stats +
                terrainStats + " rejectionCode=" + (result.RejectionCode ?? "none") +
                " rejected=" + RejectionSummary(result));
            return new P4AMapPlanningRun(input, result, profile.Id);
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

        private static LayoutClimate CaptureClimate(Map map)
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
