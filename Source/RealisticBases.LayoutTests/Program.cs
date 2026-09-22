using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RealisticBases.Planning;

// Synthetic pure-planner evidence for the P4A foundation. Nothing here runs the
// game; results are not live-map evidence.
internal static class Program
{
    private static int failures;
    private static string exportDir;
    private static readonly SettlementCapability Industrial = P2ProfileCatalog.IndustrialTemperate.Capabilities;
    private static readonly SettlementCapability Tribal = P2ProfileCatalog.TribalTemperate.Capabilities;
    private static readonly LayoutClimate Temperate = new LayoutClimate(12, 28);
    private static readonly LayoutClimate Cold = new LayoutClimate(-20, 20);
    private static readonly LayoutClimate Hot = new LayoutClimate(18, 42);

    private static int Main(string[] args)
    {
        int export = Array.IndexOf(args, "--export");
        if (export >= 0 && export + 1 < args.Length) exportDir = args[export + 1];
        Stopwatch watch = Stopwatch.StartNew();
        Run("seed derivation is stable and purpose-separated", SeedDerivation);
        Run("demand scales with population and splits barracks", DemandScalesWithPopulation);
        Run("rich settlements get personal bedrooms", PersonalRooms);
        Run("climate drives heating, cooling and lighting fixtures", ClimateFixtures);
        Run("workbenches stand against a wall with an interior work cell", WorkbenchesAgainstWall);
        Run("identical inputs replay identical layouts", Determinism);
        Run("seeds vary internal layout beyond translation and rotation", SeedVariation);
        Run("population change alters capacity and geometry coherently", PopulationChangesGeometry);
        Run("obstacles reshape the layout", ObstaclesReshapeLayout);
        Run("final obstacles and access reservations are validated", ConnectivityAfterBlocking);
        Run("rooms are never public transit", RoomsNeverTransit);
        Run("impossible demand and unusable terrain reject predictably", Rejections);
        Run("search is bounded and inputs stay unchanged", BoundedAndImmutable);
        Run("tribal settlement digs under rock with natural roof support", RockProtectedTribal);
        Run("mortar doctrine trades protection for an open-sky court", MortarDoctrine);
        Run("roof support ignores built walls and rejects wide halls", RoofSupportRule);
        Run("gatehouse crossings reserve one to three layers", WallLayers);
        Run("adapter output passes P4 connectivity when representable", AdapterCrossCheck);
        Run("tech stage is stable per faction", TechStageStablePerFaction);
        Run("tech stage respects every faction band", TechStageBands);
        Run("tech stage research sets are monotonic", TechStageMonotonic);
        Run("tech stage adaptations and step-down", TechStageAdaptations);
        Console.WriteLine("P4A layout foundation tests: " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures +
            " elapsedMs=" + watch.ElapsedMilliseconds);
        return failures == 0 ? 0 : 1;
    }

    // ---- demand and furnishing ----

    private static void SeedDerivation()
    {
        ulong a = LayoutSeed.Derive("world", 42, "Faction_12", "industrial", 1);
        Equal(a, LayoutSeed.Derive("world", 42, "Faction_12", "industrial", 1), "stable");
        NotEqual(a, LayoutSeed.Derive("world", 42, "Faction_13", "industrial", 1), "faction participates");
        NotEqual(a, LayoutSeed.Derive("world", 43, "Faction_12", "industrial", 1), "tile participates");
        NotEqual(LayoutSeed.Sub(a, "rooms"), LayoutSeed.Sub(a, "site"), "purpose streams differ");
    }

    private static void DemandScalesWithPopulation()
    {
        int previousBeds = 0, previousStorage = 0;
        foreach (int residents in new[] { 4, 8, 16 })
        {
            LayoutDemandSet demand = LayoutDemandModel.Derive(LayoutRequirements.FromPopulation(residents, Industrial, Temperate), 7);
            Equal(0, demand.Rejections.Count, "no rejection");
            int beds = demand.Rooms.Where(r => r.Kind == LayoutRoomKind.Barracks).Sum(r => r.Capacity);
            Equal(residents, beds, "beds cover residents");
            Equal((residents + 7) / 8, demand.Rooms.Count(r => r.Kind == LayoutRoomKind.Barracks), "barracks split at 8 beds");
            int storage = demand.Rooms.Single(r => r.Kind == LayoutRoomKind.Storage).Capacity;
            True(beds > previousBeds && storage > previousStorage, "capacities grow with population");
            True(demand.Rooms.All(r => r.CandidateSizes.Count > 0), "every room has proven sizes");
            True(demand.Rooms.Any(r => r.Kind == LayoutRoomKind.Power), "industrial power room");
            True(demand.Relationships.Any(r => r.Hard), "kitchen-food hard relation");
            previousBeds = beds;
            previousStorage = storage;
        }
        LayoutDemandSet tribal = LayoutDemandModel.Derive(LayoutRequirements.FromPopulation(6, Tribal, Temperate), 7);
        True(tribal.Rooms.Any(r => r.Kind == LayoutRoomKind.Pantry) && !tribal.Rooms.Any(r => r.Kind == LayoutRoomKind.Freezer || r.Kind == LayoutRoomKind.Power),
            "tribal pantry and no power");
        LayoutDemandSet fromProfile = LayoutDemandModel.Derive(LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate), 7);
        True(fromProfile.Rooms.Single(r => r.Kind == LayoutRoomKind.Freezer).Capacity >= 24, "P2 profile capacity is a floor");
    }

    private static void PersonalRooms()
    {
        ProceduralLayoutResult result = Plan(Plains(130), LayoutRequirements.FromPopulation(5, Industrial, Cold, HousingStandard.PersonalRooms), 11);
        Accepted(result);
        List<LayoutRoom> bedrooms = result.Layout.Rooms.Where(r => r.Kind == LayoutRoomKind.Bedroom).ToList();
        Equal(5, bedrooms.Count, "one bedroom per resident");
        True(!result.Layout.Rooms.Any(r => r.Kind == LayoutRoomKind.Barracks), "no barracks");
        True(bedrooms.All(r => r.Fixtures.Count(f => f.Kind == LayoutFixtureKind.Bed) == 1 &&
            r.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Heater) && r.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Lamp)), "bed, heat and light per bedroom");
        ExportLayout("industrial-cold-personal-rooms", result);
    }

    private static void ClimateFixtures()
    {
        ProceduralLayoutResult cold = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Industrial, Cold), 3);
        Accepted(cold);
        foreach (LayoutRoom room in cold.Layout.Rooms.Where(r => LayoutDemandModel.IsLiving(r.Kind)))
            True(room.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Heater), "heater in " + room.Id);
        True(cold.Layout.Rooms.All(r => r.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Lamp)), "lamps everywhere");
        True(cold.Layout.AllFixtures.Where(f => f.Kind == LayoutFixtureKind.Battery).All(f => f.RoomId.StartsWith("power")), "batteries indoors in power room");

        ProceduralLayoutResult hotTribal = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Hot), 3);
        Accepted(hotTribal);
        foreach (LayoutRoom room in hotTribal.Layout.Rooms.Where(r => LayoutDemandModel.IsLiving(r.Kind)))
            True(room.Fixtures.Any(f => f.Kind == LayoutFixtureKind.PassiveCooler), "passive cooler in " + room.Id);
        True(hotTribal.Layout.Rooms.All(r => r.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Torch)), "torches everywhere");
        True(hotTribal.Layout.AllFixtures.All(f => f.Watts == 0), "tribal has no powered fixture");

        ProceduralLayoutResult hotIndustrial = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Industrial, Hot), 3);
        Accepted(hotIndustrial);
        foreach (LayoutRoom room in hotIndustrial.Layout.Rooms.Where(r => LayoutDemandModel.IsLiving(r.Kind)))
            True(room.Fixtures.Any(f => f.Kind == LayoutFixtureKind.WallCooler && f.Exhaust.HasValue), "wall cooler with exhaust in " + room.Id);

        ProceduralLayoutResult coldTribal = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Cold), 3);
        Accepted(coldTribal);
        foreach (LayoutRoom room in coldTribal.Layout.Rooms.Where(r => LayoutDemandModel.IsLiving(r.Kind)))
            True(room.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Campfire), "campfire in " + room.Id);

        ProceduralLayoutResult temperate = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Industrial, Temperate), 3);
        Accepted(temperate);
        True(!temperate.Layout.AllFixtures.Any(f => f.Kind == LayoutFixtureKind.Heater || (f.Kind == LayoutFixtureKind.WallCooler && f.RoomId != "freezer")),
            "temperate needs no climate fixtures");
        ExportLayout("industrial-hot-barracks", hotIndustrial);
        ExportLayout("tribal-cold-barracks", coldTribal);
    }

    private static void WorkbenchesAgainstWall()
    {
        ProceduralLayoutResult result = Plan(Plains(120), LayoutRequirements.FromPopulation(14, Industrial, Temperate), 21);
        Accepted(result);
        List<LayoutFixture> benches = result.Layout.AllFixtures.Where(f => f.Kind == LayoutFixtureKind.Stove || f.Kind == LayoutFixtureKind.Workbench).ToList();
        True(benches.Count >= 5, "several benches");
        foreach (LayoutFixture bench in benches)
        {
            LayoutRoom room = result.Layout.Rooms.Single(r => r.Id == bench.RoomId);
            GridPoint back = new GridPoint(-bench.Facing.X, -bench.Facing.Z);
            True(bench.Cells.All(c => room.IsEdge(new GridPoint(c.X + back.X, c.Z + back.Z))), bench.DefName + " back against wall");
            True(bench.Access.Count == 1 && room.Interior.Contains(bench.Access[0]), bench.DefName + " work cell inside");
            GridPoint expected = Rotate(bench);
            Equal(expected, bench.Access[0], bench.DefName + " vanilla rotation puts interaction cell on access cell");
        }
    }

    // Vanilla bench interactionCellOffset (0,0,-1) rotated by the fixture rotation, from the middle cell.
    private static GridPoint Rotate(LayoutFixture bench)
    {
        GridPoint middle = bench.Cells[bench.Cells.Count / 2];
        switch (bench.Rotation)
        {
            case "North": return new GridPoint(middle.X, middle.Z - 1);
            case "South": return new GridPoint(middle.X, middle.Z + 1);
            case "East": return new GridPoint(middle.X - 1, middle.Z);
            default: return new GridPoint(middle.X + 1, middle.Z);
        }
    }

    // ---- determinism and variation ----

    private static void Determinism()
    {
        TerrainSnapshot terrain = Plains(120);
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(6, Industrial, Temperate);
        ProceduralLayoutResult first = Plan(terrain, requirements, 99);
        ProceduralLayoutResult second = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Industrial, Temperate), 99);
        Accepted(first);
        Equal(first.Layout.Digest, second.Layout.Digest, "digest");
        Equal(LayoutAsciiExport.Render(terrain, first.Layout), LayoutAsciiExport.Render(terrain, second.Layout), "ascii");
        Equal(first.Stats.ToString(), second.Stats.ToString(), "search trace");
    }

    private static void SeedVariation()
    {
        TerrainSnapshot terrain = Plains(120);
        List<ProceduralLayout> layouts = new List<ProceduralLayout>();
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
        {
            ProceduralLayoutResult result = Plan(terrain, LayoutRequirements.FromPopulation(6, Industrial, Temperate), seed);
            Accepted(result);
            layouts.Add(result.Layout);
            if (seed <= 3) ExportLayout("plains-industrial-6-seed" + seed, result);
        }
        for (int i = 0; i < layouts.Count; i++)
            for (int j = i + 1; j < layouts.Count; j++)
                True(!RigidEquivalent(layouts[i], layouts[j]), "seeds " + (i + 1) + "/" + (j + 1) + " differ beyond rigid motion");
        int topologies = layouts.Select(Topology).Distinct().Count();
        True(topologies >= 4, "distinct door-adjacency topologies=" + topologies);
        Console.WriteLine("  seed variation: 6 layouts, pairwise non-equivalent, topologies=" + topologies +
            " roomSizeSets=" + layouts.Select(SizeSet).Distinct().Count());
    }

    private static void PopulationChangesGeometry()
    {
        TerrainSnapshot terrain = Plains(140);
        ProceduralLayoutResult small = Plan(terrain, LayoutRequirements.FromPopulation(4, Industrial, Temperate), 5);
        ProceduralLayoutResult large = Plan(terrain, LayoutRequirements.FromPopulation(16, Industrial, Temperate), 5);
        Accepted(small);
        Accepted(large);
        Func<ProceduralLayout, int> interior = l => l.Rooms.Sum(r => r.Interior.Area);
        True(large.Layout.Enclosure.Count > small.Layout.Enclosure.Count, "enclosure grows");
        True(interior(large.Layout) > interior(small.Layout) * 2, "interior area grows");
        True(large.Layout.Rooms.Count > small.Layout.Rooms.Count, "more rooms (barracks split)");
        True(large.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Stove) > small.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Stove), "more stoves");
        True(large.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Table) > small.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Table), "more tables");
        Console.WriteLine("  population 4->16: enclosure " + small.Layout.Enclosure.Count + "->" + large.Layout.Enclosure.Count +
            " rooms " + small.Layout.Rooms.Count + "->" + large.Layout.Rooms.Count + " interior " + interior(small.Layout) + "->" + interior(large.Layout));
        ExportLayout("plains-industrial-16", large);
    }

    private static void ObstaclesReshapeLayout()
    {
        TerrainSnapshot plain = Plains(120);
        TerrainSnapshot obstacles = Obstacles(120);
        ProceduralLayoutResult a = Plan(plain, LayoutRequirements.FromPopulation(6, Industrial, Temperate), 8);
        ProceduralLayoutResult b = Plan(obstacles, LayoutRequirements.FromPopulation(6, Industrial, Temperate), 8);
        Accepted(a);
        Accepted(b);
        True(!RigidEquivalent(a.Layout, b.Layout), "obstacles change internal layout, not just position");
        HashSet<GridPoint> used = new HashSet<GridPoint>(b.Layout.Rooms.SelectMany(r => r.Bounds.Points()).Concat(b.Layout.Corridors.SelectMany(c => c.Cells)));
        True(used.All(c => !obstacles[c].HasAny(TerrainTraits.Water | TerrainTraits.Occupied)), "nothing placed on water or obstacles");
        ExportLayout("obstacles-industrial-6", b);
    }

    // ---- connectivity ----

    private static void ConnectivityAfterBlocking()
    {
        TerrainSnapshot terrain = Plains(120);
        ProceduralLayoutInput input = Input(terrain, LayoutRequirements.FromPopulation(6, Industrial, Temperate), 13);
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
        Accepted(result);
        ProceduralLayout layout = result.Layout;
        Equal(0, ProceduralLayoutValidator.Validate(input, layout).Count, "accepted layout validates");
        True(layout.Gates.Count == 2, "two gates");

        LayoutRoom kitchen = layout.Rooms.Single(r => r.Kind == LayoutRoomKind.Kitchen);
        Contains(ProceduralLayoutValidator.Validate(input, layout.WithAddedObstacles(new[] { kitchen.DoorOutside })), "room_door_disconnected_kitchen");
        LayoutFixture stove = kitchen.Fixtures.First(f => f.Kind == LayoutFixtureKind.Stove);
        Contains(ProceduralLayoutValidator.Validate(input, layout.WithAddedObstacles(stove.Access)), "interaction_unreachable_kitchen_Stove");
        // Isolate the second gate's interior side: its exterior route still exists,
        // but connectivity through the outside is not accepted.
        LayoutGate second = layout.Gates[1];
        IEnumerable<GridPoint> ring = Neighbors8(second.Inside).Where(c => !c.Equals(second.Cell));
        List<string> isolated = ProceduralLayoutValidator.Validate(input, layout.WithAddedObstacles(ring)).ToList();
        True(isolated.Contains("gate1_disconnected") || isolated.Contains("gate1_vestibule_blocked"), "gate isolated inside is rejected: " + string.Join(",", isolated));
        Contains(ProceduralLayoutValidator.Validate(input, layout.WithAddedObstacles(new[] { layout.Gates[0].Inside })), "public_root_blocked");
        // Cutting the main street between rooms disconnects something the validator must report.
        LayoutCorridor main = layout.Corridors.Single(c => c.Main);
        List<string> cut = ProceduralLayoutValidator.Validate(input, layout.WithAddedObstacles(CrossSection(layout, main))).ToList();
        True(cut.Count > 0, "street cut is detected: " + string.Join(",", cut.Take(4)));
    }

    private static void RoomsNeverTransit()
    {
        ProceduralLayoutInput input = Input(Plains(120), LayoutRequirements.FromPopulation(8, Industrial, Cold), 17);
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
        Accepted(result);
        HashSet<GridPoint> circulation = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input, result.Layout));
        foreach (LayoutRoom room in result.Layout.Rooms)
        {
            True(!room.Bounds.Points().Any(circulation.Contains), room.Id + " not in public circulation");
            Equal(1, room.Bounds.Points().Count(c => room.IsEdge(c) && result.Layout.Portals.Any(p => p.Cell.Equals(c))), room.Id + " single portal");
            True(circulation.Contains(room.DoorOutside), room.Id + " door reachable publicly");
        }
        foreach (LayoutGate gate in result.Layout.Gates) True(circulation.Contains(gate.Inside), "gate endpoint in the one public component");
    }

    // ---- rejection, budget, immutability ----

    private static void Rejections()
    {
        ProceduralLayoutResult huge = Plan(Plains(40), LayoutRequirements.FromPopulation(20, Industrial, Temperate), 1);
        Equal("insufficient_usable_area", huge.RejectionCode, "population exceeds usable land");
        ProceduralLayoutResult crowded = Plan(Plains(120), LayoutRequirements.FromPopulation(120, Industrial, Temperate), 1);
        Equal("search_budget_exhausted", crowded.RejectionCode, "area pre-check passes but bounded search gives up");
        True(crowded.Stats.EvaluationCount <= 200000, "crowded search stays within budget");
        ProceduralLayoutResult absurd = Plan(Plains(120), LayoutRequirements.FromPopulation(400, Industrial, Temperate), 1);
        Equal("room_demand_exceeds_legal_dimensions", absurd.RejectionCode, "demand beyond split limit");

        ProceduralLayoutResult oversized = Plan(Plains(120), LayoutRequirements.FromPopulation(4, Industrial, Temperate, HousingStandard.Barracks,
            new[] { new KeyValuePair<LayoutRoomKind, int>(LayoutRoomKind.Dining, 400) }), 1);
        Equal("room_demand_exceeds_legal_dimensions", oversized.RejectionCode, "impossible single-room demand");

        ProceduralLayoutResult small = Plan(Islands(120, 16), LayoutRequirements.FromPopulation(6, Tribal, Temperate), 1);
        Equal("insufficient_usable_area", small.RejectionCode, "small disconnected islands");

        ProceduralLayoutResult island = Plan(Islands(140, 58), LayoutRequirements.FromPopulation(4, Tribal, Temperate), 1);
        Equal("no_valid_layout", island.RejectionCode, "island without dry approach");
        True(island.Diagnostics.Any(d => d.Code == "no_gate_candidate"), "island rejection explains missing gate route");

        ProceduralLayoutResult narrow = Plan(NarrowPass(120), LayoutRequirements.FromPopulation(6, Tribal, Temperate), 1);
        Equal("insufficient_usable_area", narrow.RejectionCode, "narrow passages do not merge land");
        foreach (ProceduralLayoutResult r in new[] { huge, crowded, absurd, oversized, small, island, narrow })
            Console.WriteLine("  reject " + r.RejectionCode + " :: " + string.Join(" | ", r.Diagnostics.Where(d => d.Stage != "demand").Take(3)));
    }

    private static void BoundedAndImmutable()
    {
        TerrainSnapshot terrain = Plains(120);
        string terrainDigest = terrain.Digest;
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(6, Industrial, Temperate);
        string requirementsDigest = requirements.Digest;
        ProceduralLayoutInput tight = new ProceduralLayoutInput(requirements, terrain, 5, null, new LayoutSearchBudget(maxEvaluations: 60));
        string inputDigest = tight.Digest;
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(tight);
        Equal("search_budget_exhausted", result.RejectionCode, "budget rejection");
        True(result.Stats.EvaluationCount <= 60, "evaluations bounded: " + result.Stats.EvaluationCount);
        Equal(terrainDigest, terrain.Digest, "terrain unchanged");
        Equal(inputDigest, tight.Digest, "input unchanged");
        Equal(requirementsDigest, requirements.Digest, "requirements unchanged");

        ProceduralLayoutInput normal = new ProceduralLayoutInput(requirements, terrain, 5, null, new LayoutSearchBudget(maxSites: 2, attemptsPerSite: 2));
        ProceduralLayoutResult ok = ProceduralLayoutPlanner.Plan(normal);
        Accepted(ok);
        True(ok.Stats.SitesTriedCount <= 2 && ok.Stats.AttemptCount <= 4 && ok.Stats.EvaluationCount <= 200000, "all counters within budget: " + ok.Stats);
        Equal(terrainDigest, terrain.Digest, "terrain unchanged after success");
        Console.WriteLine("  bounded: exhausted=" + result.Stats + " | accepted=" + ok.Stats);
    }

    // ---- rock protection ----

    private static void RockProtectedTribal()
    {
        TerrainSnapshot terrain = Mountain(130, 110);
        ProceduralLayoutInput input = Input(terrain, LayoutRequirements.FromPopulation(6, Tribal, Cold), 4);
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
        Accepted(result);
        ProceduralLayout layout = result.Layout;
        Equal(LayoutDoctrine.RockProtected, layout.Doctrine, "doctrine");
        int living = layout.Rooms.Count(r => LayoutDemandModel.IsLiving(r.Kind));
        int protectedLiving = layout.Rooms.Count(r => LayoutDemandModel.IsLiving(r.Kind) && r.ProtectedByRock);
        True(protectedLiving * 2 >= living, "most living rooms under rock: " + protectedLiving + "/" + living);
        True(layout.Excavation.Count > 0 && layout.Excavation.Count <= input.Requirements.ExcavationBudgetCells, "excavation within budget");
        Equal(0, LayoutRoofSupport.Unsupported(terrain, layout.Excavation, layout.RetainedThickRoof).Count, "natural support");
        True(layout.NaturalWalls.Count > 0, "rock serves as perimeter");
        Console.WriteLine("  rock-protected tribal: protected living " + protectedLiving + "/" + living + " excavated=" + layout.Excavation.Count +
            "/" + input.Requirements.ExcavationBudgetCells + " rockFaceGates=" + layout.Gates.Count(g => g.RockFace));
        ExportLayout("mountain-tribal-rock-protected", result, true);
    }

    private static void MortarDoctrine()
    {
        TerrainSnapshot terrain = Mountain(130, 110);
        Func<int, LayoutRequirements> requirements = bias => new LayoutRequirements(6, Industrial, HousingStandard.Barracks, Temperate, bias, 600, 186);
        ProceduralLayoutResult mortar = Plan(terrain, requirements(100), 4);
        ProceduralLayoutResult protect = Plan(terrain, requirements(0), 4);
        Accepted(mortar);
        Accepted(protect);
        Equal(LayoutDoctrine.OpenGround, mortar.Layout.Doctrine, "bias 100 keeps mortar");
        True(mortar.Layout.Court != null, "open-sky court reserved");
        Equal(LayoutDoctrine.RockProtected, protect.Layout.Doctrine, "bias 0 protects");
        True(protect.Layout.Court == null && protect.Layout.Notes.Any(n => n.StartsWith("mortar_court_dropped")), "capability loss is reported");
        True(protect.Diagnostics.Any(d => d.Stage == "doctrine"), "doctrine roll recorded");
        ExportLayout("mountain-industrial-mortar-doctrine", mortar, true);
        ExportLayout("mountain-industrial-protect-doctrine", protect, true);
    }

    private static void RoofSupportRule()
    {
        TerrainSnapshot rock = Mountain(80, 80);
        Func<int, List<GridPoint>> hall = size => new GridRect(55, 30, size, size).Points().ToList();
        True(LayoutRoofSupport.Unsupported(rock, hall(20), hall(20)).Count > 0, "20x20 hall collapses without pillars");
        Equal(0, LayoutRoofSupport.Unsupported(rock, hall(12), hall(12)).Count, "12x12 room is held by its rock walls");
    }

    // ---- fortification interface ----

    private static void WallLayers()
    {
        foreach (int layers in new[] { 1, 2, 3 })
        {
            ProceduralLayoutInput input = Input(Plains(120), LayoutRequirements.FromPopulation(6, Industrial, Temperate), 31,
                new LayoutFortificationReservation(wallLayers: layers));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
            Accepted(result);
            foreach (LayoutGate gate in result.Layout.Gates)
            {
                Equal(layers - 1, gate.BandDoors.Count, "band doors");
                Equal(4 * (layers - 1), gate.BandWalls.Count, "band walls");
                True(gate.ExteriorRoute.Count > 0, "exterior route");
            }
            if (layers == 3) ExportLayout("plains-industrial-3-layers", result);
        }
    }

    private static void AdapterCrossCheck()
    {
        int compatible = 0, total = 0;
        foreach (int layers in new[] { 1, 2, 3 })
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                TerrainSnapshot terrain = Plains(120);
                ProceduralLayoutInput input = Input(terrain, LayoutRequirements.FromPopulation(6, Industrial, Temperate), seed,
                    new LayoutFortificationReservation(wallLayers: layers));
                ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
                Accepted(result);
                total++;
                IReadOnlyList<string> incompatible, losses;
                PhysicalSettlementPlan plan = ProceduralLayoutAdapter.ToPhysicalPlan(input, result.Layout, "BlocksGranite", out incompatible, out losses);
                True(losses.Count > 0, "losses are declared");
                if (plan == null) continue;
                compatible++;
                IReadOnlyList<string> errors = P4Connectivity.Validate(terrain, plan);
                Equal(0, errors.Count, "P4Connectivity layers=" + layers + " seed=" + seed + " " + string.Join(",", errors.Take(4)));
            }
        True(compatible > 0, "at least one representable layout");
        Console.WriteLine("  adapter: " + compatible + "/" + total + " layouts representable by P4 PlannedNavigationGrid; all representable ones passed");
    }

    // ---- helpers ----

    private static ProceduralLayoutInput Input(TerrainSnapshot terrain, LayoutRequirements requirements, ulong seed,
        LayoutFortificationReservation fortification = null) => new ProceduralLayoutInput(requirements, terrain, seed, fortification);

    private static ProceduralLayoutResult Plan(TerrainSnapshot terrain, LayoutRequirements requirements, ulong seed) =>
        ProceduralLayoutPlanner.Plan(Input(terrain, requirements, seed));

    private static void Accepted(ProceduralLayoutResult result)
    {
        if (!result.Success)
            throw new InvalidOperationException("layout rejected: " + result.RejectionCode + " :: " +
                string.Join(" | ", result.Diagnostics.Where(d => d.Stage != "demand").Reverse().Take(8)));
    }

    private static string Topology(ProceduralLayout layout)
    {
        // Which room kinds face each other across streets: nearest-door kinds per room, rotation independent.
        return string.Join(";", layout.Rooms.Select(r => r.Kind + ">" + layout.Rooms.Where(o => o != r)
            .OrderBy(o => Math.Abs(o.DoorOutside.X - r.DoorOutside.X) + Math.Abs(o.DoorOutside.Z - r.DoorOutside.Z)).ThenBy(o => o.Id)
            .First().Kind).OrderBy(s => s));
    }

    private static string SizeSet(ProceduralLayout layout) => string.Join(";", layout.Rooms.Select(r =>
        r.Kind + ":" + Math.Min(r.Bounds.Width, r.Bounds.Height) + "x" + Math.Max(r.Bounds.Width, r.Bounds.Height)).OrderBy(s => s));

    // True when some rotation/reflection plus translation maps every room rectangle of a onto b.
    private static bool RigidEquivalent(ProceduralLayout a, ProceduralLayout b)
    {
        if (a.Rooms.Count != b.Rooms.Count) return false;
        HashSet<string> target = Normalize(b.Rooms.Select(r => new KeyValuePair<LayoutRoomKind, GridRect>(r.Kind, r.Bounds)));
        for (int t = 0; t < 8; t++)
        {
            HashSet<string> mapped = Normalize(a.Rooms.Select(r => new KeyValuePair<LayoutRoomKind, GridRect>(r.Kind, Transform(r.Bounds, t))));
            if (mapped.SetEquals(target)) return true;
        }
        return false;
    }

    private static GridRect Transform(GridRect rect, int t)
    {
        Func<int, int, GridPoint> f = (x, z) =>
        {
            int x1 = (t & 4) != 0 ? -x : x;
            switch (t & 3)
            {
                case 0: return new GridPoint(x1, z);
                case 1: return new GridPoint(z, -x1);
                case 2: return new GridPoint(-x1, -z);
                default: return new GridPoint(-z, x1);
            }
        };
        GridPoint p = f(rect.MinX, rect.MinZ), q = f(rect.MaxX, rect.MaxZ);
        return new GridRect(Math.Min(p.X, q.X), Math.Min(p.Z, q.Z), Math.Abs(p.X - q.X) + 1, Math.Abs(p.Z - q.Z) + 1);
    }

    private static HashSet<string> Normalize(IEnumerable<KeyValuePair<LayoutRoomKind, GridRect>> rooms)
    {
        List<KeyValuePair<LayoutRoomKind, GridRect>> list = rooms.ToList();
        int minX = list.Min(r => r.Value.MinX), minZ = list.Min(r => r.Value.MinZ);
        return new HashSet<string>(list.Select(r => (r.Key == LayoutRoomKind.Bedroom ? "Barracks" : r.Key.ToString()) + ":" +
            (r.Value.MinX - minX) + "," + (r.Value.MinZ - minZ) + "," + r.Value.Width + "x" + r.Value.Height));
    }

    private static IEnumerable<GridPoint> CrossSection(ProceduralLayout layout, LayoutCorridor main)
    {
        // A full-width cut across the middle of the main street plus the setback yard on both sides.
        GridPoint mid = main.Cells[main.Cells.Count / 2];
        bool vertical = main.Cells.Select(c => c.X).Distinct().Count() <= 2;
        HashSet<GridPoint> enclosure = new HashSet<GridPoint>(layout.Enclosure);
        return enclosure.Where(c => vertical ? c.Z == mid.Z : c.X == mid.X).ToList();
    }

    private static IEnumerable<GridPoint> Neighbors8(GridPoint c)
    {
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (dx != 0 || dz != 0) yield return new GridPoint(c.X + dx, c.Z + dz);
    }

    private static void ExportLayout(string name, ProceduralLayoutResult result, bool roof = false)
    {
        if (exportDir == null || !result.Success) return;
        Directory.CreateDirectory(exportDir);
        TerrainSnapshot terrain = TerrainFor(name);
        string text = "# " + name + " (synthetic terrain; planner evidence only, not a live map)\n" + LayoutAsciiExport.Legend + "\n\n" +
            LayoutAsciiExport.Render(terrain, result.Layout);
        if (roof) text += "\nroof view:\n" + LayoutAsciiExport.RenderRoof(terrain, result.Layout);
        text += "\nsearch: " + result.Stats + "\n";
        File.WriteAllText(Path.Combine(exportDir, name + ".txt"), text);
    }

    private static TerrainSnapshot TerrainFor(string name) =>
        name.StartsWith("mountain") ? Mountain(130, 110) : name.StartsWith("obstacles") ? Obstacles(120) :
        name == "plains-industrial-16" ? Plains(140) : name.Contains("personal") ? Plains(130) : Plains(120);

    // ---- synthetic terrain ----

    private const TerrainTraits Ground = TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.SupportsHeavy | TerrainTraits.Fertile;
    private const TerrainTraits RockRoofed = TerrainTraits.NaturalRock | TerrainTraits.Occupied | TerrainTraits.SupportsHeavy | TerrainTraits.Roofed;
    private const TerrainTraits DeepWater = TerrainTraits.Water;

    private static TerrainSnapshot Build(int width, int height, Func<int, int, TerrainTraits> traits)
    {
        TerrainCell[] cells = new TerrainCell[width * height];
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                TerrainTraits t = traits(x, z);
                if (x == 0 || z == 0 || x == width - 1 || z == height - 1) t |= TerrainTraits.MapEdge;
                cells[z * width + x] = new TerrainCell(t, 1, (ushort)((t & TerrainTraits.Roofed) != 0 ? 2 : 0));
            }
        return new TerrainSnapshot(width, height, cells, "p4a-synthetic-v1");
    }

    private static TerrainSnapshot Plains(int size) => Build(size, size, (x, z) => Ground);

    private static TerrainSnapshot Obstacles(int size) => Build(size, size, (x, z) =>
    {
        if (x >= 40 && x < 56 && z >= 50 && z < 64) return DeepWater;                         // pond near the centre
        if (x >= 64 && x < 70 && z >= 40 && z < 74) return TerrainTraits.NaturalRock | TerrainTraits.Occupied | TerrainTraits.SupportsHeavy; // unroofed rock ridge
        if ((x == 36 && z == 80) || (x == 82 && z == 34) || (x == 30 && z == 38) || (x == 88 && z == 86)) return TerrainTraits.Occupied; // boulders
        return Ground;
    });

    // Open ground to the west, a roofed mountain mass to the east with an irregular face.
    private static TerrainSnapshot Mountain(int width, int height) => Build(width, height, (x, z) =>
    {
        int face = width * 2 / 5 + (int)(4 * Math.Sin(z / 7.0)) + (z % 23 < 4 ? 3 : 0);
        return x >= face ? RockRoofed : Ground;
    });

    private static TerrainSnapshot Islands(int size, int island) => Build(size, size, (x, z) =>
    {
        bool first = x >= 10 && x < 10 + island && z >= 10 && z < 10 + island;
        bool second = x >= size - 10 - island && x < size - 10 && z >= size - 10 - island && z < size - 10;
        return first || second ? Ground : DeepWater;
    });

    // Two pockets joined by 3-wide passes; the rest is an unbuildable chasm.
    private static TerrainSnapshot NarrowPass(int size) => Build(size, size, (x, z) =>
    {
        bool west = x >= 25 && x < 45 && z >= 45 && z < 65;
        bool east = x >= 50 && x < 70 && z >= 45 && z < 65;
        bool pass = x >= 45 && x < 50 && z >= 54 && z < 57;
        bool exit = x >= 0 && x < 25 && z >= 54 && z < 57;
        return west || east || pass || exit ? Ground : TerrainTraits.None;
    });

    // ---- tech stage ----

    private static void TechStageStablePerFaction()
    {
        ulong seed = LayoutSeed.DeriveFaction("world", "Faction_12");
        Equal(seed, LayoutSeed.DeriveFaction("world", "Faction_12"), "faction seed stable");
        NotEqual(seed, LayoutSeed.DeriveFaction("world", "Faction_13"), "faction participates");
        NotEqual(seed, LayoutSeed.DeriveFaction("other", "Faction_12"), "world participates");
        SettlementTechStage first, second;
        string diagnostic;
        True(TechStageModel.TryResolve(TechTier.Industrial, seed, out first, out diagnostic) && diagnostic == null, "industrial resolves");
        True(TechStageModel.TryResolve(TechTier.Industrial, seed, out second, out diagnostic), "industrial resolves again");
        Equal(first.Level, second.Level, "same faction, same stage");
        Equal(first.Digest, second.Digest, "same faction, same digest");
        // Settlement identity (tile, profile) cannot influence the faction stage:
        // the resolver only sees the faction seed.
        HashSet<SettlementTechStageLevel> seen = new HashSet<SettlementTechStageLevel>();
        for (int i = 0; i < 200; i++)
        {
            SettlementTechStage stage;
            TechStageModel.TryResolve(TechTier.Industrial, LayoutSeed.DeriveFaction("world", "Faction_" + i), out stage, out diagnostic);
            seen.Add(stage.Level);
        }
        Equal(3, seen.Count, "different factions spread over the band");
    }

    private static void TechStageBands()
    {
        foreach (TechTier tier in Enum.GetValues(typeof(TechTier)).Cast<TechTier>())
        {
            IReadOnlyList<KeyValuePair<SettlementTechStageLevel, int>> band = TechStageModel.Band(tier);
            SettlementTechStage stage;
            string diagnostic;
            if (tier == TechTier.Undefined || tier == TechTier.Animal)
            {
                Equal(0, band.Count, tier + " has no band");
                True(!TechStageModel.TryResolve(tier, 1, out stage, out diagnostic) && stage == null, tier + " unsupported");
                True(diagnostic.StartsWith(TechStageModel.UnsupportedTechLevel), "unsupported diagnostic");
                continue;
            }
            Equal(100, band.Sum(b => b.Value), tier + " weights sum to 100");
            Dictionary<SettlementTechStageLevel, int> counts = new Dictionary<SettlementTechStageLevel, int>();
            const int samples = 4000;
            for (int i = 0; i < samples; i++)
            {
                True(TechStageModel.TryResolve(tier, LayoutSeed.DeriveFaction("world-" + tier, "Faction_" + i), out stage, out diagnostic), tier + " resolves");
                True(band.Any(b => b.Key == stage.Level), tier + " stage in band: " + stage.Level);
                int count;
                counts[stage.Level] = counts.TryGetValue(stage.Level, out count) ? count + 1 : 1;
            }
            foreach (KeyValuePair<SettlementTechStageLevel, int> entry in band)
            {
                int count;
                counts.TryGetValue(entry.Key, out count);
                int percent = count * 100 / samples;
                True(Math.Abs(percent - entry.Value) <= 3, tier + " " + entry.Key + " frequency " + percent + "% vs weight " + entry.Value);
            }
        }
        Equal(TechStageModel.Band(TechTier.Spacer).Count, TechStageModel.Band(TechTier.Archotech).Count, "archotech clamps to spacer band");
        True(TechStageModel.Band(TechTier.Ultra).Select(b => b.Key).SequenceEqual(TechStageModel.Band(TechTier.Spacer).Select(b => b.Key)), "ultra clamps to spacer band");
    }

    private static void TechStageMonotonic()
    {
        SettlementTechStage previous = null;
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            SettlementTechStage stage = TechStageModel.Create(level);
            Equal(TechStageVersions.Schema, stage.SchemaVersion, "schema version");
            True(stage.Research.SequenceEqual(stage.Research.OrderBy(r => r, StringComparer.Ordinal).Distinct()), level + " research sorted and distinct");
            True(TechStageModel.AddedResearch(level).All(stage.HasResearch), level + " contains its added research");
            if (previous != null)
            {
                True(previous.Research.All(stage.HasResearch), level + " contains every earlier research");
                True(stage.Research.Count > previous.Research.Count, level + " adds research");
                True(stage.Ceiling >= previous.Ceiling, level + " ceiling never drops");
                NotEqual(previous.Digest, stage.Digest, level + " digest differs");
            }
            previous = stage;
        }
        SettlementTechStage neolithic = TechStageModel.Create(SettlementTechStageLevel.Neolithic);
        True(!neolithic.HasResearch("ComplexFurniture") && !neolithic.HasResearch("Smithing") && !neolithic.HasResearch("Stonecutting"), "tribal stays tribal");
        Equal(TechTier.Neolithic, neolithic.Ceiling, "neolithic ceiling");
        Equal(TechTier.Medieval, TechStageModel.Create(SettlementTechStageLevel.Medieval).Ceiling, "medieval ceiling");
        Equal(TechTier.Industrial, TechStageModel.Create(SettlementTechStageLevel.IndustrialLate).Ceiling, "industrial ceiling");
        Equal(TechTier.Spacer, TechStageModel.Create(SettlementTechStageLevel.Spacer).Ceiling, "spacer ceiling");
        SettlementTechStage early = TechStageModel.Create(SettlementTechStageLevel.IndustrialEarly);
        True(early.HasResearch("Electricity") && early.HasResearch("Smithing") && !early.HasResearch("FlakArmor"), "early industrial set");
        True(TechStageModel.Create(SettlementTechStageLevel.IndustrialMid).HasResearch("SterileMaterials"), "HospitalBed prerequisite in mid");
        True(TechStageModel.Create(SettlementTechStageLevel.IndustrialLate).HasResearch("Prosthetics"), "Bionics prerequisite in late");
    }

    private static void TechStageAdaptations()
    {
        SettlementTechStage tribal = TechStageModel.Create(SettlementTechStageLevel.Neolithic);
        SettlementTechStage adapted = tribal.WithAdaptation("Stonecutting", "local_wood_scarce");
        True(!tribal.HasResearch("Stonecutting") && tribal.Adaptations.Count == 0, "original unchanged");
        True(adapted.HasResearch("Stonecutting") && !adapted.BaseResearch.Contains("Stonecutting"), "adaptation adds research only");
        Equal("local_wood_scarce", adapted.Adaptations.Single().Reason, "reason recorded");
        Equal(TechTier.Neolithic, adapted.Ceiling, "adaptation never raises ceiling");
        NotEqual(tribal.Digest, adapted.Digest, "adaptation participates in digest");
        True(ReferenceEquals(adapted, adapted.WithAdaptation("Stonecutting", "again")), "duplicate adaptation is a no-op");
        True(ReferenceEquals(adapted, adapted.WithAdaptation("TreeSowing", "base")), "adaptation of base research is a no-op");
        Equal(adapted.Digest, tribal.WithAdaptation("Stonecutting", "local_wood_scarce").Digest, "adaptation digest stable");

        SettlementTechStage lower;
        True(!adapted.TryStepDown(out lower) && lower == null, "neolithic cannot step down");
        SettlementTechStage mid = TechStageModel.Create(SettlementTechStageLevel.IndustrialMid).WithAdaptation("Fabrication", "test");
        True(mid.TryStepDown(out lower), "mid steps down");
        Equal(SettlementTechStageLevel.IndustrialEarly, lower.Level, "one level lower");
        True(lower.HasResearch("Fabrication") && lower.Adaptations.Count == 1, "step-down keeps adaptations");
        True(!lower.HasResearch("FlakArmor"), "step-down drops the stage's own research");
    }

    private static void Run(string name, Action test)
    {
        Stopwatch watch = Stopwatch.StartNew();
        try { test(); Console.WriteLine("PASS " + name + " (" + watch.ElapsedMilliseconds + " ms)"); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.Message + "\n" + ex.StackTrace?.Split('\n').FirstOrDefault()); }
    }

    private static void True(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Contains(IReadOnlyList<string> errors, string expected) =>
        True(errors.Contains(expected), "expected " + expected + " in " + string.Join(",", errors.Take(6)));
    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(message + " expected=" + expected + " actual=" + actual);
    }
    private static void NotEqual<T>(T a, T b, string message) { if (EqualityComparer<T>.Default.Equals(a, b)) throw new InvalidOperationException(message); }
}
