using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;
using RimWorld;
using Verse;

namespace RealisticBases.Integration
{
    // Only the small Core palette named here is eligible. All definitions and
    // actual terrain facts are read from the loaded game, not inferred by label.
    public static class P4CMapFinishes
    {
        private static readonly string[] Basic = { "WoodPlankFloor", "Concrete", "PavedTile", "SterileTile" };

        public static P4CFinishPlan Plan(Map map, ProceduralLayoutInput input, ProceduralLayout layout)
        {
            HashSet<GridPoint> points = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Interior.Points())
                .Concat(layout.Corridors.SelectMany(c => c.Cells))
                .Concat(layout.Gates.SelectMany(g => g.InnerVestibule.Concat(g.OuterVestibule)
                    .Concat(g.BandDoors).Concat(new[] { g.Inside, g.Cell })))
                .Concat(layout.Rooms.SelectMany(r => r.Doors.SelectMany(d => new[] { d, r.OutsideOf(d) })))
                .Concat(layout.FieldWall == null ? Enumerable.Empty<GridPoint>() :
                    layout.FieldWall.ArrivalRoute.TakeWhile(c => !c.Equals(layout.FieldWall.OuterGate))
                        .Concat(new[] { layout.FieldWall.OuterGate })
                        .Concat(layout.FieldWall.InnerGate.HasValue ?
                            new[] { layout.FieldWall.InnerGate.Value } : new GridPoint[0])));
            HashSet<GridPoint> floorable = new HashSet<GridPoint>();
            Dictionary<GridPoint, string> smoothFloors = new Dictionary<GridPoint, string>();
            Dictionary<GridPoint, string> smoothWalls = new Dictionary<GridPoint, string>();
            Dictionary<string, P4CFloorDef> available = new Dictionary<string, P4CFloorDef>(StringComparer.Ordinal);
            List<string> names = Basic.Concat(input.Resources.LocalStoneStuffs.Where(s =>
                s.StartsWith("Blocks", StringComparison.Ordinal)).SelectMany(s =>
                    new[] { "Tile" + s.Substring(6), "Flagstone" + s.Substring(6) })).Distinct().ToList();
            foreach (string name in names)
            {
                TerrainDef def = DefDatabase<TerrainDef>.GetNamedSilentFail(name);
                if (!Legal(def, layout.TechStage) || !def.layerable || def.terrainAffordanceNeeded != TerrainAffordanceDefOf.Heavy)
                    continue;
                int value = Cost(input.Catalog, def);
                if (value < 0) continue;
                int wood = def.costList?.Where(c => c.thingDef == ThingDefOf.WoodLog).Sum(c => c.count) ?? 0;
                available[name] = new P4CFloorDef(name, value, wood);
            }
            foreach (GridPoint point in points)
            {
                if (!input.Terrain.InBounds(point)) continue;
                IntVec3 cell = Cell(point);
                TerrainDef top = map.terrainGrid.TerrainAt(cell);
                if (top == null || top.IsWater || top.layerable ||
                    !top.affordances.Contains(TerrainAffordanceDefOf.Heavy)) continue;
                Building edifice = cell.GetEdifice(map);
                if (edifice != null && !(layout.Excavation.Contains(point) && edifice.def?.building?.isNaturalRock == true))
                    continue;
                floorable.Add(point);
                TerrainDef smooth = top.smoothedTerrain;
                if (smooth != null && smooth != top && !smooth.IsWater && smooth.affordances.Contains(TerrainAffordanceDefOf.Heavy))
                {
                    smoothFloors[point] = smooth.defName;
                    if (!available.ContainsKey(smooth.defName)) available[smooth.defName] = new P4CFloorDef(smooth.defName, 0);
                }
            }
            foreach (GridPoint point in layout.NaturalWalls.Concat(layout.Rooms.SelectMany(r => r.NaturalWalls)).Distinct())
            {
                if (!input.Terrain.InBounds(point) || layout.Excavation.Contains(point)) continue;
                Building source = Cell(point).GetEdifice(map);
                ThingDef target = source?.def?.building?.smoothedThing;
                if (source?.def?.building?.isNaturalRock != true || target == null ||
                    target == source.def || target.building?.isNaturalRock == true ||
                    source.def.holdsRoof != target.holdsRoof || source.def.passability != target.passability ||
                    source.def.blockLight != target.blockLight || source.def.fillPercent != target.fillPercent ||
                    source.def.building.isAirtight != target.building?.isAirtight)
                    continue;
                smoothWalls[point] = target.defName;
            }
            return P4CFinishPlanner.Plan(input, layout,
                new P4CFinishSite(floorable, smoothFloors, smoothWalls), available.Values);
        }

        private static bool Legal(TerrainDef def, SettlementTechStage stage) => def != null &&
            (def.researchPrerequisites == null || def.researchPrerequisites.All(r => stage.HasResearch(r.defName)));

        private static int Cost(TechCatalog catalog, TerrainDef def)
        {
            double value = 0;
            foreach (ThingDefCountClass item in def.costList ?? new List<ThingDefCountClass>())
            {
                if (!catalog.TryGet(item.thingDef.defName, out CatalogDef source) || source.MarketValue <= 0) return -1;
                value += item.count * source.MarketValue;
            }
            return value >= int.MaxValue ? -1 : (int)Math.Ceiling(value);
        }

        private static IntVec3 Cell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }
}
