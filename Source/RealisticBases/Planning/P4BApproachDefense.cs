using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P4BAttackSide
    {
        internal P4BAttackSide(string id, GridPoint outward, IEnumerable<GridPoint> faces,
            IEnumerable<GridPoint> samples, GridPoint? post, string omission)
        {
            Id = id; Outward = outward;
            Faces = Copy(faces); Samples = Copy(samples);
            Post = post; OmissionReason = omission ?? string.Empty;
        }
        public string Id { get; }
        public GridPoint Outward { get; }
        public IReadOnlyList<GridPoint> Faces { get; }
        public IReadOnlyList<GridPoint> Samples { get; }
        public GridPoint? Post { get; }
        public string OmissionReason { get; }
        public bool Exposed => Samples.Count > 0;
        internal static IReadOnlyList<GridPoint> Copy(IEnumerable<GridPoint> cells) =>
            new ReadOnlyCollection<GridPoint>(cells.Distinct().OrderBy(c => c).ToList());
    }

    public sealed class P4BPerimeterPost
    {
        internal P4BPerimeterPost(string side, GridPoint outward, GridPoint face, GridPoint cell,
            GridPoint gate, IEnumerable<GridPoint> route, IEnumerable<GridPoint> cover, GridPoint wallReturn)
        {
            Side = side; Outward = outward; Face = face; Cell = cell; AccessGate = gate;
            Route = new ReadOnlyCollection<GridPoint>(route.ToList());
            CoverCells = P4BAttackSide.Copy(cover);
            WallReturn = wallReturn;
        }
        public string Side { get; }
        public GridPoint Outward { get; }
        public GridPoint Face { get; }
        public GridPoint Cell { get; }
        public GridPoint AccessGate { get; }
        public GridPoint WallReturn { get; }
        // Two gate-axis cells are exposed; the remaining lateral approach
        // runs behind front screens. Cover is not invulnerability.
        public IReadOnlyList<GridPoint> Route { get; }
        public IReadOnlyList<GridPoint> CoverCells { get; }
    }

    // A defense-only sally port in an ordinary one-cell built perimeter wall.
    // It is not a civilian arrival gate or a bypass through reinforced bands.
    public sealed class P4BSallyPort
    {
        internal P4BSallyPort(GridPoint cell, GridPoint inside, GridPoint outward)
        { Cell = cell; Inside = inside; Outward = outward; }
        public GridPoint Cell { get; }
        public GridPoint Inside { get; }
        public GridPoint Outward { get; }
    }

    public sealed class P4BApproachDefensePlan
    {
        private readonly HashSet<GridPoint> attackerReachable;
        internal P4BApproachDefensePlan(IEnumerable<GridPoint> reachable,
            IEnumerable<P4BAttackSide> sides, IEnumerable<P4BPerimeterPost> posts,
            IEnumerable<GridPoint> transit = null, IEnumerable<P4BSallyPort> sallyPorts = null)
        {
            attackerReachable = new HashSet<GridPoint>(reachable);
            Sides = new ReadOnlyCollection<P4BAttackSide>(sides.ToList());
            Posts = new ReadOnlyCollection<P4BPerimeterPost>(posts.ToList());
            TransitCells = P4BAttackSide.Copy(transit ?? Enumerable.Empty<GridPoint>());
            SallyPorts = new ReadOnlyCollection<P4BSallyPort>((sallyPorts ?? Enumerable.Empty<P4BSallyPort>()).ToList());
            ulong hash = StableHash.Add(StableHash.Start, "p4b-approach-defense-v1");
            foreach (P4BAttackSide side in Sides)
            {
                hash = StableHash.Add(hash, side.Id); hash = StableHash.Add(hash, side.OmissionReason);
                foreach (GridPoint c in side.Faces.Concat(side.Samples)
                    .Concat(side.Post.HasValue ? new[] { side.Post.Value } : new GridPoint[0]))
                { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            }
            foreach (P4BPerimeterPost post in Posts)
                foreach (GridPoint c in new[] { post.Face, post.Cell, post.AccessGate, post.Outward, post.WallReturn }
                    .Concat(post.Route).Concat(post.CoverCells))
                { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            foreach (GridPoint c in TransitCells)
            { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            foreach (P4BSallyPort port in SallyPorts)
                foreach (GridPoint c in new[] { port.Cell, port.Inside, port.Outward })
                { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            Digest = hash.ToString("X16");
        }
        public IReadOnlyList<P4BAttackSide> Sides { get; }
        public IReadOnlyList<P4BPerimeterPost> Posts { get; }
        public IReadOnlyList<GridPoint> TransitCells { get; }
        public IReadOnlyList<P4BSallyPort> SallyPorts { get; }
        public IEnumerable<GridPoint> CoverCells => Posts.SelectMany(p => p.CoverCells).Distinct();
        public IEnumerable<GridPoint> AllPosts => Sides.Where(s => s.Post.HasValue).Select(s => s.Post.Value).Distinct();
        public bool AttackerCanReach(GridPoint cell) => attackerReachable.Contains(cell);
        internal P4BApproachDefensePlan WithoutTransit(IEnumerable<GridPoint> blocked) =>
            new P4BApproachDefensePlan(attackerReachable, Sides, Posts, TransitCells.Except(blocked), SallyPorts);
        public string Digest { get; }
    }

    // Conservative conventional land-attack exposure, not a raid predictor:
    // edge-connected ground and firing samples along actual perimeter faces.
    // Water/rock do not imply safety if reachable ground across them is in range.
    // Drop pods, sappers and future demolition can expose additional fronts.
    public static class P4BApproachDefensePlanner
    {
        private static readonly string[] Names = { "south", "east", "north", "west" };
        private static readonly GridPoint[] Directions =
        { new GridPoint(0, -1), new GridPoint(1, 0), new GridPoint(0, 1), new GridPoint(-1, 0) };
        private sealed class GateOption
        {
            internal GridPoint Cell, Outward;
            internal P4BSallyPort Port;
        }
        private sealed class PositionOption
        {
            internal P4BPerimeterPost Post;
            internal P4BSallyPort Port;
            internal int Shots, Fronts;
        }

        public static HashSet<GridPoint> Opaque(ProceduralLayout layout, TerrainSnapshot terrain = null) => new HashSet<GridPoint>(
            layout.BuiltWalls.Concat(layout.NaturalWalls).Concat(layout.OuterWalls)
                .Concat(layout.Gates.SelectMany(g => g.BandWalls.Concat(g.BandDoors)))
                .Concat(layout.Gates.Select(g => g.Cell)).Concat(layout.ExhaustPocketWalls)
                .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points().Where(r.IsEdge)))
                .Concat(layout.FieldWall == null ? Enumerable.Empty<GridPoint>() : layout.FieldWall.Walls
                    .Concat(new[] { layout.FieldWall.InnerGate.Value, layout.FieldWall.OuterGate }))
                .Concat(layout.ApproachDefense?.SallyPorts.Select(p => p.Cell) ?? new GridPoint[0])
                .Concat(terrain == null ? Enumerable.Empty<GridPoint>() : terrain.Points().Where(c =>
                    terrain[c].HasAny(TerrainTraits.NaturalRock | TerrainTraits.Occupied) &&
                    !terrain[c].Has(TerrainTraits.ClearablePlant) && !layout.Excavation.Contains(c))));

        public static IReadOnlyList<GridPoint> AccessGates(ProceduralLayout layout) =>
            P4BAttackSide.Copy(layout.Gates.Select(g => g.BandDoors.Concat(new[] { g.Cell })
                .OrderByDescending(c => c.X * g.Outward.X + c.Z * g.Outward.Z).First())
                .Concat(layout.FieldWall == null ? Enumerable.Empty<GridPoint>() : new[] { layout.FieldWall.OuterGate })
                .Concat(layout.ApproachDefense?.SallyPorts.Select(p => p.Cell) ?? new GridPoint[0]));

        public static P4BApproachDefensePlan Plan(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            HashSet<GridPoint> opaque = Opaque(layout, input.Terrain);
            HashSet<GridPoint> interior = new HashSet<GridPoint>(layout.Enclosure.Concat(
                layout.JointSite == null ? Enumerable.Empty<GridPoint>() : layout.JointSite.Field.Points()));
            HashSet<GridPoint> occupied = new HashSet<GridPoint>(opaque.Concat(interior)
                .Concat(layout.PowerYards.SelectMany(y => y.Cells)).Concat(layout.ExtraObstacles));
            HashSet<GridPoint> reachable = FloodEdges(input.Terrain, occupied);
            HashSet<GridPoint> perimeter = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
                .Concat(layout.OuterWalls).Concat(layout.Gates.SelectMany(g => g.BandWalls))
                .Concat(layout.FieldWall == null ? Enumerable.Empty<GridPoint>() : layout.FieldWall.Walls
                    .Except(layout.FieldWall.PostAccess.Select(p => p.WallReturn))));
            List<P4BAttackSide> exposed = new List<P4BAttackSide>();
            for (int direction = 0; direction < Directions.Length; direction++)
            {
                GridPoint outward = Directions[direction], tangent = new GridPoint(-outward.Z, outward.X);
                List<GridPoint> faces = perimeter.Where(c => !perimeter.Contains(At(c, outward, tangent, 1, 0)) &&
                    Enumerable.Range(1, 5).Any(k => interior.Contains(At(c, outward, tangent, -k, 0))))
                    .OrderBy(c => c.X * tangent.X + c.Z * tangent.Z).ThenBy(c => c).ToList();
                List<GridPoint> probes = new List<GridPoint>();
                foreach (GridPoint face in Spread(faces, 12))
                    foreach (int forward in new[] { 6, 12, 20, 30, 40 })
                        foreach (int lateral in new[] { -4, 0, 4 })
                        {
                            GridPoint c = At(face, outward, tangent, forward, lateral);
                            if (reachable.Contains(c) && P4BFireLane.CanEngage(
                                At(face, outward, tangent, 1, 0), c, 0, 45, opaque)) probes.Add(c);
                        }
                exposed.Add(new P4BAttackSide(Names[direction], outward, faces, probes, null,
                    probes.Count == 0 ? "no_edge_reachable_approach_samples" : null));
            }
            // Routes remain close to the perimeter, not shortcuts through a
            // field/room or arbitrarily far across the map. Doors are roots.
            HashSet<GridPoint> navigation = new HashSet<GridPoint>();
            foreach (GridPoint face in perimeter)
                for (int dz = -6; dz <= 6; dz++)
                    for (int dx = -6; dx <= 6; dx++)
                    {
                        GridPoint c = new GridPoint(face.X + dx, face.Z + dz);
                        if (reachable.Contains(c)) navigation.Add(c);
                    }
            IReadOnlyList<GridPoint> roots = AccessGates(layout);
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint root in roots) { previous[root] = root; queue.Enqueue(root); }
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                foreach (GridPoint d in Directions)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (!navigation.Contains(next) || previous.ContainsKey(next)) continue;
                    previous[next] = here; queue.Enqueue(next);
                }
            }
            List<P4BPerimeterPost> posts = new List<P4BPerimeterPost>();
            List<P4BSallyPort> ports = new List<P4BSallyPort>();
            HashSet<GridPoint> reserved = new HashSet<GridPoint>(layout.FieldWall?.CoverCells ?? new GridPoint[0]);
            reserved.UnionWith(layout.FieldWall?.ExteriorPosts ?? new GridPoint[0]);
            reserved.UnionWith(layout.JointSite?.AllPostClearance ?? new GridPoint[0]);
            HashSet<GridPoint> arrival = new HashSet<GridPoint>(layout.Gates.SelectMany(g => g.ExteriorRoute));
            HashSet<GridPoint> publicCells = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input, layout)
                .Except(layout.PowerYards.SelectMany(y => y.Cells)));
            HashSet<GridPoint> reinforcement = new HashSet<GridPoint>(layout.OuterWalls.Concat(
                layout.Gates.SelectMany(g => g.BandWalls.Concat(g.BandDoors).Concat(g.OuterVestibule)))
                .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex]))));
            List<P4BAttackSide> sides = new List<P4BAttackSide>();
            foreach (P4BAttackSide side in exposed)
            {
                if (!side.Exposed) { sides.Add(side); continue; }
                GridPoint? existing = layout.FieldWall != null
                    ? layout.FieldWall.UsablePosts.Cast<GridPoint?>().FirstOrDefault(p =>
                        side.Faces.Any(face => Math.Abs(face.X - p.Value.X) + Math.Abs(face.Z - p.Value.Z) <= 6) &&
                        layout.FieldWall.CoverCells.Contains(new GridPoint(p.Value.X + side.Outward.X, p.Value.Z + side.Outward.Z)) &&
                        side.Samples.Count(c => P4BFireLane.CanEngage(p.Value, c, 8, 25, opaque)) >= 4)
                    : null;
                if (existing.HasValue)
                {
                    sides.Add(new P4BAttackSide(side.Id, side.Outward, side.Faces, side.Samples, existing, null));
                    continue;
                }
                // A dry corner shoulder can defend two faces. Reuse it only
                // when it is physically near this face, has adjacent cover on
                // this incoming-fire side and actually sees its approaches.
                P4BPerimeterPost shared = posts.FirstOrDefault(p => side.Faces.Any(face =>
                    Math.Abs(face.X - p.Cell.X) + Math.Abs(face.Z - p.Cell.Z) <= 6) &&
                    p.CoverCells.Contains(new GridPoint(p.Cell.X + side.Outward.X, p.Cell.Z + side.Outward.Z)) &&
                    side.Samples.Count(c => P4BFireLane.CanEngage(p.Cell, c, 8, 25, opaque)) >= 4);
                if (shared != null)
                {
                    sides.Add(new P4BAttackSide(side.Id, side.Outward, side.Faces, side.Samples, shared.Cell, null));
                    continue;
                }
                List<GateOption> gates = layout.Gates
                    .Select(g => new GateOption { Outward = g.Outward, Cell = g.BandDoors.Concat(new[] { g.Cell })
                        .OrderByDescending(c => c.X * g.Outward.X + c.Z * g.Outward.Z).First() }).ToList();
                if (layout.FieldWall != null)
                    gates.Add(new GateOption { Cell = layout.FieldWall.OuterGate,
                        Outward = layout.Gates[layout.FieldWall.GateIndex].Outward });
                // Only one-cell built perimeter walls with a public cell
                // immediately inside can become defense sally ports. Never
                // cut a room, rock, gate band or reinforced wall section.
                foreach (P4BAttackSide faceSide in exposed)
                {
                    GridPoint across = new GridPoint(-faceSide.Outward.Z, faceSide.Outward.X);
                    List<GridPoint> portFaces = faceSide.Faces.Where(face => layout.BuiltWalls.Contains(face) &&
                        !ports.Any(p => p.Cell.Equals(face)) && !reinforcement.Contains(face) && Walkable(input.Terrain, face) &&
                        input.Terrain[face].Has(TerrainTraits.Buildable) &&
                        publicCells.Contains(At(face, faceSide.Outward, across, -1, 0)) &&
                        reachable.Contains(At(face, faceSide.Outward, across, 1, 0))).ToList();
                    gates.AddRange(Spread(portFaces, 24).Select(face => new GateOption { Cell = face, Outward = faceSide.Outward,
                        Port = new P4BSallyPort(face, At(face, faceSide.Outward, across, -1, 0), faceSide.Outward) }));
                }
                List<PositionOption> candidates = new List<PositionOption>();
                foreach (GateOption gate in gates)
                    foreach (int flank in new[] { -1, 1 })
                    {
                        GridPoint tangent = new GridPoint(-gate.Outward.Z, gate.Outward.X);
                        Func<int, int, GridPoint> local = (f, l) => At(gate.Cell, gate.Outward, tangent, f, flank * l);
                        GridPoint cell = local(2, 3), rear = local(1, 4);
                        List<GridPoint> cover = Enumerable.Range(1, 4).Select(l => local(3, l))
                            .Concat(new[] { local(2, 4) }).ToList();
                        List<GridPoint> route = new[] { gate.Cell, local(1, 0), local(2, 0) }
                            .Concat(Enumerable.Range(1, 3).Select(l => local(2, l))).ToList();
                        // A connected rear return and front/flank screens,
                        // not three isolated cover objects reached by a lap
                        // around the settlement. All transit is six cells.
                        if (!Enumerable.Range(1, 4).All(l => perimeter.Contains(local(0, l))) ||
                            route.Skip(1).Any(c => !navigation.Contains(c) || reserved.Contains(c) || occupied.Contains(c)) ||
                            cover.Concat(new[] { rear }).Any(c => occupied.Contains(c) || reserved.Contains(c) ||
                                arrival.Contains(c) || reinforcement.Contains(c) || !Buildable(input.Terrain, c)) ||
                            !Buildable(input.Terrain, cell) ||
                            !side.Faces.Any(face => Math.Abs(face.X - cell.X) + Math.Abs(face.Z - cell.Z) <= 6) ||
                            !cover.Contains(new GridPoint(cell.X + side.Outward.X, cell.Z + side.Outward.Z))) continue;
                        HashSet<GridPoint> finalOpaque = new HashSet<GridPoint>(opaque) { rear };
                        int shots = side.Samples.Count(c => P4BFireLane.CanEngage(cell, c, 8, 25, finalOpaque));
                        if (shots < 4) continue;
                        int fronts = exposed.Count(s => s.Exposed && s.Faces.Any(face =>
                            Math.Abs(face.X - cell.X) + Math.Abs(face.Z - cell.Z) <= 6) &&
                            cover.Contains(new GridPoint(cell.X + s.Outward.X, cell.Z + s.Outward.Z)) &&
                            s.Samples.Count(c => P4BFireLane.CanEngage(cell, c, 8, 25, finalOpaque)) >= 4);
                        candidates.Add(new PositionOption { Port = gate.Port, Shots = shots, Fronts = fronts,
                            Post = new P4BPerimeterPost(side.Id, gate.Outward, gate.Cell, cell, gate.Cell, route, cover, rear) });
                    }
                P4BPerimeterPost selected = null;
                string omission = "no_connected_short_gate_position";
                foreach (PositionOption candidate in candidates.OrderBy(c => c.Port == null ? 0 : 1)
                    .ThenByDescending(c => c.Fronts).ThenByDescending(c => c.Shots).ThenBy(c => c.Post.Cell))
                {
                    IEnumerable<P4BPerimeterPost> trialPosts = posts.Concat(new[] { candidate.Post });
                    IEnumerable<P4BSallyPort> trialPorts = ports.Concat(candidate.Port == null ?
                        Enumerable.Empty<P4BSallyPort>() : new[] { candidate.Port });
                    if (Materials(input, layout, trialPosts.Sum(p => p.CoverCells.Count), trialPosts, trialPorts).ImportValue >
                        input.Requirements.ImportValueLimit)
                    { omission = "cover_material_budget"; break; }
                    selected = candidate.Post;
                    posts.Add(selected); reserved.UnionWith(selected.CoverCells); reserved.UnionWith(selected.Route.Skip(1));
                    reserved.Add(selected.WallReturn); opaque.Add(selected.WallReturn);
                    if (candidate.Port != null) ports.Add(candidate.Port);
                    break;
                }
                sides.Add(new P4BAttackSide(side.Id, side.Outward, side.Faces, side.Samples,
                    selected == null ? (GridPoint?)null : selected.Cell, selected == null ? omission : null));
            }
            return new P4BApproachDefensePlan(reachable, sides, posts, previous.Keys
                .Except(posts.Select(p => p.WallReturn)).Concat(posts.SelectMany(p => p.Route)), ports);
        }

        public static LayoutMaterialLedger Materials(ProceduralLayoutInput input, ProceduralLayout layout, int extraCover,
            IEnumerable<P4BPerimeterPost> posts = null, IEnumerable<P4BSallyPort> ports = null) =>
            input.Equipment.Count(layout.Rooms, layout.BuiltWalls.Except((ports ?? Enumerable.Empty<P4BSallyPort>()).Select(p => p.Cell))
                .Concat((posts ?? Enumerable.Empty<P4BPerimeterPost>()).Select(p => p.WallReturn)).Concat(layout.OuterWalls)
                .Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(layout.ExhaustPocketWalls)
                .Concat(layout.FieldWall?.Walls ?? new GridPoint[0]),
                layout.Gates.Sum(g => 1 + g.BandDoors.Count) + (layout.FieldWall == null ? 0 : 2) +
                    (layout.ApproachDefense?.SallyPorts.Count ?? 0) + (ports?.Count() ?? 0), layout.Power,
                Enumerable.Repeat(layout.FieldWall?.CoverDefName ?? "Barricade",
                    (layout.FieldWall?.CoverCells.Count ?? 0) + extraCover));

        public static IReadOnlyList<string> Validate(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            P4BApproachDefensePlan plan = layout.ApproachDefense;
            if (plan == null) return new[] { "missing_approach_defense" };
            List<string> errors = new List<string>();
            HashSet<GridPoint> opaque = Opaque(layout, input.Terrain);
            opaque.UnionWith(layout.Turret?.All.SelectMany(t => t.Footprint) ?? new GridPoint[0]);
            HashSet<GridPoint> occupied = new HashSet<GridPoint>(opaque.Concat(layout.Enclosure)
                .Concat(layout.JointSite?.Field.Points() ?? new GridPoint[0])
                .Concat(layout.PowerYards.SelectMany(y => y.Cells)).Concat(layout.ExtraObstacles));
            HashSet<GridPoint> cover = new HashSet<GridPoint>();
            HashSet<GridPoint> publicCells = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input, layout)
                .Except(layout.PowerYards.SelectMany(y => y.Cells)));
            foreach (P4BSallyPort port in plan.SallyPorts)
            {
                if (!Directions.Contains(port.Outward) || !port.Inside.Equals(new GridPoint(
                    port.Cell.X - port.Outward.X, port.Cell.Z - port.Outward.Z)) || !publicCells.Contains(port.Inside) ||
                    layout.BuiltWalls.Contains(port.Cell) || layout.NaturalWalls.Contains(port.Cell) ||
                    layout.Enclosure.Contains(port.Cell) || layout.Rooms.Any(r => r.Bounds.Contains(port.Cell)) ||
                    layout.OuterWalls.Contains(port.Cell) || layout.Gates.Any(g => g.Cell.Equals(port.Cell) ||
                        g.BandDoors.Concat(g.BandWalls).Contains(port.Cell)) || !Walkable(input.Terrain, port.Cell) ||
                    !input.Terrain[port.Cell].Has(TerrainTraits.Buildable) ||
                    !plan.Posts.Any(p => p.AccessGate.Equals(port.Cell) && p.Outward.Equals(port.Outward)))
                    errors.Add("sally_port_public_connection");
            }
            if (plan.SallyPorts.Select(p => p.Cell).Distinct().Count() != plan.SallyPorts.Count)
                errors.Add("sally_port_duplicate");
            if (!plan.Sides.Select(s => s.Id).SequenceEqual(Names)) errors.Add("attack_side_inventory");
            if (plan.Sides.SelectMany(s => s.Samples).Any(c => !plan.AttackerCanReach(c) || !Walkable(input.Terrain, c)))
                errors.Add("unreachable_attack_sample");
            foreach (P4BPerimeterPost post in plan.Posts)
            {
                P4BAttackSide side = plan.Sides.FirstOrDefault(s => s.Id == post.Side);
                if (side == null || !side.Post.HasValue || !side.Post.Value.Equals(post.Cell) ||
                    (!side.Faces.Contains(post.Face) && !AccessGates(layout).Contains(post.Face)) ||
                    !post.CoverCells.Contains(new GridPoint(post.Cell.X + side.Outward.X, post.Cell.Z + side.Outward.Z)) ||
                    !side.Faces.Any(face => Math.Abs(face.X - post.Cell.X) + Math.Abs(face.Z - post.Cell.Z) <= 6) ||
                    side.Samples.Count(c => P4BFireLane.CanEngage(post.Cell, c, 8, 25, opaque)) < 4)
                    errors.Add("perimeter_post_no_approach_fire");
                if (post.Route.Count < 2 || !post.Route.First().Equals(post.AccessGate) ||
                    !post.Route.Last().Equals(post.Cell) || !AccessGates(layout).Contains(post.AccessGate) ||
                    post.Route.Skip(1).Any(c => occupied.Contains(c) || !Walkable(input.Terrain, c)) ||
                    post.Route.Zip(post.Route.Skip(1), (a, b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z)).Any(d => d != 1))
                    errors.Add("perimeter_post_access");
                GridPoint tangent = new GridPoint(-post.Outward.Z, post.Outward.X);
                int flank = Math.Sign((post.Cell.X - post.AccessGate.X) * tangent.X +
                    (post.Cell.Z - post.AccessGate.Z) * tangent.Z);
                Func<int, int, GridPoint> local = (f, l) => At(post.AccessGate, post.Outward, tangent, f, flank * l);
                List<GridPoint> expected = Enumerable.Range(1, 4).Select(l => local(3, l))
                    .Concat(new[] { local(2, 4) }).ToList();
                List<GridPoint> expectedRoute = new[] { post.AccessGate, local(1, 0), local(2, 0) }
                    .Concat(Enumerable.Range(1, 3).Select(l => local(2, l))).ToList();
                if (flank == 0 || !post.Cell.Equals(local(2, 3)) || !post.Route.SequenceEqual(expectedRoute) ||
                    !post.Face.Equals(post.AccessGate)) errors.Add("perimeter_post_short_access_geometry");
                if (!post.WallReturn.Equals(local(1, 4)) || !layout.BuiltWalls.Contains(post.WallReturn) ||
                    !Enumerable.Range(1, 4).All(l => opaque.Contains(local(0, l))) || !Buildable(input.Terrain, post.WallReturn) ||
                    layout.ExtraObstacles.Contains(post.WallReturn) ||
                    layout.Gates.Any(g => g.ExteriorRoute.Contains(post.WallReturn))) errors.Add("perimeter_post_return");
                if (!P4BAttackSide.Copy(expected).SequenceEqual(post.CoverCells) || post.CoverCells.Any(c =>
                    !cover.Add(c) || occupied.Contains(c) || !Buildable(input.Terrain, c) ||
                    plan.AllPosts.Contains(c) || post.Route.Contains(c) ||
                    (layout.FieldWall?.CoverCells.Contains(c) ?? false) ||
                    layout.Gates.Any(g => g.ExteriorRoute.Contains(c)))) errors.Add("perimeter_post_cover");
            }
            foreach (P4BAttackSide side in plan.Sides)
            {
                if (side.Exposed && ((!side.Post.HasValue && side.OmissionReason.Length == 0) ||
                    (side.Post.HasValue && !plan.Posts.Any(p => p.Cell.Equals(side.Post.Value)) &&
                    !(layout.FieldWall?.UsablePosts.Contains(side.Post.Value) ?? false))))
                    errors.Add("unreported_attack_side");
                if (side.Post.HasValue && side.Samples.Count(c =>
                    P4BFireLane.CanEngage(side.Post.Value, c, 8, 25, opaque)) < 4)
                    errors.Add("attack_side_post_no_fire");
            }
            if (plan.TransitCells.Any(c => !AccessGates(layout).Contains(c) &&
                (occupied.Contains(c) || !Walkable(input.Terrain, c)))) errors.Add("perimeter_transit_occupancy");
            return errors;
        }

        private static HashSet<GridPoint> FloodEdges(TerrainSnapshot terrain, HashSet<GridPoint> blocked)
        {
            HashSet<GridPoint> reach = new HashSet<GridPoint>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint c in terrain.Points().Where(c => c.X == 0 || c.Z == 0 ||
                c.X == terrain.Width - 1 || c.Z == terrain.Height - 1))
                if (Walkable(terrain, c) && !blocked.Contains(c) && reach.Add(c)) queue.Enqueue(c);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                // Conservative 8-way attacker connectivity also sees diagonal
                // approaches; post/access validation remains cardinal.
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    GridPoint next = new GridPoint(here.X + dx, here.Z + dz);
                    if (Walkable(terrain, next) && !blocked.Contains(next) && reach.Add(next)) queue.Enqueue(next);
                }
            }
            return reach;
        }
        private static IEnumerable<GridPoint> Spread(List<GridPoint> cells, int count) => cells.Count <= count
            ? cells : Enumerable.Range(0, count).Select(i => cells[i * (cells.Count - 1) / (count - 1)]);
        private static GridPoint At(GridPoint c, GridPoint outward, GridPoint tangent, int f, int l) =>
            new GridPoint(c.X + outward.X * f + tangent.X * l, c.Z + outward.Z * f + tangent.Z * l);
        private static bool Walkable(TerrainSnapshot terrain, GridPoint c) => terrain.InBounds(c) &&
            (terrain[c].Has(TerrainTraits.Standable) || terrain[c].Has(TerrainTraits.ClearablePlant)) &&
            !terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.NaturalRock | TerrainTraits.Occupied);
        private static bool Buildable(TerrainSnapshot terrain, GridPoint c) => Walkable(terrain, c) &&
            terrain[c].Has(TerrainTraits.Buildable) && !terrain[c].HasAny(TerrainTraits.Roofed | TerrainTraits.MapEdge);
    }
}
