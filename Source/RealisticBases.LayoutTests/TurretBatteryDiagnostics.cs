using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    }

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
