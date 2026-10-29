using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P4BFieldPostPathResult
    {
        internal P4BFieldPostPathResult(bool allowed, int cells, bool viaGate)
        { Allowed = allowed; Cells = cells; ViaGate = viaGate; }
        public bool Allowed { get; }
        public int Cells { get; }
        public bool ViaGate { get; }
    }

    // The native route provider is the only game dependency. Keep the exact
    // direct/gate/waypoint spawn decisions executable in offline regressions.
    public static class P4BFieldPostPath
    {
        public static P4BFieldPostPathResult Evaluate(GridPoint from, GridPoint post, GridPoint center,
            GridPoint gate, IEnumerable<GridPoint> entryGates,
            Func<GridPoint, GridPoint, IReadOnlyList<GridPoint>> nativeRoute,
            IReadOnlyCollection<GridPoint> defended, IReadOnlyCollection<GridPoint> interior,
            IReadOnlyCollection<GridPoint> opaque, IReadOnlyList<GridPoint> direct = null)
        {
            var area = new HashSet<GridPoint>(defended);
            direct = direct ?? nativeRoute(from, post);
            bool Within(IReadOnlyList<GridPoint> path) => path != null && path.Count > 0 && path.All(area.Contains);
            int length = direct?.Count ?? 0;
            if (Within(direct) && length <= P3DefenderRoute.MaximumCells)
                return new P4BFieldPostPathResult(true, length, false);
            if (area.Contains(gate))
            {
                var toGate = nativeRoute(from, gate);
                var fromGate = nativeRoute(gate, post);
                length = (toGate?.Count ?? 0) + (fromGate?.Count ?? 0) - 1;
                if (Within(toGate) && Within(fromGate) && length <= P3DefenderRoute.MaximumCells)
                    return new P4BFieldPostPathResult(true, length, true);
            }
            int completeLength = 0;
            var waypoint = P3DefenderRoute.NextWaypoint(from, post, center, gate, entryGates, nativeRoute,
                defended, interior, new GridPoint[0], opaque, n => completeLength = n);
            return new P4BFieldPostPathResult(waypoint.HasValue, completeLength, waypoint.HasValue);
        }
    }
}
