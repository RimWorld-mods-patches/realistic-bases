using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Run("stable seed and input digest", StableSeedAndInputDigest);
        Run("snapshot copies source cells", SnapshotCopiesSourceCells);
        Run("connected regions are deterministic", ConnectedRegionsAreDeterministic);
        Run("disconnected regions retain edge metadata", DisconnectedRegionsRetainEdgeMetadata);
        Run("fixed inputs produce identical plan digests", FixedInputsProduceIdenticalPlanDigests);
        Run("disconnected land stays within one region", DisconnectedLandStaysWithinOneRegion);
        Run("narrow corridors are rejected", NarrowCorridorsAreRejected);
        Run("obstructed interaction cells are rejected", ObstructedInteractionCellsAreRejected);
        Run("impossible capacity is rejected before search", ImpossibleCapacityIsRejectedBeforeSearch);
        Run("invalid water terrain is rejected", InvalidWaterTerrainIsRejected);
        Run("candidate search terminates at deterministic limit", CandidateSearchTerminatesAtLimit);
        Run("planning preserves snapshot digest", PlanningPreservesSnapshotDigest);
        Run("P2 profiles have stable distinct digests", P2ProfilesHaveStableDistinctDigests);
        Run("P2 housing excludes jail and medical capacity", P2HousingExcludesSpecialBeds);
        Run("P2 startup food bridges first harvest", P2StartupFoodBridgesFirstHarvest);
        Run("P2 crop demand is resident driven", P2CropDemandIsResidentDriven);
        Run("P2 resource budgets respect allowances", P2ResourceBudgetsRespectAllowances);
        Run("P2 industrial power scenarios pass", P2IndustrialPowerScenariosPass);
        Run("P2 tribal profile has no electric substitutes", P2TribalHasNoElectricSubstitutes);
        Run("P2 stocks are finite and profile specific", P2StocksAreFiniteAndProfileSpecific);
        Run("P2 industrial layout is deterministic and valid", P2IndustrialLayoutIsDeterministicAndValid);
        Run("P2 tribal layout excludes electric placements", P2TribalLayoutExcludesElectricPlacements);
        Run("P2 layout realizes every facility demand", P2LayoutRealizesEveryFacilityDemand);
        Run("P2 layout reserves legal crop capacity", P2LayoutReservesLegalCropCapacity);
        Run("P2 cooler has exterior exhaust", P2CoolerHasExteriorExhaust);
        Run("P2 power terminals join one conduit tree", P2PowerTerminalsJoinOneConduitTree);
        Run("P2 gates and facility routes are viable", P2GatesAndFacilityRoutesAreViable);
        Run("P2 layout rejection is bounded", P2LayoutRejectionIsBounded);
        Run("P2 layout selects declared local material", P2LayoutSelectsDeclaredLocalMaterial);
        Console.WriteLine("P1/P2 pure tests: " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures);
        return failures == 0 ? 0 : 1;
    }

    private static void StableSeedAndInputDigest()
    {
        SettlementProfile profile = Profile();
        TerrainSnapshot snapshot = Open(12, 12);
        ulong first = SeedDeriver.Derive("P1-world", 420, profile);
        ulong second = SeedDeriver.Derive("P1-world", 420, profile);
        Equal(first, second, "seed");
        Equal(new PlannerInput(profile, snapshot, first).Digest,
            new PlannerInput(profile, snapshot, second).Digest, "input digest");
        NotEqual(first, SeedDeriver.Derive("P1-world", 421, profile), "tile participates in seed");
    }

    private static void SnapshotCopiesSourceCells()
    {
        TerrainCell buildable = Cell(TerrainTraits.Standable | TerrainTraits.Buildable);
        TerrainCell[] source = Enumerable.Repeat(buildable, 9).ToArray();
        TerrainSnapshot snapshot = new TerrainSnapshot(3, 3, source, "synthetic-v1");
        string digest = snapshot.Digest;
        source[4] = Cell(TerrainTraits.Water);
        Equal(digest, snapshot.Digest, "digest is immutable");
        True(snapshot[1, 1].Has(TerrainTraits.Buildable), "copied cell remains buildable");
    }

    private static void ConnectedRegionsAreDeterministic()
    {
        TerrainSnapshot snapshot = Open(5, 4);
        IReadOnlyList<ConnectedRegion> first = ConnectedRegionAnalyzer.Analyze(snapshot, IsBuildable);
        IReadOnlyList<ConnectedRegion> second = ConnectedRegionAnalyzer.Analyze(snapshot, IsBuildable);
        Equal(1, first.Count, "one region");
        Equal(20, first[0].Area, "region area");
        Equal(string.Join(";", first[0].Cells), string.Join(";", second[0].Cells), "cell order");
    }

    private static void DisconnectedRegionsRetainEdgeMetadata()
    {
        TerrainCell[] cells = Enumerable.Repeat(Cell(TerrainTraits.Water), 35).ToArray();
        cells[0] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.MapEdge);
        cells[1] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.MapEdge);
        cells[3 * 7 + 3] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable);
        cells[3 * 7 + 4] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable);
        TerrainSnapshot snapshot = new TerrainSnapshot(7, 5, cells, "islands-v1");
        IReadOnlyList<ConnectedRegion> regions = ConnectedRegionAnalyzer.Analyze(snapshot, IsBuildable);
        Equal(2, regions.Count, "region count");
        True(regions[0].TouchesEdge, "first region touches edge");
        True(!regions[1].TouchesEdge, "second region is enclosed");
    }

    private static void FixedInputsProduceIdenticalPlanDigests()
    {
        PlannerInput input = Input(Open(20, 20), Profile(), 17);
        PlanningResult first = SettlementPlanner.Plan(input);
        PlanningResult second = SettlementPlanner.Plan(input);
        True(first.Success && second.Success, "both plans succeed");
        Equal(first.Plan.Digest, second.Plan.Digest, "plan digest");
        Equal(first.Plan.CandidatesConsidered, second.Plan.CandidatesConsidered, "candidate count");
        True(SettlementPlanValidator.Validate(input, first.Plan).IsValid, "plan validates");
    }

    private static void DisconnectedLandStaysWithinOneRegion()
    {
        TerrainCell[] cells = Enumerable.Repeat(Cell(TerrainTraits.Water), 28 * 18).ToArray();
        FillRect(cells, 28, 0, 0, 12, 18, true);
        FillRect(cells, 28, 16, 0, 12, 18, true);
        TerrainSnapshot snapshot = new TerrainSnapshot(28, 18, cells, "disconnected-v1");
        PlanningResult result = SettlementPlanner.Plan(Input(snapshot, Profile(), 21));
        True(result.Success, "one island accepts the compound");
        bool west = result.Plan.Compound.MaxX < 12;
        bool east = result.Plan.Compound.MinX >= 16;
        True(west || east, "compound does not cross water split");
        True(result.Plan.Route.All(p => !snapshot[p].HasAny(TerrainTraits.Water)), "route remains on land");
    }

    private static void NarrowCorridorsAreRejected()
    {
        TerrainCell[] cells = Enumerable.Repeat(Cell(TerrainTraits.Water), 20 * 20).ToArray();
        FillRect(cells, 20, 0, 0, 3, 20, true);
        TerrainSnapshot snapshot = new TerrainSnapshot(20, 20, cells, "corridor-v1");
        PlanningResult result = SettlementPlanner.Plan(Input(snapshot, Profile(), 31));
        True(!result.Success, "narrow strip rejects 7x7 compound");
    }

    private static void ObstructedInteractionCellsAreRejected()
    {
        TerrainCell[] cells = OpenCells(9, 9);
        cells[3 * 9 + 4] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.Occupied);
        TerrainSnapshot snapshot = new TerrainSnapshot(9, 9, cells, "interaction-block-v1");
        PlanningResult result = SettlementPlanner.Plan(Input(snapshot, Profile(64), 41));
        True(!result.Success, "only candidate rejects obstruction");
        True(result.Rejections.Any(r => r.Contains("interaction_obstructed")), "specific rejection is reported");
    }

    private static void ImpossibleCapacityIsRejectedBeforeSearch()
    {
        SettlementProfile impossible = new SettlementProfile("diagnostic", 1, 7, 7, 21, 64, 512);
        PlanningResult result = SettlementPlanner.Plan(Input(Open(20, 20), impossible, 51));
        True(!result.Success, "capacity rejects");
        Equal(0, result.CandidatesConsidered, "no candidates evaluated");
        True(result.Rejections[0].StartsWith("impossible_capacity"), "capacity reason");
    }

    private static void InvalidWaterTerrainIsRejected()
    {
        TerrainSnapshot snapshot = new TerrainSnapshot(20, 20,
            Enumerable.Repeat(Cell(TerrainTraits.Water), 400), "water-v1");
        PlanningResult result = SettlementPlanner.Plan(Input(snapshot, Profile(), 61));
        True(!result.Success, "water map rejects");
    }

    private static void CandidateSearchTerminatesAtLimit()
    {
        TerrainCell[] cells = OpenCells(30, 30);
        for (int i = 0; i < cells.Length; i++)
            cells[i] = Cell(cells[i].Traits | TerrainTraits.Roofed);
        TerrainSnapshot snapshot = new TerrainSnapshot(30, 30, cells, "roofed-v1");
        SettlementProfile profile = new SettlementProfile("bounded", 1, 7, 7, 20, 3, 128);
        PlanningResult first = SettlementPlanner.Plan(Input(snapshot, profile, 71));
        PlanningResult second = SettlementPlanner.Plan(Input(snapshot, profile, 71));
        True(!first.Success && first.LimitReached, "limit is reported");
        Equal(3, first.CandidatesConsidered, "exact candidate bound");
        Equal(string.Join("|", first.Rejections), string.Join("|", second.Rejections), "deterministic rejection order");
    }

    private static void PlanningPreservesSnapshotDigest()
    {
        TerrainSnapshot snapshot = Open(20, 20);
        string before = snapshot.Digest;
        SettlementPlanner.Plan(Input(snapshot, Profile(), 81));
        Equal(before, snapshot.Digest, "snapshot digest after planning");
    }

    private static void P2ProfilesHaveStableDistinctDigests()
    {
        Equal(P2ProfileCatalog.IndustrialTemperate.Digest, P2ProfileCatalog.IndustrialTemperate.Digest, "industrial digest stable");
        NotEqual(P2ProfileCatalog.IndustrialTemperate.Digest, P2ProfileCatalog.TribalTemperate.Digest, "profile digests differ");
    }

    private static void P2HousingExcludesSpecialBeds()
    {
        foreach (PhysicalSettlementProfile profile in P2Profiles())
        {
            Equal(profile.Residents, profile.Facilities.Single(f => f.Kind == FacilityKind.Housing).Capacity, profile.Id + " housing");
            Equal(1, profile.Facilities.Single(f => f.Kind == FacilityKind.Jail).Capacity, profile.Id + " jail");
            Equal(1, profile.Facilities.Single(f => f.Kind == FacilityKind.Medical).Capacity, profile.Id + " medical");
        }
    }

    private static void P2StartupFoodBridgesFirstHarvest()
    {
        foreach (PhysicalSettlementProfile profile in P2Profiles())
            True(profile.StartupNutrition >= profile.RequiredStartupNutrition, profile.Id + " nutrition bridge");
    }

    private static void P2CropDemandIsResidentDriven()
    {
        foreach (PhysicalSettlementProfile profile in P2Profiles())
        {
            Equal(124, profile.RequiredCropCells, profile.Id + " crop cells");
            True(profile.Facilities.Single(f => f.Kind == FacilityKind.Field).Capacity >= profile.RequiredCropCells,
                profile.Id + " field covers demand");
        }
    }

    private static void P2ResourceBudgetsRespectAllowances()
    {
        foreach (PhysicalSettlementProfile profile in P2Profiles())
        {
            True(profile.Budget.IsWithinLimits, profile.Id + " budget");
            True(profile.Budget.ImportedValue <= profile.Budget.ImportValueLimit, profile.Id + " imports bounded");
            True(profile.Budget.InitializedCropValue > 0, profile.Id + " crop value counted");
        }
    }

    private static void P2IndustrialPowerScenariosPass()
    {
        PhysicalSettlementProfile profile = P2ProfileCatalog.IndustrialTemperate;
        True(profile.Has(SettlementCapability.Electricity | SettlementCapability.Refrigeration | SettlementCapability.Batteries), "industrial capabilities");
        True(profile.PowerScenarios.Count == 3 && profile.PowerScenarios.All(s => s.Passes), "all declared scenarios pass");
        Equal(437, profile.PowerScenarios.Single(s => s.Id == "generator-service-buffer").EndingBatteryWattDays, "finite battery discharge");
    }

    private static void P2TribalHasNoElectricSubstitutes()
    {
        PhysicalSettlementProfile profile = P2ProfileCatalog.TribalTemperate;
        True(!profile.Has(SettlementCapability.Electricity | SettlementCapability.Refrigeration), "no electricity/refrigeration");
        True(profile.PowerScenarios.Count == 0, "no power scenarios");
        True(profile.Facilities.Any(f => f.Kind == FacilityKind.Pantry), "pantry present");
        True(!profile.Facilities.Any(f => f.Kind == FacilityKind.Freezer || f.Kind == FacilityKind.Power), "no electric facilities");
    }

    private static void P2StocksAreFiniteAndProfileSpecific()
    {
        PhysicalSettlementProfile industrial = P2ProfileCatalog.IndustrialTemperate;
        PhysicalSettlementProfile tribal = P2ProfileCatalog.TribalTemperate;
        True(industrial.StartupFoodUnits > 0 && industrial.StartupFuelUnits > 0, "industrial finite stocks");
        True(tribal.StartupFoodUnits > 0 && tribal.StartupFuelUnits > 0, "tribal finite stocks");
        NotEqual(industrial.StartupFoodUnits, tribal.StartupFoodUnits, "food unit representation differs");
        True(industrial.WeaponDefs.All(w => w.StartsWith("Gun_")), "industrial firearms");
        True(tribal.WeaponDefs.All(w => !w.StartsWith("Gun_")), "tribal weapons");
    }

    private static IEnumerable<PhysicalSettlementProfile> P2Profiles()
    {
        yield return P2ProfileCatalog.IndustrialTemperate;
        yield return P2ProfileCatalog.TribalTemperate;
    }

    private static void P2IndustrialLayoutIsDeterministicAndValid()
    {
        PhysicalPlanningInput input = P2Input(P2ProfileCatalog.IndustrialTemperate);
        PhysicalPlanningResult first = PhysicalSettlementPlanner.Plan(input);
        PhysicalPlanningResult second = PhysicalSettlementPlanner.Plan(input);
        True(first.Success && second.Success, "industrial plans succeed: " + string.Join("|", first.Rejections));
        Equal(first.Plan.Digest, second.Plan.Digest, "physical digest");
        Equal(0, PhysicalSettlementValidator.Validate(input, first.Plan).Count, "industrial validation");
    }

    private static void P2TribalLayoutExcludesElectricPlacements()
    {
        PhysicalPlanningResult result = PhysicalSettlementPlanner.Plan(P2Input(P2ProfileCatalog.TribalTemperate));
        True(result.Success, "tribal plan succeeds");
        True(!result.Plan.Placements.Any(p => p.Kind == PhysicalPlacementKind.Generator || p.Kind == PhysicalPlacementKind.Battery ||
            p.Kind == PhysicalPlacementKind.Conduit || p.Kind == PhysicalPlacementKind.Cooler || p.Kind == PhysicalPlacementKind.Turret ||
            p.DefName.StartsWith("Electric")), "no electric placement");
        True(result.Plan.Facilities.Any(f => f.Kind == FacilityKind.Pantry), "tribal pantry");
    }

    private static void P2LayoutRealizesEveryFacilityDemand()
    {
        foreach (PhysicalSettlementProfile profile in P2Profiles())
        {
            PhysicalSettlementPlan plan = PhysicalSettlementPlanner.Plan(P2Input(profile)).Plan;
            foreach (FacilityDemand demand in profile.Facilities)
                True(plan.Facilities.Any(f => f.Kind == demand.Kind && f.Capacity >= demand.Capacity), profile.Id + " " + demand.Kind);
            Equal(4, plan.Placements.Count(p => p.Kind == PhysicalPlacementKind.OrdinaryBed), profile.Id + " ordinary beds");
        }
    }

    private static void P2LayoutReservesLegalCropCapacity()
    {
        PhysicalPlanningInput input = P2Input(P2ProfileCatalog.IndustrialTemperate);
        PhysicalSettlementPlan plan = PhysicalSettlementPlanner.Plan(input).Plan;
        Equal(130, plan.FieldCells.Count, "field cell count");
        True(plan.FieldCells.All(c => input.Terrain[c].Has(TerrainTraits.Fertile)), "all field cells fertile");
        True(plan.FieldCells.Count >= input.Profile.RequiredCropCells, "field meets 124-cell demand");
    }

    private static void P2CoolerHasExteriorExhaust()
    {
        PhysicalSettlementPlan plan = PhysicalSettlementPlanner.Plan(P2Input(P2ProfileCatalog.IndustrialTemperate)).Plan;
        PhysicalPlacement cooler = plan.Placements.Single(p => p.Kind == PhysicalPlacementKind.Cooler);
        PhysicalFacility freezer = plan.Facilities.Single(f => f.Kind == FacilityKind.Freezer);
        GridPoint exhaust = new GridPoint(cooler.Cell.X - 1, cooler.Cell.Z);
        Equal("East", cooler.Rotation, "cold side faces east into freezer");
        True(!freezer.Bounds.Contains(exhaust), "west hot side is exterior");
    }

    private static void P2PowerTerminalsJoinOneConduitTree()
    {
        PhysicalSettlementPlan plan = PhysicalSettlementPlanner.Plan(P2Input(P2ProfileCatalog.IndustrialTemperate)).Plan;
        HashSet<GridPoint> conduits = new HashSet<GridPoint>(plan.Placements.Where(p => p.Kind == PhysicalPlacementKind.Conduit).Select(p => p.Cell));
        True(conduits.Count > 0, "conduits exist");
        foreach (PhysicalPlacement terminal in plan.Placements.Where(p => p.Kind == PhysicalPlacementKind.Battery ||
            p.Kind == PhysicalPlacementKind.Cooler || p.Kind == PhysicalPlacementKind.Turret ||
            (p.Kind == PhysicalPlacementKind.Workbench && p.DefName.StartsWith("Electric"))))
            True(conduits.Contains(terminal.Cell) || OrthogonalNeighbors(terminal.Cell).Any(conduits.Contains), terminal.Kind + " connected");
    }

    private static void P2GatesAndFacilityRoutesAreViable()
    {
        PhysicalSettlementPlan plan = PhysicalSettlementPlanner.Plan(P2Input(P2ProfileCatalog.IndustrialTemperate)).Plan;
        Equal(2, plan.Gates.Count, "attack and retreat gates");
        True(plan.AttackRoute.Count > 0 && plan.RetreatRoute.Count > 0, "both edge routes exist");
        True(plan.AttackRoute.Last().X == 0 || plan.AttackRoute.Last().Z == 0 || plan.AttackRoute.Last().X == 79 || plan.AttackRoute.Last().Z == 79,
            "attack route reaches edge");
        True(plan.RetreatRoute.Last().X == 0 || plan.RetreatRoute.Last().Z == 0 || plan.RetreatRoute.Last().X == 79 || plan.RetreatRoute.Last().Z == 79,
            "retreat route reaches edge");
        HashSet<GridPoint> route = new HashSet<GridPoint>(plan.Logistics);
        foreach (PhysicalFacility facility in plan.Facilities.Where(f => f.Roofed))
            True(OrthogonalNeighbors(facility.Door).Any(route.Contains), facility.Id + " door joins logistics");
    }

    private static void P2LayoutRejectionIsBounded()
    {
        TerrainCell[] cells = OpenCells(80, 80);
        for (int i = 0; i < cells.Length; i++) cells[i] = Cell(cells[i].Traits & ~TerrainTraits.Fertile);
        PhysicalPlanningInput input = new PhysicalPlanningInput(P2ProfileCatalog.IndustrialTemperate,
            new TerrainSnapshot(80, 80, cells, "infertile-v1"), 99, new[] { "BlocksGranite" }, 3);
        PhysicalPlanningResult result = PhysicalSettlementPlanner.Plan(input);
        True(!result.Success && result.LimitReached, "bounded rejection");
        Equal(3, result.CandidatesConsidered, "candidate bound");
        True(result.Rejections.All(r => r.Contains("field_cell_illegal")), "specific field rejection");
    }

    private static void P2LayoutSelectsDeclaredLocalMaterial()
    {
        PhysicalPlanningInput input = new PhysicalPlanningInput(P2ProfileCatalog.IndustrialTemperate,
            FertileOpen(80, 80), 101, new[] { "WoodLog", "BlocksGranite" }, 6400);
        PhysicalSettlementPlan plan = PhysicalSettlementPlanner.Plan(input).Plan;
        Equal("BlocksGranite", plan.LocalWallStuff, "stable sorted local selection");
        True(input.LocalStuffDefs.Contains(plan.LocalWallStuff), "selected material is local");
    }

    private static PhysicalPlanningInput P2Input(PhysicalSettlementProfile profile) =>
        new PhysicalPlanningInput(profile, FertileOpen(80, 80), 91, new[] { "BlocksGranite" }, 6400);

    private static TerrainSnapshot FertileOpen(int width, int height)
    {
        TerrainCell[] cells = OpenCells(width, height);
        for (int i = 0; i < cells.Length; i++) cells[i] = Cell(cells[i].Traits | TerrainTraits.Fertile);
        return new TerrainSnapshot(width, height, cells, "fertile-synthetic-v1");
    }

    private static IEnumerable<GridPoint> OrthogonalNeighbors(GridPoint point)
    {
        yield return new GridPoint(point.X, point.Z - 1);
        yield return new GridPoint(point.X - 1, point.Z);
        yield return new GridPoint(point.X + 1, point.Z);
        yield return new GridPoint(point.X, point.Z + 1);
    }

    private static SettlementProfile Profile(int candidates = 128) =>
        new SettlementProfile("diagnostic", 1, 7, 7, 20, candidates, 512);

    private static PlannerInput Input(TerrainSnapshot snapshot, SettlementProfile profile, ulong seed) =>
        new PlannerInput(profile, snapshot, seed);

    private static TerrainSnapshot Open(int width, int height) =>
        new TerrainSnapshot(width, height, OpenCells(width, height), "synthetic-v1");

    private static TerrainCell[] OpenCells(int width, int height)
    {
        TerrainCell[] cells = new TerrainCell[width * height];
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                TerrainTraits traits = TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.SupportsHeavy;
                if (x == 0 || z == 0 || x == width - 1 || z == height - 1) traits |= TerrainTraits.MapEdge;
                cells[z * width + x] = Cell(traits);
            }
        return cells;
    }

    private static void FillRect(TerrainCell[] cells, int mapWidth, int minX, int minZ, int width, int height, bool edgeAware)
    {
        int mapHeight = cells.Length / mapWidth;
        for (int z = minZ; z < minZ + height; z++)
            for (int x = minX; x < minX + width; x++)
            {
                TerrainTraits traits = TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.SupportsHeavy;
                if (edgeAware && (x == 0 || z == 0 || x == mapWidth - 1 || z == mapHeight - 1)) traits |= TerrainTraits.MapEdge;
                cells[z * mapWidth + x] = Cell(traits);
            }
    }

    private static TerrainCell Cell(TerrainTraits traits) => new TerrainCell(traits, 1, 0);
    private static bool IsBuildable(TerrainCell cell) => cell.Has(TerrainTraits.Standable | TerrainTraits.Buildable);

    private static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(message + " expected=" + expected + " actual=" + actual);
    }

    private static void NotEqual<T>(T first, T second, string message)
    {
        if (EqualityComparer<T>.Default.Equals(first, second)) throw new InvalidOperationException(message);
    }
}
