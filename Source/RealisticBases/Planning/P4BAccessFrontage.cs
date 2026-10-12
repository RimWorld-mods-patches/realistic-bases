using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // A connected public extension fitted after room packing and before the
    // enclosure is finalized. Room demand and the reserved field never move.
    internal sealed class P4BAccessFrontage
    {
        internal P4BAccessFrontage(GridPoint outward, GridPoint terminal, GridPoint join,
            IEnumerable<GridPoint> route, IEnumerable<GridPoint> tip, int shots)
        {
            Outward = outward; Terminal = terminal; Join = join; Shots = shots;
            Route = new ReadOnlyCollection<GridPoint>(route.ToList());
            Cells = new ReadOnlyCollection<GridPoint>(route.Concat(tip).Distinct().OrderBy(c => c).ToList());
        }
        internal GridPoint Outward { get; }
        internal GridPoint Terminal { get; }
        internal GridPoint Join { get; }
        internal int Shots { get; }
        internal IReadOnlyList<GridPoint> Route { get; }
        internal IReadOnlyList<GridPoint> Cells { get; }
    }

    internal static class P4BAccessFrontagePlanner
    {
        internal static IReadOnlyList<P4BAccessFrontage> Candidates(ProceduralLayoutInput input,
            ProceduralLayout layout, P4BAttackSide side)
        {
            if (layout.JointSite == null || !side.Exposed || side.Faces.Count == 0)
                return new P4BAccessFrontage[0];
            GridPoint outward = side.Outward, tangent = new GridPoint(-outward.Z, outward.X);
            HashSet<GridPoint> publicCells = new HashSet<GridPoint>(
                ProceduralLayoutValidator.PublicCirculation(input, layout));
            if (publicCells.Count == 0) return new P4BAccessFrontage[0];
            HashSet<GridPoint> assets = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Bounds.Points())
                .Concat(layout.PowerYards.SelectMany(y => y.Cells.Concat(y.ZoneCells)))
                .Concat(layout.Courts.SelectMany(c => c.Cells)).Concat(layout.ExtraObstacles)
                .Concat(layout.ExhaustPockets.Select(p => p.Cell)).Concat(layout.ExhaustPocketWalls)
                .Concat(input.Resources.GeyserFootprints()));
            HashSet<GridPoint> field = new HashSet<GridPoint>(layout.JointSite.Field.Points()
                .Concat(layout.JointSite.AllPostClearance)
                .Concat(layout.JointSite.Interfaces.SelectMany(i => i.Lane.Concat(i.PostAccess))));
            LayoutTerrainGrid terrain = LayoutTerrainGrid.Build(input.Terrain,
                input.Fortification.PlacementMargin, input.Resources.GeyserFootprints());
            GridRect bounds = layout.BoundingRect;
            Func<GridPoint, bool> near = c => c.X >= bounds.MinX - 8 && c.X <= bounds.MaxX + 8 &&
                c.Z >= bounds.MinZ - 8 && c.Z <= bounds.MaxZ + 8;
            Func<GridPoint, bool> free = c => near(c) && terrain.CanPlace(c) &&
                terrain.At(c) == LayoutCellClass.Open && !assets.Contains(c) && !field.Contains(c);
            int setback = input.Fortification.Setback;
            // The terminal's buffer makes a straight wall with four cells on
            // either flank, sufficient for the existing short covered post.
            int halfTip = Math.Max(2, 4 - setback);
            int front = layout.Enclosure.Concat(layout.BuiltWalls).Concat(layout.Gates.Select(g => g.Cell))
                .Max(c => c.X * outward.X + c.Z * outward.Z) + 1;
            int low = side.Faces.Min(c => c.X * tangent.X + c.Z * tangent.Z);
            int high = side.Faces.Max(c => c.X * tangent.X + c.Z * tangent.Z);
            HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            var terminals = Enumerable.Range(0, Math.Min(12, high - low + 1)).Select(i =>
            {
                int lateral = low + (high - low) * i / Math.Max(1, Math.Min(12, high - low + 1) - 1);
                GridPoint terminal = new GridPoint(outward.X * front + tangent.X * lateral,
                    outward.Z * front + tangent.Z * lateral);
                GridPoint gate = At(terminal, outward, tangent, setback + 1, 0);
                int shots = new[] { -1, 1 }.Max(flank => side.Samples.Count(c =>
                    P4BFireLane.CanEngage(At(gate, outward, tangent, 2, flank * 3), c, 8, 25, opaque)));
                return new { Terminal = terminal, Shots = shots };
            }).Distinct().OrderByDescending(c => c.Shots)
                .ThenBy(c => Math.Abs(c.Terminal.X * tangent.X + c.Terminal.Z * tangent.Z - (low + high) / 2))
                .ThenBy(c => c.Terminal);
            List<P4BAccessFrontage> candidates = new List<P4BAccessFrontage>();
            foreach (var option in terminals)
            {
                if (option.Shots < 4) continue;
                List<GridPoint> tip = Enumerable.Range(-halfTip, halfTip * 2 + 1)
                    .Select(l => At(option.Terminal, outward, tangent, 0, l)).ToList();
                // The future wall, door, return, route and screens must fit on
                // existing dry terrain. No bridge or excavation is implied.
                List<GridPoint> frontage = Enumerable.Range(0, setback + 5).SelectMany(f =>
                    Enumerable.Range(-4, 9).Select(l => At(option.Terminal, outward, tangent, f, l))).ToList();
                if (frontage.Any(c => !free(c))) continue;
                Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>
                    { [option.Terminal] = option.Terminal };
                Queue<GridPoint> queue = new Queue<GridPoint>();
                queue.Enqueue(option.Terminal);
                GridPoint? joined = null;
                int visited = 0;
                while (queue.Count > 0 && visited++ < input.Budget.MaxRouteNodes)
                {
                    GridPoint here = queue.Dequeue();
                    if (publicCells.Contains(here)) { joined = here; break; }
                    foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                    {
                        GridPoint next = new GridPoint(here.X + step.X, here.Z + step.Z);
                        // Stay behind the terminal; the public path cannot
                        // consume the future outside position or its screens.
                        if (!free(next) || previous.ContainsKey(next) ||
                            next.X * outward.X + next.Z * outward.Z > front) continue;
                        previous[next] = here; queue.Enqueue(next);
                    }
                }
                if (!joined.HasValue) continue;
                List<GridPoint> route = new List<GridPoint>();
                for (GridPoint c = joined.Value; ; c = previous[c])
                { route.Add(c); if (c.Equals(option.Terminal)) break; }
                if (route.Count > 80) continue;
                List<GridPoint> cells = route.Concat(tip).Distinct().ToList();
                // Enclosing the new street also needs legal buffer and wall
                // ground, and cannot absorb field or existing protected space.
                if (cells.Any(c => Enumerable.Range(-setback - 1, 2 * setback + 3).Any(dx =>
                    Enumerable.Range(-setback - 1, 2 * setback + 3).Any(dz =>
                    {
                        GridPoint n = new GridPoint(c.X + dx, c.Z + dz);
                        return !terrain.InBounds(n) || field.Contains(n) ||
                            (!layout.Enclosure.Contains(n) && !layout.BuiltWalls.Contains(n) &&
                             terrain.At(n) != LayoutCellClass.Open);
                    })))) continue;
                candidates.Add(new P4BAccessFrontage(outward, option.Terminal, joined.Value, route, tip, option.Shots));
                if (candidates.Count == 4) break;
            }
            return candidates;
        }

        private static GridPoint At(GridPoint origin, GridPoint outward, GridPoint tangent, int forward, int lateral) =>
            new GridPoint(origin.X + outward.X * forward + tangent.X * lateral,
                origin.Z + outward.Z * forward + tangent.Z * lateral);
    }
}
