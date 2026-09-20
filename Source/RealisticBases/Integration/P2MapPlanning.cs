using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealisticBases.Core;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Integration
{
    public sealed class P2PlanningRun
    {
        public P2PlanningRun(TerrainSnapshot snapshot, PhysicalPlanningInput input, PhysicalPlanningResult result,
            long captureMilliseconds, long planningMilliseconds, string phase)
        {
            Snapshot = snapshot;
            Input = input;
            Result = result;
            CaptureMilliseconds = captureMilliseconds;
            PlanningMilliseconds = planningMilliseconds;
            Phase = phase;
        }

        public TerrainSnapshot Snapshot { get; }
        public PhysicalPlanningInput Input { get; }
        public PhysicalPlanningResult Result { get; }
        public long CaptureMilliseconds { get; }
        public long PlanningMilliseconds { get; }
        public string Phase { get; }
    }

    public static class P2MapPlanner
    {
        public const int ProductionCandidateLimit = 6400;

        public static P2PlanningRun Run(Map map, Faction faction, string phase)
        {
            Stopwatch captureWatch = Stopwatch.StartNew();
            TerrainSnapshot snapshot = P1MapPlanner.Capture(map);
            IReadOnlyList<string> localMaterials = DiscoverLocalMaterials(map);
            captureWatch.Stop();
            PhysicalSettlementProfile profile = SelectProfile(faction);
            ulong seed = SeedDeriver.Derive(Find.World?.info?.seedString ?? string.Empty, map.Tile.tileId,
                new SettlementProfile(profile.Id, profile.Version, PhysicalSettlementPlanner.CompoundWidth,
                    PhysicalSettlementPlanner.CompoundHeight, profile.Residents, ProductionCandidateLimit, 10000));
            PhysicalPlanningInput input = new PhysicalPlanningInput(profile, snapshot, seed, localMaterials, ProductionCandidateLimit);
            Stopwatch planningWatch = Stopwatch.StartNew();
            PhysicalPlanningResult result = PhysicalSettlementPlanner.Plan(input);
            planningWatch.Stop();
            P2Log.Message("PLAN", "phase=" + phase + " profile=" + profile.Id + "/" + profile.Version +
                " rules=" + P2Versions.Rules + " seed=" + StableHash.Hex(seed) + " inputDigest=" + input.Digest +
                " snapshotDigest=" + snapshot.Digest + " localMaterials=" + string.Join(",", localMaterials.ToArray()) +
                " success=" + result.Success + " candidates=" + result.CandidatesConsidered + "/" + input.MaxCandidates +
                " limitReached=" + result.LimitReached + " captureMs=" + captureWatch.ElapsedMilliseconds +
                " planningMs=" + planningWatch.ElapsedMilliseconds +
                (result.Success ? " planDigest=" + result.Plan.Digest : " rejected=" + P1MapPlanner.Compact(result.Rejections)));
            return new P2PlanningRun(snapshot, input, result, captureWatch.ElapsedMilliseconds,
                planningWatch.ElapsedMilliseconds, phase);
        }

        public static PhysicalSettlementProfile SelectProfile(Faction faction)
        {
            string forced = RealisticBasesMod.Settings.forceP2ProfileNext;
            if (!string.IsNullOrEmpty(forced))
            {
                RealisticBasesMod.Settings.forceP2ProfileNext = string.Empty;
                return forced == "tribal" ? P2ProfileCatalog.TribalTemperate : P2ProfileCatalog.IndustrialTemperate;
            }
            return faction?.def?.techLevel >= TechLevel.Industrial
                ? P2ProfileCatalog.IndustrialTemperate
                : P2ProfileCatalog.TribalTemperate;
        }

        public static IReadOnlyList<string> DiscoverLocalMaterials(Map map)
        {
            HashSet<ThingDef> rocks = new HashSet<ThingDef>(Find.World.NaturalRockTypesIn(map.Tile));
            List<string> local = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.IsStuff && d.stuffProps?.SourceNaturalRock != null && rocks.Contains(d.stuffProps.SourceNaturalRock))
                .Select(d => d.defName).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
            bool localWood = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                .OfType<Plant>().Any(p => p.def.plant?.harvestedThingDef == ThingDefOf.WoodLog);
            if (localWood) local.Add(ThingDefOf.WoodLog.defName);
            if (local.Count == 0) local.Add(ThingDefOf.WoodLog.defName);
            return local.Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
        }
    }

    public sealed class P2DiagnosticRecord
    {
        public P2DiagnosticRecord(P2PlanningRun run, string outcome, IEnumerable<string> recovery)
        {
            Run = run;
            Outcome = outcome;
            Recovery = (recovery ?? Enumerable.Empty<string>()).ToArray();
            string before = run.Snapshot.Digest;
            PhysicalPlanningResult replay = PhysicalSettlementPlanner.Plan(run.Input);
            SnapshotUnchanged = before == run.Snapshot.Digest;
            ReplayDigestMatches = run.Result.Success == replay.Success &&
                (!run.Result.Success || run.Result.Plan.Digest == replay.Plan.Digest);
        }

        public P2PlanningRun Run { get; }
        public string Outcome { get; }
        public IReadOnlyList<string> Recovery { get; }
        public bool SnapshotUnchanged { get; }
        public bool ReplayDigestMatches { get; }
    }

    public static class P2PlanDiagnostics
    {
        private static readonly Dictionary<int, P2DiagnosticRecord> Records = new Dictionary<int, P2DiagnosticRecord>();

        public static void Record(Map map, P2PlanningRun run, string outcome, IEnumerable<string> recovery)
        {
            P2DiagnosticRecord record = new P2DiagnosticRecord(run, outcome, recovery);
            Records[map.uniqueID] = record;
            LogReport(map, record);
        }

        public static P2DiagnosticRecord Get(Map map)
        {
            P2DiagnosticRecord value;
            return map != null && Records.TryGetValue(map.uniqueID, out value) ? value : null;
        }

        public static void LogReport(Map map)
        {
            P2DiagnosticRecord record = Get(map);
            if (record == null) { P2Log.Message("REPORT", "map=" + map?.uniqueID + " unavailable=no_record"); return; }
            LogReport(map, record);
        }

        private static void LogReport(Map map, P2DiagnosticRecord record)
        {
            PhysicalSettlementProfile profile = record.Run.Input.Profile;
            PhysicalSettlementPlan plan = record.Run.Result.Plan;
            P2Log.Message("REPORT", "map=" + map.uniqueID + " runtime=" + VersionControl.CurrentVersionStringWithRev +
                " profile=" + profile.Id + "/" + profile.Version + " outcome=" + record.Outcome +
                " inputDigest=" + record.Run.Input.Digest + " planDigest=" + (plan?.Digest ?? "none") +
                " residents=" + profile.Residents + " ordinaryBeds=" + (plan?.Placements.Count(p => p.Kind == PhysicalPlacementKind.OrdinaryBed) ?? 0) +
                " fieldCells=" + (plan?.FieldCells.Count ?? 0) + " requiredCropCells=" + profile.RequiredCropCells +
                " startupNutrition=" + profile.StartupNutrition.ToString("F1") + "/" + profile.RequiredStartupNutrition.ToString("F1") +
                " localWallStuff=" + (plan?.LocalWallStuff ?? "none") + " importedPlanned=" + profile.Budget.ImportedValue +
                "/" + profile.Budget.ImportValueLimit + " powerScenarios=" +
                string.Join(",", profile.PowerScenarios.Select(s => s.Id + ":" + s.EndingBatteryWattDays + ":" + s.Passes).ToArray()) +
                " recovery=" + P1MapPlanner.Compact(record.Recovery) + " candidates=" + record.Run.Result.CandidatesConsidered +
                "/" + record.Run.Input.MaxCandidates + " deterministicReplay=" + record.ReplayDigestMatches +
                " snapshotUnchanged=" + record.SnapshotUnchanged + " captureMs=" + record.Run.CaptureMilliseconds +
                " planningMs=" + record.Run.PlanningMilliseconds);
        }

        public static void ShowOverlay(Map map)
        {
            P2DiagnosticRecord record = Get(map);
            PhysicalSettlementPlan plan = record?.Run?.Result?.Plan;
            if (plan == null) { map?.debugDrawer.FlashCell(map.Center, 1f, "P2: no accepted plan", 600); return; }
            foreach (PhysicalFacility facility in plan.Facilities)
            {
                GridPoint center = new GridPoint(facility.Bounds.MinX + facility.Bounds.Width / 2,
                    facility.Bounds.MinZ + facility.Bounds.Height / 2);
                map.debugDrawer.FlashCell(ToCell(center), 0.25f, facility.Id + " cap=" + facility.Capacity, 600);
                foreach (GridPoint cell in facility.InteractionCells)
                    map.debugDrawer.FlashCell(ToCell(cell), 0.4f, "INTERACT " + facility.Id, 600);
            }
            foreach (GridPoint cell in plan.AttackRoute) map.debugDrawer.FlashCell(ToCell(cell), 0.5f, "attack", 600);
            foreach (GridPoint cell in plan.RetreatRoute) map.debugDrawer.FlashCell(ToCell(cell), 0.65f, "retreat", 600);
            foreach (GridPoint cell in plan.Gates) map.debugDrawer.FlashCell(ToCell(cell), 0.75f, "GATE", 600);
            P2Log.Message("OVERLAY", "map=" + map.uniqueID + " facilities=" + plan.Facilities.Count +
                " placements=" + plan.Placements.Count + " field=" + plan.FieldCells.Count +
                " logistics=" + plan.Logistics.Count + " attackRoute=" + plan.AttackRoute.Count +
                " retreatRoute=" + plan.RetreatRoute.Count);
        }

        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }

    internal static class P2Log
    {
        internal static void Message(string eventName, string details) =>
            Log.Message("[RealisticBases:P2] event=" + eventName + " tick=" + GenTicks.TicksGame + " " + details);
        internal static void Error(string eventName, string details) =>
            Log.Error("[RealisticBases:P2] event=" + eventName + " tick=" + GenTicks.TicksGame + " " + details);
    }
}
