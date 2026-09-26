using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P4BFieldWallPlan
    {
        internal P4BFieldWallPlan(int gateIndex, IEnumerable<GridPoint> fields,
            IEnumerable<GridPoint> walls, GridPoint outerGate, IEnumerable<GridPoint> exteriorPosts,
            GridPoint? innerGate = null, IEnumerable<GridPoint> access = null,
            IEnumerable<GridPoint> arrivalRoute = null)
        {
            GateIndex = gateIndex;
            Fields = new ReadOnlyCollection<GridPoint>(fields.OrderBy(c => c).ToList());
            Walls = new ReadOnlyCollection<GridPoint>(walls.Distinct().OrderBy(c => c).ToList());
            OuterGate = outerGate;
            ExteriorPosts = new ReadOnlyCollection<GridPoint>(exteriorPosts.OrderBy(c => c).ToList());
            InnerGate = innerGate;
            Access = new ReadOnlyCollection<GridPoint>((access ?? Enumerable.Empty<GridPoint>()).Distinct().OrderBy(c => c).ToList());
            ArrivalRoute = new ReadOnlyCollection<GridPoint>((arrivalRoute ?? Enumerable.Empty<GridPoint>()).ToList());
            ulong hash = StableHash.Add(StableHash.Start, "p4b-field-wall-1");
            hash = StableHash.Add(hash, gateIndex);
            hash = StableHash.Add(hash, outerGate.X); hash = StableHash.Add(hash, outerGate.Z);
            hash = StableHash.Add(hash, innerGate.HasValue ? innerGate.Value.X : -1);
            hash = StableHash.Add(hash, innerGate.HasValue ? innerGate.Value.Z : -1);
            foreach (GridPoint c in Fields.Concat(Walls).Concat(ExteriorPosts).Concat(Access).Concat(ArrivalRoute))
            { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            Digest = hash.ToString("X16");
        }

        public int GateIndex { get; }
        public IReadOnlyList<GridPoint> Fields { get; }
        public IReadOnlyList<GridPoint> Walls { get; }
        public GridPoint OuterGate { get; }
        public IReadOnlyList<GridPoint> ExteriorPosts { get; }
        public GridPoint? InnerGate { get; }
        public IReadOnlyList<GridPoint> Access { get; }
        public IReadOnlyList<GridPoint> ArrivalRoute { get; }
        public string Digest { get; }
    }

    // A small, bounded field bailey: two stepped returns join a gatehouse's
    // outermost wall. Its gate follows the exterior route where that route
    // crosses the new wall, or occupies the far wall when the route bends away.
    // This is a pure candidate generator, not yet a spawn authorization.
    public static class P4BFieldWallPlanner
    {
        // Consume the preselected field rather than searching for leftover
        // farmland after the compound has already packed its rooms. A pair is
        // rejected, without mutation, if its actual gate/perimeter cannot join.
        public static P4BFieldWallPlan PlanJoint(ProceduralLayoutInput input, ProceduralLayout layout,
            IEnumerable<GridPoint> occupied, out string diagnostic)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            P4BJointSite site = layout.JointSite;
            if (site == null) { diagnostic = "joint_site_missing"; return null; }
            HashSet<GridPoint> blocked = new HashSet<GridPoint>(occupied ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> baseline = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
                .Concat(layout.OuterWalls).Concat(layout.Gates.SelectMany(g => g.BandWalls)));
            HashSet<GridPoint> doors = new HashSet<GridPoint>(layout.Gates.Select(g => g.Cell)
                .Concat(layout.Gates.SelectMany(g => g.BandDoors)));
            HashSet<GridPoint> field = new HashSet<GridPoint>(site.Field.Points());
            List<string> rejected = new List<string>();
            int bestDistance = int.MaxValue;
            for (int gateIndex = 0; gateIndex < layout.Gates.Count; gateIndex++)
            {
                LayoutGate gate = layout.Gates[gateIndex];
                GridPoint tangent = new GridPoint(gate.Outward.Z, gate.Outward.X);
                int offset = 2 * (layout.WallLayers - 1);
                int neck = offset == 0 ? 1 : 2;
                GridPoint leftAnchor = At(gate, tangent, -neck, offset);
                GridPoint rightAnchor = At(gate, tangent, neck, offset);
                if (!baseline.Contains(leftAnchor) || !baseline.Contains(rightAnchor))
                { rejected.Add("anchor"); continue; }
                foreach (P4BFieldInterface option in site.Interfaces.OrderBy(i =>
                    Math.Abs(i.InnerGate.X - gate.Cell.X) + Math.Abs(i.InnerGate.Z - gate.Cell.Z)))
                {
                    GridPoint near = option.InnerGate;
                    int forward = (near.X - gate.Cell.X) * gate.Outward.X +
                        (near.Z - gate.Cell.Z) * gate.Outward.Z;
                    int lateral = Math.Abs((near.X - gate.Cell.X) * tangent.X +
                        (near.Z - gate.Cell.Z) * tangent.Z);
                    if (forward <= offset + 2 || lateral != 0 ||
                        !option.OuterGate.Equals(new GridPoint(near.X + gate.Outward.X * (site.Field.Width - 1),
                            near.Z + gate.Outward.Z * (site.Field.Height - 1))))
                    { rejected.Add("gate_faces_away_or_offset"); continue; }
                    bestDistance = Math.Min(bestDistance, forward);
                    HashSet<GridPoint> ring = new HashSet<GridPoint>(site.Wall);
                    ring.Remove(option.InnerGate);
                    ring.Remove(option.OuterGate);
                    List<GridPoint> edge = site.Wall.Where(c =>
                        gate.Outward.X != 0 ? c.X == option.InnerGate.X : c.Z == option.InnerGate.Z)
                        .OrderBy(c => c.X * tangent.X + c.Z * tangent.Z).ToList();
                    if (edge.Count < 3) { rejected.Add("field_edge"); continue; }
                    List<GridPoint> connectors = new List<GridPoint>();
                    HashSet<GridPoint> leftArc = RingArc(ring, edge[0]);
                    HashSet<GridPoint> rightArc = RingArc(ring, edge[edge.Count - 1]);
                    if (leftArc.SetEquals(rightArc)) { rejected.Add("field_ring_not_split"); continue; }
                    if (!TouchesBaseline(leftArc, baseline))
                    {
                        List<GridPoint> left = Connector(leftAnchor, edge[0], gate.Outward, tangent, blocked, baseline, field, input.Terrain);
                        if (left == null) { rejected.Add("connector_terrain_or_occupancy"); continue; }
                        connectors.AddRange(left);
                    }
                    if (!TouchesBaseline(rightArc, baseline))
                    {
                        List<GridPoint> right = Connector(rightAnchor, edge[edge.Count - 1], gate.Outward, tangent, blocked, baseline, field, input.Terrain);
                        if (right == null) { rejected.Add("connector_terrain_or_occupancy"); continue; }
                        connectors.AddRange(right);
                    }
                    HashSet<GridPoint> wall = new HashSet<GridPoint>(ring.Concat(connectors));
                    wall.ExceptWith(baseline);
                    if (wall.Overlaps(doors) || wall.Overlaps(option.Lane) || wall.Overlaps(option.Crops) ||
                        wall.Contains(option.InnerGate) || wall.Contains(option.OuterGate))
                    { rejected.Add("wall_overlap"); continue; }
                    if (Enumerable.Range(1, forward - 1).Any(k =>
                    {
                        GridPoint c = At(gate, tangent, 0, k);
                        return blocked.Contains(c) || baseline.Contains(c) || wall.Contains(c) ||
                            LayoutTerrainGrid.Classify(input.Terrain[c]) != LayoutCellClass.Open;
                    })) { rejected.Add("compound_field_crossing_blocked"); continue; }
                    List<GridPoint> arrival = new List<GridPoint>();
                    for (int k = 2 * layout.WallLayers - 1; k <= forward; k++)
                        arrival.Add(At(gate, tangent, 0, k));
                    for (int k = 1; k < (gate.Outward.X != 0 ? site.Field.Width : site.Field.Height); k++)
                        arrival.Add(new GridPoint(near.X + gate.Outward.X * k,
                            near.Z + gate.Outward.Z * k));
                    List<GridPoint> outside = RouteOutside(option.OuterGate, gate.Outward,
                        input.Terrain, blocked, baseline, wall, doors);
                    if (outside == null || arrival.Any(c => !Walkable(input.Terrain, c) || wall.Contains(c) ||
                        baseline.Contains(c) || (blocked.Contains(c) && !c.Equals(gate.Staging))))
                    { rejected.Add("field_arrival_route"); continue; }
                    arrival.AddRange(outside);
                    HashSet<GridPoint> opaque = new HashSet<GridPoint>(baseline.Concat(wall).Concat(doors))
                        { option.InnerGate, option.OuterGate };
                    List<GridPoint> targets = Enumerable.Range(2, 20).Select(k => new GridPoint(
                        option.OuterGate.X + gate.Outward.X * k, option.OuterGate.Z + gate.Outward.Z * k))
                        .Where(input.Terrain.InBounds).ToList();
                    if (option.PostClearance.Count < 2 || option.PostClearance.Take(2).Any(p =>
                        !targets.Any(target => P4BFireLane.CanEngage(p, target, 8, 38, opaque))))
                    { rejected.Add("outside_posts_no_shot"); continue; }
                    IReadOnlyList<string> errors = P4BFieldWall.Validate(input.Terrain, option.Crops, baseline, wall,
                        doors.Concat(new[] { option.InnerGate, option.OuterGate }), option.PostClearance,
                        gate.Inside, layout.ConstructedRoof);
                    if (errors.Count != 0) { rejected.Add("validate:" + errors[0]); continue; }
                    diagnostic = "accepted gate=" + gateIndex + " forward=" + forward + " inner=" + option.InnerGate;
                    return new P4BFieldWallPlan(gateIndex, option.Crops, wall, option.OuterGate,
                        option.PostClearance, option.InnerGate, option.Lane, arrival);
                }
            }
            diagnostic = "bestGateDistance=" + (bestDistance == int.MaxValue ? -1 : bestDistance) +
                " rejected=" + string.Join(",", rejected.GroupBy(x => x).OrderBy(x => x.Key)
                    .Select(x => x.Key + ":" + x.Count()));
            return null;
        }

        private static HashSet<GridPoint> RingArc(HashSet<GridPoint> ring, GridPoint start)
        {
            HashSet<GridPoint> arc = new HashSet<GridPoint>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            arc.Add(start);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                foreach (GridPoint d in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (ring.Contains(next) && arc.Add(next)) queue.Enqueue(next);
                }
            }
            return arc;
        }

        private static bool TouchesBaseline(HashSet<GridPoint> arc, HashSet<GridPoint> baseline) =>
            arc.Any(c => ProceduralLayoutPlanner.Cardinal.Any(d =>
                baseline.Contains(new GridPoint(c.X + d.X, c.Z + d.Z))));

        private static List<GridPoint> RouteOutside(GridPoint outerGate, GridPoint outward,
            TerrainSnapshot terrain, HashSet<GridPoint> occupied, HashSet<GridPoint> baseline,
            HashSet<GridPoint> wall, HashSet<GridPoint> doors)
        {
            GridPoint start = new GridPoint(outerGate.X + outward.X, outerGate.Z + outward.Z);
            if (!Walkable(terrain, start) || occupied.Contains(start) || baseline.Contains(start) || wall.Contains(start)) return null;
            Queue<GridPoint> queue = new Queue<GridPoint>();
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            queue.Enqueue(start);
            previous[start] = outerGate;
            int visited = 0;
            while (queue.Count > 0 && visited++ < 40000)
            {
                GridPoint here = queue.Dequeue();
                if (terrain[here].Has(TerrainTraits.MapEdge))
                {
                    List<GridPoint> path = new List<GridPoint>();
                    for (GridPoint p = here; !p.Equals(outerGate); p = previous[p]) path.Add(p);
                    path.Reverse();
                    return path;
                }
                foreach (GridPoint d in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (previous.ContainsKey(next) || !Walkable(terrain, next) || occupied.Contains(next) ||
                        baseline.Contains(next) || wall.Contains(next) || doors.Contains(next)) continue;
                    previous[next] = here;
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        private static bool Walkable(TerrainSnapshot terrain, GridPoint c) => terrain.InBounds(c) &&
            (terrain[c].Has(TerrainTraits.Standable) || terrain[c].Has(TerrainTraits.ClearablePlant)) &&
            !terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.NaturalRock | TerrainTraits.Occupied);

        private static List<GridPoint> Connector(GridPoint anchor, GridPoint corner, GridPoint outward,
            GridPoint tangent, HashSet<GridPoint> occupied, HashSet<GridPoint> baseline,
            HashSet<GridPoint> field, TerrainSnapshot terrain)
        {
            int minX = Math.Min(anchor.X, corner.X) - 4, maxX = Math.Max(anchor.X, corner.X) + 4;
            int minZ = Math.Min(anchor.Z, corner.Z) - 4, maxZ = Math.Max(anchor.Z, corner.Z) + 4;
            Queue<GridPoint> queue = new Queue<GridPoint>();
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            queue.Enqueue(anchor);
            previous[anchor] = anchor;
            int visited = 0;
            while (queue.Count > 0 && visited++ < 2048)
            {
                GridPoint here = queue.Dequeue();
                if (here.Equals(corner))
                {
                    List<GridPoint> result = new List<GridPoint>();
                    for (GridPoint p = here; !p.Equals(anchor); p = previous[p])
                        if (!p.Equals(corner)) result.Add(p);
                    result.Reverse();
                    return result.Count <= Math.Abs(anchor.X - corner.X) + Math.Abs(anchor.Z - corner.Z) + 8
                        ? result : null;
                }
                foreach (GridPoint d in ProceduralLayoutPlanner.Cardinal.OrderBy(d =>
                    Math.Abs(here.X + d.X - corner.X) + Math.Abs(here.Z + d.Z - corner.Z)))
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (previous.ContainsKey(next) || next.X < minX || next.X > maxX || next.Z < minZ || next.Z > maxZ)
                        continue;
                    if (!next.Equals(corner) && (!terrain.InBounds(next) || occupied.Contains(next) ||
                        field.Contains(next) || baseline.Contains(next) ||
                        LayoutTerrainGrid.Classify(terrain[next]) != LayoutCellClass.Open))
                    {
                        continue;
                    }
                    previous[next] = here;
                    queue.Enqueue(next);
                }
            }
            return null;
        }

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
