using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public enum GarrisonState
    {
        Routine,
        HoldSectors,
        LocalResponse,
        InnerFallback,
        // Hostiles are on the map but have not acted: a watch holds the likely
        // side and the rest stay off duty.
        Watch
    }

    public enum GarrisonRole
    {
        RangedPost,
        GateReserve,
        MortarOperator,
        Support,
        NeedsRelief,
        InnerFallback,
        // Awake in the compound with native needs, no post and no rest.
        OffDuty
    }

    public enum DefenderWeaponClass
    {
        Melee,
        ShortRanged,
        LongRanged
    }

    public sealed class DefensivePost
    {
        public DefensivePost(string id, string sectorId, GridPoint cell, int minimumRange, int maximumRange,
            bool hasCover, bool hasLineOfFire, bool routeWithinBounds, bool mortar, bool inner, bool usable,
            IEnumerable<string> supportedSectors = null, GridPoint? accessGate = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(sectorId))
                throw new ArgumentException("Post and sector ids are required.");
            if (minimumRange < 0 || maximumRange < minimumRange) throw new ArgumentOutOfRangeException(nameof(maximumRange));
            Id = id;
            SectorId = sectorId;
            SupportedSectors = new ReadOnlyCollection<string>((supportedSectors ?? new[] { sectorId })
                .Concat(new[] { sectorId }).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList());
            Cell = cell;
            AccessGate = accessGate;
            MinimumRange = minimumRange;
            MaximumRange = maximumRange;
            HasCover = hasCover;
            HasLineOfFire = hasLineOfFire;
            RouteWithinBounds = routeWithinBounds;
            Mortar = mortar;
            Inner = inner;
            Usable = usable;
        }

        public string Id { get; }
        public string SectorId { get; }
        public IReadOnlyList<string> SupportedSectors { get; }
        public bool CoversSector(string sector) => SupportedSectors.Contains(sector);
        public GridPoint Cell { get; }
        // Optional waypoint for an exterior post. Prevents vanilla's shortest
        // path from bypassing the planned short gate-to-post access route.
        public GridPoint? AccessGate { get; }
        public int MinimumRange { get; }
        public int MaximumRange { get; }
        public bool HasCover { get; }
        public bool HasLineOfFire { get; }
        public bool RouteWithinBounds { get; }
        public bool Mortar { get; }
        public bool Inner { get; }
        public bool Usable { get; }
        public bool IsValid => Usable && RouteWithinBounds && (Mortar || HasLineOfFire);
    }

    // Persisted in P3's existing postFlags list. Older saves have no sniper
    // bit and retain their original ordinary-post interpretation.
    public static class P3PostFlags
    {
        private const int Mortar = 1, Inner = 2, Melee = 4, SniperGuard = 8;
        public static int Encode(DefensivePost post) =>
            (post.Mortar ? Mortar : 0) | (post.Inner ? Inner : 0) |
            (post.MinimumRange <= 3 ? Melee : 0) | (post.MinimumRange >= 35 ? SniperGuard : 0);
        public static bool IsMortar(int flags) => (flags & Mortar) != 0;
        public static bool IsInner(int flags) => (flags & Inner) != 0;
        public static bool IsMelee(int flags) => (flags & Melee) != 0;
        public static bool IsSniperGuard(int flags) => (flags & SniperGuard) != 0;
        public static int MinimumRange(int flags) => IsMelee(flags) ? 0 : IsSniperGuard(flags) ? 35 : 8;
        public static int MaximumRange(int flags) => IsMelee(flags) ? 3 : IsSniperGuard(flags) ? 44 : 48;
        public static int SecondaryScreen(int flags) => flags & ~SniperGuard;
    }

    public sealed class DefenderCandidate
    {
        public DefenderCandidate(int id, DefenderWeaponClass weaponClass, int weaponRange,
            bool canOperateMortar, bool canSupport, bool needsFood, bool needsRest, bool needsTending,
            string previousPostId = null, GarrisonRole? previousRole = null, int lastAssignmentTick = int.MinValue,
            GridPoint? currentCell = null, bool hasSmokepop = false, bool canTend = true, float restLevel = 1f)
        {
            if (id <= 0 || weaponRange < 0) throw new ArgumentOutOfRangeException();
            Id = id;
            WeaponClass = weaponClass;
            WeaponRange = weaponRange;
            CanOperateMortar = canOperateMortar;
            CanSupport = canSupport;
            NeedsFood = needsFood;
            NeedsRest = needsRest;
            NeedsTending = needsTending;
            PreviousPostId = previousPostId;
            PreviousRole = previousRole;
            LastAssignmentTick = lastAssignmentTick;
            CurrentCell = currentCell;
            HasSmokepop = hasSmokepop;
            CanTend = canTend;
            RestLevel = restLevel;
        }

        public int Id { get; }
        public DefenderWeaponClass WeaponClass { get; }
        public int WeaponRange { get; }
        public bool CanOperateMortar { get; }
        public bool CanSupport { get; }
        public bool HasSmokepop { get; }
        // Medical work is not disabled (vanilla Caring/Doctor).
        public bool CanTend { get; }
        public bool NeedsFood { get; }
        public bool NeedsRest { get; }
        public bool NeedsTending { get; }
        // Rest need from 0 (exhausted) to 1; orders the sleep and watch rotations.
        public float RestLevel { get; }
        public string PreviousPostId { get; }
        public GarrisonRole? PreviousRole { get; }
        public int LastAssignmentTick { get; }
        public GridPoint? CurrentCell { get; }
        public bool NeedsRelief => NeedsFood || NeedsRest || NeedsTending;
    }

    public sealed class GarrisonAssignment
    {
        public GarrisonAssignment(int defenderId, GarrisonRole role, string sectorId, string postId,
            GridPoint destination, int assignedTick, string reason)
        {
            DefenderId = defenderId;
            Role = role;
            SectorId = sectorId ?? string.Empty;
            PostId = postId ?? string.Empty;
            Destination = destination;
            AssignedTick = assignedTick;
            Reason = reason ?? string.Empty;
        }

        public int DefenderId { get; }
        public GarrisonRole Role { get; }
        public string SectorId { get; }
        public string PostId { get; }
        public GridPoint Destination { get; }
        public int AssignedTick { get; }
        public string Reason { get; }
    }

    public sealed class GarrisonPlan
    {
        public GarrisonPlan(GarrisonState state, IEnumerable<GarrisonAssignment> assignments, string activeSector,
            int tick, string reason)
        {
            State = state;
            Assignments = new ReadOnlyCollection<GarrisonAssignment>((assignments ?? Enumerable.Empty<GarrisonAssignment>())
                .OrderBy(a => a.DefenderId).ToList());
            ActiveSector = activeSector ?? string.Empty;
            Tick = tick;
            Reason = reason ?? string.Empty;
            if (Assignments.GroupBy(a => a.DefenderId).Any(g => g.Count() != 1))
                throw new ArgumentException("A defender may have only one assignment.");
            if (Assignments.Where(a => a.PostId.Length > 0).GroupBy(a => a.PostId, StringComparer.Ordinal)
                .Any(g => g.Count() != 1))
                throw new ArgumentException("A post may have only one simultaneous defender.");
        }

        public GarrisonState State { get; }
        public IReadOnlyList<GarrisonAssignment> Assignments { get; }
        public string ActiveSector { get; }
        public int Tick { get; }
        public string Reason { get; }
    }

    public sealed class GarrisonSituation
    {
        public GarrisonSituation(int tick, IEnumerable<DefenderCandidate> defenders, IEnumerable<DefensivePost> posts,
            bool hostileThreat, string threatenedSector, bool gateBreached, bool outerDefenseViable,
            bool needsRouteSafe, GridPoint supportCenter, GridPoint innerFallback, int routineSupportWorkers = 1,
            IEnumerable<GridPoint> hostileCells = null, IEnumerable<GridPoint> opaqueCells = null,
            IEnumerable<string> safeTransferPosts = null, int threatSeverity = 0, GridPoint? reserveStage = null,
            bool fieldPenetrated = false, bool compoundPenetrated = false,
            IEnumerable<string> fieldFallbackPosts = null, bool criticalSupportRequired = false,
            bool assaultSignal = true, int lastAssaultTick = int.MinValue)
        {
            if (routineSupportWorkers < 0) throw new ArgumentOutOfRangeException(nameof(routineSupportWorkers));
            Tick = tick;
            Defenders = new ReadOnlyCollection<DefenderCandidate>((defenders ?? throw new ArgumentNullException(nameof(defenders)))
                .OrderBy(d => d.Id).ToList());
            Posts = new ReadOnlyCollection<DefensivePost>((posts ?? throw new ArgumentNullException(nameof(posts)))
                .OrderBy(p => p.Id, StringComparer.Ordinal).ToList());
            HostileThreat = hostileThreat;
            ThreatenedSector = threatenedSector ?? string.Empty;
            GateBreached = gateBreached;
            OuterDefenseViable = outerDefenseViable;
            NeedsRouteSafe = needsRouteSafe;
            SupportCenter = supportCenter;
            InnerFallback = innerFallback;
            RoutineSupportWorkers = routineSupportWorkers;
            HostileCells = new ReadOnlyCollection<GridPoint>((hostileCells ?? Enumerable.Empty<GridPoint>()).ToList());
            OpaqueCells = new HashSet<GridPoint>(opaqueCells ?? Enumerable.Empty<GridPoint>());
            SafeTransferPosts = new HashSet<string>(safeTransferPosts ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            ThreatSeverity = Math.Max(0, threatSeverity);
            ReserveStage = reserveStage ?? supportCenter;
            FieldPenetrated = fieldPenetrated;
            CompoundPenetrated = compoundPenetrated;
            FieldFallbackPosts = new HashSet<string>(fieldFallbackPosts ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            CriticalSupportRequired = criticalSupportRequired;
            AssaultSignal = assaultSignal;
            LastAssaultTick = lastAssaultTick;
        }

        public int Tick { get; }
        public IReadOnlyList<DefenderCandidate> Defenders { get; }
        public IReadOnlyList<DefensivePost> Posts { get; }
        public bool HostileThreat { get; }
        public string ThreatenedSector { get; }
        public bool GateBreached { get; }
        public bool OuterDefenseViable { get; }
        public bool NeedsRouteSafe { get; }
        public GridPoint SupportCenter { get; }
        public GridPoint InnerFallback { get; }
        public int RoutineSupportWorkers { get; }
        public IReadOnlyList<GridPoint> HostileCells { get; }
        public IReadOnlyCollection<GridPoint> OpaqueCells { get; }
        public IReadOnlyCollection<string> SafeTransferPosts { get; }
        public int ThreatSeverity { get; }
        public GridPoint ReserveStage { get; }
        public bool FieldPenetrated { get; }
        public bool CompoundPenetrated { get; }
        public IReadOnlyCollection<string> FieldFallbackPosts { get; }
        public bool CriticalSupportRequired { get; }
        // A hostile acted this refresh: harmed a resident, a building or a
        // turret, fired, or came within the alarm range of the perimeter.
        public bool AssaultSignal { get; }
        // The tick of the last assault signal; the assault holds for
        // GarrisonDoctrine.AssaultLatchTicks after it.
        public int LastAssaultTick { get; }
        public bool Assault => HostileThreat && (AssaultSignal || GateBreached || FieldPenetrated ||
            CompoundPenetrated || LastAssaultTick != int.MinValue &&
            Tick - LastAssaultTick < GarrisonDoctrine.AssaultLatchTicks);
    }

    // The side the pack defends. A newly attacked side replaces it only after
    // leading the threat ranking for DwellTicks, so two sides hit in turn do
    // not pull the pack back and forth on every refresh.
    public sealed class P3SectorDwell
    {
        public const int DwellTicks = 300;

        public P3SectorDwell(string held = "", string candidate = "", int candidateSince = 0)
        {
            Held = held ?? string.Empty;
            Candidate = candidate ?? string.Empty;
            CandidateSince = candidateSince;
        }

        public string Held { get; private set; }
        public string Candidate { get; private set; }
        public int CandidateSince { get; private set; }

        public string Observe(string observed, int tick)
        {
            // A refresh with no ranked side neither moves the pack nor restarts the dwell.
            if (string.IsNullOrEmpty(observed)) return Held;
            if (Held.Length == 0 || observed == Held)
            {
                Held = observed;
                Candidate = string.Empty;
                return Held;
            }
            if (observed != Candidate)
            {
                Candidate = observed;
                CandidateSince = tick;
            }
            if (tick - CandidateSince >= DwellTicks)
            {
                Held = observed;
                Candidate = string.Empty;
            }
            return Held;
        }
    }

    public static class GarrisonDoctrine
    {
        public const int ReassignmentCooldownTicks = 300;
        // One in-game hour without a new signal ends an assault.
        public const int AssaultLatchTicks = 2500;

        private const float SleeperHoldBand = 0.3f;

        public static int WatchSize(GarrisonState state, int pack) =>
            Math.Min(pack, state == GarrisonState.Routine ? 1 : Math.Max(2, (pack + 2) / 3));

        public static GarrisonPlan Assign(GarrisonSituation situation)
        {
            if (situation == null) throw new ArgumentNullException(nameof(situation));
            GarrisonState state = SelectState(situation);
            if (state == GarrisonState.Routine || state == GarrisonState.Watch) return AssignStandby(situation, state);
            // Keep the exterior line primary, but staff the already-funded inner
            // gate positions before leaving additional defenders idle at one
            // support cell. Full fallback still abandons exterior posts.
            List<DefensivePost> validPosts = situation.Posts.Where(p => p.IsValid &&
                (state != GarrisonState.InnerFallback || p.Inner)).ToList();
            if (validPosts.Count == 0) state = GarrisonState.InnerFallback;

            HashSet<string> occupied = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> relieved = ReliefCandidates(situation);
            HashSet<int> routineSupport = new HashSet<int>(situation.CriticalSupportRequired &&
                state != GarrisonState.InnerFallback && situation.Defenders.Count(d => !d.NeedsRelief) >= 4
                ? SupportWorkers(situation) : Enumerable.Empty<int>());
            Dictionary<int, DefensivePost> crews = state == GarrisonState.InnerFallback
                ? new Dictionary<int, DefensivePost>() : ReserveCrews(situation, validPosts, routineSupport);
            foreach (DefensivePost crewed in crews.Values) occupied.Add(crewed.Id);
            // Unfunded/unavailable artillery posts must not be picked by the
            // ordinary best-post path after the exclusive crew reservation.
            validPosts = validPosts.Where(p => !p.Mortar || occupied.Contains(p.Id)).ToList();
            DefenderCandidate mobileReserve = state == GarrisonState.InnerFallback ||
                routineSupport.Count > 0 ||
                situation.Defenders.Count(d => !d.NeedsRelief) < 5 ? null : situation.Defenders
                .Where(d => !crews.ContainsKey(d.Id) && !routineSupport.Contains(d.Id) && !d.NeedsRelief &&
                    d.WeaponClass != DefenderWeaponClass.Melee)
                .OrderBy(d => d.WeaponClass == DefenderWeaponClass.LongRanged)
                .ThenBy(d => d.WeaponRange).ThenBy(d => d.Id).FirstOrDefault();
            if (mobileReserve != null && situation.Defenders.Count(d => !d.NeedsRelief &&
                d.WeaponClass != DefenderWeaponClass.Melee && !crews.ContainsKey(d.Id)) < 3)
                mobileReserve = null;
            bool UrgentFieldFallback(DefenderCandidate defender, DefensivePost previous) =>
                situation.FieldPenetrated && state == GarrisonState.LocalResponse &&
                defender.WeaponRange <= 30 && (previous == null ||
                    !situation.FieldFallbackPosts.Contains(previous.Id)) && validPosts.Any(p =>
                        !occupied.Contains(p.Id) && situation.FieldFallbackPosts.Contains(p.Id) &&
                        Compatible(defender, p));
            // A defender already on a post covering the attacked side keeps
            // it, so the pack does not reshuffle within the side.
            var held = new Dictionary<int, DefensivePost>();
            if (situation.ThreatenedSector.Length > 0)
                foreach (DefenderCandidate defender in situation.Defenders.Where(d => !relieved.Contains(d.Id) &&
                    !routineSupport.Contains(d.Id) && !crews.ContainsKey(d.Id) && d.Id != mobileReserve?.Id))
                {
                    DefensivePost previous = validPosts.FirstOrDefault(p => p.Id == defender.PreviousPostId &&
                        !p.Mortar && !occupied.Contains(p.Id) && Compatible(defender, p) &&
                        p.CoversSector(situation.ThreatenedSector));
                    if (previous == null || UrgentFieldFallback(defender, previous)) continue;
                    held.Add(defender.Id, previous);
                    occupied.Add(previous.Id);
                }
            List<GarrisonAssignment> assignments = new List<GarrisonAssignment>();
            foreach (DefenderCandidate defender in situation.Defenders)
            {
                if (held.TryGetValue(defender.Id, out DefensivePost kept))
                {
                    assignments.Add(PostAssignment(defender, kept, state, situation.Tick, "held_attacked_side"));
                    continue;
                }
                if (relieved.Contains(defender.Id))
                {
                    assignments.Add(new GarrisonAssignment(defender.Id, GarrisonRole.NeedsRelief, string.Empty,
                        string.Empty, situation.SupportCenter, situation.Tick, "safe_needs_relief"));
                    continue;
                }

                if (routineSupport.Contains(defender.Id))
                {
                    assignments.Add(new GarrisonAssignment(defender.Id, GarrisonRole.Support, string.Empty,
                        string.Empty, situation.SupportCenter, situation.Tick, "funded_reserve_critical_logistics"));
                    continue;
                }

                if (crews.TryGetValue(defender.Id, out DefensivePost crewPost))
                {
                    assignments.Add(PostAssignment(defender, crewPost, state, situation.Tick,
                        "funded_mortar_operator"));
                    continue;
                }
                if (defender.Id == mobileReserve?.Id) continue;

                DefensivePost previous = validPosts.FirstOrDefault(p => p.Id == defender.PreviousPostId &&
                    Compatible(defender, p) && !occupied.Contains(p.Id));
                // A post off the attacked side is left at once while that side
                // still has a free slot for this defender.
                bool offSide = previous != null && situation.ThreatenedSector.Length > 0 &&
                    !previous.Mortar && !previous.CoversSector(situation.ThreatenedSector) &&
                    validPosts.Any(p => !occupied.Contains(p.Id) && p.CoversSector(situation.ThreatenedSector) &&
                        Compatible(defender, p));
                bool cooldown = situation.Tick - defender.LastAssignmentTick < ReassignmentCooldownTicks && !offSide;
                bool urgentFieldFallback = UrgentFieldFallback(defender, previous);
                if (previous != null && cooldown && !urgentFieldFallback && !relieved.Contains(defender.Id))
                {
                    occupied.Add(previous.Id);
                    assignments.Add(PostAssignment(defender, previous, state, situation.Tick, "stable_assignment"));
                    continue;
                }

                DefensivePost selected = SelectPost(defender, validPosts, occupied, situation, state);
                if (selected != null)
                {
                    occupied.Add(selected.Id);
                    assignments.Add(PostAssignment(defender, selected, state, situation.Tick,
                        selected.Id == defender.PreviousPostId ? "retained_valid_post" : "best_compatible_post"));
                    continue;
                }

                GarrisonRole role = state == GarrisonState.InnerFallback ? GarrisonRole.InnerFallback : GarrisonRole.GateReserve;
                GridPoint destination = state == GarrisonState.InnerFallback ? situation.InnerFallback : situation.SupportCenter;
                assignments.Add(new GarrisonAssignment(defender.Id, role, situation.ThreatenedSector,
                    string.Empty, destination, situation.Tick, "no_compatible_post"));
            }
            if (mobileReserve != null)
            {
                DefensivePost reinforcement = situation.ThreatSeverity >= 15 ?
                    SelectPost(mobileReserve, validPosts.Where(p => !p.Mortar &&
                        p.CoversSector(situation.ThreatenedSector) && situation.SafeTransferPosts.Contains(p.Id)),
                        occupied, situation, state) : null;
                if (reinforcement != null)
                {
                    occupied.Add(reinforcement.Id);
                    assignments.Add(PostAssignment(mobileReserve, reinforcement, state, situation.Tick,
                        "mobile_reserve_reinforcement"));
                }
                else assignments.Add(new GarrisonAssignment(mobileReserve.Id, GarrisonRole.GateReserve,
                    situation.ThreatenedSector, string.Empty, situation.ReserveStage, situation.Tick,
                    "funded_mobile_reserve_staging"));
            }
            return new GarrisonPlan(state, assignments, situation.ThreatenedSector, situation.Tick,
                state == GarrisonState.LocalResponse ? "local_breach_response" : state.ToString());
        }

        public static bool CanPursue(GridPoint origin, GridPoint target, IEnumerable<GridPoint> route,
            GridRect defendedBounds, int maximumRouteCells)
        {
            if (maximumRouteCells <= 0 || !defendedBounds.Contains(origin) || !defendedBounds.Contains(target)) return false;
            List<GridPoint> cells = (route ?? Enumerable.Empty<GridPoint>()).ToList();
            if (cells.Count == 0 || cells.Count > maximumRouteCells || !cells[0].Equals(origin) ||
                !cells[cells.Count - 1].Equals(target) || cells.Any(c => !defendedBounds.Contains(c))) return false;
            for (int i = 1; i < cells.Count; i++)
                if (Math.Abs(cells[i].X - cells[i - 1].X) + Math.Abs(cells[i].Z - cells[i - 1].Z) != 1) return false;
            return true;
        }

        // Routine and Watch: support workers, mortar crews in Watch, a watch on
        // the likely side, a bounded share of sleepers and everyone else off duty.
        private static GarrisonPlan AssignStandby(GarrisonSituation situation, GarrisonState state)
        {
            List<DefensivePost> validPosts = situation.Posts.Where(p => p.IsValid).ToList();
            HashSet<int> support = new HashSet<int>(SupportWorkers(situation));
            Dictionary<int, DefensivePost> crews = state == GarrisonState.Watch
                ? ReserveCrews(situation, validPosts, support) : new Dictionary<int, DefensivePost>();
            HashSet<string> occupied = new HashSet<string>(crews.Values.Select(p => p.Id), StringComparer.Ordinal);
            validPosts = validPosts.Where(p => !p.Mortar).ToList();
            List<GarrisonAssignment> assignments = new List<GarrisonAssignment>();
            foreach (int id in support)
                assignments.Add(new GarrisonAssignment(id, GarrisonRole.Support, string.Empty, string.Empty,
                    situation.SupportCenter, situation.Tick, "routine_support"));
            foreach (KeyValuePair<int, DefensivePost> crew in crews)
                assignments.Add(PostAssignment(situation.Defenders.Single(d => d.Id == crew.Key), crew.Value, state,
                    situation.Tick, "funded_mortar_operator"));

            // The most rested watch. A watcher who needs no rest keeps the
            // duty; a sleeper leaves its bed only when SleeperHoldBand more
            // rested than the others, so a tired watcher goes to bed in its place.
            int watchSize = WatchSize(state, situation.Defenders.Count);
            HashSet<int> watch = new HashSet<int>();
            foreach (DefenderCandidate defender in situation.Defenders
                .Where(d => !support.Contains(d.Id) && !crews.ContainsKey(d.Id))
                .OrderBy(d => d.NeedsFood || d.NeedsTending)
                .ThenBy(d => d.WeaponClass == DefenderWeaponClass.Melee)
                .ThenByDescending(d => AwakeScore(d))
                .ThenBy(d => d.Id))
            {
                if (watch.Count == watchSize) break;
                DefensivePost previous = validPosts.FirstOrDefault(p => p.Id == defender.PreviousPostId &&
                    !occupied.Contains(p.Id) && Compatible(defender, p) &&
                    (situation.ThreatenedSector.Length == 0 || p.CoversSector(situation.ThreatenedSector)));
                DefensivePost post = previous ?? SelectPost(defender, validPosts, occupied, situation, state);
                if (post == null) continue;
                occupied.Add(post.Id);
                watch.Add(defender.Id);
                assignments.Add(PostAssignment(defender, post, state, situation.Tick,
                    post == previous ? "stable_watch" : "watch"));
            }

            List<DefenderCandidate> offDuty = situation.Defenders.Where(d => !support.Contains(d.Id) &&
                !crews.ContainsKey(d.Id) && !watch.Contains(d.Id)).ToList();
            HashSet<int> relieved = new HashSet<int>();
            if (situation.NeedsRouteSafe)
            {
                foreach (DefenderCandidate d in offDuty.Where(d => d.NeedsFood || d.NeedsTending)) relieved.Add(d.Id);
                // Half the off-duty defenders may sleep at once, but never
                // fewer than a third of the pack: a defender sleeps about a
                // third of the day, so a smaller share exhausts the garrison.
                // A sleeper keeps its bed unless a waiting defender is
                // SleeperHoldBand more tired; the most tired go next.
                int sleepers = Math.Min(offDuty.Count, Math.Max(Math.Max(1, offDuty.Count / 2),
                    (situation.Defenders.Count + 2) / 3));
                foreach (DefenderCandidate d in offDuty.Where(d => d.NeedsRest && !relieved.Contains(d.Id))
                    .OrderBy(d => d.RestLevel - (d.PreviousRole == GarrisonRole.NeedsRelief ? SleeperHoldBand : 0f))
                    .ThenBy(d => d.Id)
                    .Take(Math.Max(0, sleepers - offDuty.Count(d => relieved.Contains(d.Id) && d.NeedsRest))))
                    relieved.Add(d.Id);
            }
            foreach (DefenderCandidate defender in offDuty)
                assignments.Add(relieved.Contains(defender.Id)
                    ? new GarrisonAssignment(defender.Id, GarrisonRole.NeedsRelief, string.Empty, string.Empty,
                        situation.SupportCenter, situation.Tick, "safe_needs_relief")
                    : new GarrisonAssignment(defender.Id, GarrisonRole.OffDuty, string.Empty, string.Empty,
                        situation.SupportCenter, situation.Tick, "off_duty"));
            return new GarrisonPlan(state, assignments, situation.ThreatenedSector, situation.Tick, state.ToString());
        }

        private static bool Sleeping(DefenderCandidate d) => d.PreviousRole == GarrisonRole.NeedsRelief && d.NeedsRest;

        private static bool Watching(DefenderCandidate d) =>
            d.PreviousRole == GarrisonRole.RangedPost || d.PreviousRole == GarrisonRole.GateReserve ||
            d.PreviousRole == GarrisonRole.MortarOperator;

        private static float AwakeScore(DefenderCandidate d) => d.RestLevel +
            (Sleeping(d) ? -SleeperHoldBand : 0f) + (Watching(d) && !d.NeedsRest ? 1f : 0f);

        private static IEnumerable<int> SupportWorkers(GarrisonSituation situation) =>
            situation.Defenders.Where(d => d.CanSupport && !d.NeedsRelief)
                // A wounded resident needs a support worker who can tend.
                .OrderBy(d => situation.Defenders.Any(w => w.NeedsTending) && !d.CanTend)
                .ThenBy(d => d.PreviousRole == GarrisonRole.MortarOperator && d.CanOperateMortar)
                .ThenByDescending(d => d.HasSmokepop)
                .ThenBy(d => d.CanOperateMortar)
                .ThenBy(d => d.PreviousRole == GarrisonRole.Support || d.PreviousRole == GarrisonRole.GateReserve ? 0 :
                    d.PreviousRole == GarrisonRole.MortarOperator ? 2 : 1)
                .ThenBy(d => d.WeaponClass == DefenderWeaponClass.LongRanged)
                .ThenBy(d => d.WeaponRange)
                .ThenBy(d => d.Id)
                .Take(situation.RoutineSupportWorkers).Select(d => d.Id).ToList();

        private static Dictionary<int, DefensivePost> ReserveCrews(GarrisonSituation situation,
            IEnumerable<DefensivePost> validPosts, HashSet<int> support)
        {
            var crews = new Dictionary<int, DefensivePost>();
            foreach (DefensivePost mortarPost in validPosts.Where(p => p.Mortar).OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                // Leave two useful direct defenders before reserving more
                // artillery. Losses release surplus guns rather than add pawns.
                // A tired defender may crew a gun; under assault nobody sleeps,
                // and in watch the rested crew first.
                if (situation.Defenders.Count(d => !d.NeedsFood && !d.NeedsTending &&
                    !support.Contains(d.Id) && !crews.ContainsKey(d.Id)) <= (crews.Count == 0 ? 1 : 2)) break;
                DefenderCandidate crew = situation.Defenders.Where(d => !support.Contains(d.Id) &&
                    !crews.ContainsKey(d.Id) && !d.NeedsFood && !d.NeedsTending && Compatible(d, mortarPost))
                    .OrderByDescending(d => AwakeScore(d) >= 0.35f)
                    .ThenBy(d => d.PreviousPostId == mortarPost.Id ? 0 : 1)
                    .ThenBy(d => d.PreviousRole == GarrisonRole.MortarOperator ? 1 : 0)
                    .ThenBy(d => d.HasSmokepop)
                    .ThenBy(d => d.WeaponClass == DefenderWeaponClass.LongRanged)
                    .ThenBy(d => d.WeaponRange).ThenBy(d => d.Id).FirstOrDefault();
                if (crew != null) crews.Add(crew.Id, mortarPost);
            }
            return crews;
        }

        private static GarrisonState SelectState(GarrisonSituation situation)
        {
            if (!situation.HostileThreat) return GarrisonState.Routine;
            if (!situation.Assault) return GarrisonState.Watch;
            if (situation.CompoundPenetrated) return GarrisonState.InnerFallback;
            if (!situation.OuterDefenseViable) return GarrisonState.InnerFallback;
            if (situation.FieldPenetrated) return GarrisonState.LocalResponse;
            if (situation.GateBreached) return GarrisonState.LocalResponse;
            return GarrisonState.HoldSectors;
        }

        // Under assault nobody is relieved to sleep; food and tending still are.
        private static HashSet<int> ReliefCandidates(GarrisonSituation situation)
        {
            if (!situation.NeedsRouteSafe) return new HashSet<int>();
            // Unstaffed geometric options do not increase the real garrison.
            // Even if everybody is hungry, release a bounded rotating subset
            // rather than deadlocking until all healthy candidates disappear.
            int minimumOnDuty = Math.Min(2, Math.Max(1, situation.Defenders.Count / 2));
            int capacity = Math.Max(0, situation.Defenders.Count - minimumOnDuty);
            return new HashSet<int>(situation.Defenders.Where(d => d.NeedsFood || d.NeedsTending)
                .OrderByDescending(d => d.NeedsFood).ThenByDescending(d => d.NeedsTending)
                .ThenByDescending(d => d.PreviousRole == GarrisonRole.NeedsRelief)
                .ThenBy(d => d.Id).Take(capacity).Select(d => d.Id));
        }

        private static DefensivePost SelectPost(DefenderCandidate defender, IEnumerable<DefensivePost> posts,
            HashSet<string> occupied, GarrisonSituation situation, GarrisonState state)
        {
            return posts.Where(p => !occupied.Contains(p.Id) && Compatible(defender, p))
                .OrderBy(p => state != GarrisonState.InnerFallback && p.Inner &&
                    !(situation.FieldPenetrated && situation.FieldFallbackPosts.Contains(p.Id)))
                .ThenByDescending(p => situation.FieldPenetrated && state == GarrisonState.LocalResponse &&
                    defender.WeaponRange <= 30 && situation.FieldFallbackPosts.Contains(p.Id))
                .ThenByDescending(p => state != GarrisonState.InnerFallback && p.CoversSector(situation.ThreatenedSector))
                .ThenByDescending(p => Engagements(defender, p, situation))
                .ThenByDescending(p => p.Mortar && defender.CanOperateMortar)
                .ThenByDescending(p => p.HasCover)
                .ThenBy(p => defender.CurrentCell.HasValue ? Manhattan(defender.CurrentCell.Value, p.Cell) : 0)
                .ThenBy(p => RangePenalty(defender, p))
                .ThenBy(p => p.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static int Engagements(DefenderCandidate defender, DefensivePost post, GarrisonSituation situation)
        {
            if (post.Mortar || defender.WeaponClass == DefenderWeaponClass.Melee || situation.HostileCells.Count == 0)
                return 0;
            int maximum = Math.Min(defender.WeaponRange, post.MaximumRange);
            if (maximum < post.MinimumRange) return 0;
            return situation.HostileCells.Count(target => P4BFireLane.CanEngage(post.Cell, target,
                post.MinimumRange, maximum, situation.OpaqueCells));
        }

        private static int Manhattan(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

        private static bool Compatible(DefenderCandidate defender, DefensivePost post)
        {
            if (!post.IsValid) return false;
            if (post.Mortar) return defender.CanOperateMortar;
            if (defender.WeaponClass == DefenderWeaponClass.Melee) return post.MaximumRange <= 3;
            return defender.WeaponRange >= post.MinimumRange;
        }

        private static int RangePenalty(DefenderCandidate defender, DefensivePost post)
        {
            int ideal = (post.MinimumRange + post.MaximumRange) / 2;
            return Math.Abs(defender.WeaponRange - ideal);
        }

        private static GarrisonAssignment PostAssignment(DefenderCandidate defender, DefensivePost post,
            GarrisonState state, int tick, string reason)
        {
            GarrisonRole role = post.Mortar ? GarrisonRole.MortarOperator :
                state == GarrisonState.InnerFallback ? GarrisonRole.InnerFallback :
                defender.WeaponClass == DefenderWeaponClass.Melee ? GarrisonRole.GateReserve : GarrisonRole.RangedPost;
            return new GarrisonAssignment(defender.Id, role, post.SectorId, post.Id, post.Cell, tick, reason);
        }
    }
}
