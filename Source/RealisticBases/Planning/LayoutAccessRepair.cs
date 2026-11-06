using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    internal sealed partial class LayoutAttempt
    {
        private int accessRepairs;

        private int CorridorRepairLimit => input.Budget.MaxRepairsPerAttempt + 2 * demand.Rooms.Count;
        private bool AccessRepairAvailable => accessRepairs < input.Budget.MaxRepairsPerAttempt &&
            (Style.Kind == LayoutStyleKind.Mountain
                ? repairs + accessRepairs < input.Budget.MaxRepairsPerAttempt
                : growths + accessRepairs < CorridorRepairLimit);

        // A locally good room may close the only dry neck leading to space
        // needed by a later room. Give that space public frontage first, then
        // reconsider the room; neither room transit nor terrain rules change.
        private bool TryPreserveRoomAccess(LayoutRoomDemand room, GridRect bounds)
        {
            if (!AccessRepairAvailable || stats.BudgetExhausted || stats.Evaluations >= input.Budget.MaxEvaluations ||
                !demand.Rooms.Any(r => r.Id != room.Id && !placed.ContainsKey(r.Id))) return false;

            bool OpenRing(GridPoint c) => grid.CanPlace(c) &&
                (occ[grid.Index(c)] == Street || OkForStreet(c));
            bool detour = true;
            for (int x = bounds.MinX - 1; x <= bounds.MaxX + 1 && detour; x++)
                detour = OpenRing(new GridPoint(x, bounds.MinZ - 1)) && OpenRing(new GridPoint(x, bounds.MaxZ + 1));
            for (int z = bounds.MinZ; z <= bounds.MaxZ && detour; z++)
                detour = OpenRing(new GridPoint(bounds.MinX - 1, z)) && OpenRing(new GridPoint(bounds.MaxX + 1, z));
            if (detour) return false;

            GridPoint[] reserved = ribReserved.ToArray();
            bool changed = false;
            ReleaseReserves();
            try
            {
                if (!TryAccessReach(null, out HashSet<GridPoint> before) ||
                    !TryAccessReach(bounds, out HashSet<GridPoint> after)) return false;
                var lost = new HashSet<GridPoint>(before.Where(c => !bounds.Contains(c) && !after.Contains(c) && OkForStreet(c)));
                // At least one minimum legal room's outer footprint must be
                // lost. Small slivers do not justify extra public circulation.
                int side = LayoutDemandModel.MinInterior + 2;
                if (lost.Count < side * side || !TryAccessChamber(lost, side, out GridRect chamber)) return false;
                int midX = chamber.MinX + (chamber.Width - 1) / 2;
                int midZ = chamber.MinZ + (chamber.Height - 1) / 2;
                var targets = new HashSet<GridPoint>(new[] {
                    new GridPoint(midX, chamber.MinZ - 1), new GridPoint(midX, chamber.MaxZ + 1),
                    new GridPoint(chamber.MinX - 1, midZ), new GridPoint(chamber.MaxX + 1, midZ)
                }.Where(c => !bounds.Contains(c) && OkForStreet(c)));
                if (!TryAccessPath(chamber, targets, out GridPoint joined, out List<GridPoint> path) || path.Count == 0)
                    return false;

                stats.Evaluations++;
                var segment = new Segment { Id = "room-access-" + accessRepairs, Retain = true,
                    Root = joined, Axis = new GridPoint(0, 1) };
                foreach (GridPoint c in path) segment.Sections.Add(new List<GridPoint> { c });
                if (!Claim(segment)) return false; // Charges the existing excavation allowance.
                segments.Add(segment);
                accessRepairs++;
                stats.Repairs++;
                changed = true;
                ProceduralLayoutPlanner.Note(diagnostics, "repair", "room_access_preserved", Site +
                    " room=" + room.Id + " lost=" + lost.Count + " chamber=" + chamber + " corridor=" + path.Count);
                return true;
            }
            finally
            {
                if (changed) RefreshReserves();
                else foreach (GridPoint c in reserved)
                    if (occ[grid.Index(c)] == Free) { occ[grid.Index(c)] = RibReserve; ribReserved.Add(c); }
            }
        }

        private bool TryAccessReach(GridRect? excluded, out HashSet<GridPoint> reached)
        {
            reached = new HashSet<GridPoint>();
            var queue = new Queue<GridPoint>();
            foreach (GridPoint root in segments.SelectMany(s => s.Cells).Distinct())
                if ((!excluded.HasValue || !excluded.Value.Contains(root)) && reached.Add(root)) queue.Enqueue(root);
            int nodes = 0;
            while (queue.Count > 0 && nodes < input.Budget.MaxRouteNodes)
            {
                GridPoint c = queue.Dequeue(); nodes++; stats.RouteNodes++;
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    var next = new GridPoint(c.X + step.X, c.Z + step.Z);
                    if ((!excluded.HasValue || !excluded.Value.Contains(next)) && OkForStreet(next) && reached.Add(next))
                        queue.Enqueue(next);
                }
            }
            return queue.Count == 0;
        }

        private bool TryAccessChamber(HashSet<GridPoint> cells, int minimumSide, out GridRect chamber)
        {
            int minX = cells.Min(c => c.X), maxX = cells.Max(c => c.X);
            int minZ = cells.Min(c => c.Z), maxZ = cells.Max(c => c.Z);
            int width = maxX - minX + 1;
            chamber = default(GridRect);
            if ((long)width * (maxZ - minZ + 1) > input.Budget.MaxRouteNodes) return false;
            var heights = new int[width];
            int bestArea = 0;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    heights[x] = cells.Contains(new GridPoint(minX + x, z)) ? heights[x] + 1 : 0;
                    stats.RouteNodes++;
                }
                var stack = new Stack<int>();
                for (int x = 0; x <= width; x++)
                {
                    int h = x == width ? 0 : heights[x];
                    while (stack.Count > 0 && heights[stack.Peek()] > h)
                    {
                        int height = heights[stack.Pop()], left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                        int w = x - left;
                        if (w >= minimumSide && height >= minimumSide && w * height > bestArea)
                        { bestArea = w * height; chamber = new GridRect(minX + left, z - height + 1, w, height); }
                    }
                    stack.Push(x);
                }
            }
            return bestArea > 0;
        }

        private bool TryAccessPath(GridRect chamber, HashSet<GridPoint> targets,
            out GridPoint joined, out List<GridPoint> path)
        {
            joined = default(GridPoint); path = new List<GridPoint>();
            if (targets.Count == 0) return false;
            var previous = new Dictionary<GridPoint, GridPoint>();
            var queue = new Queue<GridPoint>();
            foreach (GridPoint root in segments.SelectMany(s => s.Cells).Distinct())
            { previous[root] = root; queue.Enqueue(root); }
            int nodes = 0;
            while (queue.Count > 0 && nodes < input.Budget.MaxRouteNodes)
            {
                GridPoint c = queue.Dequeue(); nodes++; stats.RouteNodes++;
                if (targets.Contains(c))
                {
                    joined = c;
                    while (!previous[joined].Equals(joined)) { path.Add(joined); joined = previous[joined]; }
                    path.Reverse(); return true;
                }
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    var next = new GridPoint(c.X + step.X, c.Z + step.Z);
                    if (!previous.ContainsKey(next) && !chamber.Contains(next) && OkForStreet(next))
                    { previous[next] = c; queue.Enqueue(next); }
                }
            }
            return false;
        }
    }
}
