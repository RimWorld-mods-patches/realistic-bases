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
        // Full multi-layer compounds can require a longer internal walk. Keep
        // the same finite limit in generation preflight and live assignments.
        public const int MaximumCells = 160;
        public const int MaximumExteriorCells = 12;
        public const int MaximumExposedCells = 8;

        // Native eight-way movement rounds the corners of cardinal planner
        // routes. Declare one cell of legal clearance, not an entire exterior
        // rectangle. Exposure and aggregate route limits remain authoritative.
        public static IReadOnlyList<GridPoint> TransitEnvelope(TerrainSnapshot terrain,
            IEnumerable<GridPoint> routes, IEnumerable<GridPoint> opaque)
        {
            var blockers = new HashSet<GridPoint>(opaque ?? Enumerable.Empty<GridPoint>());
            var result = new HashSet<GridPoint>(routes ?? Enumerable.Empty<GridPoint>());
            foreach (GridPoint c in result.ToArray())
                for (int x = -1; x <= 1; x++)
                    for (int z = -1; z <= 1; z++)
                    {
                        GridPoint n = new GridPoint(c.X + x, c.Z + z);
                        if (terrain.InBounds(n) && terrain[n].Has(TerrainTraits.Standable) &&
                            !terrain[n].Has(TerrainTraits.Water) && !blockers.Contains(n)) result.Add(n);
                    }
            return result.OrderBy(c => c).ToList().AsReadOnly();
        }

        // Validate complete native paths through a small declared gateway set.
        // A defender on another front must enter through its local gate before
        // crossing the compound; the destination gate alone is not sufficient.
        public static GridPoint? NextWaypoint(GridPoint from, GridPoint destination, GridPoint center,
            GridPoint? destinationGate, IEnumerable<GridPoint> entryGates,
            Func<GridPoint, GridPoint, IReadOnlyList<GridPoint>> nativePath,
            IReadOnlyCollection<GridPoint> defended, IReadOnlyCollection<GridPoint> interior,
            IEnumerable<GridPoint> hostileCells, IReadOnlyCollection<GridPoint> opaque, Action<int> reportLength = null)
        {
            var gates = (entryGates ?? Enumerable.Empty<GridPoint>()).Distinct()
                .OrderBy(g => Math.Abs(g.X - from.X) + Math.Abs(g.Z - from.Z)).ThenBy(g => g).Take(8).ToArray();
            var chains = new List<GridPoint[]> { new[] { destination } };
            GridPoint exit = destinationGate ?? destination;
            chains.Add(new[] { exit, destination });
            chains.Add(new[] { center, exit, destination });
            foreach (GridPoint entry in gates)
            {
                chains.Add(new[] { entry, exit, destination });
                chains.Add(new[] { entry, center, exit, destination });
            }
            var cache = new Dictionary<Tuple<GridPoint, GridPoint>, IReadOnlyList<GridPoint>>();
            GridPoint? best = null;
            int bestLength = int.MaxValue;
            foreach (GridPoint[] candidate in chains)
            {
                GridPoint[] chain = candidate.TakeWhile(c => !c.Equals(destination)).Concat(new[] { destination })
                    .Where(c => !c.Equals(from)).Distinct().ToArray();
                if (chain.Length == 0) continue;
                var full = new List<GridPoint>();
                GridPoint current = from;
                bool found = true;
                foreach (GridPoint next in chain)
                {
                    var key = Tuple.Create(current, next);
                    if (!cache.TryGetValue(key, out IReadOnlyList<GridPoint> leg)) cache[key] = leg = nativePath(current, next);
                    if (leg == null || leg.Count == 0 || !leg[0].Equals(current) || !leg[leg.Count - 1].Equals(next))
                    { found = false; break; }
                    full.AddRange(full.Count == 0 ? leg : leg.Skip(1));
                    if (full.Count > MaximumCells) { found = false; break; }
                    current = next;
                }
                // Check aggregate length, exposure and outside crossings, not
                // just individually safe legs whose totals could exceed limits.
                if (!found || full.Count >= bestLength || !Audit(full, defended, interior, hostileCells, opaque).Allowed) continue;
                bestLength = full.Count;
                best = chain[0];
            }
            reportLength?.Invoke(best.HasValue ? bestLength : 0);
            return best;
        }

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
