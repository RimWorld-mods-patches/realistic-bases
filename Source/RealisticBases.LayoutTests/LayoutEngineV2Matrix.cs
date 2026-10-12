using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealisticBases.Planning;

// Layout engine v2 comparison matrix (plan checkpoint V2.4): legacy and
// clusters engines on the same terrains, requirements and seeds. Selected
// explicitly (--only "V2 matrix"); it is evidence, not a default gate.
internal static partial class Program
{
    private sealed class V2MatrixCase
    {
        public string Name;
        public bool JointSite;
        public Func<int, LayoutEngine, ProceduralLayoutInput> Input;
    }

    private static void RunLayoutEngineV2Matrix()
    {
        if (only == null || only.IndexOf("matrix", StringComparison.OrdinalIgnoreCase) < 0) return;
        Run("V2 matrix legacy versus clusters", V2Matrix);
    }

    private static TerrainSnapshot V2Clutter(int salt) => Build(160, 160, (x, z) =>
    {
        uint hash = (uint)((x / 5) * 73856093 ^ (z / 5) * 19349663 ^ salt * 83492791);
        hash ^= hash >> 13;
        hash *= 1274126177u;
        if (hash % 1000 < 65) return RockRoofed;
        uint edifice = (uint)(x * 19349663 ^ z * 83492791 ^ salt * 73856093);
        edifice ^= edifice >> 13;
        return edifice % 1000 < 10 ? TerrainTraits.Occupied : Ground;
    });

    private static TerrainSnapshot V2LiveDensity(int salt) => Build(160, 160, (x, z) =>
    {
        uint cluster = (uint)((x / 3) * 73856093 ^ (z / 3) * 19349663 ^ salt * 83492791);
        cluster ^= cluster >> 13;
        cluster *= 1274126177u;
        return cluster % 1000 < 70 ? RockRoofed : Ground;
    });

    private static TerrainSnapshot V2Rle(string file, string id)
    {
        List<TerrainCell> cells = new List<TerrainCell>();
        foreach (string row in System.IO.File.ReadAllLines(System.IO.Path.Combine(AppContext.BaseDirectory, file)))
            foreach (string token in row.Split(','))
            {
                string[] parts = token.Split('*');
                TerrainTraits traits = (TerrainTraits)Convert.ToUInt16(parts[0], 16);
                if (file == "flat-rejected-traits.rle" && traits == (TerrainTraits.Buildable | TerrainTraits.Fertile | TerrainTraits.SupportsHeavy))
                    traits |= TerrainTraits.ClearablePlant;
                for (int i = 0; i < int.Parse(parts[1]); i++) cells.Add(new TerrainCell(traits));
            }
        return new TerrainSnapshot(160, 160, cells, id);
    }

    private static IEnumerable<V2MatrixCase> V2MatrixCases()
    {
        foreach (int wealth in new[] { 60, 100, 180 })
        {
            int w = wealth;
            yield return new V2MatrixCase { Name = "industrial-joint-w" + w, JointSite = true, Input = (seed, engine) =>
                new ProceduralLayoutInput(V2Funded(P2ProfileCatalog.IndustrialTemperate, SettlementTechStageLevel.IndustrialLate, Temperate, w),
                    Plains(180), (ulong)seed, CoreCatalog, new LayoutFortificationReservation(1), engine: engine) };
            yield return new V2MatrixCase { Name = "tribal-joint-w" + w, JointSite = true, Input = (seed, engine) =>
                new ProceduralLayoutInput(V2Funded(P2ProfileCatalog.TribalTemperate, SettlementTechStageLevel.Neolithic, Temperate, w),
                    Plains(180), (ulong)seed, CoreCatalog, new LayoutFortificationReservation(1), engine: engine) };
        }
        yield return new V2MatrixCase { Name = "industrial-hot-joint", JointSite = true, Input = (seed, engine) =>
            new ProceduralLayoutInput(V2Funded(P2ProfileCatalog.IndustrialTemperate, SettlementTechStageLevel.IndustrialMid, Hot, 100),
                Plains(180), (ulong)seed, CoreCatalog, new LayoutFortificationReservation(2), engine: engine) };
        yield return new V2MatrixCase { Name = "tribal-cold-joint", JointSite = true, Input = (seed, engine) =>
            new ProceduralLayoutInput(V2Funded(P2ProfileCatalog.TribalTemperate, SettlementTechStageLevel.Neolithic, Cold, 100),
                Plains(180), (ulong)seed, CoreCatalog, new LayoutFortificationReservation(1), engine: engine) };
        yield return new V2MatrixCase { Name = "industrial-3layers-joint", JointSite = true, Input = (seed, engine) =>
            new ProceduralLayoutInput(V2Funded(P2ProfileCatalog.IndustrialTemperate, SettlementTechStageLevel.IndustrialLate, Temperate, 180),
                Plains(180), (ulong)seed, CoreCatalog, new LayoutFortificationReservation(3), engine: engine) };
        yield return new V2MatrixCase { Name = "clutter", Input = (seed, engine) =>
            new ProceduralLayoutInput(LayoutRequirements.FromPopulation(6, Industrial, Temperate), V2Clutter(seed % 4), (ulong)seed,
                CoreCatalog, engine: engine) };
        yield return new V2MatrixCase { Name = "clutter-joint", JointSite = true, Input = (seed, engine) =>
            new ProceduralLayoutInput(V2Funded(P2ProfileCatalog.IndustrialTemperate, SettlementTechStageLevel.IndustrialLate, Temperate, 100),
                V2Clutter(seed % 4), (ulong)seed, CoreCatalog, new LayoutFortificationReservation(1), engine: engine) };
        yield return new V2MatrixCase { Name = "live-density", Input = (seed, engine) =>
            new ProceduralLayoutInput(LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, new LayoutClimate(-9, 32),
                stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialMid)), V2LiveDensity(seed % 6), 0x8B704E948E310455UL + (ulong)seed,
                CoreCatalog, resources: new LayoutLocalResources(29800, new[] { "BlocksGranite", "BlocksMarble" }), engine: engine) };
        yield return new V2MatrixCase { Name = "obstacles", Input = (seed, engine) =>
            new ProceduralLayoutInput(LayoutRequirements.FromPopulation(6, Industrial, Temperate), Obstacles(120), (ulong)seed,
                CoreCatalog, engine: engine) };
        yield return new V2MatrixCase { Name = "captured-flat", Input = (seed, engine) =>
            new ProceduralLayoutInput(LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, new LayoutClimate(-8, 32),
                stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialMid)), V2Rle("flat-rejected-traits.rle", "captured-flat-obstruction-v1"),
                0xCB52CAC6AEE2BE0AUL + (ulong)seed, CoreCatalog, new LayoutFortificationReservation(1),
                resources: new LayoutLocalResources(32667, new[] { "BlocksGranite", "BlocksMarble" },
                    steamGeyserCells: new[] { new GridPoint(70, 32), new GridPoint(58, 69) }), engine: engine) };
        yield return new V2MatrixCase { Name = "p4e-ten-rejected", JointSite = true, Input = (seed, engine) =>
        {
            LayoutClimate climate = new LayoutClimate(-4, 37);
            SettlementTechStage stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
            return new ProceduralLayoutInput(LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, climate, stage: stage,
                    populationBudget: OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate, stage, climate, 150, defenseAllowancePercent: 100),
                    mortarBiasPercent: 100),
                V2Rle("p4e-ten-rejected-traits.rle", "p4e-live-ten-rejected-ED06EE5676C0012E"), 0x15A894EE21B8FC83UL + (ulong)seed, CoreCatalog,
                new LayoutFortificationReservation(1), resources: new LayoutLocalResources(28352,
                    new[] { "BlocksGranite", "BlocksLimestone", "BlocksMarble" },
                    steamGeyserCells: new[] { new GridPoint(65, 45), new GridPoint(129, 106) }), engine: engine);
        } };
        yield return new V2MatrixCase { Name = "p4e-flat-cooler", JointSite = true, Input = (seed, engine) =>
        {
            LayoutClimate climate = new LayoutClimate(-1, 40);
            SettlementTechStage stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
            return new ProceduralLayoutInput(LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, climate, stage: stage,
                    populationBudget: OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate, stage, climate, 150),
                    mortarBiasPercent: 100),
                V2Rle("p4e-flat-cooler-traits.rle", "live-flat-cooler-93DDC95416B2BC26"), 0xB0CC855318FF46C8UL + (ulong)seed,
                RuntimeFlatCatalogFixture.Build(), new LayoutFortificationReservation(1), resources:
                new LayoutLocalResources(29888, new[] { "BlocksGranite", "BlocksLimestone", "BlocksSlate" },
                    steamGeyserCells: new[] { new GridPoint(65, 124), new GridPoint(25, 32) }), engine: engine);
        } };
        yield return new V2MatrixCase { Name = "mountain-tribal", Input = (seed, engine) =>
            new ProceduralLayoutInput(LayoutRequirements.FromPopulation(6, Tribal, Cold), Mountain(130, 110), (ulong)seed, CoreCatalog, engine: engine) };
        yield return new V2MatrixCase { Name = "mountain-industrial", Input = (seed, engine) =>
            new ProceduralLayoutInput(LayoutRequirements.FromPopulation(6, Industrial, Temperate), Mountain(130, 110), (ulong)seed, CoreCatalog, engine: engine) };
    }

    // Circulation and yard overhead per room cell: enclosure cells that are neither room nor court.
    private static double V2Overhead(ProceduralLayout l)
    {
        HashSet<GridPoint> rooms = new HashSet<GridPoint>(l.Rooms.SelectMany(r => r.Bounds.Points()));
        int yards = l.PowerYards.Sum(y => y.Cells.Count + y.ZoneCells.Count) + l.Courts.Sum(c => c.Cells.Count);
        return Math.Max(0, l.Enclosure.Count - rooms.Count - yards) / (double)rooms.Count;
    }

    private sealed class V2MatrixRun
    {
        public V2MatrixCase Case;
        public LayoutEngine Engine;
        public int Seed;
        public bool Ok;
        public int Shared;
        public double Overhead;
        public long Ms;
        public string Failure;
    }

    // Plans are pure and independent, so the matrix runs them in parallel and
    // reports in a fixed order. ms/plan is per plan and rises under contention.
    private static void V2Matrix()
    {
        int seeds = 6;
        string extra = Environment.GetEnvironmentVariable("V2_SEEDS");
        if (!string.IsNullOrEmpty(extra)) seeds = int.Parse(extra);
        string filter = Environment.GetEnvironmentVariable("V2_CASE");
        TechCatalog warm = CoreCatalog;
        List<V2MatrixRun> runs = new List<V2MatrixRun>();
        foreach (V2MatrixCase c in V2MatrixCases())
        {
            if (!string.IsNullOrEmpty(filter) && c.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            foreach (LayoutEngine engine in new[] { LayoutEngine.Legacy, LayoutEngine.Clusters })
                for (int seed = 1; seed <= seeds; seed++) runs.Add(new V2MatrixRun { Case = c, Engine = engine, Seed = seed });
        }
        Stopwatch wall = Stopwatch.StartNew();
        // Longest cases first keeps the tail short.
        System.Threading.Tasks.Parallel.ForEach(runs.OrderByDescending(r => r.Case.JointSite).ToList(),
            new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, run =>
        {
            ProceduralLayoutInput input = run.Case.Input(run.Seed, run.Engine);
            Stopwatch watch = Stopwatch.StartNew();
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: run.Case.JointSite);
            run.Ms = watch.ElapsedMilliseconds;
            if (result.Success && ProceduralLayoutValidator.Validate(input, result.Layout).Count == 0)
            {
                run.Ok = true;
                run.Shared = SharedWallCells(result.Layout);
                run.Overhead = V2Overhead(result.Layout);
                if (run.Seed == 1) ExportLayout("v2m-" + run.Engine + "-" + run.Case.Name, result, input.Terrain);
            }
            else run.Failure = run.Seed + ":" + (result.RejectionCode ?? "validator") + ":" +
                string.Join("|", result.Diagnostics.Where(d => d.Stage == "attempt" || d.Stage == "validate" || d.Stage == "joint_site")
                    .Select(d => d.Code).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(3).Select(g => g.Key + "x" + g.Count()));
        });
        int[] totals = new int[4];
        foreach (IGrouping<string, V2MatrixRun> group in runs.GroupBy(r => r.Case.Name + "|" + r.Engine))
        {
            List<V2MatrixRun> list = group.ToList();
            List<V2MatrixRun> ok = list.Where(r => r.Ok).ToList();
            int i = list[0].Engine == LayoutEngine.Legacy ? 0 : 2;
            totals[i] += ok.Count;
            totals[i + 1] += list.Count;
            Console.WriteLine("  " + list[0].Case.Name.PadRight(26) + list[0].Engine.ToString().PadRight(9) + " ok=" + ok.Count + "/" + list.Count +
                " shared=" + (ok.Count == 0 ? 0 : ok.Average(r => r.Shared)).ToString("F0") +
                " overhead=" + (ok.Count == 0 ? 0 : ok.Average(r => r.Overhead)).ToString("F2") +
                " ms/plan=" + (long)list.Average(r => r.Ms) +
                (ok.Count == list.Count ? string.Empty : " fail=" + string.Join(" ; ", list.Where(r => !r.Ok).Select(r => r.Failure))));
        }
        Console.WriteLine("  TOTAL legacy=" + totals[0] + "/" + totals[1] + " clusters=" + totals[2] + "/" + totals[3] +
            " wall=" + wall.ElapsedMilliseconds / 1000 + "s threads=" + Environment.ProcessorCount);
    }
}
