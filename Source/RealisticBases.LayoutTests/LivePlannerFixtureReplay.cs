using System;
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
        string expectedInput = Constant<string>(fixture, "ExpectedInputDigest");
        string expectedLayout = Constant<string>(fixture, "ExpectedLayoutDigest");
        string expectedTerrain = Constant<string>(fixture, "ExpectedTerrainDigest");
        string expectedRequirements = Constant<string>(fixture, "ExpectedRequirementsDigest");
        string expectedCatalog = Constant<string>(fixture, "ExpectedCatalogDigest");
        string expectedResources = Constant<string>(fixture, "ExpectedResourcesDigest");
        string expectedProfile = Constant<string>(fixture, "ExpectedProfileDigest");
        string expectedEngine = Constant<string>(fixture, "ExpectedEngineId");

        Equal(expectedProfile, ProfileDigest(fixture), "profile digest");
        if (expectedLayout == "none") throw new InvalidOperationException("The captured live plan has no layout digest.");
        string[] expectedAttemptInputs = (string[])Constant<string[]>(fixture, "ExpectedAttemptInputDigests");
        string[] expectedAttemptLayouts = (string[])Constant<string[]>(fixture, "ExpectedAttemptLayoutDigests");
        if (expectedAttemptInputs.Length == 0 || expectedAttemptInputs.Length != expectedAttemptLayouts.Length)
            throw new InvalidOperationException("Captured input attempts and result digests are incomplete.");

        for (int repeat = 1; repeat <= repeats; repeat++)
        {
            ProceduralLayoutInput[] inputs = (ProceduralLayoutInput[])build.Invoke(null, new object[] { degree });
            if (inputs.Length != expectedAttemptInputs.Length)
                throw new InvalidOperationException("Captured attempt count mismatch: expected=" + expectedAttemptInputs.Length +
                    " actual=" + inputs.Length);
            ProceduralLayoutInput finalInput = inputs[inputs.Length - 1];
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
            for (int attempt = 0; attempt < inputs.Length; attempt++)
            {
                ProceduralLayoutInput input = inputs[attempt];
                Equal(expectedAttemptInputs[attempt], input.Digest, "attempt " + (attempt + 1) + " input digest");
                double attemptCpuBefore = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;
                long attemptAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                Stopwatch watch = Stopwatch.StartNew();
                result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
                watch.Stop();
                plannerMs += watch.Elapsed.TotalMilliseconds;
                cpuMs += Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds - attemptCpuBefore;
                allocatedBytes += GC.GetAllocatedBytesForCurrentThread() - attemptAllocatedBefore;
                string attemptLayout = result.Layout?.Digest ?? "none";
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
            if (result == null || !result.Success || result.Layout == null)
                throw new InvalidOperationException("Captured live planner attempts rejected: " + result?.RejectionCode);
            Equal(expectedInput, finalInput.Digest, "final input digest");
            Equal(expectedLayout, result.Layout.Digest, "layout digest");
            string stageText = "siteCandidate=" + F(site) +
                ",fieldWall=" + F(field) +
                ",fitDefenseAccess=" + F(fit) +
                ",approachDefense=" + F(approach) +
                ",turretPlanning=" + F(turret) +
                ",powerTrim=" + F(power) +
                ",validator=" + F(validator) + "/" + validationCalls +
                ",scoring=" + F(scoring);
            Console.WriteLine("live-fixture-run plannerDegree=" + degree + " repeat=" + repeat +
                " attempts=" + inputs.Length + " inputDigest=" + finalInput.Digest + " layoutDigest=" + result.Layout.Digest +
                " plannerMs=" + F(plannerMs) + " plannerCpuMs=" + F(cpuMs) +
                " plannerAllocatedBytes=" + allocatedBytes.ToString(CultureInfo.InvariantCulture) +
                " plannerStages=" + stageText);
        }
        return 0;
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

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(label + " mismatch: expected=" + expected + " actual=" + actual);
    }
}
