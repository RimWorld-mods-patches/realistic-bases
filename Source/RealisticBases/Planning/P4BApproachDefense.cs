using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P4BAttackSide
    {
        internal P4BAttackSide(string id, GridPoint outward, IEnumerable<GridPoint> faces,
            IEnumerable<GridPoint> samples, GridPoint? post, string omission, int openGroundArea = 0,
            IEnumerable<GridPoint> positions = null)
        {
            Id = id; Outward = outward;
            Faces = Copy(faces); Samples = Copy(samples);
            Post = post; OmissionReason = omission ?? string.Empty;
            OpenGroundArea = openGroundArea;
            Positions = Copy(positions ?? (post.HasValue ? new[] { post.Value } : new GridPoint[0]));
        }
        public string Id { get; }
        public GridPoint Outward { get; }
        public IReadOnlyList<GridPoint> Faces { get; }
        public IReadOnlyList<GridPoint> Samples { get; }
        public GridPoint? Post { get; }
        public IReadOnlyList<GridPoint> Positions { get; }
        public string OmissionReason { get; }
        // Edge-connected cells in the exterior strip beyond this perimeter
        // face. This distinguishes a broad staging area from a narrow margin
        // even when the sampled firing-line counts happen to be similar.
        public int OpenGroundArea { get; }
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
        internal P4BSallyPort(GridPoint cell, GridPoint inside, GridPoint outward, bool field = false,
            IEnumerable<GridPoint> interiorRoute = null)
        {
            Cell = cell; Inside = inside; Outward = outward; Field = field;
            InteriorRoute = new ReadOnlyCollection<GridPoint>((interiorRoute ?? new GridPoint[0]).ToList());
        }
        public GridPoint Cell { get; }
        public GridPoint Inside { get; }
        public GridPoint Outward { get; }
        public bool Field { get; }
        public IReadOnlyList<GridPoint> InteriorRoute { get; }
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
                hash = StableHash.Add(hash, side.OpenGroundArea);
                foreach (GridPoint c in side.Faces.Concat(side.Samples)
                    .Concat(side.Positions).Concat(side.Post.HasValue ? new[] { side.Post.Value } : new GridPoint[0]))
                { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            }
            foreach (P4BPerimeterPost post in Posts)
                foreach (GridPoint c in new[] { post.Face, post.Cell, post.AccessGate, post.Outward, post.WallReturn }
                    .Concat(post.Route).Concat(post.CoverCells))
                { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            foreach (GridPoint c in TransitCells)
            { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            foreach (P4BSallyPort port in SallyPorts)
            {
                hash = StableHash.Add(hash, port.Field ? 1 : 0);
                foreach (GridPoint c in new[] { port.Cell, port.Inside, port.Outward }.Concat(port.InteriorRoute))
                { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            }
            Digest = hash.ToString("X16");
        }
        public IReadOnlyList<P4BAttackSide> Sides { get; }
        public IReadOnlyList<P4BPerimeterPost> Posts { get; }
        public IReadOnlyList<GridPoint> TransitCells { get; }
        public IReadOnlyList<P4BSallyPort> SallyPorts { get; }
        public IEnumerable<GridPoint> CoverCells => Posts.SelectMany(p => p.CoverCells).Distinct();
        public IEnumerable<GridPoint> AllPosts => Sides.SelectMany(s => s.Positions).Distinct();
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
                int openGroundArea = 0;
                if (faces.Count > 0)
                {
                    int front = faces.Max(c => c.X * outward.X + c.Z * outward.Z);
                    int low = faces.Min(c => c.X * tangent.X + c.Z * tangent.Z) - 6;
                    int high = faces.Max(c => c.X * tangent.X + c.Z * tangent.Z) + 6;
                    openGroundArea = reachable.Count(c =>
                    {
                        int distance = c.X * outward.X + c.Z * outward.Z - front;
                        int lateral = c.X * tangent.X + c.Z * tangent.Z;
                        return distance > 0 && distance <= 80 && lateral >= low && lateral <= high;
                    });
                }
                exposed.Add(new P4BAttackSide(Names[direction], outward, faces, probes, null,
                    probes.Count == 0 ? "no_edge_reachable_approach_samples" : null, openGroundArea));
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
            Dictionary<GridPoint, GridPoint> fieldPrevious = FieldInteriorPaths(input, layout);
            HashSet<GridPoint> reinforcement = new HashSet<GridPoint>(layout.OuterWalls.Concat(
                layout.Gates.SelectMany(g => g.BandWalls.Concat(g.BandDoors).Concat(g.OuterVestibule)))
                .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex]))));
            List<P4BAttackSide> sides = new List<P4BAttackSide>();
            foreach (P4BAttackSide side in exposed)
            {
                if (!side.Exposed) { sides.Add(side); continue; }
                GridPoint? fallback = null;
                List<GridPoint> existingPositions = (layout.FieldWall?.UsablePosts ?? new GridPoint[0]).Where(p =>
                    side.Faces.Any(face => Math.Abs(face.X - p.X) + Math.Abs(face.Z - p.Z) <= 6) &&
                    layout.FieldWall.CoverCells.Contains(new GridPoint(p.X + side.Outward.X, p.Z + side.Outward.Z)) &&
                    side.Samples.Count(c => P4BFireLane.CanEngage(p, c, 8, 25, opaque)) >= 4)
                    .OrderByDescending(p => side.Samples.Count(c => P4BFireLane.CanEngage(p, c, 8, 25, opaque)))
                    .ThenBy(p => p).ToList();
                GridPoint? existing = existingPositions.Cast<GridPoint?>().FirstOrDefault();
                if (existing.HasValue)
                {
                    if (side.Samples.Count(c => existingPositions.Any(p =>
                        P4BFireLane.CanEngage(p, c, 8, 25, opaque))) >= UsefulArc(side.Samples.Count))
                    {
                        sides.Add(new P4BAttackSide(side.Id, side.Outward, side.Faces, side.Samples, existing, null,
                            side.OpenGroundArea));
                        continue;
                    }
                    fallback = existing;
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
                    if (side.Samples.Count(c => P4BFireLane.CanEngage(shared.Cell, c, 8, 25, opaque)) >=
                        UsefulArc(side.Samples.Count))
                    {
                        sides.Add(new P4BAttackSide(side.Id, side.Outward, side.Faces, side.Samples, shared.Cell, null,
                            side.OpenGroundArea));
                        continue;
                    }
                    if (!fallback.HasValue) fallback = shared.Cell;
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
                    // The enclosed, unroofed field is also reachable ground.
                    // A side gate replaces an existing wall cell and connects
                    // through it without consuming a crop or roof cell.
                    List<GridPoint> fieldFaces = faceSide.Faces.Where(face =>
                        (layout.FieldWall?.Walls.Contains(face) ?? false) && !ports.Any(p => p.Cell.Equals(face)) &&
                        fieldPrevious.ContainsKey(At(face, faceSide.Outward, across, -1, 0)) &&
                        reachable.Contains(At(face, faceSide.Outward, across, 1, 0))).ToList();
                    gates.AddRange(Spread(fieldFaces, 24).Select(face =>
                    {
                        GridPoint inside = At(face, faceSide.Outward, across, -1, 0);
                        List<GridPoint> path = new List<GridPoint>();
                        for (GridPoint c = inside; ; c = fieldPrevious[c])
                        { path.Add(c); if (fieldPrevious[c].Equals(c)) break; }
                        path.Reverse();
                        return new GateOption { Cell = face, Outward = faceSide.Outward,
                            Port = new P4BSallyPort(face, inside, faceSide.Outward, true, path) };
                    }));
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
                Func<HashSet<GridPoint>, GridPoint[], HashSet<GridPoint>> seen = (blockers, extra) =>
                    new HashSet<GridPoint>(side.Samples.Where(target => existingPositions.Concat(posts.Where(p =>
                        side.Faces.Any(face => Math.Abs(face.X - p.Cell.X) + Math.Abs(face.Z - p.Cell.Z) <= 6) &&
                        p.CoverCells.Contains(new GridPoint(p.Cell.X + side.Outward.X, p.Cell.Z + side.Outward.Z)))
                        .Select(p => p.Cell)).Concat(extra).Any(p => P4BFireLane.CanEngage(p, target, 8, 25, blockers))));
                // One corner post cannot cover a long front. Add bounded,
                // separated projections by marginal coverage, preserving the
                // same geometry, budget and short-access contract for each.
                for (int addition = 0; addition < 3; addition++)
                {
                    HashSet<GridPoint> before = seen(opaque, new GridPoint[0]);
                    if (before.Count >= UsefulArc(side.Samples.Count)) break;
                    bool added = false;
                    IEnumerable<PositionOption> ranked = addition == 0
                        ? RankPositions(candidates, side.Samples.Count, c => c.Shots, c => c.Fronts,
                            c => c.Port != null, c => c.Post.Outward.Equals(side.Outward), c => c.Post.Cell)
                        : candidates.OrderByDescending(c => seen(new HashSet<GridPoint>(opaque) { c.Post.WallReturn },
                            new[] { c.Post.Cell }).Count).ThenBy(c => c.Port != null ? 1 : 0).ThenBy(c => c.Post.Cell);
                    foreach (PositionOption candidate in ranked)
                    {
                        P4BPerimeterPost post = candidate.Post;
                        if (post.Route.Skip(1).Concat(post.CoverCells).Concat(new[] { post.WallReturn }).Any(reserved.Contains) ||
                            (candidate.Port != null && ports.Any(p => p.Cell.Equals(candidate.Port.Cell))) ||
                            seen(new HashSet<GridPoint>(opaque) { post.WallReturn }, new[] { post.Cell }).Count <= before.Count) continue;
                        IEnumerable<P4BPerimeterPost> trialPosts = posts.Concat(new[] { post });
                        IEnumerable<P4BSallyPort> trialPorts = ports.Concat(candidate.Port == null ?
                            Enumerable.Empty<P4BSallyPort>() : new[] { candidate.Port });
                        if (Materials(input, layout, trialPosts.Sum(p => p.CoverCells.Count), trialPosts, trialPorts).ImportValue >
                            input.Requirements.ImportValueLimit)
                        { omission = "cover_material_budget"; continue; }
                        if (selected == null) selected = post;
                        posts.Add(post); reserved.UnionWith(post.CoverCells); reserved.UnionWith(post.Route.Skip(1));
                        reserved.Add(post.WallReturn); opaque.Add(post.WallReturn);
                        if (candidate.Port != null) ports.Add(candidate.Port);
                        added = true; break;
                    }
                    if (!added) break;
                }
                sides.Add(new P4BAttackSide(side.Id, side.Outward, side.Faces, side.Samples,
                    selected == null ? fallback : selected.Cell,
                    selected == null && !fallback.HasValue ? omission : null, side.OpenGroundArea));
            }
            // A side may be supported by both field flanks or a neighboring
            // corner. Register every real, covered position with useful shots,
            // so the garrison and gun planner use the same physical inventory.
            var available = posts.SelectMany(p => FiringSlots(p, sides, opaque)
                    .Select(c => new { Cell = c, Cover = p.CoverCells.AsEnumerable() }))
                .Concat((layout.FieldWall?.UsablePosts ?? new GridPoint[0]).Select(c =>
                    new { Cell = c, Cover = layout.FieldWall.CoverCells.AsEnumerable() })).ToList();
            sides = sides.Select(s => new P4BAttackSide(s.Id, s.Outward, s.Faces, s.Samples, s.Post,
                s.OmissionReason, s.OpenGroundArea, available.Where(p =>
                    s.Faces.Any(face => Math.Abs(face.X - p.Cell.X) + Math.Abs(face.Z - p.Cell.Z) <= 6) &&
                    p.Cover.Contains(new GridPoint(p.Cell.X + s.Outward.X, p.Cell.Z + s.Outward.Z)) &&
                    s.Samples.Count(c => P4BFireLane.CanEngage(p.Cell, c, 8, 25, opaque)) >= 4)
                    .Select(p => p.Cell))).ToList();
            return new P4BApproachDefensePlan(reachable, sides, posts, previous.Keys
                .Except(posts.Select(p => p.WallReturn)).Concat(posts.SelectMany(p => p.Route))
                .Concat(ports.SelectMany(p => p.InteriorRoute)), ports);
        }

        private static Dictionary<GridPoint, GridPoint> FieldInteriorPaths(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            if (layout.FieldWall?.InnerGate == null || layout.JointSite == null) return previous;
            GridPoint entry = layout.FieldWall.InnerGate.Value;
            HashSet<GridPoint> walls = new HashSet<GridPoint>(layout.FieldWall.Walls.Concat(layout.BuiltWalls)
                .Concat(layout.NaturalWalls).Concat(layout.ExtraObstacles));
            Queue<GridPoint> queue = new Queue<GridPoint>();
            previous[entry] = entry; queue.Enqueue(entry);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                foreach (GridPoint direction in Directions)
                {
                    GridPoint next = new GridPoint(here.X + direction.X, here.Z + direction.Z);
                    if (!layout.JointSite.Field.Contains(next) || walls.Contains(next) ||
                        next.Equals(layout.FieldWall.OuterGate) || !Walkable(input.Terrain, next) || previous.ContainsKey(next)) continue;
                    previous[next] = here; queue.Enqueue(next);
                }
            }
            return previous;
        }

        internal static int UsefulArc(int sampleCount) => Math.Max(4, (int)Math.Ceiling(sampleCount * 0.35));

        // A five-cell screen protects three already-reserved, traversable
        // standing cells. The former garrison exposed only its terminal cell,
        // leaving funded shooters piled up at the support center.
        public static IReadOnlyList<GridPoint> FiringSlots(P4BPerimeterPost post,
            IEnumerable<P4BAttackSide> sides, IReadOnlyCollection<GridPoint> opaque)
        {
            List<P4BAttackSide> fronts = (sides ?? Enumerable.Empty<P4BAttackSide>())
                .Where(s => s.Exposed && (s.Id == post.Side || s.Positions.Contains(post.Cell))).ToList();
            return post.Route.Skip(Math.Max(0, post.Route.Count - 3))
                .Where(c => post.CoverCells.Contains(new GridPoint(c.X + post.Outward.X,
                    c.Z + post.Outward.Z)) && fronts.Any(s => s.Samples.Count(target =>
                    P4BFireLane.CanEngage(c, target, 8, 25, opaque)) >= 4))
                .Distinct().OrderBy(c => c.Equals(post.Cell) ? 0 : 1)
                .ThenBy(c => Math.Abs(c.X - post.Cell.X) + Math.Abs(c.Z - post.Cell.Z))
                .ThenBy(c => c).ToList();
        }

        // The old save format kept only the terminal post, access gate and
        // physical screen. Recover the two earlier covered cells only when
        // those facts match the exact six-cell sally-port geometry.
        public static IReadOnlyList<GridPoint> RecoverLegacySlots(GridPoint gate, GridPoint post,
            IReadOnlyCollection<GridPoint> cover)
        {
            foreach (GridPoint outward in Directions)
            {
                GridPoint tangent = new GridPoint(-outward.Z, outward.X);
                foreach (int flank in new[] { -1, 1 })
                {
                    if (!post.Equals(At(gate, outward, tangent, 2, flank * 3))) continue;
                    return Enumerable.Range(1, 2).Select(n => At(gate, outward, tangent, 2, flank * n))
                        .Where(c => cover.Contains(new GridPoint(c.X + outward.X, c.Z + outward.Z)))
                        .ToList();
                }
            }
            return new GridPoint[0];
        }

        internal static int CoveredSamples(P4BAttackSide side, HashSet<GridPoint> opaque) =>
            side.Samples.Count(c => side.Positions.Any(p => P4BFireLane.CanEngage(p, c, 8, 25, opaque)));

        internal static IReadOnlyList<T> RankPositions<T>(IEnumerable<T> candidates, int sampleCount,
            Func<T, int> shots, Func<T, int> fronts, Func<T, bool> needsPort,
            Func<T, bool> sameFacing, Func<T, GridPoint> cell)
        {
            // A nearby perpendicular gate can technically see four cells
            // while leaving almost the entire named front unanswered.
            // Compare candidates that cover a meaningful part of this
            // front first; within that set, retain the cheaper existing-
            // gate preference. If none do, take the best available arc.
            int usefulArc = UsefulArc(sampleCount);
            List<T> options = candidates.ToList();
            List<T> substantial = options.Where(c => shots(c) >= usefulArc).ToList();
            return (substantial.Count > 0
                ? substantial.OrderBy(c => needsPort(c) ? 1 : 0)
                    .ThenByDescending(fronts).ThenByDescending(shots).ThenBy(cell)
                : options.OrderByDescending(shots).ThenByDescending(sameFacing)
                    .ThenBy(c => needsPort(c) ? 1 : 0).ThenBy(cell)).ToList();
        }

        public static LayoutMaterialLedger Materials(ProceduralLayoutInput input, ProceduralLayout layout, int extraCover,
            IEnumerable<P4BPerimeterPost> posts = null, IEnumerable<P4BSallyPort> ports = null) =>
            input.Equipment.Count(layout.Rooms, layout.BuiltWalls.Except((ports ?? Enumerable.Empty<P4BSallyPort>()).Select(p => p.Cell))
                .Concat((posts ?? Enumerable.Empty<P4BPerimeterPost>()).Select(p => p.WallReturn)).Concat(layout.OuterWalls)
                .Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(layout.ExhaustPocketWalls)
                .Concat((layout.FieldWall?.Walls ?? new GridPoint[0]).Except(
                    (ports ?? Enumerable.Empty<P4BSallyPort>()).Select(p => p.Cell))),
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
            Dictionary<GridPoint, GridPoint> fieldPaths = FieldInteriorPaths(input, layout);
            foreach (P4BSallyPort port in plan.SallyPorts)
            {
                if (!Directions.Contains(port.Outward) || !port.Inside.Equals(new GridPoint(
                    port.Cell.X - port.Outward.X, port.Cell.Z - port.Outward.Z)) ||
                    (!port.Field && !publicCells.Contains(port.Inside)) ||
                    (port.Field && (!fieldPaths.ContainsKey(port.Inside) || layout.FieldWall == null ||
                        port.InteriorRoute.Count == 0 || !port.InteriorRoute.First().Equals(layout.FieldWall.InnerGate.Value) ||
                        !port.InteriorRoute.Last().Equals(port.Inside) || port.InteriorRoute.Any(c => !fieldPaths.ContainsKey(c)) ||
                        port.InteriorRoute.Zip(port.InteriorRoute.Skip(1), (a, b) =>
                            Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z)).Any(d => d != 1) ||
                        layout.FieldWall.Walls.Contains(port.Cell) || layout.FieldWall.Fields.Contains(port.Cell))) ||
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
                if (side == null || !side.Positions.Contains(post.Cell) ||
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
                if (side.Positions.Any(p => (!plan.Posts.Any(post =>
                    FiringSlots(post, plan.Sides, opaque).Contains(p)) &&
                    !(layout.FieldWall?.UsablePosts.Contains(p) ?? false)) ||
                    !side.Faces.Any(face => Math.Abs(face.X - p.X) + Math.Abs(face.Z - p.Z) <= 6) ||
                    !plan.CoverCells.Concat(layout.FieldWall?.CoverCells ?? new GridPoint[0])
                        .Contains(new GridPoint(p.X + side.Outward.X, p.Z + side.Outward.Z)) ||
                    side.Samples.Count(c => P4BFireLane.CanEngage(p, c, 8, 25, opaque)) < 4))
                    errors.Add("attack_side_position_unverified");
            }
            HashSet<GridPoint> fieldTransit = new HashSet<GridPoint>(plan.SallyPorts.Where(p => p.Field)
                .SelectMany(p => p.InteriorRoute).Where(fieldPaths.ContainsKey));
            if (plan.TransitCells.Any(c => !AccessGates(layout).Contains(c) &&
                ((occupied.Contains(c) && !fieldTransit.Contains(c)) || !Walkable(input.Terrain, c))))
                errors.Add("perimeter_transit_occupancy");
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
