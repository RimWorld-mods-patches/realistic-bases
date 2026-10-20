using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Inner-yard crops: enclosed open-sky fertile ground the final layout left
    // unused becomes extra growing area, like a player planting a courtyard.
    // Purely additive (never part of the required field), so it cannot reject a
    // layout. Patches keep a one-cell walkway around them (3x3 erosion) and
    // stay off streets, gate approaches, yards, courts and defense positions.
    public static class LayoutBonusFields
    {
        public const int MinimumPatch = 16;

        public static IReadOnlyList<GridPoint> Select(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            TerrainSnapshot terrain = input.Terrain;
            HashSet<GridPoint> blocked = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Bounds.Points())
                .Concat(layout.Corridors.SelectMany(c => c.Cells))
                .Concat(layout.Courts.SelectMany(c => c.Cells.Concat(c.Access)))
                .Concat(layout.PowerYards.SelectMany(y => y.Cells.Concat(y.ZoneCells).Concat(new[] { y.Access })))
                .Concat(layout.ExhaustPockets.Select(p => p.Cell)).Concat(layout.ExhaustPocketWalls)
                .Concat(layout.AllFixtures.SelectMany(f => f.Cells.Concat(f.Access)))
                .Concat(layout.Rooms.Select(r => r.DoorOutside))
                .Concat(layout.ConstructedRoof).Concat(layout.ExtraObstacles));
            foreach (LayoutGate gate in layout.Gates)
                foreach (GridPoint c in gate.InnerVestibule.Concat(new[] { gate.Inside }))
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dx = -2; dx <= 2; dx++) blocked.Add(new GridPoint(c.X + dx, c.Z + dz));
            if (layout.ApproachDefense != null)
                blocked.UnionWith(layout.ApproachDefense.Posts.SelectMany(p => p.Route.Concat(p.CoverCells).Concat(new[] { p.Cell, p.AccessGate }))
                    .Concat(layout.ApproachDefense.CoverCells).Concat(layout.ApproachDefense.TransitCells)
                    .Concat(layout.ApproachDefense.SallyPorts.SelectMany(p => p.InteriorRoute.Concat(p.Doors).Concat(new[] { p.Inside }))));
            if (layout.Turret != null)
                blocked.UnionWith(layout.Turret.All.SelectMany(t => t.Footprint.Concat(t.CoverCells)));
            HashSet<GridPoint> candidate = new HashSet<GridPoint>(layout.Enclosure.Where(c =>
                !blocked.Contains(c) && terrain.InBounds(c) && terrain[c].Has(TerrainTraits.Fertile) &&
                LayoutTerrainGrid.Classify(terrain[c]) == LayoutCellClass.Open && !terrain[c].Has(TerrainTraits.Roofed)));
            // 3x3 erosion: a cell is plantable only when its whole neighbourhood
            // is candidate ground, which leaves a walkway along every edge.
            HashSet<GridPoint> core = new HashSet<GridPoint>(candidate.Where(c =>
                Enumerable.Range(-1, 3).All(dz => Enumerable.Range(-1, 3).All(dx => candidate.Contains(new GridPoint(c.X + dx, c.Z + dz))))));
            List<GridPoint> result = new List<GridPoint>();
            HashSet<GridPoint> seen = new HashSet<GridPoint>();
            foreach (GridPoint start in core.OrderBy(c => c))
            {
                if (!seen.Add(start)) continue;
                List<GridPoint> patch = new List<GridPoint> { start };
                Queue<GridPoint> queue = new Queue<GridPoint>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                    foreach (GridPoint n in ProceduralLayoutValidator.Neighbors(queue.Dequeue()))
                        if (core.Contains(n) && seen.Add(n)) { patch.Add(n); queue.Enqueue(n); }
                if (patch.Count >= MinimumPatch) result.AddRange(patch);
            }
            return result.OrderBy(c => c).ToList();
        }
    }
}
