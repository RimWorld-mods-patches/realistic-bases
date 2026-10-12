using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RealisticBases.Planning;

// Synthetic pure-planner evidence for the P4A foundation. Nothing here runs the
// game; results are not live-map evidence.
internal static partial class Program
{
    private static int failures;
    private static string exportDir;
    private static string only;
    // --shard i/n runs every n-th selected test, so n processes share one build.
    private static int shard, shards = 1, runIndex;
    private static readonly SettlementCapability Industrial = P2ProfileCatalog.IndustrialTemperate.Capabilities;
    private static readonly SettlementCapability Tribal = P2ProfileCatalog.TribalTemperate.Capabilities;
    private static readonly LayoutClimate Temperate = new LayoutClimate(12, 28);
    private static readonly LayoutClimate Cold = new LayoutClimate(-20, 20);
    private static readonly LayoutClimate Hot = new LayoutClimate(18, 42);

    private static int Main(string[] args)
    {
        int export = Array.IndexOf(args, "--export");
        if (export >= 0 && export + 1 < args.Length) exportDir = args[export + 1];
        int selected = Array.IndexOf(args, "--only");
        if (selected >= 0 && selected + 1 < args.Length) only = args[selected + 1];
        int sharded = Array.IndexOf(args, "--shard");
        if (sharded >= 0 && sharded + 1 < args.Length)
        {
            string[] parts = args[sharded + 1].Split('/');
            shard = int.Parse(parts[0]);
            shards = int.Parse(parts[1]);
        }
        Stopwatch watch = Stopwatch.StartNew();
        Run("P4E laboratory stage matrix and compact quest exclusions", P4ELaboratoryStageMatrix);
        Run("P4E occupied room climate and power demand", P4EOccupiedRoomClimate);
        Run("P4E cardinal work dining seats and medical shelves", P4ESeatsAndMedicalShelves);
        Run("P4E compact mortar magazine geometry and rotated access", P4EMortarMagazine);
        Run("P4E population artillery caps shared stock and cost", P4EArtilleryCapacity);
        Run("P4E exact ten-resident rejected flat tile packs required rooms", P4ETenRejectedTile);
        Run("P4E exact flat cooler tile keeps a valid served room", P4EFlatCoolerTile);
        Run("P4E cleared vegetation does not split cooler served rooms", P4EClearedThermalPlants);
        Run("P4E spare barrels may use warehouse without moving ammunition", P4EBarrelWarehouse);
        Run("P4E ordinary defense allowance power and weapon sweep", P4EDefenseAllowance);
        Run("P4E legal logistics smoke top-up preserves population", P4ELogisticsSmokeBudget);
        Run("seed derivation is stable and purpose-separated", SeedDerivation);
        Run("P4D strategies require whole native compositions", QuestStrategies);
        Run("P4D preserves authoritative roster and deterministic budgets", QuestRosterContracts);
        Run("P4D reserves the original prisoner separately", QuestObjectiveContracts);
        Run("P4D compact camps use shared geometry without ordinary gear", QuestCampLayouts);
        Run("P4D unusable terrain rejects without changing inputs", QuestCampRejection);
        Run("P4D terrain and tech matrix preserves objective and native roster", QuestTerrainTechMatrix);
        Run("P4C finishes are deterministic, whole-room, protected and budgeted", P4CFinishPolicy);
        Run("ordinary population follows wealth budgets with mean seven", OrdinaryPopulationBudgets);
        Run("ordinary population remains stable and preserves legacy saves", OrdinaryPopulationPersistence);
        Run("ordinary population reserves facility and circulation space upfront", OrdinaryPopulationReservation);
        Run("ordinary population plans complete four seven and twelve resident sites", OrdinaryPopulationLayouts);
        Run("ordinary population fits cluttered flat tiles without reducing residents", OrdinaryPopulationClutter);
        Run("ordinary population reserves usable layered gate frontage", OrdinaryPopulationLayeredFrontage);
        Run("ordinary population stocks use all split rooms and prefer shelves", OrdinaryPopulationStocks);
        Run("P5 sale offer is sized into storage without changing other inputs", P5SaleStorageSizing);
        Run("P5 captured native trade tile fits its whole offer with dense storage", P5NativeTradeStorage);
        Run("demand scales with population and splits barracks", DemandScalesWithPopulation);
        Run("rich settlements get personal bedrooms", PersonalRooms);
        Run("climate drives heating, cooling and lighting fixtures", ClimateFixtures);
        Run("freezer and occupied-room cooler targets differ", CoolerTemperatureTargets);
        Run("workbenches stand against a wall with an interior work cell", WorkbenchesAgainstWall);
        RunCoolerExhaustTests();
        RunLayoutEngineV2Tests();
        Run("natural barriers: shallow water walks, deep water and rock block", ExposureRules);
        Run("joint sites prefer natural barriers on beach and lakeshore maps", BarrierSiting);
        Run("inner yards become crop patches without touching circulation", InnerYardCrops);
        RunLayoutEngineV2Matrix();
        Run("identical inputs replay identical layouts", Determinism);
        Run("seeds vary internal layout beyond translation and rotation", SeedVariation);
        Run("population change alters capacity and geometry coherently", PopulationChangesGeometry);
        Run("obstacles reshape the layout", ObstaclesReshapeLayout);
        Run("scattered rock on open ground does not force mountain doctrine", ScatteredRockUsesOpenGround);
        Run("flat maps with natural clutter remain placeable", ClutteredFlatMaps);
        Run("live-density flat tiles place complete rooms", LiveDensityFlatTiles);
        Run("only clearable plants relax nonstandable flat terrain", ClearablePlantClassification);
        Run("captured flat obstruction pattern places complete rooms without RimWorld", CapturedFlatTile);
        Run("final obstacles and access reservations are validated", ConnectivityAfterBlocking);
        Run("rooms are never public transit", RoomsNeverTransit);
        Run("impossible demand and unusable terrain reject predictably", Rejections);
        Run("search is bounded and inputs stay unchanged", BoundedAndImmutable);
        Run("tribal settlement digs under rock with natural roof support", RockProtectedTribal);
        Run("mortar doctrine trades protection for an open-sky court", MortarDoctrine);
        Run("roof support ignores built walls and rejects wide halls", RoofSupportRule);
        Run("tech and spend capacity weight one to three physical wall layers", LayerPolicy);
        Run("gatehouse crossings and exterior sections reserve one to three layers", WallLayers);
        Run("P4B full perimeter has no single-wall northern breach shortcut", FullPerimeterDepth);
        Run("layered walls retain service lanes on rock, water and irregular terrain", FortificationTerrainMatrix);
        Run("narrow dry approach keeps layered gates and recovery bounded", NarrowFortificationApproach);
        Run("corner obstruction replans connected wall returns within budget", CornerWallReturnRecovery);
        Run("validator rejects a blocked outer-wall service lane", BlockedFortificationServiceLane);
        Run("P4B firing-lane prefilter rejects opaque and out-of-range shots", P4BFireLaneCases);
        Run("P4B field wall joins the perimeter with unroofed crops and exterior posts", P4BFieldWallCases);
        Run("P4B field wall planner finds an attached flat-map bailey", P4BFieldWallPlannerCases);
        Run("P4B joint site keeps crop capacity and compound space together", P4BJointSiteCases);
        Run("P4B turret emplacement respects stage, geometry and finite stocks", P4BTurretPlacementCases);
        Run("P4B high-industrial field pair fits its facilities", P4BHighIndustrialJointSite);
        Run("P4B turret network adds coordinated budgeted groups", P4BTurretNetwork);
        Run("P4B staffing does not borrow one marksman across separated fronts", P4BStaffingIsExclusive);
        Run("P4B field breach has compound gate fallback firing posts", P4BFieldFallbackPosts);
        Run("P4B final path audit flushes deferred grids and restores batching", P4BFinalPathAuditSynchronization);
        Run("P4B high-industrial settlement packs on a captured flat tile", P4BHighIndustrialCapturedFlat);
        Run("P4B high-industrial settlement tolerates live-density flat clutter", P4BHighIndustrialFlatDensity);
        Run("P4B joint reservation constrains detailed room packing", P4BJointPackingCases);
        Run("P4B approach defense covers exposed sides and respects terrain", P4BApproachDefenseCases);
        Run("P4B public frontages repair weak sides without moving rooms or fields", P4BAccessFrontageCases);
        Run("adapter output passes P4 connectivity when representable", AdapterCrossCheck);
        Run("tech stage is stable per faction", TechStageStablePerFaction);
        Run("tech stage respects every faction band", TechStageBands);
        Run("tech stage research sets are monotonic", TechStageMonotonic);
        Run("tech stage adaptations and step-down", TechStageAdaptations);
        Run("catalog stage sets are closed under prerequisites", CatalogPrerequisiteClosure);
        Run("Core turret facts retain resolved range, power and finite reloads", P4BTurretCatalogFacts);
        Run("catalog golden legality per stage", CatalogGoldenLegality);
        Run("catalog never allows traps or IEDs", CatalogProhibitedContent);
        Run("catalog legality rules and rule order", CatalogLegalityRules);
        Run("catalog fixture data and digest", CatalogFixtureData);
        Run("catalog raw minimum tiers gate exotic stuff", CatalogRawMinimumTiers);
        Run("roles resolve per stage from the Core catalog", RoleCoreResolution);
        Run("unequippable tier steps down and re-resolves the kit", RoleStepDown);
        Run("essential role without legal option rejects at Neolithic", RoleRejection);
        Run("cooking caps: tribal campfire, medieval fueled stove", CookingCaps);
        Run("tribal layout has no ComplexFurniture items", TribalLayoutKit);
        Run("medieval and industrial layouts furnish from the catalog", StagedLayoutKits);
        Run("wood-poor stony map adapts stonecutting and imports fuel", WoodPoorStonyMap);
        Run("wood- and stone-poor map rejects on the material budget", WoodAndStonePoorMap);
        Run("default resources never trigger the adaptation", DefaultResourcesNoAdaptation);
        Run("unresolvable climate control rejects", ClimateControlUnresolved);
        Run("stage and resources participate in digests", EquipmentDigests);
        Run("power: tribal has none", PowerTribalNone);
        Run("power: early is wood-fired dispatchable plus wind", PowerEarly);
        Run("power: mid on open sky adds renewables", PowerMidOpenSky);
        Run("power: rock-protected mid is fuel only with a larger stock", PowerRockProtected);
        Run("power: fuel stock matches rate x horizon and is stored", PowerFuelStock);
        Run("power: geothermal yard sits on the geyser", PowerGeothermal);
        Run("power: wind clear zone respected", PowerWindZone);
        Run("power: critical load covered by dispatchable output", PowerCriticalCovered);
        Run("power: battery count matches the formula", PowerBatteries);
        Run("power: chemfuel when wood is short, imported at catalog value", PowerChemfuel);
        Run("power: watermill and oversized power diagnostics", PowerDiagnostics);
        Run("unsupported interaction offsets reject with a diagnostic", InteractionOffsetUnsupported);
        Run("loadout: role shares by stage", LoadoutRoles);
        Run("loadout: role x stage weapon golden table", LoadoutWeapons);
        Run("loadout: stage apparel golden table", LoadoutApparel);
        Run("loadout: cold adds shells, hot never does", LoadoutClimate);
        Run("loadout: spacer shield belts only on melee, one helmet", LoadoutSpacer);
        Run("loadout: quality is seeded and deterministic", LoadoutQualityDeterminism);
        Run("loadout: quality requires a native quality component", LoadoutQualityCapability);
        Run("loadout: armoury, medicine and component stocks", LoadoutStocks);
        Run("loadout: tribal has no gun, flak or industrial medicine", LoadoutTribal);
        Run("loadout: temperate kits fit the stage default budget", LoadoutBudgetDefaults);
        Run("loadout: budget degrades then rejects", LoadoutBudgetDegradation);
        Run("loadout: built from a planned layout and validated", LoadoutFromLayout);
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
            LayoutDemandSet demand = Demand(LayoutRequirements.FromPopulation(residents, Industrial, Temperate));
            Equal(0, demand.Rejections.Count, "no rejection");
            int beds = demand.Rooms.Where(r => r.Kind == LayoutRoomKind.Barracks).Sum(r => r.Capacity);
            Equal(residents, beds, "beds cover residents");
            Equal((residents + 7) / 8, demand.Rooms.Count(r => r.Kind == LayoutRoomKind.Barracks), "barracks split at 8 beds");
            int storage = demand.Rooms.Where(r => r.Kind == LayoutRoomKind.Storage).Sum(r => r.Capacity);
            True(beds > previousBeds && storage > previousStorage, "capacities grow with population");
            True(demand.Rooms.All(r => r.CandidateSizes.Count > 0), "every room has proven sizes");
            True(demand.Rooms.Any(r => r.Kind == LayoutRoomKind.Power), "industrial power room");
            True(demand.Relationships.Any(r => r.Hard), "kitchen-food hard relation");
            previousBeds = beds;
            previousStorage = storage;
        }
        LayoutDemandSet tribal = Demand(LayoutRequirements.FromPopulation(6, Tribal, Temperate));
        True(tribal.Rooms.Any(r => r.Kind == LayoutRoomKind.Pantry) && !tribal.Rooms.Any(r => r.Kind == LayoutRoomKind.Freezer || r.Kind == LayoutRoomKind.Power),
            "tribal pantry and no power");
        LayoutDemandSet fromProfile = Demand(LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate));
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
        True(cold.Layout.Rooms.Where(r => r.Kind != LayoutRoomKind.MortarMagazine).All(r =>
            r.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Lamp)), "lamps in occupied and general storage rooms");
        True(cold.Layout.Rooms.Where(r => r.Kind == LayoutRoomKind.MortarMagazine).All(r =>
            r.Fixtures.Count == 1 && r.Fixtures[0].Kind == LayoutFixtureKind.Shelf), "micro magazine contains only its shelf");
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

    private static void CoolerTemperatureTargets()
    {
        Equal(-10f, SettlementTemperatureTargets.ForRoom(LayoutRoomKind.Freezer), "freezer setpoint");
        foreach (LayoutRoomKind kind in Enum.GetValues<LayoutRoomKind>().Where(kind => kind != LayoutRoomKind.Freezer))
            Equal(21f, SettlementTemperatureTargets.ForRoom(kind), kind + " setpoint");
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
            int width = bench.Facing.X == 0 ? bench.Cells.Select(c => c.X).Distinct().Count() : bench.Cells.Select(c => c.Z).Distinct().Count();
            Equal(width, bench.Cells.Count(c => room.IsEdge(new GridPoint(c.X + back.X, c.Z + back.Z))), bench.DefName + " complete back row against wall");
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

    private static void ScatteredRockUsesOpenGround()
    {
        TerrainSnapshot terrain = Build(140, 140, (x, z) =>
            x > 8 && z > 8 && x % 24 < 4 && z % 24 < 4 ? RockRoofed : Ground);
        ProceduralLayoutResult result = Plan(terrain,
            LayoutRequirements.FromPopulation(6, Industrial, Temperate), 8);
        Accepted(result);
        Equal(LayoutDoctrine.OpenGround, result.Layout.Doctrine,
            "separate stone outcrops cannot sustain a rock-protected settlement");
    }

    private static void ClutteredFlatMaps()
    {
        for (int sample = 0; sample < 4; sample++)
        {
            int salt = sample;
            TerrainSnapshot terrain = Build(160, 160, (x, z) =>
            {
                uint hash = (uint)((x / 5) * 73856093 ^ (z / 5) * 19349663 ^ salt * 83492791);
                hash ^= hash >> 13;
                hash *= 1274126177u;
                int roll = (int)(hash % 1000);
                if (roll < 65) return RockRoofed;
                uint edifice = (uint)(x * 19349663 ^ z * 83492791 ^ salt * 73856093);
                edifice ^= edifice >> 13;
                if (edifice % 1000 < 10) return TerrainTraits.Occupied;
                return Ground;
            });
            ProceduralLayoutResult result = Plan(terrain, LayoutRequirements.FromPopulation(6, Industrial, Temperate), 8);
            True(result.Success, "flat clutter sample=" + sample + " rejected=" + result.RejectionCode + " " + result.Stats +
                " tail=" + string.Join("|", result.Diagnostics.Where(d => d.Stage == "attempt" || d.Stage == "site").TakeLast(4)));
            True(result.Layout.BuiltWalls.All(c => !terrain[c].Has(TerrainTraits.Occupied)),
                "perimeter skirts existing edifices sample=" + sample);
        }
    }

    private static void LiveDensityFlatTiles()
    {
        // 160x160 live rejected flat tile: 1780/25600 natural-rock cells and
        // virtually no water. Model small boulder groups rather than 5x5 rock
        // chunks, using its stage, climate and 4-resident profile.
        LayoutRequirements needs = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
            new LayoutClimate(-9, 32), stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialMid));
        for (int salt = 0; salt < 6; salt++)
        {
            int sample = salt;
            TerrainSnapshot terrain = Build(160, 160, (x, z) =>
            {
                uint cluster = (uint)((x / 3) * 73856093 ^ (z / 3) * 19349663 ^ sample * 83492791);
                cluster ^= cluster >> 13;
                cluster *= 1274126177u;
                if (cluster % 1000 < 70) return RockRoofed;
                return Ground;
            });
            ProceduralLayoutInput input = new ProceduralLayoutInput(needs, terrain, 0x8B704E948E310455UL + (ulong)sample,
                CoreCatalog, resources: new LayoutLocalResources(29800, new[] { "BlocksGranite", "BlocksMarble" }));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
            True(result.Success, "live-density sample=" + sample + " rejected=" + result.RejectionCode + " " + result.Stats +
                " tail=" + string.Join("|", result.Diagnostics.Where(d => d.Stage == "attempt").TakeLast(4)));
        }
    }

    private static void ClearablePlantClassification()
    {
        TerrainTraits ground = TerrainTraits.Buildable | TerrainTraits.Fertile | TerrainTraits.SupportsHeavy;
        GridPoint center = new GridPoint(5, 5);
        TerrainSnapshot blocked = Build(11, 11, (x, z) => ground);
        TerrainSnapshot clearable = Build(11, 11, (x, z) =>
            ground | TerrainTraits.ClearablePlant);
        TerrainSnapshot occupied = Build(11, 11, (x, z) =>
            ground | TerrainTraits.ClearablePlant | TerrainTraits.Occupied);
        Equal(LayoutCellClass.Bad, LayoutTerrainGrid.Classify(blocked[center]), "nonstandable ground stays blocked");
        Equal(LayoutCellClass.Open, LayoutTerrainGrid.Classify(clearable[center]), "clearable plant becomes open");
        Equal(LayoutCellClass.Bad, LayoutTerrainGrid.Classify(occupied[center]), "edifice remains blocked");
        True(!LayoutTerrainGrid.Build(blocked, 0).Passable(center), "unclearable ground is not a route");
        True(LayoutTerrainGrid.Build(clearable, 0).Passable(center), "clearable plant can be a route");
        True(!LayoutTerrainGrid.Build(occupied, 0).Passable(center), "occupied plant is not a route");
    }

    private static void CapturedFlatTile()
    {
        TerrainSnapshot terrain = LoadCapturedFlatTerrain();
        LayoutRequirements needs = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
            new LayoutClimate(-8, 32), stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialMid));
        LayoutLocalResources resources = new LayoutLocalResources(32667, new[] { "BlocksGranite", "BlocksMarble" },
            steamGeyserCells: new[] { new GridPoint(70, 32), new GridPoint(58, 69) });
        ProceduralLayoutInput input = new ProceduralLayoutInput(needs, terrain, 0xCB52CAC6AEE2BE0AUL,
            CoreCatalog, new LayoutFortificationReservation(1), resources: resources);
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
        True(result.Success, "captured flat tile rejected=" + result.RejectionCode + " " + result.Stats +
            " tail=" + string.Join("|", result.Diagnostics.Where(d => d.Stage == "attempt").TakeLast(5)));
        Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, "captured layout validates");
    }

    private static TerrainSnapshot LoadCapturedFlatTerrain()
    {
        string[] rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "flat-rejected-traits.rle"));
        Equal(160, rows.Length, "captured flat tile height");
        List<TerrainCell> cells = new List<TerrainCell>();
        foreach (string row in rows)
        {
            int width = 0;
            foreach (string token in row.Split(','))
            {
                string[] parts = token.Split('*');
                TerrainTraits traits = (TerrainTraits)Convert.ToUInt16(parts[0], 16);
                // The capture predates the explicit ClearablePlant trait. The
                // 0x062 cells are buildable fertile ground obstructed by trees,
                // not water, rock or an edifice.
                if (traits == (TerrainTraits.Buildable | TerrainTraits.Fertile | TerrainTraits.SupportsHeavy))
                    traits |= TerrainTraits.ClearablePlant;
                int count = int.Parse(parts[1]);
                for (int i = 0; i < count; i++) cells.Add(new TerrainCell(traits));
                width += count;
            }
            Equal(160, width, "captured flat tile row width");
        }
        return new TerrainSnapshot(160, 160, cells, "captured-flat-obstruction-v1");
    }

    // ---- connectivity ----

    private static void ConnectivityAfterBlocking()
    {
        TerrainSnapshot terrain = Plains(120);
        ProceduralLayoutInput input = Input(terrain, LayoutRequirements.FromPopulation(6, Industrial, Temperate), 13,
            new LayoutFortificationReservation(minimumGates: 2));
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
        // Racked storage lets 20 residents fit 40x40; 30x30 is genuinely too small.
        ProceduralLayoutResult huge = Plan(Plains(30), LayoutRequirements.FromPopulation(20, Industrial, Temperate), 1);
        Equal("insufficient_usable_area", huge.RejectionCode, "population exceeds usable land");
        ProceduralLayoutResult crowded = Plan(Plains(120), LayoutRequirements.FromPopulation(120, Tribal, Temperate), 1);
        if (crowded.Success)
            Equal(0, ProceduralLayoutValidator.Validate(Input(Plains(120), LayoutRequirements.FromPopulation(120, Tribal, Temperate), 1),
                crowded.Layout).Count, "a formerly crowded case may now fit, without weakening validation");
        else Equal("search_budget_exhausted", crowded.RejectionCode, "an unsuccessful crowded search is bounded");
        True(crowded.Stats.EvaluationCount <= new LayoutSearchBudget().MaxEvaluations,
            "crowded search stays within budget");
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
            Console.WriteLine("  reject " + r.RejectionCode + " :: " + string.Join(" | ", r.Diagnostics.Where(d => d.Stage != "demand" && d.Stage != "equipment").Take(3)));
    }

    private static void BoundedAndImmutable()
    {
        TerrainSnapshot terrain = Plains(120);
        string terrainDigest = terrain.Digest;
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(6, Industrial, Temperate);
        string requirementsDigest = requirements.Digest;
        ProceduralLayoutInput tight = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog, null, new LayoutSearchBudget(maxEvaluations: 60));
        string inputDigest = tight.Digest;
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(tight);
        Equal("search_budget_exhausted", result.RejectionCode, "budget rejection");
        True(result.Stats.EvaluationCount <= 60, "evaluations bounded: " + result.Stats.EvaluationCount);
        Equal(terrainDigest, terrain.Digest, "terrain unchanged");
        Equal(inputDigest, tight.Digest, "input unchanged");
        Equal(requirementsDigest, requirements.Digest, "requirements unchanged");

        ProceduralLayoutInput normal = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog, null, new LayoutSearchBudget(maxSites: 2, attemptsPerSite: 2));
        ProceduralLayoutResult ok = ProceduralLayoutPlanner.Plan(normal);
        Accepted(ok);
        True(ok.Stats.SitesTriedCount <= 2 && ok.Stats.AttemptCount <= 4 &&
            ok.Stats.EvaluationCount <= normal.Budget.MaxEvaluations, "all counters within budget: " + ok.Stats);
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
        ProceduralLayoutResult mortar = null, protect = null;
        foreach (ulong seed in Enumerable.Range(1, 12).Select(n => (ulong)n))
        {
            mortar = Plan(terrain, requirements(100), seed);
            protect = Plan(terrain, requirements(0), seed);
            Accepted(mortar); Accepted(protect);
            if (mortar.Layout.Court != null && protect.Layout.Doctrine == LayoutDoctrine.RockProtected) break;
            if (mortar.Layout.Court == null) True(mortar.Layout.Notes.Any(n => n.StartsWith("mortar_court")),
                "constrained optional artillery records why its court/store is omitted");
        }
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

    private static void LayerPolicy()
    {
        int[] counts = new int[4];
        for (ulong seed = 0; seed < 10000; seed++)
            counts[P4BLayerPolicy.Select(seed, SettlementTechStageLevel.IndustrialLate, 30000, 6000, 6)]++;
        True(Math.Abs(counts[1] - 5000) < 250, "rich late-tech one-layer share " + counts[1]);
        True(Math.Abs(counts[2] - 3000) < 250, "rich late-tech two-layer share " + counts[2]);
        True(Math.Abs(counts[3] - 2000) < 250, "rich late-tech three-layer share " + counts[3]);
        for (ulong seed = 0; seed < 100; seed++)
        {
            Equal(1, P4BLayerPolicy.Select(seed, SettlementTechStageLevel.Neolithic, 20000, 6000, 6), "neolithic cap");
            True(P4BLayerPolicy.Select(seed, SettlementTechStageLevel.IndustrialEarly, 20000, 6000, 6) <= 2, "early tech cap");
            Equal(1, P4BLayerPolicy.Select(seed, SettlementTechStageLevel.IndustrialLate, 1000, 500, 6), "low spend cap");
        }
    }

    private static void WallLayers()
    {
        bool Touches(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z) == 1;
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
            Equal(result.Layout.Gates.Count * (layers - 1), result.Layout.OuterSections.Count,
                "every added gate layer returns to the previous wall");
            if (layers == 1) Equal(0, result.Layout.OuterSections.Count, "single-layer has no outer sections");
            else
            {
                foreach (LayoutWallSection section in result.Layout.OuterSections)
                {
                    True(section.Left.Count + section.Right.Count >= 3, "gate has substantive outer arms");
                    True(section.Cells.All(c => !result.Layout.Enclosure.Contains(c)), "outer walls remain exterior");
                    LayoutGate gate = result.Layout.Gates[section.GateIndex];
                    LayoutWallSection inner = result.Layout.OuterSections.FirstOrDefault(s =>
                        s.GateIndex == section.GateIndex && s.Layer == section.Layer - 1);
                    foreach (int side in new[] { -1, 1 })
                    {
                        IReadOnlyList<GridPoint> arm = side < 0 ? section.Left : section.Right;
                        IReadOnlyList<GridPoint> path = side < 0 ? section.LeftReturn : section.RightReturn;
                        GridPoint tangent = new GridPoint(gate.Outward.Z, gate.Outward.X);
                        GridPoint junction = arm.Count > 0 ? arm[arm.Count - 1] :
                            new GridPoint(gate.Cell.X + 2 * side * tangent.X + 2 * section.Layer * gate.Outward.X,
                                gate.Cell.Z + 2 * side * tangent.Z + 2 * section.Layer * gate.Outward.Z);
                        True(path.Count > 0 && Touches(junction, path[0]), "return starts at outer wall");
                        for (int j = 1; j < path.Count; j++) True(Touches(path[j - 1], path[j]), "return path is continuous");
                        GridPoint terminal = path[path.Count - 1];
                        GridPoint parent = arm.Count == 0 && section.Layer == 1
                            ? new GridPoint(gate.Cell.X + side * tangent.X, gate.Cell.Z + side * tangent.Z)
                            : new GridPoint(terminal.X - gate.Outward.X, terminal.Z - gate.Outward.Z);
                        True(Touches(terminal, parent), "return touches previous wall");
                        True(section.Layer == 1 ? result.Layout.BuiltWalls.Contains(parent) || result.Layout.NaturalWalls.Contains(parent) :
                            arm.Count == 0 ? gate.BandWalls.Contains(parent) : inner != null && inner.Cells.Contains(parent),
                            "return anchors to previous physical wall " + terminal + " -> " + parent);
                        True(path.All(c => section.Cells.Contains(c) && !section.ServiceLane(gate).Contains(c)),
                            "return is built and leaves service lane clear");
                    }
                }
                AssertOuterWallConnectivity(result.Layout);
            }
            Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, "layered plan validates");
            if (layers == 3) ExportLayout("plains-industrial-3-layers", result);
        }
    }

    private static void AssertOuterWallConnectivity(ProceduralLayout layout, TerrainSnapshot terrain = null)
    {
        // The old straight arms touched only their gate band and failed this graph check.
        HashSet<GridPoint> roots = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls));
        HashSet<GridPoint> wallGraph = new HashSet<GridPoint>(roots.Concat(layout.OuterWalls)
            .Concat(layout.Gates.SelectMany(g => g.BandWalls)));
        // Complete outer shells can terminate against existing rock farther
        // out than the inner outline. Include actual intact rock as graph
        // edges, never as extra roots and never count planned excavation.
        if (terrain != null) wallGraph.UnionWith(terrain.Points().Where(c => terrain[c].Has(TerrainTraits.NaturalRock))
            .Except(layout.Excavation));
        HashSet<GridPoint> connected = new HashSet<GridPoint>(roots);
        Queue<GridPoint> queue = new Queue<GridPoint>(roots);
        while (queue.Count > 0)
        {
            GridPoint cell = queue.Dequeue();
            foreach (GridPoint next in new[] { new GridPoint(cell.X - 1, cell.Z), new GridPoint(cell.X + 1, cell.Z),
                new GridPoint(cell.X, cell.Z - 1), new GridPoint(cell.X, cell.Z + 1) })
                if (wallGraph.Contains(next) && connected.Add(next)) queue.Enqueue(next);
        }
        True(layout.OuterWalls.All(connected.Contains), "every outer wall connects to enclosure");
    }

    private static void FortificationTerrainMatrix()
    {
        var sites = new[]
        {
            new { Name = "pond-ridge", Terrain = Obstacles(120), Seed = 31UL },
            new { Name = "mountain-face", Terrain = Mountain(130, 110), Seed = 4UL },
            new { Name = "shoreline", Terrain = Build(120, 120, (x, z) => x < 31 + (z / 13) % 5 ? DeepWater : Ground), Seed = 31UL },
            new { Name = "water-corner", Terrain = Build(120, 120, (x, z) => x >= 69 && z >= 69 ? DeepWater : Ground), Seed = 31UL }
        };
        foreach (var site in sites)
            foreach (int layers in new[] { 1, 2, 3 })
            {
                ProceduralLayoutInput input = Input(site.Terrain, LayoutRequirements.FromPopulation(6, Industrial, Temperate), site.Seed,
                    new LayoutFortificationReservation(wallLayers: layers));
                ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
                Accepted(result);
                ProceduralLayout layout = result.Layout;
                LayoutTerrainGrid grid = LayoutTerrainGrid.Build(site.Terrain, 0);
                Equal(0, ProceduralLayoutValidator.Validate(input, layout).Count, site.Name + " layer " + layers + " validates");
                AssertOuterWallConnectivity(layout, site.Terrain);
                Equal(layout.Gates.Count * (layers - 1), layout.OuterSections.Count,
                    site.Name + " returns every added gate layer");
                foreach (LayoutWallSection section in layout.OuterSections)
                {
                    LayoutGate gate = layout.Gates[section.GateIndex];
                    GridPoint tangent = new GridPoint(gate.Outward.Z, gate.Outward.X);
                    foreach (int side in new[] { -1, 1 })
                    {
                        int armLength = side < 0 ? section.Left.Count : section.Right.Count;
                        if (armLength == 0) continue;
                        for (int distance = 0; distance <= armLength + 1; distance++)
                        {
                            GridPoint lane = new GridPoint(gate.Cell.X + distance * side * tangent.X +
                                (2 * section.Layer - 1) * gate.Outward.X, gate.Cell.Z + distance * side * tangent.Z +
                                (2 * section.Layer - 1) * gate.Outward.Z);
                            True(grid.Passable(lane), site.Name + " service lane " + lane);
                            True(!layout.OuterWalls.Contains(lane) && !gate.BandWalls.Contains(lane),
                                site.Name + " lane has no physical wall at " + lane);
                        }
                    }
                }
                Console.WriteLine("  " + site.Name + " layers=" + layers + " sections=" + layout.OuterSections.Count +
                    " outerWalls=" + layout.OuterWalls.Count() + " gates=" + layout.Gates.Count);
            }
    }

    private static void NarrowFortificationApproach()
    {
        // A roomy peninsula has only a three-cell-wide dry bridge to the edge.
        // The approach cannot be replaced by an assumed water crossing.
        TerrainSnapshot terrain = Build(120, 120, (x, z) =>
            (x >= 35 && x < 99 && z >= 27 && z < 94) || (x < 35 && z >= 59 && z <= 61)
                ? Ground : DeepWater);
        string digest = terrain.Digest;
        foreach (int layers in new[] { 1, 2, 3 })
        {
            ProceduralLayoutInput input = Input(terrain, LayoutRequirements.FromPopulation(6, Industrial, Temperate), 31,
                new LayoutFortificationReservation(wallLayers: layers));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
            True(result.Stats.EvaluationCount <= input.Budget.MaxEvaluations && result.Stats.AttemptCount <= 30,
                "narrow approach search bounded: " + result.Stats);
            Equal(digest, terrain.Digest, "narrow approach terrain unchanged");
            Accepted(result);
            Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, "narrow approach final occupancy");
            AssertOuterWallConnectivity(result.Layout, terrain);
            True(result.Layout.Gates.All(g => g.ExteriorRoute.Any(c => c.X < 35 && c.Z >= 59 && c.Z <= 61)),
                "every exterior route crosses the dry bridge");
            Console.WriteLine("  narrow approach layers=" + layers + " sections=" + result.Layout.OuterSections.Count +
                " gates=" + result.Layout.Gates.Count);
        }
    }

    private static void CornerWallReturnRecovery()
    {
        TerrainSnapshot corner = Build(120, 120, (x, z) => x >= 69 && z >= 69 ? DeepWater : Ground);
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(6, Industrial, Temperate);
        LayoutFortificationReservation fortification = new LayoutFortificationReservation(wallLayers: 3);
        ProceduralLayoutInput input = Input(corner, requirements, 31, fortification);
        ProceduralLayoutResult original = ProceduralLayoutPlanner.Plan(input);
        Accepted(original);
        LayoutWallSection section = original.Layout.OuterSections.First();
        GridPoint blocked = section.LeftReturn[0];
        TerrainSnapshot changed = Build(120, 120, (x, z) =>
            x == blocked.X && z == blocked.Z ? TerrainTraits.Occupied :
            x >= 69 && z >= 69 ? DeepWater : Ground);
        string digest = changed.Digest;
        ProceduralLayoutInput changedInput = Input(changed, requirements, 31, fortification);
        Contains(ProceduralLayoutValidator.Validate(changedInput, original.Layout), "outer_section_unreturned");
        ProceduralLayoutResult recovered = ProceduralLayoutPlanner.Plan(changedInput);
        True(recovered.Stats.AttemptCount <= 30 && recovered.Stats.EvaluationCount <= changedInput.Budget.MaxEvaluations,
            "corner recovery bounded: " + recovered.Stats);
        Equal(digest, changed.Digest, "corner recovery does not mutate terrain");
        Accepted(recovered);
        True(recovered.Layout.OuterWalls.All(c => !c.Equals(blocked)), "recovered walls avoid blocked corner");
        Equal(0, ProceduralLayoutValidator.Validate(changedInput, recovered.Layout).Count, "recovered corner validates");
        AssertOuterWallConnectivity(recovered.Layout);
    }

    private static void BlockedFortificationServiceLane()
    {
        TerrainSnapshot clear = Plains(120);
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(6, Industrial, Temperate);
        LayoutFortificationReservation fortification = new LayoutFortificationReservation(wallLayers: 2);
        ProceduralLayoutInput input = Input(clear, requirements, 31, fortification);
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
        Accepted(result);
        LayoutWallSection section = result.Layout.OuterSections.First();
        GridPoint blocked = section.ServiceTermini(result.Layout.Gates[section.GateIndex]).First();
        TerrainSnapshot obstructed = Build(120, 120, (x, z) => x == blocked.X && z == blocked.Z ? TerrainTraits.Occupied : Ground);
        ProceduralLayoutInput changed = Input(obstructed, requirements, 31, fortification);
        Contains(ProceduralLayoutValidator.Validate(changed, result.Layout), "outer_section_service_lane_blocked");
    }

    private static void P4BFireLaneCases()
    {
        GridPoint inner = new GridPoint(0, 0), approach = new GridPoint(6, 0);
        True(P4BFireLane.CanEngage(inner, approach, 1, 8, Array.Empty<GridPoint>()), "clear direct approach");
        GridPoint closedGate = new GridPoint(3, 0);
        True(!P4BFireLane.CanEngage(inner, approach, 1, 8, new[] { closedGate }),
            "closed opaque gate blocks an inner post");
        True(P4BFireLane.CanEngage(new GridPoint(3, 1), new GridPoint(6, 1), 1, 8, new[] { closedGate }),
            "an exterior flanking post can still cover the approach");
        True(!P4BFireLane.CanEngage(inner, new GridPoint(2, 0), 3, 8, Array.Empty<GridPoint>()),
            "minimum range creates a real blind zone");
        True(!P4BFireLane.CanEngage(inner, approach, 1, 5, Array.Empty<GridPoint>()),
            "maximum range excludes the far approach");
        True(!P4BFireLane.CanEngage(inner, new GridPoint(2, 2), 1, 8,
            new[] { new GridPoint(1, 0), new GridPoint(0, 1) }), "diagonal wall slit is not clear");
        True(P4BFireLane.CanEngage(inner, new GridPoint(2, 2), 1, 8, Array.Empty<GridPoint>()),
            "open diagonal remains visible");
    }

    private static void P4BFieldWallCases()
    {
        TerrainSnapshot terrain = Build(20, 20, (x, z) => Ground);
        GridPoint innerGate = new GridPoint(9, 4), outerGate = new GridPoint(9, 15);
        GridPoint[] gates = { innerGate, outerGate };
        List<GridPoint> baseline = Enumerable.Range(4, 12).Select(x => new GridPoint(x, 4))
            .Where(c => !c.Equals(innerGate)).ToList();
        List<GridPoint> fieldWall = Enumerable.Range(5, 11)
            .SelectMany(z => new[] { new GridPoint(4, z), new GridPoint(15, z) })
            .Concat(Enumerable.Range(5, 10).Select(x => new GridPoint(x, 15)))
            .Where(c => !c.Equals(outerGate)).ToList();
        List<GridPoint> crops = Enumerable.Range(6, 8)
            .SelectMany(z => Enumerable.Range(6, 8).Select(x => new GridPoint(x, z))).ToList();
        GridPoint post = new GridPoint(9, 17), access = new GridPoint(9, 2);
        IReadOnlyList<string> Valid(IEnumerable<GridPoint> b, IEnumerable<GridPoint> w,
            IEnumerable<GridPoint> f, IEnumerable<GridPoint> p, IEnumerable<GridPoint> roof = null) =>
            P4BFieldWall.Validate(terrain, f, b, w, gates, p, access, roof);
        Equal(0, Valid(baseline, fieldWall, crops, new[] { post }).Count,
            "attached field wall protects unroofed crops and leaves a post outside");
        Contains(Valid(baseline, fieldWall.Where(c => c.Z != 5), crops, new[] { post }),
            "field_wall_detached");
        Contains(Valid(baseline, fieldWall.Where(c => !c.Equals(new GridPoint(6, 15))),
            crops, new[] { post }), "field_exposed_behind_closed_wall");
        Contains(Valid(baseline, fieldWall, crops, new[] { new GridPoint(9, 5) }),
            "post_not_outside_field_wall");
        Contains(Valid(baseline, fieldWall, crops, new[] { post }, new[] { crops[0] }),
            "field_not_fertile_unroofed_ground");
    }

    private static void P4BFieldWallPlannerCases()
    {
        TerrainSnapshot terrain = Plains(160);
        for (int layers = 1; layers <= 3; layers++)
        {
            ProceduralLayoutInput input = Input(terrain, LayoutRequirements.FromPopulation(6, Industrial, Temperate), 31,
                new LayoutFortificationReservation(wallLayers: layers));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            Accepted(result);
            ProceduralLayout layout = result.Layout;
            HashSet<GridPoint> occupied = new HashSet<GridPoint>(layout.Enclosure
                .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points()))
                .Concat(layout.PowerYards.SelectMany(y => y.Cells))
                .Concat(layout.AllFixtures.SelectMany(f => f.Cells))
                .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex]))));
            string diagnostic;
            P4BFieldWallPlan plan = P4BFieldWallPlanner.PlanJoint(input, layout, occupied, out diagnostic);
            True(plan != null, "flat layer " + layers + " has a bounded attached field enclosure candidate");
            P4BFieldWallPlan replay = P4BFieldWallPlanner.PlanJoint(input, layout, occupied, out diagnostic);
            Equal(string.Join(";", plan.Walls), string.Join(";", replay.Walls),
                "field wall selection is deterministic");
            True(plan.Fields.Count >= layout.FieldCellsRequired, "field demand survives wall reservation");
            True(plan.Fields.All(c => !terrain[c].Has(TerrainTraits.Roofed)), "main field stays unroofed");
            True(plan.ExteriorPosts.Count == 2, "positions are outside the field-facing wall");
            Equal(terrain.Digest, input.Terrain.Digest, "field wall selection preserves terrain");
        }
    }

    private static void P4BJointSiteCases()
    {
        TerrainSnapshot flat = Plains(160);
        string originalDigest = flat.Digest;
        P4BJointSiteResult first = P4BJointSitePlanner.Find(flat, 124, 550, 4, 1234, 16, 4096);
        P4BJointSiteResult replay = P4BJointSitePlanner.Find(flat, 124, 550, 4, 1234, 16, 4096);
        True(first.Candidates.Count > 0, "flat land supplies a field-compound pair");
        Equal(first.Candidates[0].Digest, replay.Candidates[0].Digest, "joint reservations replay exactly");
        True(first.Evaluated <= 4096, "joint search is bounded");
        P4BJointSite site = first.Candidates[0];
        True(site.Crops.Count > 124, "all usable crop soil is planted beyond minimum demand");
        True(!site.Crops.Intersect(site.Lane.Concat(site.Wall).Concat(site.PostClearance)).Any(),
            "crops do not consume gate, wall, lane or defense clearance");
        True(!site.Crops.Any(site.Compound.Contains), "compound is outside crop reservation");
        List<P4BJointSite> localGrowth = first.Candidates
            .Select(c => P4BJointSitePlanner.GrowthVariants(c, LayoutTerrainGrid.Build(flat, 4), 4).ToList())
            .FirstOrDefault(v => v.Count > 1);
        True(localGrowth != null, "a roomy tile can enlarge an existing compound without moving its field");
        foreach (P4BJointSite grown in localGrowth.Skip(1))
        {
            Equal(localGrowth[0].Field.ToString(), grown.Field.ToString(), "growth keeps the chosen field");
            Equal(localGrowth[0].InnerGate, grown.InnerGate, "growth keeps the field crossing");
            True(localGrowth[0].Compound.Points().All(grown.Compound.Contains),
                "growth contains the original compound footprint");
            True(grown.Compound.Width > localGrowth[0].Compound.Width && grown.Compound.Contains(grown.Anchor),
                "growth provides more room around a valid local anchor");
        }
        Equal(originalDigest, flat.Digest, "input terrain unchanged");

        TerrainSnapshot separated = Build(160, 160, (x, z) =>
            (x >= 18 && x <= 40 && z >= 18 && z <= 40) ||
            (x >= 73 && x <= 150 && z >= 73 && z <= 150) ? Ground : DeepWater);
        P4BJointSiteResult paired = P4BJointSitePlanner.Find(separated, 124, 550, 3, 4567, 16, 4096);
        True(paired.Candidates.Count > 0, "second fertile region has adjacent compound room");
        True(paired.Candidates.All(c => c.Field.MinX >= 70 && c.Field.MinZ >= 70),
            "fertile patch without a compound cannot win by soil score alone");
        TerrainSnapshot roofed = Build(80, 80, (x, z) => Ground | TerrainTraits.Roofed);
        Equal(0, P4BJointSitePlanner.Find(roofed, 124, 400, 3, 1).Candidates.Count,
            "roofed land cannot become an outdoor field");
    }

    private static void P4BTurretPlacementCases()
    {
        TerrainSnapshot terrain = Plains(160);
        ProceduralLayoutInput input = null;
        ProceduralLayoutResult result = null;
        List<string> rejections = new List<string>();
        foreach (ulong seed in new ulong[] { 184, 185, 186, 187, 31, 32, 1, 2 })
        {
            ProceduralLayoutInput candidate = Input(terrain,
                LayoutRequirements.FromPopulation(4, Industrial, Temperate, stage: MidStage), seed,
                new LayoutFortificationReservation(wallLayers: 1));
            ProceduralLayoutResult planned = ProceduralLayoutPlanner.Plan(candidate, requireJointSite: true);
            if (planned.Layout == null) { rejections.Add(seed + ":" + planned.RejectionCode); continue; }
            input = candidate;
            result = planned;
            break;
        }
        True(result != null, "mid-industrial joint-site fixtures rejected: " + string.Join(",", rejections));
        Accepted(result);
        ProceduralLayout layout = result.Layout;
        True(layout.Turret != null, "joint layout retains selected optional turret");
        Equal(0, ProceduralLayoutValidator.Validate(input, layout).Count,
            "integrated turret passes final layout validation");
        HashSet<GridPoint> occupied = new HashSet<GridPoint>(layout.Enclosure
            .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points()))
            .Concat(layout.PowerYards.SelectMany(y => y.Cells))
            .Concat(layout.AllFixtures.SelectMany(f => f.Cells))
            .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex]))));
        string diagnostic;
        P4BTurretPlan turret = P4BTurretPlanner.Plan(input, layout, layout.FieldWall, occupied, out diagnostic);
        True(turret != null, "late-industrial flat emplacement: " + diagnostic);
        True(turret.Footprint.Count > 0 && turret.ServiceRoute.Count > 1,
            "physical footprint and service route: " + turret.DefName + " cells=" + turret.Footprint.Count +
            " route=" + turret.ServiceRoute.Count + " " + diagnostic);
        True(turret.LoadedFuelUnits > 0 && turret.ReserveFuelUnits > 0 && turret.ImportValue > 0,
            "turret load and stock have finite item and spend counts");
        True(turret.CoverCells.Count > 0 && turret.CoverCells.All(c =>
            turret.Footprint.Any(f => Math.Abs(f.X - c.X) + Math.Abs(f.Z - c.Z) == 1)),
            "turret has adjacent low cover, not a remote decorative bag");
        True(layout.Materials.CostListSummary.ContainsKey("Steel"),
            "turret construction enters the declared materials ledger");
        CoreCatalog.TryGet(turret.DefName, out CatalogDef turretDef);
        P4BTurretPlan impossible = new P4BTurretPlan(turretDef, turret.Origin,
            layout.FieldWall.Fields.Take(1), turret.ServiceCell, turret.ServiceRoute,
            turret.FuelDefName, turret.LoadedFuelUnits, turret.ReserveFuelUnits, turret.ImportValue);
        True(P4BTurretPlanner.Validate(input, layout, impossible).Contains("footprint"),
            "final validator rejects an occupied/incorrect turret footprint");
        P4BTurretPlan replay = P4BTurretPlanner.Plan(input, layout, layout.FieldWall, occupied, out diagnostic);
        Equal(turret.Digest, replay.Digest, "emplacement replay");
        P4BTurretPlan blocked = P4BTurretPlanner.Plan(input, layout, layout.FieldWall,
            terrain.Points(), out diagnostic);
        True(blocked == null, "occupied exterior rejects the optional turret");
        Equal(terrain.Digest, input.Terrain.Digest, "emplacement leaves terrain unchanged");
    }

    private static void P4BHighIndustrialJointSite()
    {
        TerrainSnapshot terrain = Plains(160);
        List<string> rejections = new List<string>();
        ProceduralLayoutResult accepted = null;
        ProceduralLayoutInput acceptedInput = null;
        foreach (ulong seed in Enumerable.Range(1, 8).Select(n => (ulong)n))
        {
            ProceduralLayoutInput input = Input(terrain,
                LayoutRequirements.FromPopulation(4, Industrial, Temperate,
                    stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate), importValueLimit: 10000), seed,
                new LayoutFortificationReservation(wallLayers: 1));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            if (result.Layout == null)
            {
                rejections.Add(seed + ":" + result.RejectionCode + ":" +
                    string.Join("+", result.Diagnostics.TakeLast(12)));
                continue;
            }
            if (result.Layout.Turret == null || result.Layout.Turret.MinimumRange == 0 || result.Layout.Turret.Support == null)
            {
                rejections.Add(seed + ":no-supported-heavy-emplacement");
                continue;
            }
            accepted = result;
            acceptedInput = input;
            break;
        }
        True(accepted != null, "high-industrial joint sites: " + string.Join(",", rejections));
        True(accepted.Layout.TechStage.Level == SettlementTechStageLevel.IndustrialLate,
            "late-stage facility and defense eligibility survives planning");
        Console.WriteLine("  late-industrial defense " + string.Join(";", accepted.Diagnostics.Where(d => d.Stage == "turret")) +
            " posts=" + string.Join(";", accepted.Layout.ApproachDefense.Sides.Select(s => s.Id + ":" + s.Post +
                "/" + (s.Post.HasValue ? s.Samples.Count(c => P4BFireLane.CanEngage(s.Post.Value, c, 8, 25,
                    P4BApproachDefensePlanner.Opaque(accepted.Layout, terrain))) : 0) + "/" + s.Samples.Count)));
        Equal(0, ProceduralLayoutValidator.Validate(acceptedInput, accepted.Layout).Count,
            "late-stage joint field remains valid");
        True(accepted.Layout.Turret != null && accepted.Layout.Turret.Support != null,
            "high-industrial heavy turret has a smaller close-support turret; chosen=" +
            accepted.Layout.Turret?.DefName);
        True(accepted.Layout.Turret.All.All(t => t.CoverCells.Count > 0),
            "both turrets have adjacent cover");
        P4BTurretPlan heavy = accepted.Layout.Turret;
        CoreCatalog.TryGet(heavy.DefName, out CatalogDef heavyDef);
        P4BTurretPlan unsupported = new P4BTurretPlan(heavyDef, heavy.Origin, heavy.Footprint,
            heavy.ServiceCell, heavy.ServiceRoute, heavy.FuelDefName, heavy.LoadedFuelUnits,
            heavy.ReserveFuelUnits, heavy.ImportValue - heavy.Support.ImportValue,
            coverCells: heavy.CoverCells);
        True(P4BTurretPlanner.Validate(acceptedInput, accepted.Layout, unsupported)
            .Contains("blind_zone_without_small_turret"), "an isolated large gun is rejected");
        P4BTurretPlan uncovered = new P4BTurretPlan(heavyDef, heavy.Origin, heavy.Footprint,
            heavy.ServiceCell, heavy.ServiceRoute, heavy.FuelDefName, heavy.LoadedFuelUnits,
            heavy.ReserveFuelUnits, heavy.ImportValue - heavy.Support.ImportValue, heavy.Support);
        True(P4BTurretPlanner.Validate(acceptedInput, accepted.Layout, uncovered)
            .Contains("adjacent_cover"), "missing turret-adjacent cover is rejected");
        True(P4BTurretPlanner.Validate(acceptedInput, accepted.Layout, uncovered)
            .Contains("adjacent_flank_cover"), "missing flank cover is rejected independently of forward cover");
    }

    private static void P4BTurretNetwork()
    {
        TerrainSnapshot terrain = Plains(160);
        LayoutRequirements needs = LayoutRequirements.FromPopulation(4, Industrial, Temperate,
            stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate));
        ProceduralLayoutInput input = null;
        ProceduralLayoutResult result = null;
        // Additional magazines change the room envelope. Optional defended
        // groups must be proven on a bounded corpus, not demanded on one seed.
        foreach (ulong seed in Enumerable.Range(1, 8).Select(n => (ulong)n))
        {
            var candidateInput = Input(terrain, needs, seed, new LayoutFortificationReservation(1));
            ProceduralLayoutResult candidate = ProceduralLayoutPlanner.Plan(candidateInput, requireJointSite: true);
            Accepted(candidate);
            input = candidateInput; result = candidate;
            if (candidate.Layout.Turret != null && candidate.Layout.Turret.Groups.Count() > 1 &&
                candidate.Layout.Turret.All.Count() > 2) break;
        }
        Accepted(result);
        P4BTurretPlan network = result.Layout.Turret;
        True(network != null && network.Groups.Count() > 1 && network.All.Count() > 2,
            "flat settlement is not capped at a single heavy/mini pair: " +
            string.Join(";", result.Diagnostics.Where(d => d.Stage == "turret")));
        Console.WriteLine("  network groups=" + network.Groups.Count() + " turrets=" + network.All.Count() +
            " watts=" + network.TotalWatts + " value=" + network.ImportValue);
        Console.WriteLine("  groups " + string.Join(";", network.Groups.Select(g => g.DefName + "@" + g.Origin + "/" + g.Side)));
        Console.WriteLine("  sides " + string.Join(";", result.Layout.ApproachDefense.Sides.Select(s =>
            s.Id + ":post=" + s.Post + "/samples=" + s.Samples.Count)));
        Console.WriteLine("  turret diagnostics " + string.Join(";", result.Diagnostics.Where(d => d.Stage == "turret")));
        Equal(0, P4BTurretPlanner.Validate(input, result.Layout, network).Count, "complete network validates");
        Equal(network.All.Sum(t => t.Watts), network.TotalWatts, "power counts every gun once");
        HashSet<GridPoint> wiring = P4BTurretPlanner.PowerAccessCells(result.Layout);
        True(result.Layout.FieldWall.Access.All(wiring.Contains) && wiring.Contains(result.Layout.FieldWall.InnerGate.Value),
            "conduit reservation includes the explicit field lane and its inner gate");
        True(P4BTurretPlanner.PowerTerminalsConnected(input, result.Layout, network),
            "each network gun has a permitted conduit path into the compound");
        True(network.Groups.All(g => g.MinimumRange == 0 || g.Support != null), "every heavy retains close support");
        True(LayoutLoadoutModel.Build(input, result.Layout).Residents.Any(r =>
            r.Weapon?.DefName == "Gun_SniperRifle"), "turret guards have a real equipped sniper rifle");
        True(!result.Layout.ApproachDefense.Sides.Single(s => s.Id == "south").Post.Equals(
            result.Layout.ApproachDefense.Sides.Single(s => s.Id == "west").Post),
            "large southern exposure gets its own post instead of borrowing a weak western flank");
        P4BStaffingAudit staffing = P4BStaffing.Audit(input, result.Layout, network);
        Equal(network.All.Count(), staffing.GuardedGuns,
            "every selected gun is guarded by a concurrently staffed sniper post");
        Equal(network.All.Count(), staffing.CloseSupportedGuns,
            "every selected gun has a concurrently staffed close defender");
        Equal(1, staffing.SniperPosts.Count,
            "the four-resident loadout funds one marksman and cannot borrow that pawn across separated fronts");
        True(result.Layout.ApproachDefense.Sides.Single(s => s.Id == "south").Positions.Count > 0,
            "the exposed southern approach retains usable pawn positions when its turret group is omitted");
        HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(result.Layout, terrain);
        opaque.UnionWith(network.All.SelectMany(t => t.Footprint));
        foreach (P4BTurretPlan gun in network.All)
        {
            P4BTurretPlanner.SniperGuardCoverage(result.Layout.ApproachDefense, gun, opaque,
                out int threats, out int answered, out GridPoint? guard);
            Console.WriteLine("  guard gun=" + gun.DefName + " side=" + gun.Side + " post=" + guard +
                " gun=" + gun.Origin + " sniperThreats=" + threats + " answered=" + answered);
            True(threats > 0 && answered * 10 >= threats * 9,
                "a same-front post answers at least 90% of modeled sniper firing cells against " + gun.DefName);
            True(P4BTurretPlanner.NearbyPawnSupport(result.Layout.ApproachDefense, gun, opaque),
                "each gun has a nearby usable pawn post with close-approach shots");
        }
        bool Shot(P4BTurretPlan gun, GridPoint target)
        {
            HashSet<GridPoint> blockers = new HashSet<GridPoint>(opaque);
            blockers.ExceptWith(gun.Footprint);
            return P4BFireLane.CanEngage(gun.Origin, target, (int)Math.Ceiling(gun.MinimumRange),
                (int)Math.Floor(gun.MaximumRange), blockers);
        }
        GridPoint[] targets = result.Layout.ApproachDefense.Sides.SelectMany(s => s.Samples).Distinct().ToArray();
        True(targets.Any(c => network.All.Count(g => Shot(g, c)) >= 2), "network has interlocking fields of fire");
        int Utility(IEnumerable<P4BTurretPlan> guns) => targets.Sum(c =>
        {
            int shots = guns.Count(g => Shot(g, c));
            // Same bounded utility as production: first shot earns four,
            // simultaneous crossfire earns one; a third redundant gun earns none.
            return shots == 0 ? 0 : shots == 1 ? 4 : 5;
        });
        True(Utility(network.All) > Utility(new[] { network, network.Support }.Where(g => g != null)),
            "additional groups add unique coverage or simultaneous crossfire, not just gun count");
        P4BTurretPlan peer = network.AdditionalGroups.First();
        True(P4BTurretPlanner.Validate(input,
            result.Layout.WithAddedObstacles(new[] { peer.ServiceCell }), network).Contains("service_cell"),
            "blocking a peer's refuel cell is rejected");
        True(P4BTurretPlanner.Validate(input,
            result.Layout.WithAddedObstacles(new[] { peer.Footprint[0] }), network).Contains("footprint"),
            "blocking a peer's footprint is rejected");
        LayoutRequirements poor = LayoutRequirements.FromPopulation(4, Industrial, Temperate,
            stage: needs.Stage, importValueLimit: 0);
        ProceduralLayoutInput poorInput = Input(terrain, poor, input.Seed, new LayoutFortificationReservation(1));
        True(P4BTurretPlanner.Validate(poorInput, result.Layout, network).Contains("network_import_budget"),
            "whole-network fuel, construction and cover are charged against one budget");
        string diagnostic;
        P4BTurretPlan poorPlan = P4BTurretPlanner.Plan(poorInput, result.Layout, result.Layout.FieldWall,
            Enumerable.Empty<GridPoint>(), out diagnostic);
        True(poorPlan == null, "insufficient shared budget omits optional guns rather than spending per group");
        LayoutRequirements rich = LayoutRequirements.FromPopulation(4, Industrial, Temperate,
            stage: needs.Stage, importValueLimit: 20000);
        ProceduralLayoutInput richInput = Input(terrain, rich, input.Seed, new LayoutFortificationReservation(1));
        HashSet<GridPoint> occupied = new HashSet<GridPoint>(result.Layout.Enclosure
            .Concat(result.Layout.Rooms.SelectMany(r => r.Bounds.Points()))
            .Concat(result.Layout.PowerYards.SelectMany(y => y.Cells))
            .Concat(result.Layout.AllFixtures.SelectMany(f => f.Cells)));
        P4BTurretPlan richPlan = P4BTurretPlanner.Plan(richInput, result.Layout, result.Layout.FieldWall,
            occupied, out diagnostic);
        True(richPlan != null && richPlan.ImportValue >= network.ImportValue,
            "higher capacity can fund a more expensive supported weapon mix; group count is not utility: " + diagnostic);
        True(richPlan.Groups.Count() <= result.Layout.ApproachDefense.Sides.Count(s => s.Exposed),
            "group count is bounded by exposed fronts rather than filling terrain with guns");
        Equal(0, P4BTurretPlanner.Validate(richInput, result.Layout, richPlan).Count,
            "richer network respects final spacing, mutual support and aggregate power");
        Console.WriteLine("  rich network groups=" + richPlan.Groups.Count() + " turrets=" + richPlan.All.Count() +
            " watts=" + richPlan.TotalWatts + " value=" + richPlan.ImportValue);
        CoreCatalog.TryGet(network.DefName, out CatalogDef rootDef);
        P4BTurretPlan duplicate = new P4BTurretPlan(rootDef, network.Origin, network.Footprint,
            network.ServiceCell, network.ServiceRoute, network.FuelDefName, network.LoadedFuelUnits,
            network.ReserveFuelUnits, network.ImportValue - (network.Support?.ImportValue ?? 0) -
                network.AdditionalGroups.Sum(g => g.ImportValue), network.Support, network.CoverCells,
            network.AccessGate, network.Outward, network.Side, network.AdditionalGroups.Concat(new[] { peer }));
        True(P4BTurretPlanner.Validate(input, result.Layout, duplicate).Contains("network_collision_or_blast"),
            "reusing a peer emplacement is rejected even when each individual group is legal");
        Equal(result.Layout.Digest, ProceduralLayoutPlanner.Plan(input, requireJointSite: true).Layout.Digest,
            "network replay is deterministic");
    }

    private static void P4BStaffingIsExclusive()
    {
        GridPoint east = new GridPoint(20, 10), south = new GridPoint(10, 20);
        IReadOnlyList<IReadOnlyCollection<GridPoint>> separated = new IReadOnlyCollection<GridPoint>[] {
            new[] { east }, new[] { east }, new[] { south }
        };
        List<GridPoint> one = P4BStaffing.GreedyCover(separated, 1);
        Equal(1, one.Count, "one funded sniper occupies one post");
        Equal(2, separated.Count(options => options.Contains(one[0])),
            "two same-front guns can share a real guard while the separate front stays responder-dependent");
        List<GridPoint> two = P4BStaffing.GreedyCover(separated, 2);
        Equal(2, two.Count, "a second funded sniper can guard the other front concurrently");
        True(separated.All(options => options.Any(two.Contains)), "both fronts have staffed guard coverage");
    }

    private static void P4BFieldFallbackPosts()
    {
        TerrainSnapshot terrain = Plains(160);
        LayoutRequirements needs = LayoutRequirements.FromPopulation(4, Industrial, Temperate,
            stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate));
        ProceduralLayoutInput input = Input(terrain, needs, 1, new LayoutFortificationReservation(1));
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Accepted(result);
        ProceduralLayout layout = result.Layout;
        IReadOnlyCollection<GridPoint> publicCells = ProceduralLayoutValidator.PublicCirculation(input, layout);
        EncounterProfile profile = layout.Court != null ? P3EncounterProfiles.Industrial :
            P3EncounterProfiles.IndustrialNoMortar;
        EncounterPlan encounter = GeometryEncounterPlanner.Plan(new GeometryEncounterInput(layout.Digest, profile,
            layout.Enclosure, layout.BuiltWalls.Concat(layout.NaturalWalls).Concat(layout.OuterWalls)
                .Concat(layout.FieldWall.Walls),
            layout.Gates.Select(g => new KeyValuePair<GridPoint, GridPoint>(g.Cell, g.Inside))
                .Concat(layout.ApproachDefense.SallyPorts.Where(p => !p.Field)
                    .Select(p => new KeyValuePair<GridPoint, GridPoint>(p.Cell, p.Inside))),
            publicCells, LayoutStockCells.ForRooms(layout.Rooms, LayoutRoomKind.Storage),
            layout.Court?.Cells.FirstOrDefault(), layout.Court?.Access.FirstOrDefault()));
        IReadOnlyList<string> fallback = P4BFieldFallback.Select(input, layout, encounter);
        True(fallback.Count > 0, "compound gate has a real firing position after its crossing opens");
        True(fallback.All(id => encounter.Posts.Any(p => p.Id == id && p.Inner &&
            publicCells.Contains(p.Cell))), "fallback posts are inside accessible public compound geometry");
    }

    private static void P4BFinalPathAuditSynchronization()
    {
        // Model the installed game's generation-time suppression: removing a
        // tree changes geometry but leaves its impassable cost cached until flush.
        bool disabled = true, geometryPassable = true, cachedPassable = false;
        int flushes = 0, restores = 0;
        Action flush = () => { disabled = false; cachedPassable = geometryPassable; flushes++; };
        Action restore = () => { disabled = true; restores++; };
        True(!cachedPassable && geometryPassable, "stale grid reproduces a false path rejection");
        using (var audit = new P4BFinalPathAuditScope(disabled, flush, restore))
        {
            True(cachedPassable && !disabled, "audit reads final realized geometry, not deferred costs");
        }
        True(disabled && restores == 1 && flushes == 1, "caller batching restored after successful audit");
        try
        {
            using var audit = new P4BFinalPathAuditScope(disabled, flush, restore);
            throw new InvalidOperationException("synthetic audit failure");
        }
        catch (InvalidOperationException) { }
        True(disabled && restores == 2, "failure restores batching before transactional recovery");
        using (var audit = new P4BFinalPathAuditScope(false, flush, restore)) { }
        Equal(2, flushes, "normal play does not toggle path-grid mode");
        Equal(2, restores, "normal play needs no restoration");
        var once = new P4BFinalPathAuditScope(true, flush, restore);
        once.Dispose(); once.Dispose();
        Equal(3, restores, "restoration is idempotent");
        try
        {
            using var audit = new P4BFinalPathAuditScope(true,
                () => { disabled = false; throw new InvalidOperationException("synthetic flush failure"); }, restore);
        }
        catch (InvalidOperationException) { }
        True(disabled && restores == 4, "a failed flush also restores batching");
    }

    private static void P4BHighIndustrialCapturedFlat()
    {
        TerrainSnapshot terrain = LoadCapturedFlatTerrain();
        LayoutRequirements needs = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
            new LayoutClimate(-8, 33), stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate));
        LayoutLocalResources resources = new LayoutLocalResources(28952, new[] { "BlocksGranite", "BlocksMarble" },
            steamGeyserCells: new[] { new GridPoint(70, 32), new GridPoint(58, 69) });
        ProceduralLayoutInput input = new ProceduralLayoutInput(needs, terrain, 0xE615D0C09671DAF4UL,
            CoreCatalog, new LayoutFortificationReservation(1), resources: resources);
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        True(result.Success, "captured high-industrial flat rejected=" + result.RejectionCode + " " + result.Stats +
            " tail=" + string.Join("|", result.Diagnostics.Where(d => d.Stage == "attempt").TakeLast(6)));
        Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count,
            "captured high-industrial layout validates");
    }

    private static void P4BHighIndustrialFlatDensity()
    {
        // Rejected live flat tiles had ~400-1200 natural-rock cells,
        // 0-300 water cells and scattered other occupied cells on 160x160 maps.
        // Use clustered rock/water plus independent occupied cells, while
        // keeping shrubs/trees explicitly clearable.
        ulong[] seeds = { 0xE615D0C09671DAF4UL, 0xEC11B98F175FC0F6UL, 0xD435ADBACA556D3FUL,
            0x7A5C6FE3CCF5E177UL, 0x3B29F11CF334903EUL, 0x81097B4D691BA0C4UL,
            0xDAFF8C368DA22507UL, 0xE1AD2270845C1220UL, 0x99A7D07C99B3504AUL,
            0x186C3EB2863CEDC7UL, 0xFC9C6ECEC3120E68UL, 0x59002D4741A6741BUL };
        int accepted = 0;
        int grownSites = 0;
        List<string> failures = new List<string>();
        for (int sample = 0; sample < seeds.Length; sample++)
        {
            int salt = sample;
            TerrainSnapshot terrain = Build(160, 160, (x, z) =>
            {
                uint cluster = (uint)((x / 3) * 73856093 ^ (z / 3) * 19349663 ^ salt * 83492791);
                cluster ^= cluster >> 13;
                cluster *= 1274126177u;
                if (cluster % 1000 < (sample == 0 ? 46 : 27 + (sample % 3) * 9)) return TerrainTraits.NaturalRock | TerrainTraits.Occupied |
                    TerrainTraits.SupportsHeavy | TerrainTraits.Roofed;
                uint water = (uint)((x / 5) * 19349663 ^ (z / 5) * 83492791 ^ salt * 73856093);
                water ^= water >> 13;
                if (water % 1000 < 12) return DeepWater;
                uint point = (uint)(x * 19349663 ^ z * 83492791 ^ salt * 73856093);
                point ^= point >> 13;
                if (point % 1000 < 19) return Ground | TerrainTraits.Occupied;
                return point % 1000 < 460 ? Ground | TerrainTraits.ClearablePlant : Ground;
            });
            LayoutRequirements needs = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
                new LayoutClimate(-8, 33), stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate));
            LayoutLocalResources resources = new LayoutLocalResources(30000,
                new[] { "BlocksGranite", "BlocksMarble" },
                steamGeyserCells: new[] { new GridPoint(70, 32), new GridPoint(58, 69) });
            ProceduralLayoutInput input = new ProceduralLayoutInput(needs, terrain, seeds[sample],
                CoreCatalog, new LayoutFortificationReservation(1), resources: resources);
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            if (!result.Success)
            {
                failures.Add(sample + ":" + result.RejectionCode + ":" + result.Stats + ":" +
                    string.Join("+", result.Diagnostics.Where(d => d.Stage == "attempt" || d.Stage == "joint_site")
                        .TakeLast(6)));
                continue;
            }
            accepted++;
            if (result.Diagnostics.Any(d => d.Stage == "joint_site" && d.Code == "grow_compound")) grownSites++;
            Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count,
                "high-industrial clutter layout " + sample + " validates");
        }
        True(accepted == seeds.Length, "all plausible flat maps accept; " +
            accepted + "/" + seeds.Length + " failures=" + string.Join("|", failures));
        True(grownSites > 0, "the flat corpus exercises same-site compound growth");
        Console.WriteLine("  high-industrial flat-density acceptance: " + accepted + "/" + seeds.Length +
            " localGrowth=" + grownSites +
            (failures.Count == 0 ? string.Empty : " rejected=" + string.Join("|", failures)));
    }

    private static void P4BJointPackingCases()
    {
        TerrainSnapshot terrain = Plains(160);
        ProceduralLayoutInput input = Input(terrain,
            LayoutRequirements.FromPopulation(4, Industrial, Temperate), 184,
            new LayoutFortificationReservation(wallLayers: 1));
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Accepted(result);
        P4BJointSite site = result.Layout.JointSite;
        True(site != null, "joint site is carried into the layout");
        True(result.Layout.FieldWall != null, "accepted layout includes the attached field wall, not just a candidate");
        True(site.Crops.Count > input.Requirements.FieldCells, "all usable field soil is reserved for crops");
        Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count,
            "final room packing retains the field and defense reservations");
        ProceduralLayoutResult replay = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Equal(result.Layout.Digest, replay.Layout.Digest, "joined layout and site digest replay");
        P4BFieldWallPlan wall = result.Layout.FieldWall;
        True(wall.ArrivalRoute.Contains(wall.InnerGate.Value) && wall.ArrivalRoute.Contains(wall.OuterGate),
            "accepted arrival path crosses both reserved field gates");
        True(wall.Fields.Count > input.Requirements.FieldCells,
            "realized field plants beyond minimum crop demand while preserving its lane");
        True(wall.CoverCells.Count >= wall.ExteriorPosts.Count,
            "each exterior post retains at least its reserved forward cover; actual=" + wall.CoverCells.Count);
        True(wall.ExteriorPosts.All(wall.Access.Contains),
            "outside post positions belong to the defended access reservation");
        True(wall.CoverCells.All(c => !wall.Access.Contains(c) && !wall.Fields.Contains(c)),
            "cover stays outside the walkable access and crop cells");
        Equal(2, wall.UsablePosts.Count(), "both flat-map posts have validated screened access");
        GridPoint outward = result.Layout.Gates[wall.GateIndex].Outward;
        GridPoint tangent = new GridPoint(outward.Z, outward.X);
        foreach (int flank in new[] { -1, 1 })
        {
            GridPoint post = new GridPoint(wall.OuterGate.X + outward.X * 2 + tangent.X * flank * 3,
                wall.OuterGate.Z + outward.Z * 2 + tangent.Z * flank * 3);
            True(wall.ExteriorPosts.Contains(post), "paired field flanks retain a pawn position");
            for (int lateral = 1; lateral <= 3; lateral++)
            {
                GridPoint approach = new GridPoint(wall.OuterGate.X + outward.X * 2 + tangent.X * flank * lateral,
                    wall.OuterGate.Z + outward.Z * 2 + tangent.Z * flank * lateral);
                True(wall.Access.Contains(approach), "gate-to-post approach remains walkable");
                True(wall.CoverCells.Contains(new GridPoint(approach.X + outward.X,
                    approach.Z + outward.Z)), "approach has adjacent outward cover at " + approach);
            }
            True(wall.CoverCells.Contains(new GridPoint(post.X + outward.X, post.Z + outward.Z)) &&
                wall.CoverCells.Contains(new GridPoint(post.X + tangent.X * flank, post.Z + tangent.Z * flank)),
                "post has forward and exposed-flank cover at " + post + " cover=" + wall.CoverCells.Count);
        }
        foreach (P4BPostAccessPlan access in wall.PostAccess)
        {
            True(wall.Walls.Contains(access.WallReturn), "rear return is a physical connected wall");
            Equal(2, access.ExposedCells.Count, "open gate-axis crossing is not mislabeled as covered");
            True(access.Route.First().Equals(wall.OuterGate) && access.Route.Last().Equals(access.Post),
                "explicit route joins gate and post");
        }
        P4BPostAccessPlan firstAccess = wall.PostAccess.First();
        P4BFieldWallPlan missingScreen = new P4BFieldWallPlan(wall.GateIndex, wall.Fields, wall.Walls,
            wall.OuterGate, wall.ExteriorPosts, wall.InnerGate, wall.Access, wall.ArrivalRoute,
            wall.CoverCells.Except(new[] { firstAccess.Screens.First() }), wall.CoverDefName, wall.PostAccess);
        True(P4BFieldWallPlanner.ValidatePostAccess(terrain, missingScreen, outward,
            Enumerable.Empty<GridPoint>()).Contains("post_screen_disconnected_or_missing"),
            "removing an advertised screen invalidates its post");
        True(P4BFieldWallPlanner.ValidatePostAccess(terrain, wall, outward,
            new[] { firstAccess.Route[3] }).Contains("post_access_blocked"),
            "final occupancy cannot obstruct an advertised route");
        P4BFieldWallPlan omitted = P4BFieldWallPlanner.PlanJoint(input, result.Layout,
            new[] { firstAccess.WallReturn }, out string omissionDiagnostic);
        True(omitted != null, "optional return obstruction retains the accepted field enclosure: " + omissionDiagnostic);
        Equal(1, omitted.UsablePosts.Count(), "obstructed flank is omitted, not advertised as protected");
        True(omitted.PostAccess.Single(p => !p.Usable).OmissionReason.Length > 0,
            "omission reason is retained in the plan");
        TerrainSnapshot wetReturn = Build(160, 160, (x, z) =>
            firstAccess.WallReturn.Equals(new GridPoint(x, z)) ? DeepWater : Ground);
        ProceduralLayoutInput wetInput = Input(wetReturn,
            LayoutRequirements.FromPopulation(4, Industrial, Temperate), 184,
            new LayoutFortificationReservation(wallLayers: 1));
        P4BFieldWallPlan wetPlan = P4BFieldWallPlanner.PlanJoint(wetInput, result.Layout,
            Enumerable.Empty<GridPoint>(), out string wetDiagnostic);
        True(wetPlan != null, "water under an optional return does not reject the base: " + wetDiagnostic);
        Equal(1, wetPlan.UsablePosts.Count(), "terrain-constrained screen incompleteness is explicit");
        Equal(0, P4BFieldWallPlanner.ValidatePostAccess(wetReturn, wetPlan, outward,
            Enumerable.Empty<GridPoint>()).Count, "remaining advertised route is valid on constrained terrain");
        foreach (ulong seed in new ulong[] { 185, 186, 187 })
        {
            ProceduralLayoutInput varied = Input(terrain,
                LayoutRequirements.FromPopulation(4, Industrial, Temperate), seed,
                new LayoutFortificationReservation(wallLayers: 1));
            ProceduralLayoutResult alternate = ProceduralLayoutPlanner.Plan(varied, requireJointSite: true);
            Accepted(alternate);
            True(alternate.Layout.FieldWall != null,
                "multiple seeded room packings retain a joined field wall");
        }
    }

    private static void P4BApproachDefenseCases()
    {
        var weakChoices = new[]
        {
            new { Name = "perpendicular-gate", Shots = 5, Fronts = 2, NeedsPort = false,
                SameFacing = false, Cell = new GridPoint(1, 1) },
            new { Name = "southern-sally", Shots = 38, Fronts = 1, NeedsPort = true,
                SameFacing = true, Cell = new GridPoint(2, 2) }
        };
        var rankedWeak = P4BApproachDefensePlanner.RankPositions(weakChoices, 92,
            c => c.Shots, c => c.Fronts, c => c.NeedsPort, c => c.SameFacing, c => c.Cell);
        Equal("southern-sally", rankedWeak[0].Name,
            "a post seeing 5/92 southern samples cannot outrank a useful same-facing sally port");
        var usefulGateChoices = new[]
        {
            new { Name = "existing-gate", Shots = 49, Fronts = 2, NeedsPort = false,
                SameFacing = false, Cell = new GridPoint(1, 1) },
            new { Name = "extra-port", Shots = 50, Fronts = 1, NeedsPort = true,
                SameFacing = true, Cell = new GridPoint(2, 2) }
        };
        var rankedUseful = P4BApproachDefensePlanner.RankPositions(usefulGateChoices, 58,
            c => c.Shots, c => c.Fronts, c => c.NeedsPort, c => c.SameFacing, c => c.Cell);
        Equal("existing-gate", rankedUseful[0].Name,
            "a well-covered existing gate need not be replaced by a marginally better port");
        TerrainSnapshot terrain = Plains(160);
        ProceduralLayoutInput input = Input(terrain,
            LayoutRequirements.FromPopulation(4, Industrial, Temperate,
                stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate)), 1,
            new LayoutFortificationReservation(wallLayers: 1));
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Accepted(result);
        P4BApproachDefensePlan defense = result.Layout.ApproachDefense;
        True(defense != null, "approach analysis is part of the final layout");
        Equal(4, defense.Sides.Count(s => s.Exposed), "flat ground is attackable from all four faces");
        True(defense.Sides.All(s => s.Post.HasValue), "each exposed flat side has a real post: " +
            string.Join(";", defense.Sides.Select(s => s.Id + ":" + s.OmissionReason)));
        True(defense.Posts.All(p => p.CoverCells.Count == 5 && p.Route.Count == 6 &&
            result.Layout.BuiltWalls.Contains(p.WallReturn)),
            "new side posts have connected returns, front/flank screens and six-cell gate access");
        HashSet<GridPoint> slotOpaque = P4BApproachDefensePlanner.Opaque(result.Layout, terrain);
        True(defense.Posts.Any(p => P4BApproachDefensePlanner.FiringSlots(p, defense.Sides, slotOpaque).Count > 1),
            "the same long barricade offers multiple distinct firing cells");
        True(defense.Posts.All(p => P4BApproachDefensePlanner.FiringSlots(p, defense.Sides, slotOpaque)
            .All(c => p.Route.Contains(c) && p.CoverCells.Contains(new GridPoint(
                c.X + p.Outward.X, c.Z + p.Outward.Z)))),
            "every added firing cell is on the protected, reserved access lane");
        P4BPerimeterPost recoverable = defense.Posts.First();
        IReadOnlyList<GridPoint> recovered = P4BApproachDefensePlanner.RecoverLegacySlots(
            recoverable.AccessGate, recoverable.Cell, recoverable.CoverCells);
        Equal(2, recovered.Count, "an old saved terminal post recovers two screened route cells");
        Equal(0, P4BApproachDefensePlanner.RecoverLegacySlots(recoverable.AccessGate,
            new GridPoint(recoverable.Cell.X + 1, recoverable.Cell.Z), recoverable.CoverCells).Count,
            "unrecognized legacy geometry invents no firing positions");
        HashSet<GridPoint> publicCells = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input, result.Layout));
        True(defense.SallyPorts.Where(p => !p.Field).All(p => publicCells.Contains(p.Inside) && !result.Layout.BuiltWalls.Contains(p.Cell) &&
            !result.Layout.Rooms.Any(r => r.Bounds.Contains(p.Cell))),
            "new sally ports connect public circulation without cutting room walls");
        True(result.Diagnostics.Any(d => d.Stage == "turret"), "final turret decision is always retained");
        Equal(0, P4BApproachDefensePlanner.Validate(input, result.Layout).Count, "final side occupancy validates");
        ProceduralLayoutResult replay = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Equal(result.Layout.Digest, replay.Layout.Digest, "approach and turret ranking replay deterministically");
        True(result.Layout.Turret != null && result.Layout.Turret.Side.Length > 0,
            "turret is assigned an actual attack front");
        HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(result.Layout, terrain);
        P4BTurretPlan turret = result.Layout.Turret;
        int coveredSides = defense.Sides.Count(s => s.Samples.Count(c => P4BFireLane.CanEngage(
            turret.Origin, c, (int)Math.Ceiling(turret.MinimumRange),
            (int)Math.Floor(turret.MaximumRange), opaque)) >= 4);
        True(coveredSides >= 2, "ranked flat-map turret covers approaches on more than one side; actual=" + coveredSides);
        True(!defense.Posts.Any(p => p.Route.Intersect(turret.All.SelectMany(t => t.Footprint)).Any()),
            "turrets do not consume a defender's access path");
        P4BPerimeterPost post = defense.Posts.First();
        True(P4BApproachDefensePlanner.Validate(input, result.Layout.WithAddedObstacles(new[] { post.Cell }))
            .Contains("perimeter_post_access"), "a final blocked side position cannot pass validation");
        True(P4BApproachDefensePlanner.Validate(input, result.Layout.WithAddedObstacles(new[] { post.WallReturn }))
            .Contains("perimeter_post_return"), "a blocked connected return cannot pass validation");
        if (defense.SallyPorts.Count > 0)
            True(P4BApproachDefensePlanner.Validate(input,
                result.Layout.WithAddedObstacles(new[] { defense.SallyPorts[0].Inside }))
                .Contains("sally_port_public_connection"), "a disconnected new gate cannot pass validation");

        // Keep geometry fixed so terrain alone changes the exposure decision.
        ProceduralLayout rectangle = DefenseRectangle(input);
        P4BApproachDefensePlan all = P4BApproachDefensePlanner.Plan(input, rectangle);
        True(all.Sides.All(s => s.Exposed && s.Post.HasValue), "rectangle has one covered post per open front");
        TerrainSnapshot southWide = Build(160, 100, (x, z) => Ground);
        ProceduralLayoutInput southWideInput = Input(southWide, input.Requirements, 1,
            new LayoutFortificationReservation(1));
        P4BApproachDefensePlan southernApproach = P4BApproachDefensePlanner.Plan(southWideInput, rectangle);
        True(southernApproach.Sides.Single(s => s.Id == "south").OpenGroundArea >
            southernApproach.Sides.Single(s => s.Id == "north").OpenGroundArea,
            "edge-reachable exterior area detects more approach ground south of a north-placed base");
        ProceduralLayout northPrivate = rectangle.WithAddedObstacles(Enumerable.Range(51, 19)
            .Select(x => new GridPoint(x, 69)));
        P4BApproachDefensePlan corner = P4BApproachDefensePlanner.Plan(input, northPrivate);
        P4BAttackSide north = corner.Sides.Single(s => s.Id == "north");
        True(north.Post.HasValue, "a north front without a north-facing public entrance still gets a gate-side corner position");
        P4BPerimeterPost northPost = corner.Posts.Single(p => p.Cell.Equals(north.Post.Value));
        True(!northPost.Outward.Equals(north.Outward) && northPost.Route.Count == 6 &&
            northPost.CoverCells.Contains(new GridPoint(northPost.Cell.X, northPost.Cell.Z + 1)),
            "perpendicular gate access retains short transit and adjacent incoming north cover");
        TerrainSnapshot rock = Build(160, 160, (x, z) => x < 50 ? RockRoofed : Ground);
        ProceduralLayoutInput rockInput = Input(rock, input.Requirements, 1, new LayoutFortificationReservation(1));
        P4BApproachDefensePlan rockDefense = P4BApproachDefensePlanner.Plan(rockInput, rectangle);
        True(!rockDefense.Sides.Single(s => s.Id == "west").Exposed,
            "solid rock blocks the conventional western approach and sightline");
        True(rockDefense.Sides.Where(s => s.Id != "west").All(s => s.Exposed && s.Post.HasValue),
            "blocked west does not suppress the other attackable fronts");
        TerrainSnapshot lake = Build(160, 160, (x, z) => x >= 36 && x < 50 ? DeepWater : Ground);
        ProceduralLayoutInput lakeInput = Input(lake, input.Requirements, 1, new LayoutFortificationReservation(1));
        P4BApproachDefensePlan lakeDefense = P4BApproachDefensePlanner.Plan(lakeInput, rectangle);
        P4BAttackSide west = lakeDefense.Sides.Single(s => s.Id == "west");
        True(west.Exposed, "reachable firing ground across water is not mistaken for an immune side");
        True(west.Post.HasValue, "dry corner shoulder supplies the threatened shoreline post: " + west.OmissionReason);
        True(lakeDefense.Posts.All(p => p.CoverCells.Concat(p.Route.Skip(1)).All(c => !lake[c].Has(TerrainTraits.Water))),
            "defenses and access do not assume bridges or water construction");
    }

    private static void P4BAccessFrontageCases()
    {
        TerrainSnapshot terrain = Plains(160);
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(8, Industrial, Temperate,
            stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate));
        ProceduralLayoutInput input = Input(terrain, requirements, 1, new LayoutFortificationReservation(1));
        GridRect field = new GridRect(20, 20, 14, 14);
        P4BJointSite site = new P4BJointSite(field, new GridRect(40, 40, 50, 50), new GridPoint(60, 60),
            field.Points(), new GridPoint[0], new GridPoint[0], new GridPoint(33, 25), new GridPoint(20, 25),
            new GridPoint[0], 0);
        ProceduralLayout packed = DefenseRectangle(input, site).WithAddedObstacles(
            Enumerable.Range(51, 19).Select(x => new GridPoint(x, 69)));
        P4BAttackSide north = P4BApproachDefensePlanner.Plan(input, packed).Sides.Single(s => s.Id == "north");
        IReadOnlyList<P4BAccessFrontage> frontages = P4BAccessFrontagePlanner.Candidates(input, packed, north);
        True(frontages.Count > 0, "a packed northern edge can grow a public frontage on the same site");
        HashSet<GridPoint> publicCells = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(input, packed));
        foreach (P4BAccessFrontage frontage in frontages)
        {
            True(publicCells.Contains(frontage.Join) && frontage.Route.First().Equals(frontage.Join) &&
                frontage.Route.Last().Equals(frontage.Terminal), "frontage connects to the public network");
            True(frontage.Route.Zip(frontage.Route.Skip(1), (a, b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z))
                .All(d => d == 1), "public frontage route is cardinally connected");
            True(frontage.Cells.All(c => !packed.ExtraObstacles.Contains(c) && !field.Contains(c)),
                "frontage does not cut the packed private edge or field");
        }
        TerrainSnapshot wet = Build(160, 160, (x, z) => z >= 71 ? DeepWater : Ground);
        ProceduralLayoutInput wetInput = Input(wet, requirements, 1, new LayoutFortificationReservation(1));
        Equal(0, P4BAccessFrontagePlanner.Candidates(wetInput, packed, north).Count,
            "a public frontage cannot assume a bridge across the northern water");
        ProceduralLayout wide = DefenseRectangle(input, boundsOverride: new GridRect(25, 55, 95, 25));
        P4BApproachDefensePlan wideDefense = P4BApproachDefensePlanner.Plan(input, wide);
        ProceduralLayout wideDefended = wide.WithApproachDefense(input, wideDefense);
        HashSet<GridPoint> wideOpaque = P4BApproachDefensePlanner.Opaque(wideDefended, terrain);
        Console.WriteLine("  wide frontage=" + string.Join(";", wideDefense.Sides.Select(s => s.Id + ":" +
            P4BApproachDefensePlanner.CoveredSamples(s, wideOpaque) + "/" + s.Samples.Count)));
        True(wideDefense.Sides.Where(s => s.Exposed).All(s =>
            P4BApproachDefensePlanner.CoveredSamples(s, wideOpaque) >= P4BApproachDefensePlanner.UsefulArc(s.Samples.Count)),
            "a long exposed wall gets enough separated gate-side positions, not only one corner");
        GridRect sideField = new GridRect(71, 51, 19, 19);
        P4BJointSite fieldSite = new P4BJointSite(sideField, new GridRect(40, 40, 31, 40), new GridPoint(60, 60),
            sideField.Points(), new GridPoint[0], new GridPoint[0], new GridPoint(70, 60), new GridPoint(90, 60),
            new GridPoint[0], 0);
        GridPoint outerGate = new GridPoint(90, 60);
        GridPoint[] crops = new GridRect(73, 53, 15, 15).Points().ToArray();
        GridPoint[] fieldWalls = Enumerable.Range(71, 20).SelectMany(x => new[] { new GridPoint(x, 50), new GridPoint(x, 70) })
            .Concat(Enumerable.Range(51, 19).Select(z => new GridPoint(90, z))).Where(c => !c.Equals(outerGate)).ToArray();
        P4BFieldWallPlan fieldPlan = new P4BFieldWallPlan(0, crops, fieldWalls, outerGate, new GridPoint[0],
            new GridPoint(70, 60), sideField.Points(), Enumerable.Range(71, 23).Select(x => new GridPoint(x, 60)),
            coverDefName: "Barricade");
        ProceduralLayout fieldLayout = DefenseRectangle(input, fieldSite).WithFieldWall(input, fieldPlan)
            .WithAddedObstacles(new[] { new GridPoint(91, 60) });
        P4BApproachDefensePlan fieldDefense = P4BApproachDefensePlanner.Plan(input, fieldLayout);
        True(fieldDefense.SallyPorts.Any(p => p.Field), "a field flank gets a sally port when the far arrival gate is blocked");
        ProceduralLayout fieldDefended = fieldLayout.WithApproachDefense(input, fieldDefense);
        Equal(0, P4BApproachDefensePlanner.Validate(input, fieldDefended).Count,
            "field sally gates have legal interior and exterior routes");
        Equal(string.Join(";", crops.OrderBy(c => c)), string.Join(";", fieldDefended.FieldWall.Fields),
            "field sally access does not remove crops");
        True(fieldDefense.SallyPorts.Where(p => p.Field).All(p => !fieldDefended.FieldWall.Walls.Contains(p.Cell) &&
            p.InteriorRoute.First().Equals(fieldPlan.InnerGate.Value) && p.InteriorRoute.Last().Equals(p.Inside)),
            "field sally port replaces a wall and routes from the existing compound entrance");
        P4BSallyPort fieldPort = fieldDefense.SallyPorts.First(p => p.Field);
        Contains(P4BApproachDefensePlanner.Validate(input, fieldDefended.WithAddedObstacles(new[] { fieldPort.Inside })),
            "sally_port_public_connection");
        int repaired = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
        {
            ProceduralLayoutInput candidate = Input(terrain, requirements, seed, new LayoutFortificationReservation(1));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(candidate, requireJointSite: true);
            Accepted(result);
            HashSet<GridPoint> publicPlan = new HashSet<GridPoint>(ProceduralLayoutValidator.PublicCirculation(candidate, result.Layout));
            LayoutCorridor[] extensions = result.Layout.Corridors.Where(c => c.Id.StartsWith("defense-access-")).ToArray();
            repaired += extensions.Length + result.Layout.ApproachDefense.SallyPorts.Count(p => p.Field);
            True(extensions.SelectMany(c => c.Cells).All(publicPlan.Contains), "retained defense access is public and connected");
            True(extensions.SelectMany(c => c.Cells).All(c => !result.Layout.Rooms.Any(r => r.Bounds.Contains(c)) &&
                !result.Layout.JointSite.Field.Contains(c)), "access extensions preserve room and field reservations");
            HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(result.Layout, terrain);
            opaque.UnionWith(result.Layout.Turret?.All.SelectMany(t => t.Footprint) ?? new GridPoint[0]);
            Console.WriteLine("  frontage seed=" + seed + " extensions=" + extensions.Length +
                " fieldPorts=" + result.Layout.ApproachDefense.SallyPorts.Count(p => p.Field) + " sides=" +
                string.Join(";", result.Layout.ApproachDefense.Sides.Select(s => s.Id + ":" +
                    P4BApproachDefensePlanner.CoveredSamples(s, opaque) +
                    "/" + s.Samples.Count)) + " decisions=" + string.Join(";", result.Diagnostics.Where(d => d.Stage == "defense_access")));
            True(result.Layout.ApproachDefense.Sides.Where(s => s.Exposed).All(s =>
                P4BApproachDefensePlanner.CoveredSamples(s, opaque) >= P4BApproachDefensePlanner.UsefulArc(s.Samples.Count)),
                "every exposed flat side has a useful combined firing arc after final turret occupancy");
        }
        True(repaired > 0, "flat packing corpus exercises actual frontage or field-side access repair");
    }

    private static ProceduralLayout DefenseRectangle(ProceduralLayoutInput input, P4BJointSite jointSite = null,
        GridRect? boundsOverride = null)
    {
        GridRect bounds = boundsOverride ?? new GridRect(50, 50, 21, 21);
        int middleZ = (bounds.MinZ + bounds.MaxZ) / 2;
        GridPoint gateCell = new GridPoint(bounds.MaxX, middleZ);
        List<GridPoint> walls = bounds.Points().Where(c => c.X == bounds.MinX || c.Z == bounds.MinZ || c.X == bounds.MaxX || c.Z == bounds.MaxZ)
            .Where(c => !c.Equals(gateCell)).ToList();
        LayoutGate gate = new LayoutGate(gateCell, new GridPoint(bounds.MaxX - 1, middleZ), new GridPoint(1, 0),
            new GridPoint[0], new GridPoint[0], new GridPoint[0], new GridPoint(bounds.MaxX + 1, middleZ),
            Enumerable.Range(1, 3).Select(d => new GridPoint(bounds.MaxX + d, middleZ)), new GridPoint[0], false);
        return new ProceduralLayout(input.Digest, input.Seed, LayoutDoctrine.OpenGround, false,
            new GridPoint(60, 60), new LayoutRoom[0], new LayoutCorridor[0], null,
            new GridRect(bounds.MinX + 1, bounds.MinZ + 1, bounds.Width - 2, bounds.Height - 2).Points(), walls, new GridPoint[0], new[] { gate },
            new GridPoint[0], new GridPoint[0], new GridPoint[0], 1, 0, new LayoutRelationship[0],
            new GridPoint[0], new string[0], input.Equipment.Kit.Stage,
            input.Equipment.Kit.Roles.Digest, input.Equipment.Digest, "BlocksGranite", null, null, jointSite: jointSite);
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

    // ---- tech-stage equipment in the planner (synthetic; Core fixture catalog) ----

    private static ProceduralLayoutResult PlanWith(TerrainSnapshot terrain, LayoutRequirements requirements, ulong seed, LayoutLocalResources resources,
        TechCatalog catalog = null) => ProceduralLayoutPlanner.Plan(new ProceduralLayoutInput(requirements, terrain, seed, catalog ?? CoreCatalog, null, null, resources));

    private static void CookingCaps()
    {
        RoleResolutionSet tribal = RoleResolver.Resolve(CoreCatalog, TechStageModel.Create(SettlementTechStageLevel.Neolithic));
        Equal("Campfire", tribal.ChosenDefName(SettlementRole.Cooking), "tribal cooks on a campfire");
        True(tribal.Get(SettlementRole.Cooking).Rejected.Any(r => r.Key == "FueledStove" && r.Value == "below_min_stage:Medieval"), "fueled stove below its min stage");
        Equal("FueledStove", RoleResolver.Resolve(CoreCatalog, TechStageModel.Create(SettlementTechStageLevel.Medieval)).ChosenDefName(SettlementRole.Cooking), "medieval fueled stove");
        True(RoleResolver.Resolve(CoreCatalog, TechStageModel.Create(SettlementTechStageLevel.Medieval)).Get(SettlementRole.Cooking).Rejected
            .Any(r => r.Key == "Campfire" && r.Value == "above_max_stage:Neolithic"), "campfire capped at Neolithic");
        RoleDefinition cooking;
        RoleTable.Standard.TryGet(SettlementRole.Cooking, out cooking);
        List<RoleDefinition> withoutMin = RoleTable.Standard.Definitions.Where(d => d.Role != SettlementRole.Cooking).ToList();
        withoutMin.Add(new RoleDefinition(SettlementRole.Cooking, true, cooking.Options.Select(o => RoleOption.Def(o.DefName, o.MaxStage))));
        NotEqual(RoleTable.Standard.Digest, new RoleTable(withoutMin).Digest, "MinStage participates in the table digest");

        ProceduralLayoutResult layout = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Temperate), 3);
        Accepted(layout);
        List<LayoutFixture> stoves = layout.Layout.AllFixtures.Where(f => f.Role == SettlementRole.Cooking).ToList();
        True(stoves.Count > 0 && stoves.All(f => f.DefName == "Campfire" && f.Kind == LayoutFixtureKind.Stove && f.Cells.Count == 1 && f.Access.Count == 1),
            "tribal kitchen has 1x1 campfires with a work cell");
    }

    private static void TribalLayoutKit()
    {
        foreach (LayoutClimate climate in new[] { Temperate, Cold, Hot })
        {
            ProceduralLayoutResult result = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, climate), 5);
            Accepted(result);
            SettlementTechStage stage = result.Layout.TechStage;
            Equal(SettlementTechStageLevel.Neolithic, stage.Level, "tribal stage");
            string[] forbidden = { "ComplexFurniture", "Smithing", "ComplexClothing", "Stonecutting", "Electricity" };
            foreach (LayoutFixture fixture in result.Layout.AllFixtures)
            {
                CatalogDef def;
                if (!CoreCatalog.TryGet(fixture.DefName, out def)) continue;
                True(!def.ResearchPrerequisites.Any(forbidden.Contains), climate + " tribal fixture " + def.DefName + " needs no complex research");
                True(CoreCatalog.IsLegal(def, stage), climate + " " + def.DefName + " legal");
            }
            List<string> defs = result.Layout.AllFixtures.Select(f => f.DefName).Distinct().ToList();
            True(!defs.Contains("Bed") && !defs.Contains("Shelf") && !defs.Contains("DiningChair") && !defs.Contains("FueledSmithy") &&
                !defs.Contains("HandTailoringBench") && !defs.Contains("FueledStove"), climate + " no bed, shelf, chair, smithy, tailoring bench or stove");
            LayoutRoom workshop = result.Layout.Rooms.Single(r => r.Kind == LayoutRoomKind.Workshop);
            List<LayoutFixture> benches = workshop.Fixtures.Where(f => f.Kind == LayoutFixtureKind.Workbench).ToList();
            True(benches.Count == 1 && benches[0].DefName == "CraftingSpot" && !benches[0].Blocking && benches[0].Access.Count == 1,
                climate + " one deduplicated, non-blocking crafting spot with a work cell");
            True(result.Layout.AllFixtures.Where(f => f.Role == SettlementRole.Sleep).All(f => f.DefName == "Bedroll" && !f.Blocking && f.Cells.Count == 2),
                climate + " bedrolls are 1x2 and non-blocking");
            True(result.Layout.AllFixtures.Where(f => f.Kind == LayoutFixtureKind.Chair).All(f => f.DefName == "Stool"), climate + " stools");
            Equal(LayoutEquipment.Wood, result.Layout.WallStuff, climate + " wooden walls");
            True(!result.Layout.AllFixtures.Any(f => f.Watts > 0 || f.Kind == LayoutFixtureKind.Generator), climate + " unpowered");
        }
    }

    private static void StagedLayoutKits()
    {
        SettlementTechStage medieval = TechStageModel.Create(SettlementTechStageLevel.Medieval);
        ProceduralLayoutResult result = Plan(Plains(130), LayoutRequirements.FromPopulation(6, Tribal, Cold, stage: medieval), 7);
        Accepted(result);
        List<string> workshop = result.Layout.Rooms.Single(r => r.Kind == LayoutRoomKind.Workshop).Fixtures
            .Where(f => f.Kind == LayoutFixtureKind.Workbench).Select(f => f.DefName).ToList();
        True(workshop.Take(3).SequenceEqual(new[] { "FueledSmithy", "HandTailoringBench", "TableStonecutter" }), "medieval workshop: " + string.Join(",", workshop));
        True(result.Layout.AllFixtures.Where(f => f.Role == SettlementRole.Cooking).All(f => f.DefName == "FueledStove"), "medieval fueled stove");
        True(result.Layout.AllFixtures.Any(f => f.Kind == LayoutFixtureKind.Shelf && f.DefName == "Shelf"), "medieval shelves");
        True(result.Layout.AllFixtures.Where(f => f.Role == SettlementRole.Sleep).All(f => f.DefName == "Bed" && f.Blocking), "medieval beds block");
        True(result.Layout.AllFixtures.Where(f => f.Kind == LayoutFixtureKind.Heater || f.Kind == LayoutFixtureKind.Campfire).All(f => f.DefName == "Campfire"),
            "medieval heats with campfires");

        ProceduralLayoutResult industrial = Plan(Plains(130), LayoutRequirements.FromPopulation(8, Industrial, Temperate), 7);
        Accepted(industrial);
        List<string> benches = industrial.Layout.Rooms.Single(r => r.Kind == LayoutRoomKind.Workshop).Fixtures
            .Where(f => f.Kind == LayoutFixtureKind.Workbench).Select(f => f.DefName).ToList();
        True(benches.Take(4).SequenceEqual(new[] { "ElectricSmithy", "ElectricTailoringBench", "TableMachining", "TableStonecutter" }),
            "industrial workshop covers every craft role: " + string.Join(",", benches));
        foreach (LayoutFixture bench in industrial.Layout.AllFixtures.Where(f => f.Kind == LayoutFixtureKind.Workbench || f.Kind == LayoutFixtureKind.Stove))
        {
            CatalogDef def;
            True(CoreCatalog.TryGet(bench.DefName, out def) && bench.Cells.Count == def.SizeX * def.SizeZ, bench.DefName + " catalog footprint");
            True(bench.Watts == Math.Max(0, def.BasePowerConsumption), bench.DefName + " catalog watts");
        }
        LayoutFixture generator = industrial.Layout.AllFixtures.First(f => f.Kind == LayoutFixtureKind.Generator);
        Equal(4, generator.Cells.Count, "generator 2x2 from catalog");
        Equal(LayoutEquipment.Wood, industrial.Layout.WallStuff, "no local stone: wooden walls");

        LayoutLocalResources stony = new LayoutLocalResources(100000, new[] { "BlocksGranite" });
        ProceduralLayoutResult stoneIndustrial = PlanWith(Plains(130), LayoutRequirements.FromPopulation(8, Industrial, Temperate), 7, stony);
        Accepted(stoneIndustrial);
        Equal("BlocksGranite", stoneIndustrial.Layout.WallStuff, "industrial builds with local stone blocks");
        True(stoneIndustrial.Layout.AllFixtures.Where(f => f.DefName == "Table2x2c").All(f => f.Stuff == "BlocksGranite"), "stony furniture uses stone");
        True(stoneIndustrial.Layout.Materials.Stuff["BlocksGranite"] > 0, "stone charged in the ledger");
    }

    private static void WoodPoorStonyMap()
    {
        LayoutLocalResources resources = new LayoutLocalResources(120, new[] { "BlocksSlate", "BlocksGranite" });
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(6, Tribal, Temperate);
        LayoutEquipment equipment = LayoutEquipment.Resolve(requirements, CoreCatalog, resources, 2, 9);
        True(equipment.StonecuttingAdapted, "stonecutting adaptation granted");
        TechAdaptation adaptation = equipment.Kit.Stage.Adaptations.Single();
        True(adaptation.ResearchDefName == "Stonecutting" && adaptation.Reason.StartsWith("wood 120/"), "reason records yield/need: " + adaptation.Reason);
        ProceduralLayoutResult result = PlanWith(Plains(120), requirements, 9, resources);
        Accepted(result);
        True(result.Layout.WallStuff == "BlocksSlate" || result.Layout.WallStuff == "BlocksGranite", "stone walls: " + result.Layout.WallStuff);
        True(result.Layout.AllFixtures.Any(f => f.DefName == "TableStonecutter"), "workshop gains a stonecutter");
        True(result.Layout.AllFixtures.Any(f => f.DefName == "CraftingSpot"), "crafting spot stays");
        LayoutMaterialLedger materials = result.Layout.Materials;
        True(materials.FuelWood > 0, "fuel wood counted");
        Equal(materials.FuelWood + Math.Max(0, materials.ConstructionWood - 120), materials.ImportedWood, "all fuel wood imported under the adaptation");
        Equal((int)Math.Ceiling(materials.ImportedWood * 1.2), materials.ImportValue, "import valued at WoodLog market value");
        True(materials.ImportValue <= requirements.ImportValueLimit, "within the import limit");
        True(result.Diagnostics.Any(d => d.Stage == "equipment" && d.Detail.StartsWith("adaptation:Stonecutting:wood 120/")), "adaptation logged");
    }

    private static void WoodAndStonePoorMap()
    {
        LayoutLocalResources barren = new LayoutLocalResources(0);
        ProceduralLayoutResult result = PlanWith(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Temperate), 9, barren);
        True(!result.Success, "rejected");
        Equal(LayoutEquipment.MaterialBudgetExceeded, result.RejectionCode, "material budget");
        Equal(0, result.Stats.EvaluationCount, "rejected before search");
        LayoutEquipment equipment = LayoutEquipment.Resolve(LayoutRequirements.FromPopulation(6, Tribal, Temperate), CoreCatalog, barren, 2, 9);
        True(!equipment.StonecuttingAdapted && equipment.Estimate.ImportedWood == equipment.Estimate.ConstructionWood + equipment.Estimate.FuelWood,
            "without stone all wood is imported");
        ProceduralLayoutResult generous = PlanWith(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Temperate, importValueLimit: 100000), 9, barren);
        Accepted(generous);
        True(generous.Layout.Materials.ImportedWood > 0 && generous.Layout.WallStuff == LayoutEquipment.Wood, "a large enough budget imports all wood");
    }

    private static void DefaultResourcesNoAdaptation()
    {
        LayoutEquipment equipment = LayoutEquipment.Resolve(LayoutRequirements.FromPopulation(6, Tribal, Temperate), CoreCatalog, null, 2, 9);
        True(!equipment.StonecuttingAdapted && equipment.Kit.Stage.Adaptations.Count == 0, "default resources: no adaptation");
        Equal(0, equipment.Estimate.ImportedWood, "default resources: nothing imported");
        LayoutEquipment rich = LayoutEquipment.Resolve(LayoutRequirements.FromPopulation(6, Tribal, Temperate), CoreCatalog,
            new LayoutLocalResources(100000, new[] { "BlocksGranite" }), 2, 9);
        True(!rich.StonecuttingAdapted && rich.ConstructionStuff == LayoutEquipment.Wood, "wood-rich tribe keeps wooden construction despite stone");
    }

    private static void ClimateControlUnresolved()
    {
        TechCatalog noHeat = new TechCatalog("core-without-heat", CoreCatalog.Defs.Where(d => d.DefName != "Campfire" && d.DefName != "Heater"), CoreCatalog.Research);
        SettlementTechStage medieval = TechStageModel.Create(SettlementTechStageLevel.Medieval);
        ProceduralLayoutResult cold = PlanWith(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Cold, stage: medieval), 3, null, noHeat);
        Equal("climate_control_unresolved:Heat", cold.RejectionCode, "cold without heat source");
        Equal(0, cold.Stats.EvaluationCount, "rejected before search");
        ProceduralLayoutResult temperate = PlanWith(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Temperate, stage: medieval), 3, null, noHeat);
        Accepted(temperate);
        True(temperate.Diagnostics.Any(d => d.Stage == "equipment" && d.Detail == "role_unresolved:Heat"), "heat only a diagnostic when the climate does not need it");
        ProceduralLayoutResult tribal = PlanWith(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Temperate), 3, null, noHeat);
        Equal("essential_role_unresolved", tribal.RejectionCode, "campfire was also the only tribal cooking option");
    }

    private static void EquipmentDigests()
    {
        TerrainSnapshot terrain = Plains(120);
        LayoutRequirements neolithic = LayoutRequirements.FromPopulation(6, Tribal, Temperate);
        LayoutRequirements medieval = LayoutRequirements.FromPopulation(6, Tribal, Temperate, stage: TechStageModel.Create(SettlementTechStageLevel.Medieval));
        NotEqual(neolithic.Digest, medieval.Digest, "stage in requirements digest");
        NotEqual(Input(terrain, neolithic, 1).Digest, new ProceduralLayoutInput(neolithic, terrain, 1, CoreCatalog, null, null, new LayoutLocalResources(50)).Digest,
            "resources in input digest");
        TechCatalog smaller = new TechCatalog("smaller", CoreCatalog.Defs.Where(d => d.DefName != "Gun_Revolver"), CoreCatalog.Research);
        NotEqual(Input(terrain, neolithic, 1).Digest, new ProceduralLayoutInput(neolithic, terrain, 1, smaller).Digest, "catalog in input digest");
        Equal(LayoutRequirements.DefaultImportValueLimitNonElectric, neolithic.ImportValueLimit, "provisional tribal import limit");
        Equal(P2ProfileCatalog.IndustrialTemperate.Budget.ImportValueLimit, LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate).ImportValueLimit,
            "profile import limit");
        ProceduralLayoutResult result = Plan(terrain, neolithic, 1);
        Accepted(result);
        Equal(result.Layout.RolesDigest, Input(terrain, neolithic, 1).Equipment.Kit.Roles.Digest, "role resolution recorded on the layout");
        True(result.Layout.AllFixtures.Where(f => f.Kind != LayoutFixtureKind.MortarCourt).All(f => f.Role.HasValue || f.Kind == LayoutFixtureKind.Generator ||
            f.Kind == LayoutFixtureKind.Battery), "every furnishing fixture records its role");
    }

    // ---- power progression (synthetic) ----

    private static readonly SettlementTechStage MidStage = TechStageModel.Create(SettlementTechStageLevel.IndustrialMid);
    private static readonly List<GridPoint> CentreGeysers = new List<GridPoint> { new GridPoint(70, 65), new GridPoint(58, 65), new GridPoint(65, 58), new GridPoint(65, 72) };

    private static ProceduralLayoutResult Mid(ulong seed, LayoutClimate climate = null, LayoutLocalResources resources = null, int importValueLimit = -1) =>
        PlanWith(Plains(130), LayoutRequirements.FromPopulation(8, Industrial, climate ?? Temperate, stage: MidStage, importValueLimit: importValueLimit), seed, resources);

    private static ProceduralLayoutResult Early(ulong seed, int residents = 8) =>
        PlanWith(Plains(130), LayoutRequirements.FromPopulation(residents, Industrial, Temperate), seed, null);

    private static void PowerTribalNone()
    {
        ProceduralLayoutResult result = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Cold), 3);
        Accepted(result);
        True(result.Layout.Power == null && result.Layout.PowerYards.Count == 0, "no power plan");
        True(!result.Layout.AllFixtures.Any(f => f.Kind == LayoutFixtureKind.Generator || f.Kind == LayoutFixtureKind.Battery), "no generator or battery");
        True(!result.Layout.Rooms.Any(r => r.Kind == LayoutRoomKind.Power), "no power room");
        Equal(0, result.Layout.Materials.GeneratorFuel, "no generator fuel");
    }

    private static void PowerEarly()
    {
        int wind = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4 })
        {
            ProceduralLayoutResult result = Early(seed);
            Accepted(result);
            LayoutPowerPlan power = result.Layout.Power;
            Equal(LayoutPowerModel.WoodGenerator, power.GeneratorDef, "early dispatchable is wood-fired");
            True(result.Layout.AllFixtures.Where(f => f.Kind == LayoutFixtureKind.Generator).All(f => f.DefName == LayoutPowerModel.WoodGenerator), "no chemfuel generator");
            True(power.Yards.All(y => y.DefName == LayoutPowerModel.Wind), "only wind is legal at early: " + string.Join(",", power.Yards.Select(y => y.DefName)));
            Equal(LayoutEquipment.Wood, result.Layout.Materials.GeneratorFuelDef, "wood fuel");
            if (power.Yards.Count > 0) wind++;
            else Contains(power.Diagnostics, "power_source_unplaceable:WindTurbine:no_clear_zone");
        }
        True(wind > 0, "a wind turbine fits for at least one seed");
    }

    private static void PowerMidOpenSky()
    {
        ProceduralLayoutResult result = null;
        foreach (ulong seed in Enumerable.Range(1, 8).Select(n => (ulong)n))
        {
            result = Mid(seed);
            Accepted(result);
            if (result.Layout.Power.Yards.Any(y => y.DefName == LayoutPowerModel.Solar || y.DefName == LayoutPowerModel.Wind)) break;
        }
        Accepted(result);
        LayoutPowerPlan power = result.Layout.Power;
        True(power.Yards.Any(y => y.DefName == LayoutPowerModel.Solar || y.DefName == LayoutPowerModel.Wind), "solar or wind on open sky");
        True(power.Yards.All(y => y.Cells.All(c => !result.Layout.ConstructedRoof.Contains(c))), "yards stay open-sky");
        LayoutEquipment equipment = new ProceduralLayoutInput(LayoutRequirements.FromPopulation(8, Industrial, Temperate, stage: MidStage), Plains(130), 1, CoreCatalog).Equipment;
        True(power.Generators < equipment.Demand.PowerGenerators, "renewables reduce dispatchable units below the worst case " +
            power.Generators + "<" + equipment.Demand.PowerGenerators);
        True(power.Yards.All(y => y.Cells.All(c => result.Layout.Enclosure.Contains(c))), "yards inside the enclosure");
    }

    private static void PowerRockProtected()
    {
        LayoutRequirements requirements = new LayoutRequirements(8, Industrial, HousingStandard.Barracks, Temperate, 0, 700, 186, null, MidStage);
        ProceduralLayoutResult rock = PlanWith(Mountain(130, 110), requirements, 4, null);
        Accepted(rock);
        Equal(LayoutDoctrine.RockProtected, rock.Layout.Doctrine, "protect doctrine");
        True(rock.Layout.Power.Yards.Count == 0, "fuel only");
        Contains(rock.Layout.Power.Diagnostics, "renewables_skipped_rock_protected");
        ProceduralLayoutResult open = Mid(4);
        Accepted(open);
        True(rock.Layout.Power.FuelStock > open.Layout.Power.FuelStock, "larger fuel stock: " + rock.Layout.Power.FuelStock + ">" + open.Layout.Power.FuelStock);
        ExportLayout("mountain-industrial-mid-rock-protected-power", rock, true);
    }

    private static void PowerFuelStock()
    {
        foreach (ProceduralLayoutResult result in new[] { Early(1), Mid(1), Mid(2, Cold) })
        {
            Accepted(result);
            LayoutPowerPlan power = result.Layout.Power;
            CatalogDef generator;
            CoreCatalog.TryGet(power.GeneratorDef, out generator);
            Equal((int)Math.Ceiling(power.Generators * generator.FuelPerDay * LayoutEquipment.FuelHorizonDays), power.FuelStock, "stock = units x rate x horizon");
            Equal(power.FuelStock, result.Layout.Materials.GeneratorFuel, "ledger carries the stock");
        }
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(8, Industrial, Temperate);
        LayoutEquipment equipment = LayoutEquipment.Resolve(requirements, CoreCatalog, null, 2, 1);
        int worstStock = LayoutPowerModel.FuelStock(equipment.Kit.Generator, equipment.Demand.PowerGenerators);
        int storage = equipment.Demand.Rooms.Where(r => r.Kind == LayoutRoomKind.Storage).Sum(r => r.Capacity);
        Equal(Math.Max(12, 6 * 8) + (worstStock + 74) / 75, storage, "storage raised by the worst-case fuel stacks");
    }

    private static void PowerGeothermal()
    {
        LayoutLocalResources resources = new LayoutLocalResources(int.MaxValue, null, null, CentreGeysers);
        ProceduralLayoutResult result = Mid(1, null, resources);
        Accepted(result);
        LayoutPowerYard yard = result.Layout.PowerYards.SingleOrDefault(y => y.DefName == LayoutPowerModel.Geothermal);
        True(yard != null, "geothermal placed: " + string.Join(",", result.Layout.Power.Diagnostics));
        GridPoint geyser = CentreGeysers.Single(g => yard.Cells.Contains(g));
        Equal(new GridPoint(geyser.X - 2, geyser.Z - 2), yard.Cells.Min(), "vanilla footprint: Position == geyser");
        True(new[] { geyser, new GridPoint(geyser.X + 1, geyser.Z), new GridPoint(geyser.X, geyser.Z + 1), new GridPoint(geyser.X + 1, geyser.Z + 1) }
            .All(yard.Cells.Contains), "the geyser's 2x2 lies in the 6x6 footprint");
        Equal(36, yard.Cells.Count, "6x6 footprint");
        TerrainSnapshot occupiedGeysers = Build(130, 130, (x, z) => CentreGeysers.Any(g => (x == g.X || x == g.X + 1) && (z == g.Z || z == g.Z + 1)) ? TerrainTraits.Occupied : Ground);
        ProceduralLayoutResult occupied = PlanWith(occupiedGeysers, LayoutRequirements.FromPopulation(8, Industrial, Temperate, stage: MidStage), 1, resources);
        Accepted(occupied);
        True(occupied.Layout.PowerYards.Any(y => y.DefName == LayoutPowerModel.Geothermal), "geysers reported as Occupied terrain still host geothermal");
        ExportLayout("plains-industrial-mid-geothermal", result);
    }

    private static void PowerWindZone()
    {
        // Yards are optional and room demand changes their seed-specific fit.
        // Require a real turbine in a bounded corpus, then test every wind cell.
        ProceduralLayoutResult result = Enumerable.Range(1, 6).Select(i => Early((ulong)i))
            .FirstOrDefault(r => r.Success && r.Layout.PowerYards.Any(y => y.DefName == LayoutPowerModel.Wind));
        True(result != null, "at least one legal turbine in six open-ground layouts");
        Accepted(result);
        LayoutPowerYard turbine = result.Layout.PowerYards.First(y => y.DefName == LayoutPowerModel.Wind);
        int minX = turbine.Cells.Min(c => c.X), minZ = turbine.Cells.Min(c => c.Z);
        True(turbine.Cells.All(c => c.X >= minX && c.X <= minX + 6 && c.Z >= minZ && c.Z <= minZ + 1) && turbine.Cells.Count == 14, "7x2 north footprint");
        True(turbine.ZoneCells.Count == 7 * 16 && turbine.ZoneCells.All(c => c.X >= minX && c.X <= minX + 6 &&
            ((c.Z >= minZ + 2 && c.Z <= minZ + 11) || (c.Z >= minZ - 6 && c.Z <= minZ - 1))), "vanilla wind cells: 10 rows ahead, 6 behind");
        Equal("North", turbine.Rotation, "north only");
        HashSet<GridPoint> blocked = new HashSet<GridPoint>(result.Layout.Rooms.SelectMany(r => r.Bounds.Points()).Concat(result.Layout.BuiltWalls)
            .Concat(result.Layout.PowerYards.Where(y => y != turbine).SelectMany(y => y.Cells)).Concat(result.Layout.ConstructedRoof));
        True(turbine.ZoneCells.All(c => result.Layout.Enclosure.Contains(c) && !blocked.Contains(c)), "clear zone unroofed, building-free, inside the enclosure");
        ExportLayout("plains-industrial-early-wind", result);
    }

    private static void PowerCriticalCovered()
    {
        LayoutLocalResources everything = new LayoutLocalResources(int.MaxValue, null, new[] { new GridPoint(5, 5) }, CentreGeysers);
        foreach (ProceduralLayoutResult result in new[] { Mid(1, Cold, everything), Mid(2, Hot, everything), Early(3), Mid(3, Cold) })
        {
            Accepted(result);
            int critical, nonCritical;
            LayoutPowerModel.Loads(result.Layout.Rooms, out critical, out nonCritical);
            LayoutPowerPlan power = result.Layout.Power;
            True(critical > 0, "scenario has a critical load");
            True(power.Generators * power.GeneratorWatts >= critical, "dispatchable covers critical " + power.Generators * power.GeneratorWatts + ">=" + critical +
                " despite renewables " + power.RenewableWatts);
            True(power.Generators * power.GeneratorWatts + power.RenewableWatts >= critical + nonCritical, "all load covered");
        }
    }

    private static void PowerBatteries()
    {
        Equal(1, LayoutPowerModel.Batteries(0), "at least one");
        Equal(1, LayoutPowerModel.Batteries(2400), "2400W x 6h = 600Wd");
        Equal(2, LayoutPowerModel.Batteries(2401), "just over one battery");
        Equal(3, LayoutPowerModel.Batteries(4801), "three");
        foreach (ProceduralLayoutResult result in new[] { Early(1), Mid(3, Cold), PlanWith(Plains(150), LayoutRequirements.FromPopulation(24, Industrial, Cold), 2, null) })
        {
            Accepted(result);
            int critical, nonCritical;
            LayoutPowerModel.Loads(result.Layout.Rooms, out critical, out nonCritical);
            int expected = Math.Max(1, (int)Math.Ceiling(critical * 6.0 / 24 / 600));
            Equal(expected, result.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Battery), "battery count for " + critical + "W critical");
            True(result.Layout.AllFixtures.Where(f => f.Kind == LayoutFixtureKind.Battery).All(f => f.RoomId == "power"), "batteries indoors");
        }
    }

    private static void PowerChemfuel()
    {
        ProceduralLayoutResult result = Mid(1, null, new LayoutLocalResources(0), 100000);
        Accepted(result);
        LayoutPowerPlan power = result.Layout.Power;
        Equal(LayoutPowerModel.ChemfuelGenerator, power.GeneratorDef, "chemfuel when wood cannot cover the stock");
        Equal("Chemfuel", power.FuelDef, "chemfuel stock");
        LayoutMaterialLedger materials = result.Layout.Materials;
        Equal(power.FuelStock, materials.GeneratorFuelImported, "all chemfuel imported");
        Equal((int)Math.Ceiling(materials.ImportedWood * 1.2 + power.FuelStock * 2.3) +
            P4EArtillery.ImportValue(CoreCatalog, result.Layout.Courts.Count), materials.ImportValue,
            "chemfuel and the separate finite artillery ledger use catalog market values");
        ProceduralLayoutResult woodRich = Mid(1);
        Equal(LayoutPowerModel.WoodGenerator, woodRich.Layout.Power.GeneratorDef, "wood when the yield covers the stock");
        ProceduralLayoutResult early = PlanWith(Plains(130), LayoutRequirements.FromPopulation(8, Industrial, Temperate, importValueLimit: 100000), 1, new LayoutLocalResources(0));
        Accepted(early);
        Equal(LayoutPowerModel.WoodGenerator, early.Layout.Power.GeneratorDef, "early has no chemfuel: imported wood instead");
        True(early.Layout.Materials.ImportedWood >= early.Layout.Power.FuelStock, "generator wood imported");
    }

    private static void PowerDiagnostics()
    {
        ProceduralLayoutResult dry = Mid(1);
        Contains(dry.Layout.Power.Diagnostics, "power_source_unplaceable:WatermillGenerator:no_river");
        Contains(dry.Layout.Power.Diagnostics, "power_source_unplaceable:GeothermalGenerator:no_geyser");
        ProceduralLayoutResult river = Mid(1, null, new LayoutLocalResources(int.MaxValue, null, new[] { new GridPoint(3, 60), new GridPoint(3, 61) }));
        Contains(river.Layout.Power.Diagnostics, "power_source_unplaceable:WatermillGenerator:river_outside_enclosure");
        // Racked storage needs fewer lamps; 200 residents still exceed the largest legal power room.
        ProceduralLayoutResult oversized = Plan(Plains(120), LayoutRequirements.FromPopulation(200, Industrial, Temperate), 1);
        Equal("room_demand_exceeds_legal_dimensions", oversized.RejectionCode, "a load beyond eight generators rejects before search");
        True(oversized.Diagnostics.Any(d => d.Detail.StartsWith("room_demand_exceeds_legal_dimensions:Power")), "power room named");
    }

    private static void InteractionOffsetUnsupported()
    {
        TechCatalog skewed = new TechCatalog("skewed", CoreCatalog.Defs.Where(d => d.DefName != "CraftingSpot").Concat(new[]
        {
            new CatalogDef("CraftingSpot", CatalogCategory.Building, hasInteractionCell: true, interactionOffset: new GridPoint(1, -1),
                thingClass: "Building_WorkTable", designationCategory: "Production")
        }), CoreCatalog.Research);
        ProceduralLayoutResult result = PlanWith(Plains(120), LayoutRequirements.FromPopulation(6, Tribal, Temperate), 1, null, skewed);
        Equal("interaction_offset_unsupported:CraftingSpot", result.RejectionCode, "diagnostic instead of a silent furnishing failure");
        Equal(0, result.Stats.EvaluationCount, "before search");
    }

    // ---- loadout (synthetic; vanilla CalculatedBaseMarketValue port) ----

    private static SettlementLoadout Loadout(SettlementTechStageLevel level, LayoutClimate climate = null, int residents = 8, ulong seed = 5,
        int equipmentValueLimit = -1, bool mortarCrew = true)
    {
        LayoutRequirements requirements = Kit(level, climate, residents, equipmentValueLimit);
        LayoutEquipment equipment = LayoutEquipment.Resolve(requirements, CoreCatalog, null, 2, 1);
        return LayoutLoadoutModel.Build(requirements, equipment.Kit, CoreCatalog, seed, 2, mortarCrew);
    }

    private static LayoutRequirements Kit(SettlementTechStageLevel level, LayoutClimate climate, int residents = 8, int equipmentValueLimit = -1) =>
        LayoutRequirements.FromPopulation(residents, level >= SettlementTechStageLevel.IndustrialEarly ? Industrial : Tribal, climate ?? Temperate,
            stage: TechStageModel.Create(level), equipmentValueLimit: equipmentValueLimit);

    private static void LoadoutValid(SettlementTechStageLevel level, LayoutClimate climate, SettlementLoadout loadout)
    {
        ProceduralLayoutInput input = new ProceduralLayoutInput(Kit(level, climate, loadout.Residents.Count, loadout.EquipmentValueLimit), Plains(120), 1, CoreCatalog);
        IReadOnlyList<string> errors = ProceduralLayoutValidator.ValidateLoadout(input, null, loadout);
        Equal(0, errors.Count, level + " " + climate + " loadout valid: " + string.Join(",", errors.Take(6)));
    }

    private static void LoadoutRoles()
    {
        Func<SettlementTechStageLevel, int, bool, string> roles = (level, n, crew) => string.Join(",", LayoutLoadoutModel.Roles(n, level, crew));
        Equal("RangedLine,RangedLine,RangedLine,Marksman,CloseDefense,CloseDefense,MeleeReserve,Support",
            roles(SettlementTechStageLevel.IndustrialEarly, 8, true), "8 residents before Mid: heavy folded into the line");
        Equal("RangedLine,RangedLine,Marksman,CloseDefense,CloseDefense,Heavy,MeleeReserve,Support",
            roles(SettlementTechStageLevel.IndustrialMid, 8, true), "8 residents from Mid");
        Equal("RangedLine", roles(SettlementTechStageLevel.Spacer, 1, true), "a lone resident is line");
        True(LayoutLoadoutModel.Roles(4, SettlementTechStageLevel.IndustrialEarly, true).Contains(LoadoutRole.Support), "mortar crew kept");
        True(!LayoutLoadoutModel.Roles(4, SettlementTechStageLevel.IndustrialEarly, false).Contains(LoadoutRole.Support), "no crew without a court");
        foreach (int n in new[] { 1, 2, 3, 5, 13, 40 })
        {
            List<LoadoutRole> list = LayoutLoadoutModel.Roles(n, SettlementTechStageLevel.IndustrialMid, true);
            True(list.Count == n && list.Contains(LoadoutRole.RangedLine), n + " residents keep a line");
        }
    }

    private static void LoadoutWeapons()
    {
        Dictionary<SettlementTechStageLevel, string> golden = new Dictionary<SettlementTechStageLevel, string>
        {
            { SettlementTechStageLevel.Neolithic, "RangedLine=Bow_Recurve Marksman=Bow_Recurve CloseDefense=MeleeWeapon_Club MeleeReserve=MeleeWeapon_Club Support=MeleeWeapon_Knife" },
            { SettlementTechStageLevel.Medieval, "RangedLine=Bow_Great Marksman=Bow_Great CloseDefense=MeleeWeapon_LongSword MeleeReserve=MeleeWeapon_LongSword Support=MeleeWeapon_Knife" },
            { SettlementTechStageLevel.IndustrialEarly, "RangedLine=Gun_BoltActionRifle Marksman=Gun_BoltActionRifle CloseDefense=Gun_PumpShotgun MeleeReserve=MeleeWeapon_LongSword Support=Gun_Revolver" },
            { SettlementTechStageLevel.IndustrialMid, "RangedLine=Gun_AssaultRifle Marksman=Gun_SniperRifle CloseDefense=Gun_ChainShotgun Heavy=Gun_LMG MeleeReserve=MeleeWeapon_LongSword Support=Gun_Autopistol" },
            { SettlementTechStageLevel.IndustrialLate, "RangedLine=Gun_AssaultRifle Marksman=Gun_SniperRifle CloseDefense=Gun_ChainShotgun Heavy=Gun_Minigun MeleeReserve=MeleeWeapon_LongSword Support=Gun_Autopistol" },
            { SettlementTechStageLevel.Spacer, "RangedLine=Gun_ChargeRifle Marksman=Gun_SniperRifle CloseDefense=Gun_ChainShotgun Heavy=Gun_Minigun MeleeReserve=MeleeWeapon_LongSword Support=Gun_Autopistol" }
        };
        foreach (KeyValuePair<SettlementTechStageLevel, string> entry in golden)
        {
            SettlementLoadout loadout = Loadout(entry.Key);
            foreach (string pair in entry.Value.Split(' '))
            {
                string[] kv = pair.Split('=');
                LoadoutRole role = (LoadoutRole)Enum.Parse(typeof(LoadoutRole), kv[0]);
                True(loadout.Residents.Where(r => r.Role == role).All(r => r.Weapon != null && r.Weapon.DefName == kv[1]) &&
                    loadout.Residents.Any(r => r.Role == role), entry.Key + " " + role + " expects " + kv[1]);
            }
            LoadoutValid(entry.Key, Temperate, loadout);
        }
        SettlementLoadout medieval = Loadout(SettlementTechStageLevel.Medieval);
        True(medieval.Residents.Where(r => LayoutLoadoutModel.IsMelee(r.Weapon.DefName)).All(r => r.Weapon.Stuff == "Steel"), "medieval melee is steel");
        SettlementLoadout tribal = Loadout(SettlementTechStageLevel.Neolithic);
        True(tribal.Residents.Where(r => r.Weapon.DefName == "MeleeWeapon_Club").All(r => r.Weapon.Stuff == "WoodLog"), "tribal clubs are wooden");
        True(!Loadout(SettlementTechStageLevel.IndustrialEarly).Residents.Any(r => r.Weapon.DefName.StartsWith("Bow_") || r.Weapon.DefName == "MeleeWeapon_Club"),
            "no bows or clubs above Medieval");
    }

    private static void LoadoutApparel()
    {
        Dictionary<SettlementTechStageLevel, string> line = new Dictionary<SettlementTechStageLevel, string>
        {
            { SettlementTechStageLevel.Neolithic, "Apparel_TribalA,Apparel_WarMask" },
            { SettlementTechStageLevel.Medieval, "Apparel_PlateArmor,Apparel_BasicShirt,Apparel_Pants" },
            { SettlementTechStageLevel.IndustrialEarly, "Apparel_SimpleHelmet,Apparel_BasicShirt,Apparel_Pants" },
            { SettlementTechStageLevel.IndustrialMid, "Apparel_FlakVest,Apparel_FlakPants,Apparel_SimpleHelmet,Apparel_BasicShirt" },
            { SettlementTechStageLevel.IndustrialLate, "Apparel_FlakJacket,Apparel_AdvancedHelmet,Apparel_FlakVest,Apparel_FlakPants,Apparel_BasicShirt" },
            { SettlementTechStageLevel.Spacer, "Apparel_ArmorRecon,Apparel_ArmorHelmetRecon,Apparel_BasicShirt,Apparel_Pants" }
        };
        foreach (KeyValuePair<SettlementTechStageLevel, string> entry in line)
        {
            SettlementLoadout loadout = Loadout(entry.Key);
            ResidentLoadout rifleman = loadout.Residents.First(r => r.Role == LoadoutRole.RangedLine && !r.Elite);
            Equal(entry.Value, string.Join(",", rifleman.Apparel.Select(a => a.DefName)), entry.Key + " line apparel");
        }
        SettlementLoadout medieval = Loadout(SettlementTechStageLevel.Medieval);
        True(medieval.Residents.Where(r => LayoutLoadoutModel.IsFrontLine(r.Role)).All(r => r.Apparel.Any(a => a.DefName == "Apparel_PlateArmor")) &&
            !medieval.Residents.Where(r => !LayoutLoadoutModel.IsFrontLine(r.Role)).Any(r => r.Apparel.Any(a => a.DefName == "Apparel_PlateArmor")),
            "plate armour on the front line only");
        SettlementLoadout spacer = Loadout(SettlementTechStageLevel.Spacer);
        True(spacer.Residents.Where(r => r.Role == LoadoutRole.Heavy || r.Elite).All(r => r.Apparel.Any(a => a.DefName == "Apparel_PowerArmor")),
            "power armour for the heavy and the elite");
        True(spacer.Residents.Where(r => r.Role != LoadoutRole.Heavy && !r.Elite && r.Role != LoadoutRole.Support)
            .All(r => r.Apparel.Any(a => a.DefName == "Apparel_ArmorRecon")), "recon armour for the other combat roles");
        Equal("Cloth", Loadout(SettlementTechStageLevel.IndustrialEarly).Residents[0].Apparel.First(a => a.DefName == "Apparel_Pants").Stuff, "early fabric is cloth");
        Equal("Synthread", Loadout(SettlementTechStageLevel.IndustrialMid).Residents[0].Apparel.First(a => a.DefName == "Apparel_BasicShirt").Stuff, "synthread from Mid");
        Equal("Leather_Plain", Loadout(SettlementTechStageLevel.Neolithic).Residents[0].Apparel.First(a => a.DefName == "Apparel_TribalA").Stuff, "tribal leather");
        True(Loadout(SettlementTechStageLevel.IndustrialMid).Residents.Where(r => r.Role == LoadoutRole.Support).All(r => r.Utility != null &&
            r.Utility.DefName == "Apparel_SmokepopBelt"), "support carries a smokepop belt from Mid");
    }

    private static void LoadoutClimate()
    {
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            SettlementLoadout cold = Loadout(level, Cold);
            SettlementLoadout hot = Loadout(level, Hot);
            LoadoutValid(level, Cold, cold);
            LoadoutValid(level, Hot, hot);
            string shell = level == SettlementTechStageLevel.Medieval ? "Apparel_Duster" : "Apparel_Parka";
            True(cold.Residents.Any(r => r.Apparel.Any(a => a.DefName == shell)), level + " cold adds " + shell);
            True(!hot.AllItems.Any(i => i.DefName == "Apparel_Parka" || i.DefName == "Apparel_Duster"), level + " hot never adds parkas or dusters");
            True(!Loadout(level).AllItems.Any(i => i.DefName == "Apparel_Parka" || i.DefName == "Apparel_Duster"), level + " temperate has none");
        }
    }

    private static void LoadoutSpacer()
    {
        SettlementLoadout spacer = Loadout(SettlementTechStageLevel.Spacer, Temperate, 12);
        List<ResidentLoadout> belted = spacer.Residents.Where(r => r.Utility != null && r.Utility.DefName == "Apparel_ShieldBelt").ToList();
        True(belted.Count > 0, "melee reserve wears shield belts");
        True(belted.All(r => LayoutLoadoutModel.IsMelee(r.Weapon.DefName) && (r.Role == LoadoutRole.MeleeReserve || r.Role == LoadoutRole.CloseDefense)),
            "shield belts only with melee weapons");
        foreach (ResidentLoadout resident in spacer.Residents)
        {
            List<CatalogDef> worn = resident.Apparel.Select(a => { CatalogDef d; CoreCatalog.TryGet(a.DefName, out d); return d; }).ToList();
            True(worn.Count(d => d.ApparelLayers.Contains("Overhead")) <= 1, "one helmet for #" + resident.Index + ": " + string.Join(",", resident.Apparel));
        }
        CatalogDef recon, advanced;
        CoreCatalog.TryGet("Apparel_ArmorHelmetRecon", out recon);
        CoreCatalog.TryGet("Apparel_AdvancedHelmet", out advanced);
        True(LayoutLoadoutModel.Conflicts(recon, advanced), "FullHead covers UpperHead");
        LoadoutValid(SettlementTechStageLevel.Spacer, Temperate, spacer);
    }

    private static void LoadoutQualityDeterminism()
    {
        SettlementLoadout a = Loadout(SettlementTechStageLevel.IndustrialMid, Temperate, 8, 11);
        SettlementLoadout b = Loadout(SettlementTechStageLevel.IndustrialMid, Temperate, 8, 11);
        Equal(a.Digest, b.Digest, "same seed, same loadout");
        NotEqual(a.Digest, Loadout(SettlementTechStageLevel.IndustrialMid, Temperate, 8, 12).Digest, "seed varies quality");
        Equal(1, a.Residents.Count(r => r.Elite), "one elite");
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            IReadOnlyList<int> weights = LayoutLoadoutModel.QualityWeights(level);
            Equal(100, weights.Sum(), level + " quality weights sum to 100");
            int top = Enumerable.Range(0, weights.Count).Last(q => weights[q] > 0);
            SettlementLoadout loadout = Loadout(level, Temperate, 16, 3, 10000000);
            True(loadout.Residents.Where(r => !r.Elite).SelectMany(r => r.Items).Where(i => i.Quality.HasValue)
                .All(i => (int)i.Quality.Value <= top), level + " quality-bearing gear within the stage table");
        }
        True(Loadout(SettlementTechStageLevel.Neolithic, Temperate, 16, 3, 10000000).AllItems.Where(i => i.Quality.HasValue)
            .Count(i => i.Quality.Value <= LoadoutQuality.Poor) > 0, "tribal gear is often poor");
    }

    private static void LoadoutStocks()
    {
        SettlementLoadout early = Loadout(SettlementTechStageLevel.IndustrialEarly, Temperate, 8, 5, 10000000);
        Equal(2, early.Armoury.Where(i => IsWeapon(i.DefName)).Sum(i => i.Count), "ceil(25% of 8) spare weapons");
        True(early.Armoury.Any(i => i.DefName == "Gun_BoltActionRifle") && early.Armoury.Any(i => i.DefName == "MeleeWeapon_LongSword"), "line and melee spares");
        Equal(2, early.Armoury.Where(i => i.DefName == "Apparel_Pants").Sum(i => i.Count), "two spare apparel sets");
        Equal("MedicineHerbal", early.Medicine.Single().DefName, "herbal before Mid");
        Equal(2 * 8 + 5, early.Medicine.Single().Count, "medicine 2 per resident + 5");
        Equal("MedicineIndustrial", Loadout(SettlementTechStageLevel.IndustrialMid).Medicine.Single().DefName, "industrial medicine from Mid");
        Equal(3, Loadout(SettlementTechStageLevel.IndustrialEarly, Temperate, 10, 5, 10000000).Armoury.Where(i => IsWeapon(i.DefName)).Sum(i => i.Count),
            "ceil(25% of 10)");
        Equal(4, early.Components.Single(c => c.DefName == "ComponentIndustrial").Count, "components = generators + 2");
        True(!early.Components.Any(c => c.DefName == "ComponentSpacer"), "no spacer components below Spacer");
        True(!Loadout(SettlementTechStageLevel.IndustrialLate, Temperate, 8, 5, 10000000).Components.Any(c => c.DefName == "ComponentSpacer"), "none at Late");
        SettlementLoadout spacer = Loadout(SettlementTechStageLevel.Spacer, Temperate, 8, 5, 10000000);
        Equal(2, spacer.Components.Single(c => c.DefName == "ComponentSpacer").Count, "ComponentSpacer only at Spacer");
        True(CoreCatalog.IsLegal("ComponentSpacer", TechStageModel.Create(SettlementTechStageLevel.Spacer)) &&
            !CoreCatalog.IsLegal("ComponentSpacer", TechStageModel.Create(SettlementTechStageLevel.IndustrialLate)), "decision C: raw, Spacer minimum tier");
    }

    private static bool IsWeapon(string defName)
    {
        CatalogDef def;
        return CoreCatalog.TryGet(defName, out def) && def.Category == CatalogCategory.Weapon;
    }

    private static void LoadoutQualityCapability()
    {
        True(CoreCatalog.TryGet("MeleeWeapon_Club", out CatalogDef club) && !club.SupportsQuality,
            "resolved Core club has no quality component");
        True(CoreCatalog.TryGet("Gun_AssaultRifle", out CatalogDef rifle) && rifle.SupportsQuality,
            "quality-bearing weapons retain their native capability");
        SettlementLoadout primitive = Loadout(SettlementTechStageLevel.Neolithic, Temperate, 12, 5, 10000000);
        var clubs = primitive.AllItems.Where(i => i.DefName == "MeleeWeapon_Club").ToArray();
        True(clubs.Length > 0 && clubs.All(i => !i.Quality.HasValue), "resident and spare clubs have no invented quality");
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            var loadout = Loadout(level, Temperate, 12, 5, 10000000);
            True(loadout.AllItems.All(i => !i.Quality.HasValue ||
                CoreCatalog.TryGet(i.DefName, out CatalogDef def) && def.SupportsQuality), level + " all planned quality is realizable");
            LoadoutValid(level, Temperate, loadout);
        }
    }

    private static void LoadoutTribal()
    {
        foreach (LayoutClimate climate in new[] { Temperate, Cold, Hot })
        {
            SettlementLoadout tribal = Loadout(SettlementTechStageLevel.Neolithic, climate, 12);
            True(!tribal.AllItems.Any(i => i.DefName.StartsWith("Gun_")), climate + " no guns");
            True(!tribal.AllItems.Any(i => i.DefName.StartsWith("Apparel_Flak") || i.DefName == "Apparel_SimpleHelmet"), climate + " no flak or helmets");
            True(!tribal.AllItems.Any(i => i.DefName == "MedicineIndustrial") && tribal.Components.Count == 0, climate + " no industrial medicine or components");
            True(tribal.AllItems.Where(i => i.Quality.HasValue).All(i => CoreCatalog.IsLegal(i.DefName, TechStageModel.Create(SettlementTechStageLevel.Neolithic))),
                climate + " all legal");
            LoadoutValid(SettlementTechStageLevel.Neolithic, climate, tribal);
        }
    }

    private static void LoadoutBudgetDefaults()
    {
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            SettlementLoadout loadout = Loadout(level);
            True(loadout.Success && loadout.Degradations.Count == 0, level + " temperate kit fits the stage default: " + loadout.EquipmentValue + "/" +
                loadout.EquipmentValueLimit + " " + string.Join(",", loadout.Degradations));
            Equal(8 * LayoutLoadoutModel.DefaultEquipmentValuePerResident(level), loadout.EquipmentValueLimit, level + " default limit");
        }
        Equal((int)Math.Round(8 * 1000 * 1.2), Kit(SettlementTechStageLevel.IndustrialEarly, Cold).EquipmentValueLimit, "cold multiplier");
        LayoutRequirements profile = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate);
        Equal(Math.Max(P2ProfileCatalog.IndustrialTemperate.Budget.EquipmentValue, 4 * 1000), profile.EquipmentValueLimit, "profile budget is at least the stage default");
        Equal(0, LayoutLoadoutModel.UnitValue(CoreCatalog, "NoSuchDef", null, null), "unknown defs have no value");
        // Vanilla CalculatedBaseMarketValue: bolt-action rifle = costs + work x 0.0036.
        CatalogDef rifle;
        CoreCatalog.TryGet("Gun_BoltActionRifle", out rifle);
        double expected = rifle.CostList.Sum(c => { CatalogDef d; CoreCatalog.TryGet(c.Key, out d); return c.Value * d.MarketValue; }) + rifle.WorkToMake * 0.0036;
        Equal((int)Math.Round(expected, MidpointRounding.AwayFromZero), LayoutLoadoutModel.UnitValue(CoreCatalog, "Gun_BoltActionRifle", null, LoadoutQuality.Normal),
            "vanilla market value port");
    }

    private static void LoadoutBudgetDegradation()
    {
        SettlementLoadout full = Loadout(SettlementTechStageLevel.IndustrialMid, Temperate, 8, 5, 10000000);
        True(full.Success && full.Degradations.Count == 0, "unlimited budget keeps everything");
        SettlementLoadout slightly = Loadout(SettlementTechStageLevel.IndustrialMid, Temperate, 8, 5, full.EquipmentValue - 1);
        True(slightly.Success && slightly.Degradations.SequenceEqual(new[] { "quality_degraded" }), "first quality drops one step");
        True(slightly.EquipmentValue < full.EquipmentValue && slightly.Armoury.Count > 0, "armoury kept after the quality step");
        SettlementLoadout tighter = Loadout(SettlementTechStageLevel.IndustrialMid, Temperate, 8, 5, slightly.EquipmentValue - 1);
        True(tighter.Degradations.Take(2).SequenceEqual(new[] { "quality_degraded", "armoury_dropped" }) && tighter.Armoury.Count == 0, "then the armoury goes");
        SettlementLoadout broke = Loadout(SettlementTechStageLevel.IndustrialMid, Temperate, 8, 5, 100);
        True(!broke.Success && broke.Rejection == LayoutLoadoutModel.EquipmentBudgetExceeded, "then it rejects");
        True(broke.Degradations.SequenceEqual(new[] { "quality_degraded", "armoury_dropped", "utility_dropped" }), "every degradation recorded");
        True(broke.Residents.All(r => r.Utility == null), "utility dropped");
    }

    private static void LoadoutFromLayout()
    {
        ProceduralLayoutInput input = Input(Plains(130), LayoutRequirements.FromPopulation(8, Industrial, Temperate), 1);
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
        Accepted(result);
        SettlementLoadout loadout = LayoutLoadoutModel.Build(input, result.Layout);
        Equal(result.Layout.Power.Generators + 2, loadout.Components.Single(c => c.DefName == "ComponentIndustrial").Count, "components follow the layout's generators");
        Equal(result.Layout.Court != null, loadout.Residents.Any(r => r.Role == LoadoutRole.Support), "mortar crew follows the court");
        Equal(0, ProceduralLayoutValidator.ValidateLoadout(input, result.Layout, loadout).Count, "valid against the layout");
        Equal(loadout.Digest, LayoutLoadoutModel.Build(input, result.Layout).Digest, "deterministic");
    }

    // ---- helpers ----

    private static void P4CFinishPolicy()
    {
        P4CFloorDef[] finishes =
        {
            new P4CFloorDef("WoodPlankFloor", 4, 3), new P4CFloorDef("Concrete", 2),
            new P4CFloorDef("PavedTile", 4), new P4CFloorDef("TileGranite", 12),
            new P4CFloorDef("SterileTile", 35)
        };
        TerrainSnapshot ground = Plains(120);
        ProceduralLayoutInput input = new ProceduralLayoutInput(
            LayoutRequirements.FromPopulation(7, Industrial, Temperate,
                stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialMid)), ground, 0x4cUL,
            CoreCatalog, resources: new LayoutLocalResources(1200, new[] { "BlocksGranite" }));
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Accepted(result);
        string layoutDigest = result.Layout.Digest, inputDigest = input.Digest;
        GridPoint water = new GridPoint(2, 2);
        HashSet<GridPoint> eligible = new HashSet<GridPoint>(ground.Points().Where(c => !c.Equals(water)));
        P4CFinishSite site = new P4CFinishSite(eligible);
        P4CFinishPlan first = P4CFinishPlanner.Plan(input, result.Layout, site, finishes);
        P4CFinishPlan again = P4CFinishPlanner.Plan(input, result.Layout, site, finishes);
        Equal(first.Digest, again.Digest, "same input replays finish digest");
        Equal(layoutDigest, result.Layout.Digest, "finish plan leaves geometry and equipment digest unchanged");
        Equal(inputDigest, input.Digest, "finish plan does not mutate its input");
        True(first.ImportValue <= first.Allowance, "finishes do not consume defense/facility funds");
        True(!first.Floors.ContainsKey(water), "unsupported/protected terrain stays untouched");
        True(!first.Floors.Keys.Intersect(result.Layout.FieldWall?.Fields ?? new GridPoint[0]).Any(),
            "unroofed crops remain ground");
        True(first.Floors.Keys.Intersect(result.Layout.Corridors.SelectMany(c => c.Cells)).Any(),
            "planned outdoor paths inside the perimeter receive floor");
        List<GridPoint> fieldRoute = result.Layout.FieldWall.ArrivalRoute
            .TakeWhile(c => !c.Equals(result.Layout.FieldWall.OuterGate))
            .Concat(new[] { result.Layout.FieldWall.OuterGate }).ToList();
        True(fieldRoute.All(first.Floors.ContainsKey),
            "every supported cell on the gate-to-gate field path receives floor");
        True(first.Floors.ContainsKey(result.Layout.Gates[result.Layout.FieldWall.GateIndex].Cell),
            "the compound gate has no unfloored seam in the field approach");
        HashSet<GridPoint> gateCrossings = new HashSet<GridPoint>(result.Layout.Gates.SelectMany(g =>
            g.OuterVestibule.Concat(g.BandDoors).Concat(new[] { g.Cell })));
        True(first.Floors.Keys.All(c => result.Layout.Enclosure.Contains(c) || fieldRoute.Contains(c) || gateCrossings.Contains(c)),
            "outside paths beyond both wall perimeters stay natural");
        True(!first.Floors.Keys.Intersect(result.Layout.FieldWall.Fields).Any(),
            "the field path does not pave crop cells");
        foreach (LayoutRoom room in result.Layout.Rooms)
        {
            int count = room.Interior.Points().Count(first.Floors.ContainsKey);
            True(count == 0 || count == room.Interior.Points().Count(),
                "room " + room.Id + " is finished coherently or omitted");
        }
        P4CFinishPlan unavailable = P4CFinishPlanner.Plan(input, result.Layout, site, new P4CFloorDef[0]);
        Equal(0, unavailable.Floors.Count, "unsupported definitions cause omission, not rejection");
        True(unavailable.Reasons.Any(r => r.Contains("no_legal_affordable_finish")), "omission is diagnosed");

        TerrainSnapshot mountain = Mountain(130, 110);
        ProceduralLayoutInput tribal = Input(mountain, LayoutRequirements.FromPopulation(6, Tribal, Cold), 4);
        ProceduralLayoutResult tribalResult = ProceduralLayoutPlanner.Plan(tribal);
        Accepted(tribalResult);
        P4CFinishSite mountainSite = new P4CFinishSite(mountain.Points().Where(c =>
            mountain[c].Has(TerrainTraits.SupportsHeavy) && !mountain[c].Has(TerrainTraits.Water)),
            smoothWalls: tribalResult.Layout.NaturalWalls.Select(c =>
                new KeyValuePair<GridPoint, string>(c, "SmoothedGranite")));
        P4CFinishPlan tribalFinish = P4CFinishPlanner.Plan(tribal, tribalResult.Layout, mountainSite, finishes);
        True(tribalFinish.Floors.Values.All(name => name == "WoodPlankFloor"),
            "poor tribal profile gets no industrial or luxury floor");
        True(tribalFinish.ImportValue <= tribalFinish.Allowance, "tribal finishes remain bounded");
        True(tribalFinish.Walls.Keys.All(tribalResult.Layout.NaturalWalls.Contains),
            "only retained planned natural boundaries are smoothed");
        True(tribalFinish.Walls.Keys.All(c => !tribalResult.Layout.Excavation.Contains(c)),
            "excavated rock is never converted twice");
    }

    private static ProceduralLayoutInput Input(TerrainSnapshot terrain, LayoutRequirements requirements, ulong seed,
        LayoutFortificationReservation fortification = null) => new ProceduralLayoutInput(requirements, terrain, seed, CoreCatalog, fortification);

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
        // Trailing blanks (open ground at line ends) are trimmed so the files pass git diff --check.
        text = string.Join("\n", text.Split('\n').Select(line => line.TrimEnd(' ')));
        File.WriteAllText(Path.Combine(exportDir, name + ".txt"), text);
    }

    private static TerrainSnapshot TerrainFor(string name) =>
        name.StartsWith("mountain") ? Mountain(130, 110) : name.StartsWith("obstacles") ? Obstacles(120) :
        name == "plains-industrial-16" ? Plains(140) : name.Contains("personal") || name.StartsWith("plains-industrial-mid") ||
        name.StartsWith("plains-industrial-early") ? Plains(130) : Plains(120);

    // ---- synthetic terrain ----

    private const TerrainTraits Ground = TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.SupportsHeavy | TerrainTraits.Fertile;
    private const TerrainTraits RockRoofed = TerrainTraits.NaturalRock | TerrainTraits.Occupied | TerrainTraits.SupportsHeavy |
        TerrainTraits.Roofed | TerrainTraits.ThickRoof;
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
        True(!TechStageModel.Create(SettlementTechStageLevel.IndustrialMid).HasResearch("SniperTurret") &&
            TechStageModel.Create(SettlementTechStageLevel.IndustrialLate).HasResearch("SniperTurret"),
            "uranium slug turret research begins at high industrial");
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

    // ---- tech catalog (synthetic Core fixture; test data, not live defs) ----

    private static LayoutDemandSet Demand(LayoutRequirements requirements) =>
        LayoutEquipment.Resolve(requirements, CoreCatalog, null, 2, 7).Demand;

    private static TechCatalog coreCatalog;
    private static TechCatalog CoreCatalog => coreCatalog ?? (coreCatalog = CoreCatalogFixture.Build());

    private static void CatalogPrerequisiteClosure()
    {
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            SettlementTechStage stage = TechStageModel.Create(level);
            IReadOnlyList<string> gaps = CoreCatalog.ResearchSetGaps(stage.Research);
            Equal(0, gaps.Count, level + " closed under prerequisites: " + string.Join(",", gaps));
            True(!stage.Research.Any(ProhibitedContent.IsProhibitedResearch), level + " never contains IEDs research");
        }
        CatalogResearch ieds;
        True(CoreCatalog.TryGetResearch("IEDs", out ieds), "fixture carries the IEDs research");
        Contains(CoreCatalog.ResearchSetGaps(new[] { "HospitalBed", "IEDs" }), "missing_prerequisite:HospitalBed->SterileMaterials");
        Contains(CoreCatalog.ResearchSetGaps(new[] { "IEDs" }), "prohibited_research:IEDs");
        Contains(CoreCatalog.ResearchSetGaps(new[] { "NoSuchProject" }), "unknown_research:NoSuchProject");
    }

    private static void P4BTurretCatalogFacts()
    {
        foreach (string name in new[] { "Turret_MiniTurret", "Turret_Autocannon", "Turret_Sniper" })
        {
            True(CoreCatalog.TryGet(name, out CatalogDef def) && def.AutonomousTurret,
                name + " has an autonomous turret gun and draw");
            True(def.MaximumRange > def.MinimumRange && def.FuelCapacity > 0 &&
                def.FuelMultiplier > 0 && def.FuelPerShot > 0 && def.InitialFuelPercent == 1 &&
                def.FuelFactorByDifficulty, name + " has finite difficulty-scaled shots");
        }
        CoreCatalog.TryGet("Turret_MiniTurret", out CatalogDef mini);
        CoreCatalog.TryGet("Turret_Autocannon", out CatalogDef auto);
        CoreCatalog.TryGet("Turret_Sniper", out CatalogDef slug);
        Equal(0.0, mini.MinimumRange, "mini-turret has no inner blind zone");
        Equal(8.9, auto.MinimumRange, "autocannon close blind zone");
        Equal(11.9, slug.MinimumRange, "uranium turret close blind zone");
        Equal(45.9, slug.MaximumRange, "uranium turret long lane");
        Equal(150, slug.BasePowerConsumption, "uranium turret power load");
        Equal("Uranium", slug.Fuel.Single(), "uranium is its actual reload input");
        Equal(30.0, slug.FuelCapacity, "uranium turret fuel capacity");
        Equal(0.5, slug.FuelMultiplier, "uranium reload multiplier before difficulty");
        Equal(5.9, slug.ExplosionRadius, "uranium turret explosion clearance");
        True(!CoreCatalog.IsLegal(slug, TechStageModel.Create(SettlementTechStageLevel.IndustrialMid)) &&
            CoreCatalog.IsLegal(slug, TechStageModel.Create(SettlementTechStageLevel.IndustrialLate)),
            "late industrial eligibility remains stage-gated");
    }

    private static void CatalogGoldenLegality()
    {
        Golden(SettlementTechStageLevel.Neolithic,
            new[] { "Bedroll", "SleepingSpot", "CraftingSpot", "Campfire", "TorchLamp", "PassiveCooler", "FueledStove", "Wall", "Door",
                "HorseshoesPin", "Bow_Short", "Bow_Recurve", "MeleeWeapon_Club", "MeleeWeapon_Knife", "Pila", "Apparel_TribalA",
                "Apparel_Parka", "WoodLog", "ChunkGranite", "MedicineHerbal", "Leather_Plain", "Cloth" },
            new[]
            {
                "Bed=research_missing", "Shelf=research_missing", "DiningChair=research_missing", "ChessTable=research_missing",
                "OrnateDoor=research_missing", "Vent=research_missing", "FueledSmithy=research_missing", "HandTailoringBench=research_missing",
                "TableStonecutter=research_missing", "BlocksGranite=producer_illegal", "Gun_Revolver=research_missing",
                "Apparel_Cape=above_ceiling", "StandingLamp=research_missing", "Heater=research_missing",
                "WoodFiredGenerator=research_missing", "Bow_Great=research_missing", "MeleeWeapon_Ikwa=research_missing",
                "Apparel_BasicShirt=research_missing"
            });
        Golden(SettlementTechStageLevel.Medieval,
            new[] { "Bed", "Shelf", "DiningChair", "FueledSmithy", "HandTailoringBench", "TableStonecutter", "BlocksGranite",
                "MeleeWeapon_LongSword", "Apparel_PlateArmor", "Bow_Great", "Apparel_BasicShirt", "Apparel_Cape" },
            new[] { "Gun_Revolver=research_missing", "StandingLamp=research_missing", "Apparel_SimpleHelmet=above_ceiling", "Battery=research_missing" });
        Golden(SettlementTechStageLevel.IndustrialEarly,
            new[] { "Gun_BoltActionRifle", "Gun_Revolver", "Gun_PumpShotgun", "Turret_Mortar", "Shell_HighExplosive", "WoodFiredGenerator",
                "ChemfuelPoweredGenerator", "WindTurbine", "Battery", "StandingLamp", "Heater", "Cooler", "ElectricStove", "ElectricSmithy",
                "TableMachining", "Apparel_SimpleHelmet", "Bed" },
            new[]
            {
                "Apparel_FlakVest=research_missing", "HospitalBed=research_missing", "Gun_AssaultRifle=research_missing",
                "SolarGenerator=research_missing", "Chemfuel=producer_illegal", "MedicineIndustrial=research_missing",
                "Turret_MiniTurret=research_missing"
            });
        Golden(SettlementTechStageLevel.IndustrialMid,
            new[] { "HospitalBed", "Apparel_FlakVest", "Gun_SniperRifle", "Gun_AssaultRifle", "SolarGenerator", "WatermillGenerator",
                "GeothermalGenerator", "Chemfuel", "MedicineIndustrial", "Turret_MiniTurret" },
            new[] { "VitalsMonitor=research_missing", "Gun_Minigun=research_missing", "Gun_ChargeRifle=research_missing", "FabricationBench=research_missing" });
        Golden(SettlementTechStageLevel.IndustrialLate,
            new[] { "VitalsMonitor", "MultiAnalyzer", "Gun_Minigun", "FabricationBench" },
            new[] { "Gun_ChargeRifle=research_missing", "Apparel_ShieldBelt=research_missing" });
        Golden(SettlementTechStageLevel.Spacer,
            new[] { "Gun_ChargeRifle", "Apparel_PowerArmor", "Apparel_ShieldBelt", "HospitalBed", "Gun_Revolver" },
            new string[0]);

        HashSet<string> previous = new HashSet<string>();
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            HashSet<string> legal = new HashSet<string>(CoreCatalog.LegalDefs(TechStageModel.Create(level)).Select(d => d.DefName));
            True(previous.All(legal.Contains), level + " keeps every earlier legal def");
            True(legal.Count > previous.Count, level + " adds legal defs");
            previous = legal;
        }
    }

    private static void Golden(SettlementTechStageLevel level, string[] legal, string[] illegal)
    {
        SettlementTechStage stage = TechStageModel.Create(level);
        foreach (string def in legal)
            True(CoreCatalog.IsLegal(def, stage), level + " should allow " + def + ": " + CoreCatalog.Evaluate(def, stage));
        foreach (string entry in illegal)
        {
            string[] parts = entry.Split('=');
            CatalogLegality verdict = CoreCatalog.Evaluate(parts[0], stage);
            True(!verdict.Legal, level + " should forbid " + parts[0]);
            Equal(parts[1], verdict.Code, level + " " + parts[0] + " reason (" + verdict.Reason + ")");
        }
    }

    private static void CatalogProhibitedContent()
    {
        string[] prohibited = CoreCatalog.Defs.Where(ProhibitedContent.IsProhibited).Select(d => d.DefName).ToArray();
        True(prohibited.SequenceEqual(new[] { "TrapIED_AntigrainWarhead", "TrapIED_EMP", "TrapIED_Firefoam", "TrapIED_HighExplosive",
            "TrapIED_Incendiary", "TrapIED_Smoke", "TrapSpike" }), "Core traps and IEDs: " + string.Join(",", prohibited));
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            SettlementTechStage stage = TechStageModel.Create(level).WithAdaptation("IEDs", "test_only_forced");
            True(!CoreCatalog.LegalDefs(stage).Any(d => ProhibitedContent.IsProhibited(d)), level + " legal set has no trap or IED");
            foreach (string def in prohibited) Equal(CatalogLegality.Prohibited, CoreCatalog.Evaluate(def, stage).Code, level + " " + def);
        }
    }

    private static void CatalogLegalityRules()
    {
        TechCatalog catalog = new TechCatalog("rules", new[]
        {
            new CatalogDef("Spot", CatalogCategory.Building),
            new CatalogDef("SpacerThing", CatalogCategory.Apparel, techLevel: TechTier.Spacer),
            new CatalogDef("QuietTrap", CatalogCategory.Building, thingClass: "Building_TrapDamager"),
            new CatalogDef("Pit", CatalogCategory.Building, isTrap: true),
            new CatalogDef("ScrapIEDKit", CatalogCategory.Item),
            new CatalogDef("Bench", CatalogCategory.Building, research: new[] { "Stonecutting" }),
            new CatalogDef("Blocks", CatalogCategory.Item, producedAt: new[] { "Bench" }),
            new CatalogDef("Ore", CatalogCategory.Item, producedAt: new[] { "Bench" }, raw: true),
            new CatalogDef("Orphan", CatalogCategory.Item, producedAt: new[] { "MissingBench" }),
            new CatalogDef("Loot", CatalogCategory.Item),
            new CatalogDef("SmithedAxe", CatalogCategory.Weapon, producedAt: new[] { "Bench" })
        }, new[] { new CatalogResearch("Stonecutting", TechTier.Medieval) });
        SettlementTechStage tribal = TechStageModel.Create(SettlementTechStageLevel.Neolithic);
        True(catalog.IsLegal("Spot", tribal), "undefined tech level and no research is legal");
        Equal(CatalogLegality.AboveCeiling, catalog.Evaluate("SpacerThing", tribal).Code, "no-research def above ceiling");
        Equal(CatalogLegality.Prohibited, catalog.Evaluate("QuietTrap", tribal).Code, "trap class");
        Equal(CatalogLegality.Prohibited, catalog.Evaluate("Pit", tribal).Code, "trap flag");
        Equal(CatalogLegality.Prohibited, catalog.Evaluate("ScrapIEDKit", tribal).Code, "IED name");
        Equal(CatalogLegality.ProducerIllegal, catalog.Evaluate("Blocks", tribal).Code, "producer gate");
        True(catalog.IsLegal("Blocks", tribal.WithAdaptation("Stonecutting", "local_wood_scarce")), "adaptation legalizes the producer");
        True(catalog.IsLegal("Ore", tribal), "raw items need no producer");
        Equal(CatalogLegality.ProducerIllegal, catalog.Evaluate("Orphan", tribal).Code, "unknown producer");
        True(catalog.IsLegal("Loot", tribal), "item without producer");
        True(catalog.IsLegal("SmithedAxe", tribal), "producer gate applies to items only");
        Equal("unknown_def:Nope", catalog.Evaluate("Nope", tribal).Reason, "unknown def");
        Equal("research_missing:Stonecutting", catalog.Evaluate("Bench", tribal).Reason, "research reason names the project");
    }

    private static void CatalogFixtureData()
    {
        TechCatalog catalog = CoreCatalog;
        Equal(catalog.Digest, CoreCatalogFixture.Build().Digest, "digest stable");
        True(catalog.Defs.Select(d => d.DefName).SequenceEqual(catalog.Defs.Select(d => d.DefName).OrderBy(n => n, StringComparer.Ordinal)), "defs sorted");
        CatalogDef def;
        True(catalog.TryGet("Bed", out def) && def.SizeX == 1 && def.SizeZ == 2 && def.ResearchPrerequisites.Contains("ComplexFurniture"), "bed");
        True(catalog.TryGet("FueledSmithy", out def) && def.SizeX == 3 && def.SizeZ == 1 && def.HasInteractionCell &&
            def.InteractionOffset.Equals(new GridPoint(0, -1)) && def.Fuel.Contains("WoodLog"), "smithy");
        True(catalog.TryGet("WoodFiredGenerator", out def) && def.BasePowerConsumption == -1000 && def.FuelPerDay == 22 && def.SizeX == 2 && def.SizeZ == 2, "wood generator");
        True(catalog.TryGet("ChemfuelPoweredGenerator", out def) && def.BasePowerConsumption == -1000 && def.FuelPerDay == 4.5 && def.Fuel.Contains("Chemfuel"), "chemfuel generator");
        True(catalog.TryGet("WindTurbine", out def) && def.SizeX == 7 && def.SizeZ == 2 && def.BasePowerConsumption == -2300, "wind turbine");
        True(catalog.TryGet("SolarGenerator", out def) && def.SizeX == 4 && def.SizeZ == 4 && def.BasePowerConsumption == -1700, "solar");
        True(catalog.TryGet("WatermillGenerator", out def) && def.SizeX == 5 && def.SizeZ == 6, "watermill");
        True(catalog.TryGet("GeothermalGenerator", out def) && def.SizeX == 6 && def.SizeZ == 6, "geothermal");
        True(catalog.TryGet("Wall", out def) && def.Blocking && def.StuffCategories.Count > 0, "wall");
        True(catalog.TryGet("BlocksGranite", out def) && def.ProducedAt.SequenceEqual(new[] { "TableStonecutter" }) && !def.Raw, "stone blocks");
        True(catalog.TryGet("Apparel_Parka", out def) && def.ApparelLayers.Contains("Shell") && def.ApparelBodyParts.Contains("Torso"), "parka");
        True(catalog.TryGet("Gun_Revolver", out def) && def.ResearchPrerequisites.SequenceEqual(new[] { "Gunsmithing" }), "revolver inherits Gunsmithing");
        True(!catalog.Defs.Any(d => d.DefName.StartsWith("Artillery_") || d.DefName == "Gun_MiniTurret"), "turret-top guns excluded");
        TechCatalog changed = new TechCatalog(catalog.Source, catalog.Defs.Where(d => d.DefName != "Bed")
            .Concat(new[] { new CatalogDef("Bed", CatalogCategory.Building, sizeX: 1, sizeZ: 2) }), catalog.Research);
        NotEqual(catalog.Digest, changed.Digest, "digest covers def fields");
        bool threw = false;
        try { new TechCatalog("dup", new[] { new CatalogDef("A", CatalogCategory.Item), new CatalogDef("A", CatalogCategory.Item) }, null); }
        catch (ArgumentException) { threw = true; }
        True(threw, "duplicate defs rejected");
    }

    private static void CatalogRawMinimumTiers()
    {
        SettlementTechStage neolithic = TechStageModel.Create(SettlementTechStageLevel.Neolithic);
        SettlementTechStage early = TechStageModel.Create(SettlementTechStageLevel.IndustrialEarly);
        SettlementTechStage late = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        SettlementTechStage spacer = TechStageModel.Create(SettlementTechStageLevel.Spacer);
        foreach (string def in new[] { "Plasteel", "Synthread", "ComponentIndustrial", "Uranium", "Hyperweave", "ComponentSpacer" })
            Equal(CatalogLegality.AboveCeiling, CoreCatalog.Evaluate(def, neolithic).Code, "neolithic lacks " + def);
        foreach (string def in new[] { "Plasteel", "Synthread", "ComponentIndustrial", "Uranium" })
            True(CoreCatalog.IsLegal(def, early), "industrial early has " + def);
        Equal(CatalogLegality.AboveCeiling, CoreCatalog.Evaluate("ComponentSpacer", late).Code, "industrial late lacks ComponentSpacer");
        Equal(CatalogLegality.AboveCeiling, CoreCatalog.Evaluate("Hyperweave", late).Code, "industrial late lacks Hyperweave");
        True(CoreCatalog.IsLegal("ComponentSpacer", spacer) && CoreCatalog.IsLegal("Hyperweave", spacer), "spacer has ComponentSpacer and Hyperweave");
        True(CoreCatalog.IsLegal("Steel", neolithic) && CoreCatalog.IsLegal("WoodLog", neolithic), "ordinary raw stuff stays legal");
        CatalogDef def2;
        True(CoreCatalog.TryGet("ComponentSpacer", out def2) && def2.Raw && def2.ResearchPrerequisites.Count == 0, "ComponentSpacer is raw without production research");
        Equal(TechTier.Spacer, TechCatalog.EffectiveTechLevel(def2), "effective tier from the override table");
    }

    private static void RoleCoreResolution()
    {
        Dictionary<SettlementTechStageLevel, string> expected = new Dictionary<SettlementTechStageLevel, string>
        {
            { SettlementTechStageLevel.Neolithic, "Sleep=Bedroll Light=TorchLamp Cooking=Campfire CraftSmithing=CraftingSpot CraftTailoring=CraftingSpot Storage=Stockpile Door=Door Seat=Stool Recreation=HorseshoesPin Heat=Campfire Cool=PassiveCooler MedicalBed=Bedroll" },
            { SettlementTechStageLevel.Medieval, "Sleep=Bed Light=TorchLamp Cooking=FueledStove CraftSmithing=FueledSmithy CraftTailoring=HandTailoringBench CraftStonecutting=TableStonecutter Storage=Shelf Seat=DiningChair Recreation=ChessTable" },
            { SettlementTechStageLevel.IndustrialEarly, "Sleep=Bed Light=StandingLamp Cooking=ElectricStove CraftSmithing=ElectricSmithy CraftMachining=TableMachining Heat=Heater Cool=Cooler Defense=Sandbags MedicalBed=Bed" },
            { SettlementTechStageLevel.IndustrialMid, "MedicalBed=HospitalBed Defense=Turret_MiniTurret UtilityBelt=Apparel_SmokepopBelt" },
            { SettlementTechStageLevel.IndustrialLate, "MedicalMonitor=VitalsMonitor CraftFabrication=FabricationBench Defense=Turret_Sniper" },
            { SettlementTechStageLevel.Spacer, "UtilityBelt=Apparel_ShieldBelt Sleep=Bed Light=StandingLamp" }
        };
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            RoleResolutionSet set = RoleResolver.Resolve(CoreCatalog, TechStageModel.Create(level));
            True(set.Success && set.StepDowns.Count == 0 && set.Stage.Level == level, level + " resolves without step-down");
            True(set.Resolutions.Where(r => r.Essential).All(r => r.Resolved), level + " essentials resolved");
            foreach (string pair in expected[level].Split(' '))
            {
                string[] kv = pair.Split('=');
                Equal(kv[1], set.ChosenDefName((SettlementRole)Enum.Parse(typeof(SettlementRole), kv[0])), level + " " + kv[0]);
            }
            foreach (RoleResolution r in set.Resolutions.Where(r => !r.Resolved))
                Contains(set.Diagnostics, RoleResolver.RoleUnresolved + ":" + r.Role);
            Equal(set.Digest, RoleResolver.Resolve(CoreCatalog, TechStageModel.Create(level)).Digest, level + " digest stable");
        }
        RoleResolutionSet tribal = RoleResolver.Resolve(CoreCatalog, TechStageModel.Create(SettlementTechStageLevel.Neolithic));
        True(!CoreCatalog.IsLegal("Turret_Sniper", TechStageModel.Create(SettlementTechStageLevel.IndustrialMid)) &&
            CoreCatalog.IsLegal("Turret_Sniper", TechStageModel.Create(SettlementTechStageLevel.IndustrialLate)),
            "uranium slug turret def respects the high-industrial research boundary");
        string[] complexFurniture = { "Bed", "Shelf", "DiningChair", "ChessTable", "OrnateDoor", "Vent", "FueledSmithy", "HandTailoringBench" };
        True(!tribal.Resolutions.SelectMany(r => r.Candidates).Any(o => complexFurniture.Contains(o.DefName)), "tribal kit has no ComplexFurniture, smithy or tailoring bench");
        Contains(tribal.Diagnostics, "role_unresolved:CraftStonecutting");
        RoleResolutionSet adapted = RoleResolver.Resolve(CoreCatalog, TechStageModel.Create(SettlementTechStageLevel.Neolithic).WithAdaptation("Stonecutting", "local_wood_scarce"));
        Equal("TableStonecutter", adapted.ChosenDefName(SettlementRole.CraftStonecutting), "stonecutting adaptation resolves the stonecutter");
        NotEqual(tribal.Digest, adapted.Digest, "adaptation changes the resolution digest");
        RoleResolutionSet early = RoleResolver.Resolve(CoreCatalog, TechStageModel.Create(SettlementTechStageLevel.IndustrialEarly));
        True(early.Get(SettlementRole.Light).Rejected.Any(r => r.Key == "TorchLamp" && r.Value == "above_max_stage:Medieval"), "torch capped above Medieval");
        True(early.Get(SettlementRole.Sleep).Candidates.Select(o => o.DefName).SequenceEqual(new[] { "Bed" }), "no primitive sleep fallback at industrial");
        True(early.Get(SettlementRole.Defense).Candidates.Select(o => o.DefName).SequenceEqual(new[] { "Sandbags", "Barricade" }), "cover uncapped, turrets gated");
    }

    private static void RoleStepDown()
    {
        TechCatalog noBed = new TechCatalog("core-without-bed", CoreCatalog.Defs.Where(d => d.DefName != "Bed"), CoreCatalog.Research);
        SettlementTechStage mid = TechStageModel.Create(SettlementTechStageLevel.IndustrialMid).WithAdaptation("Fabrication", "test_adaptation");
        RoleResolutionSet set = RoleResolver.Resolve(noBed, mid);
        True(set.Success, "resolves after stepping down");
        Equal(SettlementTechStageLevel.IndustrialMid, set.RequestedStage.Level, "requested stage recorded");
        // No Core option is capped at IndustrialEarly, so Sleep stays unresolved there and a second step reaches Bedroll.
        True(set.StepDowns.SequenceEqual(new[]
        {
            "stage_step_down:IndustrialMid->IndustrialEarly:essential_role_unresolved:Sleep",
            "stage_step_down:IndustrialEarly->Medieval:essential_role_unresolved:Sleep"
        }), "step-down log: " + string.Join("|", set.StepDowns));
        Equal(SettlementTechStageLevel.Medieval, set.Stage.Level, "final stage");
        True(set.Stage.HasResearch("Fabrication"), "adaptations survive step-down");
        RoleResolutionSet direct = RoleResolver.Resolve(noBed, TechStageModel.Create(SettlementTechStageLevel.Medieval).WithAdaptation("Fabrication", "test_adaptation"));
        foreach (RoleResolution r in set.Resolutions)
            Equal(direct.ChosenDefName(r.Role), r.ChosenDefName, "whole kit re-resolved at the lower tier: " + r.Role);
        Equal("Bedroll", set.ChosenDefName(SettlementRole.Sleep), "sleep falls back to bedroll");
        Equal("TorchLamp", set.ChosenDefName(SettlementRole.Light), "light re-resolved at Medieval");
        Equal("FueledSmithy", set.ChosenDefName(SettlementRole.CraftSmithing), "smithing re-resolved at Medieval");

        // One step with a table whose primitive option is capped at IndustrialEarly.
        List<RoleDefinition> definitions = RoleTable.Standard.Definitions.Where(d => d.Role != SettlementRole.Sleep).ToList();
        definitions.Add(new RoleDefinition(SettlementRole.Sleep, true, new[] { RoleOption.Def("Bed"), RoleOption.Def("Bedroll", SettlementTechStageLevel.IndustrialEarly) }));
        RoleTable table = new RoleTable(definitions);
        NotEqual(RoleTable.Standard.Digest, table.Digest, "table digest covers caps");
        RoleResolutionSet single = RoleResolver.Resolve(noBed, TechStageModel.Create(SettlementTechStageLevel.IndustrialMid), table);
        True(single.StepDowns.SequenceEqual(new[] { "stage_step_down:IndustrialMid->IndustrialEarly:essential_role_unresolved:Sleep" }), "single step-down");
        Equal(SettlementTechStageLevel.IndustrialEarly, single.Stage.Level, "final stage one lower");
        Equal("Bedroll", single.ChosenDefName(SettlementRole.Sleep), "sleep at early");
        Equal("Sandbags", single.ChosenDefName(SettlementRole.Defense), "defense re-resolved at early (no GunTurrets)");
        True(single.Get(SettlementRole.MedicalBed).Rejected.Any(r => r.Key == "HospitalBed" && r.Value.StartsWith("research_missing")), "medical re-resolved at early");
    }

    private static void RoleRejection()
    {
        TechCatalog noDoor = new TechCatalog("core-without-door", CoreCatalog.Defs.Where(d => d.DefName != "Door"), CoreCatalog.Research);
        RoleResolutionSet tribal = RoleResolver.Resolve(noDoor, TechStageModel.Create(SettlementTechStageLevel.Neolithic));
        True(!tribal.Success && tribal.StepDowns.Count == 0, "neolithic cannot step down");
        Equal("essential_role_unresolved:Door", tribal.Diagnostics[0], "rejection diagnostic first");
        RoleResolutionSet medieval = RoleResolver.Resolve(noDoor, TechStageModel.Create(SettlementTechStageLevel.Medieval));
        True(!medieval.Success && medieval.Stage.Level == SettlementTechStageLevel.Neolithic, "steps down to neolithic, then rejects");
        True(medieval.StepDowns.SequenceEqual(new[] { "stage_step_down:Medieval->Neolithic:essential_role_unresolved:Door" }), "logged step-down before rejection");
        Contains(medieval.Diagnostics, "essential_role_unresolved:Door");
    }

    private static void Run(string name, Action test)
    {
        if (only != null && name.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0) return;
        if (runIndex++ % shards != shard) return;
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
