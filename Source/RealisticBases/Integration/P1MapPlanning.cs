using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealisticBases.Planning;
using RimWorld;
using Verse;

namespace RealisticBases.Integration
{
    public sealed class P1PlanningRun
    {
        public P1PlanningRun(TerrainSnapshot snapshot, PlannerInput input, PlanningResult result,
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
        public PlannerInput Input { get; }
        public PlanningResult Result { get; }
        public long CaptureMilliseconds { get; }
        public long PlanningMilliseconds { get; }
        public string Phase { get; }
    }

    public static class P1MapPlanner
    {
        public static readonly SettlementProfile PrimaryProfile =
            new SettlementProfile("industrial-diagnostic", 1, 9, 9, 24, 6400, 6400);

        public static readonly SettlementProfile CompactProfile =
            new SettlementProfile("compact-diagnostic", 1, 5, 5, 6, 6400, 6400);

        public static P1PlanningRun Run(Map map, SettlementProfile profile, string phase)
        {
            Stopwatch captureWatch = Stopwatch.StartNew();
            TerrainSnapshot snapshot = Capture(map);
            captureWatch.Stop();
            ulong seed = SeedDeriver.Derive(Find.World?.info?.seedString ?? string.Empty, map.Tile.tileId, profile);
            PlannerInput input = new PlannerInput(profile, snapshot, seed);
            Stopwatch planningWatch = Stopwatch.StartNew();
            PlanningResult result = SettlementPlanner.Plan(input);
            planningWatch.Stop();
            P1Log.Message("PLAN", "phase=" + phase + " profile=" + profile.Id + " profileVersion=" + profile.Version +
                " plannerVersion=" + P1Versions.Planner + " snapshotVersion=" + snapshot.SchemaVersion +
                " seed=" + StableHash.Hex(seed) + " inputDigest=" + input.Digest +
                " snapshotDigest=" + snapshot.Digest + " success=" + result.Success +
                " candidates=" + result.CandidatesConsidered + "/" + profile.MaxCandidates +
                " limitReached=" + result.LimitReached + " captureMs=" + captureWatch.ElapsedMilliseconds +
                " planningMs=" + planningWatch.ElapsedMilliseconds +
                (result.Success ? " planDigest=" + result.Plan.Digest : " rejected=" + Compact(result.Rejections)));
            return new P1PlanningRun(snapshot, input, result, captureWatch.ElapsedMilliseconds,
                planningWatch.ElapsedMilliseconds, phase);
        }

        public static TerrainSnapshot Capture(Map map)
        {
            TerrainCell[] cells = new TerrainCell[map.Size.x * map.Size.z];
            for (int z = 0; z < map.Size.z; z++)
            {
                for (int x = 0; x < map.Size.x; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    TerrainDef terrain = cell.GetTerrain(map);
                    RoofDef roof = map.roofGrid.RoofAt(cell);
                    Building edifice = cell.GetEdifice(map);
                    TerrainTraits traits = TerrainTraits.None;
                    if (cell.Standable(map)) traits |= TerrainTraits.Standable;
                    if (GenConstruct.CanBuildOnTerrain(ThingDefOf.Wall, cell, map, Rot4.North, null, ThingDefOf.Steel))
                        traits |= TerrainTraits.Buildable;
                    if (roof != null) traits |= TerrainTraits.Roofed;
                    if (roof == RoofDefOf.RoofRockThick) traits |= TerrainTraits.ThickRoof;
                    if (terrain.IsWater) traits |= TerrainTraits.Water;
                    if (edifice?.def?.building?.isNaturalRock == true) traits |= TerrainTraits.NaturalRock;
                    if (terrain.fertility >= 0.7f) traits |= TerrainTraits.Fertile;
                    if (terrain.affordances.Contains(TerrainAffordanceDefOf.Heavy)) traits |= TerrainTraits.SupportsHeavy;
                    if (cell.OnEdge(map)) traits |= TerrainTraits.MapEdge;
                    if (edifice != null) traits |= TerrainTraits.Occupied;
                    cells[z * map.Size.x + x] = new TerrainCell(traits, terrain.shortHash, roof?.shortHash ?? 0);
                }
            }
            string sourceVersion = VersionControl.CurrentVersionStringWithRev + ";generator=" + map.generatorDef?.defName;
            return new TerrainSnapshot(map.Size.x, map.Size.z, cells, sourceVersion);
        }

        internal static string Compact(IEnumerable<string> values)
        {
            string text = string.Join("|", (values ?? Enumerable.Empty<string>()).Take(6).ToArray());
            return string.IsNullOrEmpty(text) ? "none" : text.Replace(' ', '_');
        }
    }

    public sealed class P1DiagnosticRecord
    {
        public P1DiagnosticRecord(P1PlanningRun run, string outcome, IEnumerable<string> repairs)
        {
            Run = run;
            Outcome = outcome;
            Repairs = (repairs ?? Enumerable.Empty<string>()).ToArray();
            string before = run.Snapshot.Digest;
            PlanningResult replay = SettlementPlanner.Plan(run.Input);
            SnapshotUnchanged = before == run.Snapshot.Digest;
            ReplayDigestMatches = run.Result.Success == replay.Success &&
                (!run.Result.Success || run.Result.Plan.Digest == replay.Plan.Digest);
        }

        public P1PlanningRun Run { get; }
        public string Outcome { get; }
        public IReadOnlyList<string> Repairs { get; }
        public bool SnapshotUnchanged { get; }
        public bool ReplayDigestMatches { get; }
    }

    public static class P1PlanDiagnostics
    {
        private static readonly Dictionary<int, P1DiagnosticRecord> Records = new Dictionary<int, P1DiagnosticRecord>();

        public static void Record(Map map, P1PlanningRun run, string outcome, IEnumerable<string> repairs)
        {
            P1DiagnosticRecord record = new P1DiagnosticRecord(run, outcome, repairs);
            Records[map.uniqueID] = record;
            LogReport(map, record);
        }

        public static P1DiagnosticRecord Get(Map map)
        {
            P1DiagnosticRecord record;
            return map != null && Records.TryGetValue(map.uniqueID, out record) ? record : null;
        }

        public static void LogReport(Map map)
        {
            P1DiagnosticRecord record = Get(map);
            if (record == null)
            {
                P1Log.Message("REPORT", "map=" + map?.uniqueID + " unavailable=no_record");
                return;
            }
            LogReport(map, record);
        }

        private static void LogReport(Map map, P1DiagnosticRecord record)
        {
            P1PlanningRun run = record.Run;
            SettlementPlan plan = run.Result.Plan;
            P1Log.Message("REPORT", "map=" + map.uniqueID + " runtime=" + VersionControl.CurrentVersionStringWithRev +
                " planner=" + P1Versions.Planner + " profile=" + run.Input.Profile.Id + "/" + run.Input.Profile.Version +
                " snapshotSchema=" + run.Snapshot.SchemaVersion + " planSchema=" + P1Versions.PlanSchema +
                " seed=" + StableHash.Hex(run.Input.Seed) + " inputDigest=" + run.Input.Digest +
                " outcome=" + record.Outcome + " archetype=" + (plan?.Archetype ?? "none") +
                " capacity=" + (plan?.PlannedCapacity.ToString() ?? "unavailable") +
                " supply=" + (plan?.SupplyStatus ?? "unavailable") +
                " candidates=" + run.Result.CandidatesConsidered + "/" + run.Input.Profile.MaxCandidates +
                " routeNodes=" + (plan?.RouteNodesVisited.ToString() ?? "unavailable") +
                " repairs=" + P1MapPlanner.Compact(record.Repairs) +
                " rejected=" + P1MapPlanner.Compact(run.Result.Rejections) +
                " captureMs=" + run.CaptureMilliseconds + " planningMs=" + run.PlanningMilliseconds +
                " deterministicReplay=" + record.ReplayDigestMatches +
                " snapshotUnchanged=" + record.SnapshotUnchanged +
                " operationalPower=false operationalDefense=false");
        }

        public static void ShowOverlay(Map map)
        {
            P1DiagnosticRecord record = Get(map);
            if (record == null || record.Run.Result.Plan == null)
            {
                map?.debugDrawer.FlashCell(map.Center, 1f, "P1: no accepted plan", 600);
                return;
            }
            TerrainSnapshot snapshot = record.Run.Snapshot;
            SettlementPlan plan = record.Run.Result.Plan;
            foreach (GridPoint point in snapshot.Points())
            {
                TerrainCell cell = snapshot[point];
                bool suitable = cell.Has(TerrainTraits.Standable | TerrainTraits.Buildable) &&
                    !cell.HasAny(TerrainTraits.Water | TerrainTraits.Roofed | TerrainTraits.Occupied);
                map.debugDrawer.FlashCell(ToCell(point), suitable ? 0.08f : 0.92f, null, 600);
            }
            foreach (RoomEnvelope room in plan.Rooms)
            {
                GridPoint center = new GridPoint(room.Bounds.MinX + room.Bounds.Width / 2,
                    room.Bounds.MinZ + room.Bounds.Height / 2);
                map.debugDrawer.FlashCell(ToCell(center), 0.25f, room.Label, 600);
                map.debugDrawer.FlashCell(ToCell(room.InteractionCell), 0.35f, "INTERACT " + room.Id, 600);
            }
            foreach (GridPoint point in plan.Route)
                map.debugDrawer.FlashCell(ToCell(point), 0.45f, "route", 600);
            map.debugDrawer.FlashCell(ToCell(plan.Gate), 0.55f, "GATE", 600);
            foreach (PlanReservation reservation in plan.Reservations)
            {
                string label = null;
                float color = 0.5f;
                if (reservation.Kind == ReservationKind.RoofSupport) { label = "ROOF SUPPORT"; color = 0.65f; }
                if (reservation.Kind == ReservationKind.PowerNetwork) { label = "PWR planned"; color = 0.72f; }
                if (reservation.Kind == ReservationKind.DefenseSector) { label = "DEF planned"; color = 0.82f; }
                if (label != null) map.debugDrawer.FlashCell(ToCell(reservation.Cell), color, label, 600);
            }
            if (record.Run.Result.Rejections.Count > 0)
                map.debugDrawer.FlashCell(new IntVec3(plan.Compound.MinX, 0, plan.Compound.MinZ), 1f,
                    "rejected candidates=" + record.Run.Result.Rejections.Count, 600);
            P1Log.Message("OVERLAY", "map=" + map.uniqueID + " suitableCells=" +
                snapshot.Points().Count(p => snapshot[p].Has(TerrainTraits.Standable | TerrainTraits.Buildable) &&
                    !snapshot[p].HasAny(TerrainTraits.Water | TerrainTraits.Roofed | TerrainTraits.Occupied)) +
                " rooms=" + plan.Rooms.Count + " routeCells=" + plan.Route.Count +
                " gate=1 roofSupport=" + plan.Reservations.Count(r => r.Kind == ReservationKind.RoofSupport) +
                " plannedPower=" + plan.Reservations.Count(r => r.Kind == ReservationKind.PowerNetwork) +
                " plannedDefense=" + plan.Reservations.Count(r => r.Kind == ReservationKind.DefenseSector) +
                " rejectedConstraints=" + record.Run.Result.Rejections.Count);
        }

        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }

    internal static class P1Log
    {
        internal static void Message(string eventName, string details)
        {
            Log.Message("[RealisticBases:P1] event=" + eventName + " tick=" + GenTicks.TicksGame + " " + details);
        }
    }
}
