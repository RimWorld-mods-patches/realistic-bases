using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    public static class P4FieldPlacement
    {
        // Search the exterior from the gate approaches. A fertile cell on the
        // far side of water or rock is not a usable field, however close it is
        // to a gate in straight-line distance.
        public static IReadOnlyList<GridPoint> SelectReachable(TerrainSnapshot terrain,
            IEnumerable<GridPoint> approaches, IEnumerable<GridPoint> reserved, int required)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (approaches == null) throw new ArgumentNullException(nameof(approaches));
            if (reserved == null) throw new ArgumentNullException(nameof(reserved));
            if (required < 0) throw new ArgumentOutOfRangeException(nameof(required));
            HashSet<GridPoint> blocked = new HashSet<GridPoint>(reserved);
            HashSet<GridPoint> startCells = new HashSet<GridPoint>(approaches);
            HashSet<GridPoint> visited = new HashSet<GridPoint>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint start in startCells.OrderBy(c => c))
                if (Passable(start) && visited.Add(start)) queue.Enqueue(start);
            List<GridPoint> fields = new List<GridPoint>();
            GridPoint[] directions = { new GridPoint(0, -1), new GridPoint(-1, 0),
                new GridPoint(1, 0), new GridPoint(0, 1) };
            while (queue.Count > 0 && fields.Count < required)
            {
                GridPoint here = queue.Dequeue();
                if (!startCells.Contains(here) && terrain[here].Has(TerrainTraits.Fertile)) fields.Add(here);
                foreach (GridPoint direction in directions)
                {
                    GridPoint next = new GridPoint(here.X + direction.X, here.Z + direction.Z);
                    if (Passable(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return fields;

            bool Passable(GridPoint cell) => terrain.InBounds(cell) && !blocked.Contains(cell) &&
                (terrain[cell].Has(TerrainTraits.Standable) || terrain[cell].Has(TerrainTraits.ClearablePlant)) &&
                !terrain[cell].HasAny(TerrainTraits.Water | TerrainTraits.NaturalRock | TerrainTraits.Occupied);
        }
    }
}
