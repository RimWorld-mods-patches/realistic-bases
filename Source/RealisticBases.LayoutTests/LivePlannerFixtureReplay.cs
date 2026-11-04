using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RealisticBases.Planning;

internal static class LivePlannerFixtureReplay
{
    public static int? RunIfRequested(string[] args)
    {
        if (!args.Contains("--live-fixture")) return null;
        int degree = ReadInt(args, "--degree", 1);
        int repeats = ReadInt(args, "--repeat", 1);
        if (degree < 1 || degree > 8) throw new ArgumentOutOfRangeException("--degree", "Degree must be from 1 through 8.");
        if (repeats < 1 || repeats > 10) throw new ArgumentOutOfRangeException("--repeat", "Repeat count must be from 1 through 10.");

        Type fixture = Assembly.GetExecutingAssembly().GetType("CapturedTimingTileFixture", throwOnError: false);
        if (fixture == null)
            throw new InvalidOperationException("No captured live fixture is compiled. Set LivePlannerFixture to its .cs path.");
        MethodInfo build = fixture.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        string tableVersion = fixture.GetField("ExpectedResearchTableVersion")?.GetRawConstantValue() as string ?? "tech-research.1";
        if (tableVersion != TechStageModel.ResearchTableVersion)
            throw new InvalidOperationException("Captured research table " + tableVersion + " differs from " + TechStageModel.ResearchTableVersion +
                ". Use -p:LegacyCapturedStageTables=true for the original pre-hydroponics captures.");
        string expectedInput = Constant<string>(fixture, "ExpectedInputDigest");
        string expectedLayout = Constant<string>(fixture, "ExpectedLayoutDigest");
        string expectedTerrain = Constant<string>(fixture, "ExpectedTerrainDigest");
        string expectedRequirements = Constant<string>(fixture, "ExpectedRequirementsDigest");
        string expectedCatalog = Constant<string>(fixture, "ExpectedCatalogDigest");
        string expectedResources = Constant<string>(fixture, "ExpectedResourcesDigest");
        string expectedProfile = Constant<string>(fixture, "ExpectedProfileDigest");
        string expectedEngine = Constant<string>(fixture, "ExpectedEngineId");
        string expectedRejection = ReadString(args, "--expect-rejection");
        string fixedLayout = ReadString(args, "--expect-fixed-layout");
        bool requireFixedSuccess = args.Contains("--require-success") || fixedLayout != null;
        if (requireFixedSuccess && expectedRejection != null)
            throw new InvalidOperationException("Fixed-success replay cannot be combined with --expect-rejection.");

        Equal(expectedProfile, ProfileDigest(fixture), "profile digest");
        bool expectsRejection = expectedLayout == "none" && !requireFixedSuccess;
        if (expectsRejection && string.IsNullOrEmpty(expectedRejection))
            throw new InvalidOperationException("A rejected fixture requires --expect-rejection with its captured rejection code.");
        if (!expectsRejection && !string.IsNullOrEmpty(expectedRejection))
            throw new InvalidOperationException("--expect-rejection was supplied for a successful fixture.");
        string[] expectedAttemptInputs = (string[])Constant<string[]>(fixture, "ExpectedAttemptInputDigests");
        string[] expectedAttemptLayouts = (string[])Constant<string[]>(fixture, "ExpectedAttemptLayoutDigests");
        if (expectedAttemptInputs.Length == 0 || expectedAttemptInputs.Length != expectedAttemptLayouts.Length)
            throw new InvalidOperationException("Captured input attempts and result digests are incomplete.");

        for (int repeat = 1; repeat <= repeats; repeat++)
        {
            using var rejectedCapture = RejectedFieldCandidateExport.Capture(ReadString(args, "--capture-field-failures"));
            ProceduralLayoutInput[] inputs = (ProceduralLayoutInput[])build.Invoke(null, new object[] { degree });
            if (inputs.Length != expectedAttemptInputs.Length)
                throw new InvalidOperationException("Captured attempt count mismatch: expected=" + expectedAttemptInputs.Length +
                    " actual=" + inputs.Length);
            ProceduralLayoutInput finalInput = inputs[inputs.Length - 1];
            string capturedFinalInputDigest = finalInput.Digest;
            for (int attempt = 0; attempt < inputs.Length; attempt++)
                Equal(expectedAttemptInputs[attempt], inputs[attempt].Digest, "captured attempt " + (attempt + 1) + " input digest");
            Equal(expectedTerrain, finalInput.Terrain.Digest, "terrain digest");
            Equal(expectedRequirements, finalInput.Requirements.Digest, "requirements digest");
            Equal(expectedCatalog, finalInput.Catalog.Digest, "catalog digest");
            Equal(expectedResources, finalInput.Resources.Digest, "resources digest");
            Equal(expectedEngine, finalInput.Engine.Id, "engine id");

            double plannerMs = 0, cpuMs = 0, site = 0, field = 0, fit = 0, approach = 0;
            double turret = 0, power = 0, validator = 0, scoring = 0;
            long allocatedBytes = 0;
            int validationCalls = 0;
            ProceduralLayoutResult result = null;
            ProceduralLayoutInput plannedInput = null;
            int attemptsRun = 0;
            for (int attempt = 0; attempt < inputs.Length; attempt++)
            {
                ProceduralLayoutInput input = inputs[attempt];
                plannedInput = input;
                attemptsRun++;
                double attemptCpuBefore = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;
                long attemptAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                Stopwatch watch = Stopwatch.StartNew();
                result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
                watch.Stop();
                plannerMs += watch.Elapsed.TotalMilliseconds;
                cpuMs += Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds - attemptCpuBefore;
                allocatedBytes += GC.GetAllocatedBytesForCurrentThread() - attemptAllocatedBefore;
                string attemptLayout = result.Layout?.Digest ?? "none";
                if (!requireFixedSuccess)
                    Equal(expectedAttemptLayouts[attempt], attemptLayout, "attempt " + (attempt + 1) + " layout digest");
                ProceduralLayoutStageTimings timing = result.StageTimings;
                site += timing.SiteAndCandidateSearchMilliseconds;
                field += timing.FieldWallMilliseconds;
                fit += timing.FitDefenseAccessMilliseconds;
                approach += timing.ApproachDefenseMilliseconds;
                turret += timing.TurretPlanningMilliseconds;
                power += timing.PowerTrimMilliseconds;
                validator += timing.ValidationMilliseconds;
                validationCalls += timing.ValidationCallCount;
                scoring += timing.ScoringMilliseconds;
                if (result.Success && P4BPerimeterDepth.Minimum(input.Terrain, result.Layout) == input.Fortification.WallLayers)
                    break;
            }
            Equal(expectedInput, finalInput.Digest, "final input digest");
            // A repair can succeed at an earlier wall-layer attempt. Keep the
            // capture anchors above, but validate/report that actual plan input.
            finalInput = plannedInput;
            if (expectsRejection)
            {
                if (result == null || result.Success || result.Layout != null)
                    throw new InvalidOperationException("Captured live rejection no longer reproduces; actual layout=" +
                        (result?.Layout?.Digest ?? "none") + " success=" + (result?.Success.ToString() ?? "null"));
                Equal(expectedRejection, result.RejectionCode, "rejection code");
                if (args.Contains("--joint-diagnostics"))
                    foreach (var diagnostic in result.Diagnostics) Console.WriteLine(diagnostic);
                Console.WriteLine("live-fixture-rejection-run inputDigest=" + finalInput.Digest +
                    " rejectionCode=" + result.RejectionCode + " attempts=" + inputs.Length +
                    " terrainDigest=" + finalInput.Terrain.Digest);
                continue;
            }
            if (result == null || !result.Success || result.Layout == null)
            {
                if (args.Contains("--joint-diagnostics") && result != null)
                    foreach (var diagnostic in result.Diagnostics) Console.WriteLine(diagnostic);
                throw new InvalidOperationException("Captured live planner attempts rejected: " + result?.RejectionCode);
            }
            if (!requireFixedSuccess) Equal(expectedLayout, result.Layout.Digest, "layout digest");
            else
            {
                if (fixedLayout != null) Equal(fixedLayout, result.Layout.Digest, "fixed layout digest");
                var preflight = RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(finalInput, result.Layout);
                if (!preflight.Accepted)
                    throw new InvalidOperationException("Fixed fixture spawn preflight rejected: " + preflight.Failure);
                Console.WriteLine("live-fixture-fixed-success inputDigest=" + finalInput.Digest +
                    " capturedFinalInputDigest=" + capturedFinalInputDigest +
                    " terrainDigest=" + finalInput.Terrain.Digest + " layoutDigest=" + result.Layout.Digest +
                    " residents=" + finalInput.Requirements.Residents + " crops=" + (result.Layout.FieldWall?.Fields.Count ?? 0));
            }
            if (args.Contains("--field-route")) CalderaFieldRouteRegression.Run(finalInput, result.Layout);
            if (args.Contains("--food-report") && result.Layout.FoodSupply != null)
            {
                var food = result.Layout.FoodSupply;
                Console.WriteLine("food-supply-report natural=" + food.NaturalCells +
                    " basins=" + result.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.HydroponicsBasin) +
                    " sunLamps=" + result.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.SunLamp) +
                    " hydroFieldEquivalent=" + food.HydroponicFieldEquivalent.ToString(CultureInfo.InvariantCulture) +
                    " importedNutritionPerDay=" + food.ImportNutritionPerDay.ToString(CultureInfo.InvariantCulture) +
                    " foodStock=" + food.TotalFoodUnits + " foodValue=" + food.ImportValue +
                    " fuelDays=" + result.Layout.Power?.FuelDays + " ordinaryImports=" + result.Layout.Materials.OrdinaryImportValue +
                    "/" + finalInput.Requirements.BaseImportValueLimit);
            }
            if (args.Contains("--resident-home-report") || args.Contains("--require-home-floor"))
            {
                int available = ReportResidentHomeSpace(finalInput, result.Layout);
                if (args.Contains("--require-home-floor") && available < finalInput.Requirements.Residents)
                    throw new InvalidOperationException("Resident housing floor shortfall: " + available + "/" + finalInput.Requirements.Residents);
            }
            if (args.Contains("--joint-diagnostics"))
                foreach (var diagnostic in result.Diagnostics) Console.WriteLine(diagnostic);
            string stageText = "siteCandidate=" + F(site) +
                ",fieldWall=" + F(field) +
                ",fitDefenseAccess=" + F(fit) +
                ",approachDefense=" + F(approach) +
                ",turretPlanning=" + F(turret) +
                ",powerTrim=" + F(power) +
                ",validator=" + F(validator) + "/" + validationCalls +
                ",scoring=" + F(scoring);
            Console.WriteLine("live-fixture-run plannerDegree=" + degree + " repeat=" + repeat +
                " attempts=" + attemptsRun + " capturedAttempts=" + inputs.Length +
                " inputDigest=" + finalInput.Digest + " layoutDigest=" + result.Layout.Digest +
                " plannerMs=" + F(plannerMs) + " plannerCpuMs=" + F(cpuMs) +
                " plannerAllocatedBytes=" + allocatedBytes.ToString(CultureInfo.InvariantCulture) +
                " plannerStages=" + stageText);
        }
        return 0;
    }

    private static int ReportResidentHomeSpace(ProceduralLayoutInput input, ProceduralLayout layout)
    {
        var rooms = layout.Rooms.Where(r => r.Kind == LayoutRoomKind.Barracks || r.Kind == LayoutRoomKind.Bedroom).ToArray();
        var interior = rooms.SelectMany(r => r.Interior.Points()).Distinct().ToArray();
        var fixtures = new HashSet<GridPoint>(layout.AllFixtures.SelectMany(f => f.Cells));
        var terrainWalkable = interior.Where(c => input.Terrain.InBounds(c) &&
            input.Terrain[c].Has(TerrainTraits.Standable) &&
            !input.Terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.Occupied)).ToArray();
        var modeledAvailable = ProceduralLayoutValidator.HousingFloorCells(input, layout);
        Console.WriteLine("resident-home-space-model planned_terrain_fixture_only=true required=" + input.Requirements.Residents +
            " rooms=" + rooms.Length + " interior=" + interior.Length + " terrainWalkable=" + terrainWalkable.Length +
            " fixtureBlocked=" + terrainWalkable.Count(fixtures.Contains) + " modeledAvailable=" + modeledAvailable.Count +
            " layout=" + layout.Digest + " terrain=" + input.Terrain.Digest);
        Console.WriteLine("resident-home-space-cells=" + string.Join(";", modeledAvailable.Select(c => c.ToString())));
        return modeledAvailable.Count;
    }

    private static string ProfileDigest(Type fixture)
    {
        object profile = fixture.GetMethod("BuildProfile", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        return (string)profile.GetType().GetProperty("Digest").GetValue(profile, null);
    }

    private static T Constant<T>(Type fixture, string name) =>
        (T)fixture.GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static int ReadInt(string[] args, string key, int fallback)
    {
        int at = Array.IndexOf(args, key);
        return at >= 0 && at + 1 < args.Length ? int.Parse(args[at + 1], CultureInfo.InvariantCulture) : fallback;
    }

    private static string ReadString(string[] args, string key)
    {
        int at = Array.IndexOf(args, key);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(label + " mismatch: expected=" + expected + " actual=" + actual);
    }
}
