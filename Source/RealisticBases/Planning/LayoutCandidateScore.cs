using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Layout engine v2 candidate selection (docs/plans/2026-10-12-layout-engine-v2.md):
    // compares complete, already valid plans. Hard invariants never enter the
    // score; they are the validator's job. Lower is better.
    public sealed class LayoutCandidateScore
    {
        private LayoutCandidateScore(double total, double overhead, int sharedWalls, int routeCells, int gapFronts, int downgrades)
        {
            Total = total;
            Overhead = overhead;
            SharedWalls = sharedWalls;
            RouteCells = routeCells;
            GapFronts = gapFronts;
            Downgrades = downgrades;
        }

        public double Total { get; }
        // Circulation enclosure cells per room cell (yards and courts excluded).
        public double Overhead { get; }
        public int SharedWalls { get; }
        // Public walking distance summed over the routes residents use most.
        public int RouteCells { get; }
        public int GapFronts { get; }
        public int Downgrades { get; }

        public override string ToString() => "score=" + Total.ToString("F1") + " overhead=" + Overhead.ToString("F2") +
            " shared=" + SharedWalls + " routes=" + RouteCells + " gapFronts=" + GapFronts + " downgrades=" + Downgrades;

        public static LayoutCandidateScore Evaluate(ProceduralLayoutInput input, ProceduralLayout layout, int gapFronts, int downgrades)
        {
            HashSet<GridPoint> roomCells = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Bounds.Points()));
            int yards = layout.PowerYards.Sum(y => y.Cells.Count + y.ZoneCells.Count) + layout.Courts.Sum(c => c.Cells.Count);
            double overhead = Math.Max(0, layout.Enclosure.Count - roomCells.Count - yards) / (double)Math.Max(1, roomCells.Count);
            int shared = layout.Rooms.SelectMany(r => r.Bounds.Points()).GroupBy(c => c).Count(g => g.Count() > 1);
            int routes = Routes(input, layout);
            double total = 40 * overhead - 0.25 * shared + 0.5 * routes + 15 * gapFronts + 10 * downgrades;
            return new LayoutCandidateScore(total, overhead, shared, routes, gapFronts, downgrades);
        }

        // Food store -> kitchen, food store -> dining, storage -> workshop and
        // housing -> dining, each as the nearest pair over public circulation.
        private static int Routes(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            HashSet<GridPoint> walk = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input, layout));
            Func<LayoutRoomKind[], List<LayoutRoom>> of = kinds => layout.Rooms.Where(r => kinds.Contains(r.Kind)).ToList();
            LayoutRoomKind[] food = { LayoutRoomKind.Freezer, LayoutRoomKind.Pantry };
            int total = 0;
            total += Nearest(walk, of(food), of(new[] { LayoutRoomKind.Kitchen }));
            total += Nearest(walk, of(food), of(new[] { LayoutRoomKind.Dining }));
            total += Nearest(walk, of(new[] { LayoutRoomKind.Storage }), of(new[] { LayoutRoomKind.Workshop }));
            total += Nearest(walk, of(new[] { LayoutRoomKind.Barracks, LayoutRoomKind.Bedroom }), of(new[] { LayoutRoomKind.Dining }));
            return total;
        }

        private const int Unreachable = 200;

        private static int Nearest(HashSet<GridPoint> walk, List<LayoutRoom> from, List<LayoutRoom> to)
        {
            if (from.Count == 0 || to.Count == 0) return 0;
            HashSet<GridPoint> targets = new HashSet<GridPoint>(to.Select(r => r.DoorOutside));
            Dictionary<GridPoint, int> distance = new Dictionary<GridPoint, int>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint start in from.Select(r => r.DoorOutside).Distinct())
                if (walk.Contains(start)) { distance[start] = 0; queue.Enqueue(start); }
            while (queue.Count > 0)
            {
                GridPoint c = queue.Dequeue();
                if (targets.Contains(c)) return distance[c];
                foreach (GridPoint n in ProceduralLayoutValidator.Neighbors(c))
                    if (walk.Contains(n) && !distance.ContainsKey(n)) { distance[n] = distance[c] + 1; queue.Enqueue(n); }
            }
            return Unreachable;
        }
    }
}
