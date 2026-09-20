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
        Console.WriteLine("P1.1 pure tests: " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures);
        return failures == 0 ? 0 : 1;
    }

    private static void StableSeedAndInputDigest()
    {
        SettlementProfile profile = new SettlementProfile("industrial-diagnostic", 1, 9, 9, 25, 128);
        TerrainSnapshot snapshot = Filled(8, 8, TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.SupportsHeavy);
        ulong first = SeedDeriver.Derive("P1-world", 420, profile);
        ulong second = SeedDeriver.Derive("P1-world", 420, profile);
        Equal(first, second, "seed");
        Equal(new PlannerInput(profile, snapshot, first).Digest,
            new PlannerInput(profile, snapshot, second).Digest, "input digest");
        NotEqual(first, SeedDeriver.Derive("P1-world", 421, profile), "tile participates in seed");
    }

    private static void SnapshotCopiesSourceCells()
    {
        TerrainCell buildable = new TerrainCell(TerrainTraits.Standable | TerrainTraits.Buildable);
        TerrainCell[] source = Enumerable.Repeat(buildable, 9).ToArray();
        TerrainSnapshot snapshot = new TerrainSnapshot(3, 3, source, "synthetic-v1");
        string digest = snapshot.Digest;
        source[4] = new TerrainCell(TerrainTraits.Water);
        Equal(digest, snapshot.Digest, "digest is immutable");
        True(snapshot[1, 1].Has(TerrainTraits.Buildable), "copied cell remains buildable");
    }

    private static void ConnectedRegionsAreDeterministic()
    {
        TerrainSnapshot snapshot = Filled(5, 4, TerrainTraits.Standable | TerrainTraits.Buildable);
        IReadOnlyList<ConnectedRegion> first = ConnectedRegionAnalyzer.Analyze(snapshot, IsBuildable);
        IReadOnlyList<ConnectedRegion> second = ConnectedRegionAnalyzer.Analyze(snapshot, IsBuildable);
        Equal(1, first.Count, "one region");
        Equal(20, first[0].Area, "region area");
        Equal(string.Join(";", first[0].Cells), string.Join(";", second[0].Cells), "cell order");
    }

    private static void DisconnectedRegionsRetainEdgeMetadata()
    {
        TerrainCell blocked = new TerrainCell(TerrainTraits.Water);
        TerrainCell open = new TerrainCell(TerrainTraits.Standable | TerrainTraits.Buildable);
        TerrainCell[] cells = Enumerable.Repeat(blocked, 35).ToArray();
        cells[0] = open;
        cells[1] = open;
        cells[3 * 7 + 3] = open;
        cells[3 * 7 + 4] = open;
        TerrainSnapshot snapshot = new TerrainSnapshot(7, 5, cells, "islands-v1");
        IReadOnlyList<ConnectedRegion> regions = ConnectedRegionAnalyzer.Analyze(snapshot, IsBuildable);
        Equal(2, regions.Count, "region count");
        True(regions[0].TouchesEdge, "first region touches edge");
        True(!regions[1].TouchesEdge, "second region is enclosed");
    }

    private static TerrainSnapshot Filled(int width, int height, TerrainTraits traits)
    {
        return new TerrainSnapshot(width, height,
            Enumerable.Repeat(new TerrainCell(traits), width * height), "synthetic-v1");
    }

    private static bool IsBuildable(TerrainCell cell) => cell.Has(TerrainTraits.Standable | TerrainTraits.Buildable);

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("FAIL " + name + ": " + ex.Message);
        }
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
        if (EqualityComparer<T>.Default.Equals(first, second))
            throw new InvalidOperationException(message + " values unexpectedly equal");
    }
}
