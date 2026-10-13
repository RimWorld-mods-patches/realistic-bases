using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// Defense posts and threat-arc cover (docs/plans/2026-10-13-defense-posts-cover.md).
internal static partial class Program
{
    private static void RunCoverNestTests()
    {
        Run("cover nest: octants snap to the nearest of eight directions", CoverNestOctants);
        Run("cover nest: a straight front wraps every slot and keeps a wall return", CoverNestStraightFront);
        Run("cover nest: a second front adds flank cover", CoverNestCornerFront);
        Run("cover nest: blocked cover or missing wall drops outer slots", CoverNestShrinks);
        Run("cover nest: an anchor that cannot be covered is rejected", CoverNestRejectsAnchor);
        Run("defense capacity: flat bases hold the ranged pack on every exposed side", DefenseCapacityFlat);
        Run("defense capacity: twelve residents hold the pack on most seeds", DefenseCapacityFlatLarge);
        Run("defense capacity: a rock-backed side gets no posts", DefenseCapacityMountain);
        Run("defense capacity: a cramped side comes out short but is accepted", DefenseCapacityShort);
    }

    // Gate at (20,10) in a wall along z=10, facing north; flank +1 runs toward -x.
    private static readonly GridPoint NestGate = new GridPoint(20, 10);
    private static readonly GridPoint North = new GridPoint(0, 1);

    private static List<GridPoint> NorthSamples() =>
        Enumerable.Range(5, 11).SelectMany(i => new[] { new GridPoint(i * 2, 25), new GridPoint(i * 2, 30) }).ToList();

    private static P4BPost Nest(IEnumerable<GridPoint> samples, Func<GridPoint, bool> coverable = null,
        Func<GridPoint, bool> wall = null)
    {
        List<GridPoint> targets = samples.ToList();
        HashSet<GridPoint> none = new HashSet<GridPoint>();
        Func<GridPoint, IEnumerable<GridPoint>> engaged = c =>
            targets.Where(t => P4BFireLane.CanEngage(c, t, P4BCoverNest.MinimumRange, P4BCoverNest.MaximumRange, none));
        return P4BCoverNest.Build("north", NestGate, North, 1, P4BCoverNest.MaxSlots, engaged,
            c => engaged(c).Count() >= P4BCoverNest.MinimumShots, c => c.Z > 10,
            coverable ?? (c => c.Z > 10), wall ?? (c => c.Z == 10 && !c.Equals(NestGate)), c => c.Z > 10);
    }

    private static void CoverNestOctants()
    {
        GridPoint o = new GridPoint(0, 0);
        Equal(new GridPoint(1, 0), P4BCoverNest.Octant(o, new GridPoint(10, 1)), "east");
        Equal(new GridPoint(1, 1), P4BCoverNest.Octant(o, new GridPoint(10, 10)), "north-east");
        Equal(new GridPoint(0, 1), P4BCoverNest.Octant(o, new GridPoint(1, 10)), "north");
        Equal(new GridPoint(-1, 0), P4BCoverNest.Octant(o, new GridPoint(-10, -4)), "shallow line stays west");
        Equal(new GridPoint(-1, -1), P4BCoverNest.Octant(o, new GridPoint(-10, -5)), "steeper line is diagonal");
    }

    private static void CoverNestStraightFront()
    {
        P4BPost post = Nest(NorthSamples());
        True(post != null, "an open front builds a nest");
        Equal(4, post.Slots.Count, "full row of slots");
        True(post.Slots.SequenceEqual(new[] { new GridPoint(19, 12), new GridPoint(18, 12), new GridPoint(17, 12), new GridPoint(16, 12) }),
            "slots stand one cell off the wall: " + string.Join(",", post.Slots));
        True(post.CoverCells.SequenceEqual(Enumerable.Range(15, 6).Select(x => new GridPoint(x, 13))),
            "front and diagonals are covered, including the free gate axis beyond the route: " + string.Join(",", post.CoverCells));
        P4BPost busyAxis = Nest(NorthSamples(), coverable: c => c.Z > 10 && c.X != 20);
        True(busyAxis != null && busyAxis.Slots.Count == 4 && !busyAxis.CoverCells.Contains(new GridPoint(20, 13)),
            "a used gate axis stays open without shrinking the nest");
        Equal((GridPoint?)new GridPoint(15, 11), post.WallReturn, "threat-free far flank closes with a wall return");
        True(post.Route.SequenceEqual(new[] { NestGate, new GridPoint(20, 11), new GridPoint(20, 12) }.Concat(post.Slots)),
            "route crosses the gate axis and runs behind the cover");
        True(!post.Route.Intersect(post.CoverCells).Any(), "route never crosses cover");
    }

    private static void CoverNestCornerFront()
    {
        // A western front adds fire along the row: the far flank needs cover.
        List<GridPoint> samples = NorthSamples().Concat(Enumerable.Range(0, 4).Select(i => new GridPoint(2 + i, 12))).ToList();
        P4BPost post = Nest(samples);
        True(post != null && post.CoverCells.Contains(new GridPoint(15, 12)), "western threat covers the row end: " +
            (post == null ? "null" : string.Join(",", post.CoverCells)));
        Equal((GridPoint?)new GridPoint(15, 11), post.WallReturn, "the return behind it still faces no threat");
        True(!post.Route.Intersect(post.CoverCells).Any(), "route never crosses cover");
        // Fire down the row: only the end slot is shielded from the west.
        List<GridPoint> west = Enumerable.Range(0, 4).Select(i => new GridPoint(2 + i, 12)).ToList();
        Func<GridPoint, bool> solid = c => c.Z == 10 && !c.Equals(NestGate);
        True(P4BCoverNest.Shielded(new GridPoint(16, 12), west, post.CoverCells.ToList(), solid), "the row end holds the west");
        True(!P4BCoverNest.Shielded(new GridPoint(19, 12), west, post.CoverCells.ToList(), solid),
            "a slot behind its neighbours does not hold the west");
        True(post.Slots.All(c => P4BCoverNest.Shielded(c, NorthSamples(), post.CoverCells.ToList(), solid)),
            "every slot holds the north");
    }

    private static void CoverNestShrinks()
    {
        P4BPost blocked = Nest(NorthSamples(), coverable: c => c.Z > 10 && !c.Equals(new GridPoint(15, 13)));
        Equal(3, blocked.Slots.Count, "a blocked outer cover cell drops the outermost slot");
        Equal((GridPoint?)new GridPoint(16, 11), blocked.WallReturn, "the return follows the shorter row");
        P4BPost noWall = Nest(NorthSamples(), wall: c => c.Z == 10 && !c.Equals(NestGate) && !c.Equals(new GridPoint(17, 10)));
        Equal(2, noWall.Slots.Count, "slots stay backed by the wall");
        Equal((GridPoint?)null, noWall.WallReturn, "no return where its wall would not connect");
    }

    private static void CoverNestRejectsAnchor()
    {
        True(Nest(NorthSamples(), coverable: c => c.Z > 10 && !c.Equals(new GridPoint(19, 13))) == null,
            "a first slot that cannot be covered rejects the anchor");
        True(Nest(new GridPoint[0]) == null, "a post with no useful shots is rejected");
    }

    private static void AssertCapacityDiagnostics(ProceduralLayoutResult result, string name)
    {
        P4BApproachDefensePlan defense = result.Layout.ApproachDefense;
        foreach (P4BAttackSide side in defense.Sides.Where(s => s.Exposed))
            True(result.Diagnostics.Any(d => d.Stage == "defense_front" && d.Code.StartsWith("side_capacity") &&
                d.Detail == side.Id + "=" + side.Positions.Count + "/" + defense.Pack), name + " logs " + side.Id + " capacity");
        Console.WriteLine("  " + name + " pack=" + defense.Pack + " posts=" + defense.Posts.Count + " cover=" + defense.CoverCells.Count() +
            " sides=" + string.Join(",", defense.Sides.Select(s => s.Id + (s.Exposed ? "=" + s.Positions.Count : ":closed"))));
    }

    private static void DefenseCapacityFlat()
    {
        foreach (int residents in new[] { 4, 7 })
        {
            ProceduralLayoutInput input = Input(Plains(180), LayoutRequirements.FromPopulation(residents, Industrial, Temperate), 3,
                new LayoutFortificationReservation(1));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            Accepted(result);
            AssertCapacityDiagnostics(result, "flat-" + residents);
            ExportLayout("posts-flat-" + residents, result, input.Terrain);
            P4BApproachDefensePlan defense = result.Layout.ApproachDefense;
            True(defense.Pack > 0, "the loadout has ranged defenders");
            True(defense.Sides.Where(s => s.Exposed).All(s => s.Positions.Count >= defense.Pack),
                residents + " residents: every exposed side holds the pack of " + defense.Pack);
        }
    }

    // Twelve residents need ten slots per side; whether a side fits them
    // depends on the compound the seed produces. Shortfalls are logged, never
    // rejected; most seeds hold the full pack.
    private static void DefenseCapacityFlatLarge()
    {
        int full = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            ProceduralLayoutInput input = Input(Plains(180), LayoutRequirements.FromPopulation(12, Industrial, Temperate), seed,
                new LayoutFortificationReservation(1));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            Accepted(result);
            AssertCapacityDiagnostics(result, "flat-12-seed" + seed);
            if (seed == 3) ExportLayout("posts-flat-12", result, input.Terrain);
            P4BApproachDefensePlan defense = result.Layout.ApproachDefense;
            if (defense.Sides.Where(s => s.Exposed).All(s => s.Positions.Count >= defense.Pack)) full++;
        }
        True(full >= 5, "most 12-resident seeds hold the pack on every exposed side: " + full + "/8");
    }

    private static void DefenseCapacityMountain()
    {
        // Fixed compound geometry so terrain alone closes the western side.
        ProceduralLayoutInput flat = Input(Plains(160), LayoutRequirements.FromPopulation(6, Industrial, Temperate), 1,
            new LayoutFortificationReservation(1));
        ProceduralLayout rectangle = DefenseRectangle(flat);
        TerrainSnapshot rock = Build(160, 160, (x, z) => x < 50 ? RockRoofed : Ground);
        ProceduralLayoutInput input = Input(rock, flat.Requirements, 1, new LayoutFortificationReservation(1));
        P4BApproachDefensePlan defense = P4BApproachDefensePlanner.Plan(input, rectangle);
        Console.WriteLine("  rock-west pack=" + defense.Pack + " posts=" + defense.Posts.Count + " sides=" +
            string.Join(",", defense.Sides.Select(s => s.Id + (s.Exposed ? "=" + s.Positions.Count : ":closed"))));
        P4BAttackSide west = defense.Sides.Single(s => s.Id == "west");
        True(!west.Exposed && west.Positions.Count == 0 && !west.Post.HasValue, "the rock-backed side gets no posts");
        True(defense.Sides.Where(s => s.Exposed).All(s => s.Positions.Count > 0), "open sides keep their posts");
        True(defense.Posts.All(p => p.CoverCells.Concat(p.Slots).All(c => !rock[c].Has(TerrainTraits.NaturalRock))),
            "nests stand on open ground");
    }

    private static void DefenseCapacityShort()
    {
        // Fixed compound geometry: south of the wall lies unbuildable marsh that
        // attackers cross but where no cover can be built, so the
        // southern side cannot hold the pack. Independent of room packing.
        ProceduralLayoutInput flat = Input(Plains(160), LayoutRequirements.FromPopulation(6, Industrial, Temperate), 1,
            new LayoutFortificationReservation(1));
        ProceduralLayout rectangle = DefenseRectangle(flat);
        TerrainSnapshot shore = Build(160, 160, (x, z) => z < 48 ? TerrainTraits.Standable : Ground);
        ProceduralLayoutInput input = Input(shore, flat.Requirements, 1, new LayoutFortificationReservation(1));
        P4BApproachDefensePlan defense = P4BApproachDefensePlanner.Plan(input, rectangle);
        Console.WriteLine("  shore pack=" + defense.Pack + " posts=" + defense.Posts.Count + " sides=" +
            string.Join(",", defense.Sides.Select(s => s.Id + (s.Exposed ? "=" + s.Positions.Count : ":closed"))));
        P4BAttackSide south = defense.Sides.Single(s => s.Id == "south");
        True(south.Exposed && south.Positions.Count < defense.Pack, "the marsh side is exposed but cannot hold the pack");
        True(defense.Posts.All(p => p.CoverCells.All(c => shore[c].Has(TerrainTraits.Buildable))), "no cover is built on the marsh");
        Equal(0, P4BApproachDefensePlanner.Validate(input, rectangle.WithApproachDefense(input, defense)).Count, "the short plan validates");
    }
}
