using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// Layout engine v2 (docs/plans/2026-10-12-layout-engine-v2.md).
internal static partial class Program
{
    private static void RunLayoutEngineV2Tests()
    {
        Run("V2 neighbouring rooms may share plain walls only", V2SharedWallContract);
        Run("V2 shared walls are built and charged once", V2SharedWallLedger);
        Run("V2 clusters is the default engine; an injected engine plans and keys the digest", V2EngineInjection);
        Run("V2 clusters engine plans superstructure, town and mountain styles", V2ClusterStyles);
        Run("V2 candidate pool is bounded, deterministic and keeps the best score", V2CandidatePool);
    }

    private static void V2CandidatePool()
    {
        V2Case c = V2StyleCases().Single(x => x.Name == "superstructure-plain");
        ProceduralLayoutResult first = V2Plan(c);
        ProceduralLayoutResult again = V2Plan(c);
        Accepted(first);
        Equal(first.Layout.Digest, again.Layout.Digest, "same input replays the same chosen candidate");
        List<double> scores = first.Diagnostics.Where(d => d.Stage == "candidate" && d.Code == "valid")
            .Select(d => double.Parse(d.Detail.Split(' ').First(t => t.StartsWith("score=")).Substring(6),
                System.Globalization.CultureInfo.InvariantCulture)).ToList();
        True(scores.Count >= 1 && scores.Count <= ProceduralLayoutPlanner.CandidatePoolSize, "bounded pool " + scores.Count);
        LayoutDiagnostic accepted = first.Diagnostics.Single(d => d.Stage == "result" && d.Code == "accepted");
        True(accepted.Detail.Contains("candidates=" + scores.Count), "acceptance reports the pool: " + accepted.Detail);
        double chosen = double.Parse(accepted.Detail.Split(' ').First(t => t.StartsWith("preDefense=")).Substring(11),
            System.Globalization.CultureInfo.InvariantCulture);
        Equal(scores.Min().ToString("F1"), chosen.ToString("F1"), "lowest-scoring candidate finished first");
        Console.WriteLine("  candidate scores " + string.Join(",", scores.Select(x => x.ToString("F1"))) + " chosen=" + chosen.ToString("F1"));
    }

    private sealed class V2Case
    {
        public string Name;
        public TerrainSnapshot Terrain;
        public LayoutRequirements Requirements;
        public bool JointSite;
        public LayoutStyleKind Style;
        public ulong Seed = 1;
    }

    private static LayoutRequirements V2Funded(PhysicalSettlementProfile profile, SettlementTechStageLevel level, LayoutClimate climate, int wealth)
    {
        SettlementTechStage stage = TechStageModel.Create(level);
        return LayoutRequirements.FromProfile(profile, climate, stage: stage,
            populationBudget: OrdinarySettlementPopulation.Create(profile, stage, climate, wealth));
    }

    private static IEnumerable<V2Case> V2StyleCases() => new[]
    {
        new V2Case { Name = "superstructure-joint", Terrain = Plains(180), JointSite = true, Style = LayoutStyleKind.Superstructure,
            Requirements = V2Funded(P2ProfileCatalog.IndustrialTemperate, SettlementTechStageLevel.IndustrialLate, Temperate, 100) },
        new V2Case { Name = "superstructure-plain", Terrain = Plains(120), Style = LayoutStyleKind.Superstructure,
            Requirements = LayoutRequirements.FromPopulation(6, Industrial, Temperate) },
        new V2Case { Name = "town-joint", Terrain = Plains(180), JointSite = true, Style = LayoutStyleKind.Town,
            Requirements = V2Funded(P2ProfileCatalog.TribalTemperate, SettlementTechStageLevel.Neolithic, Temperate, 100) },
        new V2Case { Name = "mountain-tribal", Terrain = Mountain(130, 110), Style = LayoutStyleKind.Mountain, Seed = 4,
            Requirements = LayoutRequirements.FromPopulation(6, Tribal, Cold) },
    };

    private static ProceduralLayoutResult V2Plan(V2Case c, LayoutFortificationReservation fortification = null) =>
        ProceduralLayoutPlanner.Plan(new ProceduralLayoutInput(c.Requirements, c.Terrain, c.Seed, CoreCatalog,
            fortification ?? new LayoutFortificationReservation(1)), requireJointSite: c.JointSite);

    private static void ExportLayout(string name, ProceduralLayoutResult result, TerrainSnapshot terrain)
    {
        if (exportDir == null || !result.Success) return;
        System.IO.Directory.CreateDirectory(exportDir);
        string text = "# " + name + " (synthetic terrain; planner evidence only, not a live map)\n" + LayoutAsciiExport.Legend + "\n\n" +
            LayoutAsciiExport.Render(terrain, result.Layout) + "\nroof view:\n" + LayoutAsciiExport.RenderRoof(terrain, result.Layout) +
            "\nmetrics: " + V2Metrics(result) + "\n";
        text = string.Join("\n", text.Split('\n').Select(line => line.TrimEnd(' ')));
        System.IO.File.WriteAllText(System.IO.Path.Combine(exportDir, name + ".txt"), text);
    }

    internal static int SharedWallCells(ProceduralLayout layout) =>
        layout.Rooms.SelectMany(r => r.Bounds.Points()).GroupBy(c => c).Count(g => g.Count() > 1);

    private static string V2Metrics(ProceduralLayoutResult result)
    {
        if (!result.Success) return "rejected=" + result.RejectionCode;
        ProceduralLayout l = result.Layout;
        int roomCells = l.Rooms.SelectMany(r => r.Bounds.Points()).Distinct().Count();
        return "rooms=" + l.Rooms.Count + " enclosure=" + l.Enclosure.Count + " roomCells=" + roomCells +
            " sharedWallCells=" + SharedWallCells(l) + " compactness=" + (l.Enclosure.Count / (double)roomCells).ToString("F2") +
            " pockets=" + l.ExhaustPockets.Count + " gates=" + l.Gates.Count + " " + result.Stats;
    }

    private static void V2ClusterStyles()
    {
        foreach (V2Case c in V2StyleCases())
        {
            ProceduralLayoutResult v2 = V2Plan(c);
            Console.WriteLine("  " + c.Name + " clusters: " + V2Metrics(v2));
            ExportLayout("v2-clusters-" + c.Name, v2, c.Terrain);
            Accepted(v2);
            ProceduralLayoutInput input = new ProceduralLayoutInput(c.Requirements, c.Terrain, c.Seed, CoreCatalog,
                new LayoutFortificationReservation(1));
            Equal(0, ProceduralLayoutValidator.Validate(input, v2.Layout).Count, c.Name + " validator");
            True(v2.Layout.Notes.Any(n => n.Contains("style=" + c.Style)), c.Name + " style " + c.Style);
            True(SharedWallCells(v2.Layout) > 0, c.Name + " rooms share walls");
        }
    }

    private static LayoutRoom V2Room(string id, GridRect bounds, GridPoint door, GridPoint facing, params LayoutFixture[] fixtures) =>
        new LayoutRoom(new LayoutRoomDemand(id, LayoutRoomKind.Storage, 1, "stockpile-cells", new[] { new GridRect(0, 0, 3, 3) }),
            bounds, door, facing, 1, fixtures, false, new GridPoint[0]);

    private static void V2SharedWallContract()
    {
        // Two 3x3-interior rooms side by side on x=4, doors on the south wall.
        LayoutRoom west = V2Room("west", new GridRect(0, 0, 5, 5), new GridPoint(2, 0), new GridPoint(0, 1));
        LayoutRoom east = V2Room("east", new GridRect(4, 0, 5, 5), new GridPoint(6, 0), new GridPoint(0, 1));
        Equal(0, ProceduralLayoutValidator.SharedWallViolations(new[] { west, east }).Count, "common wall is legal");

        // A shared corner (rooms meeting diagonally) is a wall of both as well.
        LayoutRoom north = V2Room("north", new GridRect(4, 4, 5, 5), new GridPoint(6, 8), new GridPoint(0, -1));
        Equal(0, ProceduralLayoutValidator.SharedWallViolations(new[] { west, north }).Count, "shared corner is legal");

        LayoutRoom overlapping = V2Room("overlap", new GridRect(3, 0, 5, 5), new GridPoint(5, 0), new GridPoint(0, 1));
        Contains(ProceduralLayoutValidator.SharedWallViolations(new[] { west, overlapping }), "room_overlap_overlap");

        LayoutRoom doorOnShared = V2Room("door", new GridRect(0, 0, 5, 5), new GridPoint(4, 2), new GridPoint(-1, 0));
        Contains(ProceduralLayoutValidator.SharedWallViolations(new[] { doorOnShared, east }), "room_overlap_door");

        LayoutFixture cooler = new LayoutFixture(LayoutFixtureKind.WallCooler, "Cooler", new[] { new GridPoint(4, 2) }, null,
            new GridPoint(-1, 0), true, 200, false, "cooled", new GridPoint(5, 2));
        LayoutRoom cooled = V2Room("cooled", new GridRect(0, 0, 5, 5), new GridPoint(2, 0), new GridPoint(0, 1), cooler);
        Contains(ProceduralLayoutValidator.SharedWallViolations(new[] { cooled, east }), "room_overlap_cooled");
    }

    private static void V2SharedWallLedger()
    {
        ProceduralLayoutInput input = Input(Plains(60), LayoutRequirements.FromPopulation(4, Industrial, Temperate), 7);
        LayoutRoom west = V2Room("west", new GridRect(0, 0, 5, 5), new GridPoint(2, 0), new GridPoint(0, 1));
        LayoutRoom east = V2Room("east", new GridRect(4, 0, 5, 5), new GridPoint(6, 0), new GridPoint(0, 1));
        LayoutRoom apart = V2Room("east", new GridRect(10, 0, 5, 5), new GridPoint(12, 0), new GridPoint(0, 1));
        int shared = input.Equipment.Count(new[] { west, east }, new GridPoint[0], 0, null).WallCells;
        int separate = input.Equipment.Count(new[] { west, apart }, new GridPoint[0], 0, null).WallCells;
        // Each room has 16 edge cells less its door; the common wall (5 cells) is counted once.
        Equal(30, separate, "two separate rooms");
        Equal(25, shared, "shared wall charged once");
    }

    private sealed class StubEngine : ILayoutEngine
    {
        public int Calls;
        public string Id => "Stub";
        public ProceduralLayoutResult Plan(ProceduralLayoutInput input, bool requireJointSite)
        {
            Calls++;
            return ClustersLayoutEngine.Instance.Plan(input, requireJointSite);
        }
    }

    private static void V2EngineInjection()
    {
        TerrainSnapshot terrain = Plains(60);
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(4, Industrial, Temperate);
        ProceduralLayoutInput implicitDefault = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog);
        ProceduralLayoutInput clusters = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog, engine: ClustersLayoutEngine.Instance);
        StubEngine stub = new StubEngine();
        ProceduralLayoutInput injected = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog, engine: stub);
        Equal("Clusters", implicitDefault.Engine.Id, "layout engine v2 is the default");
        Equal(clusters.Digest, implicitDefault.Digest, "default input is the clusters input");
        True(injected.Digest != clusters.Digest, "another engine is a distinct input");
        ProceduralLayoutPlanner.Plan(injected);
        Equal(1, stub.Calls, "planning goes through the injected engine");
    }
}
