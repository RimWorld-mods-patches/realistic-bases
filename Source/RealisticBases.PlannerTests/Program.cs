using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Run("stable seed and input digest", StableSeedAndInputDigest);
        Run("snapshot copies source cells", SnapshotCopiesSourceCells);
        Run("connected regions are deterministic", ConnectedRegionsAreDeterministic);
        Run("disconnected regions retain edge metadata", DisconnectedRegionsRetainEdgeMetadata);
        Run("fixed inputs produce identical plan digests", FixedInputsProduceIdenticalPlanDigests);
        Run("disconnected land stays within one region", DisconnectedLandStaysWithinOneRegion);
        Run("narrow corridors are rejected", NarrowCorridorsAreRejected);
        Run("obstructed interaction cells are rejected", ObstructedInteractionCellsAreRejected);
        Run("impossible capacity is rejected before search", ImpossibleCapacityIsRejectedBeforeSearch);
        Run("invalid water terrain is rejected", InvalidWaterTerrainIsRejected);
        Run("candidate search terminates at deterministic limit", CandidateSearchTerminatesAtLimit);
        Run("planning preserves snapshot digest", PlanningPreservesSnapshotDigest);
        Console.WriteLine("P1.2 pure tests: " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures);
        return failures == 0 ? 0 : 1;
    }

    private static void StableSeedAndInputDigest()
    {
        SettlementProfile profile = Profile();
        TerrainSnapshot snapshot = Open(12, 12);
        ulong first = SeedDeriver.Derive("P1-world", 420, profile);
        ulong second = SeedDeriver.Derive("P1-world", 420, profile);
        Equal(first, second, "seed");
        Equal(new PlannerInput(profile, snapshot, first).Digest,
            new PlannerInput(profile, snapshot, second).Digest, "input digest");
        NotEqual(first, SeedDeriver.Derive("P1-world", 421, profile), "tile participates in seed");
    }

    private static void SnapshotCopiesSourceCells()
    {
        TerrainCell buildable = Cell(TerrainTraits.Standable | TerrainTraits.Buildable);
        TerrainCell[] source = Enumerable.Repeat(buildable, 9).ToArray();
        TerrainSnapshot snapshot = new TerrainSnapshot(3, 3, source, "synthetic-v1");
        string digest = snapshot.Digest;
        source[4] = Cell(TerrainTraits.Water);
        Equal(digest, snapshot.Digest, "digest is immutable");
        True(snapshot[1, 1].Has(TerrainTraits.Buildable), "copied cell remains buildable");
    }

    private static void ConnectedRegionsAreDeterministic()
    {
        TerrainSnapshot snapshot = Open(5, 4);
        IReadOnlyList<ConnectedRegion> first = ConnectedRegionAnalyzer.Analyze(snapshot, IsBuildable);
        IReadOnlyList<ConnectedRegion> second = ConnectedRegionAnalyzer.Analyze(snapshot, IsBuildable);
        Equal(1, first.Count, "one region");
        Equal(20, first[0].Area, "region area");
        Equal(string.Join(";", first[0].Cells), string.Join(";", second[0].Cells), "cell order");
    }

    private static void DisconnectedRegionsRetainEdgeMetadata()
    {
        TerrainCell[] cells = Enumerable.Repeat(Cell(TerrainTraits.Water), 35).ToArray();
        cells[0] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.MapEdge);
        cells[1] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.MapEdge);
        cells[3 * 7 + 3] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable);
        cells[3 * 7 + 4] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable);
        TerrainSnapshot snapshot = new TerrainSnapshot(7, 5, cells, "islands-v1");
        IReadOnlyList<ConnectedRegion> regions = ConnectedRegionAnalyzer.Analyze(snapshot, IsBuildable);
        Equal(2, regions.Count, "region count");
        True(regions[0].TouchesEdge, "first region touches edge");
        True(!regions[1].TouchesEdge, "second region is enclosed");
    }

    private static void FixedInputsProduceIdenticalPlanDigests()
    {
        PlannerInput input = Input(Open(20, 20), Profile(), 17);
        PlanningResult first = SettlementPlanner.Plan(input);
        PlanningResult second = SettlementPlanner.Plan(input);
        True(first.Success && second.Success, "both plans succeed");
        Equal(first.Plan.Digest, second.Plan.Digest, "plan digest");
        Equal(first.Plan.CandidatesConsidered, second.Plan.CandidatesConsidered, "candidate count");
        True(SettlementPlanValidator.Validate(input, first.Plan).IsValid, "plan validates");
    }

    private static void DisconnectedLandStaysWithinOneRegion()
    {
        TerrainCell[] cells = Enumerable.Repeat(Cell(TerrainTraits.Water), 28 * 18).ToArray();
        FillRect(cells, 28, 0, 0, 12, 18, true);
        FillRect(cells, 28, 16, 0, 12, 18, true);
        TerrainSnapshot snapshot = new TerrainSnapshot(28, 18, cells, "disconnected-v1");
        PlanningResult result = SettlementPlanner.Plan(Input(snapshot, Profile(), 21));
        True(result.Success, "one island accepts the compound");
        bool west = result.Plan.Compound.MaxX < 12;
        bool east = result.Plan.Compound.MinX >= 16;
        True(west || east, "compound does not cross water split");
        True(result.Plan.Route.All(p => !snapshot[p].HasAny(TerrainTraits.Water)), "route remains on land");
    }

    private static void NarrowCorridorsAreRejected()
    {
        TerrainCell[] cells = Enumerable.Repeat(Cell(TerrainTraits.Water), 20 * 20).ToArray();
        FillRect(cells, 20, 0, 0, 3, 20, true);
        TerrainSnapshot snapshot = new TerrainSnapshot(20, 20, cells, "corridor-v1");
        PlanningResult result = SettlementPlanner.Plan(Input(snapshot, Profile(), 31));
        True(!result.Success, "narrow strip rejects 7x7 compound");
    }

    private static void ObstructedInteractionCellsAreRejected()
    {
        TerrainCell[] cells = OpenCells(9, 9);
        cells[3 * 9 + 4] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.Occupied);
        TerrainSnapshot snapshot = new TerrainSnapshot(9, 9, cells, "interaction-block-v1");
        PlanningResult result = SettlementPlanner.Plan(Input(snapshot, Profile(64), 41));
        True(!result.Success, "only candidate rejects obstruction");
        True(result.Rejections.Any(r => r.Contains("interaction_obstructed")), "specific rejection is reported");
    }

    private static void ImpossibleCapacityIsRejectedBeforeSearch()
    {
        SettlementProfile impossible = new SettlementProfile("diagnostic", 1, 7, 7, 21, 64, 512);
        PlanningResult result = SettlementPlanner.Plan(Input(Open(20, 20), impossible, 51));
        True(!result.Success, "capacity rejects");
        Equal(0, result.CandidatesConsidered, "no candidates evaluated");
        True(result.Rejections[0].StartsWith("impossible_capacity"), "capacity reason");
    }

    private static void InvalidWaterTerrainIsRejected()
    {
        TerrainSnapshot snapshot = new TerrainSnapshot(20, 20,
            Enumerable.Repeat(Cell(TerrainTraits.Water), 400), "water-v1");
        PlanningResult result = SettlementPlanner.Plan(Input(snapshot, Profile(), 61));
        True(!result.Success, "water map rejects");
    }

    private static void CandidateSearchTerminatesAtLimit()
    {
        TerrainCell[] cells = OpenCells(30, 30);
        for (int i = 0; i < cells.Length; i++)
            cells[i] = Cell(cells[i].Traits | TerrainTraits.Roofed);
        TerrainSnapshot snapshot = new TerrainSnapshot(30, 30, cells, "roofed-v1");
        SettlementProfile profile = new SettlementProfile("bounded", 1, 7, 7, 20, 3, 128);
        PlanningResult first = SettlementPlanner.Plan(Input(snapshot, profile, 71));
        PlanningResult second = SettlementPlanner.Plan(Input(snapshot, profile, 71));
        True(!first.Success && first.LimitReached, "limit is reported");
        Equal(3, first.CandidatesConsidered, "exact candidate bound");
        Equal(string.Join("|", first.Rejections), string.Join("|", second.Rejections), "deterministic rejection order");
    }

    private static void PlanningPreservesSnapshotDigest()
    {
        TerrainSnapshot snapshot = Open(20, 20);
        string before = snapshot.Digest;
        SettlementPlanner.Plan(Input(snapshot, Profile(), 81));
        Equal(before, snapshot.Digest, "snapshot digest after planning");
    }

    private static SettlementProfile Profile(int candidates = 128) =>
        new SettlementProfile("diagnostic", 1, 7, 7, 20, candidates, 512);

    private static PlannerInput Input(TerrainSnapshot snapshot, SettlementProfile profile, ulong seed) =>
        new PlannerInput(profile, snapshot, seed);

    private static TerrainSnapshot Open(int width, int height) =>
        new TerrainSnapshot(width, height, OpenCells(width, height), "synthetic-v1");

    private static TerrainCell[] OpenCells(int width, int height)
    {
        TerrainCell[] cells = new TerrainCell[width * height];
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                TerrainTraits traits = TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.SupportsHeavy;
                if (x == 0 || z == 0 || x == width - 1 || z == height - 1) traits |= TerrainTraits.MapEdge;
                cells[z * width + x] = Cell(traits);
            }
        return cells;
    }

    private static void FillRect(TerrainCell[] cells, int mapWidth, int minX, int minZ, int width, int height, bool edgeAware)
    {
        int mapHeight = cells.Length / mapWidth;
        for (int z = minZ; z < minZ + height; z++)
            for (int x = minX; x < minX + width; x++)
            {
                TerrainTraits traits = TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.SupportsHeavy;
                if (edgeAware && (x == 0 || z == 0 || x == mapWidth - 1 || z == mapHeight - 1)) traits |= TerrainTraits.MapEdge;
                cells[z * mapWidth + x] = Cell(traits);
            }
    }

    private static TerrainCell Cell(TerrainTraits traits) => new TerrainCell(traits, 1, 0);
    private static bool IsBuildable(TerrainCell cell) => cell.Has(TerrainTraits.Standable | TerrainTraits.Buildable);

    private static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(message + " expected=" + expected + " actual=" + actual);
    }

    private static void NotEqual<T>(T first, T second, string message)
    {
        if (EqualityComparer<T>.Default.Equals(first, second)) throw new InvalidOperationException(message);
    }
}
