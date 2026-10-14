using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Count physical wall/closed-door cells on the cheapest eight-way breach
    // route (no corner cutting past barriers), not reinforced sections or
    // gate-door counters. Intact natural rock
    // costs a barrier too; unrelated ruins/furniture never earn wall credit.
    public static class P4BPerimeterDepth
    {
        // The protected space inside the outermost complete shell, including
        // its wall cells. Does not grant movement into arbitrary exterior land.
        public static IReadOnlyList<GridPoint> Envelope(TerrainSnapshot terrain, ProceduralLayout layout)
        {
            int radius = layout.WallLayers;
            var cells = new HashSet<GridPoint>(layout.Gates.SelectMany(g => g.OuterVestibule.Concat(g.BandDoors)));
            foreach (GridPoint c in layout.Enclosure)
                for (int dz = -radius; dz <= radius; dz++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        GridPoint n = new GridPoint(c.X + dx, c.Z + dz);
                        if (terrain.InBounds(n)) cells.Add(n);
                    }
            return cells.OrderBy(c => c).ToList().AsReadOnly();
        }

        public static int Minimum(TerrainSnapshot terrain, IEnumerable<GridPoint> interior,
            IEnumerable<GridPoint> barriers, IEnumerable<GridPoint> excavation, int limit)
        {
            if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
            var targets = new HashSet<GridPoint>(interior);
            var walls = new HashSet<GridPoint>(barriers);
            var dug = new HashSet<GridPoint>(excavation);
            int[] distances = Enumerable.Repeat(int.MaxValue, terrain.Width * terrain.Height).ToArray();
            var queue = new LinkedList<GridPoint>();
            foreach (GridPoint c in terrain.Points().Where(c => c.X == 0 || c.Z == 0 ||
                c.X == terrain.Width - 1 || c.Z == terrain.Height - 1)) Visit(c, 0);
            while (queue.Count > 0)
            {
                GridPoint c = queue.First.Value;
                queue.RemoveFirst();
                int cost = distances[c.Z * terrain.Width + c.X];
                if (targets.Contains(c)) return Math.Min(cost, limit);
                // Diagonal steps follow vanilla pathing: a pawn cannot cut a
                // corner past a wall, closed door or rock on either side.
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dz != 0) && (dx == 0 || dz == 0 ||
                            (!Barrier(new GridPoint(c.X + dx, c.Z)) && !Barrier(new GridPoint(c.X, c.Z + dz)))))
                            Visit(new GridPoint(c.X + dx, c.Z + dz), cost);
            }
            // No route cheaper than limit exists (including impassable terrain).
            return limit;

            bool Barrier(GridPoint c) => terrain.InBounds(c) &&
                (walls.Contains(c) || (terrain[c].Has(TerrainTraits.NaturalRock) && !dug.Contains(c)));

            void Visit(GridPoint c, int previous)
            {
                if (!terrain.InBounds(c)) return;
                TerrainCell cell = terrain[c];
                bool barrier = Barrier(c);
                if (!barrier && !dug.Contains(c) && !cell.HasAny(TerrainTraits.Standable |
                    TerrainTraits.ClearablePlant | TerrainTraits.Buildable)) return;
                int next = previous + (barrier ? 1 : 0);
                int index = c.Z * terrain.Width + c.X;
                if (next >= limit || next >= distances[index]) return;
                distances[index] = next;
                if (barrier) queue.AddLast(c); else queue.AddFirst(c);
            }
        }

        public static int Minimum(TerrainSnapshot terrain, ProceduralLayout layout) =>
            Minimum(terrain, layout.Enclosure, layout.BuiltWalls.Concat(layout.OuterWalls)
                .Concat(layout.NaturalWalls).Concat(layout.Gates.SelectMany(g =>
                    g.BandWalls.Concat(g.BandDoors).Concat(new[] { g.Cell })))
                .Concat(layout.ApproachDefense?.SallyPorts.Select(p => p.Cell) ?? new GridPoint[0]),
                layout.Excavation, layout.WallLayers);
    }
}
