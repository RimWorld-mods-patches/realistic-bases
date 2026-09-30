using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P3DefenderRouteAudit
    {
        internal P3DefenderRouteAudit(bool allowed, string reason, int cells, int exterior, int exposed)
        {
            Allowed = allowed; Reason = reason; Cells = cells; Exterior = exterior; Exposed = exposed;
        }
        public bool Allowed { get; }
        public string Reason { get; }
        public int Cells { get; }
        public int Exterior { get; }
        public int Exposed { get; }
    }

    // Check every cell of the actual path, including the transfer across a
    // gate. A covered endpoint alone says nothing about the route to it.
    public static class P3DefenderRoute
    {
        public const int MaximumCells = 100;
        public const int MaximumExteriorCells = 12;
        public const int MaximumExposedCells = 8;

        public static P3DefenderRouteAudit Audit(IEnumerable<GridPoint> route,
            IReadOnlyCollection<GridPoint> defended, IReadOnlyCollection<GridPoint> interior,
            IEnumerable<GridPoint> hostileCells, IReadOnlyCollection<GridPoint> opaque,
            int maximumCells = MaximumCells, int maximumExterior = MaximumExteriorCells,
            int maximumExposed = MaximumExposedCells)
        {
            List<GridPoint> cells = (route ?? Enumerable.Empty<GridPoint>()).ToList();
            if (cells.Count == 0 || cells.Count > maximumCells)
                return new P3DefenderRouteAudit(false, "route_length", cells.Count, 0, 0);
            HashSet<GridPoint> defendedSet = new HashSet<GridPoint>(defended ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> interiorSet = new HashSet<GridPoint>(interior ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> blockers = new HashSet<GridPoint>(opaque ?? Enumerable.Empty<GridPoint>());
            // A pawn opens a door on its own path; evaluate exposure while
            // crossing it instead of treating that door as permanent cover.
            blockers.ExceptWith(cells);
            List<GridPoint> enemies = (hostileCells ?? Enumerable.Empty<GridPoint>()).ToList();
            int exterior = 0, exposed = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                GridPoint c = cells[i];
                if (!defendedSet.Contains(c)) return new P3DefenderRouteAudit(false, "outside_defended_area", cells.Count, exterior, exposed);
                if (i > 0 && Math.Abs(c.X - cells[i - 1].X) + Math.Abs(c.Z - cells[i - 1].Z) > 2)
                    return new P3DefenderRouteAudit(false, "discontinuous_path", cells.Count, exterior, exposed);
                if (!interiorSet.Contains(c)) exterior++;
                if (enemies.Any(e => P4BFireLane.CanEngage(e, c, 0, 45, blockers))) exposed++;
                if (exterior > maximumExterior)
                    return new P3DefenderRouteAudit(false, "long_exterior_transfer", cells.Count, exterior, exposed);
                if (exposed > maximumExposed)
                    return new P3DefenderRouteAudit(false, "route_under_fire", cells.Count, exterior, exposed);
            }
            return new P3DefenderRouteAudit(true, "safe_bounded_route", cells.Count, exterior, exposed);
        }
    }
}
