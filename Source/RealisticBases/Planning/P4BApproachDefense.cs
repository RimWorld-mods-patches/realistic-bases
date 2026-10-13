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
            IEnumerable<P4BAttackSide> sides, IEnumerable<P4BPost> posts,
            IEnumerable<GridPoint> transit = null, IEnumerable<P4BSallyPort> sallyPorts = null,
            string coverDefName = null, int pack = 0)
        {
            attackerReachable = new HashSet<GridPoint>(reachable);
            Sides = new ReadOnlyCollection<P4BAttackSide>(sides.ToList());
            Posts = new ReadOnlyCollection<P4BPost>(posts.ToList());
            TransitCells = P4BAttackSide.Copy(transit ?? Enumerable.Empty<GridPoint>());
            SallyPorts = new ReadOnlyCollection<P4BSallyPort>((sallyPorts ?? Enumerable.Empty<P4BSallyPort>()).ToList());
            CoverDefName = coverDefName ?? "Barricade";
            Pack = pack;
            ulong hash = StableHash.Add(StableHash.Start, "p4b-approach-defense-v2");
            hash = StableHash.Add(hash, CoverDefName);
            hash = StableHash.Add(hash, Pack);
            foreach (P4BAttackSide side in Sides)
            {
                hash = StableHash.Add(hash, side.Id); hash = StableHash.Add(hash, side.OmissionReason);
                hash = StableHash.Add(hash, side.OpenGroundArea);
                foreach (GridPoint c in side.Faces.Concat(side.Samples)
                    .Concat(side.Positions).Concat(side.Post.HasValue ? new[] { side.Post.Value } : new GridPoint[0]))
                { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            }
            foreach (P4BPost post in Posts)
            {
                hash = StableHash.Add(hash, post.Side);
                foreach (GridPoint c in new[] { post.AccessGate, post.Outward, post.WallReturn ?? new GridPoint(-1, -1) }
                    .Concat(post.Route).Concat(post.Slots).Concat(post.CoverCells))
                { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            }
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
        public IReadOnlyList<P4BPost> Posts { get; }
        public IReadOnlyList<GridPoint> TransitCells { get; }
        public IReadOnlyList<P4BSallyPort> SallyPorts { get; }
        public string CoverDefName { get; }
        // Ranged defenders the plan sizes every exposed side for.
        public int Pack { get; }
        public IEnumerable<GridPoint> WallReturns => Posts.Where(p => p.WallReturn.HasValue).Select(p => p.WallReturn.Value);
        public IEnumerable<GridPoint> CoverCells => Posts.SelectMany(p => p.CoverCells).Distinct();
        public IEnumerable<GridPoint> AllPosts => Sides.SelectMany(s => s.Positions).Distinct();
        public bool AttackerCanReach(GridPoint cell) => attackerReachable.Contains(cell);
        internal P4BApproachDefensePlan WithoutTransit(IEnumerable<GridPoint> blocked) =>
            new P4BApproachDefensePlan(attackerReachable, Sides, Posts, TransitCells.Except(blocked), SallyPorts, CoverDefName, Pack);
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
                .Concat(layout.FieldWall?.Walls ?? new GridPoint[0]));
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
            List<P4BPost> posts = new List<P4BPost>();
            List<P4BSallyPort> ports = new List<P4BSallyPort>();
            HashSet<GridPoint> reserved = new HashSet<GridPoint>();
            HashSet<GridPoint> arrival = new HashSet<GridPoint>(layout.Gates.SelectMany(g => g.ExteriorRoute)
                .Concat(layout.FieldWall?.ArrivalRoute ?? new GridPoint[0]));
            HashSet<GridPoint> publicCells = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input, layout)
                .Except(layout.PowerYards.SelectMany(y => y.Cells)));
            Dictionary<GridPoint, GridPoint> fieldPrevious = FieldInteriorPaths(input, layout);
            HashSet<GridPoint> reinforcement = new HashSet<GridPoint>(layout.OuterWalls.Concat(
                layout.Gates.SelectMany(g => g.BandWalls.Concat(g.BandDoors).Concat(g.OuterVestibule)))
                .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex]))));
            int pack = PackSize(input, layout);
            List<GridPoint> allSamples = exposed.SelectMany(s => s.Samples).Distinct().ToList();
            Dictionary<GridPoint, HashSet<GridPoint>> engagedCache = new Dictionary<GridPoint, HashSet<GridPoint>>();
            Func<GridPoint, HashSet<GridPoint>> engaged = cell =>
            {
                if (!engagedCache.TryGetValue(cell, out HashSet<GridPoint> seen))
                    engagedCache[cell] = seen = new HashSet<GridPoint>(allSamples.Where(t =>
                        P4BFireLane.CanEngage(cell, t, P4BCoverNest.MinimumRange, P4BCoverNest.MaximumRange, opaque)));
                return seen;
            };
            Func<P4BAttackSide, GridPoint, bool> facesSide = (side, cell) => side.Exposed &&
                side.Faces.Any(face => Math.Abs(face.X - cell.X) + Math.Abs(face.Z - cell.Z) <= 6) &&
                side.Samples.Count(engaged(cell).Contains) >= P4BCoverNest.MinimumShots;
            List<GateOption> gates = layout.Gates
                .Select(g => new GateOption { Outward = g.Outward, Cell = g.BandDoors.Concat(new[] { g.Cell })
                    .OrderByDescending(c => c.X * g.Outward.X + c.Z * g.Outward.Z).First() }).ToList();
            if (layout.FieldWall != null)
                gates.Add(new GateOption { Cell = layout.FieldWall.OuterGate,
                    Outward = layout.Gates[layout.FieldWall.GateIndex].Outward });
            // Only one-cell built perimeter walls with a public cell
            // immediately inside can become defense sally ports. Never
            // cut a room, rock, gate band or reinforced wall section.
            foreach (P4BAttackSide faceSide in exposed.Where(s => s.Exposed))
            {
                GridPoint across = new GridPoint(-faceSide.Outward.Z, faceSide.Outward.X);
                List<GridPoint> portFaces = faceSide.Faces.Where(face => layout.BuiltWalls.Contains(face) &&
                    !reinforcement.Contains(face) && Walkable(input.Terrain, face) &&
                    input.Terrain[face].Has(TerrainTraits.Buildable) &&
                    publicCells.Contains(At(face, faceSide.Outward, across, -1, 0)) &&
                    reachable.Contains(At(face, faceSide.Outward, across, 1, 0))).ToList();
                gates.AddRange(Spread(portFaces, 24).Select(face => new GateOption { Cell = face, Outward = faceSide.Outward,
                    Port = new P4BSallyPort(face, At(face, faceSide.Outward, across, -1, 0), faceSide.Outward) }));
                // The enclosed, unroofed field is also reachable ground.
                // A side gate replaces an existing wall cell and connects
                // through it without consuming a crop or roof cell.
                List<GridPoint> fieldFaces = faceSide.Faces.Where(face =>
                    (layout.FieldWall?.Walls.Contains(face) ?? false) &&
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
            List<GridPoint> doors = layout.Gates.SelectMany(g => g.BandDoors.Concat(new[] { g.Cell }))
                .Concat(layout.FieldWall == null ? new GridPoint[0] : new[] { layout.FieldWall.OuterGate }
                    .Concat(layout.FieldWall.InnerGate.HasValue ? new[] { layout.FieldWall.InnerGate.Value } : new GridPoint[0]))
                .ToList();
            string coverDef = P4BCoverNest.CoverDefName(input);
            var nests = new Dictionary<KeyValuePair<GridPoint, int>, P4BPost>();
            List<P4BAttackSide> sides = new List<P4BAttackSide>();
            foreach (P4BAttackSide side in exposed)
            {
                if (!side.Exposed) { sides.Add(side); continue; }
                Func<P4BPost, int> facing = post => post.Slots.Count(c => facesSide(side, c));
                HashSet<GridPoint> seen = new HashSet<GridPoint>(side.Samples.Where(t =>
                    posts.Any(p => p.Slots.Any(c => facesSide(side, c) && engaged(c).Contains(t)))));
                int have = posts.Sum(facing);
                P4BPost selected = posts.FirstOrDefault(p => facing(p) > 0);
                string omission = coverDef == null ? "cover_def_unavailable" : "no_connected_short_gate_position";
                // Every exposed side can hold the whole ranged pack (P5B moves
                // the pack to the attacked side); posts spread along long fronts.
                for (int added = 0; coverDef != null && added < 4 &&
                    (have < pack || seen.Count < UsefulArc(side.Samples.Count)); added++)
                {
                    var ranked = new List<KeyValuePair<P4BPost, GateOption>>();
                    foreach (GateOption gate in gates)
                    {
                        // A door needs solid wall on both sides: no new port beside another door.
                        if (gate.Port != null && doors.Concat(ports.Select(p => p.Cell)).Any(d =>
                            Math.Abs(d.X - gate.Port.Cell.X) <= 1 && Math.Abs(d.Z - gate.Port.Cell.Z) <= 1)) continue;
                        foreach (int flank in new[] { -1, 1 })
                        {
                            // Reservations only grow: an unobstructed cached nest
                            // stays valid, and one that failed stays failed.
                            var key = new KeyValuePair<GridPoint, int>(gate.Cell, flank);
                            if (nests.TryGetValue(key, out P4BPost cached) && (cached == null ||
                                !cached.Route.Skip(1).Concat(cached.CoverCells).Any(reserved.Contains) &&
                                !(cached.WallReturn.HasValue && reserved.Contains(cached.WallReturn.Value))))
                            {
                                if (cached != null && facing(cached) > 0) ranked.Add(new KeyValuePair<P4BPost, GateOption>(cached, gate));
                                continue;
                            }
                            P4BPost post = P4BCoverNest.Build(side.Id, gate.Cell, gate.Outward, flank, P4BCoverNest.MaxSlots,
                                c => engaged(c), c => exposed.Any(s => facesSide(s, c)),
                                c => navigation.Contains(c) && !reserved.Contains(c) && !occupied.Contains(c) && !arrival.Contains(c),
                                c => !occupied.Contains(c) && !reserved.Contains(c) && !arrival.Contains(c) &&
                                    !reinforcement.Contains(c) && Buildable(input.Terrain, c),
                                perimeter.Contains,
                                c => !occupied.Contains(c) && !reserved.Contains(c) && !arrival.Contains(c) &&
                                    !reinforcement.Contains(c) && Buildable(input.Terrain, c));
                            nests[key] = post;
                            if (post != null && facing(post) > 0) ranked.Add(new KeyValuePair<P4BPost, GateOption>(post, gate));
                        }
                    }
                    bool accepted = false;
                    foreach (var option in ranked
                        .OrderByDescending(o => Math.Min(facing(o.Key), Math.Max(0, pack - have)))
                        .ThenByDescending(o => exposed.Count(s => o.Key.Slots.Any(c => facesSide(s, c))))
                        .ThenByDescending(o => side.Samples.Count(t => !seen.Contains(t) &&
                            o.Key.Slots.Any(c => facesSide(side, c) && engaged(c).Contains(t))))
                        .ThenBy(o => o.Value.Port != null ? 1 : 0).ThenBy(o => o.Key.Cell))
                    {
                        P4BPost post = option.Key;
                        int gain = Math.Min(facing(post), Math.Max(0, pack - have));
                        int newShots = side.Samples.Count(t => !seen.Contains(t) &&
                            post.Slots.Any(c => facesSide(side, c) && engaged(c).Contains(t)));
                        if (gain == 0 && newShots == 0) continue;
                        IEnumerable<P4BPost> trialPosts = posts.Concat(new[] { post });
                        IEnumerable<P4BSallyPort> trialPorts = ports.Concat(option.Value.Port == null ?
                            Enumerable.Empty<P4BSallyPort>() : new[] { option.Value.Port });
                        if (Materials(input, layout, trialPosts, trialPorts).ImportValue > input.Requirements.ImportValueLimit)
                        { omission = "defense_material_budget"; continue; }
                        if (post.Side != side.Id)
                            post = new P4BPost(side.Id, post.Outward, post.AccessGate, post.Route, post.Slots, post.CoverCells, post.WallReturn);
                        posts.Add(post);
                        if (selected == null) selected = post;
                        reserved.UnionWith(post.Route.Skip(1).Concat(post.CoverCells));
                        if (post.WallReturn.HasValue) reserved.Add(post.WallReturn.Value);
                        if (option.Value.Port != null) ports.Add(option.Value.Port);
                        have += facing(post);
                        seen.UnionWith(side.Samples.Where(t => post.Slots.Any(c => facesSide(side, c) && engaged(c).Contains(t))));
                        accepted = true; break;
                    }
                    if (!accepted) break;
                }
                // The side's representative post is its slot with the most shots at it.
                GridPoint? best = posts.SelectMany(p => p.Slots).Where(c => facesSide(side, c))
                    .OrderByDescending(c => side.Samples.Count(engaged(c).Contains)).ThenBy(c => c)
                    .Cast<GridPoint?>().FirstOrDefault();
                sides.Add(new P4BAttackSide(side.Id, side.Outward, side.Faces, side.Samples,
                    best ?? selected?.Cell, selected == null ? omission : null, side.OpenGroundArea));
            }
            // Wall returns are opaque. Register every slot that still has
            // useful shots, so the garrison and gun planner share one inventory.
            opaque.UnionWith(posts.Where(p => p.WallReturn.HasValue).Select(p => p.WallReturn.Value));
            sides = sides.Select(s => new P4BAttackSide(s.Id, s.Outward, s.Faces, s.Samples, s.Post,
                s.OmissionReason, s.OpenGroundArea, posts.SelectMany(p => p.Slots).Where(c => s.Exposed &&
                    s.Faces.Any(face => Math.Abs(face.X - c.X) + Math.Abs(face.Z - c.Z) <= 6) &&
                    s.Samples.Count(t => P4BFireLane.CanEngage(c, t, P4BCoverNest.MinimumRange,
                        P4BCoverNest.MaximumRange, opaque)) >= P4BCoverNest.MinimumShots))).ToList();
            return new P4BApproachDefensePlan(reachable, sides, posts, previous.Keys
                .Except(posts.Where(p => p.WallReturn.HasValue).Select(p => p.WallReturn.Value))
                .Concat(posts.SelectMany(p => p.Route))
                .Concat(ports.SelectMany(p => p.InteriorRoute)), ports, coverDef, pack);
        }

        // Ranged residents of the planned loadout: the defender pack that
        // every exposed side is sized to hold.
        internal static int PackSize(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            SettlementLoadout staffing = LayoutLoadoutModel.Build(input, layout);
            return !staffing.Success ? 0 : staffing.Residents.Count(r => r.Weapon != null &&
                input.Catalog.TryGet(r.Weapon.DefName, out CatalogDef weapon) && weapon.MaximumRange > 0);
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

        // Every slot of a nest that still has useful shots at a front.
        public static IReadOnlyList<GridPoint> FiringSlots(P4BPost post,
            IEnumerable<P4BAttackSide> sides, IReadOnlyCollection<GridPoint> opaque)
        {
            List<P4BAttackSide> fronts = (sides ?? Enumerable.Empty<P4BAttackSide>()).Where(s => s.Exposed).ToList();
            return post.Slots.Where(c => fronts.Any(s => s.Samples.Count(target => P4BFireLane.CanEngage(c, target,
                P4BCoverNest.MinimumRange, P4BCoverNest.MaximumRange, opaque)) >= P4BCoverNest.MinimumShots)).ToList();
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

        // Cover is built from the base's own construction stuff and adds no
        // import; only wall returns, sally-port doors and walls are counted.
        public static LayoutMaterialLedger Materials(ProceduralLayoutInput input, ProceduralLayout layout,
            IEnumerable<P4BPost> posts = null, IEnumerable<P4BSallyPort> ports = null) =>
            input.Equipment.Count(layout.Rooms, layout.BuiltWalls.Except((ports ?? Enumerable.Empty<P4BSallyPort>()).Select(p => p.Cell))
                .Concat((posts ?? Enumerable.Empty<P4BPost>()).Where(p => p.WallReturn.HasValue).Select(p => p.WallReturn.Value))
                .Concat(layout.OuterWalls).Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(layout.ExhaustPocketWalls)
                .Concat((layout.FieldWall?.Walls ?? new GridPoint[0]).Except(
                    (ports ?? Enumerable.Empty<P4BSallyPort>()).Select(p => p.Cell))),
                layout.Gates.Sum(g => 1 + g.BandDoors.Count) + (layout.FieldWall == null ? 0 : 2) +
                    (layout.ApproachDefense?.SallyPorts.Count ?? 0) + (ports?.Count() ?? 0), layout.Power);

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
            HashSet<GridPoint> slots = new HashSet<GridPoint>();
            HashSet<GridPoint> routes = new HashSet<GridPoint>(plan.Posts.SelectMany(p => p.Route));
            HashSet<GridPoint> arrival = new HashSet<GridPoint>(layout.Gates.SelectMany(g => g.ExteriorRoute)
                .Concat(layout.FieldWall?.ArrivalRoute ?? new GridPoint[0]));
            if (!input.Catalog.TryGet(plan.CoverDefName, out CatalogDef coverDef) ||
                !input.Catalog.IsLegal(coverDef, input.Equipment.Kit.Stage)) errors.Add("cover_def_illegal");
            HashSet<GridPoint> firing = new HashSet<GridPoint>();
            foreach (P4BPost post in plan.Posts)
            {
                int before = firing.Count;
                firing.UnionWith(FiringSlots(post, plan.Sides, opaque));
                if (plan.Sides.All(s => s.Id != post.Side) || post.Slots.Count > P4BCoverNest.MaxSlots ||
                    post.Slots.Any(c => !slots.Add(c)) || firing.Count == before)
                    errors.Add("perimeter_post_no_approach_fire");
                GridPoint tangent = new GridPoint(-post.Outward.Z, post.Outward.X);
                int flank = Math.Sign((post.Cell.X - post.AccessGate.X) * tangent.X +
                    (post.Cell.Z - post.AccessGate.Z) * tangent.Z);
                if (!Directions.Contains(post.Outward) || flank == 0 || !AccessGates(layout).Contains(post.AccessGate) ||
                    !post.Route.SequenceEqual(P4BCoverNest.RouteFor(post.AccessGate, post.Outward, flank, post.Slots.Count)) ||
                    !post.Route.Skip(3).SequenceEqual(post.Slots) ||
                    post.Route.Skip(1).Any(c => occupied.Contains(c) || !Walkable(input.Terrain, c)))
                    errors.Add("perimeter_post_access");
                if (flank != 0 && Enumerable.Range(1, post.Slots.Count).Any(l =>
                    !opaque.Contains(P4BCoverNest.Local(post.AccessGate, post.Outward, flank, 0, l))))
                    errors.Add("perimeter_post_wall");
                if (post.WallReturn.HasValue && (flank == 0 ||
                    !post.WallReturn.Value.Equals(P4BCoverNest.Local(post.AccessGate, post.Outward, flank, 1, post.Slots.Count + 1)) ||
                    !layout.BuiltWalls.Contains(post.WallReturn.Value) || !Buildable(input.Terrain, post.WallReturn.Value) ||
                    layout.ExtraObstacles.Contains(post.WallReturn.Value) || arrival.Contains(post.WallReturn.Value)))
                    errors.Add("perimeter_post_return");
                if (post.CoverCells.Count == 0 || post.CoverCells.Any(c => !cover.Add(c) || occupied.Contains(c) ||
                    !Buildable(input.Terrain, c) || routes.Contains(c) || arrival.Contains(c) ||
                    !post.Slots.Any(slot => Math.Abs(slot.X - c.X) <= 1 && Math.Abs(slot.Z - c.Z) <= 1)))
                    errors.Add("perimeter_post_cover");
            }
            if (cover.Overlaps(slots)) errors.Add("perimeter_post_cover");
            foreach (P4BAttackSide side in plan.Sides)
            {
                if (side.Exposed && ((!side.Post.HasValue && side.OmissionReason.Length == 0) ||
                    (side.Post.HasValue && !plan.Posts.Any(p => p.Slots.Contains(side.Post.Value)))))
                    errors.Add("unreported_attack_side");
                if (side.Post.HasValue && side.Samples.Count(c =>
                    P4BFireLane.CanEngage(side.Post.Value, c, 8, 25, opaque)) < 4)
                    errors.Add("attack_side_post_no_fire");
                if (side.Positions.Any(p => !firing.Contains(p) ||
                    !side.Faces.Any(face => Math.Abs(face.X - p.X) + Math.Abs(face.Z - p.Z) <= 6) ||
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
