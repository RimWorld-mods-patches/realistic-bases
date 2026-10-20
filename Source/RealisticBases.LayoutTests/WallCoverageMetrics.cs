using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    // Potential geometry only: alternative slots are not simultaneous staffed shooters.
    private sealed class WallCoverageMetrics
    {
        internal int CompoundEdges, FieldEdges, CompoundUncovered, FieldUncovered;
        internal int LongestGap, CompoundLongestGap, FieldLongestGap, Posts, Slots, CompoundUsefulSlots;
        internal int Entrances, CompoundEntrances, FieldEntrances, Doors, ArrivalPosts, ArrivalCrossingCells;
        internal int[] CompoundGatesBySide, FieldGatesBySide;
        internal int[] CompoundEdgesBySide, CompoundUncoveredBySide;
        internal int[] CompoundGateCandidatesBySide;
        internal int CompoundGatehousesSelected, FieldAccessPortsSelected;
        internal static WallCoverageMetrics Measure(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            var result = new WallCoverageMetrics
            {
                CompoundEdgesBySide = new int[directions.Length],
                CompoundUncoveredBySide = new int[directions.Length]
            };
            P4BApproachDefensePlan defense = layout.ApproachDefense;
            var opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            opaque.UnionWith(layout.Turret?.All.SelectMany(t => t.Footprint) ?? new GridPoint[0]);
            opaque.UnionWith(defense?.WallReturns ?? Enumerable.Empty<GridPoint>());
            var blocked = new HashSet<GridPoint>(opaque.Concat(layout.Enclosure)
                .Concat(layout.JointSite == null ? Enumerable.Empty<GridPoint>() : layout.JointSite.Field.Points())
                .Concat(layout.PowerYards.SelectMany(y => y.Cells)).Concat(layout.ExtraObstacles));
            var reachable = FloodEdges(input.Terrain, blocked);
            // Measure enclosure boundaries rather than the final placed-wall
            // set: auxiliary post returns remain opaque obstacles, while doors
            // remain boundary cells even though construction removes them from
            // the wall inventory. Shared field interfaces belong to the field
            // metric and retain their logical crossing counts in both ledgers.
            var compoundDoors = layout.Gates.SelectMany(g => g.BandDoors.Concat(new[] { g.Cell }))
                .Concat(defense?.SallyPorts.Where(p => !p.Field).SelectMany(p => p.Doors) ?? Enumerable.Empty<GridPoint>());
            var fieldDoors = (layout.FieldWall == null ? Enumerable.Empty<GridPoint>() :
                    new[] { layout.FieldWall.OuterGate }.Concat(layout.FieldWall.InnerGate.HasValue
                        ? new[] { layout.FieldWall.InnerGate.Value } : Enumerable.Empty<GridPoint>())
                    .Concat(layout.FieldWall.AccessPorts.SelectMany(p => p.Doors)))
                .Concat(defense?.SallyPorts.Where(p => p.Field).SelectMany(p => p.Doors) ?? Enumerable.Empty<GridPoint>());
            var field = new HashSet<GridPoint>((layout.FieldWall?.Walls ?? new GridPoint[0]).Concat(fieldDoors));
            var walls = new HashSet<GridPoint>(layout.BuiltWalls.Except(defense?.WallReturns ?? Enumerable.Empty<GridPoint>())
                .Concat(layout.NaturalWalls).Concat(layout.OuterWalls)
                .Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(compoundDoors).Concat(field));
            var slots = (defense?.Posts ?? new P4BPost[0]).SelectMany(p => p.Slots).Distinct().ToList();
            var compoundTargets = new HashSet<GridPoint>(walls.Except(field).SelectMany(wall => directions.Select(d =>
                new GridPoint(wall.X + d.X, wall.Z + d.Z))).Where(reachable.Contains));
            result.CompoundUsefulSlots = slots.Count(s => compoundTargets.Any(target =>
                P4BFireLane.CanEngage(s, target, 0, P4BCoverNest.MaximumRange, opaque)));
            var shot = new Dictionary<GridPoint, bool>();
            foreach (GridPoint d in directions)
            {
                var compoundGaps = new HashSet<GridPoint>();
                var fieldGaps = new HashSet<GridPoint>();
                foreach (GridPoint wall in walls.OrderBy(c => c))
                {
                    GridPoint target = new GridPoint(wall.X + d.X, wall.Z + d.Z);
                    if (!reachable.Contains(target)) continue;
                    if (!shot.TryGetValue(target, out bool covered))
                        shot[target] = covered = slots.Any(s => P4BFireLane.CanEngage(s, target, 0, P4BCoverNest.MaximumRange, opaque));
                    if (field.Contains(wall)) { result.FieldEdges++; if (!covered) result.FieldUncovered++; }
                    else
                    {
                        result.CompoundEdges++; result.CompoundEdgesBySide[Array.IndexOf(directions, d)]++;
                        if (!covered)
                        {
                            result.CompoundUncovered++; result.CompoundUncoveredBySide[Array.IndexOf(directions, d)]++;
                        }
                    }
                    if (!covered) (field.Contains(wall) ? fieldGaps : compoundGaps).Add(wall);
                }
                GridPoint tangent = new GridPoint(-d.Z, d.X);
                foreach (GridPoint start in compoundGaps.OrderBy(c => c))
                {
                    if (compoundGaps.Contains(new GridPoint(start.X - tangent.X, start.Z - tangent.Z))) continue;
                    int length = 0;
                    for (GridPoint c = start; compoundGaps.Contains(c); c = new GridPoint(c.X + tangent.X, c.Z + tangent.Z)) length++;
                    result.CompoundLongestGap = Math.Max(result.CompoundLongestGap, length);
                }
                foreach (GridPoint start in fieldGaps.OrderBy(c => c))
                {
                    if (fieldGaps.Contains(new GridPoint(start.X - tangent.X, start.Z - tangent.Z))) continue;
                    int length = 0;
                    for (GridPoint c = start; fieldGaps.Contains(c); c = new GridPoint(c.X + tangent.X, c.Z + tangent.Z)) length++;
                    result.FieldLongestGap = Math.Max(result.FieldLongestGap, length);
                }
            }
            result.LongestGap = Math.Max(result.CompoundLongestGap, result.FieldLongestGap);
            result.Posts = defense?.Posts.Count ?? 0;
            result.Slots = slots.Count;
            var arrivalRoutes = new HashSet<GridPoint>(layout.Gates.SelectMany(g => g.ExteriorRoute));
            var arrivalGates = new HashSet<GridPoint>(layout.Gates.Select(g => g.BandDoors.Concat(new[] { g.Cell })
                .OrderByDescending(c => c.X * g.Outward.X + c.Z * g.Outward.Z).First()));
            result.ArrivalPosts = (defense?.Posts ?? new P4BPost[0]).Count(p => arrivalGates.Contains(p.AccessGate));
            result.ArrivalCrossingCells = (defense?.Posts ?? new P4BPost[0]).SelectMany(p => p.Route.Skip(1).Take(2))
                .Where(arrivalRoutes.Contains).Distinct().Count();
            result.CompoundGatesBySide = P4BApproachDefensePlanner.CompoundGateCrossings(layout);
            result.FieldGatesBySide = P4BApproachDefensePlanner.FieldGateCrossings(layout);
            result.CompoundGateCandidatesBySide = defense?.CompoundGateCandidates.ToArray() ?? new int[4];
            result.CompoundGatehousesSelected = defense?.SallyPorts.Count(p => !p.Field) ?? 0;
            result.FieldAccessPortsSelected = defense?.SallyPorts.Count(p => p.Field) ?? 0;
            result.CompoundEntrances = result.CompoundGatesBySide.Sum();
            result.FieldEntrances = result.FieldGatesBySide.Sum();
            result.Entrances = result.CompoundEntrances + result.FieldEntrances;
            result.Doors = layout.Gates.Sum(g => 1 + g.BandDoors.Count) +
                (layout.FieldWall == null ? 0 : 2) + (layout.FieldWall?.AccessPorts ?? new P4BSallyPort[0])
                    .Concat(defense?.SallyPorts ?? new P4BSallyPort[0]).GroupBy(p => p.Cell)
                    .Select(g => g.First()).Sum(p => p.Doors.Count);
            return result;
        }
        internal static HashSet<GridPoint> FloodEdges(TerrainSnapshot terrain, HashSet<GridPoint> blocked)
        {
            var reachable = new HashSet<GridPoint>();
            var queue = new Queue<GridPoint>();
            foreach (GridPoint c in terrain.Points().Where(c => c.X == 0 || c.Z == 0 ||
                c.X == terrain.Width - 1 || c.Z == terrain.Height - 1))
                if (Walkable(terrain, c) && !blocked.Contains(c) && reachable.Add(c)) queue.Enqueue(c);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var next = new GridPoint(here.X + dx, here.Z + dz);
                    GridPoint sideX = new GridPoint(here.X + dx, here.Z);
                    GridPoint sideZ = new GridPoint(here.X, here.Z + dz);
                    if (dx != 0 && dz != 0 && ((!Walkable(terrain, sideX) || blocked.Contains(sideX)) ||
                        (!Walkable(terrain, sideZ) || blocked.Contains(sideZ)))) continue;
                    if (Walkable(terrain, next) && !blocked.Contains(next) && reachable.Add(next)) queue.Enqueue(next);
                }
            }
            return reachable;
        }
        private static readonly GridPoint[] directions = { new GridPoint(0, 1), new GridPoint(1, 0),
            new GridPoint(0, -1), new GridPoint(-1, 0) };
        private static bool Walkable(TerrainSnapshot terrain, GridPoint c) => terrain.InBounds(c) &&
            (terrain[c].Has(TerrainTraits.Standable) || terrain[c].Has(TerrainTraits.ClearablePlant)) &&
            !terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.NaturalRock | TerrainTraits.Occupied);
        public override string ToString() => "metric=v4-final-boundaries-no-corner-cut compound=" + CompoundUncovered + "/" + CompoundEdges +
            " field=" + FieldUncovered + "/" + FieldEdges + " longestCompound=" + CompoundLongestGap +
            " longestField=" + FieldLongestGap + " posts=" + Posts + " slots=" + Slots +
            " compoundUsefulSlots=" + CompoundUsefulSlots + " arrivalPosts=" + ArrivalPosts +
            " arrivalCrossingCells=" + ArrivalCrossingCells + " compoundEntrances=" + CompoundEntrances +
            " fieldEntrances=" + FieldEntrances + " compoundGates=" + string.Join(",", CompoundGatesBySide ?? new int[0]) +
            " fieldGates=" + string.Join(",", FieldGatesBySide ?? new int[0]) +
            " compoundGateCandidates=" + string.Join(",", CompoundGateCandidatesBySide ?? new int[0]) +
            " compoundGatehousesAdded=" + CompoundGatehousesSelected + " fieldAccessPortsAdded=" + FieldAccessPortsSelected +
            " doors=" + Doors;
    }
}
