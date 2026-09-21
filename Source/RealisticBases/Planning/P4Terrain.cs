using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public static class P4Versions
    {
        public const int ArchetypeSchema = 1;
        public const string Rules = "p4.1";
    }

    public enum TerrainArchetype
    {
        Flat,
        Foothill,
        ExcavatedMountain,
        Lakeshore
    }

    public sealed class TerrainArchetypeInput
    {
        public TerrainArchetypeInput(PhysicalSettlementProfile profile, TerrainSnapshot terrain, ulong seed, int maxCandidates)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            if (maxCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
            Seed = seed;
            MaxCandidates = maxCandidates;
            Digest = ComputeDigest();
        }

        public PhysicalSettlementProfile Profile { get; }
        public TerrainSnapshot Terrain { get; }
        public ulong Seed { get; }
        public int MaxCandidates { get; }
        public int MinimumBuildableCells => PhysicalSettlementPlanner.CompoundWidth * PhysicalSettlementPlanner.CompoundHeight;
        public string Digest { get; }

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, P4Versions.Rules);
            hash = StableHash.Add(hash, P4Versions.ArchetypeSchema);
            hash = StableHash.Add(hash, Profile.Digest);
            hash = StableHash.Add(hash, Terrain.Digest);
            hash = StableHash.Add(hash, Seed);
            hash = StableHash.Add(hash, MaxCandidates);
            return StableHash.Hex(hash);
        }
    }

    public sealed class TerrainArchetypeCandidate
    {
        public TerrainArchetypeCandidate(TerrainArchetype archetype, int regionId, GridPoint anchor, int landCells,
            int buildableCells, int fertileCells, int naturalRockCells, int roofedRockCells, int waterBoundaryCells)
        {
            Archetype = archetype;
            RegionId = regionId;
            Anchor = anchor;
            LandCells = landCells;
            BuildableCells = buildableCells;
            FertileCells = fertileCells;
            NaturalRockCells = naturalRockCells;
            RoofedRockCells = roofedRockCells;
            WaterBoundaryCells = waterBoundaryCells;
        }

        public TerrainArchetype Archetype { get; }
        public int RegionId { get; }
        public GridPoint Anchor { get; }
        public int LandCells { get; }
        public int BuildableCells { get; }
        public int FertileCells { get; }
        public int NaturalRockCells { get; }
        public int RoofedRockCells { get; }
        public int WaterBoundaryCells { get; }
    }

    public sealed class TerrainArchetypeResult
    {
        private TerrainArchetypeResult(TerrainArchetypeInput input, TerrainArchetypeCandidate candidate,
            IEnumerable<string> rejections, int considered, bool limitReached)
        {
            InputDigest = input.Digest;
            Candidate = candidate;
            Rejections = new ReadOnlyCollection<string>((rejections ?? Enumerable.Empty<string>()).ToList());
            CandidatesConsidered = considered;
            LimitReached = limitReached;
        }

        public string InputDigest { get; }
        public TerrainArchetypeCandidate Candidate { get; }
        public IReadOnlyList<string> Rejections { get; }
        public int CandidatesConsidered { get; }
        public bool LimitReached { get; }
        public bool Success => Candidate != null;

        internal static TerrainArchetypeResult Accepted(TerrainArchetypeInput input, TerrainArchetypeCandidate candidate,
            IEnumerable<string> rejections, int considered) => new TerrainArchetypeResult(input, candidate, rejections, considered, false);
        internal static TerrainArchetypeResult Rejected(TerrainArchetypeInput input, IEnumerable<string> rejections,
            int considered, bool limitReached) => new TerrainArchetypeResult(input, null, rejections, considered, limitReached);
    }

    public static class TerrainArchetypePlanner
    {
        private static readonly GridPoint[] Neighbors =
        {
            new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1)
        };

        public static TerrainArchetypeResult Plan(TerrainArchetypeInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            TerrainSnapshot terrain = input.Terrain;
            IReadOnlyList<ConnectedRegion> regions = ConnectedRegionAnalyzer.Analyze(terrain, IsTraversableLand);
            List<ConnectedRegion> ordered = regions.OrderByDescending(r => r.TouchesEdge).ThenByDescending(r => r.Area)
                .ThenBy(r => StableTie(input.Seed, r)).ThenBy(r => r.Id).ToList();
            List<string> rejections = new List<string>();
            int considered = 0;
            foreach (ConnectedRegion region in ordered)
            {
                if (considered >= input.MaxCandidates) break;
                considered++;
                if (!region.TouchesEdge)
                {
                    Add(rejections, "region_" + region.Id + ":island_requires_verified_approach");
                    continue;
                }
                TerrainArchetypeCandidate candidate;
                string rejection;
                if (TryClassify(input, region, out candidate, out rejection))
                    return TerrainArchetypeResult.Accepted(input, candidate, rejections, considered);
                Add(rejections, "region_" + region.Id + ":" + rejection);
            }
            if (rejections.Count == 0) rejections.Add("no_traversable_land_region");
            return TerrainArchetypeResult.Rejected(input, rejections, considered, ordered.Count > considered && considered >= input.MaxCandidates);
        }

        private static bool TryClassify(TerrainArchetypeInput input, ConnectedRegion region,
            out TerrainArchetypeCandidate candidate, out string rejection)
        {
            TerrainSnapshot terrain = input.Terrain;
            int buildable = region.Cells.Count(c => terrain[c].Has(TerrainTraits.Standable | TerrainTraits.Buildable) &&
                !terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.Occupied));
            int fertile = region.Cells.Count(c => terrain[c].Has(TerrainTraits.Fertile));
            HashSet<GridPoint> regionCells = new HashSet<GridPoint>(region.Cells);
            int naturalRock = terrain.Points().Count(c => terrain[c].Has(TerrainTraits.NaturalRock) && AdjacentToRegion(c, regionCells));
            int roofedRock = terrain.Points().Count(c => terrain[c].Has(TerrainTraits.NaturalRock | TerrainTraits.Roofed) && AdjacentToRegion(c, regionCells));
            int waterBoundary = region.Cells.Count(c => Neighbors.Any(n => terrain.InBounds(new GridPoint(c.X + n.X, c.Z + n.Z)) &&
                terrain[new GridPoint(c.X + n.X, c.Z + n.Z)].Has(TerrainTraits.Water)));
            if (buildable < input.MinimumBuildableCells) { candidate = null; rejection = "insufficient_buildable_land"; return false; }
            if (fertile < input.Profile.RequiredCropCells) { candidate = null; rejection = "insufficient_fertile_land"; return false; }
            TerrainArchetype archetype = roofedRock >= 36 ? TerrainArchetype.ExcavatedMountain :
                naturalRock >= 20 ? TerrainArchetype.Foothill : waterBoundary >= 20 ? TerrainArchetype.Lakeshore : TerrainArchetype.Flat;
            candidate = new TerrainArchetypeCandidate(archetype, region.Id, region.Cells[0], region.Area, buildable, fertile,
                naturalRock, roofedRock, waterBoundary);
            rejection = null;
            return true;
        }

        private static bool AdjacentToRegion(GridPoint point, HashSet<GridPoint> regionCells) =>
            regionCells.Contains(point) || Neighbors.Any(n => regionCells.Contains(new GridPoint(point.X + n.X, point.Z + n.Z)));

        private static bool IsTraversableLand(TerrainCell cell) => cell.Has(TerrainTraits.Standable) &&
            !cell.HasAny(TerrainTraits.Water | TerrainTraits.Occupied);

        private static int StableTie(ulong seed, ConnectedRegion region)
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, seed);
            hash = StableHash.Add(hash, region.Id);
            hash = StableHash.Add(hash, region.MinX);
            hash = StableHash.Add(hash, region.MinZ);
            return unchecked((int)hash);
        }

        private static void Add(List<string> rejections, string rejection)
        {
            if (rejections.Count < 20) rejections.Add(rejection);
        }
    }
}
