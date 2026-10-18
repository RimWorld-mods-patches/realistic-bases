using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RealisticBases.Planning;

internal static partial class Program
{
    private const double EnemySniperRange = 44.9;

    private static void T4ARenewableFixtures()
    {
        LayoutPowerYard panel = new LayoutPowerYard(LayoutPowerModel.Solar, new GridPoint[0], new GridPoint(0, 0),
            null, "North", 1700, 0.5);
        LayoutPowerYard turbine = new LayoutPowerYard(LayoutPowerModel.Wind, new GridPoint[0], new GridPoint(0, 0),
            null, "North", 2300, 0.4);
        Equal(566, LayoutPowerModel.FirmWatts(new[] { panel }), "solar credit includes charging losses");
        Equal(613, LayoutPowerModel.FirmWatts(new[] { turbine }), "wind credit includes charging losses");
        Equal(1, LayoutPowerModel.RenewableStorageBatteries(new[] { panel, turbine }), "off-share storage is added to the bridge");
        Equal(1, LayoutPowerModel.Generators(100, 0, new[] { panel }, 1000), "intermittent critical credit retains fuel backup");
        foreach (bool steam in new[] { true, false })
        {
            ProceduralLayoutInput input = new ProceduralLayoutInput(
                LayoutRequirements.FromPopulation(8, Industrial, Temperate, stage: MidStage),
                Plains(130), steam ? 1UL : 17UL, CoreCatalog,
                resources: steam ? new LayoutLocalResources(int.MaxValue, null, null, CentreGeysers) : null);
            Stopwatch watch = Stopwatch.StartNew();
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
            watch.Stop();
            Accepted(result);
            LayoutPowerPlan power = result.Layout.Power;
            True(steam ? power.Yards.Any(y => y.DefName == LayoutPowerModel.Geothermal) :
                power.Yards.Any(y => y.DefName == LayoutPowerModel.Solar) && power.Yards.Any(y => y.DefName == LayoutPowerModel.Wind),
                "renewable comparison fixture retains the requested sources");
            if (steam) Equal(0, power.Generators, "geothermal alone funds this fixture's entire connected load");
            else Equal(LayoutPowerModel.Batteries(power.CriticalWatts + power.NonCriticalWatts) +
                power.RenewableStorageBatteries, power.Batteries, "wind and solar keep the bridge plus off-share storage");
            True(P4BTurretPlanner.AvailableWatts(result.Layout) > 0, "renewable firm output is available to turret planning");
            Console.WriteLine("  t4a-renewable " + (steam ? "steam" : "wind-solar") + " input=" + input.Digest +
                " sources=" + string.Join(",", power.Yards.Select(y => y.DefName)) +
                " averageW=" + power.RenewableWatts + " firmW=" + power.FirmWatts + " storageBatteries=" + power.RenewableStorageBatteries + " criticalW=" + power.CriticalWatts +
                " nonCriticalW=" + power.NonCriticalWatts + " generators=" + power.Generators +
                " batteries=" + power.Batteries + " fuel=" + power.FuelStock + " plannerMs=" + watch.ElapsedMilliseconds);
        }
    }

    private static void PrintTurretBatteryDiagnostics(ProceduralLayoutInput input,
        ProceduralLayout layout, string label, long plannerMilliseconds,
        IEnumerable<LayoutDiagnostic> diagnostics = null)
    {
        P4BTurretPlan network = layout.Turret;
        if (network == null)
        {
            Console.WriteLine("  turret-t0 " + label + " turrets=0 watts=0 import=0 plannerMs=" +
                plannerMilliseconds + " reason=no_turret_plan decision=" + string.Join(";",
                    (diagnostics ?? Enumerable.Empty<LayoutDiagnostic>()).Where(d => d.Stage == "turret")));
            foreach (IGrouping<string, GridPoint> crossing in (layout.ApproachDefense?.Posts
                .Select(post => post.AccessGate) ?? Enumerable.Empty<GridPoint>())
                .Distinct().OrderBy(c => CrossingName(layout, c), StringComparer.Ordinal)
                .GroupBy(c => CrossingName(layout, c), StringComparer.Ordinal))
                Console.WriteLine("  turret-t0-crossing " + label + " crossing=" + crossing.Key +
                    " types=none turrets=0 exposure=0 answered=0 unanswered=0 watts=0 import=0");
            Console.WriteLine("  turret-t0-total " + label + " denseExposure=0 answered=0 unanswered=0");
            return;
        }
        if (layout.ApproachDefense == null)
        {
            Console.WriteLine("  turret-t0 " + label + " inputDigest=" + input.Digest + " layoutDigest=" +
                layout.Digest + " turrets=" + network.All.Count() + " watts=" + network.TotalWatts +
                " import=" + network.ImportValue + " plannerMs=" + plannerMilliseconds +
                " reason=no_approach_defense");
            return;
        }

        List<P4BTurretPlan> guns = network.All.OrderBy(g => g.AccessGate ?? new GridPoint(-1, -1))
            .ThenBy(g => g.Side, StringComparer.Ordinal).ThenBy(g => g.DefName, StringComparer.Ordinal)
            .ThenBy(g => g.Origin).ToList();
        string types = string.Join(",", guns.GroupBy(g => g.DefName, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count()));
        Console.WriteLine("  turret-t0 " + label + " inputDigest=" + input.Digest + " layoutDigest=" + layout.Digest +
            " types=" + types + " turrets=" + guns.Count + " watts=" + network.TotalWatts +
            " import=" + network.ImportValue + " plannerMs=" + plannerMilliseconds);

        P4BStaffingAudit staffing = P4BStaffing.Audit(input, layout, network);
        HashSet<GridPoint> baseOpaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
        Dictionary<P4BTurretPlan, (int Exposure, int Answered)> metrics = guns.ToDictionary(g => g,
            g => TurretExposure(input, layout, g, guns, staffing, baseOpaque));
        HashSet<GridPoint> defendedGates = new HashSet<GridPoint>(layout.ApproachDefense?.Posts
            .Select(post => post.AccessGate) ?? Enumerable.Empty<GridPoint>());
        var crossings = defendedGates.OrderBy(c => CrossingName(layout, c), StringComparer.Ordinal)
            .GroupBy(c => CrossingName(layout, c), StringComparer.Ordinal);
        int totalExposure = 0, totalAnswered = 0;
        foreach (IGrouping<string, GridPoint> crossing in crossings)
        {
            HashSet<GridPoint> gates = new HashSet<GridPoint>(crossing);
            List<P4BTurretPlan> crossingGuns = guns.Where(g => g.AccessGate.HasValue && gates.Contains(g.AccessGate.Value)).ToList();
            int exposure = crossingGuns.Sum(g => metrics[g].Exposure);
            int answered = crossingGuns.Sum(g => metrics[g].Answered);
            totalExposure += exposure;
            totalAnswered += answered;
            string crossingTypes = string.Join(",", crossingGuns.GroupBy(g => g.DefName, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count()));
            int crossingWatts = crossingGuns.Sum(g => g.Watts);
            int crossingImport = crossingGuns.Sum(PhysicalTurretImportValue);
            Console.WriteLine("  turret-t0-crossing " + label + " crossing=" + crossing.Key +
                " types=" + (crossingTypes.Length == 0 ? "none" : crossingTypes) +
                " turrets=" + crossingGuns.Count + " exposure=" + exposure + " answered=" + answered +
                " unanswered=" + (exposure - answered) + " watts=" + crossingWatts + " import=" + crossingImport);
        }
        Console.WriteLine("  turret-t0-total " + label + " denseExposure=" + totalExposure +
            " answered=" + totalAnswered + " unanswered=" + (totalExposure - totalAnswered));
        foreach (P4BTurretPlan gun in guns)
        {
            (int exposure, int answered) = metrics[gun];
            Console.WriteLine("  turret-t0-gun " + label + " crossing=" + CrossingName(layout,
                gun.AccessGate ?? new GridPoint(-1, -1)) + " type=" + gun.DefName + " origin=" + gun.Origin +
                " exposure=" + exposure + " answered=" + answered + " unanswered=" + (exposure - answered) +
                " watts=" + gun.Watts + " import=" + PhysicalTurretImportValue(gun));
        }
    }

    private static void RunTurretBatteryT4OutcomeTests()
    {
        if (only == null || only.IndexOf("T4 outcome", StringComparison.OrdinalIgnoreCase) < 0) return;
        Run("T4 outcome wealth sweep", TurretBatteryT4Outcomes, 400);
    }

    private static void TurretBatteryT4Outcomes()
    {
        string filter = Environment.GetEnvironmentVariable("T4_OUTCOME_FILTER");
        string[] filterLabels = string.IsNullOrEmpty(filter) ? Array.Empty<string>() : filter.Split(',');
        List<Action> cases = new List<Action>();
        SettlementTechStage industrialLate = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        foreach (ulong seed in Enumerable.Range(1, 8).Select(n => (ulong)n))
        {
            OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(
                P2ProfileCatalog.IndustrialTemperate, industrialLate, Temperate, 180);
            LayoutRequirements requirements = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
                Temperate, stage: industrialLate, populationBudget: budget);
            ProceduralLayoutInput input = new ProceduralLayoutInput(requirements, Plains(180), seed, CoreCatalog,
                new LayoutFortificationReservation(1));
            cases.Add(() => RunTurretBatteryOutcome(input, "total12-seed" + seed + "-w180", 180, true, filterLabels));
        }

        foreach (int wealth in new[] { 60, 100, 180 })
        {
            LayoutRequirements requirements = V2Funded(P2ProfileCatalog.TribalTemperate,
                SettlementTechStageLevel.Neolithic, Temperate, wealth);
            ProceduralLayoutInput input = new ProceduralLayoutInput(requirements, Lakeshore(180), 1, CoreCatalog,
                new LayoutFortificationReservation(1));
            cases.Add(() => RunTurretBatteryOutcome(input, "lakeshore-joint-w" + wealth, wealth, true, filterLabels));
        }

        foreach (int wealth in new[] { 60, 100, 180 })
        {
            OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(
                P2ProfileCatalog.IndustrialTemperate, industrialLate, Temperate, wealth);
            LayoutRequirements requirements = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
                Temperate, stage: industrialLate, populationBudget: budget);
            ProceduralLayoutInput input = new ProceduralLayoutInput(requirements, Plains(160), 1, CoreCatalog,
                new LayoutFortificationReservation(1));
            cases.Add(() => RunTurretBatteryOutcome(input, "broad-flat-w" + wealth, wealth, true, filterLabels));
        }
        System.Threading.Tasks.Parallel.ForEach(cases,
            new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 }, run => run());
    }

    private static void RunTurretBatteryOutcome(ProceduralLayoutInput input, string label, int wealth, bool jointSite,
        string[] filterLabels)
    {
        if (filterLabels.Length > 0 && !filterLabels.Any(f => label.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)) return;
        Stopwatch watch = Stopwatch.StartNew();
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: jointSite);
        watch.Stop();
        if (result.Layout == null)
        {
            string rejections = TopIncompleteBatteryRejections(result.Diagnostics);
            string turretDetails = string.Join(";", result.Diagnostics.Where(d => d.Stage == "turret").Select(d => d.Detail));
            Console.WriteLine("  turret-t4-outcome " + label + " wealth=" + wealth + " residents=" + input.Requirements.Residents +
                " status=failed rejection=" + result.RejectionCode + " plannerMs=" + watch.ElapsedMilliseconds +
                " incompleteCrossingRejects=" + rejections + " diagnostics=" + string.Join(";",
                    result.Diagnostics.Where(d => d.Stage == "turret")));
            if (turretDetails.Length > 0) Console.WriteLine("  turret-t4-diagnostic " + label + " " + turretDetails);
            throw new InvalidOperationException(label + ": " + result.RejectionCode + " " +
                string.Join(";", result.Diagnostics.TakeLast(8)));
        }
        ProceduralLayout layout = result.Layout;
        LayoutRoom powerRoom = layout.Rooms.FirstOrDefault(r => r.Kind == LayoutRoomKind.Power);
        string powerRoomSize = powerRoom == null ? "none" :
            powerRoom.Interior.Width + "x" + powerRoom.Interior.Height;
        P4BTurretPlan network = layout.Turret;
        Equal(0, ProceduralLayoutValidator.Validate(input, layout).Count, "outcome final layout validates");
        if (network != null) True(P4BTurretPlanner.PowerTerminalsConnected(input, layout, network),
            "trimmed outcome retains every turret power terminal");
        List<P4BTurretPlan> guns = (network?.All ?? Enumerable.Empty<P4BTurretPlan>()).ToList();
        foreach (var crossing in guns.GroupBy(g => g.AccessGate))
        {
            True(crossing.Count(g => g.DefName == "Turret_MiniTurret") <= 6, "crossing mini limit includes heavy support");
            True(crossing.Count(g => g.DefName == "Turret_Autocannon") <= 2, "crossing autocannon limit");
            True(crossing.Count(g => g.DefName == "Turret_Sniper") <= 1, "crossing slug limit");
        }
        if (layout.Power != null && input.Requirements.DefenseReserveWatts > 0)
        {
            LayoutPowerModel.Loads(layout.Rooms, out int critical, out int nonCritical);
            Equal(LayoutPowerModel.Generators(critical + (network?.TotalWatts ?? 0), nonCritical,
                layout.Power.Yards, layout.Power.GeneratorWatts), layout.Power.Generators,
                "trim keeps the minimum generators for actual connected load");
            Equal(LayoutPowerModel.Batteries(critical + nonCritical + (network?.TotalWatts ?? 0)) + layout.Power.RenewableStorageBatteries,
                layout.Power.Batteries, "trim keeps the actual six-hour battery bridge");
            True(layout.Power.ReservedGenerators >= layout.Power.Generators, "trim adds no generator");
            True(layout.Power.ReservedBatteries >= layout.Power.Batteries, "trim adds no battery");
        }
        int defended = layout.ApproachDefense?.Posts.Select(p => p.AccessGate).Distinct().Count() ?? 0;
        Dictionary<GridPoint?, int> minis = guns.Where(g => g.DefName == "Turret_MiniTurret")
            .GroupBy(g => g.AccessGate).ToDictionary(g => g.Key, g => g.Count());
        int withBattery = minis.Count(kv => kv.Value >= 3);
        int withoutBattery = Math.Max(0, defended - withBattery);
        int[] counts = minis.Where(kv => kv.Value >= 3).Select(kv => kv.Value).OrderBy(n => n).ToArray();
        string batteryMinMedianMax = counts.Length == 0 ? "none" : counts.Min() + "/" +
            (counts.Length % 2 == 1 ? counts[counts.Length / 2] :
                (counts[counts.Length / 2 - 1] + counts[counts.Length / 2]) / 2.0) + "/" + counts.Max();
        string byType = string.Join(",", guns.GroupBy(g => g.DefName, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count()));
        int reservedWatts = layout.Power?.DefenseReserveWatts ?? 0;
        int reservedImport = input.Requirements.DefenseImportReserveValue;
        int baseImport = Math.Max(0, layout.Materials.ImportValue - (network?.ImportValue ?? 0));
        string rejectionCodes = TopIncompleteBatteryRejections(result.Diagnostics);
        Console.WriteLine("  turret-t4-outcome " + label + " wealth=" + wealth + " residents=" + input.Requirements.Residents +
            " status=accepted types=" + (byType.Length == 0 ? "none" : byType) +
            " defendedCrossings=" + defended + " batteryCrossings=" + withBattery + " withoutBattery=" + withoutBattery +
            " minisPerBatteryMinMedianMax=" + batteryMinMedianMax +
            " heavies=" + guns.Count(g => g.DefName == "Turret_Autocannon" || g.DefName == "Turret_Sniper") +
            " wattsUsedReserved=" + (network?.TotalWatts ?? 0) + "/" + reservedWatts +
            " renewableAverageFirm=" + (layout.Power?.RenewableWatts ?? 0) + "/" + (layout.Power?.FirmWatts ?? 0) +
            " renewableStorageBatteries=" + (layout.Power?.RenewableStorageBatteries ?? 0) +
            " turretImportSpentReserved=" + (network?.ImportValue ?? 0) + "/" + reservedImport +
            " generatorsReservedKept=" + (layout.Power?.ReservedGenerators ?? 0) + "/" + (layout.Power?.Generators ?? 0) +
            " batteriesReservedKept=" + (layout.Power?.ReservedBatteries ?? 0) + "/" + (layout.Power?.Batteries ?? 0) +
            " powerRoomBeforeAfter=" + powerRoomSize + "/" + powerRoomSize +
            " fuelReservedKept=" + LayoutPowerModel.FuelStock(input.Equipment.Kit.Generator,
                layout.Power?.ReservedGenerators ?? 0) + "/" + (layout.Power?.FuelStock ?? 0) +
            " warehouseCellsReleased=" + (layout.Power?.ReleasedFuelStorageCells ?? 0) +
            " materialImportPackedKept=" + (layout.Power?.PackedMaterialImportValue ?? 0) + "/" + layout.Materials.ImportValue +
            " baseImportSpent=" + baseImport + " totalImportCap=" + input.Requirements.ImportValueLimit +
            " plannerMs=" + watch.ElapsedMilliseconds + " incompleteCrossingRejects=" + rejectionCodes);
        if (rejectionCodes.Length == 0)
        {
            string turretDetails = string.Join(";", result.Diagnostics.Where(d => d.Stage == "turret").Select(d => d.Detail));
            if (turretDetails.Length > 0)
                Console.WriteLine("  turret-t4-diagnostic " + label + " " + turretDetails);
        }
        PrintTurretBatteryDiagnostics(input, layout, label, watch.ElapsedMilliseconds, result.Diagnostics);
        string mapExportDirectory = Environment.GetEnvironmentVariable("T4_MAP_EXPORT_DIR");
        if (!string.IsNullOrEmpty(mapExportDirectory))
            ExportTurretMap(input, layout, label, result.Diagnostics, mapExportDirectory);
    }

    private static void ExportTurretMap(ProceduralLayoutInput input, ProceduralLayout layout,
        string label, IEnumerable<LayoutDiagnostic> diagnostics, string directory)
    {
        Directory.CreateDirectory(directory);
        List<P4BTurretPlan> guns = (layout.Turret?.All ?? Enumerable.Empty<P4BTurretPlan>()).ToList();
        P4BStaffingAudit staffing = guns.Count == 0 ? null : P4BStaffing.Audit(input, layout, layout.Turret);
        HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
        Dictionary<P4BTurretPlan, (int Exposure, int Answered)> gunMetrics = guns.ToDictionary(g => g,
            g => TurretExposure(input, layout, g, guns, staffing, opaque));
        List<P4BSallyPort> ports = (layout.ApproachDefense?.SallyPorts ?? Array.Empty<P4BSallyPort>()).ToList();
        List<P4BPost> posts = (layout.ApproachDefense?.Posts ?? Array.Empty<P4BPost>()).ToList();
        Dictionary<string, Dictionary<string, int>> rejections = IncompleteCrossingRejections(diagnostics);
        StringBuilder json = new StringBuilder();
        json.AppendLine("{");
        json.Append("  \"base\": ").Append(Json(label)).AppendLine(",");
        json.Append("  \"layoutDigest\": ").Append(Json(layout.Digest)).AppendLine(",");
        json.AppendLine("  \"asciiGrid\": {");
        json.Append("    \"legend\": ").Append(Json(LayoutAsciiExport.Legend)).AppendLine(",");
        int minX, minZ, maxX, maxZ;
        AsciiBounds(layout, input.Terrain, 3, out minX, out minZ, out maxX, out maxZ);
        string[] rendered = LayoutAsciiExport.Render(input.Terrain, layout).Split('\n');
        string[] rows = rendered.Where(row => row.Length > 0).TakeLast(maxZ - minZ + 1).ToArray();
        json.Append("    \"originOffset\": {\"x\": ").Append(minX).Append(", \"z\": ").Append(maxZ)
            .AppendLine("},");
        json.Append("    \"width\": ").Append(maxX - minX + 1).AppendLine(",");
        json.Append("    \"height\": ").Append(rows.Length).AppendLine(",");
        json.AppendLine("    \"rowOrder\": \"north_to_south\",");
        json.AppendLine("    \"rows\": [");
        for (int i = 0; i < rows.Length; i++)
            json.Append("      ").Append(Json(rows[i])).AppendLine(i + 1 == rows.Length ? string.Empty : ",");
        json.AppendLine("    ]");
        json.AppendLine("  },");

        json.AppendLine("  \"crossings\": [");
        for (int i = 0; i < ports.Count; i++)
        {
            P4BSallyPort port = ports[i];
            string id = (port.Field ? "field-sally@" : "compound-sally@") + port.Cell;
            List<P4BPost> crossingPosts = posts.Where(p => port.Doors.Contains(p.AccessGate)).ToList();
            string[] sides = crossingPosts.Select(p => p.Side).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
            List<P4BTurretPlan> crossingGuns = guns.Where(g => g.AccessGate.HasValue &&
                port.Doors.Contains(g.AccessGate.Value)).ToList();
            int minis = crossingGuns.Count(g => g.DefName == "Turret_MiniTurret");
            int heavies = crossingGuns.Count(g => g.DefName == "Turret_Autocannon" || g.DefName == "Turret_Sniper");
            bool failedAttempt = sides.Any(side => rejections.ContainsKey(port.Cell + "/" + side));
            string status = minis >= 3 ? "complete" : failedAttempt ? "failed" : "none";
            var top = sides.Where(side => rejections.ContainsKey(port.Cell + "/" + side))
                .SelectMany(side => rejections[port.Cell + "/" + side])
                .GroupBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(g => new { Code = g.Key, Count = g.Sum(x => x.Value) })
                .OrderByDescending(x => x.Count).ThenBy(x => x.Code, StringComparer.Ordinal).FirstOrDefault();
            json.AppendLine("    {");
            json.Append("      \"id\": ").Append(Json(id)).AppendLine(",");
            json.Append("      \"doorCells\": ").Append(JsonPoints(port.Doors)).AppendLine(",");
            json.Append("      \"side\": ").Append(Json(sides.Length == 0 ? "none" : string.Join(",", sides))).AppendLine(",");
            json.Append("      \"status\": ").Append(Json(status)).AppendLine(",");
            json.Append("      \"minis\": ").Append(minis).AppendLine(",");
            json.Append("      \"heavies\": {\"total\": ").Append(heavies)
                .Append(", \"autocannon\": ").Append(crossingGuns.Count(g => g.DefName == "Turret_Autocannon"))
                .Append(", \"sniper\": ").Append(crossingGuns.Count(g => g.DefName == "Turret_Sniper")).AppendLine("},");
            json.Append("      \"topRejection\": ");
            if (top == null) json.AppendLine("null,");
            else json.Append("{\"code\": ").Append(Json(top.Code)).Append(", \"count\": ").Append(top.Count).AppendLine("},");
            json.AppendLine("      \"rejectedCandidateOriginSamples\": [],");
            json.AppendLine("      \"rejectedCandidateOriginSamplesNote\": \"Current planner diagnostics aggregate rejection codes without recording rejected candidate coordinates.\"");
            json.Append("    }").AppendLine(i + 1 == ports.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");

        json.AppendLine("  \"turrets\": [");
        for (int i = 0; i < guns.Count; i++)
        {
            P4BTurretPlan gun = guns[i];
            (int exposure, int answered) = gunMetrics[gun];
            json.AppendLine("    {");
            json.Append("      \"type\": ").Append(Json(gun.DefName)).AppendLine(",");
            json.Append("      \"footprint\": ").Append(JsonPoints(gun.Footprint)).AppendLine(",");
            json.Append("      \"coverCells\": ").Append(JsonPoints(gun.CoverCells)).AppendLine(",");
            json.Append("      \"crossingId\": ").Append(Json(CrossingName(layout,
                gun.AccessGate ?? new GridPoint(-1, -1)))).AppendLine(",");
            json.Append("      \"exposure\": {\"answered\": ").Append(answered)
                .Append(", \"unanswered\": ").Append(exposure - answered).AppendLine("}");
            json.Append("    }").AppendLine(i + 1 == guns.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");

        json.AppendLine("  \"nestSlots\": [");
        for (int i = 0; i < posts.Count; i++)
        {
            P4BPost post = posts[i];
            json.Append("    {\"crossingId\": ").Append(Json(CrossingName(layout, post.AccessGate)))
                .Append(", \"side\": ").Append(Json(post.Side)).Append(", \"accessGate\": ")
                .Append(JsonPoint(post.AccessGate)).Append(", \"slots\": ").Append(JsonPoints(post.Slots))
                .Append(", \"coverCells\": ").Append(JsonPoints(post.CoverCells)).Append("}")
                .AppendLine(i + 1 == posts.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");
        json.Append("  \"sniperPosts\": ").Append(JsonPoints(staffing?.SniperPosts ?? Array.Empty<GridPoint>())).AppendLine(",");

        json.AppendLine("  \"attackerExposureGrid\": [");
        List<GridPoint> attackers = new List<GridPoint>();
        for (int x = 0; x < input.Terrain.Width; x += 2)
            for (int z = 0; z < input.Terrain.Height; z += 2) attackers.Add(new GridPoint(x, z));
        for (int i = 0; i < attackers.Count; i++)
        {
            GridPoint cell = attackers[i];
            bool reachable = layout.ApproachDefense.AttackerCanReach(cell);
            bool sees = reachable && guns.Any(g =>
            {
                if (Distance(g.Origin, cell) > EnemySniperRange) return false;
                HashSet<GridPoint> blockers = new HashSet<GridPoint>(opaque);
                blockers.ExceptWith(g.Footprint);
                return P4BFireLane.CanEngage(cell, g.Origin, 0, 45, blockers);
            });
            bool answered = sees && IsExposureAnswered(cell, guns, staffing, opaque);
            json.Append("    {\"x\": ").Append(cell.X).Append(", \"z\": ").Append(cell.Z)
                .Append(", \"attackerReachable\": ").Append(reachable ? "true" : "false")
                .Append(", \"canSeeAnyTurret\": ").Append(sees ? "true" : "false")
                .Append(", \"answered\": ").Append(answered ? "true" : "false").Append("}")
                .AppendLine(i + 1 == attackers.Count ? string.Empty : ",");
        }
        json.AppendLine("  ]");
        json.AppendLine("}");
        File.WriteAllText(Path.Combine(directory, label + ".json"), json.ToString());
    }

    private static Dictionary<string, Dictionary<string, int>> IncompleteCrossingRejections(
        IEnumerable<LayoutDiagnostic> diagnostics)
    {
        Dictionary<string, Dictionary<string, int>> totals = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (LayoutDiagnostic diagnostic in diagnostics ?? Enumerable.Empty<LayoutDiagnostic>())
        {
            if (diagnostic.Stage != "turret") continue;
            Match decisions = Regex.Match(diagnostic.Detail, @"decisions=\[(.*)\]");
            if (!decisions.Success) continue;
            string[] tokens = decisions.Groups[1].Value.Split(';');
            HashSet<string> incomplete = new HashSet<string>(tokens
                .Where(t => t.EndsWith(":incomplete_battery_removed", StringComparison.Ordinal))
                .Select(t => t.Substring(0, t.IndexOf(':'))), StringComparer.Ordinal);
            foreach (string token in tokens)
            {
                int split = token.IndexOf(':');
                if (split <= 0) continue;
                string key = token.Substring(0, split);
                if (!incomplete.Contains(key) || token.EndsWith(":incomplete_battery_removed", StringComparison.Ordinal)) continue;
                if (!totals.TryGetValue(key, out Dictionary<string, int> counts))
                    totals[key] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
                string reason = token.Substring(split + 1);
                MatchCollection matches = Regex.Matches(reason, @"([a-z][a-z0-9_]*):(\d+)");
                foreach (Match match in matches)
                {
                    string code = match.Groups[1].Value;
                    counts.TryGetValue(code, out int old);
                    counts[code] = old + int.Parse(match.Groups[2].Value);
                }
                if (matches.Count == 0 && !reason.StartsWith("accepted ", StringComparison.Ordinal) &&
                    reason != "incomplete_battery_removed")
                {
                    counts.TryGetValue(reason, out int old);
                    counts[reason] = old + 1;
                }
            }
        }
        return totals;
    }

    private static bool IsExposureAnswered(GridPoint cell, IReadOnlyList<P4BTurretPlan> guns,
        P4BStaffingAudit staffing, HashSet<GridPoint> opaque)
    {
        if (staffing != null && staffing.SniperPosts.Any(post => Distance(post, cell) <= EnemySniperRange &&
            P4BFireLane.CanEngage(post, cell, 0, 45, opaque))) return true;
        if (guns.Where(g => g.DefName == "Turret_Sniper").Any(slug =>
        {
            double distance = Distance(slug.Origin, cell);
            HashSet<GridPoint> blockers = new HashSet<GridPoint>(opaque);
            blockers.ExceptWith(slug.Footprint);
            return distance >= slug.MinimumRange && distance <= slug.MaximumRange &&
                P4BFireLane.CanEngage(slug.Origin, cell, 12, 46, blockers);
        })) return true;
        return guns.Where(g => g.DefName == "Turret_MiniTurret").Any(mini =>
            Distance(mini.Origin, cell) <= mini.MaximumRange && P4BFireLane.CanEngage(mini.Origin, cell,
                0, (int)Math.Floor(mini.MaximumRange), opaque));
    }

    private static void AsciiBounds(ProceduralLayout layout, TerrainSnapshot terrain, int margin,
        out int minX, out int minZ, out int maxX, out int maxZ)
    {
        GridRect bounds = layout.BoundingRect;
        List<GridPoint> extras = layout.Gates.SelectMany(g => g.BandWalls.Concat(new[] { g.Staging }))
            .Concat(layout.OuterWalls).Concat(layout.FieldWall?.Walls ?? Array.Empty<GridPoint>())
            .Concat(layout.ApproachDefense?.CoverCells ?? Array.Empty<GridPoint>())
            .Concat(layout.Turret?.All.SelectMany(g => g.Footprint.Concat(g.CoverCells)) ?? Array.Empty<GridPoint>()).ToList();
        minX = Math.Max(0, Math.Min(bounds.MinX, extras.Select(c => c.X).DefaultIfEmpty(bounds.MinX).Min()) - margin);
        minZ = Math.Max(0, Math.Min(bounds.MinZ, extras.Select(c => c.Z).DefaultIfEmpty(bounds.MinZ).Min()) - margin);
        maxX = Math.Min(terrain.Width - 1, Math.Max(bounds.MinX + bounds.Width - 1,
            extras.Select(c => c.X).DefaultIfEmpty(bounds.MinX + bounds.Width - 1).Max()) + margin);
        maxZ = Math.Min(terrain.Height - 1, Math.Max(bounds.MinZ + bounds.Height - 1,
            extras.Select(c => c.Z).DefaultIfEmpty(bounds.MinZ + bounds.Height - 1).Max()) + margin);
    }

    private static string JsonPoints(IEnumerable<GridPoint> points) => "[" + string.Join(",", (points ?? Enumerable.Empty<GridPoint>())
        .Select(JsonPoint)) + "]";
    private static string JsonPoint(GridPoint point) => "{\"x\":" + point.X + ",\"z\":" + point.Z + "}";
    private static string Json(string value) => "\"" + (value ?? string.Empty).Replace("\\", "\\\\")
        .Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";

    private static string TopIncompleteBatteryRejections(IEnumerable<LayoutDiagnostic> diagnostics)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (LayoutDiagnostic diagnostic in diagnostics ?? Enumerable.Empty<LayoutDiagnostic>())
        {
            if (diagnostic.Stage != "turret") continue;
            // Decision entries include nested rejectedBefore=[...] lists, so
            // capture through the final closing bracket of the decision vector.
            Match decisions = Regex.Match(diagnostic.Detail, @"decisions=\[(.*)\]");
            if (!decisions.Success) continue;
            string[] tokens = decisions.Groups[1].Value.Split(';');
            HashSet<string> incomplete = new HashSet<string>(tokens
                .Where(t => t.EndsWith(":incomplete_battery_removed", StringComparison.Ordinal))
                .Select(t => t.Substring(0, t.IndexOf(':'))), StringComparer.Ordinal);
            foreach (string token in tokens)
            {
                int split = token.IndexOf(':');
                if (split <= 0 || !incomplete.Contains(token.Substring(0, split))) continue;
                string reason = token.Substring(split + 1);
                MatchCollection reasonCounts = Regex.Matches(reason, @"([a-z][a-z0-9_]*):(\d+)");
                foreach (Match match in reasonCounts)
                {
                    string code = match.Groups[1].Value;
                    int count = int.Parse(match.Groups[2].Value);
                    counts.TryGetValue(code, out int existing);
                    counts[code] = existing + count;
                }
                if (reasonCounts.Count == 0 && !reason.StartsWith("accepted ", StringComparison.Ordinal) &&
                    reason != "incomplete_battery_removed")
                {
                    counts.TryGetValue(reason, out int existing);
                    counts[reason] = existing + 1;
                }
            }
        }
        return string.Join(",", counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(5).Select(kv => kv.Key + ":" + kv.Value));
    }

    private static int PhysicalTurretImportValue(P4BTurretPlan gun) =>
        gun.ImportValue - (gun.Support?.ImportValue ?? 0) - gun.AdditionalGroups.Sum(group => group.ImportValue);

    private static void PrintPowerLedgerDiagnostics(ProceduralLayoutInput input, ProceduralLayout layout, string label)
    {
        string dispatchable = input.Equipment.Notes.FirstOrDefault(note => note.StartsWith("dispatchable:", StringComparison.Ordinal))
            ?? "dispatchable:none";
        string renewables = string.Join(",", layout.PowerYards.Where(y => y.DefName.Contains("Generator"))
            .GroupBy(y => y.DefName, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key + ":" + g.Count()));
        string dispatchablePlan = layout.Power == null ? "none" : layout.Power.GeneratorDef + ":" + layout.Power.Generators;
        string powerFuel = layout.Power == null ? "none" : layout.Power.FuelDef + ":" + layout.Power.FuelStock;
        Console.WriteLine("  power-t1 " + label + " dispatchablePlan=" + dispatchablePlan +
            " renewables=" + (renewables.Length == 0 ? "none" : renewables) + " powerFuel=" + powerFuel +
            " fuel=" + layout.Materials.GeneratorFuelDef + ":" + layout.Materials.GeneratorFuel +
            " fuelImported=" + layout.Materials.GeneratorFuelImported +
            " import=" + layout.Materials.ImportValue + " " + dispatchable);
    }

    private static (int Exposure, int Answered) TurretExposure(ProceduralLayoutInput input,
        ProceduralLayout layout, P4BTurretPlan gun, IReadOnlyList<P4BTurretPlan> guns,
        P4BStaffingAudit staffing, HashSet<GridPoint> baseOpaque)
    {
        HashSet<GridPoint> lineBlockers = new HashSet<GridPoint>(baseOpaque.Concat(guns
            .Where(other => !ReferenceEquals(other, gun)).SelectMany(other => other.Footprint)));
        HashSet<GridPoint> defenderBlockers = new HashSet<GridPoint>(baseOpaque.Concat(guns.SelectMany(other => other.Footprint)));
        int exposure = 0, answered = 0;
        for (int x = 0; x < input.Terrain.Width; x += 2)
            for (int z = 0; z < input.Terrain.Height; z += 2)
            {
                GridPoint attacker = new GridPoint(x, z);
                if (!layout.ApproachDefense.AttackerCanReach(attacker) ||
                    Distance(gun.Origin, attacker) > EnemySniperRange ||
                    !P4BFireLane.CanEngage(attacker, gun.Origin, 0, 45, lineBlockers)) continue;
                exposure++;
                bool answeredByPost = staffing.SniperPosts.Any(post =>
                    Distance(post, attacker) <= EnemySniperRange &&
                    P4BFireLane.CanEngage(post, attacker, 0, 45, defenderBlockers));
                bool answeredBySlug = guns.Where(other => other.DefName == "Turret_Sniper").Any(slug =>
                {
                    double distance = Distance(slug.Origin, attacker);
                    HashSet<GridPoint> slugBlockers = new HashSet<GridPoint>(baseOpaque.Concat(guns
                        .Where(other => !ReferenceEquals(other, slug)).SelectMany(other => other.Footprint)));
                    return distance >= slug.MinimumRange && distance <= slug.MaximumRange &&
                        P4BFireLane.CanEngage(slug.Origin, attacker, 0, 45, slugBlockers);
                });
                bool answeredByMini = gun.DefName == "Turret_MiniTurret" &&
                    Distance(gun.Origin, attacker) <= 28.9 &&
                    P4BFireLane.CanEngage(gun.Origin, attacker, 0, 29, lineBlockers);
                if (answeredByPost || answeredBySlug || answeredByMini) answered++;
            }
        return (exposure, answered);
    }

    private static string CrossingName(ProceduralLayout layout, GridPoint accessGate)
    {
        P4BSallyPort port = layout.ApproachDefense?.SallyPorts.FirstOrDefault(p => p.Doors.Contains(accessGate));
        if (port != null) return (port.Field ? "field" : "compound") + "-sally@" + port.Cell;
        LayoutGate arrival = layout.Gates.FirstOrDefault(g => g.Cell.Equals(accessGate) ||
            g.BandDoors.Contains(accessGate));
        if (arrival != null) return "arrival@" + arrival.Cell;
        return "access@" + accessGate;
    }

    private static double Distance(GridPoint a, GridPoint b)
    {
        long dx = a.X - b.X, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
