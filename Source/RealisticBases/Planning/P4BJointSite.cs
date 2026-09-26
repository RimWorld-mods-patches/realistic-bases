using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P4BFieldInterface
    {
        internal P4BFieldInterface(GridPoint innerGate, GridPoint outerGate,
            IEnumerable<GridPoint> crops, IEnumerable<GridPoint> lane, IEnumerable<GridPoint> posts)
        {
            InnerGate = innerGate;
            OuterGate = outerGate;
            Crops = Copy(crops);
            Lane = Copy(lane);
            PostClearance = Copy(posts);
        }
        private static IReadOnlyList<GridPoint> Copy(IEnumerable<GridPoint> cells) =>
            new ReadOnlyCollection<GridPoint>(cells.Distinct().OrderBy(c => c).ToList());
        public GridPoint InnerGate { get; }
        public GridPoint OuterGate { get; }
        public IReadOnlyList<GridPoint> Crops { get; }
        public IReadOnlyList<GridPoint> Lane { get; }
        public IReadOnlyList<GridPoint> PostClearance { get; }
    }

    // A bounded field-first preflight. The compound envelope is a packing
    // reservation, not a claim that every square in it will be built over.
    public sealed class P4BJointSite
    {
        internal P4BJointSite(GridRect field, GridRect compound, GridPoint anchor,
            IEnumerable<GridPoint> crops, IEnumerable<GridPoint> lane, IEnumerable<GridPoint> wall,
            GridPoint innerGate, GridPoint outerGate, IEnumerable<GridPoint> postClearance, int score,
            IEnumerable<P4BFieldInterface> interfaces = null)
        {
            Field = field;
            Compound = compound;
            Anchor = anchor;
            Crops = Copy(crops);
            Lane = Copy(lane);
            Wall = Copy(wall);
            InnerGate = innerGate;
            OuterGate = outerGate;
            PostClearance = Copy(postClearance);
            Interfaces = new ReadOnlyCollection<P4BFieldInterface>((interfaces ?? Enumerable.Empty<P4BFieldInterface>()).ToList());
            Score = score;
            ulong hash = StableHash.Add(StableHash.Start, "p4b-joint-site-1");
            foreach (int value in new[] { field.MinX, field.MinZ, field.Width, field.Height,
                compound.MinX, compound.MinZ, compound.Width, compound.Height, anchor.X, anchor.Z,
                innerGate.X, innerGate.Z, outerGate.X, outerGate.Z, score }) hash = StableHash.Add(hash, value);
            foreach (IReadOnlyList<GridPoint> list in new[] { Crops, Lane, Wall, PostClearance })
                foreach (GridPoint c in list) { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            foreach (P4BFieldInterface option in Interfaces)
            {
                hash = StableHash.Add(hash, option.InnerGate.X); hash = StableHash.Add(hash, option.InnerGate.Z);
                hash = StableHash.Add(hash, option.OuterGate.X); hash = StableHash.Add(hash, option.OuterGate.Z);
                foreach (GridPoint c in option.Crops.Concat(option.Lane).Concat(option.PostClearance))
                { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            }
            Digest = hash.ToString("X16");
        }

        private static IReadOnlyList<GridPoint> Copy(IEnumerable<GridPoint> cells) =>
            new ReadOnlyCollection<GridPoint>(cells.Distinct().OrderBy(c => c).ToList());

        public GridRect Field { get; }
        public GridRect Compound { get; }
        public GridPoint Anchor { get; }
        public IReadOnlyList<GridPoint> Crops { get; }
        public IReadOnlyList<GridPoint> Lane { get; }
        public IReadOnlyList<GridPoint> Wall { get; }
        public GridPoint InnerGate { get; }
        public GridPoint OuterGate { get; }
        public IReadOnlyList<GridPoint> PostClearance { get; }
        public IReadOnlyList<P4BFieldInterface> Interfaces { get; }
        public IEnumerable<GridPoint> AllPostClearance => Interfaces.SelectMany(i => i.PostClearance).Distinct();
        public int Score { get; }
        public string Digest { get; }
    }

    public sealed class P4BJointSiteResult
    {
        internal P4BJointSiteResult(IEnumerable<P4BJointSite> candidates, IEnumerable<string> rejections, int evaluated)
        {
            Candidates = new ReadOnlyCollection<P4BJointSite>(candidates.ToList());
            Rejections = new ReadOnlyCollection<string>(rejections.ToList());
            Evaluated = evaluated;
        }
        public IReadOnlyList<P4BJointSite> Candidates { get; }
        public IReadOnlyList<string> Rejections { get; }
        public int Evaluated { get; }
    }

    public static class P4BJointSitePlanner
    {
        // Search a small fixed family of square field footprints. Gates and
        // hauling lanes are counted as exclusions, never as crop capacity.
        public static P4BJointSiteResult Find(TerrainSnapshot terrain, int requiredCrops,
            int minimumCompoundCells, int margin, ulong seed, int maxCandidates = 16, int maxEvaluations = 4096)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (requiredCrops <= 0) throw new ArgumentOutOfRangeException(nameof(requiredCrops));
            if (minimumCompoundCells <= 0) throw new ArgumentOutOfRangeException(nameof(minimumCompoundCells));
            if (maxCandidates <= 0 || maxEvaluations <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
            LayoutTerrainGrid grid = LayoutTerrainGrid.Build(terrain, margin);
            int fieldSide = (int)Math.Ceiling(Math.Sqrt(requiredCrops)) + 5;
            int compoundSide = Math.Max(30, (int)Math.Ceiling(Math.Sqrt(minimumCompoundCells)) + 8);
            Dictionary<string, int> rejected = new Dictionary<string, int>(StringComparer.Ordinal);
            List<P4BJointSite> accepted = new List<P4BJointSite>();
            int evaluated = 0;
            // Seeded, well-spaced origins bound work on large maps without
            // privileging the original compound-only clearance maximum.
            List<GridPoint> origins = new List<GridPoint>();
            for (int z = margin + 2; z + fieldSide + margin + 2 < terrain.Height; z += 4)
                for (int x = margin + 2; x + fieldSide + margin + 2 < terrain.Width; x += 4)
                    origins.Add(new GridPoint(x, z));
            foreach (GridPoint origin in origins.OrderBy(p => LayoutSeed.Key(seed, p.X, p.Z)).ThenBy(p => p))
            {
                if (evaluated >= maxEvaluations) break;
                GridRect field = new GridRect(origin.X, origin.Z, fieldSide, fieldSide);
                foreach (int side in new[] { 0, 1, 2, 3 })
                {
                    if (evaluated++ >= maxEvaluations) break;
                    GridRect compound = CompoundAt(field, compoundSide, side);
                    if (!Fits(compound, terrain, margin)) { Reject("compound_bounds"); continue; }
                    List<GridPoint> wall = field.Points().Where(c => Edge(field, c)).ToList();
                    if (wall.Any(c => grid.At(c) != LayoutCellClass.Open || !grid.CanPlace(c)))
                    { Reject("field_wall_terrain"); continue; }
                    List<GridPoint> perimeterLane = field.Points().Where(c => !Edge(field, c) &&
                        (c.X == field.MinX + 1 || c.X == field.MaxX - 1 ||
                         c.Z == field.MinZ + 1 || c.Z == field.MaxZ - 1)).ToList();
                    if (perimeterLane.Any(c => !grid.Passable(c))) { Reject("field_lane"); continue; }
                    int available = compound.Points().Count(grid.CanPlace);
                    if (available < minimumCompoundCells ||
                        LargestConnected(compound.Points().Where(grid.CanPlace)).Count < minimumCompoundCells)
                    { Reject("compound_capacity"); continue; }
                    List<P4BFieldInterface> interfaces = new List<P4BFieldInterface>();
                    for (int gateOffset = 2; gateOffset <= fieldSide - 3; gateOffset++)
                    {
                        GridPoint innerGate = GateAt(field, side, true, gateOffset);
                        GridPoint outerGate = GateAt(field, side, false, gateOffset);
                        List<GridPoint> posts = PostCells(field, outerGate, side);
                        if (posts.Count != 2 || posts.Any(c => !grid.Passable(c) || grid.At(c) != LayoutCellClass.Open)) continue;
                        List<GridPoint> crossLane = field.Points().Where(c => !Edge(field, c) &&
                            (side == 0 || side == 2 ? c.X == innerGate.X : c.Z == innerGate.Z)).ToList();
                        List<GridPoint> lane = perimeterLane.Concat(crossLane).Distinct().ToList();
                        if (lane.Any(c => !grid.Passable(c))) continue;
                        HashSet<GridPoint> laneSet = new HashSet<GridPoint>(lane);
                        List<GridPoint> interior = field.Points().Where(c => !Edge(field, c) &&
                            c.X > field.MinX + 1 && c.X < field.MaxX - 1 &&
                            c.Z > field.MinZ + 1 && c.Z < field.MaxZ - 1 &&
                            !laneSet.Contains(c) && grid.At(c) == LayoutCellClass.Open &&
                            terrain[c].Has(TerrainTraits.Fertile)).ToList();
                        List<GridPoint> reachable = ReachableFromLane(interior, lane);
                        if (reachable.Count < requiredCrops) continue;
                        List<GridPoint> chosen = reachable.Take(requiredCrops).ToList();
                        interfaces.Add(new P4BFieldInterface(innerGate, outerGate, chosen, lane, posts));
                    }
                    if (interfaces.Count == 0) { Reject("crop_capacity_or_interface"); continue; }
                    P4BFieldInterface preferred = interfaces.OrderBy(i => Manhattan(i.InnerGate,
                        new GridPoint((field.MinX + field.MaxX) / 2, (field.MinZ + field.MaxZ) / 2))).First();
                    int farmlandLost = compound.Points().Count(c => terrain[c].Has(TerrainTraits.Fertile));
                    int score = requiredCrops * 4 - farmlandLost - (wall.Count * 2) + available / 8;
                    GridPoint center = new GridPoint((compound.MinX + compound.MaxX) / 2,
                        (compound.MinZ + compound.MaxZ) / 2);
                    GridPoint anchor = compound.Points().Where(grid.CanPlace)
                        .OrderBy(c => Manhattan(c, center)).ThenBy(c => c).First();
                    accepted.Add(new P4BJointSite(field, compound, anchor, preferred.Crops, preferred.Lane, wall,
                        preferred.InnerGate, preferred.OuterGate, preferred.PostClearance, score, interfaces));
                }
            }
            return new P4BJointSiteResult(accepted.OrderByDescending(c => c.Score)
                .ThenBy(c => LayoutSeed.Key(seed, c.Field.MinX, c.Field.MinZ, c.Compound.MinX + c.Compound.MinZ))
                .ThenBy(c => c.Field.MinX).ThenBy(c => c.Field.MinZ).Take(maxCandidates),
                rejected.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + ":" + kv.Value), evaluated);

            void Reject(string code) { int n; rejected.TryGetValue(code, out n); rejected[code] = n + 1; }
        }

        private static GridRect CompoundAt(GridRect field, int side, int direction)
        {
            int centerX = (field.MinX + field.MaxX) / 2;
            int centerZ = (field.MinZ + field.MaxZ) / 2;
            switch (direction)
            {
                case 0: return new GridRect(centerX - side / 2, field.MinZ - side - 2, side, side);
                case 1: return new GridRect(field.MaxX + 3, centerZ - side / 2, side, side);
                case 2: return new GridRect(centerX - side / 2, field.MaxZ + 3, side, side);
                default: return new GridRect(field.MinX - side - 2, centerZ - side / 2, side, side);
            }
        }

        private static bool Fits(GridRect rect, TerrainSnapshot terrain, int margin) =>
            rect.MinX >= margin && rect.MinZ >= margin && rect.MaxX < terrain.Width - margin && rect.MaxZ < terrain.Height - margin;

        private static bool Edge(GridRect rect, GridPoint c) =>
            c.X == rect.MinX || c.X == rect.MaxX || c.Z == rect.MinZ || c.Z == rect.MaxZ;

        private static GridPoint GateAt(GridRect field, int side, bool inner, int offset)
        {
            int cx = field.MinX + offset, cz = field.MinZ + offset;
            switch (side)
            {
                case 0: return new GridPoint(cx, inner ? field.MinZ : field.MaxZ);
                case 1: return new GridPoint(inner ? field.MaxX : field.MinX, cz);
                case 2: return new GridPoint(cx, inner ? field.MaxZ : field.MinZ);
                default: return new GridPoint(inner ? field.MinX : field.MaxX, cz);
            }
        }

        private static List<GridPoint> PostCells(GridRect field, GridPoint outerGate, int side)
        {
            GridPoint outward = side == 0 ? new GridPoint(0, 1) : side == 1 ? new GridPoint(-1, 0) :
                side == 2 ? new GridPoint(0, -1) : new GridPoint(1, 0);
            GridPoint tangent = new GridPoint(outward.Z, outward.X);
            return new[] { -3, 3 }.Select(k => new GridPoint(outerGate.X + outward.X * 2 + tangent.X * k,
                outerGate.Z + outward.Z * 2 + tangent.Z * k)).ToList();
        }

        private static int Manhattan(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

        private static List<GridPoint> LargestConnected(IEnumerable<GridPoint> cells)
        {
            HashSet<GridPoint> remaining = new HashSet<GridPoint>(cells);
            List<GridPoint> best = new List<GridPoint>();
            while (remaining.Count > 0)
            {
                GridPoint start = remaining.OrderBy(c => c).First();
                Queue<GridPoint> queue = new Queue<GridPoint>();
                List<GridPoint> component = new List<GridPoint>();
                queue.Enqueue(start);
                remaining.Remove(start);
                while (queue.Count > 0)
                {
                    GridPoint here = queue.Dequeue();
                    component.Add(here);
                    foreach (GridPoint d in ProceduralLayoutPlanner.Cardinal)
                    {
                        GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                        if (remaining.Remove(next)) queue.Enqueue(next);
                    }
                }
                if (component.Count > best.Count) best = component;
            }
            return best.OrderBy(c => c).ToList();
        }

        private static List<GridPoint> ReachableFromLane(IEnumerable<GridPoint> crops, IEnumerable<GridPoint> lane)
        {
            HashSet<GridPoint> remaining = new HashSet<GridPoint>(crops);
            HashSet<GridPoint> lanes = new HashSet<GridPoint>(lane);
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint c in remaining.OrderBy(c => c).Where(c =>
                ProceduralLayoutPlanner.Cardinal.Any(d => lanes.Contains(new GridPoint(c.X + d.X, c.Z + d.Z)))))
                queue.Enqueue(c);
            List<GridPoint> reachable = new List<GridPoint>();
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                if (!remaining.Remove(here)) continue;
                reachable.Add(here);
                foreach (GridPoint d in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (remaining.Contains(next)) queue.Enqueue(next);
                }
            }
            return reachable;
        }
    }
}
