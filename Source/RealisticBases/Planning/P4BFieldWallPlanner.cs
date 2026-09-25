using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P4BFieldWallPlan
    {
        internal P4BFieldWallPlan(int gateIndex, IEnumerable<GridPoint> fields,
            IEnumerable<GridPoint> walls, GridPoint outerGate, IEnumerable<GridPoint> exteriorPosts)
        {
            GateIndex = gateIndex;
            Fields = new ReadOnlyCollection<GridPoint>(fields.OrderBy(c => c).ToList());
            Walls = new ReadOnlyCollection<GridPoint>(walls.Distinct().OrderBy(c => c).ToList());
            OuterGate = outerGate;
            ExteriorPosts = new ReadOnlyCollection<GridPoint>(exteriorPosts.OrderBy(c => c).ToList());
        }

        public int GateIndex { get; }
        public IReadOnlyList<GridPoint> Fields { get; }
        public IReadOnlyList<GridPoint> Walls { get; }
        public GridPoint OuterGate { get; }
        public IReadOnlyList<GridPoint> ExteriorPosts { get; }
    }

    // A small, bounded field bailey: two stepped returns join a gatehouse's
    // outermost wall. Its gate follows the exterior route where that route
    // crosses the new wall, or occupies the far wall when the route bends away.
    // This is a pure candidate generator, not yet a spawn authorization.
    public static class P4BFieldWallPlanner
    {
        public static P4BFieldWallPlan Plan(ProceduralLayoutInput input, ProceduralLayout layout,
            IEnumerable<GridPoint> occupied)
        {
            string ignored;
            return Plan(input, layout, occupied, out ignored);
        }

        public static P4BFieldWallPlan Plan(ProceduralLayoutInput input, ProceduralLayout layout,
            IEnumerable<GridPoint> occupied, out string diagnostic)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            HashSet<GridPoint> blocked = new HashSet<GridPoint>(occupied ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> baseline = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
                .Concat(layout.OuterWalls).Concat(layout.Gates.SelectMany(g => g.BandWalls)));
            HashSet<GridPoint> doors = new HashSet<GridPoint>(layout.Gates.Select(g => g.Cell)
                .Concat(layout.Gates.SelectMany(g => g.BandDoors)));
            HashSet<GridPoint> roof = new HashSet<GridPoint>(layout.ConstructedRoof);
            Dictionary<string, int> rejection = new Dictionary<string, int>(StringComparer.Ordinal);
            int bestField = 0;
            string firstBlocked = null;
            Action<string> reject = reason =>
            {
                int count;
                rejection.TryGetValue(reason, out count);
                rejection[reason] = count + 1;
            };
            for (int gateIndex = 0; gateIndex < layout.Gates.Count; gateIndex++)
            {
                LayoutGate gate = layout.Gates[gateIndex];
                GridPoint tangent = new GridPoint(gate.Outward.Z, gate.Outward.X);
                int anchor = 2 * (layout.WallLayers - 1);
                int neck = anchor == 0 ? 1 : 2;
                if (!baseline.Contains(At(gate, tangent, -neck, anchor)) ||
                    !baseline.Contains(At(gate, tangent, neck, anchor))) { reject("anchor"); continue; }
                for (int halfWidth = 7; halfWidth <= 12; halfWidth++)
                    for (int depth = 12; depth <= 26; depth++)
                    {
                        HashSet<GridPoint> wall = new HashSet<GridPoint>();
                        for (int side = -1; side <= 1; side += 2)
                        {
                            int previous = neck;
                            for (int step = 1; step <= depth; step++)
                            {
                                int current = Math.Min(halfWidth, neck + step);
                                for (int j = previous; j <= current; j++)
                                    wall.Add(At(gate, tangent, side * j, anchor + step));
                                previous = current;
                            }
                        }
                        for (int j = -halfWidth; j <= halfWidth; j++)
                            wall.Add(At(gate, tangent, j, anchor + depth));
                        // A stepped return may run through cells of the existing
                        // irregular perimeter. Those are already solid wall, not
                        // new construction or an occupancy conflict.
                        wall.ExceptWith(baseline);
                        List<GridPoint> crossings = gate.ExteriorRoute.Where(wall.Contains).Distinct().ToList();
                        if (crossings.Count > 1) { reject("route_crossings"); continue; }
                        GridPoint outerDoor = crossings.Count == 1 ? crossings[0] :
                            At(gate, tangent, 0, anchor + depth);
                        wall.Remove(outerDoor);
                        GridPoint blockedWall = wall.FirstOrDefault(c => !Free(c));
                        if (!Free(outerDoor) || wall.Any(c => !Free(c)))
                        {
                            if (firstBlocked == null)
                            {
                                GridPoint bad = !Free(outerDoor) ? outerDoor : blockedWall;
                                firstBlocked = "gate=" + gateIndex + " anchor=" + anchor + " cell=" + bad +
                                    " blocked=" + blocked.Contains(bad) + " traits=" +
                                    (input.Terrain.InBounds(bad) ? input.Terrain[bad].Traits.ToString() : "out_of_bounds");
                            }
                            reject("wall_terrain_or_occupancy"); continue;
                        }
                        List<GridPoint> field = new List<GridPoint>();
                        HashSet<GridPoint> route = new HashSet<GridPoint>(gate.ExteriorRoute);
                        for (int step = 2; step < depth; step++)
                        {
                            int width = Math.Min(halfWidth, neck + step);
                            for (int j = -width + 2; j <= width - 2; j++)
                            {
                                GridPoint c = At(gate, tangent, j, anchor + step);
                                if (route.Contains(c) || blocked.Contains(c) || wall.Contains(c) ||
                                    !input.Terrain.InBounds(c) || !input.Terrain[c].Has(TerrainTraits.Fertile) ||
                                    input.Terrain[c].Has(TerrainTraits.Roofed) || roof.Contains(c) || !Free(c)) continue;
                                field.Add(c);
                            }
                        }
                        bestField = Math.Max(bestField, field.Count);
                        if (field.Count < layout.FieldCellsRequired) { reject("fertile_field_area"); continue; }
                        HashSet<GridPoint> opaque = new HashSet<GridPoint>(baseline.Concat(wall).Concat(doors))
                            { outerDoor };
                        List<GridPoint> approach = gate.ExteriorRoute.SkipWhile(c => !c.Equals(outerDoor))
                            .Skip(1).Take(30).ToList();
                        if (approach.Count == 0 || crossings.Count == 0)
                            approach = Enumerable.Range(1, 20).Select(k =>
                                At(gate, tangent, 0, anchor + depth + k)).Where(input.Terrain.InBounds).ToList();
                        List<GridPoint> posts = new List<GridPoint>();
                        for (int radius = 2; radius <= 5 && posts.Count < 2; radius++)
                            foreach (int side in new[] { -1, 1 })
                            {
                                GridPoint post = At(gate, tangent, side * radius, anchor + depth + 1);
                                if (posts.Contains(post) || !Free(post) || route.Contains(post) ||
                                    !approach.Any(target => P4BFireLane.CanEngage(post, target, 8, 38, opaque))) continue;
                                posts.Add(post);
                            }
                        if (posts.Count != 2) { reject("exterior_firing_posts"); continue; }
                        List<GridPoint> chosen = field.Take(layout.FieldCellsRequired).ToList();
                        IReadOnlyList<string> validation = P4BFieldWall.Validate(input.Terrain, chosen, baseline, wall,
                            doors.Concat(new[] { outerDoor }), posts, gate.Inside, roof);
                        if (validation.Count != 0) { reject("validation:" + validation[0]); continue; }
                        diagnostic = "accepted gate=" + gateIndex + " halfWidth=" + halfWidth + " depth=" + depth;
                        return new P4BFieldWallPlan(gateIndex, chosen, wall, outerDoor, posts);
                    }
            }
            diagnostic = "bestField=" + bestField + "/" + layout.FieldCellsRequired + " firstBlocked=" + firstBlocked + " rejected=" +
                string.Join(",", rejection.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + ":" + kv.Value));
            return null;

            bool Free(GridPoint c) => input.Terrain.InBounds(c) && !blocked.Contains(c) &&
                !input.Terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.NaturalRock |
                    TerrainTraits.Occupied | TerrainTraits.MapEdge) &&
                input.Terrain[c].Has(TerrainTraits.Buildable) &&
                (input.Terrain[c].Has(TerrainTraits.Standable) ||
                 input.Terrain[c].Has(TerrainTraits.ClearablePlant));
        }

        private static GridPoint At(LayoutGate gate, GridPoint tangent, int lateral, int forward) =>
            new GridPoint(gate.Cell.X + tangent.X * lateral + gate.Outward.X * forward,
                gate.Cell.Z + tangent.Z * lateral + gate.Outward.Z * forward);
    }
}
