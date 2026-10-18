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
        Run("defense capacity: broad fixed bases hold the ranged pack on every exposed side", DefenseCapacityFlat, 5);
        Run("defense access: an arrival gate may share only its open crossing with a post", DefenseArrivalGateCrossing);
        Run("defense access: one sally port may serve opposite nest flanks", DefenseSharedSallyPort);
        Run("defense access: compound crossings share a total twelve-gate budget", DefenseGateTotalCap);
        Run("defense allocation: a sole viable front can exceed three crossings", DefenseGateTotalBudgetAllocation, 5);
        Run("defense selection: compound coverage outranks field-only capacity and approach samples",
            CompoundCoverageCandidatePriority);
        Run("defense selection: a shortened nest cannot reuse tail-slot compound coverage",
            CompoundCoverageCacheTracksNestGeometry);
        Run("wall coverage metric: attacker flood cannot cut a blocked corner", WallCoverageFloodBlockedCorner);
        Run("P3 garrison keeps excess ranged defenders at safe reserve when frontage is bounded",
            P3NarrowFrontCapacityFallback);
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            Run("defense capacity: twelve residents, seed " + seed, () => DefenseCapacityFlatLarge(seed), 60);
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
            True(result.Diagnostics.Any(d => d.Stage == "defense_front" &&
                d.Code == (side.Positions.Count >= defense.Pack ? "side_capacity" : "side_capacity_short") &&
                d.Detail == side.Id + "=" + side.Positions.Count + "/" + defense.Pack), name + " truthfully logs " + side.Id + " capacity");
        Console.WriteLine("  " + name + " pack=" + defense.Pack + " posts=" + defense.Posts.Count + " cover=" + defense.CoverCells.Count() +
            " sides=" + string.Join(",", defense.Sides.Select(s => s.Id + (s.Exposed ? "=" + s.Positions.Count : ":closed"))));
    }

    private static void PrintCapacityByWallScope(ProceduralLayoutInput input, ProceduralLayoutResult result, string name)
    {
        ProceduralLayout layout = result.Layout;
        P4BApproachDefensePlan defense = layout.ApproachDefense;
        HashSet<GridPoint> field = new HashSet<GridPoint>(layout.FieldWall?.Walls ?? new GridPoint[0]);
        HashSet<GridPoint> perimeter = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
            .Concat(layout.OuterWalls).Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(field));
        HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
        foreach (P4BAttackSide side in defense.Sides.Where(s => s.Exposed))
        {
            HashSet<GridPoint> compoundTargets = new HashSet<GridPoint>(perimeter.Where(w => !field.Contains(w))
                .Select(w => new GridPoint(w.X + side.Outward.X, w.Z + side.Outward.Z))
                .Where(defense.AttackerCanReach));
            HashSet<GridPoint> fieldTargets = new HashSet<GridPoint>(field
                .Select(w => new GridPoint(w.X + side.Outward.X, w.Z + side.Outward.Z))
                .Where(defense.AttackerCanReach));
            int compoundSlots = side.Positions.Count(p => compoundTargets.Any(t =>
                P4BFireLane.CanEngage(p, t, 0, P4BCoverNest.MaximumRange, opaque)));
            int fieldSlots = side.Positions.Count(p => fieldTargets.Any(t =>
                P4BFireLane.CanEngage(p, t, 0, P4BCoverNest.MaximumRange, opaque)));
            int slotsWithoutReturns = defense.Posts.SelectMany(post => post.Slots.Where(slot =>
                side.Faces.Any(face => Math.Abs(face.X - slot.X) + Math.Abs(face.Z - slot.Z) <= 6) &&
                side.Samples.Count(target => P4BFireLane.CanEngage(slot, target,
                    P4BCoverNest.MinimumRange, P4BCoverNest.MaximumRange, opaque)) >= P4BCoverNest.MinimumShots &&
                P4BCoverNest.Shielded(slot, side.Samples.Where(target =>
                    P4BFireLane.CanEngage(slot, target, 0, P4BCoverNest.ThreatRange, opaque)),
                    post.CoverCells, opaque.Contains))).Distinct().Count();
            Console.WriteLine("  wall-scope " + name + " side=" + side.Id + " compoundTargets=" + compoundTargets.Count +
                " openArea=" + side.OpenGroundArea + " fieldTargets=" + fieldTargets.Count + " compoundSlots=" + compoundSlots +
                " fieldSlots=" + fieldSlots + " totalSlots=" + side.Positions.Count +
                " slotsWithoutReturns=" + slotsWithoutReturns +
                " gateCandidates=" + defense.CompoundGateCandidates[P4BApproachDefensePlanner.SideIndex(side.Outward)] +
                " crossings=" + P4BApproachDefensePlanner.CompoundGateCrossings(layout)[P4BApproachDefensePlanner.SideIndex(side.Outward)] +
                " compoundPorts=" + string.Join(",", defense.SallyPorts.Where(p => !p.Field && p.Outward.Equals(side.Outward))
                    .Select(p => p.Cell)) +
                " arc=" + P4BApproachDefensePlanner.CoveredSamples(side, opaque) + "/" + side.Samples.Count +
                " posts=" + string.Join(";", defense.Posts.Where(p => p.Slots.Any(side.Positions.Contains))
                    .Select(p => p.AccessGate + "/" + p.Outward.X + "," + p.Outward.Z + "/" + p.Slots.Count +
                        "[" + string.Join(",", p.Slots.Where(side.Positions.Contains)) + "]")));
        }
    }

    private static void DefenseCapacityFlat()
    {
        ProceduralLayoutInput input = Input(Plains(160), LayoutRequirements.FromPopulation(6, Industrial, Temperate), 1,
            new LayoutFortificationReservation(1));
        ProceduralLayout geometry = DefenseRectangle(input);
        P4BApproachDefensePlan defense = P4BApproachDefensePlanner.Plan(input, geometry);
        Console.WriteLine("  broad-fixed pack=" + defense.Pack + " sides=" +
            string.Join(",", defense.Sides.Select(side => side.Id + (side.Exposed ? "=" + side.Positions.Count : ":closed"))));
        True(defense.Sides.Where(side => side.Exposed).All(side => side.Positions.Count >= defense.Pack),
            "each broad fixed exposed side has enough unique positions for the ranged pack");
        Equal(0, P4BApproachDefensePlanner.Validate(input, geometry.WithApproachDefense(input, defense)).Count,
            "the broad fixed full-pack plan validates");
    }

    // Generated fronts can be constrained by terrain, neighboring field walls,
    // and the explicit total compound crossing budget. Check real compound fire,
    // truthful omissions and the physical entrance limits without pretending
    // every compass side can hold the whole roster.
    private static void DefenseCapacityFlatLarge(ulong seed)
    {
        ProceduralLayoutInput input = Input(Plains(180), LayoutRequirements.FromPopulation(12, Industrial, Temperate), seed,
            new LayoutFortificationReservation(1));
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        watch.Stop();
        Accepted(result);
        PrintTurretBatteryDiagnostics(input, result.Layout, "total12-seed" + seed,
            watch.ElapsedMilliseconds, result.Diagnostics);
        PrintPowerLedgerDiagnostics(input, result.Layout, "total12-seed" + seed);
        AssertCapacityDiagnostics(result, "flat-12-seed" + seed);
        PrintCapacityByWallScope(input, result, "flat-12-seed" + seed);
        if (seed == 1)
        {
            SettlementLoadout loadout = LayoutLoadoutModel.Build(input, result.Layout);
            Console.WriteLine("  ranged-weapon-ranges seed1=" + string.Join(";", loadout.Residents
                .Where(r => r.Weapon != null && input.Catalog.TryGet(r.Weapon.DefName, out CatalogDef weapon) &&
                    weapon.MaximumRange > 0)
                .Select(r =>
                {
                    input.Catalog.TryGet(r.Weapon.DefName, out CatalogDef weapon);
                    return r.Weapon.DefName + "=" + weapon.MinimumRange + ".." + weapon.MaximumRange;
                })));
        }
        if (seed == 1) ExportLayout("flat-12-seed1", result, input.Terrain);
        if (seed == 3) ExportLayout("posts-flat-12", result, input.Terrain);
        P4BApproachDefensePlan defense = result.Layout.ApproachDefense;
        AssertGeneratedNarrowFrontContract(input, result, "flat-12-seed" + seed);
    }

    private static void AssertGeneratedNarrowFrontContract(ProceduralLayoutInput input,
        ProceduralLayoutResult result, string name)
    {
        ProceduralLayout layout = result.Layout;
        P4BApproachDefensePlan defense = layout.ApproachDefense;
        int[] mainCrossings = P4BApproachDefensePlanner.CompoundGateCrossings(layout);
        int[] fieldCrossings = P4BApproachDefensePlanner.FieldGateCrossings(layout);
        True(mainCrossings.Sum() <= P4BApproachDefensePlanner.MaxCompoundGateCrossingsTotal,
            name + " respects the total twelve-logical-crossing compound budget including existing gates");
        True(fieldCrossings.All(count => count <= P4BApproachDefensePlanner.MaxFieldGateCrossingsPerSide),
            name + " respects the one-logical-crossing field cap including its existing gates");
        Equal(defense.Posts.SelectMany(post => post.Slots).Count(),
            defense.Posts.SelectMany(post => post.Slots).Distinct().Count(),
            name + " gives each physical post slot to one post");
        True(defense.CompoundTargets > 0 && defense.CompoundTargetsCovered > 0,
            name + " reports reachable main-compound target coverage");
        WallCoverageMetrics finalMetrics = WallCoverageMetrics.Measure(input, layout);
        True(finalMetrics.CompoundUsefulSlots > 0,
            name + " retains compound coverage under an independent final-boundary metric-v4 flood");

        HashSet<GridPoint> field = new HashSet<GridPoint>(layout.FieldWall?.Walls ?? new GridPoint[0]);
        HashSet<GridPoint> walls = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
            .Concat(layout.OuterWalls).Concat(layout.Gates.SelectMany(g => g.BandWalls)));
        HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
        SettlementLoadout loadout = LayoutLoadoutModel.Build(input, layout);
        List<CatalogDef> weapons = loadout.Residents.Where(r => r.Weapon != null)
            .Select(r => input.Catalog.TryGet(r.Weapon.DefName, out CatalogDef weapon) ? weapon : null)
            .Where(weapon => weapon != null && weapon.MaximumRange > 0).Distinct().ToList();
        HashSet<GridPoint> compoundPositions = new HashSet<GridPoint>();
        foreach (P4BAttackSide side in defense.Sides.Where(side => side.Exposed))
        {
            True(result.Diagnostics.Any(d => d.Stage == "defense_front" && d.Code.StartsWith("side_capacity") &&
                d.Detail == side.Id + "=" + side.Positions.Count + "/" + defense.Pack),
                name + " reports the actual " + side.Id + " capacity");
            HashSet<GridPoint> compoundTargets = new HashSet<GridPoint>(side.Faces.Where(face => !field.Contains(face))
                .Select(face => new GridPoint(face.X + side.Outward.X, face.Z + side.Outward.Z))
                .Where(defense.AttackerCanReach));
            foreach (GridPoint position in side.Positions)
            {
                bool canProtectCompound = compoundTargets.Any(target => weapons.Any(weapon =>
                    P4BFireLane.CanEngage(position, target, (int)Math.Ceiling(weapon.MinimumRange),
                        (int)Math.Floor(weapon.MaximumRange), opaque)));
                if (canProtectCompound) compoundPositions.Add(position);
            }
        }
        True(compoundPositions.Count > 0,
            name + " credits at least one weapon-compatible position against compound faces, not field-only targets");
        HashSet<GridPoint> allCompoundTargets = new HashSet<GridPoint>(walls.Where(wall => !field.Contains(wall))
            .SelectMany(wall => new[] { new GridPoint(wall.X + 1, wall.Z), new GridPoint(wall.X - 1, wall.Z),
                new GridPoint(wall.X, wall.Z + 1), new GridPoint(wall.X, wall.Z - 1) })
            .Where(defense.AttackerCanReach));
        foreach (P4BSallyPort port in defense.SallyPorts.Where(port => port.Field))
        {
            P4BPost[] portPosts = defense.Posts.Where(post => post.AccessGate.Equals(port.Outer)).ToArray();
            True(portPosts.Any(post => post.Slots.Any(position => allCompoundTargets.Any(target => weapons.Any(weapon =>
                P4BFireLane.CanEngage(position, target, (int)Math.Ceiling(weapon.MinimumRange),
                    (int)Math.Floor(weapon.MaximumRange), opaque))))),
                name + " only spends a field crossing when its post can fire on compound targets");
        }
        Console.WriteLine("  narrow-front " + name + " pack=" + defense.Pack + " compoundCovered=" +
            defense.CompoundTargetsCovered + "/" + defense.CompoundTargets + " compoundPositions=" + compoundPositions.Count +
            " metricV4Compound=" + (finalMetrics.CompoundEdges - finalMetrics.CompoundUncovered) + "/" +
            finalMetrics.CompoundEdges + " bySide=" + string.Join(",", new[] { "north", "east", "south", "west" }
                .Select((side, index) => side + ":" + (finalMetrics.CompoundEdgesBySide[index] -
                    finalMetrics.CompoundUncoveredBySide[index]) + "/" + finalMetrics.CompoundEdgesBySide[index])) +
            " mainCrossings=" + string.Join(",", mainCrossings) +
            " fieldCrossings=" + string.Join(",", fieldCrossings));
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

    private static void DefenseArrivalGateCrossing()
    {
        ProceduralLayoutInput input = Input(Plains(160), LayoutRequirements.FromPopulation(6, Industrial, Temperate), 1,
            new LayoutFortificationReservation(1));
        ProceduralLayout layout = DefenseRectangle(input);
        LayoutGate gate = layout.Gates[0];
        HashSet<GridPoint> arrival = new HashSet<GridPoint>(gate.ExteriorRoute);
        List<GridPoint> samples = Enumerable.Range(8, 16).SelectMany(i => new[] { new GridPoint(100, 42 + i * 2),
            new GridPoint(105, 42 + i * 2) }).ToList();
        Func<GridPoint, IEnumerable<GridPoint>> threats = c => samples.Where(t =>
            P4BFireLane.CanEngage(c, t, P4BCoverNest.MinimumRange, P4BCoverNest.ThreatRange, new HashSet<GridPoint>()));
        Func<GridPoint, bool> wall = c => c.X == gate.Cell.X && c.Z != gate.Cell.Z;
        Func<GridPoint, bool> allowedCrossing = c => P4BApproachDefensePlanner.IsArrivalCrossingCell(c, gate.Cell,
            gate.Outward, 1, new[] { gate });
        P4BPost crossing = P4BCoverNest.Build("east", gate.Cell, gate.Outward, 1, P4BCoverNest.MaxSlots,
            threats, c => c.X > gate.Cell.X,
            c => c.X > gate.Cell.X && (!arrival.Contains(c) || allowedCrossing(c)),
            c => c.X > gate.Cell.X && !arrival.Contains(c), wall, c => false);
        True(crossing != null, "the ordinary gate can supply a usable outside post");
        True(crossing.Route.Skip(1).Take(2).Any(arrival.Contains) && crossing.Route.Skip(1).Take(2)
            .Where(arrival.Contains).All(allowedCrossing), "only the gate-axis crossing prefix is shared");
        True(!crossing.Slots.Any(arrival.Contains) && !crossing.CoverCells.Any(arrival.Contains),
            "slots and cover stay clear of the arrival route");
        True(!P4BApproachDefensePlanner.IsArrivalCrossingCell(gate.ExteriorRoute.Last(), gate.Cell,
            gate.Outward, 1, new[] { gate }), "the route beyond the two-cell crossing is not shareable");
        foreach (GridPoint blocked in crossing.Route.Skip(1).Take(2))
            True(P4BCoverNest.Build("east", gate.Cell, gate.Outward, 1, P4BCoverNest.MaxSlots,
                threats, c => c.X > gate.Cell.X,
                c => c.X > gate.Cell.X && !c.Equals(blocked) && (!arrival.Contains(c) || allowedCrossing(c)),
                c => c.X > gate.Cell.X && !arrival.Contains(c), wall, c => false) == null,
                "an obstruction at shared crossing cell " + blocked + " prevents the post route");
    }

    private static void WallCoverageFloodBlockedCorner()
    {
        TerrainSnapshot terrain = Plains(20);
        HashSet<GridPoint> blocked = new HashSet<GridPoint>
        {
            new GridPoint(4, 5), new GridPoint(5, 4), new GridPoint(6, 5), new GridPoint(5, 6)
        };
        HashSet<GridPoint> reachable = WallCoverageMetrics.FloodEdges(terrain, blocked);
        True(reachable.Contains(new GridPoint(4, 4)), "the outside side of the corner remains reachable");
        True(!reachable.Contains(new GridPoint(5, 5)), "a diagonal cannot pass between two blocked orthogonal cells");
    }

    private static void DefenseSharedSallyPort()
    {
        ProceduralLayoutInput input = Input(Plains(160), LayoutRequirements.FromPopulation(12, Industrial, Temperate), 1,
            new LayoutFortificationReservation(1));
        ProceduralLayout geometry = DefenseRectangle(input);
        P4BApproachDefensePlan defense = P4BApproachDefensePlanner.Plan(input, geometry);
        var shared = defense.Posts.GroupBy(p => new { p.AccessGate, p.Outward })
            .Where(group => defense.SallyPorts.Any(port => port.Cell.Equals(group.Key.AccessGate)))
            .SelectMany(group => group.SelectMany((a, i) => group.Skip(i + 1)
                .Where(b => a.Route.Skip(1).Take(2).SequenceEqual(b.Route.Skip(1).Take(2)) &&
                    (a.Cell.X - a.AccessGate.X) * -a.Outward.Z + (a.Cell.Z - a.AccessGate.Z) * a.Outward.X !=
                    (b.Cell.X - b.AccessGate.X) * -b.Outward.Z + (b.Cell.Z - b.AccessGate.Z) * b.Outward.X)
                .Select(b => new[] { a, b }))).FirstOrDefault();
        Console.WriteLine("  sally reuse ports=" + defense.SallyPorts.Count + " posts=" + defense.Posts.Count +
            " slots=" + defense.Posts.Sum(p => p.Slots.Count) + " shared=" +
            (shared == null ? "none" : shared[0].AccessGate + " slots=" + shared.Sum(p => p.Slots.Count) +
                " coverCells=" + shared[0].CoverCells.Intersect(shared[1].CoverCells).Count()));
        True(shared != null, "two useful nests share the same first two gate-axis cells");
        True(defense.SallyPorts.Count(p => p.Cell.Equals(shared[0].AccessGate)) == 1,
            "the shared port is counted once");
        Equal(0, P4BApproachDefensePlanner.Validate(input, geometry.WithApproachDefense(input, defense)).Count,
            "the opposite-flank posts and shared port pass the final defense validator");
        foreach (GridPoint sharedPrefixCell in shared[0].Route.Skip(1).Take(2))
            True(P4BApproachDefensePlanner.Validate(input,
                geometry.WithApproachDefense(input, defense).WithAddedObstacles(new[] { sharedPrefixCell }))
                .Contains("perimeter_post_access"), "a blocked shared crossing cell " + sharedPrefixCell + " invalidates both routes");
        Equal(0, shared[0].CoverCells.Intersect(shared[1].CoverCells).Count(),
            "the two nests share only their entrance crossing and physical door");
    }

    private static void DefenseGateTotalCap()
    {
        ProceduralLayoutInput input = Input(Plains(160), LayoutRequirements.FromPopulation(6, Industrial, Temperate), 1,
            new LayoutFortificationReservation(1));
        ProceduralLayout geometry = DefenseRectangle(input);
        P4BApproachDefensePlan planned = P4BApproachDefensePlanner.Plan(input, geometry);
        List<P4BSallyPort> ports = planned.SallyPorts.ToList();
        int total = P4BApproachDefensePlanner.CompoundGateCrossings(geometry, ports).Sum();
        int next = 0;
        while (total < P4BApproachDefensePlanner.MaxCompoundGateCrossingsTotal)
        {
            GridPoint cell = new GridPoint(100 + next * 3, 100);
            ports.Add(new P4BSallyPort(cell, new GridPoint(cell.X - 1, cell.Z), new GridPoint(1, 0),
                band: new[] { new GridPoint(cell.X + 1, cell.Z), new GridPoint(cell.X + 2, cell.Z) }));
            next++;
            total = P4BApproachDefensePlanner.CompoundGateCrossings(geometry, ports).Sum();
        }
        Equal(P4BApproachDefensePlanner.MaxCompoundGateCrossingsTotal, total,
            "ordinary arrival crossings and serial gatehouse bands count once each toward twelve");
        int east = P4BApproachDefensePlanner.SideIndex(new GridPoint(1, 0));
        Equal(1, P4BApproachDefensePlanner.CompoundGateCrossings(geometry, new[]
        {
            new P4BSallyPort(new GridPoint(110, 110), new GridPoint(109, 110), new GridPoint(1, 0),
                band: new[] { new GridPoint(111, 110), new GridPoint(112, 110) })
        })[east] - P4BApproachDefensePlanner.CompoundGateCrossings(geometry)[east],
            "three serial doors through adjacent layers add one logical crossing");
        P4BApproachDefensePlan atCap = new P4BApproachDefensePlan(new GridPoint[0], planned.Sides,
            planned.Posts, planned.TransitCells, ports, planned.CoverDefName, planned.Pack,
            planned.CompoundTargets, planned.CompoundTargetsCovered, planned.CompoundGateCandidates);
        ProceduralLayout valid = geometry.WithApproachDefense(input, atCap);
        True(!P4BApproachDefensePlanner.ValidateGateCaps(valid).Contains("compound_gate_total_cap"),
            "twelve total crossings are allowed across all sides");

        ports.Add(new P4BSallyPort(new GridPoint(100 + next * 3, 100), new GridPoint(99 + next * 3, 100),
            new GridPoint(1, 0)));
        P4BApproachDefensePlan overCap = new P4BApproachDefensePlan(new GridPoint[0], planned.Sides,
            planned.Posts, planned.TransitCells, ports, planned.CoverDefName, planned.Pack,
            planned.CompoundTargets, planned.CompoundTargetsCovered, planned.CompoundGateCandidates);
        ProceduralLayout invalid = geometry.WithApproachDefense(input, overCap);
        True(ProceduralLayoutValidator.Validate(input, invalid).Contains("compound_gate_total_cap"),
            "the final validator rejects a thirteenth logical compound crossing");
    }

    private static void DefenseGateTotalBudgetAllocation()
    {
        TerrainSnapshot terrain = Build(160, 160, (x, z) =>
            x >= 35 && x <= 124 && z >= 80 && z <= 100 || x >= 30 && x <= 129 && z < 80
                ? Ground : RockRoofed);
        ProceduralLayoutInput input = Input(terrain,
            LayoutRequirements.FromPopulation(6, Industrial, Temperate), 1, new LayoutFortificationReservation(1));
        ProceduralLayout geometry = DefenseRectangle(input, boundsOverride: new GridRect(35, 80, 90, 21),
            gateOutwardOverride: new GridPoint(0, -1));
        P4BApproachDefensePlan defense = P4BApproachDefensePlanner.Plan(input, geometry);
        int[] counts = P4BApproachDefensePlanner.CompoundGateCrossings(geometry, defense.SallyPorts);
        P4BAttackSide south = defense.Sides.Single(side => side.Id == "south");
        True(south.Exposed, "the broad southern compound wall has reachable approach ground");
        True(defense.Sides.Where(side => side.Id != "south").All(side => !side.Exposed),
            "rock-backed fronts are irrelevant and do not consume crossings");
        True(counts[P4BApproachDefensePlanner.SideIndex(new GridPoint(0, -1))] >
            P4BApproachDefensePlanner.PreferredCompoundGateCrossingsPerSide,
            "the sole useful front may receive more than the soft three-crossing preference");
        True(counts.Sum() <= P4BApproachDefensePlanner.MaxCompoundGateCrossingsTotal,
            "all selected crossings remain inside the hard twelve-gate total");
        Console.WriteLine("  one-front-allocation exposed=" + string.Join(",",
            defense.Sides.Where(side => side.Exposed).Select(side => side.Id)) + " compound=" +
            string.Join(",", counts) + " gates=" + defense.SallyPorts.Count(port => !port.Field) +
            " posts=" + defense.Posts.Count + " targets=" + defense.CompoundTargetsCovered + "/" + defense.CompoundTargets);
    }

    private static void CompoundCoverageCandidatePriority()
    {
        var fieldOnly = new { Name = "field-only", Compound = 0, Gap = 0, Ranged = 99, Spread = 1, Capacity = 4, Arc = 24, Other = 4,
            Approach = 24, Crossing = 1, Cell = new GridPoint(80, 20) };
        var main = new { Name = "main-wall", Compound = 3, Gap = 2, Ranged = 0, Spread = 0, Capacity = 1, Arc = 1, Other = 1,
            Approach = 2, Crossing = 1, Cell = new GridPoint(120, 80) };
        var ranked = P4BApproachDefensePlanner.RankCandidates(new[] { fieldOnly, main },
            c => c.Compound, c => c.Gap, c => c.Ranged, c => c.Spread, c => c.Capacity, c => c.Arc, c => c.Other,
            c => c.Approach, c => c.Crossing, c => c.Cell).ToArray();
        Equal("main-wall", ranked[0].Name,
            "marginal compound coverage and gap reduction outrank field-only ranged, capacity and arc scores");
    }

    private static void CompoundCoverageCacheTracksNestGeometry()
    {
        GridPoint gate = new GridPoint(20, 10), north = new GridPoint(0, 1);
        IEnumerable<GridPoint> threats(GridPoint slot) => new[] { new GridPoint(slot.X, slot.Z + 10) };
        P4BPost full = P4BCoverNest.Build("north", gate, north, 1, 4, threats,
            c => true, c => c.Z > 10, c => true, c => c.Z == 10 && c.X != gate.X, c => true);
        P4BPost shortened = P4BCoverNest.Build("north", gate, north, 1, 4, threats,
            c => true, c => c.Z > 10 && c.X >= 18, c => true, c => c.Z == 10 && c.X != gate.X, c => true);
        True(full != null && shortened != null && full.Slots.Count == 4 && shortened.Slots.Count == 2,
            "the same gate/flank can retain its first slot while reservations shorten the nest tail");
        Equal(full.Cell, shortened.Cell, "both nest shapes share their first-cell coordinate");
        GridPoint tailTarget = new GridPoint(1, 1), lastTarget = new GridPoint(2, 2);
        GridPoint[] targets = { tailTarget, lastTarget };
        bool CanEngage(GridPoint slot, GridPoint target) => target.Equals(tailTarget) ? slot.X <= 17 : slot.X <= 16;
        Dictionary<P4BPost, HashSet<GridPoint>> cache = new Dictionary<P4BPost, HashSet<GridPoint>>();
        HashSet<GridPoint> fullCoverage = P4BApproachDefensePlanner.MainTargetCoverage(full, targets, CanEngage, cache);
        HashSet<GridPoint> shortCoverage = P4BApproachDefensePlanner.MainTargetCoverage(shortened, targets, CanEngage, cache);
        Equal(2, fullCoverage.Count, "tail-only target cells are covered by the four-slot candidate");
        Equal(0, shortCoverage.Count, "the shortened candidate loses target hits supplied by its removed tail slots");
        True(!ReferenceEquals(fullCoverage, shortCoverage), "different slot geometries never share cached coverage");
    }

    private static void P3NarrowFrontCapacityFallback()
    {
        DefenderCandidate[] defenders = Enumerable.Range(1, 10).Select(id => new DefenderCandidate(id,
            DefenderWeaponClass.ShortRanged, 25, false, false, false, false, false)).ToArray();
        DefensivePost[] posts =
        {
            new DefensivePost("west-a", "west", new GridPoint(40, 50), 8, 25, true, true, true,
                false, false, true, new[] { "west" }),
            new DefensivePost("west-b", "west", new GridPoint(40, 54), 8, 25, true, true, true,
                false, false, true, new[] { "west" })
        };
        GridPoint support = new GridPoint(50, 50), reserve = new GridPoint(52, 50);
        GarrisonPlan plan = GarrisonDoctrine.Assign(new GarrisonSituation(1000, defenders, posts,
            hostileThreat: true, threatenedSector: "west", gateBreached: false, outerDefenseViable: true,
            needsRouteSafe: true, supportCenter: support, innerFallback: new GridPoint(49, 49),
            routineSupportWorkers: 0, reserveStage: reserve, assaultSignal: true));
        Equal(GarrisonState.HoldSectors, plan.State, "a bounded but viable west frontage holds its sector");
        Equal(10, plan.Assignments.Count, "the whole ranged roster retains one assignment each");
        Equal(2, plan.Assignments.Count(a => a.Role == GarrisonRole.RangedPost),
            "only the two physically available west positions receive post assignments");
        Equal(2, plan.Assignments.Where(a => a.PostId.Length > 0).Select(a => a.PostId).Distinct().Count(),
            "one defender occupies each real post without duplicates");
        Equal(8, plan.Assignments.Count(a => a.Role == GarrisonRole.GateReserve && a.PostId.Length == 0),
            "the remaining ranged defenders stage as reserves instead of receiving fictitious posts");
        True(plan.Assignments.Where(a => a.Role == GarrisonRole.GateReserve)
            .All(a => a.Destination.Equals(support) || a.Destination.Equals(reserve)),
            "reserves stay at the authorized safe staging cells");
    }
}
