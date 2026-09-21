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
        InnerFallback
    }

    public enum GarrisonRole
    {
        RangedPost,
        GateReserve,
        MortarOperator,
        Support,
        NeedsRelief,
        InnerFallback
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
            bool hasCover, bool hasLineOfFire, bool routeWithinBounds, bool mortar, bool inner, bool usable)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(sectorId))
                throw new ArgumentException("Post and sector ids are required.");
            if (minimumRange < 0 || maximumRange < minimumRange) throw new ArgumentOutOfRangeException(nameof(maximumRange));
            Id = id;
            SectorId = sectorId;
            Cell = cell;
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
        public GridPoint Cell { get; }
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

    public sealed class DefenderCandidate
    {
        public DefenderCandidate(int id, DefenderWeaponClass weaponClass, int weaponRange,
            bool canOperateMortar, bool canSupport, bool needsFood, bool needsRest, bool needsTending,
            string previousPostId = null, GarrisonRole? previousRole = null, int lastAssignmentTick = int.MinValue)
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
        }

        public int Id { get; }
        public DefenderWeaponClass WeaponClass { get; }
        public int WeaponRange { get; }
        public bool CanOperateMortar { get; }
        public bool CanSupport { get; }
        public bool NeedsFood { get; }
        public bool NeedsRest { get; }
        public bool NeedsTending { get; }
        public string PreviousPostId { get; }
        public GarrisonRole? PreviousRole { get; }
        public int LastAssignmentTick { get; }
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
            bool needsRouteSafe, GridPoint supportCenter, GridPoint innerFallback, int routineSupportWorkers = 1)
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
    }

    public static class GarrisonDoctrine
    {
        public const int ReassignmentCooldownTicks = 300;

        public static GarrisonPlan Assign(GarrisonSituation situation)
        {
            if (situation == null) throw new ArgumentNullException(nameof(situation));
            GarrisonState state = SelectState(situation);
            List<DefensivePost> validPosts = situation.Posts.Where(p => p.IsValid &&
                (state == GarrisonState.InnerFallback ? p.Inner : !p.Inner)).ToList();
            if (state != GarrisonState.Routine && validPosts.Count == 0) state = GarrisonState.InnerFallback;

            HashSet<string> occupied = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> routineSupport = new HashSet<int>(state == GarrisonState.Routine
                ? situation.Defenders.Where(d => d.CanSupport && !d.NeedsRelief)
                    .OrderBy(d => d.CanOperateMortar)
                    .ThenBy(d => d.WeaponClass == DefenderWeaponClass.LongRanged)
                    .ThenBy(d => d.WeaponRange)
                    .ThenBy(d => d.Id)
                    .Take(situation.RoutineSupportWorkers).Select(d => d.Id)
                : Enumerable.Empty<int>());
            List<GarrisonAssignment> assignments = new List<GarrisonAssignment>();
            foreach (DefenderCandidate defender in situation.Defenders)
            {
                if (MustRelieve(defender, situation, state))
                {
                    assignments.Add(new GarrisonAssignment(defender.Id, GarrisonRole.NeedsRelief, string.Empty,
                        string.Empty, situation.SupportCenter, situation.Tick, "safe_needs_relief"));
                    continue;
                }

                if (routineSupport.Contains(defender.Id))
                {
                    assignments.Add(new GarrisonAssignment(defender.Id, GarrisonRole.Support, string.Empty,
                        string.Empty, situation.SupportCenter, situation.Tick, "routine_support"));
                    continue;
                }

                DefensivePost previous = validPosts.FirstOrDefault(p => p.Id == defender.PreviousPostId &&
                    Compatible(defender, p) && !occupied.Contains(p.Id));
                bool cooldown = situation.Tick - defender.LastAssignmentTick < ReassignmentCooldownTicks;
                if (previous != null && cooldown && !MustRelieve(defender, situation, state))
                {
                    occupied.Add(previous.Id);
                    assignments.Add(PostAssignment(defender, previous, state, situation.Tick, "stable_assignment"));
                    continue;
                }

                DefensivePost selected = SelectPost(defender, validPosts, occupied, situation.ThreatenedSector, state);
                if (selected != null)
                {
                    occupied.Add(selected.Id);
                    assignments.Add(PostAssignment(defender, selected, state, situation.Tick,
                        selected.Id == defender.PreviousPostId ? "retained_valid_post" : "best_compatible_post"));
                    continue;
                }

                GarrisonRole role = state == GarrisonState.InnerFallback ? GarrisonRole.InnerFallback :
                    defender.CanSupport && state == GarrisonState.Routine ? GarrisonRole.Support : GarrisonRole.GateReserve;
                GridPoint destination = state == GarrisonState.InnerFallback ? situation.InnerFallback : situation.SupportCenter;
                assignments.Add(new GarrisonAssignment(defender.Id, role, situation.ThreatenedSector,
                    string.Empty, destination, situation.Tick, "no_compatible_post"));
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

        private static GarrisonState SelectState(GarrisonSituation situation)
        {
            if (!situation.HostileThreat) return GarrisonState.Routine;
            if (!situation.OuterDefenseViable) return GarrisonState.InnerFallback;
            if (situation.GateBreached) return GarrisonState.LocalResponse;
            return GarrisonState.HoldSectors;
        }

        private static bool MustRelieve(DefenderCandidate defender, GarrisonSituation situation, GarrisonState state)
        {
            if (!defender.NeedsRelief || !situation.NeedsRouteSafe) return false;
            int available = situation.Defenders.Count(d => !d.NeedsRelief);
            return state == GarrisonState.Routine || available >= Math.Max(1, situation.Posts.Count(p => p.IsValid && !p.Mortar) / 2);
        }

        private static DefensivePost SelectPost(DefenderCandidate defender, IEnumerable<DefensivePost> posts,
            HashSet<string> occupied, string threatenedSector, GarrisonState state)
        {
            return posts.Where(p => !occupied.Contains(p.Id) && Compatible(defender, p))
                .OrderByDescending(p => state == GarrisonState.LocalResponse && p.SectorId == threatenedSector)
                .ThenByDescending(p => p.Mortar && defender.CanOperateMortar)
                .ThenByDescending(p => p.HasCover)
                .ThenBy(p => RangePenalty(defender, p))
                .ThenBy(p => p.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

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
