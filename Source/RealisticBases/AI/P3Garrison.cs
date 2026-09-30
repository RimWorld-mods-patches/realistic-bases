using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Core;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RealisticBases.AI
{
    public sealed class LordJob_P3Garrison : LordJob
    {
        private int settlementId = -1;

        public LordJob_P3Garrison()
        {
        }

        public LordJob_P3Garrison(int settlementId)
        {
            this.settlementId = settlementId;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            graph.StartingToil = new LordToil_P3Garrison(settlementId);
            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref settlementId, "settlementId", -1);
        }
    }

    public sealed class LordToil_P3Garrison : LordToil
    {
        private readonly int settlementId;

        public LordToil_P3Garrison(int settlementId)
        {
            this.settlementId = settlementId;
        }

        public override IntVec3 FlagLoc => Map?.GetComponent<P3GarrisonController>()?.SupportCenter ?? IntVec3.Invalid;

        public override void UpdateAllDuties()
        {
            Map?.GetComponent<P3GarrisonController>()?.RefreshAndApply(lord, true);
        }

        public override void LordToilTick()
        {
            if ((GenTicks.TicksGame + settlementId) % 120 == 0)
                Map?.GetComponent<P3GarrisonController>()?.RefreshAndApply(lord, false);
        }

        public override void Notify_PawnLost(Pawn victim, PawnLostCondition cond)
        {
            Map?.GetComponent<P3GarrisonController>()?.RefreshAndApply(lord, true);
        }
    }

    public sealed class JobGiver_P3DefendPost : JobGiver_AIDefendPoint
    {
        protected override bool ExtraTargetValidator(Pawn pawn, Thing target)
        {
            P3GarrisonController controller = pawn.Map?.GetComponent<P3GarrisonController>();
            return base.ExtraTargetValidator(pawn, target) && controller != null && controller.TargetAllowed(pawn, target);
        }

        protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
        {
            if (!base.TryFindShootingPosition(pawn, out dest, verbToUse)) return false;
            P3GarrisonController controller = pawn.Map?.GetComponent<P3GarrisonController>();
            return controller != null && controller.RouteAllowed(pawn, dest, 48);
        }

        protected override Job MeleeAttackJob(Pawn pawn, Thing enemyTarget)
        {
            P3GarrisonController controller = pawn.Map?.GetComponent<P3GarrisonController>();
            return controller != null && controller.RouteAllowed(pawn, enemyTarget.Position, 24)
                ? base.MeleeAttackJob(pawn, enemyTarget)
                : null;
        }
    }

    public sealed class JobGiver_P3ManMortar : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => pawn.Map?.GetComponent<P3GarrisonController>()?.TryGiveMortarJob(pawn);
    }

    public sealed class JobGiver_P3Support : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => pawn.Map?.GetComponent<P3GarrisonController>()?.TryGiveSupportJob(pawn);
    }

    public sealed class JobGiver_P3HoldAssignedPost : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => pawn.Map?.GetComponent<P3GarrisonController>()?.TryGiveHoldJob(pawn);
    }

    public sealed class P3GarrisonController : MapComponent
    {
        private Settlement settlement;
        private Faction faction;
        private Thing mortar;
        private List<Pawn> defenders = new List<Pawn>();
        private List<Thing> ownedThings = new List<Thing>();
        private List<Thing> operatingStocks = new List<Thing>();
        private List<string> postIds = new List<string>();
        private List<string> postSectors = new List<string>();
        private List<IntVec3> postCells = new List<IntVec3>();
        private List<IntVec3> postAccessGates = new List<IntVec3>();
        private List<int> postFlags = new List<int>();
        private List<IntVec3> gateCells = new List<IntVec3>();
        private List<IntVec3> perimeterCells = new List<IntVec3>();
        private List<string> perimeterSectors = new List<string>();
        private List<int> perimeterRegions = new List<int>();
        private List<bool> perimeterGates = new List<bool>();
        private List<int> perimeterHitPoints = new List<int>();
        private List<int> perimeterLastHitTicks = new List<int>();
        private List<int> perimeterLastHitDamage = new List<int>();
        private List<int> perimeterLastBreachTicks = new List<int>();
        private List<IntVec3> defendedCells = new List<IntVec3>();
        private List<IntVec3> interiorCells = new List<IntVec3>();
        private IntVec3 supportCenter = IntVec3.Invalid;
        private IntVec3 innerFallback = IntVec3.Invalid;
        private IntVec3 boundsMin = IntVec3.Invalid;
        private int boundsWidth;
        private int boundsHeight;
        private string planDigest;
        private string profileId;
        private int initialHe;
        private int initialIncendiary;
        private int initialBarrels;
        private int routineSupportWorkers = 1;
        private List<int> assignedPawnIds = new List<int>();
        private List<string> assignedPostIds = new List<string>();
        private List<IntVec3> assignedDestinations = new List<IntVec3>();
        private List<int> assignedRoles = new List<int>();
        private List<int> assignedTicks = new List<int>();
        private GarrisonState currentState;
        private string currentSector = "north";
        private int currentThreatSeverity;
        private int lastAuditTick = -1;
        private int reassignmentCount;
        private int invalidPostRecoveries;
        private bool incendiarySafe;
        // Opt-in disposable combat probe; never serialized or enabled in ordinary play.
        private int fieldProbeUntilTick;
        private int fieldProbeShots;
        private readonly HashSet<int> fieldProbeProjectiles = new HashSet<int>();

        public P3GarrisonController(Map map) : base(map)
        {
        }

        public IntVec3 SupportCenter => supportCenter;

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (fieldProbeUntilTick <= GenTicks.TicksGame || GenTicks.TicksGame % 5 != 0) return;
            foreach (Projectile projectile in map.listerThings.AllThings.OfType<Projectile>())
            {
                Pawn launcher = projectile.Launcher as Pawn;
                if (launcher == null || !defenders.Contains(launcher) ||
                    !fieldProbeProjectiles.Add(projectile.thingIDNumber)) continue;
                int postIndex = assignedPawnIds.IndexOf(launcher.thingIDNumber);
                if (postIndex < 0 || postIndex >= assignedPostIds.Count ||
                    !assignedPostIds[postIndex].StartsWith("field-outer-", StringComparison.Ordinal)) continue;
                fieldProbeShots++;
                P3Log.Message("FIELD_ASSAULT_SHOT", "map=" + map.uniqueID + " tick=" + GenTicks.TicksGame +
                    " pawn=" + launcher.thingIDNumber + " post=" + assignedPostIds[postIndex] +
                    " origin=" + launcher.Position + " projectile=" + projectile.def.defName);
            }
            if (GenTicks.TicksGame % 300 == 0) AuditFieldAssault("periodic");
        }

        public void DebugStageFieldAssault()
        {
            List<IntVec3> posts = postIds.Select((id, i) => new { id, i })
                .Where(x => x.id.StartsWith("field-outer-", StringComparison.Ordinal) && x.i < postCells.Count)
                .Select(x => postCells[x.i]).ToList();
            Pawn targetPawn = map.mapPawns.FreeColonistsSpawned.OrderBy(p => p.thingIDNumber).FirstOrDefault();
            if (posts.Count < 2 || targetPawn == null)
            {
                P3Log.Message("FIELD_ASSAULT_STAGE_REJECT", "map=" + map.uniqueID +
                    " posts=" + posts.Count + " playerPawn=" + (targetPawn != null));
                return;
            }
            int centerX = (int)Math.Round(posts.Average(c => c.x));
            int centerZ = (int)Math.Round(posts.Average(c => c.z));
            int dx = centerX - supportCenter.x, dz = centerZ - supportCenter.z;
            IntVec3 outward = Math.Abs(dx) > Math.Abs(dz)
                ? new IntVec3(Math.Sign(dx), 0, 0) : new IntVec3(0, 0, Math.Sign(dz));
            IntVec3 lateral = outward.x != 0 ? new IntVec3(0, 0, 1) : new IntVec3(1, 0, 0);
            IntVec3 destination = IntVec3.Invalid;
            foreach (int distance in new[] { 14, 12, 16, 10, 18 })
            {
                foreach (int offset in new[] { 0, -1, 1, -2, 2 })
                {
                    IntVec3 candidate = new IntVec3(centerX + outward.x * distance + lateral.x * offset,
                        0, centerZ + outward.z * distance + lateral.z * offset);
                    if (!candidate.InBounds(map) || !candidate.Standable(map) || candidate.GetEdifice(map) != null ||
                        !posts.Any(p => p.InHorDistOf(candidate, 35f))) continue;
                    destination = candidate;
                    break;
                }
                if (destination.IsValid) break;
            }
            if (!destination.IsValid)
            {
                P3Log.Message("FIELD_ASSAULT_STAGE_REJECT", "map=" + map.uniqueID + " no_clear_approach");
                return;
            }
            targetPawn.DeSpawn();
            GenSpawn.Spawn(targetPawn, destination, map);
            targetPawn.drafter.Drafted = true;
            targetPawn.jobs.StopAll();
            fieldProbeShots = 0;
            fieldProbeProjectiles.Clear();
            fieldProbeUntilTick = GenTicks.TicksGame + 3600;
            P3Log.Message("FIELD_ASSAULT_STAGE", "map=" + map.uniqueID + " target=" + targetPawn.thingIDNumber +
                " cell=" + destination + " fieldPosts=" + string.Join(",", posts.Select(p => p.ToString()).ToArray()) +
                " untilTick=" + fieldProbeUntilTick);
            AuditFieldAssault("stage");
        }

        public void AuditFieldAssault(string phase)
        {
            Pawn targetPawn = map.mapPawns.FreeColonistsSpawned.OrderBy(p => p.thingIDNumber).FirstOrDefault();
            List<string> postState = new List<string>();
            for (int i = 0; i < postIds.Count && i < postCells.Count; i++)
            {
                if (!postIds[i].StartsWith("field-outer-", StringComparison.Ordinal)) continue;
                Pawn assigned = defenders.FirstOrDefault(p => p != null && p.Spawned &&
                    assignedPawnIds.IndexOf(p.thingIDNumber) is int assignment && assignment >= 0 &&
                    assignment < assignedPostIds.Count && assignedPostIds[assignment] == postIds[i]);
                bool cover = ownedThings.Any(t => t != null && t.Spawned &&
                    (t.def.defName == "Sandbags" || t.def.defName == "Barricade") &&
                    t.Position.AdjacentTo8WayOrInside(postCells[i]));
                postState.Add(postIds[i] + ":cover=" + cover + ":pawn=" +
                    (assigned?.thingIDNumber.ToString() ?? "none") + ":occupied=" +
                    (assigned?.Position.InHorDistOf(postCells[i], 1.5f) ?? false) + ":job=" +
                    (assigned?.CurJobDef?.defName ?? "none"));
            }
            P3Log.Message("FIELD_ASSAULT_AUDIT", "map=" + map.uniqueID + " phase=" + phase +
                " tick=" + GenTicks.TicksGame + " shotsObserved=" + fieldProbeShots +
                " target=" + (targetPawn?.thingIDNumber.ToString() ?? "none") +
                " targetHealth=" + (targetPawn?.health?.summaryHealth?.SummaryHealthPercent.ToString("F3") ?? "none") +
                " posts=" + string.Join(";", postState.ToArray()));
        }

        public void Initialize(Settlement parent, PhysicalSettlementPlan settlementPlan, EncounterPlan plan, IEnumerable<Pawn> residents,
            IEnumerable<Thing> allOwnedThings, Thing mortarThing, IEnumerable<Thing> combatStocks)
        {
            Initialize(parent, settlementPlan.Gates, plan, residents, allOwnedThings, mortarThing, combatStocks);
        }

        public void Initialize(Settlement parent, IEnumerable<GridPoint> gates, EncounterPlan plan, IEnumerable<Pawn> residents,
            IEnumerable<Thing> allOwnedThings, Thing mortarThing, IEnumerable<Thing> combatStocks,
            IEnumerable<P3PerimeterSegment> perimeter = null, IEnumerable<GridPoint> interior = null)
        {
            settlement = parent;
            faction = parent.Faction;
            defenders = residents.OrderBy(p => p.thingIDNumber).ToList();
            ownedThings = allOwnedThings.Distinct().ToList();
            mortar = mortarThing;
            operatingStocks = combatStocks.Distinct().ToList();
            planDigest = plan.Digest;
            profileId = plan.Profile.Id;
            supportCenter = ToCell(plan.SupportCenter);
            innerFallback = ToCell(plan.InnerFallback);
            boundsMin = ToCell(new GridPoint(plan.DefendedBounds.MinX, plan.DefendedBounds.MinZ));
            boundsWidth = plan.DefendedBounds.Width;
            boundsHeight = plan.DefendedBounds.Height;
            defendedCells = plan.DefendedCells.Select(ToCell).Distinct().ToList();
            interiorCells = (interior ?? plan.DefendedCells).Select(ToCell).Distinct().ToList();
            postIds = plan.Posts.Select(p => p.Id).ToList();
            // Keep the existing string-list save field. Older single-sector
            // records decode identically; corner posts can serve two fronts.
            postSectors = plan.Posts.Select(p => string.Join("|", new[] { p.SectorId }
                .Concat(p.SupportedSectors.Where(s => s != p.SectorId)))).ToList();
            postCells = plan.Posts.Select(p => ToCell(p.Cell)).ToList();
            postAccessGates = plan.Posts.Select(p => p.AccessGate.HasValue
                ? ToCell(p.AccessGate.Value) : IntVec3.Invalid).ToList();
            postFlags = plan.Posts.Select(P3PostFlags.Encode).ToList();
            gateCells = gates.Select(ToCell).ToList();
            List<P3PerimeterSegment> segments = (perimeter ?? Enumerable.Empty<P3PerimeterSegment>())
                .GroupBy(s => s.Cell).Select(g => g.First()).OrderBy(s => s.Cell).ToList();
            perimeterCells = segments.Select(s => ToCell(s.Cell)).ToList();
            perimeterSectors = segments.Select(s => s.Sector).ToList();
            perimeterRegions = segments.Select(s => (int)s.Region).ToList();
            perimeterGates = segments.Select(s => s.Gate).ToList();
            perimeterHitPoints = perimeterCells.Select(c => c.GetEdifice(map)?.HitPoints ?? 0).ToList();
            perimeterLastHitTicks = Enumerable.Repeat(-1, segments.Count).ToList();
            perimeterLastHitDamage = Enumerable.Repeat(0, segments.Count).ToList();
            perimeterLastBreachTicks = Enumerable.Repeat(-1, segments.Count).ToList();
            initialHe = CountStock("Shell_HighExplosive");
            initialIncendiary = CountStock("Shell_Incendiary");
            initialBarrels = CountStock("ReinforcedBarrel") + (mortar?.TryGetComp<CompRefuelable>()?.HasFuel == true ? 1 : 0);
            routineSupportWorkers = plan.Profile.SupportWorkers;
            P3Log.Message("CONTROLLER_INIT", "map=" + map.uniqueID + " settlement=" + parent.ID +
                " plan=" + planDigest + " profile=" + profileId + " defenders=" + defenders.Count +
                " posts=" + postIds.Count + " mortar=" + (mortar?.thingIDNumber.ToString() ?? "none") +
                " operatingStocks=" + operatingStocks.Count + " he=" + initialHe +
                " incendiary=" + initialIncendiary + " barrels=" + initialBarrels);
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref settlement, "p3Settlement");
            Scribe_References.Look(ref faction, "p3Faction");
            Scribe_References.Look(ref mortar, "p3Mortar");
            Scribe_Collections.Look(ref defenders, "p3Defenders", LookMode.Reference);
            Scribe_Collections.Look(ref ownedThings, "p3OwnedThings", LookMode.Reference);
            Scribe_Collections.Look(ref operatingStocks, "p3OperatingStocks", LookMode.Reference);
            Scribe_Collections.Look(ref postIds, "p3PostIds", LookMode.Value);
            Scribe_Collections.Look(ref postSectors, "p3PostSectors", LookMode.Value);
            Scribe_Collections.Look(ref postCells, "p3PostCells", LookMode.Value);
            Scribe_Collections.Look(ref postAccessGates, "p3PostAccessGates", LookMode.Value);
            Scribe_Collections.Look(ref postFlags, "p3PostFlags", LookMode.Value);
            Scribe_Collections.Look(ref gateCells, "p3GateCells", LookMode.Value);
            Scribe_Collections.Look(ref perimeterCells, "p3PerimeterCells", LookMode.Value);
            Scribe_Collections.Look(ref perimeterSectors, "p3PerimeterSectors", LookMode.Value);
            Scribe_Collections.Look(ref perimeterRegions, "p3PerimeterRegions", LookMode.Value);
            Scribe_Collections.Look(ref perimeterGates, "p3PerimeterGates", LookMode.Value);
            Scribe_Collections.Look(ref perimeterHitPoints, "p3PerimeterHitPoints", LookMode.Value);
            Scribe_Collections.Look(ref perimeterLastHitTicks, "p3PerimeterLastHitTicks", LookMode.Value);
            Scribe_Collections.Look(ref perimeterLastHitDamage, "p3PerimeterLastHitDamage", LookMode.Value);
            Scribe_Collections.Look(ref perimeterLastBreachTicks, "p3PerimeterLastBreachTicks", LookMode.Value);
            Scribe_Collections.Look(ref defendedCells, "p3DefendedCells", LookMode.Value);
            Scribe_Collections.Look(ref interiorCells, "p3InteriorCells", LookMode.Value);
            Scribe_Values.Look(ref supportCenter, "p3SupportCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref innerFallback, "p3InnerFallback", IntVec3.Invalid);
            Scribe_Values.Look(ref boundsMin, "p3BoundsMin", IntVec3.Invalid);
            Scribe_Values.Look(ref boundsWidth, "p3BoundsWidth", 0);
            Scribe_Values.Look(ref boundsHeight, "p3BoundsHeight", 0);
            Scribe_Values.Look(ref planDigest, "p3PlanDigest");
            Scribe_Values.Look(ref profileId, "p3ProfileId");
            Scribe_Values.Look(ref initialHe, "p3InitialHe", 0);
            Scribe_Values.Look(ref initialIncendiary, "p3InitialIncendiary", 0);
            Scribe_Values.Look(ref initialBarrels, "p3InitialBarrels", 0);
            Scribe_Values.Look(ref routineSupportWorkers, "p3RoutineSupportWorkers", 1);
            Scribe_Collections.Look(ref assignedPawnIds, "p3AssignedPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref assignedPostIds, "p3AssignedPostIds", LookMode.Value);
            Scribe_Collections.Look(ref assignedDestinations, "p3AssignedDestinations", LookMode.Value);
            Scribe_Collections.Look(ref assignedRoles, "p3AssignedRoles", LookMode.Value);
            Scribe_Collections.Look(ref assignedTicks, "p3AssignedTicks", LookMode.Value);
            Scribe_Values.Look(ref currentState, "p3CurrentState", GarrisonState.Routine);
            Scribe_Values.Look(ref currentSector, "p3CurrentSector", "north");
            Scribe_Values.Look(ref lastAuditTick, "p3LastAuditTick", -1);
            Scribe_Values.Look(ref reassignmentCount, "p3Reassignments", 0);
            Scribe_Values.Look(ref invalidPostRecoveries, "p3InvalidPostRecoveries", 0);
            Scribe_Values.Look(ref incendiarySafe, "p3IncendiarySafe", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                defenders = defenders?.Where(p => p != null).ToList() ?? new List<Pawn>();
                ownedThings = ownedThings?.Where(t => t != null).ToList() ?? new List<Thing>();
                operatingStocks = operatingStocks?.Where(t => t != null).ToList() ?? new List<Thing>();
                defendedCells = defendedCells?.Distinct().ToList() ?? new List<IntVec3>();
                interiorCells = interiorCells?.Distinct().ToList() ?? defendedCells.ToList();
                if (perimeterCells == null) perimeterCells = new List<IntVec3>();
                int perimeterCount = perimeterCells.Count;
                if (perimeterSectors?.Count != perimeterCount || perimeterRegions?.Count != perimeterCount ||
                    perimeterGates?.Count != perimeterCount || perimeterHitPoints?.Count != perimeterCount ||
                    perimeterLastHitTicks?.Count != perimeterCount || perimeterLastHitDamage?.Count != perimeterCount ||
                    perimeterLastBreachTicks?.Count != perimeterCount)
                {
                    // Older saves did not persist a perimeter. They retain the
                    // gate-only response until a newly generated plan supplies it.
                    perimeterCells.Clear();
                    perimeterSectors = new List<string>(); perimeterRegions = new List<int>();
                    perimeterGates = new List<bool>(); perimeterHitPoints = new List<int>();
                    perimeterLastHitTicks = new List<int>(); perimeterLastHitDamage = new List<int>();
                    perimeterLastBreachTicks = new List<int>();
                }
                if (postAccessGates == null || postAccessGates.Count != postCells.Count)
                    postAccessGates = Enumerable.Repeat(IntVec3.Invalid, postCells.Count).ToList();
                if (assignedDestinations == null || assignedDestinations.Count != assignedPawnIds.Count)
                    assignedDestinations = assignedPostIds.Select(id =>
                    {
                        int index = postIds.IndexOf(id);
                        return index >= 0 && index < postCells.Count ? postCells[index] : supportCenter;
                    }).ToList();
                P3Log.Message("CONTROLLER_RELOADED", "map=" + map.uniqueID + " state=" + currentState +
                    " defenders=" + defenders.Count + " assignments=" + assignedPawnIds.Count +
                    " mortarLoaded=" + MortarLoadedShell() + " heRemaining=" + CountStock("Shell_HighExplosive"));
            }
        }

        public void RefreshAndApply(Lord lord, bool force)
        {
            if (lord == null || faction == null || defenders.Count == 0) return;
            EnforceDefendedBounds(lord);
            List<Pawn> active = lord.ownedPawns.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed)
                .OrderBy(p => p.thingIDNumber).ToList();
            if (active.Count == 0) return;
            bool threat = map.mapPawns.AllPawnsSpawned.Any(p => p.Faction != null && p.HostileTo(faction) && !p.Dead);
            string sector = ObserveThreatSector(threat);
            bool breached = perimeterCells.Count > 0
                ? perimeterCells.Where((c, i) => perimeterHitPoints[i] > 0 || perimeterLastBreachTicks[i] >= 0)
                    .Any(c => c.GetEdifice(map) == null)
                : gateCells.Any(c => c.IsValid && !(c.GetEdifice(map) is Building_Door));
            List<DefensivePost> posts = RuntimePosts(active[0]);
            bool outerViable = posts.Count(p => p.IsValid && !p.Inner && !p.Mortar) >= 2;
            bool safeNeeds = !map.mapPawns.AllPawnsSpawned.Any(p => p.Faction != null && p.HostileTo(faction) &&
                p.Position.InHorDistOf(supportCenter, 12f));
            List<DefenderCandidate> candidates = active.Select(ToCandidate).ToList();
            List<GridPoint> hostileCells = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null &&
                p.HostileTo(faction) && !p.Dead && !p.Downed)
                .Select(p => new GridPoint(p.Position.x, p.Position.z)).ToList();
            HashSet<GridPoint> opaqueCells = RuntimeOpaqueCells();
            List<string> safeTransferPosts = posts.Where(p => p.IsValid && !p.Inner && !p.Mortar &&
                ResponseRouteAllowed(active[0], supportCenter, ToCell(p.Cell), opaqueCells).Allowed)
                .Select(p => p.Id).ToList();
            GridPoint reserveStage = ChooseReserveStage(active[0], sector, opaqueCells);
            GarrisonPlan plan = GarrisonDoctrine.Assign(new GarrisonSituation(GenTicks.TicksGame, candidates, posts,
                threat, sector, breached, outerViable, safeNeeds,
                new GridPoint(supportCenter.x, supportCenter.z), new GridPoint(innerFallback.x, innerFallback.z),
                routineSupportWorkers, hostileCells, opaqueCells, safeTransferPosts, currentThreatSeverity,
                reserveStage));
            UpdateIncendiaryGate(lord, threat);
            bool changed = force || plan.State != currentState || plan.ActiveSector != currentSector || AssignmentsChanged(plan);
            currentState = plan.State;
            currentSector = plan.ActiveSector;
            if (changed)
            {
                ApplyAssignments(lord, plan);
                P3Log.Message("DUTIES", "map=" + map.uniqueID + " lord=" + lord.loadID + " state=" + plan.State +
                    " sector=" + plan.ActiveSector + " threat=" + threat + " breached=" + breached +
                    " outerViable=" + outerViable + " assignments=" + string.Join(",", plan.Assignments.Select(a =>
                        a.DefenderId + ":" + a.Role + ":" + (a.PostId.Length == 0 ? "none" : a.PostId)).ToArray()));
            }
            if (force || lastAuditTick < 0 || GenTicks.TicksGame - lastAuditTick >= 600) Audit("periodic");
        }

        public bool TargetAllowed(Pawn pawn, Thing target)
        {
            IntVec3 post = AssignedPost(pawn);
            return target != null && post.IsValid && target.Position.InHorDistOf(post, 48f);
        }

        public bool RouteAllowed(Pawn pawn, IntVec3 destination, int maxCells)
        {
            if (pawn == null || !destination.InBounds(map) || !InsideBounds(pawn.Position) || !InsideBounds(destination)) return false;
            using (PawnPath path = map.pathFinder.FindPathNow(pawn.Position, destination, TraverseParms.For(pawn), null, PathEndMode.OnCell))
                return path.Found && path.NodesReversed.Count <= maxCells && path.NodesReversed.All(InsideBounds);
        }

        public Job TryGiveMortarJob(Pawn pawn)
        {
            if (mortar == null || mortar.Destroyed || !(mortar is Building_TurretGun turret) || AssignedRole(pawn) != GarrisonRole.MortarOperator)
                return null;
            if (!pawn.CanReserve(turret) || JobDriver_ManTurret.FindAmmoForTurret(pawn, turret) == null) return null;
            CompRefuelable fuel = turret.TryGetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel && !Find.Storyteller.difficulty.classicMortars &&
                JobDriver_ManTurret.FindFuelForTurret(pawn, turret) == null) return null;
            Job job = JobMaker.MakeJob(JobDefOf.ManTurret, turret);
            job.expiryInterval = 1200;
            job.checkOverrideOnExpire = true;
            return job;
        }

        public Job TryGiveSupportJob(Pawn pawn)
        {
            if (AssignedRole(pawn) != GarrisonRole.Support) return null;
            WorkGiver_RescueDowned rescue = DefDatabase<WorkGiverDef>.GetNamedSilentFail("RescueDowned")?.Worker as WorkGiver_RescueDowned;
            WorkGiver_Tend tend = DefDatabase<WorkGiverDef>.GetNamedSilentFail("DoctorTendToHumanlikes")?.Worker as WorkGiver_Tend;
            foreach (Pawn patient in defenders.Where(p => p != null && p.Spawned && p != pawn).OrderBy(p => p.thingIDNumber))
            {
                if (patient.Downed && rescue != null && rescue.HasJobOnThing(pawn, patient)) return rescue.JobOnThing(pawn, patient);
                if (tend != null && tend.HasJobOnThing(pawn, patient)) return tend.JobOnThing(pawn, patient);
            }
            WorkGiver_Refuel refuel = DefDatabase<WorkGiverDef>.GetNamedSilentFail("Refuel")?.Worker as WorkGiver_Refuel;
            foreach (Thing thing in ownedThings.Where(t => t != null && t.Spawned && t.TryGetComp<CompRefuelable>() != null)
                .OrderBy(t => t.thingIDNumber))
                if (refuel != null && refuel.HasJobOnThing(pawn, thing)) return refuel.JobOnThing(pawn, thing);
            WorkGiver_HaulGeneral haul = DefDatabase<WorkGiverDef>.GetNamedSilentFail("HaulGeneral")?.Worker as WorkGiver_HaulGeneral;
            foreach (Thing thing in operatingStocks.Where(t => t != null && t.Spawned).OrderBy(t => t.thingIDNumber))
            {
                Job job = haul?.JobOnThing(pawn, thing);
                if (job != null && RouteAllowed(pawn, job.targetB.Cell.IsValid ? job.targetB.Cell : thing.Position, 64)) return job;
            }
            return null;
        }

        public Job TryGiveHoldJob(Pawn pawn)
        {
            IntVec3 destination = AssignedPost(pawn);
            GarrisonRole role = AssignedRole(pawn);
            if (role == GarrisonRole.MortarOperator && mortar != null && !mortar.Destroyed)
                destination = mortar.InteractionCell;
            else if (!destination.IsValid)
                destination = AssignedDestination(pawn);
            if (!destination.IsValid || !InsideBounds(destination) || !destination.Standable(map)) return null;
            if (!pawn.Position.InHorDistOf(destination, 1.5f))
            {
                IntVec3 next = destination;
                if (!ResponseRouteAllowed(pawn, pawn.Position, destination, RuntimeOpaqueCells()).Allowed)
                {
                    IntVec3 gate = AssignedAccessGate(pawn);
                    if (!gate.IsValid || pawn.Position.InHorDistOf(gate, 1f) ||
                        !ResponseRouteAllowed(pawn, pawn.Position, gate, RuntimeOpaqueCells()).Allowed ||
                        !ResponseRouteAllowed(pawn, gate, destination, RuntimeOpaqueCells()).Allowed) return null;
                    next = gate;
                }
                Job go = JobMaker.MakeJob(JobDefOf.Goto, next);
                go.expiryInterval = 600;
                go.checkOverrideOnExpire = true;
                return go;
            }
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = 180;
            wait.checkOverrideOnExpire = true;
            return wait;
        }

        public void Audit(string phase)
        {
            lastAuditTick = GenTicks.TicksGame;
            int occupied = 0;
            int outside = 0;
            List<string> outsideDetails = new List<string>();
            foreach (Pawn pawn in defenders.Where(p => p != null && p.Spawned && !p.Dead))
            {
                IntVec3 post = AssignedPost(pawn);
                if (post.IsValid && pawn.Position.InHorDistOf(post, 1.5f)) occupied++;
                if (!InsideBounds(pawn.Position))
                {
                    outside++;
                    outsideDetails.Add(pawn.thingIDNumber + ":" + AssignedRole(pawn) + ":" + pawn.Position + ":" +
                        (pawn.CurJobDef?.defName ?? "no-job"));
                }
            }
            Building_TurretGun turret = mortar as Building_TurretGun;
            CompMannable mannable = turret?.TryGetComp<CompMannable>();
            CompChangeableProjectile projectile = turret?.gun?.TryGetComp<CompChangeableProjectile>();
            int he = CountStock("Shell_HighExplosive") + (projectile?.LoadedShell?.defName == "Shell_HighExplosive" ? projectile.loadedCount : 0);
            int incendiary = CountStock("Shell_Incendiary") + (projectile?.LoadedShell?.defName == "Shell_Incendiary" ? projectile.loadedCount : 0);
            int barrels = CountStock("ReinforcedBarrel") + (turret?.TryGetComp<CompRefuelable>()?.HasFuel == true ? 1 : 0);
            CompPowerTrader generatorPower = ownedThings.FirstOrDefault(t => t != null && !t.Destroyed &&
                t.def.defName == "WoodFiredGenerator")?.TryGetComp<CompPowerTrader>();
            float netGainWatts = generatorPower?.PowerNet == null ? 0f :
                generatorPower.PowerNet.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
            P3Log.Message("AUDIT", "map=" + map.uniqueID + " phase=" + phase + " plan=" + planDigest +
                " profile=" + profileId + " state=" + currentState + " sector=" + currentSector + " defendersAlive=" +
                defenders.Count(p => p != null && !p.Dead) + " assigned=" + assignedPawnIds.Count +
                " occupiedPosts=" + occupied + " outsideBounds=" + outside + " outsideDetails=" +
                (outsideDetails.Count == 0 ? "none" : string.Join(",", outsideDetails.ToArray())) + " mortarPresent=" +
                (turret != null && turret.Spawned) + " mortarManned=" + (mannable?.MannedNow ?? false) +
                " mortarLoaded=" + MortarLoadedShell() + " he=" + he + "/" + initialHe +
                " incendiary=" + incendiary + "/" + initialIncendiary + " barrels=" + barrels +
                "/" + initialBarrels + " physicalConsumed=" + ((initialHe - he) + (initialIncendiary - incendiary)) +
                " reassignments=" + reassignmentCount + " invalidPostRecoveries=" + invalidPostRecoveries +
                " incendiarySafe=" + incendiarySafe + " bounds=" + boundsMin + "+" + boundsWidth + "x" + boundsHeight +
                " netGainWatts=" + netGainWatts.ToString("F1") + " addedMortarContinuousWatts=0" +
                " playerPawnsControlled=" + defenders.Count(p => p?.Faction == Faction.OfPlayer));
        }

        public void DebugBreachGate()
        {
            Building_Door door = gateCells.Select(c => c.IsValid ? c.GetEdifice(map) as Building_Door : null)
                .FirstOrDefault(d => d != null && !d.Destroyed);
            if (door == null) return;
            IntVec3 cell = door.Position;
            door.Destroy(DestroyMode.KillFinalize);
            P3Log.Message("SCENARIO_GATE_BREACH", "map=" + map.uniqueID + " cell=" + cell);
        }

        public void DebugInvalidateOuterPosts()
        {
            List<IntVec3> outer = postCells.Where((c, i) => i < postFlags.Count && (postFlags[i] & 3) == 0).ToList();
            List<Thing> cover = ownedThings.Where(t => t != null && t.Spawned &&
                (t.def.defName == "Sandbags" || t.def.defName == "Barricade") &&
                outer.Any(c => t.Position.AdjacentTo8WayOrInside(c))).ToList();
            foreach (Thing thing in cover) thing.Destroy(DestroyMode.KillFinalize);
            P3Log.Message("SCENARIO_INVALIDATE_OUTER", "map=" + map.uniqueID + " coverDestroyed=" + cover.Count);
        }

        public void DebugExhaustMortarAmmunition()
        {
            int destroyed = 0;
            foreach (Thing stock in operatingStocks.Where(t => t != null && !t.Destroyed &&
                (t.def.defName == "Shell_HighExplosive" || t.def.defName == "Shell_Incendiary")).ToList())
            {
                destroyed += stock.stackCount;
                stock.Destroy(DestroyMode.Vanish);
            }
            CompChangeableProjectile projectile = (mortar as Building_TurretGun)?.gun?.TryGetComp<CompChangeableProjectile>();
            int unloaded = projectile?.loadedCount ?? 0;
            projectile?.LoadShell(null, 0);
            P3Log.Message("SCENARIO_AMMO_EXHAUSTED", "map=" + map.uniqueID + " looseDestroyed=" + destroyed +
                " loadedRemoved=" + unloaded);
        }

        public void DebugRemoveMortarOperator()
        {
            Pawn pawn = defenders.FirstOrDefault(p => p != null && !p.Dead && AssignedRole(p) == GarrisonRole.MortarOperator);
            if (pawn == null) return;
            int id = pawn.thingIDNumber;
            pawn.Kill(null);
            P3Log.Message("SCENARIO_OPERATOR_REMOVED", "map=" + map.uniqueID + " pawn=" + id);
        }

        public void DebugCutPower()
        {
            Thing generator = ownedThings.FirstOrDefault(t => t != null && !t.Destroyed && t.def.defName == "WoodFiredGenerator");
            if (generator == null) return;
            int id = generator.thingIDNumber;
            generator.Destroy(DestroyMode.KillFinalize);
            P3Log.Message("SCENARIO_POWER_CUT", "map=" + map.uniqueID + " generator=" + id + " destroyed=True");
        }

        public void DebugDrainNeeds()
        {
            Pawn pawn = defenders.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed &&
                AssignedRole(p) != GarrisonRole.MortarOperator).OrderByDescending(p => p.thingIDNumber).FirstOrDefault();
            if (pawn == null) return;
            if (pawn.needs?.food != null) pawn.needs.food.CurLevelPercentage = 0.1f;
            if (pawn.needs?.rest != null) pawn.needs.rest.CurLevelPercentage = 0.1f;
            P3Log.Message("SCENARIO_NEEDS_DRAINED", "map=" + map.uniqueID + " pawn=" + pawn.thingIDNumber);
        }

        private void EnforceDefendedBounds(Lord lord)
        {
            foreach (Pawn pawn in lord.ownedPawns.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed &&
                !InsideBounds(p.Position)).OrderBy(p => p.thingIDNumber))
            {
                IntVec3 destination = AssignedPost(pawn);
                if (!destination.IsValid || !InsideBounds(destination) || !destination.Standable(map))
                    destination = innerFallback;
                if (!destination.IsValid || !InsideBounds(destination) || !destination.Standable(map))
                    destination = supportCenter;
                if (!destination.IsValid || !InsideBounds(destination) || !destination.Standable(map)) continue;
                Job recovery = JobMaker.MakeJob(JobDefOf.Goto, destination);
                recovery.expiryInterval = 600;
                recovery.checkOverrideOnExpire = true;
                pawn.jobs.StartJob(recovery, JobCondition.InterruptForced);
                P3Log.Message("BOUNDS_RECOVERY", "map=" + map.uniqueID + " pawn=" + pawn.thingIDNumber +
                    " role=" + AssignedRole(pawn) + " from=" + pawn.Position + " to=" + destination);
            }
        }

        private DefenderCandidate ToCandidate(Pawn pawn)
        {
            ThingWithComps weapon = pawn.equipment?.Primary;
            Verb verb = pawn.TryGetAttackVerb(null, !pawn.IsColonist);
            float range = verb?.EffectiveRange ?? 0f;
            DefenderWeaponClass weaponClass = verb == null || verb.verbProps.IsMeleeAttack
                ? DefenderWeaponClass.Melee : range >= 24f ? DefenderWeaponClass.LongRanged : DefenderWeaponClass.ShortRanged;
            int index = assignedPawnIds.IndexOf(pawn.thingIDNumber);
            string previousPost = index >= 0 && index < assignedPostIds.Count ? assignedPostIds[index] : null;
            GarrisonRole? previousRole = index >= 0 && index < assignedRoles.Count ? (GarrisonRole?)assignedRoles[index] : null;
            int previousTick = index >= 0 && index < assignedTicks.Count ? assignedTicks[index] : int.MinValue;
            bool food = pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < 0.25f;
            bool rest = pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < 0.2f;
            bool tend = pawn.health?.HasHediffsNeedingTend() ?? false;
            return new DefenderCandidate(pawn.thingIDNumber, weaponClass, (int)Math.Round(range),
                mortar != null && weaponClass != DefenderWeaponClass.Melee, true, food, rest, tend,
                previousPost, previousRole, previousTick, new GridPoint(pawn.Position.x, pawn.Position.z));
        }

        private List<DefensivePost> RuntimePosts(Pawn reachPawn)
        {
            List<DefensivePost> result = new List<DefensivePost>();
            for (int i = 0; i < postIds.Count && i < postCells.Count && i < postFlags.Count; i++)
            {
                bool mortarPost = P3PostFlags.IsMortar(postFlags[i]);
                bool inner = P3PostFlags.IsInner(postFlags[i]);
                bool melee = P3PostFlags.IsMelee(postFlags[i]);
                IntVec3 cell = mortarPost && mortar != null ? mortar.InteractionCell : postCells[i];
                bool cover = mortarPost || melee || ownedThings.Any(t => t != null && t.Spawned &&
                    (t.def.defName == "Sandbags" || t.def.defName == "Barricade") && t.Position.AdjacentTo8WayOrInside(cell));
                bool usable = cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null &&
                    cover && (reachPawn == null || map.reachability.CanReach(reachPawn.Position, cell, PathEndMode.OnCell, TraverseParms.For(reachPawn)));
                if (!usable && PreviousPostAssigned(postIds[i])) invalidPostRecoveries++;
                string[] sectors = postSectors[i].Split('|');
                result.Add(new DefensivePost(postIds[i], sectors[0], new GridPoint(cell.x, cell.z),
                    P3PostFlags.MinimumRange(postFlags[i]), P3PostFlags.MaximumRange(postFlags[i]),
                    cover, true, usable && InsideBounds(cell),
                    mortarPost, inner, usable, sectors));
            }
            return result;
        }

        private void ApplyAssignments(Lord lord, GarrisonPlan plan)
        {
            Dictionary<int, Tuple<string, int, int>> previous = assignedPawnIds.Select((id, i) => new { id, i })
                .ToDictionary(x => x.id, x => Tuple.Create(
                    x.i < assignedPostIds.Count ? assignedPostIds[x.i] : string.Empty,
                    x.i < assignedRoles.Count ? assignedRoles[x.i] : -1,
                    x.i < assignedTicks.Count ? assignedTicks[x.i] : GenTicks.TicksGame));
            assignedPawnIds.Clear();
            assignedPostIds.Clear();
            assignedDestinations.Clear();
            assignedRoles.Clear();
            assignedTicks.Clear();
            foreach (GarrisonAssignment assignment in plan.Assignments)
            {
                Pawn pawn = lord.ownedPawns.FirstOrDefault(p => p.thingIDNumber == assignment.DefenderId);
                if (pawn == null) continue;
                assignedPawnIds.Add(assignment.DefenderId);
                assignedPostIds.Add(assignment.PostId);
                assignedDestinations.Add(ToCell(assignment.Destination));
                assignedRoles.Add((int)assignment.Role);
                Tuple<string, int, int> old;
                bool retained = previous.TryGetValue(assignment.DefenderId, out old) &&
                    old.Item1 == assignment.PostId && old.Item2 == (int)assignment.Role;
                assignedTicks.Add(retained ? old.Item3 : assignment.AssignedTick);
                if (old != null && old.Item1 != assignment.PostId) reassignmentCount++;
                DutyDef duty = DutyFor(assignment.Role);
                pawn.mindState.duty = new PawnDuty(duty, ToCell(assignment.Destination),
                    assignment.Role == GarrisonRole.RangedPost ? 7f : 4f);
            }
        }

        private void UpdateIncendiaryGate(Lord lord, bool threatPresent)
        {
            List<Thing> incendiary = operatingStocks.Where(t => t != null && !t.Destroyed &&
                t.def.defName == "Shell_Incendiary").ToList();
            bool safe = threatPresent && IncendiarySafe();
            incendiarySafe = safe;
            foreach (Thing stock in incendiary)
            {
                bool forbidden = lord.extraForbiddenThings.Contains(stock);
                if (!safe && !forbidden) lord.extraForbiddenThings.Add(stock);
                else if (safe && forbidden) lord.extraForbiddenThings.Remove(stock);
            }
        }

        private bool IncendiarySafe()
        {
            List<Pawn> threats = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && faction != null &&
                p.HostileTo(faction) && !p.Dead).ToList();
            if (threats.Count == 0) return false;
            foreach (Pawn threat in threats)
            {
                if (defenders.Any(p => p != null && p.Spawned && p.Position.InHorDistOf(threat.Position, 15f))) return false;
                if (ownedThings.OfType<Building>().Any(b => b != null && b.Spawned && b.Position.InHorDistOf(threat.Position, 15f)))
                    return false;
            }
            return true;
        }

        private static DutyDef DutyFor(GarrisonRole role)
        {
            if (role == GarrisonRole.MortarOperator) return RBDefOf.RB_P3_ManMortar;
            if (role == GarrisonRole.Support) return RBDefOf.RB_P3_Support;
            if (role == GarrisonRole.NeedsRelief) return RBDefOf.RB_P3_Relief;
            if (role == GarrisonRole.InnerFallback) return RBDefOf.RB_P3_InnerFallback;
            return RBDefOf.RB_P3_DefendPost;
        }

        private bool AssignmentsChanged(GarrisonPlan plan)
        {
            if (plan.Assignments.Count != assignedPawnIds.Count) return true;
            foreach (GarrisonAssignment assignment in plan.Assignments)
            {
                int index = assignedPawnIds.IndexOf(assignment.DefenderId);
                if (index < 0 || index >= assignedPostIds.Count || index >= assignedRoles.Count ||
                    assignedPostIds[index] != assignment.PostId || assignedRoles[index] != (int)assignment.Role) return true;
            }
            return false;
        }

        private string ThreatSector()
        {
            Pawn threat = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && faction != null && p.HostileTo(faction) && !p.Dead)
                .OrderBy(p => p.Position.DistanceToSquared(supportCenter)).FirstOrDefault();
            if (threat == null) return "north";
            int dx = threat.Position.x - supportCenter.x;
            int dz = threat.Position.z - supportCenter.z;
            return Math.Abs(dx) > Math.Abs(dz) ? (dx < 0 ? "west" : "east") : (dz < 0 ? "south" : "north");
        }

        private HashSet<GridPoint> RuntimeOpaqueCells() => new HashSet<GridPoint>(map.listerThings.AllThings
            .Where(t => t.Spawned && t is Building && t.def.Fillage == FillCategory.Full)
            .Select(t => new GridPoint(t.Position.x, t.Position.z)));

        private GridPoint ChooseReserveStage(Pawn pawn, string sector, IReadOnlyCollection<GridPoint> opaque)
        {
            IntVec3[] directions = { new IntVec3(0, 0, 1), new IntVec3(1, 0, 0),
                new IntVec3(0, 0, -1), new IntVec3(-1, 0, 0) };
            List<IntVec3> targets = perimeterCells.Where((c, i) => perimeterSectors[i] == sector).ToList();
            IntVec3 target = targets.Count == 0 ? supportCenter : targets
                .OrderBy(c => c.DistanceToSquared(supportCenter)).First();
            IntVec3? found = gateCells.SelectMany(g => directions.Select(d => g + d))
                .Where(c => c.InBounds(map) && interiorCells.Contains(c) && c.Standable(map) &&
                    c.GetEdifice(map) == null && ResponseRouteAllowed(pawn, supportCenter, c, opaque).Allowed)
                .Distinct().OrderBy(c => c.DistanceToSquared(target))
                .ThenBy(c => c.DistanceToSquared(supportCenter)).Cast<IntVec3?>().FirstOrDefault();
            IntVec3 stage = found ?? supportCenter;
            if (!stage.IsValid || !InsideBounds(stage)) stage = supportCenter;
            return new GridPoint(stage.x, stage.z);
        }

        private P3DefenderRouteAudit ResponseRouteAllowed(Pawn pawn, IntVec3 from, IntVec3 destination,
            IReadOnlyCollection<GridPoint> opaque)
        {
            if (pawn == null || !from.InBounds(map) || !destination.InBounds(map) ||
                !InsideBounds(from) || !InsideBounds(destination))
                return P3DefenderRoute.Audit(new GridPoint[0], new GridPoint[0], new GridPoint[0],
                    new GridPoint[0], new GridPoint[0]);
            using (PawnPath path = map.pathFinder.FindPathNow(from, destination, TraverseParms.For(pawn),
                null, PathEndMode.OnCell))
            {
                if (!path.Found) return P3DefenderRoute.Audit(new GridPoint[0], new GridPoint[0],
                    new GridPoint[0], new GridPoint[0], new GridPoint[0]);
                List<GridPoint> cells = path.NodesReversed.AsEnumerable().Reverse()
                    .Select(c => new GridPoint(c.x, c.z)).ToList();
                List<GridPoint> enemies = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null &&
                    faction != null && p.HostileTo(faction) && !p.Dead && !p.Downed)
                    .Select(p => new GridPoint(p.Position.x, p.Position.z)).ToList();
                return P3DefenderRoute.Audit(cells, defendedCells.Select(c => new GridPoint(c.x, c.z)).ToList(),
                    interiorCells.Select(c => new GridPoint(c.x, c.z)).ToList(), enemies, opaque);
            }
        }

        private string ObserveThreatSector(bool hostileThreat)
        {
            if (perimeterCells.Count == 0) return ThreatSector();
            List<GridPoint> enemies = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null &&
                faction != null && p.HostileTo(faction) && !p.Dead && !p.Downed)
                .Select(p => new GridPoint(p.Position.x, p.Position.z)).ToList();
            for (int i = 0; i < perimeterCells.Count; i++)
            {
                IntVec3 cell = perimeterCells[i];
                int current = cell.GetEdifice(map)?.HitPoints ?? 0;
                int previous = perimeterHitPoints[i];
                if (previous > 0 && current == 0) perimeterLastBreachTicks[i] = GenTicks.TicksGame;
                else if (previous == 0 && current > 0) perimeterLastBreachTicks[i] = -1;
                if (hostileThreat && previous > current && enemies.Any(e =>
                    (e.X - cell.x) * (e.X - cell.x) + (e.Z - cell.z) * (e.Z - cell.z) <= 55 * 55))
                {
                    perimeterLastHitTicks[i] = GenTicks.TicksGame;
                    perimeterLastHitDamage[i] = previous - current;
                }
                perimeterHitPoints[i] = current;
            }
            List<P3PerimeterSegment> segments = new List<P3PerimeterSegment>();
            List<P3PerimeterObservation> observations = new List<P3PerimeterObservation>();
            for (int i = 0; i < perimeterCells.Count; i++)
            {
                GridPoint cell = new GridPoint(perimeterCells[i].x, perimeterCells[i].z);
                segments.Add(new P3PerimeterSegment(cell, perimeterSectors[i],
                    (P3PerimeterRegion)perimeterRegions[i], perimeterGates[i]));
                if (perimeterLastHitTicks[i] >= 0)
                    observations.Add(new P3PerimeterObservation(cell, perimeterLastHitTicks[i], false,
                        perimeterLastHitDamage[i]));
                if (perimeterLastBreachTicks[i] >= 0)
                    observations.Add(new P3PerimeterObservation(cell, perimeterLastBreachTicks[i], true, 0));
            }
            P3SectorThreat first = P3PerimeterThreat.Rank(GenTicks.TicksGame, segments, observations,
                enemies, currentSector).FirstOrDefault();
            currentThreatSeverity = first?.Score ?? 0;
            return first == null || first.Score == 0 ? ThreatSector() : first.Sector;
        }

        private GarrisonRole AssignedRole(Pawn pawn)
        {
            int index = assignedPawnIds.IndexOf(pawn.thingIDNumber);
            return index >= 0 && index < assignedRoles.Count ? (GarrisonRole)assignedRoles[index] : GarrisonRole.GateReserve;
        }

        private IntVec3 AssignedPost(Pawn pawn)
        {
            int index = assignedPawnIds.IndexOf(pawn.thingIDNumber);
            if (index < 0 || index >= assignedPostIds.Count) return IntVec3.Invalid;
            int post = postIds.IndexOf(assignedPostIds[index]);
            return post >= 0 && post < postCells.Count ? postCells[post] : IntVec3.Invalid;
        }

        private IntVec3 AssignedAccessGate(Pawn pawn)
        {
            int index = assignedPawnIds.IndexOf(pawn.thingIDNumber);
            if (index < 0 || index >= assignedPostIds.Count) return IntVec3.Invalid;
            int post = postIds.IndexOf(assignedPostIds[index]);
            return post >= 0 && post < postAccessGates.Count ? postAccessGates[post] : IntVec3.Invalid;
        }

        private IntVec3 AssignedDestination(Pawn pawn)
        {
            int index = assignedPawnIds.IndexOf(pawn.thingIDNumber);
            return index >= 0 && index < assignedDestinations.Count ? assignedDestinations[index] : IntVec3.Invalid;
        }

        private bool PreviousPostAssigned(string id) => assignedPostIds.Contains(id);

        private bool InsideBounds(IntVec3 cell) => defendedCells != null && defendedCells.Count > 0
            ? defendedCells.Contains(cell)
            : boundsMin.IsValid && cell.x >= boundsMin.x && cell.z >= boundsMin.z &&
                cell.x < boundsMin.x + boundsWidth && cell.z < boundsMin.z + boundsHeight;

        private int CountStock(string defName) => operatingStocks.Where(t => t != null && !t.Destroyed && t.def.defName == defName)
            .Sum(t => t.stackCount);

        private string MortarLoadedShell()
        {
            Building_TurretGun turret = mortar as Building_TurretGun;
            CompChangeableProjectile projectile = turret?.gun?.TryGetComp<CompChangeableProjectile>();
            return projectile?.LoadedShell == null ? "none" : projectile.LoadedShell.defName + "x" + projectile.loadedCount;
        }

        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }

    internal static class P3Log
    {
        internal const string Prefix = "[RealisticBases:P3]";

        internal static void Message(string eventName, string details) =>
            Log.Message(Prefix + " event=" + eventName + " tick=" + GenTicks.TicksGame + " " + details);

        internal static void Error(string eventName, string details) =>
            Log.Error(Prefix + " event=" + eventName + " tick=" + GenTicks.TicksGame + " " + details);
    }
}
