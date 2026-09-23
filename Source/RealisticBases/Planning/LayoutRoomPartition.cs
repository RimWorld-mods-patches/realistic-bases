using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Pure reproduction of vanilla room splitting and roof coverage on a final
    // planned layout, for temperature rules only (connectivity stays with
    // ProceduralLayoutValidator). Walls, doors, gates, coolers, intact rock and
    // impassable terrain separate rooms; everything else floods cardinally.
    // Room.UsesOutdoorTemperature: touches the map edge, or at least
    // ceil(25% of its cells) are unroofed.
    public sealed class LayoutRoomPartition
    {
        public const double OutdoorOpenFraction = 0.25;

        private readonly int width;
        private readonly int height;
        private readonly int[] roomOf;
        private readonly List<int> cellCount = new List<int>();
        private readonly List<int> openCount = new List<int>();
        private readonly List<bool> touchesEdge = new List<bool>();

        private LayoutRoomPartition(int width, int height)
        {
            this.width = width;
            this.height = height;
            roomOf = Enumerable.Repeat(-1, width * height).ToArray();
        }

        public int RoomCount => cellCount.Count;

        public static LayoutRoomPartition Build(TerrainSnapshot terrain, ProceduralLayout layout)
        {
            if (terrain == null || layout == null) throw new ArgumentNullException();
            HashSet<GridPoint> separators = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
                .Concat(layout.Gates.Select(g => g.Cell)).Concat(layout.Gates.SelectMany(g => g.BandDoors.Concat(g.BandWalls)))
                .Concat(layout.ExhaustPockets.SelectMany(p => p.Walls)));
            foreach (LayoutRoom room in layout.Rooms)
                foreach (GridPoint c in room.Bounds.Points())
                    if (room.IsEdge(c)) separators.Add(c);
            HashSet<GridPoint> excavated = new HashSet<GridPoint>(layout.Excavation);
            HashSet<GridPoint> cleared = new HashSet<GridPoint>(layout.ExhaustRoofClearance);
            HashSet<GridPoint> constructed = new HashSet<GridPoint>(layout.ConstructedRoof);

            LayoutRoomPartition partition = new LayoutRoomPartition(terrain.Width, terrain.Height);
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint start in terrain.Points())
            {
                if (partition.roomOf[partition.Index(start)] >= 0 || Separates(terrain, start, separators, excavated)) continue;
                int id = partition.cellCount.Count;
                int cells = 0, open = 0;
                bool edge = false;
                partition.roomOf[partition.Index(start)] = id;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    GridPoint c = queue.Dequeue();
                    cells++;
                    if (!Roofed(terrain, c, cleared, constructed)) open++;
                    edge |= c.X == 0 || c.Z == 0 || c.X == terrain.Width - 1 || c.Z == terrain.Height - 1;
                    foreach (GridPoint n in ProceduralLayoutValidator.Neighbors(c))
                    {
                        if (!terrain.InBounds(n) || partition.roomOf[partition.Index(n)] >= 0 || Separates(terrain, n, separators, excavated)) continue;
                        partition.roomOf[partition.Index(n)] = id;
                        queue.Enqueue(n);
                    }
                }
                partition.cellCount.Add(cells);
                partition.openCount.Add(open);
                partition.touchesEdge.Add(edge);
            }
            return partition;
        }

        // -1 for separator cells (walls, doors, rock, impassable terrain).
        public int RoomAt(GridPoint cell) => cell.X < 0 || cell.Z < 0 || cell.X >= width || cell.Z >= height ? -1 : roomOf[Index(cell)];

        public bool UsesOutdoorTemperature(int room) =>
            room >= 0 && (touchesEdge[room] || openCount[room] >= (int)Math.Ceiling(cellCount[room] * OutdoorOpenFraction));

        public bool IsOutdoor(GridPoint cell) => UsesOutdoorTemperature(RoomAt(cell));

        public string Describe(GridPoint cell)
        {
            int room = RoomAt(cell);
            return room < 0 ? "separator" : "room" + room + " cells=" + cellCount[room] + " open=" + openCount[room] + " edge=" + touchesEdge[room];
        }

        private int Index(GridPoint c) => c.Z * width + c.X;

        private static bool Separates(TerrainSnapshot terrain, GridPoint c, HashSet<GridPoint> separators, HashSet<GridPoint> excavated)
        {
            if (separators.Contains(c)) return true;
            if (excavated.Contains(c)) return false;
            TerrainCell cell = terrain[c];
            return cell.Has(TerrainTraits.NaturalRock) || cell.Has(TerrainTraits.Occupied) || !cell.Has(TerrainTraits.Standable);
        }

        private static bool Roofed(TerrainSnapshot terrain, GridPoint c, HashSet<GridPoint> cleared, HashSet<GridPoint> constructed) =>
            !cleared.Contains(c) && (constructed.Contains(c) || terrain[c].Has(TerrainTraits.Roofed));
    }
}
