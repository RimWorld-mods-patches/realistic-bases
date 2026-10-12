using System;
using System.Collections.Generic;

namespace RealisticBases.Planning
{
    // How exposed a candidate site is to attackers arriving from the map edge.
    // Deep water, natural rock and edifices block; shallow water and clearable
    // plants are walkable (slow, not a barrier). A site with its back to the
    // sea or a cliff has fewer approach cells, so the planner can concentrate
    // its defenses on the remaining front.
    public static class LayoutExposure
    {
        public static bool Walkable(TerrainCell cell) =>
            (cell.Has(TerrainTraits.Standable) || cell.Has(TerrainTraits.ClearablePlant)) &&
            !cell.HasAny(TerrainTraits.Occupied | TerrainTraits.NaturalRock);

        // Cells an attacker on foot can reach from any walkable map-edge cell.
        public static bool[] EdgeReach(TerrainSnapshot terrain)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            bool[] reach = new bool[terrain.Width * terrain.Height];
            Queue<GridPoint> queue = new Queue<GridPoint>();
            for (int z = 0; z < terrain.Height; z++)
                for (int x = 0; x < terrain.Width; x++)
                {
                    if (x != 0 && z != 0 && x != terrain.Width - 1 && z != terrain.Height - 1) continue;
                    GridPoint c = new GridPoint(x, z);
                    if (!Walkable(terrain[c])) continue;
                    reach[z * terrain.Width + x] = true;
                    queue.Enqueue(c);
                }
            while (queue.Count > 0)
            {
                GridPoint c = queue.Dequeue();
                foreach (GridPoint n in ProceduralLayoutValidator.Neighbors(c))
                {
                    if (!terrain.InBounds(n)) continue;
                    int i = n.Z * terrain.Width + n.X;
                    if (reach[i] || !Walkable(terrain[n])) continue;
                    reach[i] = true;
                    queue.Enqueue(n);
                }
            }
            return reach;
        }

        // Ring cells at `offset` outside `rect` that attackers cannot reach.
        // Cells beyond the map are not counted: the edge is where they arrive.
        public static int ProtectedRing(TerrainSnapshot terrain, bool[] reach, GridRect rect, int offset, out int ringCells)
        {
            ringCells = 0;
            int protectedCells = 0;
            int minX = rect.MinX - offset, maxX = rect.MaxX + offset, minZ = rect.MinZ - offset, maxZ = rect.MaxZ + offset;
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (x != minX && x != maxX && z != minZ && z != maxZ) continue;
                    GridPoint c = new GridPoint(x, z);
                    if (!terrain.InBounds(c)) continue;
                    ringCells++;
                    if (!reach[z * terrain.Width + x]) protectedCells++;
                }
            return protectedCells;
        }

        public static GridRect Union(GridRect a, GridRect b)
        {
            int minX = Math.Min(a.MinX, b.MinX), minZ = Math.Min(a.MinZ, b.MinZ);
            int maxX = Math.Max(a.MaxX, b.MaxX), maxZ = Math.Max(a.MaxZ, b.MaxZ);
            return new GridRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        }
    }
}
