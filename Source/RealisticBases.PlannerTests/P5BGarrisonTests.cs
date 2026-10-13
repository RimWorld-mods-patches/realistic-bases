using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static class P5BGarrisonTests
{
    // A caravan that has arrived but not acted: a third of the pack watches the
    // likely side, the mortar is crewed and everyone else is off duty.
    public static void WatchWithoutAction()
    {
        GarrisonPlan plan = GarrisonDoctrine.Assign(Situation(1000, Pack(9), "west", assault: false));
        Equal(GarrisonState.Watch, plan.State, "hostiles without action keep the watch");
        List<GarrisonAssignment> watch = plan.Assignments.Where(a => a.Role == GarrisonRole.RangedPost).ToList();
        Equal(3, watch.Count, "watch of a third of nine");
        Check(watch.All(a => a.PostId.StartsWith("west-")), "watchers cover the likely side");
        Equal(1, plan.Assignments.Count(a => a.Role == GarrisonRole.MortarOperator), "mortar crewed in watch");
        Equal(5, plan.Assignments.Count(a => a.Role == GarrisonRole.OffDuty), "the rest are off duty");
        Equal(2, GarrisonDoctrine.Assign(Situation(1000, Pack(4), "west", assault: false, mortar: false))
            .Assignments.Count(a => a.Role == GarrisonRole.RangedPost), "watch of at least two");
        GarrisonPlan routine = GarrisonDoctrine.Assign(Situation(1000, Pack(9), "west", hostile: false));
        Equal(GarrisonState.Routine, routine.State, "no hostiles is routine");
        Equal(1, routine.Assignments.Count(a => a.Role == GarrisonRole.RangedPost), "one routine sentry");
        Equal(0, routine.Assignments.Count(a => a.Role == GarrisonRole.MortarOperator), "no routine mortar crew");
    }

    public static void FirstShotAlarmsThePack()
    {
        GarrisonPlan plan = GarrisonDoctrine.Assign(Situation(1000, Pack(9), "west", assault: true, mortar: false));
        Equal(GarrisonState.HoldSectors, plan.State, "the first hostile action starts the assault");
        Check(plan.Assignments.All(a => a.Role == GarrisonRole.RangedPost ||
            a.Reason == "funded_mobile_reserve_staging"), "everyone is alarmed: posts or the funded reserve");
        Check(plan.Assignments.Where(a => a.PostId.Length > 0).All(a => a.PostId.StartsWith("west-")),
            "the whole pack goes to the attacked side");
    }

    public static void RedeployFollowsTheAttack()
    {
        P3SectorDwell dwell = new P3SectorDwell();
        Equal("west", dwell.Observe("west", 1000), "first attacked side is taken at once");
        Equal("west", dwell.Observe("north", 1100), "a new side waits for the dwell");
        Equal("west", dwell.Observe("", 1200), "no ranked side keeps the held one");
        Equal("west", dwell.Observe("north", 1399), "dwell not reached");
        Equal("north", dwell.Observe("north", 1400), "the attacked side persisted for the dwell");
        Equal("north", dwell.Observe("west", 1500), "the old side must persist again to return");

        GarrisonPlan west = GarrisonDoctrine.Assign(Situation(1000, Pack(9), "west", assault: true, mortar: false));
        List<DefenderCandidate> posted = Pack(9).Select(d =>
        {
            GarrisonAssignment a = west.Assignments.Single(x => x.DefenderId == d.Id);
            return new DefenderCandidate(d.Id, d.WeaponClass, d.WeaponRange, false, false, false, false, false,
                a.PostId, a.Role, 1390);
        }).ToList();
        GarrisonPlan north = GarrisonDoctrine.Assign(Situation(1400, posted, "north", assault: true, mortar: false));
        Check(north.Assignments.Where(a => a.PostId.Length > 0).All(a => a.PostId.StartsWith("north-")),
            "every posted defender leaves the old side inside the reassignment cooldown");
        Equal(west.Assignments.Count(a => a.PostId.Length > 0), north.Assignments.Count(a => a.PostId.Length > 0),
            "the whole pack moves");
        List<DefenderCandidate> settled = Pack(9).Select(d =>
        {
            GarrisonAssignment a = north.Assignments.Single(x => x.DefenderId == d.Id);
            return new DefenderCandidate(d.Id, d.WeaponClass, d.WeaponRange, false, false, false, false, false,
                a.PostId, a.Role, 1400, new GridPoint(0, 0));
        }).ToList();
        GarrisonPlan again = GarrisonDoctrine.Assign(new GarrisonSituation(2000, settled,
            Situation(2000, settled, "north", assault: true, mortar: false).Posts, true, "north", false, true, true,
            new GridPoint(14, 14), new GridPoint(15, 16), hostileCells: new[] { new GridPoint(19, 40) }));
        Check(north.Assignments.Where(a => a.PostId.Length > 0).All(a =>
            again.Assignments.Single(x => x.DefenderId == a.DefenderId).PostId == a.PostId),
            "defenders on the attacked side keep their posts after the cooldown");
    }

    public static void SleepRotationIsCapped()
    {
        List<DefenderCandidate> tired = Enumerable.Range(1, 9).Select(id => new DefenderCandidate(id,
            DefenderWeaponClass.LongRanged, 30, false, false, false, true, false, restLevel: id / 10f)).ToList();
        GarrisonPlan night = GarrisonDoctrine.Assign(Situation(1000, tired, "west", hostile: false));
        List<int> sleepers = night.Assignments.Where(a => a.Role == GarrisonRole.NeedsRelief).Select(a => a.DefenderId).ToList();
        Equal(4, sleepers.Count, "half of the eight off-duty defenders sleep");
        // Seven defenders in Watch: one support worker and three watchers leave
        // three off duty, but a third of the pack (three) may sleep.
        List<DefenderCandidate> crewed = tired.Take(7).Select(d => new DefenderCandidate(d.Id, d.WeaponClass,
            d.WeaponRange, false, d.Id == 1, false, d.Id != 1, false, restLevel: d.RestLevel)).ToList();
        Equal(3, GarrisonDoctrine.Assign(Situation(1000, crewed, "west", assault: false, mortar: false))
            .Assignments.Count(a => a.Role == GarrisonRole.NeedsRelief),
            "a third of the pack may sleep even when the watch leaves fewer off duty");
        Check(sleepers.SequenceEqual(new[] { 1, 2, 3, 4 }), "the most tired off-duty defenders sleep first");
        Equal(9, night.Assignments.Single(a => a.Role == GarrisonRole.RangedPost).DefenderId,
            "the most rested defender takes the watch");

        List<DefenderCandidate> continuing = tired.Select(d => new DefenderCandidate(d.Id, d.WeaponClass, d.WeaponRange,
            false, false, false, true, false, null, d.Id == 8 ? GarrisonRole.NeedsRelief : (GarrisonRole?)null,
            restLevel: d.Id == 8 ? 0.35f : d.RestLevel)).ToList();
        Check(GarrisonDoctrine.Assign(Situation(1000, continuing, "west", hostile: false)).Assignments
            .Single(a => a.DefenderId == 8).Role == GarrisonRole.NeedsRelief, "a sleeper keeps the bed within the band");
        List<DefenderCandidate> yielding = continuing.Select(d => new DefenderCandidate(d.Id, d.WeaponClass, d.WeaponRange,
            false, false, false, true, false, null, d.PreviousRole, restLevel: d.Id == 8 ? 0.8f : d.RestLevel)).ToList();
        Check(GarrisonDoctrine.Assign(Situation(1000, yielding, "west", hostile: false)).Assignments
            .Single(a => a.DefenderId == 8).Role == GarrisonRole.OffDuty, "a nearly rested sleeper yields to the exhausted");

        List<DefenderCandidate> allTired = Enumerable.Range(1, 4).Select(id => new DefenderCandidate(id,
            DefenderWeaponClass.LongRanged, 30, false, false, false, true, false, id == 1 ? "west-0" : null,
            id == 1 ? GarrisonRole.RangedPost : (GarrisonRole?)null, restLevel: id == 1 ? 0.06f : 0.2f + id / 100f)).ToList();
        Equal(4, GarrisonDoctrine.Assign(Situation(1000, allTired, "west", hostile: false)).Assignments
            .Single(a => a.Role == GarrisonRole.RangedPost).DefenderId, "with everyone tired the most rested watches");

        List<DefenderCandidate> rotation = Enumerable.Range(1, 4).Select(id => new DefenderCandidate(id,
            DefenderWeaponClass.LongRanged, 30, false, false, false, id == 1, false, id == 1 ? "west-0" : null,
            id == 1 ? GarrisonRole.RangedPost : (GarrisonRole?)null, restLevel: id == 1 ? 0.2f : 0.9f)).ToList();
        GarrisonPlan swapped = GarrisonDoctrine.Assign(Situation(1000, rotation, "west", hostile: false));
        Equal(GarrisonRole.NeedsRelief, swapped.Assignments.Single(a => a.DefenderId == 1).Role,
            "a tired watcher is relieved");
        Equal(1, swapped.Assignments.Count(a => a.Role == GarrisonRole.RangedPost), "a rested defender takes over the watch");

        List<DefenderCandidate> rested = rotation.Select(d => new DefenderCandidate(d.Id, d.WeaponClass, d.WeaponRange,
            false, false, false, false, false, d.PreviousPostId, d.PreviousRole, restLevel: d.Id == 1 ? 0.6f : 0.9f)).ToList();
        Equal(1, GarrisonDoctrine.Assign(Situation(1000, rested, "west", hostile: false)).Assignments
            .Single(a => a.Role == GarrisonRole.RangedPost).DefenderId, "a watcher who needs no relief keeps the watch");
    }

    // Seven defenders in Watch for three days with vanilla-like rest rates:
    // awake drains rest in about 26 game hours, a bed restores it in about
    // 10.5. The rotation must keep everyone clear of exhaustion.
    public static void RestRotationSustainsWatch()
    {
        const float drain = 1f / 63000f, gain = 1f / 26250f;
        const int step = 120;
        Dictionary<int, float> rest = Enumerable.Range(1, 7).ToDictionary(id => id, id => 0.2f + id * 0.05f);
        Dictionary<int, GarrisonAssignment> last = new Dictionary<int, GarrisonAssignment>();
        float lowest = 1f;
        int wakings = 0;
        for (int tick = 0; tick < 180000; tick += step)
        {
            List<DefenderCandidate> roster = rest.Select(r =>
            {
                last.TryGetValue(r.Key, out GarrisonAssignment previous);
                bool resting = previous?.Role == GarrisonRole.NeedsRelief;
                return new DefenderCandidate(r.Key, DefenderWeaponClass.LongRanged, 30, true, r.Key == 1, false,
                    r.Value < 0.35f || resting && r.Value < 0.95f, false, previous?.PostId, previous?.Role,
                    previous?.AssignedTick ?? int.MinValue, restLevel: r.Value);
            }).ToList();
            GarrisonPlan plan = GarrisonDoctrine.Assign(Situation(tick, roster, "west", assault: false));
            foreach (GarrisonAssignment a in plan.Assignments)
            {
                bool asleep = a.Role == GarrisonRole.NeedsRelief;
                if (last.TryGetValue(a.DefenderId, out GarrisonAssignment previous) &&
                    previous.Role == GarrisonRole.NeedsRelief && !asleep && rest[a.DefenderId] < 0.95f) wakings++;
                rest[a.DefenderId] = Math.Max(0f, Math.Min(1f, rest[a.DefenderId] + (asleep ? gain : -drain) * step));
                last[a.DefenderId] = a;
            }
            if (tick > 30000) lowest = Math.Min(lowest, rest.Values.Min());
        }
        Check(lowest > 0.1f, "the rotation keeps every defender above 10% rest, lowest=" + lowest);
        Check(wakings < 60, "sleepers are not woken back and forth, wakings=" + wakings);
    }

    public static void NoRestReliefUnderAssault()
    {
        List<DefenderCandidate> tired = Enumerable.Range(1, 6).Select(id => new DefenderCandidate(id,
            DefenderWeaponClass.LongRanged, 30, false, false, false, true, id == 6, restLevel: 0.2f)).ToList();
        GarrisonPlan plan = GarrisonDoctrine.Assign(Situation(1000, tired, "west", assault: true, mortar: false));
        Equal(6, plan.Assignments.Single(a => a.Role == GarrisonRole.NeedsRelief).DefenderId,
            "only the wounded defender is relieved under assault");
    }

    public static void AssaultLatch()
    {
        Equal(GarrisonState.HoldSectors, GarrisonDoctrine.Assign(Situation(5000, Pack(9), "west", assault: false,
            lastAssaultTick: 5000 - GarrisonDoctrine.AssaultLatchTicks + 1)).State, "the assault holds inside the latch");
        Equal(GarrisonState.Watch, GarrisonDoctrine.Assign(Situation(5000, Pack(9), "west", assault: false,
            lastAssaultTick: 5000 - GarrisonDoctrine.AssaultLatchTicks)).State, "an hour without a signal falls back to watch");
        Equal(GarrisonState.Routine, GarrisonDoctrine.Assign(Situation(5000, Pack(9), "west", hostile: false,
            lastAssaultTick: 4999)).State, "no hostiles left is routine");
    }

    private static List<DefenderCandidate> Pack(int count) => Enumerable.Range(1, count).Select(id =>
        new DefenderCandidate(id, DefenderWeaponClass.LongRanged, 30, true, false, false, false, false)).ToList();

    private static GarrisonSituation Situation(int tick, IEnumerable<DefenderCandidate> defenders, string sector,
        bool hostile = true, bool assault = false, bool mortar = true, int lastAssaultTick = int.MinValue)
    {
        IEnumerable<DefensivePost> posts = Enumerable.Range(0, 10).Select(i => new DefensivePost("west-" + i, "west",
                new GridPoint(0, 10 + i), 8, 36, true, true, true, false, false, true))
            .Concat(Enumerable.Range(0, 10).Select(i => new DefensivePost("north-" + i, "north",
                new GridPoint(10 + i, 30), 8, 36, true, true, true, false, false, true)));
        if (mortar) posts = posts.Concat(new[] { new DefensivePost("mortar", "center", new GridPoint(15, 15), 0, 100,
            false, true, true, true, false, true) });
        return new GarrisonSituation(tick, defenders, posts, hostile, sector, false, true, true,
            new GridPoint(14, 14), new GridPoint(15, 16), assaultSignal: assault, lastAssaultTick: lastAssaultTick);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(message + " expected=" + expected + " actual=" + actual);
    }
}
