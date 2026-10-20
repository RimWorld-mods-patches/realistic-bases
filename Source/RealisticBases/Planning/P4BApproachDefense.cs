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
            FaceSet = new HashSet<GridPoint>(Faces);
            SampleSet = new HashSet<GridPoint>(Samples);
            NearFacesSet = new HashSet<GridPoint>();
            foreach (GridPoint face in Faces)
                for (int dx = -6; dx <= 6; dx++)
                    for (int dz = -6; dz <= 6; dz++)
                        if (Math.Abs(dx) + Math.Abs(dz) <= 6)
                            NearFacesSet.Add(new GridPoint(face.X + dx, face.Z + dz));
            Post = post; OmissionReason = omission ?? string.Empty;
            OpenGroundArea = openGroundArea;
            Positions = Copy(positions ?? (post.HasValue ? new[] { post.Value } : new GridPoint[0]));
            PositionSet = new HashSet<GridPoint>(Positions);
        }
        public string Id { get; }
        public GridPoint Outward { get; }
        public IReadOnlyList<GridPoint> Faces { get; }
        internal HashSet<GridPoint> FaceSet { get; }
        internal HashSet<GridPoint> NearFacesSet { get; }
        public IReadOnlyList<GridPoint> Samples { get; }
        internal HashSet<GridPoint> SampleSet { get; }
        public GridPoint? Post { get; }
        public IReadOnlyList<GridPoint> Positions { get; }
        internal HashSet<GridPoint> PositionSet { get; }
        public string OmissionReason { get; }
        // Edge-connected cells in the exterior strip beyond this perimeter
        // face. This distinguishes a broad staging area from a narrow margin
        // even when the sampled firing-line counts happen to be similar.
        public int OpenGroundArea { get; }
        public bool Exposed => Samples.Count > 0;
        internal static IReadOnlyList<GridPoint> Copy(IEnumerable<GridPoint> cells) =>
            new ReadOnlyCollection<GridPoint>(cells.Distinct().OrderBy(c => c).ToList());
    }

    // One logical defense crossing. Cell is the innermost built-wall door;
    // Band contains one aligned door in each further perimeter layer.
    public sealed class P4BSallyPort
    {
        internal P4BSallyPort(GridPoint cell, GridPoint inside, GridPoint outward, bool field = false,
            IEnumerable<GridPoint> interiorRoute = null, IEnumerable<GridPoint> band = null)
        {
            Cell = cell; Inside = inside; Outward = outward; Field = field;
            InteriorRoute = new ReadOnlyCollection<GridPoint>((interiorRoute ?? new GridPoint[0]).ToList());
            Band = new ReadOnlyCollection<GridPoint>((band ?? new GridPoint[0]).ToList());
            Doors = new ReadOnlyCollection<GridPoint>(new[] { cell }.Concat(Band).ToList());
        }
        public GridPoint Cell { get; }
        public IReadOnlyList<GridPoint> Band { get; }
        public IReadOnlyList<GridPoint> Doors { get; }
        public GridPoint Outer => Doors[Doors.Count - 1];
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
            string coverDefName = null, int pack = 0, int compoundTargets = 0, int compoundTargetsCovered = 0,
            IEnumerable<int> compoundGateCandidates = null)
        {
            attackerReachable = new HashSet<GridPoint>(reachable);
            Sides = new ReadOnlyCollection<P4BAttackSide>(sides.ToList());
            Posts = new ReadOnlyCollection<P4BPost>(posts.ToList());
            TransitCells = P4BAttackSide.Copy(transit ?? Enumerable.Empty<GridPoint>());
            SallyPorts = new ReadOnlyCollection<P4BSallyPort>((sallyPorts ?? Enumerable.Empty<P4BSallyPort>()).ToList());
            CoverDefName = coverDefName ?? "Barricade";
            Pack = pack;
            CompoundTargets = compoundTargets;
            CompoundTargetsCovered = compoundTargetsCovered;
            CompoundGateCandidates = new ReadOnlyCollection<int>((compoundGateCandidates ?? new int[4]).ToList());
            ulong hash = StableHash.Add(StableHash.Start, "p4b-approach-defense-v2");
            hash = StableHash.Add(hash, CoverDefName);
            hash = StableHash.Add(hash, Pack);
            hash = StableHash.Add(hash, CompoundTargets);
            hash = StableHash.Add(hash, CompoundTargetsCovered);
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
                foreach (GridPoint c in new[] { port.Inside, port.Outward }.Concat(port.Doors).Concat(port.InteriorRoute))
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
        public int CompoundTargets { get; }
        public int CompoundTargetsCovered { get; }
        public IReadOnlyList<int> CompoundGateCandidates { get; }
        public IEnumerable<GridPoint> WallReturns => Posts.Where(p => p.WallReturn.HasValue).Select(p => p.WallReturn.Value);
        public IEnumerable<GridPoint> CoverCells => Posts.SelectMany(p => p.CoverCells).Distinct();
        public IEnumerable<GridPoint> AllPosts => Sides.SelectMany(s => s.Positions).Distinct();
        public bool AttackerCanReach(GridPoint cell) => attackerReachable.Contains(cell);
        internal P4BApproachDefensePlan WithoutTransit(IEnumerable<GridPoint> blocked) =>
            new P4BApproachDefensePlan(attackerReachable, Sides, Posts, TransitCells.Except(blocked), SallyPorts,
                CoverDefName, Pack, CompoundTargets, CompoundTargetsCovered, CompoundGateCandidates);
        public string Digest { get; }
    }

    // Conservative conventional land-attack exposure, not a raid predictor:
    // edge-connected ground and firing samples along actual perimeter faces.
    // Water/rock do not imply safety if reachable ground across them is in range.
    // Drop pods, sappers and future demolition can expose additional fronts.
    public static class P4BApproachDefensePlanner
    {
        private const int AttackProbeColumnsPerSide = 16;
        public const int MaxCompoundGateCrossingsTotal = 12;
        public const int PreferredCompoundGateCrossingsPerSide = 3;
        public const int MaxFieldGateCrossingsPerSide = 1;
        internal const int MaxGateCandidatesPerSide = MaxCompoundGateCrossingsTotal;
        internal const int MaxInteriorConnectorCells = 13;
        private static readonly string[] Names = { "south", "east", "north", "west" };
        private static readonly GridPoint[] Directions =
        { new GridPoint(0, -1), new GridPoint(1, 0), new GridPoint(0, 1), new GridPoint(-1, 0) };
        private sealed class GateOption
        {
            internal GridPoint Cell, Outward;
            internal P4BSallyPort Port;
            internal bool Field;
        }
        private sealed class RankedPostOption
        {
            internal P4BAttackSide Side;
            internal P4BPost Post;
            internal GateOption Gate;
            internal int CompoundGain, GapReduction, RangedCompoundGain, ReusesOuterFieldGate, FieldGain;
            internal int SpreadPreference, CapacityGain;
            internal int UsefulArcGain, OtherFrontGain, ApproachGain, AddedCrossingCost;
        }
        private sealed class AttackCorner
        {
            internal int SideA, SideB;
            internal GridPoint FaceA, FaceB, Cell, OutwardA, OutwardB;
        }

        public static HashSet<GridPoint> Opaque(ProceduralLayout layout, TerrainSnapshot terrain = null)
        {
            // This set models line-of-sight blockers (full walls/rock and
            // closed doors). Turret buildings are partial-fill: they occupy
            // cells but do not block GenSight fire lanes.
            HashSet<GridPoint> excavated = new HashSet<GridPoint>(layout.Excavation);
            return new HashSet<GridPoint>(
            layout.BuiltWalls.Concat(layout.NaturalWalls).Concat(layout.OuterWalls)
                .Concat(layout.Gates.SelectMany(g => g.BandWalls.Concat(g.BandDoors)))
                .Concat(layout.Gates.Select(g => g.Cell)).Concat(layout.ExhaustPocketWalls)
                .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points().Where(r.IsEdge)))
                .Concat(layout.FieldWall == null ? Enumerable.Empty<GridPoint>() : layout.FieldWall.Walls
                    .Concat(new[] { layout.FieldWall.InnerGate.Value, layout.FieldWall.OuterGate }))
                .Concat(layout.ApproachDefense?.SallyPorts.SelectMany(p => p.Doors) ?? new GridPoint[0])
                .Concat(layout.FieldWall?.AccessPorts.SelectMany(p => p.Doors) ?? new GridPoint[0])
                .Concat(terrain == null ? Enumerable.Empty<GridPoint>() : terrain.Points().Where(c =>
                    terrain[c].HasAny(TerrainTraits.NaturalRock | TerrainTraits.Occupied) &&
                    !terrain[c].Has(TerrainTraits.ClearablePlant) && !excavated.Contains(c))));
        }

        public static IReadOnlyList<GridPoint> AccessGates(ProceduralLayout layout) =>
            P4BAttackSide.Copy(layout.Gates.Select(g => g.BandDoors.Concat(new[] { g.Cell })
                .OrderByDescending(c => c.X * g.Outward.X + c.Z * g.Outward.Z).First())
                .Concat(layout.FieldWall == null ? Enumerable.Empty<GridPoint>() : new[] { layout.FieldWall.OuterGate })
                .Concat(layout.FieldWall?.AccessPorts.Select(p => p.Outer) ?? Enumerable.Empty<GridPoint>())
                .Concat(layout.ApproachDefense?.SallyPorts.Select(p => p.Outer) ?? new GridPoint[0]));

        public static P4BApproachDefensePlan Plan(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            HashSet<GridPoint> opaque = Opaque(layout, input.Terrain);
            HashSet<GridPoint> interior = new HashSet<GridPoint>(layout.Enclosure.Concat(
                layout.JointSite == null ? Enumerable.Empty<GridPoint>() : layout.JointSite.Field.Points()));
            HashSet<GridPoint> enclosureCells = new HashSet<GridPoint>(layout.Enclosure);
            HashSet<GridPoint> occupied = new HashSet<GridPoint>(opaque.Concat(interior)
                .Concat(layout.PowerYards.SelectMany(y => y.Cells)).Concat(layout.ExtraObstacles));
            HashSet<GridPoint> reachable = FloodEdges(input.Terrain, occupied);
            HashSet<GridPoint> perimeter = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
                .Concat(layout.OuterWalls).Concat(layout.Gates.SelectMany(g => g.BandWalls))
                .Concat(layout.FieldWall?.Walls ?? new GridPoint[0]));
            HashSet<GridPoint> builtWalls = new HashSet<GridPoint>(layout.BuiltWalls);
            HashSet<GridPoint> outerWalls = new HashSet<GridPoint>(layout.OuterWalls);
            HashSet<GridPoint> fieldWalls = new HashSet<GridPoint>(layout.FieldWall?.Walls ?? new GridPoint[0]);
            HashSet<GridPoint> wallSupport = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
                .Concat(layout.OuterWalls).Concat(fieldWalls));
            int[] compoundGateCandidates = new int[Directions.Length];
            List<P4BAttackSide> exposed = new List<P4BAttackSide>();
            List<GridPoint>[] sideFaces = new List<GridPoint>[Directions.Length];
            List<GridPoint>[] sampleFaceColumns = new List<GridPoint>[Directions.Length];
            List<List<GridPoint>>[] faceRuns = new List<List<GridPoint>>[Directions.Length];
            for (int direction = 0; direction < Directions.Length; direction++)
            {
                GridPoint outward = Directions[direction], tangent = new GridPoint(-outward.Z, outward.X);
                List<GridPoint> allSideFaces = perimeter.Where(c => !perimeter.Contains(At(c, outward, tangent, 1, 0)) &&
                    Enumerable.Range(1, 5).Any(k => interior.Contains(At(c, outward, tangent, -k, 0))))
                    .OrderBy(c => c.X * tangent.X + c.Z * tangent.Z).ThenBy(c => c).ToList();
                sideFaces[direction] = allSideFaces;
                sampleFaceColumns[direction] = allSideFaces
                    .GroupBy(c => c.X * tangent.X + c.Z * tangent.Z)
                    .Select(g => g.OrderByDescending(c => c.X * outward.X + c.Z * outward.Z).ThenBy(c => c).First())
                    .OrderBy(c => c.X * tangent.X + c.Z * tangent.Z).ThenBy(c => c).ToList();
                faceRuns[direction] = FaceRuns(sampleFaceColumns[direction], tangent);
            }
            List<AttackCorner>[] attackCorners = ConvexAttackCorners(sideFaces, faceRuns, reachable);
            for (int direction = 0; direction < Directions.Length; direction++)
            {
                GridPoint outward = Directions[direction], tangent = new GridPoint(-outward.Z, outward.X);
                List<GridPoint> faces = sideFaces[direction];
                List<GridPoint> probes = new List<GridPoint>();
                int cornerColumns = Math.Min(2, attackCorners[direction].Count);
                int faceColumnBudget = Math.Max(faceRuns[direction].Count, AttackProbeColumnsPerSide - cornerColumns);
                int[] runColumns = AllocateFaceColumns(faceRuns[direction], faceColumnBudget);
                for (int runIndex = 0; runIndex < faceRuns[direction].Count; runIndex++)
                foreach (GridPoint face in SpreadFaceRun(faceRuns[direction][runIndex], runColumns[runIndex]))
                    foreach (int forward in new[] { 6, 12, 20, 30, 40 })
                    {
                        int fan = forward >= 30 ? forward / 2 : 4;
                        foreach (int lateral in new[] { -fan, 0, fan })
                        {
                            GridPoint c = At(face, outward, tangent, forward, lateral);
                            if (reachable.Contains(c) && P4BFireLane.CanEngage(
                                At(face, outward, tangent, 1, 0), c, 0, P4BCoverNest.ThreatRange, opaque))
                            {
                                probes.Add(c);
                            }
                        }
                    }
                foreach (AttackCorner corner in attackCorners[direction])
                {
                    GridPoint diagonal = new GridPoint(corner.OutwardA.X + corner.OutwardB.X,
                        corner.OutwardA.Z + corner.OutwardB.Z);
                    GridPoint perpendicular = new GridPoint(-diagonal.Z, diagonal.X);
                    GridPoint face = corner.SideA == direction ? corner.FaceA : corner.FaceB;
                    GridPoint origin = At(face, outward, tangent, 1, 0);
                    foreach (int forward in new[] { 6, 12, 20, 30, 40 })
                    {
                        int distance = forward / 2;
                        GridPoint center = new GridPoint(corner.Cell.X + diagonal.X * distance,
                            corner.Cell.Z + diagonal.Z * distance);
                        int fan = Math.Max(1, forward / 4);
                        foreach (int lateral in new[] { -fan, 0, fan })
                        {
                            GridPoint c = new GridPoint(center.X + perpendicular.X * lateral,
                                center.Z + perpendicular.Z * lateral);
                            int fromCornerX = c.X - corner.Cell.X;
                            int fromCornerZ = c.Z - corner.Cell.Z;
                            int outwardA = fromCornerX * corner.OutwardA.X + fromCornerZ * corner.OutwardA.Z;
                            int outwardB = fromCornerX * corner.OutwardB.X + fromCornerZ * corner.OutwardB.Z;
                            int assignedSide = outwardA > outwardB ? corner.SideA : outwardB > outwardA ? corner.SideB :
                                AttackSideTieOrder(corner.SideA) <= AttackSideTieOrder(corner.SideB)
                                    ? corner.SideA : corner.SideB;
                            if (direction != assignedSide) continue;
                            if (reachable.Contains(c) && P4BFireLane.CanEngage(
                                origin, c, 0, P4BCoverNest.ThreatRange, opaque))
                            {
                                probes.Add(c);
                            }
                        }
                    }
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
            List<P4BSallyPort> ports = new List<P4BSallyPort>(layout.FieldWall?.AccessPorts ?? new P4BSallyPort[0]);
            HashSet<GridPoint> reserved = new HashSet<GridPoint>();
            HashSet<GridPoint> arrival = new HashSet<GridPoint>(layout.Gates.SelectMany(g => g.ExteriorRoute)
                .Concat(layout.FieldWall?.ArrivalRoute ?? new GridPoint[0]));
            HashSet<GridPoint> publicCells = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input, layout)
                .Except(layout.PowerYards.SelectMany(y => y.Cells)));
            HashSet<GridPoint> basePublicCells = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input,
                layout, includeDefenseConnectors: false).Except(layout.PowerYards.SelectMany(y => y.Cells)));
            Dictionary<GridPoint, GridPoint> fieldPrevious = FieldInteriorPaths(input, layout);
            HashSet<GridPoint> reinforcement = new HashSet<GridPoint>(layout.OuterWalls.Concat(
                layout.Gates.SelectMany(g => g.BandWalls.Concat(g.BandDoors).Concat(g.OuterVestibule)))
                .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex]))));
            int pack = PackSize(input, layout);
            List<GridPoint> allSamples = exposed.SelectMany(s => s.Samples).Distinct().ToList();
            HashSet<GridPoint> compoundTargets = new HashSet<GridPoint>(perimeter.Where(w => !fieldWalls.Contains(w))
                .SelectMany(w => Directions.Select(d => new GridPoint(w.X + d.X, w.Z + d.Z)))
                .Where(c => reachable.Contains(c) && Walkable(input.Terrain, c)));
            Dictionary<P4BPost, HashSet<GridPoint>> compoundCoverageCache = new Dictionary<P4BPost, HashSet<GridPoint>>();
            Func<P4BPost, HashSet<GridPoint>> compoundCoverage = post => MainTargetCoverage(post, compoundTargets,
                (slot, target) => P4BFireLane.CanEngage(slot, target, 0, P4BCoverNest.MaximumRange, opaque),
                compoundCoverageCache);
            HashSet<GridPoint> compoundRangedTargets = new HashSet<GridPoint>(exposed
                .Where(side => side.Exposed).SelectMany(side => side.Faces.Where(face => !fieldWalls.Contains(face))
                    .Select(face => At(face, side.Outward, new GridPoint(-side.Outward.Z, side.Outward.X), 1, 0))));
            HashSet<GridPoint> compoundRangedSamples = new HashSet<GridPoint>(allSamples.Where(sample =>
                compoundRangedTargets.Any(target => P4BFireLane.CanEngage(target, sample, 0,
                    P4BCoverNest.ThreatRange, opaque))));
            Dictionary<P4BPost, HashSet<GridPoint>> compoundRangedCoverageCache = new Dictionary<P4BPost, HashSet<GridPoint>>();
            Func<P4BPost, HashSet<GridPoint>> compoundRangedCoverage = post => MainTargetCoverage(post,
                compoundRangedSamples,
                (slot, sample) => P4BFireLane.CanEngage(slot, sample, P4BCoverNest.MinimumRange,
                    P4BCoverNest.MaximumRange, opaque), compoundRangedCoverageCache);
            Dictionary<GridPoint, HashSet<GridPoint>> engagedCache = new Dictionary<GridPoint, HashSet<GridPoint>>();
            Func<GridPoint, HashSet<GridPoint>> engaged = cell =>
            {
                if (!engagedCache.TryGetValue(cell, out HashSet<GridPoint> seen))
                    engagedCache[cell] = seen = new HashSet<GridPoint>(allSamples.Where(t =>
                        P4BFireLane.CanEngage(cell, t, P4BCoverNest.MinimumRange, P4BCoverNest.MaximumRange, opaque)));
                return seen;
            };
            // Attackers shoot from beyond the defenders' own range: a slot
            // needs cover toward every sample that can see it.
            Dictionary<GridPoint, HashSet<GridPoint>> visibleCache = new Dictionary<GridPoint, HashSet<GridPoint>>();
            Func<GridPoint, HashSet<GridPoint>> visible = cell =>
            {
                if (!visibleCache.TryGetValue(cell, out HashSet<GridPoint> seen))
                    visibleCache[cell] = seen = new HashSet<GridPoint>(allSamples.Where(t =>
                        P4BFireLane.CanEngage(cell, t, 0, P4BCoverNest.ThreatRange, opaque)));
                return seen;
            };
            Dictionary<P4BAttackSide, Dictionary<GridPoint, int>> engagedCounts =
                new Dictionary<P4BAttackSide, Dictionary<GridPoint, int>>();
            Dictionary<P4BAttackSide, Dictionary<GridPoint, bool>> facesSideCache =
                new Dictionary<P4BAttackSide, Dictionary<GridPoint, bool>>();
            Func<P4BAttackSide, GridPoint, bool> facesSide = (side, cell) =>
            {
                if (!facesSideCache.TryGetValue(side, out Dictionary<GridPoint, bool> cache))
                    facesSideCache.Add(side, cache = new Dictionary<GridPoint, bool>());
                if (cache.TryGetValue(cell, out bool result)) return result;
                if (!side.Exposed || !side.NearFacesSet.Contains(cell)) result = false;
                else
                {
                    if (!engagedCounts.TryGetValue(side, out Dictionary<GridPoint, int> counts))
                        engagedCounts.Add(side, counts = new Dictionary<GridPoint, int>());
                    if (!counts.TryGetValue(cell, out int shots))
                        counts.Add(cell, shots = side.Samples.Count(engaged(cell).Contains));
                    result = shots >= P4BCoverNest.MinimumShots;
                }
                cache.Add(cell, result);
                return result;
            };
            List<GateOption> gates = layout.Gates
                .Select(g => new GateOption { Outward = g.Outward, Cell = g.BandDoors.Concat(new[] { g.Cell })
                    .OrderByDescending(c => c.X * g.Outward.X + c.Z * g.Outward.Z).First() }).ToList();
            if (layout.FieldWall != null)
            {
                gates.Add(new GateOption { Cell = layout.FieldWall.OuterGate,
                    Outward = layout.Gates[layout.FieldWall.GateIndex].Outward, Field = true });
                gates.AddRange(layout.FieldWall.AccessPorts.Select(port => new GateOption
                    { Cell = port.Outer, Outward = port.Outward, Port = port, Field = true }));
            }
            // One-layer walls may use a single door. Thick walls are
            // considered as one serial crossing through every shell.
            foreach (P4BAttackSide faceSide in exposed.Where(s => s.Exposed))
            {
                GridPoint across = new GridPoint(-faceSide.Outward.Z, faceSide.Outward.X);
                List<P4BSallyPort> singleLayerPorts = new List<P4BSallyPort>();
                if (layout.WallLayers == 1)
                    foreach (GridPoint face in faceSide.Faces.Where(face => builtWalls.Contains(face) &&
                        !reinforcement.Contains(face) && Buildable(input.Terrain, face) &&
                        reachable.Contains(At(face, faceSide.Outward, across, 1, 0)) &&
                        Enumerable.Range(1, 2).SelectMany(offset => new[] { -offset, offset })
                            .All(offset => wallSupport.Contains(At(face, faceSide.Outward, across, 0, offset))))
                        .OrderBy(c => c))
                    {
                        GridPoint inside = At(face, faceSide.Outward, across, -1, 0);
                        List<GridPoint> connector = PublicInteriorConnector(input, layout, inside, publicCells, enclosureCells);
                        if (connector == null) continue;
                        singleLayerPorts.Add(new P4BSallyPort(face, inside, faceSide.Outward,
                            interiorRoute: connector.Count <= 1 ? null : connector));
                    }
                int gateSideIndex = SideIndex(faceSide.Outward);
                if (gateSideIndex >= 0) compoundGateCandidates[gateSideIndex] = singleLayerPorts.Count;
                HashSet<GridPoint> selectedSinglePorts = new HashSet<GridPoint>(Spread(
                    singleLayerPorts.Select(port => port.Cell).ToList(), MaxGateCandidatesPerSide));
                gates.AddRange(singleLayerPorts.Where(port => selectedSinglePorts.Contains(port.Cell)).Select(port =>
                    new GateOption { Cell = port.Outer, Outward = faceSide.Outward, Port = port }));
                if (layout.WallLayers > 1)
                {
                    List<P4BSallyPort> gatehouses = new List<P4BSallyPort>();
                    HashSet<GridPoint> existingGateParts = new HashSet<GridPoint>(layout.Gates.SelectMany(g =>
                        g.BandWalls.Concat(g.BandDoors).Concat(g.OuterVestibule).Concat(new[] { g.Cell })));
                    foreach (GridPoint innerDoor in layout.BuiltWalls.OrderBy(c => c))
                    {
                        GridPoint outerFace = new GridPoint(innerDoor.X + (layout.WallLayers - 1) * faceSide.Outward.X,
                            innerDoor.Z + (layout.WallLayers - 1) * faceSide.Outward.Z);
                        if (!faceSide.FaceSet.Contains(outerFace)) continue;
                        GridPoint inside = new GridPoint(innerDoor.X - faceSide.Outward.X,
                            innerDoor.Z - faceSide.Outward.Z);
                        if (!Buildable(input.Terrain, innerDoor)) continue;
                        List<GridPoint> connector = PublicInteriorConnector(input, layout, inside, publicCells, enclosureCells);
                        if (connector == null) continue;
                        List<GridPoint> band = Enumerable.Range(1, layout.WallLayers - 1).Select(layer =>
                            new GridPoint(innerDoor.X + layer * faceSide.Outward.X,
                                innerDoor.Z + layer * faceSide.Outward.Z)).ToList();
                        HashSet<GridPoint> crossingDoors = new HashSet<GridPoint>(new[] { innerDoor }.Concat(band));
                        bool flanks = Enumerable.Range(0, layout.WallLayers)
                            .SelectMany(layer => new[] { -2, -1, 1, 2 }.Select(offset =>
                                new GridPoint(innerDoor.X + layer * faceSide.Outward.X + offset * across.X,
                                    innerDoor.Z + layer * faceSide.Outward.Z + offset * across.Z)))
                            .All(c => perimeter.Contains(c) && !existingGateParts.Contains(c));
                        bool clearPassage = crossingDoors.All(c => Walkable(input.Terrain, c) && opaque.Contains(c));
                        GridPoint outside = new GridPoint(outerFace.X + faceSide.Outward.X,
                            outerFace.Z + faceSide.Outward.Z);
                        if (!band.Last().Equals(outerFace) || band.Any(c => !outerWalls.Contains(c)) ||
                            !flanks || !clearPassage ||
                            !reachable.Contains(outside) || crossingDoors.Any(c => existingGateParts.Contains(c) ||
                                !input.Terrain[c].Has(TerrainTraits.Buildable))) continue;
                        gatehouses.Add(new P4BSallyPort(innerDoor, inside, faceSide.Outward,
                            interiorRoute: connector.Count <= 1 ? null : connector, band: band));
                    }
                    if (gateSideIndex >= 0) compoundGateCandidates[gateSideIndex] = gatehouses.Count;
                    HashSet<GridPoint> selectedGatehouses = new HashSet<GridPoint>(Spread(
                        gatehouses.Select(port => port.Outer).OrderBy(c => c).ToList(), MaxGateCandidatesPerSide));
                    gates.AddRange(gatehouses.Where(port => selectedGatehouses.Contains(port.Outer)).Select(port =>
                        new GateOption { Cell = port.Outer, Outward = faceSide.Outward, Port = port }));
                }
                // The enclosed, unroofed field is also reachable ground.
                // A side gate replaces an existing wall cell and connects
                // through it without consuming a crop or roof cell.
                List<GridPoint> fieldFaces = faceSide.Faces.Where(face =>
                    fieldWalls.Contains(face) &&
                    fieldPrevious.ContainsKey(At(face, faceSide.Outward, across, -1, 0)) &&
                    reachable.Contains(At(face, faceSide.Outward, across, 1, 0))).ToList();
                List<GridPoint> routeAdjacentFieldFaces = fieldFaces.Where(face =>
                    arrival.Contains(At(face, faceSide.Outward, across, 1, 0))).ToList();
                HashSet<GridPoint> selectedFieldFaces = new HashSet<GridPoint>(
                    Spread(fieldFaces, MaxGateCandidatesPerSide).Concat(
                        Spread(routeAdjacentFieldFaces, MaxGateCandidatesPerSide)));
                gates.AddRange(fieldFaces.Where(selectedFieldFaces.Contains).Select(face =>
                {
                    GridPoint inside = At(face, faceSide.Outward, across, -1, 0);
                    List<GridPoint> path = new List<GridPoint>();
                    for (GridPoint c = inside; ; c = fieldPrevious[c])
                    { path.Add(c); if (fieldPrevious[c].Equals(c)) break; }
                    path.Reverse();
                    return new GateOption { Cell = face, Outward = faceSide.Outward,
                        Port = new P4BSallyPort(face, inside, faceSide.Outward, true, path), Field = true };
                }));
            }
            // A slot holds a side only when that side's shooters mostly cross
            // its cover: along a row the neighbouring slot is no barricade.
            Dictionary<P4BAttackSide, Dictionary<P4BPost, Dictionary<GridPoint, bool>>> holdsCache =
                new Dictionary<P4BAttackSide, Dictionary<P4BPost, Dictionary<GridPoint, bool>>>();
            Func<P4BAttackSide, P4BPost, GridPoint, bool> holds = (side, post, c) =>
            {
                if (!holdsCache.TryGetValue(side, out Dictionary<P4BPost, Dictionary<GridPoint, bool>> postsBySide))
                    holdsCache.Add(side, postsBySide = new Dictionary<P4BPost, Dictionary<GridPoint, bool>>());
                if (!postsBySide.TryGetValue(post, out Dictionary<GridPoint, bool> cellsByPost))
                    postsBySide.Add(post, cellsByPost = new Dictionary<GridPoint, bool>());
                if (cellsByPost.TryGetValue(c, out bool result)) return result;
                result = facesSide(side, c) && P4BCoverNest.Shielded(c,
                    side.Samples.Where(visible(c).Contains), post.CoverCellsSet, opaque.Contains);
                cellsByPost.Add(c, result);
                return result;
            };
            List<GridPoint> doors = layout.Gates.SelectMany(g => g.BandDoors.Concat(new[] { g.Cell }))
                .Concat(layout.FieldWall == null ? new GridPoint[0] : new[] { layout.FieldWall.OuterGate }
                    .Concat(layout.FieldWall.InnerGate.HasValue ? new[] { layout.FieldWall.InnerGate.Value } : new GridPoint[0]))
                .Concat(layout.FieldWall?.AccessPorts.SelectMany(p => p.Doors) ?? new GridPoint[0])
                .ToList();
            string coverDef = P4BCoverNest.CoverDefName(input);
            var nests = new Dictionary<KeyValuePair<GridPoint, int>, P4BPost>();
            // A bounded greedy pass compares the next legal post from every
            // exposed front before spending another logical crossing.
            int maxIterations = 2 * (MaxCompoundGateCrossingsTotal + Directions.Length);
            for (int iteration = 0; coverDef != null && iteration < maxIterations; iteration++)
            {
                HashSet<GridPoint> seenCompound = new HashSet<GridPoint>(posts.SelectMany(compoundCoverage));
                HashSet<GridPoint> seenRangedCompound = new HashSet<GridPoint>(posts.SelectMany(compoundRangedCoverage));
                List<RankedPostOption> candidates = new List<RankedPostOption>();
                foreach (P4BAttackSide side in exposed.Where(s => s.Exposed))
                {
                    Func<P4BPost, int> facing = post => post.Slots.Count(c => holds(side, post, c));
                    GridPoint sideTangent = new GridPoint(-side.Outward.Z, side.Outward.X);
                    HashSet<GridPoint> sideFieldTargets = new HashSet<GridPoint>(side.Faces
                        .Where(fieldWalls.Contains)
                        .Select(face => At(face, side.Outward, sideTangent, 1, 0))
                        .Where(c => reachable.Contains(c) && Walkable(input.Terrain, c)));
                    Dictionary<P4BPost, HashSet<GridPoint>> fieldCoverageCache =
                        new Dictionary<P4BPost, HashSet<GridPoint>>();
                    Func<P4BPost, HashSet<GridPoint>> fieldCoverage = post =>
                    {
                        if (!fieldCoverageCache.TryGetValue(post, out HashSet<GridPoint> coverage))
                            fieldCoverageCache[post] = coverage = new HashSet<GridPoint>(sideFieldTargets.Where(target =>
                                post.Slots.Any(slot => P4BFireLane.CanEngage(slot, target, 0,
                                    P4BCoverNest.MaximumRange, opaque))));
                        return coverage;
                    };
                    HashSet<GridPoint> seenFieldTargets = new HashSet<GridPoint>(posts.SelectMany(fieldCoverage));
                    bool outerFieldGateSelected = posts.Any(post => IsOuterFieldGateNest(layout, post));
                    HashSet<GridPoint> seen = new HashSet<GridPoint>();
                    foreach (P4BPost existingPost in posts)
                        foreach (GridPoint slot in existingPost.Slots)
                            if (holds(side, existingPost, slot))
                                foreach (GridPoint sample in engaged(slot))
                                    if (side.SampleSet.Contains(sample)) seen.Add(sample);
                    int have = posts.Sum(facing);
                    if (have >= pack && seen.Count >= UsefulArc(side.Samples.Count) &&
                        seenCompound.Count >= compoundTargets.Count) continue;

                    foreach (GateOption gate in gates)
                    {
                        bool alreadySelectedPort = gate.Port != null && ports.Any(p => p.Cell.Equals(gate.Port.Cell));
                        if (gate.Port != null && !alreadySelectedPort)
                        {
                            int gateSide = SideIndex(gate.Outward);
                            if (gate.Port.Field)
                            {
                                int[] fieldCounts = FieldGateCrossings(layout, ports,
                                    includeOuterGate: outerFieldGateSelected);
                                if (gateSide < 0 || fieldCounts[gateSide] >= MaxFieldGateCrossingsPerSide) continue;
                            }
                            else if (gateSide < 0 || CompoundGateCrossings(layout, ports).Sum() >= MaxCompoundGateCrossingsTotal)
                                continue;
                        }
                        if (gate.Field && gate.Port == null && IsOuterFieldGate(layout, gate) &&
                            !outerFieldGateSelected)
                        {
                            int gateSide = SideIndex(gate.Outward);
                            int[] fieldCounts = FieldGateCrossings(layout, ports, includeOuterGate: true);
                            if (gateSide < 0 || fieldCounts[gateSide] > MaxFieldGateCrossingsPerSide) continue;
                        }
                        // Keep every port's two-cell wall flanks solid after adding another port.
                        if (gate.Port != null && PortConflictsWithWallSupport(gate.Port, doors, ports)) continue;

                        foreach (int flank in new[] { -1, 1 })
                        {
                            var key = new KeyValuePair<GridPoint, int>(gate.Cell, flank);
                            P4BPost post;
                            if (nests.TryGetValue(key, out P4BPost cached) &&
                                (cached == null || !ConflictsWithPosts(cached, posts))) post = cached;
                            else
                            {
                                bool CanOpenPostCell(GridPoint c) => navigation.Contains(c) &&
                                    (!reserved.Contains(c) || SharedAccessPrefixCell(c, gate, flank, posts)) &&
                                    !occupied.Contains(c) && (!arrival.Contains(c) ||
                                        IsArrivalCrossingCell(c, gate.Cell, gate.Outward, flank, layout.Gates) ||
                                        gate.Field && IsFieldArrivalCrossingCell(c, gate.Cell,
                                            gate.Outward, arrival));
                                post = P4BCoverNest.Build(side.Id, gate.Cell, gate.Outward, flank, P4BCoverNest.MaxSlots,
                                    c => visible(c), c => exposed.Any(s => facesSide(s, c)), CanOpenPostCell,
                                    c => !occupied.Contains(c) && !reserved.Contains(c) && !arrival.Contains(c) &&
                                        !reinforcement.Contains(c) && Buildable(input.Terrain, c), perimeter.Contains,
                                    c => !occupied.Contains(c) && !reserved.Contains(c) && !arrival.Contains(c) &&
                                        !reinforcement.Contains(c) && Buildable(input.Terrain, c));
                                nests[key] = post;
                            }
                            if (post == null || ConflictsWithPosts(post, posts) || facing(post) == 0) continue;
                            HashSet<GridPoint> coverage = compoundCoverage(post);
                            int compoundGain = coverage.Count(c => !seenCompound.Contains(c));
                            int gapGain = GapReduction(compoundTargets, seenCompound, coverage);
                            int rangedCompoundGain = compoundRangedCoverage(post).Count(c => !seenRangedCompound.Contains(c));
                            HashSet<GridPoint> fieldTargets = fieldCoverage(post);
                            int fieldGain = fieldTargets.Count(c => !seenFieldTargets.Contains(c));
                            int capacityGain = Math.Min(facing(post), Math.Max(0, pack - have));
                            int approachGain = side.Samples.Count(t => !seen.Contains(t) &&
                                post.Slots.Any(c => holds(side, post, c) && engaged(c).Contains(t)));
                            int arcGain = seen.Count < UsefulArc(side.Samples.Count) ? approachGain : 0;
                            if (capacityGain == 0 && arcGain == 0 && compoundGain == 0 && rangedCompoundGain == 0) continue;
                            if (gate.Field && (fieldGain == 0 || approachGain == 0)) continue;
                            bool addsCompoundCrossing = gate.Port != null && !gate.Port.Field && !alreadySelectedPort;
                            if (addsCompoundCrossing && compoundGain == 0 && rangedCompoundGain == 0) continue;
                            int direction = SideIndex(gate.Outward);
                            int[] sideCounts = gate.Port?.Field == true
                                ? FieldGateCrossings(layout, ports, includeOuterGate: outerFieldGateSelected)
                                : CompoundGateCrossings(layout, ports);
                            int spread = addsCompoundCrossing && direction >= 0 &&
                                sideCounts[direction] < PreferredCompoundGateCrossingsPerSide ? 1 : 0;
                            candidates.Add(new RankedPostOption
                            {
                                Side = side, Post = post, Gate = gate, CompoundGain = compoundGain,
                                GapReduction = gapGain, RangedCompoundGain = rangedCompoundGain,
                                ReusesOuterFieldGate = IsOuterFieldGate(layout, gate) ? 1 : 0,
                                FieldGain = fieldGain,
                                SpreadPreference = spread, CapacityGain = capacityGain,
                                UsefulArcGain = arcGain,
                                OtherFrontGain = exposed.Count(s => post.Slots.Any(c => holds(s, post, c))),
                                ApproachGain = approachGain, AddedCrossingCost = addsCompoundCrossing ||
                                    (gate.Port != null && !alreadySelectedPort) ? 1 : 0
                            });
                        }
                    }
                }

                bool accepted = false;
                foreach (RankedPostOption option in RankCandidates(candidates,
                    c => c.CompoundGain, c => c.GapReduction, c => c.RangedCompoundGain,
                    c => c.ReusesOuterFieldGate, c => c.FieldGain,
                    c => c.SpreadPreference, c => c.CapacityGain,
                    c => c.UsefulArcGain, c => c.OtherFrontGain, c => c.ApproachGain,
                    c => c.AddedCrossingCost, c => c.Post.Cell))
                {
                    P4BAttackSide side = option.Side;
                    P4BPost post = option.Post;
                    HashSet<GridPoint> newCompound = compoundCoverage(post);
                    if (Materials(input, layout, posts.Concat(new[] { post }), ports.Concat(
                        option.Gate.Port == null || ports.Any(p => p.Cell.Equals(option.Gate.Port.Cell))
                            ? Enumerable.Empty<P4BSallyPort>() : new[] { option.Gate.Port })).OrdinaryImportValue >
                        input.Requirements.BaseImportValueLimit) continue;
                    if (post.Side != side.Id)
                        post = new P4BPost(side.Id, post.Outward, post.AccessGate, post.Route, post.Slots,
                            post.CoverCells, post.WallReturn);
                    posts.Add(post);
                    reserved.UnionWith(post.Route.Skip(1).Concat(post.CoverCells));
                    if (post.WallReturn.HasValue) reserved.Add(post.WallReturn.Value);
                    if (option.Gate.Port != null && !ports.Any(p => p.Cell.Equals(option.Gate.Port.Cell)))
                        ports.Add(option.Gate.Port);
                    accepted = true;
                    break;
                }
                if (!accepted) break;
            }
            List<P4BAttackSide> sides = exposed.Select(side =>
            {
                if (!side.Exposed) return side;
                Func<P4BPost, int> facing = post => post.Slots.Count(c => holds(side, post, c));
                P4BPost selected = posts.FirstOrDefault(p => facing(p) > 0);
                GridPoint? best = posts.SelectMany(p => p.Slots.Where(c => holds(side, p, c)))
                    .OrderByDescending(c => side.Samples.Count(engaged(c).Contains)).ThenBy(c => c)
                    .Cast<GridPoint?>().FirstOrDefault();
                string omission = coverDef == null ? "cover_def_unavailable" : "no_connected_short_gate_position";
                return new P4BAttackSide(side.Id, side.Outward, side.Faces, side.Samples,
                    best ?? selected?.Cell, selected == null ? omission : null, side.OpenGroundArea);
            }).ToList();
            // Wall returns are opaque. Register every slot that still has
            // useful shots, so the garrison and gun planner share one inventory.
            opaque.UnionWith(posts.Where(p => p.WallReturn.HasValue).Select(p => p.WallReturn.Value));
            // Re-audit main-wall potential against the completed defense. The
            // selection cache was scored before returns existed; use the final
            // opaque/occupied flood and the same no-corner-cut rule as metric v3.
            HashSet<GridPoint> finalBlocked = new HashSet<GridPoint>(opaque.Concat(layout.Enclosure)
                .Concat(layout.JointSite == null ? Enumerable.Empty<GridPoint>() : layout.JointSite.Field.Points())
                .Concat(layout.PowerYards.SelectMany(y => y.Cells)).Concat(layout.ExtraObstacles));
            HashSet<GridPoint> finalReachable = FloodEdgesNoCornerCut(input.Terrain, finalBlocked);
            compoundTargets.Clear();
            compoundTargets.UnionWith(perimeter.Where(wall => !fieldWalls.Contains(wall))
                .SelectMany(wall => Directions.Select(d => new GridPoint(wall.X + d.X, wall.Z + d.Z)))
                .Where(c => finalReachable.Contains(c) && Walkable(input.Terrain, c)));
            compoundCoverageCache.Clear();
            sides = sides.Select(s => new P4BAttackSide(s.Id, s.Outward, s.Faces, s.Samples, s.Post,
                s.OmissionReason, s.OpenGroundArea,
                posts.SelectMany(p => p.Slots.Where(c => s.Exposed &&
                    s.NearFacesSet.Contains(c) &&
                    s.Samples.Count(t => P4BFireLane.CanEngage(c, t, P4BCoverNest.MinimumRange,
                        P4BCoverNest.MaximumRange, opaque)) >= P4BCoverNest.MinimumShots &&
                    P4BCoverNest.Shielded(c,
                        s.Samples.Where(t => P4BFireLane.CanEngage(c, t, 0, P4BCoverNest.ThreatRange, opaque)),
                        p.CoverCellsSet, opaque.Contains))))).ToList();
            HashSet<GridPoint> compoundCoveredTargets = new HashSet<GridPoint>(posts.SelectMany(compoundCoverage));
            return new P4BApproachDefensePlan(reachable, sides, posts, previous.Keys
                .Except(posts.Where(p => p.WallReturn.HasValue).Select(p => p.WallReturn.Value))
                .Concat(posts.SelectMany(p => p.Route))
                .Concat(ports.SelectMany(p => p.InteriorRoute)), ports, coverDef, pack,
                compoundTargets.Count, compoundCoveredTargets.Count, compoundGateCandidates);
        }

        internal static HashSet<GridPoint> MainTargetCoverage(P4BPost post, IEnumerable<GridPoint> targets,
            Func<GridPoint, GridPoint, bool> canEngage, Dictionary<P4BPost, HashSet<GridPoint>> cache)
        {
            if (!cache.TryGetValue(post, out HashSet<GridPoint> hit))
                cache[post] = hit = new HashSet<GridPoint>((targets ?? Enumerable.Empty<GridPoint>()).Where(target =>
                    post.Slots.Any(slot => canEngage(slot, target))));
            return hit;
        }

        internal static IEnumerable<T> RankCandidates<T>(IEnumerable<T> candidates,
            Func<T, int> compoundCoverageGain, Func<T, int> compoundGapReduction,
            Func<T, int> rangedCompoundCoverageGain, Func<T, int> reusesOuterFieldGate,
            Func<T, int> fieldFaceGain, Func<T, int> spreadPreference,
            Func<T, int> capacityGain, Func<T, int> usefulArcGain,
            Func<T, int> otherFrontGain, Func<T, int> approachGain, Func<T, int> addedCrossingCost,
            Func<T, GridPoint> cell) =>
            candidates.OrderByDescending(reusesOuterFieldGate).ThenByDescending(compoundCoverageGain)
                .ThenByDescending(compoundGapReduction).ThenByDescending(rangedCompoundCoverageGain)
                .ThenByDescending(fieldFaceGain).ThenByDescending(spreadPreference)
                .ThenByDescending(capacityGain).ThenByDescending(usefulArcGain)
                .ThenByDescending(otherFrontGain).ThenByDescending(approachGain)
                .ThenBy(addedCrossingCost).ThenBy(cell);

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
            HashSet<GridPoint> fieldCells = new HashSet<GridPoint>(layout.JointSite.Field.Points());
            Queue<GridPoint> queue = new Queue<GridPoint>();
            previous[entry] = entry; queue.Enqueue(entry);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                foreach (GridPoint direction in Directions)
                {
                    GridPoint next = new GridPoint(here.X + direction.X, here.Z + direction.Z);
                    if (!fieldCells.Contains(next) || walls.Contains(next) ||
                        next.Equals(layout.FieldWall.OuterGate) || !Walkable(input.Terrain, next) || previous.ContainsKey(next)) continue;
                    previous[next] = here; queue.Enqueue(next);
                }
            }
            return previous;
        }

        private static List<GridPoint> PublicInteriorConnector(ProceduralLayoutInput input,
            ProceduralLayout layout, GridPoint start, HashSet<GridPoint> publicCells,
            HashSet<GridPoint> enclosureCells)
        {
            if (publicCells.Contains(start)) return new List<GridPoint> { start };
            HashSet<GridPoint> privateCells = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Bounds.Points())
                .Concat(layout.PowerYards.SelectMany(y => y.Cells.Concat(y.ZoneCells)))
                .Concat(layout.Courts.SelectMany(c => c.Cells)).Concat(layout.ExtraObstacles)
                .Concat(layout.ExhaustPocketWalls).Concat(layout.ExhaustPockets.Select(p => p.Cell)));
            HashSet<GridPoint> walls = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
                .Concat(layout.OuterWalls).Concat(layout.Gates.SelectMany(g => g.BandWalls.Concat(g.BandDoors)))
                .Concat(layout.FieldWall?.Walls ?? new GridPoint[0]));
            if (!enclosureCells.Contains(start) || !Buildable(input.Terrain, start) ||
                privateCells.Contains(start) || walls.Contains(start)) return null;
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint> { [start] = start };
            Queue<GridPoint> queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            GridPoint? destination = null;
            int visited = 0;
            while (queue.Count > 0 && visited++ < input.Budget.MaxRouteNodes)
            {
                GridPoint here = queue.Dequeue();
                if (publicCells.Contains(here)) { destination = here; break; }
                foreach (GridPoint direction in Directions)
                {
                    GridPoint next = new GridPoint(here.X + direction.X, here.Z + direction.Z);
                    if (!enclosureCells.Contains(next) || previous.ContainsKey(next) || privateCells.Contains(next) ||
                        walls.Contains(next) || !Buildable(input.Terrain, next)) continue;
                    previous[next] = here;
                    queue.Enqueue(next);
                }
            }
            if (!destination.HasValue) return null;
            List<GridPoint> route = new List<GridPoint>();
            for (GridPoint c = destination.Value; ; c = previous[c])
            { route.Add(c); if (c.Equals(start)) break; }
            route.Reverse();
            return route.Count <= MaxInteriorConnectorCells ? route : null;
        }

        internal static int UsefulArc(int sampleCount) => Math.Max(4, (int)Math.Ceiling(sampleCount * 0.35));

        internal static int SideIndex(GridPoint outward) =>
            Array.FindIndex(Directions, d => d.Equals(outward));

        internal static int[] CompoundGateCrossings(ProceduralLayout layout,
            IEnumerable<P4BSallyPort> additionalPorts = null)
        {
            int[] counts = new int[Directions.Length];
            foreach (LayoutGate gate in layout.Gates)
            {
                int side = SideIndex(gate.Outward);
                if (side >= 0) counts[side]++;
            }
            IEnumerable<P4BSallyPort> ports = (layout.ApproachDefense?.SallyPorts ?? new P4BSallyPort[0])
                .Concat(layout.FieldWall?.AccessPorts ?? new P4BSallyPort[0])
                .Concat(additionalPorts ?? Enumerable.Empty<P4BSallyPort>())
                .Where(p => !p.Field).GroupBy(p => p.Cell).Select(g => g.First());
            foreach (P4BSallyPort port in ports)
            {
                int side = SideIndex(port.Outward);
                if (side >= 0) counts[side]++;
            }
            return counts;
        }

        internal static int[] FieldGateCrossings(ProceduralLayout layout,
            IEnumerable<P4BSallyPort> additionalPorts = null, bool? includeOuterGate = null)
        {
            int[] counts = new int[Directions.Length];
            P4BFieldWallPlan fieldWall = layout.FieldWall;
            if (fieldWall == null || layout.JointSite == null || !fieldWall.InnerGate.HasValue) return counts;
            GridPoint compoundOutward = layout.Gates[fieldWall.GateIndex].Outward;
            int innerSide = SideIndex(new GridPoint(-compoundOutward.X, -compoundOutward.Z));
            int outerSide = SideIndex(compoundOutward);
            if (innerSide >= 0) counts[innerSide]++;
            bool countOuterGate = includeOuterGate ?? (layout.ApproachDefense?.Posts.Any(post =>
                IsOuterFieldGateNest(layout, post)) == true);
            if (countOuterGate && outerSide >= 0) counts[outerSide]++;
            IEnumerable<P4BSallyPort> ports = (layout.ApproachDefense?.SallyPorts ?? new P4BSallyPort[0])
                .Concat(fieldWall.AccessPorts)
                .Concat(additionalPorts ?? Enumerable.Empty<P4BSallyPort>())
                .Where(p => p.Field).GroupBy(p => p.Cell).Select(g => g.First());
            foreach (P4BSallyPort port in ports)
            {
                int side = SideIndex(port.Outward);
                if (side >= 0) counts[side]++;
            }
            return counts;
        }

        private static bool IsOuterFieldGate(ProceduralLayout layout, GateOption gate)
        {
            P4BFieldWallPlan fieldWall = layout.FieldWall;
            return fieldWall != null && gate.Field && gate.Port == null &&
                gate.Cell.Equals(fieldWall.OuterGate) &&
                gate.Outward.Equals(layout.Gates[fieldWall.GateIndex].Outward);
        }

        private static bool IsOuterFieldGateNest(ProceduralLayout layout, P4BPost post)
        {
            P4BFieldWallPlan fieldWall = layout.FieldWall;
            return fieldWall != null && post.AccessGate.Equals(fieldWall.OuterGate) &&
                post.Outward.Equals(layout.Gates[fieldWall.GateIndex].Outward);
        }

        private static bool PortConflictsWithWallSupport(P4BSallyPort candidate,
            IEnumerable<GridPoint> existingDoors, IEnumerable<P4BSallyPort> existingPorts)
        {
            List<P4BSallyPort> otherPorts = (existingPorts ?? Enumerable.Empty<P4BSallyPort>())
                .Where(p => !p.Cell.Equals(candidate.Cell)).ToList();
            HashSet<GridPoint> otherDoors = new HashSet<GridPoint>((existingDoors ?? Enumerable.Empty<GridPoint>())
                .Concat(otherPorts.SelectMany(p => p.Doors)));
            GridPoint tangent = new GridPoint(-candidate.Outward.Z, candidate.Outward.X);
            for (int layer = 0; layer < candidate.Doors.Count; layer++)
            {
                GridPoint center = new GridPoint(candidate.Cell.X + layer * candidate.Outward.X,
                    candidate.Cell.Z + layer * candidate.Outward.Z);
                if (new[] { -2, -1, 1, 2 }.Any(offset => otherDoors.Contains(new GridPoint(
                    center.X + offset * tangent.X, center.Z + offset * tangent.Z)))) return true;
            }
            foreach (P4BSallyPort port in otherPorts)
            {
                GridPoint portTangent = new GridPoint(-port.Outward.Z, port.Outward.X);
                for (int layer = 0; layer < port.Doors.Count; layer++)
                {
                    GridPoint center = new GridPoint(port.Cell.X + layer * port.Outward.X,
                        port.Cell.Z + layer * port.Outward.Z);
                    if (new[] { -2, -1, 1, 2 }.Any(offset => candidate.Doors.Contains(new GridPoint(
                        center.X + offset * portTangent.X, center.Z + offset * portTangent.Z)))) return true;
                }
            }
            return false;
        }

        internal static IReadOnlyList<string> ValidateGateCaps(ProceduralLayout layout)
        {
            List<string> errors = new List<string>();
            int[] compound = CompoundGateCrossings(layout);
            int[] field = FieldGateCrossings(layout);
            if (compound.Sum() > MaxCompoundGateCrossingsTotal)
                errors.Add("compound_gate_total_cap");
            for (int side = 0; side < Directions.Length; side++)
            {
                if (field[side] > MaxFieldGateCrossingsPerSide)
                    errors.Add("field_gate_side_cap_" + Names[side]);
            }
            return errors;
        }

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
            IEnumerable<P4BPost> posts = null, IEnumerable<P4BSallyPort> ports = null)
        {
            List<P4BSallyPort> allPorts = (layout.ApproachDefense?.SallyPorts ?? new P4BSallyPort[0])
                .Concat(layout.FieldWall?.AccessPorts ?? new P4BSallyPort[0])
                .Concat(ports ?? Enumerable.Empty<P4BSallyPort>()).GroupBy(p => p.Cell).Select(g => g.First()).ToList();
            return input.Equipment.Count(layout.Rooms, layout.BuiltWalls.Except(allPorts.Select(p => p.Cell))
                .Concat((posts ?? Enumerable.Empty<P4BPost>()).Where(p => p.WallReturn.HasValue).Select(p => p.WallReturn.Value))
                .Concat(layout.OuterWalls.Except(allPorts.SelectMany(p => p.Band)))
                .Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(layout.ExhaustPocketWalls)
                .Concat((layout.FieldWall?.Walls ?? new GridPoint[0]).Except(
                    allPorts.Where(p => p.Field).SelectMany(p => p.Doors))),
                layout.Gates.Sum(g => 1 + g.BandDoors.Count) + (layout.FieldWall == null ? 0 : 2) +
                    allPorts.Sum(p => p.Doors.Count), layout.Power);
        }

        public static IReadOnlyList<string> Validate(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            P4BApproachDefensePlan plan = layout.ApproachDefense;
            if (plan == null) return new[] { "missing_approach_defense" };
            List<string> errors = new List<string>();
            HashSet<GridPoint> opaque = Opaque(layout, input.Terrain);
            HashSet<GridPoint> occupied = new HashSet<GridPoint>(opaque.Concat(layout.Enclosure)
                .Concat(layout.JointSite?.Field.Points() ?? new GridPoint[0])
                .Concat(layout.PowerYards.SelectMany(y => y.Cells)).Concat(layout.ExtraObstacles));
            occupied.UnionWith(layout.Turret?.All.SelectMany(t => t.Footprint) ?? new GridPoint[0]);
            HashSet<GridPoint> cover = new HashSet<GridPoint>();
            HashSet<GridPoint> publicCells = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input, layout)
                .Except(layout.PowerYards.SelectMany(y => y.Cells)));
            HashSet<GridPoint> basePublicCells = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input,
                layout, includeDefenseConnectors: false).Except(layout.PowerYards.SelectMany(y => y.Cells)));
            Dictionary<GridPoint, GridPoint> fieldPaths = FieldInteriorPaths(input, layout);
            HashSet<GridPoint> priorDoorCells = new HashSet<GridPoint>(layout.Gates.SelectMany(g =>
                g.BandDoors.Concat(new[] { g.Cell })).Concat(layout.FieldWall == null ? new GridPoint[0] :
                    new[] { layout.FieldWall.OuterGate }.Concat(layout.FieldWall.InnerGate.HasValue
                        ? new[] { layout.FieldWall.InnerGate.Value } : new GridPoint[0])));
            HashSet<GridPoint> shell = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
                .Concat(layout.OuterWalls).Concat(layout.FieldWall?.Walls ?? Enumerable.Empty<GridPoint>())
                .Concat(plan.SallyPorts.SelectMany(p => p.Band)));
            foreach (P4BSallyPort port in plan.SallyPorts)
            {
                if (!Directions.Contains(port.Outward) || !port.Inside.Equals(new GridPoint(
                    port.Cell.X - port.Outward.X, port.Cell.Z - port.Outward.Z)) ||
                    (!port.Field && !publicCells.Contains(port.Inside)) ||
                    (!port.Field && (port.InteriorRoute.Count > MaxInteriorConnectorCells || port.InteriorRoute.Any(c =>
                        !layout.Enclosure.Contains(c) || !Walkable(input.Terrain, c) ||
                        layout.Rooms.Any(r => r.Bounds.Contains(c)) || layout.PowerYards.Any(y => y.Cells.Contains(c))) ||
                        (port.InteriorRoute.Count == 0 ? !basePublicCells.Contains(port.Inside) :
                            !port.InteriorRoute.First().Equals(port.Inside) ||
                            !basePublicCells.Contains(port.InteriorRoute.Last()) ||
                            port.InteriorRoute.Zip(port.InteriorRoute.Skip(1), (a, b) =>
                                Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z)).Any(d => d != 1)))) ||
                    (port.Field && port.Band.Count != 0) ||
                    (!port.Field && port.Band.Count != Math.Max(0, layout.WallLayers - 1)) ||
                    port.Band.Where((c, index) => !c.Equals(new GridPoint(
                        port.Cell.X + (index + 1) * port.Outward.X,
                        port.Cell.Z + (index + 1) * port.Outward.Z))).Any() ||
                    (port.Field && (!fieldPaths.ContainsKey(port.Inside) || layout.FieldWall == null ||
                        port.InteriorRoute.Count == 0 || !port.InteriorRoute.First().Equals(layout.FieldWall.InnerGate.Value) ||
                        !port.InteriorRoute.Last().Equals(port.Inside) || port.InteriorRoute.Any(c => !fieldPaths.ContainsKey(c)) ||
                        port.InteriorRoute.Zip(port.InteriorRoute.Skip(1), (a, b) =>
                            Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z)).Any(d => d != 1) ||
                        layout.FieldWall.Walls.Contains(port.Cell) || layout.FieldWall.Fields.Contains(port.Cell))) ||
                    port.Doors.Distinct().Count() != port.Doors.Count || port.Doors.Any(d => priorDoorCells.Contains(d) ||
                        layout.Enclosure.Contains(d) || layout.Rooms.Any(r => r.Bounds.Contains(d)) ||
                        !Walkable(input.Terrain, d) || !input.Terrain[d].Has(TerrainTraits.Buildable)) ||
                    (!port.Field && (!layout.Enclosure.Contains(port.Inside) ||
                        port.Band.Any(d => !layout.PerimeterReinforcement.Contains(d)) ||
                        !Enumerable.Range(0, port.Doors.Count).Select(step => new GridPoint(
                            port.Cell.X + step * port.Outward.X, port.Cell.Z + step * port.Outward.Z))
                            .All(c => Walkable(input.Terrain, c) && (port.Doors.Contains(c) ||
                                !shell.Contains(c) && !layout.ExhaustPocketWalls.Contains(c))) ||
                        Enumerable.Range(0, port.Doors.Count).Any(layer =>
                        {
                            GridPoint center = new GridPoint(port.Cell.X + layer * port.Outward.X,
                                port.Cell.Z + layer * port.Outward.Z);
                            GridPoint tangent = new GridPoint(-port.Outward.Z, port.Outward.X);
                            return new[] { -2, -1, 1, 2 }.Any(offset => !shell.Contains(new GridPoint(
                                center.X + offset * tangent.X, center.Z + offset * tangent.Z)));
                        }))) ||
                    !plan.Posts.Any(p => p.AccessGate.Equals(port.Outer) && p.Outward.Equals(port.Outward)))
                    errors.Add("sally_port_public_connection");
                priorDoorCells.UnionWith(port.Doors);
            }
            if (plan.SallyPorts.SelectMany(p => p.Doors).Distinct().Count() != plan.SallyPorts.Sum(p => p.Doors.Count))
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
                bool shapeValid = post.Route.SequenceEqual(P4BCoverNest.RouteFor(
                    post.AccessGate, post.Outward, flank, post.Slots.Count)) &&
                    post.Route.Skip(3).SequenceEqual(post.Slots);
                if (!Directions.Contains(post.Outward) || flank == 0 || !AccessGates(layout).Contains(post.AccessGate) ||
                    !shapeValid || post.Route.Distinct().Count() != post.Route.Count ||
                    post.Route.Skip(1).Any(c => occupied.Contains(c) || !Walkable(input.Terrain, c)))
                    errors.Add("perimeter_post_access");
                LayoutGate arrivalGate = layout.Gates.FirstOrDefault(g => g.Outward.Equals(post.Outward) &&
                    g.BandDoors.Concat(new[] { g.Cell }).OrderByDescending(c => c.X * g.Outward.X + c.Z * g.Outward.Z)
                        .First().Equals(post.AccessGate));
                P4BSallyPort fieldArrivalPort = plan.SallyPorts.FirstOrDefault(p => p.Field &&
                    p.Outer.Equals(post.AccessGate) && p.Outward.Equals(post.Outward));
                bool fieldOuterGate = IsOuterFieldGateNest(layout, post);
                if (post.Route.Skip(1).Where(arrival.Contains).Any(c =>
                    arrivalGate != null ? !IsArrivalCrossingCell(c, post.AccessGate, post.Outward, flank,
                        new[] { arrivalGate }) : (!fieldOuterGate && fieldArrivalPort == null) ||
                        !IsFieldArrivalCrossingCell(c, post.AccessGate, post.Outward, arrival)) ||
                    post.Slots.Any(arrival.Contains))
                    errors.Add("perimeter_post_arrival_overlap");
                int wallWidth = post.Slots.Count;
                if (flank != 0 && Enumerable.Range(1, wallWidth).Any(l =>
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
            HashSet<GridPoint> compoundTransit = new HashSet<GridPoint>(plan.SallyPorts.Where(p => !p.Field)
                .SelectMany(p => p.InteriorRoute));
            if (plan.TransitCells.Any(c => !AccessGates(layout).Contains(c) &&
                ((occupied.Contains(c) && !fieldTransit.Contains(c) && !compoundTransit.Contains(c)) ||
                    !Walkable(input.Terrain, c))))
                errors.Add("perimeter_transit_occupancy");
            return errors;
        }

        internal static bool TurretsPreserveAttackSlots(ProceduralLayout layout,
            IEnumerable<GridPoint> turretFootprints)
        {
            P4BApproachDefensePlan plan = layout?.ApproachDefense;
            if (plan == null) return true;
            HashSet<GridPoint> occupied = new HashSet<GridPoint>(turretFootprints ?? Enumerable.Empty<GridPoint>());
            if (plan.Posts.Any(p => p.Slots.Concat(p.Route).Any(occupied.Contains))) return false;
            HashSet<GridPoint> opaque = Opaque(layout);
            HashSet<GridPoint> firing = new HashSet<GridPoint>();
            foreach (P4BPost post in plan.Posts)
            {
                int before = firing.Count;
                firing.UnionWith(FiringSlots(post, plan.Sides, opaque));
                if (firing.Count == before) return false;
            }
            foreach (P4BAttackSide side in plan.Sides)
            {
                if (side.Post.HasValue && side.Samples.Count(c =>
                    P4BFireLane.CanEngage(side.Post.Value, c, 8, 25, opaque)) < 4) return false;
                if (side.Positions.Any(p => !firing.Contains(p) ||
                    !side.Faces.Any(face => Math.Abs(face.X - p.X) + Math.Abs(face.Z - p.Z) <= 6) ||
                    side.Samples.Count(c => P4BFireLane.CanEngage(p, c, 8, 25, opaque)) < 4)) return false;
            }
            return true;
        }

        // An existing arrival route may share only the two outward gate-axis
        // cells with a defensive post. Slots, cover and returns still stay out
        // of all arrival routes.
        internal static bool IsArrivalCrossingCell(GridPoint cell, GridPoint gateCell, GridPoint outward, int flank,
            IEnumerable<LayoutGate> layoutGates)
        {
            GridPoint one = P4BCoverNest.Local(gateCell, outward, flank, 1, 0);
            GridPoint two = P4BCoverNest.Local(gateCell, outward, flank, 2, 0);
            if (!cell.Equals(one) && !cell.Equals(two)) return false;
            return layoutGates.Any(g => g.Outward.Equals(outward) &&
                g.BandDoors.Concat(new[] { g.Cell }).OrderByDescending(c => c.X * outward.X + c.Z * outward.Z)
                    .First().Equals(gateCell) && g.ExteriorRoute.Contains(cell));
        }

        internal static bool IsFieldArrivalCrossingCell(GridPoint cell, GridPoint gateCell, GridPoint outward,
            IEnumerable<GridPoint> arrivalRoute)
        {
            GridPoint one = new GridPoint(gateCell.X + outward.X, gateCell.Z + outward.Z);
            GridPoint two = new GridPoint(gateCell.X + 2 * outward.X, gateCell.Z + 2 * outward.Z);
            HashSet<GridPoint> arrival = new HashSet<GridPoint>(arrivalRoute ?? Enumerable.Empty<GridPoint>());
            return (cell.Equals(one) || cell.Equals(two)) && arrival.Contains(one) && arrival.Contains(cell);
        }

        private static bool ConnectedTransit(IEnumerable<GridPoint> transitCells)
        {
            HashSet<GridPoint> cells = new HashSet<GridPoint>(transitCells ?? Enumerable.Empty<GridPoint>());
            if (cells.Count == 0) return false;
            HashSet<GridPoint> reached = new HashSet<GridPoint>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            GridPoint start = cells.OrderBy(c => c).First();
            reached.Add(start); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                foreach (GridPoint direction in Directions)
                {
                    GridPoint next = new GridPoint(here.X + direction.X, here.Z + direction.Z);
                    if (cells.Contains(next) && reached.Add(next)) queue.Enqueue(next);
                }
            }
            return reached.Count == cells.Count;
        }

        private static bool SharedAccessPrefixCell(GridPoint cell, GateOption gate, int flank, IEnumerable<P4BPost> posts)
        {
            if (flank == 0) return false;
            return posts.Any(post => post.AccessGate.Equals(gate.Cell) && post.Outward.Equals(gate.Outward) &&
                Flank(post) == -flank && post.Route.Skip(1).Take(2).Contains(cell));
        }

        private static bool ConflictsWithPosts(P4BPost candidate, IEnumerable<P4BPost> existing)
        {
            List<P4BPost> prior = existing.ToList();
            HashSet<GridPoint> sharedPrefix = new HashSet<GridPoint>(prior.Where(post =>
                post.AccessGate.Equals(candidate.AccessGate) && post.Outward.Equals(candidate.Outward) &&
                Flank(post) == -Flank(candidate)).SelectMany(post => post.Route.Skip(1).Take(2)));
            HashSet<GridPoint> routes = new HashSet<GridPoint>(prior.SelectMany(post => post.Route.Skip(1)));
            HashSet<GridPoint> cover = new HashSet<GridPoint>(prior.SelectMany(post => post.CoverCells));
            HashSet<GridPoint> returns = new HashSet<GridPoint>(prior.Where(post => post.WallReturn.HasValue)
                .Select(post => post.WallReturn.Value));
            if (candidate.Route.Skip(1).Any(c => (routes.Contains(c) || cover.Contains(c) || returns.Contains(c)) &&
                !sharedPrefix.Contains(c))) return true;
            if (candidate.CoverCells.Any(c => routes.Contains(c) || cover.Contains(c) || returns.Contains(c))) return true;
            return candidate.WallReturn.HasValue && (routes.Contains(candidate.WallReturn.Value) ||
                cover.Contains(candidate.WallReturn.Value) || returns.Contains(candidate.WallReturn.Value));
        }

        private static int Flank(P4BPost post)
        {
            GridPoint tangent = new GridPoint(-post.Outward.Z, post.Outward.X);
            return Math.Sign((post.Cell.X - post.AccessGate.X) * tangent.X +
                (post.Cell.Z - post.AccessGate.Z) * tangent.Z);
        }

        private static HashSet<GridPoint> FloodEdgesNoCornerCut(TerrainSnapshot terrain, HashSet<GridPoint> blocked)
        {
            HashSet<GridPoint> reach = new HashSet<GridPoint>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint c in terrain.Points().Where(c => c.X == 0 || c.Z == 0 ||
                c.X == terrain.Width - 1 || c.Z == terrain.Height - 1))
                if (Walkable(terrain, c) && !blocked.Contains(c) && reach.Add(c)) queue.Enqueue(c);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    GridPoint next = new GridPoint(here.X + dx, here.Z + dz);
                    GridPoint sideX = new GridPoint(here.X + dx, here.Z);
                    GridPoint sideZ = new GridPoint(here.X, here.Z + dz);
                    if (dx != 0 && dz != 0 && ((!Walkable(terrain, sideX) || blocked.Contains(sideX)) ||
                        (!Walkable(terrain, sideZ) || blocked.Contains(sideZ)))) continue;
                    if (Walkable(terrain, next) && !blocked.Contains(next) && reach.Add(next)) queue.Enqueue(next);
                }
            }
            return reach;
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
        private static int GapReduction(HashSet<GridPoint> targets, HashSet<GridPoint> covered,
            HashSet<GridPoint> candidateCoverage)
        {
            int before = LongestUncoveredRun(targets.Where(c => !covered.Contains(c)));
            int after = LongestUncoveredRun(targets.Where(c => !covered.Contains(c) && !candidateCoverage.Contains(c)));
            return before - after;
        }

        internal static int LongestUncoveredRun(IEnumerable<GridPoint> targets)
        {
            List<GridPoint> cells = targets.Distinct().ToList();
            int longest = 0;
            foreach (GridPoint outward in Directions)
            {
                GridPoint tangent = new GridPoint(-outward.Z, outward.X);
                foreach (IGrouping<int, GridPoint> line in cells.GroupBy(c => c.X * outward.X + c.Z * outward.Z))
                {
                    List<int> positions = line.Select(c => c.X * tangent.X + c.Z * tangent.Z).Distinct().OrderBy(x => x).ToList();
                    int run = 0, last = int.MinValue;
                    foreach (int position in positions)
                    {
                        run = position == last + 1 ? run + 1 : 1;
                        longest = Math.Max(longest, run);
                        last = position;
                    }
                }
            }
            return longest;
        }

        private static List<List<GridPoint>> FaceRuns(List<GridPoint> faces, GridPoint tangent)
        {
            List<List<GridPoint>> runs = new List<List<GridPoint>>();
            List<GridPoint> current = null;
            int last = int.MinValue;
            foreach (GridPoint face in faces.OrderBy(c => c.X * tangent.X + c.Z * tangent.Z).ThenBy(c => c))
            {
                int position = face.X * tangent.X + face.Z * tangent.Z;
                if (current == null || position != last + 1)
                {
                    current = new List<GridPoint>();
                    runs.Add(current);
                }
                current.Add(face);
                last = position;
            }
            return runs;
        }

        private static int AttackSideTieOrder(int sideIndex)
        {
            switch (Names[sideIndex])
            {
                case "north": return 0;
                case "east": return 1;
                case "south": return 2;
                default: return 3;
            }
        }

        private static int[] AllocateFaceColumns(List<List<GridPoint>> runs, int budget)
        {
            if (runs.Count == 0) return new int[0];
            int[] columns = Enumerable.Repeat(1, runs.Count).ToArray();
            int remaining = Math.Max(0, Math.Max(budget, runs.Count) - runs.Count);
            int totalLength = runs.Sum(run => run.Count);
            double[] fractions = new double[runs.Count];
            int allocated = 0;
            for (int i = 0; i < runs.Count; i++)
            {
                double share = (double)remaining * runs[i].Count / totalLength;
                int whole = (int)Math.Floor(share);
                columns[i] += whole;
                fractions[i] = share - whole;
                allocated += whole;
            }
            foreach (int i in Enumerable.Range(0, runs.Count)
                .OrderByDescending(index => fractions[index]).ThenBy(index => index).Take(remaining - allocated))
                columns[i]++;
            return columns;
        }

        private static IEnumerable<GridPoint> SpreadFaceRun(List<GridPoint> cells, int count)
        {
            if (cells.Count <= count) return cells;
            if (count <= 1) return new[] { cells[(cells.Count - 1) / 2] };
            return Enumerable.Range(0, count).Select(i => cells[i * (cells.Count - 1) / (count - 1)]);
        }

        private static List<AttackCorner>[] ConvexAttackCorners(List<GridPoint>[] sideFaces,
            List<List<GridPoint>>[] faceRuns, HashSet<GridPoint> reachable)
        {
            List<AttackCorner>[] corners = Enumerable.Range(0, sideFaces.Length)
                .Select(_ => new List<AttackCorner>()).ToArray();
            for (int sideA = 0; sideA < sideFaces.Length; sideA++)
            {
                int sideB = (sideA + 1) % sideFaces.Length;
                if (faceRuns[sideA].Count == 0 || faceRuns[sideB].Count == 0) continue;
                GridPoint outwardA = Directions[sideA], outwardB = Directions[sideB];
                GridPoint tangentA = new GridPoint(-outwardA.Z, outwardA.X);
                GridPoint tangentB = new GridPoint(-outwardB.Z, outwardB.X);
                GridPoint faceA = faceRuns[sideA].SelectMany(run => new[] { run[0], run[run.Count - 1] })
                    .OrderByDescending(c => c.X * tangentA.X + c.Z * tangentA.Z).ThenBy(c => c).First();
                GridPoint faceB = faceRuns[sideB].SelectMany(run => new[] { run[0], run[run.Count - 1] })
                    .OrderBy(c => c.X * tangentB.X + c.Z * tangentB.Z).ThenBy(c => c).First();
                if (Math.Abs(faceA.X - faceB.X) > 2 || Math.Abs(faceA.Z - faceB.Z) > 2) continue;
                GridPoint cell = new GridPoint((faceA.X + faceB.X) / 2, (faceA.Z + faceB.Z) / 2);
                GridPoint diagonal = new GridPoint(outwardA.X + outwardB.X, outwardA.Z + outwardB.Z);
                GridPoint beyondCorner = new GridPoint(cell.X + diagonal.X, cell.Z + diagonal.Z);
                if (!reachable.Contains(beyondCorner)) continue;
                AttackCorner corner = new AttackCorner
                {
                    SideA = sideA, SideB = sideB, FaceA = faceA, FaceB = faceB, Cell = cell,
                    OutwardA = outwardA, OutwardB = outwardB
                };
                corners[sideA].Add(corner);
                corners[sideB].Add(corner);
            }
            return corners;
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
