using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P4BFieldInterface
    {
        internal P4BFieldInterface(GridPoint innerGate, GridPoint outerGate,
            IEnumerable<GridPoint> crops, IEnumerable<GridPoint> lane, IEnumerable<GridPoint> posts,
            IEnumerable<GridPoint> cover, IEnumerable<GridPoint> postAccess)
        {
            InnerGate = innerGate;
            OuterGate = outerGate;
            Crops = Copy(crops);
            Lane = Copy(lane);
            PostClearance = Copy(posts);
            CoverCells = Copy(cover);
            PostAccess = Copy(postAccess);
        }
        private static IReadOnlyList<GridPoint> Copy(IEnumerable<GridPoint> cells) =>
            new ReadOnlyCollection<GridPoint>(cells.Distinct().OrderBy(c => c).ToList());
        public GridPoint InnerGate { get; }
        public GridPoint OuterGate { get; }
        public IReadOnlyList<GridPoint> Crops { get; }
        public IReadOnlyList<GridPoint> Lane { get; }
        public IReadOnlyList<GridPoint> PostClearance { get; }
        public IReadOnlyList<GridPoint> CoverCells { get; }
        public IReadOnlyList<GridPoint> PostAccess { get; }
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
                foreach (GridPoint c in option.Crops.Concat(option.Lane).Concat(option.PostClearance)
                    .Concat(option.CoverCells).Concat(option.PostAccess))
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
        public IEnumerable<GridPoint> AllPostClearance => Interfaces.SelectMany(i => i.PostClearance.Concat(i.CoverCells)).Distinct();
        public int Score { get; }
        public string Digest { get; }
    }

    public sealed class P4BJointSiteResult
    {
        internal P4BJointSiteResult(IEnumerable<P4BJointSite> candidates, IEnumerable<string> rejections, int evaluated,
            int fieldOriginsScanned = 0)
        {
            Candidates = new ReadOnlyCollection<P4BJointSite>(candidates.ToList());
            Rejections = new ReadOnlyCollection<string>(rejections.ToList());
            Evaluated = evaluated;
            FieldOriginsScanned = fieldOriginsScanned;
        }
        public IReadOnlyList<P4BJointSite> Candidates { get; }
        public IReadOnlyList<string> Rejections { get; }
        public int Evaluated { get; }
        public int FieldOriginsScanned { get; }
    }

    public static class P4BJointSitePlanner
    {
        // Score per ring cell attackers cannot reach. A site backed by water on
        // one side of a ~60-cell envelope gains roughly 180.
        public const int ProtectionWeight = 3;

        // Search a small fixed family of square field footprints. Gates and
        // hauling lanes are counted as exclusions, never as crop capacity.
        // The compound side of a field interface: the planner reserves a public
        // street tip `AttachmentDepth` cells inside the field's inner gate and
        // a three-cell lane to it, which becomes the aligned compound gate.
        public static int AttachmentDepth(LayoutFortificationReservation fortification) =>
            2 * fortification.WallLayers + fortification.Setback + 4;

        // Layered gate bands need a straight frontage with returning arms.
        public static int AttachmentHalfTip(LayoutFortificationReservation fortification) =>
            fortification.WallLayers > 1 ? 4 : 1;

        // Static terrain part of that reservation; occupancy is checked when rooms are packed.
        internal static bool AttachmentClear(LayoutTerrainGrid grid, GridPoint innerGate, GridPoint outerGate, int depth, int halfTip)
        {
            GridPoint outward = new GridPoint(Math.Sign(outerGate.X - innerGate.X), Math.Sign(outerGate.Z - innerGate.Z));
            GridPoint tangent = new GridPoint(outward.Z, outward.X);
            GridPoint terminal = new GridPoint(innerGate.X - outward.X * depth, innerGate.Z - outward.Z * depth);
            for (int j = -halfTip; j <= halfTip; j++)
            {
                GridPoint c = new GridPoint(terminal.X + tangent.X * j, terminal.Z + tangent.Z * j);
                if (!grid.InBounds(c) || !grid.CanPlace(c) || grid.At(c) != LayoutCellClass.Open) return false;
            }
            for (int k = 1; k < depth; k++)
                for (int j = -1; j <= 1; j++)
                {
                    GridPoint c = new GridPoint(terminal.X + outward.X * k + tangent.X * j, terminal.Z + outward.Z * k + tangent.Z * j);
                    if (!grid.Terrain.InBounds(c) || LayoutTerrainGrid.Classify(grid.Terrain[c]) != LayoutCellClass.Open) return false;
                }
            return true;
        }

        public static P4BJointSiteResult Find(TerrainSnapshot terrain, int requiredCrops,
            int minimumCompoundCells, int margin, ulong seed, int maxCandidates = 16, int maxEvaluations = 4096,
            int residents = 4, bool refineFieldOrigins = false, int attachmentDepth = 0, int attachmentHalfTip = 1)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (requiredCrops <= 0) throw new ArgumentOutOfRangeException(nameof(requiredCrops));
            if (minimumCompoundCells <= 0) throw new ArgumentOutOfRangeException(nameof(minimumCompoundCells));
            if (residents <= 0) throw new ArgumentOutOfRangeException(nameof(residents));
            if (maxCandidates <= 0 || maxEvaluations <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
            LayoutTerrainGrid grid = LayoutTerrainGrid.Build(terrain, margin);
            bool[] edgeReach = LayoutExposure.EdgeReach(terrain);
            bool[] opaqueTargets = new bool[terrain.Width * terrain.Height];
            foreach (GridPoint cell in terrain.Points())
                opaqueTargets[cell.Z * terrain.Width + cell.X] = terrain[cell].HasAny(
                    TerrainTraits.NaturalRock | TerrainTraits.Occupied) &&
                    !terrain[cell].Has(TerrainTraits.ClearablePlant);
            return FindCore(terrain, requiredCrops, minimumCompoundCells, margin, seed, maxCandidates,
                maxEvaluations, residents, refineFieldOrigins, attachmentDepth, attachmentHalfTip,
                grid, edgeReach, opaqueTargets);
        }

        private static P4BJointSiteResult FindCore(TerrainSnapshot terrain, int requiredCrops,
            int minimumCompoundCells, int margin, ulong seed, int maxCandidates, int maxEvaluations,
            int residents, bool refineFieldOrigins, int attachmentDepth, int attachmentHalfTip,
            LayoutTerrainGrid grid, bool[] edgeReach, bool[] opaqueTargets)
        {
            int fieldSide = (int)Math.Ceiling(Math.Sqrt(requiredCrops)) + 5;
            // Room packing needs circulation, gate returns and utility pockets in
            // addition to the minimum room-area count. A 30-cell square passed
            // site preflight but repeatedly ran out of room on live 160x160 maps.
            // High-industrial demand needs more slack even on flat terrain:
            // sparse ruins and rock can otherwise consume every room repair.
            int compoundSide = Math.Max(38, (int)Math.Ceiling(Math.Sqrt(minimumCompoundCells)) +
                (minimumCompoundCells >= 500 ? 22 : 14));
            // Reserve additional frontage/circulation from the start, not just
            // additional bed area. Each extra resident
            // adds two cells to both reservation axes (the land is not all built).
            compoundSide += 2 * Math.Max(0, residents - 4);
            Dictionary<string, int> rejected = new Dictionary<string, int>(StringComparer.Ordinal);
            List<P4BJointSite> accepted = new List<P4BJointSite>();
            int evaluated = 0;
            int fieldOriginsScanned = 0;
            // Seeded, well-spaced origins bound work on large maps without
            // privileging the original compound-only clearance maximum.
            List<GridPoint> origins = new List<GridPoint>();
            // Larger population fields cannot rely on a four-cell sampling
            // lattice: a legal long fence can fit between scattered boulders at
            // an unsampled offset. Scan their field-only geometry once at cell
            // resolution, bounded by map area, before the expensive pair budget.
            // Refinement is attempted only after the coarse phase found no
            // feasible pair, preserving successful established seeded layouts.
            // Each phase has maxEvaluations pair work; at most two phases run.
            bool denseFields = refineFieldOrigins;
            int originStep = denseFields ? 1 : 4;
            for (int z = margin + 2; z + fieldSide + margin + 2 < terrain.Height; z += originStep)
                for (int x = margin + 2; x + fieldSide + margin + 2 < terrain.Width; x += originStep)
                {
                    GridRect possibleField = new GridRect(x, z, fieldSide, fieldSide);
                    fieldOriginsScanned++;
                    if (denseFields)
                    {
                        if (possibleField.Points().Where(c => Edge(possibleField, c))
                            .Any(c => grid.At(c) != LayoutCellClass.Open || !grid.CanPlace(c)))
                        { Reject("field_wall_terrain"); continue; }
                        if (possibleField.Points().Where(c => !Edge(possibleField, c) &&
                            (c.X == possibleField.MinX + 1 || c.X == possibleField.MaxX - 1 ||
                             c.Z == possibleField.MinZ + 1 || c.Z == possibleField.MaxZ - 1))
                            .Any(c => !grid.Passable(c)))
                        { Reject("field_lane"); continue; }
                    }
                    origins.Add(new GridPoint(x, z));
                }
            foreach (GridPoint origin in origins.OrderBy(p => LayoutSeed.Key(seed, p.X, p.Z)).ThenBy(p => p))
            {
                if (evaluated >= maxEvaluations) break;
                GridRect field = new GridRect(origin.X, origin.Z, fieldSide, fieldSide);
                List<GridPoint> fieldPoints = null;
                List<GridPoint> wall = null;
                List<GridPoint> perimeterLane = null;
                List<GridPoint> fertileOpenInterior = null;
                bool fieldDataReady = false, wallPassable = false, perimeterLanePassable = false;
                foreach (int side in new[] { 0, 1, 2, 3 })
                {
                    if (evaluated >= maxEvaluations) break;
                    evaluated++;
                    GridRect compound = CompoundAt(field, compoundSide, side);
                    if (!Fits(compound, terrain, margin)) { Reject("compound_bounds"); continue; }
                    if (!fieldDataReady)
                    {
                        fieldPoints = field.Points().ToList();
                        wall = fieldPoints.Where(c => Edge(field, c)).ToList();
                        perimeterLane = fieldPoints.Where(c => !Edge(field, c) &&
                            (c.X == field.MinX + 1 || c.X == field.MaxX - 1 ||
                             c.Z == field.MinZ + 1 || c.Z == field.MaxZ - 1)).ToList();
                        fertileOpenInterior = fieldPoints.Where(c => !Edge(field, c) &&
                            c.X > field.MinX + 1 && c.X < field.MaxX - 1 &&
                            c.Z > field.MinZ + 1 && c.Z < field.MaxZ - 1 &&
                            grid.At(c) == LayoutCellClass.Open && terrain[c].Has(TerrainTraits.Fertile)).ToList();
                        wallPassable = !wall.Any(c => grid.At(c) != LayoutCellClass.Open || !grid.CanPlace(c));
                        perimeterLanePassable = !perimeterLane.Any(c => !grid.Passable(c));
                        fieldDataReady = true;
                    }
                    if (!wallPassable)
                    { Reject("field_wall_terrain"); continue; }
                    if (!perimeterLanePassable) { Reject("field_lane"); continue; }
                    List<GridPoint> compoundCells = compound.Points().ToList();
                    List<GridPoint> compoundPlaceable = compoundCells.Where(grid.CanPlace).ToList();
                    int available = compoundPlaceable.Count;
                    if (available < minimumCompoundCells ||
                        LargestConnected(compoundPlaceable).Count < minimumCompoundCells)
                    { Reject("compound_capacity"); continue; }
                    List<P4BFieldInterface> interfaces = new List<P4BFieldInterface>();
                    bool attachmentBlocked = false;
                    for (int gateOffset = 2; gateOffset <= fieldSide - 3; gateOffset++)
                    {
                        GridPoint innerGate = GateAt(field, side, true, gateOffset);
                        GridPoint outerGate = GateAt(field, side, false, gateOffset);
                        // A field the compound cannot reach through open ground
                        // would fail every room attempt at its approach reservation.
                        if (attachmentDepth > 0 && !AttachmentClear(grid, innerGate, outerGate, attachmentDepth, attachmentHalfTip))
                        { attachmentBlocked = true; continue; }
                        List<GridPoint> posts = PostCells(field, outerGate, side);
                        GridPoint outward = side == 0 ? new GridPoint(0, 1) : side == 1 ? new GridPoint(-1, 0) :
                            side == 2 ? new GridPoint(0, -1) : new GridPoint(1, 0);
                        List<GridPoint> cover = posts.Select(p => new GridPoint(p.X + outward.X, p.Z + outward.Z)).ToList();
                        HashSet<GridPoint> coverSet = new HashSet<GridPoint>(cover);
                        GridPoint tangent = new GridPoint(outward.Z, outward.X);
                        if (posts.Count != 2 || posts.Concat(cover).Any(c => !grid.Passable(c) || grid.At(c) != LayoutCellClass.Open)) continue;
                        List<GridPoint> outsideTargets = Enumerable.Range(2, 20).Select(k => new GridPoint(
                                outerGate.X + outward.X * k, outerGate.Z + outward.Z * k))
                            .Where(terrain.InBounds).ToList();
                        bool outsideApproachReachable = outsideTargets.Any(target =>
                            edgeReach[target.Z * terrain.Width + target.X]);
                        // Match the later field-wall check: filter only a reachable
                        // exterior approach whose posts have no clear shot at any target.
                        // Isolated fertile regions keep their interface candidates.
                        if (outsideApproachReachable && posts.Any(post => !outsideTargets.Any(target =>
                            edgeReach[target.Z * terrain.Width + target.X] &&
                            SiteCanEngage(post, target)))) continue;
                        List<GridPoint> postAccess = new List<GridPoint>();
                        for (int forward = 1; forward <= 4; forward++)
                            for (int lateral = -6; lateral <= 6; lateral++)
                            {
                                GridPoint c = new GridPoint(outerGate.X + outward.X * forward + tangent.X * lateral,
                                    outerGate.Z + outward.Z * forward + tangent.Z * lateral);
                                if (grid.Passable(c) && !coverSet.Contains(c)) postAccess.Add(c);
                            }
                        HashSet<GridPoint> postAccessSet = new HashSet<GridPoint>(postAccess) { outerGate };
                        if (posts.Any(p => !ConnectedTo(outerGate, p, postAccessSet))) continue;
                        List<GridPoint> crossLane = fieldPoints.Where(c => !Edge(field, c) &&
                            (side == 0 || side == 2 ? c.X == innerGate.X : c.Z == innerGate.Z)).ToList();
                        List<GridPoint> lane = perimeterLane.Concat(crossLane).Distinct().ToList();
                        if (crossLane.Any(c => !grid.Passable(c))) continue;
                        HashSet<GridPoint> laneSet = new HashSet<GridPoint>(lane);
                        List<GridPoint> interior = fertileOpenInterior.Where(c => !laneSet.Contains(c)).ToList();
                        List<GridPoint> reachable = ReachableFromLane(interior, lane);
                        if (reachable.Count < requiredCrops) continue;
                        // Demand is a floor, not a planting cap: an established
                        // field uses every reachable fertile cell beyond its lane.
                        List<GridPoint> chosen = reachable;
                        interfaces.Add(new P4BFieldInterface(innerGate, outerGate, chosen, lane, posts, cover, postAccess));
                    }
                    if (interfaces.Count == 0) { Reject(attachmentBlocked ? "field_attachment_or_capacity" : "crop_capacity_or_interface"); continue; }
                    P4BFieldInterface preferred = interfaces.OrderBy(i => Manhattan(i.InnerGate,
                        new GridPoint((field.MinX + field.MaxX) / 2, (field.MinZ + field.MaxZ) / 2))).First();
                    int farmlandLost = compoundCells.Count(c => terrain[c].Has(TerrainTraits.Fertile));
                    // Natural barriers (deep water, cliffs) just outside the whole
                    // site remove approach directions; prefer them, all else equal.
                    int protectedRing = LayoutExposure.ProtectedRing(terrain, edgeReach,
                        LayoutExposure.Union(field, compound), margin + 2, out int ring);
                    int score = requiredCrops * 4 - farmlandLost - (wall.Count * 2) + available / 8 + ProtectionWeight * protectedRing;
                    GridPoint center = new GridPoint((compound.MinX + compound.MaxX) / 2,
                        (compound.MinZ + compound.MaxZ) / 2);
                    GridPoint anchor = compoundPlaceable
                        .OrderBy(c => Manhattan(c, center)).ThenBy(c => c).First();
                    accepted.Add(new P4BJointSite(field, compound, anchor, preferred.Crops, preferred.Lane, wall,
                        preferred.InnerGate, preferred.OuterGate, preferred.PostClearance, score, interfaces));
                }
            }
            if (accepted.Count == 0 && !refineFieldOrigins)
            {
                P4BJointSiteResult refined = FindCore(terrain, requiredCrops, minimumCompoundCells, margin, seed,
                    maxCandidates, maxEvaluations, residents, refineFieldOrigins: true,
                    attachmentDepth: attachmentDepth, attachmentHalfTip: attachmentHalfTip,
                    grid: grid, edgeReach: edgeReach, opaqueTargets: opaqueTargets);
                return new P4BJointSiteResult(refined.Candidates,
                    rejected.OrderBy(kv => kv.Key).Select(kv => "coarse_" + kv.Key + ":" + kv.Value)
                        .Concat(refined.Rejections.Select(r => "refined_" + r)),
                    evaluated + refined.Evaluated, fieldOriginsScanned + refined.FieldOriginsScanned);
            }
            return new P4BJointSiteResult(accepted.OrderByDescending(c => c.Score)
                .ThenBy(c => LayoutSeed.Key(seed, c.Field.MinX, c.Field.MinZ, c.Compound.MinX + c.Compound.MinZ))
                .ThenBy(c => c.Field.MinX).ThenBy(c => c.Field.MinZ).Take(maxCandidates),
                rejected.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + ":" + kv.Value), evaluated,
                fieldOriginsScanned);

            bool SiteCanEngage(GridPoint origin, GridPoint target)
            {
                return P4BFireLane.CanEngage(origin, target, 8, 38, opaqueTargets, terrain.Width, terrain.Height);
            }
            void Reject(string code) { int n; rejected.TryGetValue(code, out n); rejected[code] = n + 1; }
        }

        // A failed room pack does not prove that this field/compound pair is
        // unusable. Keep the chosen field and its interfaces fixed while the
        // compound grows outward into unused tile space. Growth never consumes
        // the field or moves its gates, and the new anchor is still inside the
        // same connected site rather than a new location search.
        internal static IEnumerable<P4BJointSite> GrowthVariants(P4BJointSite site,
            LayoutTerrainGrid grid, int margin, bool allowClippedGrowth = false, bool extendedGrowth = false)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            yield return site;
            int direction = site.Compound.MaxZ < site.Field.MinZ ? 0 :
                site.Compound.MinX > site.Field.MaxX ? 1 :
                site.Compound.MinZ > site.Field.MaxZ ? 2 : 3;
            GridRect previousGrowth = site.Compound;
            // Keep this field and connection fixed, but don't confuse the
            // former 24-cell growth cap with the usable land on the tile.
            // The map bounds and the global search budget still bound work.
            for (int extra = 12; extra <= (extendedGrowth ? Math.Max(grid.Terrain.Width, grid.Terrain.Height) : 24); extra += 12)
            {
                GridRect grown = CompoundAt(site.Field, site.Compound.Width + extra, direction);
                if (!Fits(grown, grid.Terrain, margin))
                {
                    if (!allowClippedGrowth) break;
                    // Reaching one map edge does not forbid extending the same
                    // settlement on its other free sides. Clip the proposed
                    // outward growth to legal bounds instead of abandoning it.
                    int minX = Math.Max(margin, grown.MinX), minZ = Math.Max(margin, grown.MinZ);
                    int maxX = Math.Min(grid.Terrain.Width - margin - 1, grown.MaxX);
                    int maxZ = Math.Min(grid.Terrain.Height - margin - 1, grown.MaxZ);
                    if (minX > site.Compound.MinX || minZ > site.Compound.MinZ ||
                        maxX < site.Compound.MaxX || maxZ < site.Compound.MaxZ) break;
                    grown = new GridRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
                    if (grown.Width == site.Compound.Width && grown.Height == site.Compound.Height) break;
                }
                if (grown.MinX == previousGrowth.MinX && grown.MinZ == previousGrowth.MinZ &&
                    grown.Width == previousGrowth.Width && grown.Height == previousGrowth.Height) break;
                previousGrowth = grown;
                GridPoint center = new GridPoint((grown.MinX + grown.MaxX) / 2,
                    (grown.MinZ + grown.MaxZ) / 2);
                // Do not "grow" across an impassable belt into a disconnected
                // patch that would really be a different settlement site.
                HashSet<GridPoint> connected = new HashSet<GridPoint> { site.Anchor };
                Queue<GridPoint> frontier = new Queue<GridPoint>();
                frontier.Enqueue(site.Anchor);
                while (frontier.Count > 0)
                {
                    GridPoint here = frontier.Dequeue();
                    foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                    {
                        GridPoint next = new GridPoint(here.X + step.X, here.Z + step.Z);
                        if (grown.Contains(next) && grid.CanPlace(next) && connected.Add(next))
                            frontier.Enqueue(next);
                    }
                }
                GridPoint? anchor = connected
                    .OrderBy(c => Math.Abs(c.X - center.X) + Math.Abs(c.Z - center.Z))
                    .Take(64)
                    .OrderByDescending(c => Math.Min(6, grid.OpenClearance[grid.Index(c)]))
                    .ThenBy(c => Math.Abs(c.X - center.X) + Math.Abs(c.Z - center.Z))
                    .ThenBy(c => c)
                    .Cast<GridPoint?>().FirstOrDefault();
                if (!anchor.HasValue) break;
                yield return new P4BJointSite(site.Field, grown, anchor.Value, site.Crops,
                    site.Lane, site.Wall, site.InnerGate, site.OuterGate,
                    site.PostClearance, site.Score, site.Interfaces);
            }
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

        private static bool ConnectedTo(GridPoint start, GridPoint target, HashSet<GridPoint> legal)
        {
            HashSet<GridPoint> seen = new HashSet<GridPoint> { start };
            Queue<GridPoint> queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                if (here.Equals(target)) return true;
                foreach (GridPoint d in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (legal.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return false;
        }

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
            HashSet<GridPoint> laneNeighbors = new HashSet<GridPoint>();
            foreach (GridPoint c in lanes)
            {
                laneNeighbors.Add(new GridPoint(c.X + 1, c.Z));
                laneNeighbors.Add(new GridPoint(c.X - 1, c.Z));
                laneNeighbors.Add(new GridPoint(c.X, c.Z + 1));
                laneNeighbors.Add(new GridPoint(c.X, c.Z - 1));
            }
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint c in remaining.OrderBy(c => c))
                if (laneNeighbors.Contains(c) && remaining.Remove(c))
                    queue.Enqueue(c);
            List<GridPoint> reachable = new List<GridPoint>();
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                reachable.Add(here);
                foreach (GridPoint d in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (remaining.Remove(next)) queue.Enqueue(next);
                }
            }
            return reachable;
        }
    }
}
