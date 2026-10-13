using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Run("P5 explicit stock epochs reject empty reads and loaded restock", P5StockTests.Epochs);
        Run("P5 immutable stock conserves allocations and physical stack identities", P5StockTests.Conservation);
        Run("P5 actual offers reserve full legal storage and value without truncation", P5StockTests.Capacity);
        Run("P5 reserve ledger caps re-entry at the unload remainder", P5StockTests.ReserveLedger);
        Run("P5.4 re-entry restores survivors only and conserves carried goods", P5EncounterArchiveTests.Roster);
        Run("P5.4 asset state returns as archived and never refills", P5EncounterArchiveTests.Assets);
        Run("P5.4 revisits pin storage demand and match assets on a changed layout", P5EncounterArchiveTests.Matching);
        Run("P5 food uses nutrition and preserves larger finite reserves", P5ReadinessTests.Food);
        Run("P5 combat aggregates all installed guns without copying shared stock", P5ReadinessTests.Combat);
        Run("P5 outage uses actual load and battery self-discharge without zero-clamp passes", P5ReadinessTests.Outage);
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
        Run("P4 archetype selection is deterministic and preserves terrain", P4ArchetypeSelectionIsDeterministic);
        Run("P4 terrain analysis classifies foothill mountain and lakeshore", P4TerrainAnalysisClassifiesLandforms);
        Run("P4 inaccessible islands reject without assumed bridges", P4InaccessibleIslandRejects);
        Run("P4 terrain candidate search is bounded", P4TerrainCandidateSearchIsBounded);
        Run("P4 mountain plan owns only roofed rock excavation", P4MountainPlanOwnsOnlyRoofedRock);
        Run("P4 retained thick roofs are explicit excavation ledger entries", P4RetainedThickRoofLedger);
        Run("P4 non-mountain plans have no landform mutation", P4NonMountainPlansHaveNoLandformMutation);
        Run("P4 lakeshore plan preserves water and dry routes", P4LakeshorePlanPreservesWaterAndDryRoutes);
        Run("P4 final navigation rejects an inset wall that blocks the circulation root", P4ConnectivityRejectsBlockedInsetWall);
        Run("P4 gatehouse layers are serially connected and reject offset or blocked crossings", P4GatehouseLayersValidateConnectivity);
        Run("P4 fields are reachable from gate approaches across dry terrain", P4FieldsRequireDryGateAccess);
        Run("P3 assignments are deterministic and exclusive", P3AssignmentsAreDeterministicAndExclusive);
        Run("P3 assignment is weapon and post aware", P3AssignmentIsWeaponAndPostAware);
        Run("P3 pursuit validates destination and full route", P3PursuitValidatesDestinationAndFullRoute);
        Run("P3 breach response remains local", P3BreachResponseRemainsLocal);
        Run("P3 intact perimeter prioritizes an attacked corner front", P3IntactPerimeterPrioritizesFront);
        Run("P3 sniper guard posts require long-range defenders", P3SniperGuardRequiresRange);
        Run("P3 safe needs relief preserves minimum staffing", P3SafeNeedsReliefPreservesMinimumStaffing);
        Run("P5 unused post options cannot deadlock a hungry funded roster", P5ReliefUsesRealRoster);
        Run("P3 invalid outer posts trigger inner fallback", P3InvalidOuterPostsTriggerInnerFallback);
        Run("P3 valid assignments resist oscillation", P3ValidAssignmentsResistOscillation);
        Run("P3 routine reserves a support worker", P3RoutineReservesSupportWorker);
        Run("P5 a wounded roster gets a support worker who can tend", P5WoundedRosterGetsTendCapableSupport);
        Run("P5B a hostile arrival without action keeps a watch", P5BGarrisonTests.WatchWithoutAction);
        Run("P5B the first hostile action alarms the whole pack", P5BGarrisonTests.FirstShotAlarmsThePack);
        Run("P5B the pack follows the attacked side after the dwell", P5BGarrisonTests.RedeployFollowsTheAttack);
        Run("P5B sleepers: half the off-duty, at least a third of the pack", P5BGarrisonTests.SleepRotationIsCapped);
        Run("P5B nobody is relieved to sleep under assault", P5BGarrisonTests.NoRestReliefUnderAssault);
        Run("P5B the watch and sleep rotation sustains a garrison for three days", P5BGarrisonTests.RestRotationSustainsWatch);
        Run("P5B the assault latch falls back to watch after an hour", P5BGarrisonTests.AssaultLatch);
        Run("P3 wall damage outranks a decoy", P3WallDamageOutranksDecoy);
        Run("P3 breached wall and threat memory rank deterministically", P3WallBreachAndMemory);
        Run("P3 wall-threat assignment requires an actual firing lane", P3WallThreatUsesFiringLane);
        Run("P3 funded mortar and mobile reserve staffing remains exclusive", P3FundedReserveAndMortar);
        Run("P3 fills distinct inner gate posts before idling extra defenders", P3InnerGatePostsBeforeIdleReserve);
        Run("P3 reserves a mortar post before earlier pawn ids are assigned", P3MortarPostReservation);
        Run("P4E unique multiple crews survive gun and operator loss", P4EMultipleCrews);
        Run("P3 reinforcement audits the whole route under fire", P3ReinforcementRouteUnderFire);
        Run("P3 failed service defers same-tick retries without blocking other work", P3ServiceRetryBackoff);
        Run("P3 service detours follow declared corridors and cannot return a stationary waypoint", P3ServiceDetour);
        Run("P3 field penetration holds compound entrances before inner defeat", P3FieldToCompoundFallback);
        Run("P3 industrial encounter plan is deterministic and valid", P3IndustrialEncounterPlanIsDeterministicAndValid);
        Run("P3 posts avoid opaque walls and cover approaches", P3PostsAvoidOpaqueWallsAndCoverApproaches);
        Run("P3 mortar court is unroofed reachable and supplied", P3MortarCourtIsUnroofedReachableAndSupplied);
        Run("P3 combat stock is operating reserve only", P3CombatStockIsOperatingReserveOnly);
        Run("P3 mortar adds no electrical load", P3MortarAddsNoElectricalLoad);
        Run("P3 tribal encounter excludes mortar content", P3TribalEncounterExcludesMortarContent);
        Run("P3 geometry derives gate and perimeter posts without compound offsets", P3GeometryDerivesPosts);
        Run("P3 geometry rejects disconnected gate endpoints and missing court", P3GeometryRejectsInvalidInputs);
        Console.WriteLine("P1/P2/P3/P4 pure tests: " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures);
        return failures == 0 ? 0 : 1;
    }

    private static void P3ServiceDetour()
    {
        GridPoint start = new GridPoint(0, 0), finish = new GridPoint(6, 0);
        var corridor = new List<GridPoint>();
        for (int z = 0; z <= 5; z++) corridor.Add(new GridPoint(0, z));
        for (int x = 1; x <= 6; x++) corridor.Add(new GridPoint(x, 5));
        for (int z = 4; z >= 0; z--) corridor.Add(new GridPoint(6, z));
        Func<GridPoint, GridPoint, IReadOnlyList<GridPoint>> native = (a, b) =>
        {
            var path = new List<GridPoint> { a };
            while (!a.Equals(b))
            {
                a = new GridPoint(a.X + Math.Sign(b.X - a.X), a.Z + Math.Sign(b.Z - a.Z)); path.Add(a);
            }
            return path;
        };
        Equal("outside_defended_area", P3DefenderRoute.Audit(native(start, finish), corridor, corridor,
            new GridPoint[0], new GridPoint[0]).Reason, "native shortcut leaves U-shaped corridor");
        Equal(new GridPoint(0, 5), P3DefenderRoute.ServiceDetour(start, new[] { finish }, c => true,
            native, corridor, corridor, new GridPoint[0], new GridPoint[0]).Value, "legal first bend is selected");
        True(!P3DefenderRoute.ServiceDetour(start, new[] { start }, c => true, native,
            corridor, corridor, new GridPoint[0], new GridPoint[0]).HasValue, "zero-distance loop refused");
        True(!P3DefenderRoute.ServiceDetour(start, new[] { finish }, c => !c.Equals(new GridPoint(3, 5)),
            native, corridor, corridor, new GridPoint[0], new GridPoint[0]).HasValue, "blocked corridor cannot be traversed");
        True(!P3DefenderRoute.ServiceDetour(start, new[] { finish }, c => true, native,
            corridor, new GridPoint[0], new GridPoint[0], new GridPoint[0]).HasValue, "aggregate exterior bound remains enforced");
        True(P3DefenderRoute.ServiceDetour(start, new[] { finish }, c => true, native,
            corridor, new GridPoint[0], new GridPoint[0], new GridPoint[0],
            P3DefenderRoute.MaximumServiceExteriorCells).HasValue, "finite service detour may wrap a bastion corner");
        var longService = Enumerable.Range(0, 33).Select(x => new GridPoint(x, 0)).ToArray();
        True(!P3DefenderRoute.ServiceDetour(start, new[] { longService.Last() }, c => true, native,
            longService, new GridPoint[0], new GridPoint[0], new GridPoint[0],
            P3DefenderRoute.MaximumServiceExteriorCells).HasValue, "service exterior has its own hard limit");
        True(!P3DefenderRoute.ServiceDetour(start, new[] { finish }, c => true, native,
            corridor, corridor, new[] { new GridPoint(3, 2) }, new GridPoint[0]).HasValue,
            "aggregate exposure bound remains enforced");
        GridPoint current = start;
        int moves = 0;
        while (!current.Equals(finish) && moves++ < corridor.Count)
        {
            var next = P3DefenderRoute.ServiceDetour(current, new[] { finish }, c => true, native,
                corridor, corridor, new GridPoint[0], new GridPoint[0]);
            True(next.HasValue && !next.Value.Equals(current), "every resumed leg progresses");
            current = next.Value;
        }
        Equal(finish, current, "successive bounded waypoints reach service target");
    }

    private static void P3ServiceRetryBackoff()
    {
        var retry = new P3ServiceRetry();
        True(retry.Allowed(1, 2, 100), "first attempt permitted");
        retry.Failed(1, 2, 100);
        for (int i = 0; i < 20; i++) True(!retry.Allowed(1, 2, 100), "same-tick restart blocked");
        True(!retry.Allowed(1, 2, 399), "bounded backoff retained");
        True(retry.Allowed(1, 2, 400), "retry after finite delay");
        True(retry.Allowed(1, 3, 100), "other target available");
        True(retry.Allowed(3, 2, 100), "other worker available");
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
        Equal("West", cooler.Rotation, "cold side faces east into freezer");
        True(!freezer.Bounds.Contains(exhaust), "west hot side is exterior");
    }

    private static void P2PowerTerminalsJoinOneConduitTree()
    {
        PhysicalSettlementPlan plan = PhysicalSettlementPlanner.Plan(P2Input(P2ProfileCatalog.IndustrialTemperate)).Plan;
        HashSet<GridPoint> conduits = new HashSet<GridPoint>(plan.Placements.Where(p => p.Kind == PhysicalPlacementKind.Conduit).Select(p => p.Cell));
        True(conduits.Count > 0, "conduits exist");
        GridPoint generator = plan.Placements.Single(p => p.Kind == PhysicalPlacementKind.Generator).Cell;
        GridPoint battery = plan.Placements.Single(p => p.Kind == PhysicalPlacementKind.Battery).Cell;
        GridPoint[] transmitterFootprints = { generator, new GridPoint(generator.X + 1, generator.Z),
            new GridPoint(generator.X, generator.Z + 1), new GridPoint(generator.X + 1, generator.Z + 1),
            battery, new GridPoint(battery.X, battery.Z + 1) };
        True(!transmitterFootprints.Any(conduits.Contains), "conduits do not overlap transmitter footprints");
        True(transmitterFootprints.Take(4).Any(c => OrthogonalNeighbors(c).Any(conduits.Contains)), "generator connected");
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

    private static void P4ArchetypeSelectionIsDeterministic()
    {
        TerrainSnapshot snapshot = FertileOpen(80, 80);
        string before = snapshot.Digest;
        TerrainArchetypeInput input = new TerrainArchetypeInput(P2ProfileCatalog.IndustrialTemperate, snapshot, 1001, 16);
        TerrainArchetypeResult first = TerrainArchetypePlanner.Plan(input);
        TerrainArchetypeResult second = TerrainArchetypePlanner.Plan(input);
        True(first.Success && second.Success, "open land accepts");
        Equal(TerrainArchetype.Flat, first.Candidate.Archetype, "open land is flat");
        Equal(first.Candidate.Archetype, second.Candidate.Archetype, "stable archetype");
        Equal(first.Candidate.Anchor, second.Candidate.Anchor, "stable anchor");
        Equal(before, snapshot.Digest, "analysis preserves snapshot");
    }

    private static void P4TerrainAnalysisClassifiesLandforms()
    {
        TerrainCell[] foothillCells = FertileOpenCells(80, 80);
        for (int z = 20; z < 26; z++) for (int x = 20; x < 26; x++)
            foothillCells[z * 80 + x] = Cell(foothillCells[z * 80 + x].Traits | TerrainTraits.NaturalRock);
        TerrainArchetypeResult foothill = TerrainArchetypePlanner.Plan(new TerrainArchetypeInput(P2ProfileCatalog.IndustrialTemperate,
            new TerrainSnapshot(80, 80, foothillCells, "foothill-v1"), 1002, 16));
        Equal(TerrainArchetype.Foothill, foothill.Candidate.Archetype, "foothill classification");

        TerrainCell[] mountainCells = FertileOpenCells(80, 80);
        for (int z = 20; z < 26; z++) for (int x = 20; x < 26; x++)
            mountainCells[z * 80 + x] = Cell(mountainCells[z * 80 + x].Traits | TerrainTraits.NaturalRock | TerrainTraits.Roofed);
        TerrainArchetypeResult mountain = TerrainArchetypePlanner.Plan(new TerrainArchetypeInput(P2ProfileCatalog.IndustrialTemperate,
            new TerrainSnapshot(80, 80, mountainCells, "mountain-v1"), 1003, 16));
        Equal(TerrainArchetype.ExcavatedMountain, mountain.Candidate.Archetype, "mountain classification");

        TerrainCell[] shoreCells = FertileOpenCells(80, 80);
        for (int z = 1; z < 79; z++) for (int x = 0; x < 4; x++) shoreCells[z * 80 + x] = Cell(TerrainTraits.Water);
        TerrainArchetypeResult shore = TerrainArchetypePlanner.Plan(new TerrainArchetypeInput(P2ProfileCatalog.IndustrialTemperate,
            new TerrainSnapshot(80, 80, shoreCells, "shore-v1"), 1004, 16));
        Equal(TerrainArchetype.Lakeshore, shore.Candidate.Archetype, "lakeshore classification");
    }

    private static void P4InaccessibleIslandRejects()
    {
        TerrainCell[] cells = Enumerable.Repeat(Cell(TerrainTraits.Water), 80 * 80).ToArray();
        for (int z = 20; z < 60; z++) for (int x = 20; x < 60; x++)
            cells[z * 80 + x] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.Fertile | TerrainTraits.SupportsHeavy);
        TerrainArchetypeResult result = TerrainArchetypePlanner.Plan(new TerrainArchetypeInput(P2ProfileCatalog.IndustrialTemperate,
            new TerrainSnapshot(80, 80, cells, "island-v1"), 1005, 16));
        True(!result.Success, "unapproachable island rejects");
        True(result.Rejections.Any(r => r.Contains("island_requires_verified_approach")), "does not assume a bridge");
    }

    private static void P4TerrainCandidateSearchIsBounded()
    {
        TerrainCell[] cells = Enumerable.Repeat(Cell(TerrainTraits.Water), 80 * 80).ToArray();
        for (int z = 0; z < 20; z++) for (int x = 0; x < 20; x++)
            cells[z * 80 + x] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.Fertile | TerrainTraits.SupportsHeavy | TerrainTraits.MapEdge);
        for (int z = 40; z < 50; z++) for (int x = 40; x < 50; x++)
            cells[z * 80 + x] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.Fertile | TerrainTraits.SupportsHeavy);
        TerrainArchetypeResult result = TerrainArchetypePlanner.Plan(new TerrainArchetypeInput(P2ProfileCatalog.IndustrialTemperate,
            new TerrainSnapshot(80, 80, cells, "tiny-edge-v1"), 1006, 1));
        True(!result.Success && result.LimitReached, "bounded terrain rejection");
        Equal(1, result.CandidatesConsidered, "one candidate considered");
        True(result.Rejections.Any(r => r.Contains("insufficient_buildable_land")), "specific rejection");
    }

    private static void P4MountainPlanOwnsOnlyRoofedRock()
    {
        TerrainCell[] cells = FertileOpenCells(80, 80);
        for (int z = 20; z < 26; z++) for (int x = 20; x < 26; x++)
            cells[z * 80 + x] = Cell(cells[z * 80 + x].Traits | TerrainTraits.NaturalRock | TerrainTraits.Roofed);
        TerrainSnapshot terrain = new TerrainSnapshot(80, 80, cells, "mountain-layout-v1");
        PhysicalPlanningInput input = new PhysicalPlanningInput(P2ProfileCatalog.IndustrialTemperate, terrain,
            91, new[] { "BlocksGranite" }, 6400, TerrainArchetype.ExcavatedMountain);
        PhysicalPlanningResult result = PhysicalSettlementPlanner.Plan(input);
        True(result.Success, "mountain plan accepts a bounded excavation");
        TerrainMutationFootprint footprint = result.Plan.TerrainMutations;
        Equal(TerrainArchetype.ExcavatedMountain, result.Plan.Archetype, "archetype retained");
        True(footprint.ExcavationCells.Count > 0 && footprint.ExcavationCells.Count <= 36,
            "only the selected portion of the explicit roofed rock patch is excavated");
        True(footprint.ExcavationCells.All(c => terrain[c].Has(TerrainTraits.NaturalRock | TerrainTraits.Roofed)), "excavation source legal");
        True(footprint.RoofClearanceCells.SequenceEqual(footprint.ExcavationCells), "every roofed excavation owns its roof change");
        True(PhysicalSettlementValidator.Validate(input, result.Plan).Count == 0, "mountain plan validates");
    }

    private static void P4RetainedThickRoofLedger()
    {
        TerrainCell[] cells = FertileOpenCells(40, 40);
        GridPoint roofedRock = new GridPoint(20, 20);
        cells[roofedRock.Z * 40 + roofedRock.X] = Cell(TerrainTraits.NaturalRock | TerrainTraits.Roofed | TerrainTraits.ThickRoof);
        TerrainSnapshot terrain = new TerrainSnapshot(40, 40, cells, "retained-roof-v1");
        TerrainMutationFootprint retained = new TerrainMutationFootprint(new[] { roofedRock }, Enumerable.Empty<GridPoint>(),
            Enumerable.Empty<GridPoint>(), Enumerable.Empty<GridPoint>(), null, new[] { roofedRock });
        True(TerrainMutationPlanner.Validate(TerrainArchetype.ExcavatedMountain, terrain, retained).Count == 0,
            "retained roof is a legal alternative to roof clearance");
        TerrainMutationFootprint overlap = new TerrainMutationFootprint(new[] { roofedRock }, new[] { roofedRock },
            Enumerable.Empty<GridPoint>(), Enumerable.Empty<GridPoint>(), null, new[] { roofedRock });
        True(TerrainMutationPlanner.Validate(TerrainArchetype.ExcavatedMountain, terrain, overlap).Contains("roof_ledger_overlap"),
            "a roof cannot be both cleared and retained");
    }

    private static void P4NonMountainPlansHaveNoLandformMutation()
    {
        PhysicalPlanningInput input = P2Input(P2ProfileCatalog.IndustrialTemperate);
        PhysicalSettlementPlan plan = PhysicalSettlementPlanner.Plan(input).Plan;
        True(!plan.TerrainMutations.HasOwnedLandformMutation, "flat plan does not claim terrain ownership");
        True(plan.TerrainMutations.PreservedNaturalRockCells.Count == 0, "flat synthetic terrain has no fake natural boundary");
    }

    private static void P4LakeshorePlanPreservesWaterAndDryRoutes()
    {
        TerrainCell[] cells = FertileOpenCells(80, 80);
        for (int z = 0; z < 80; z++) cells[z * 80 + 18] = Cell(TerrainTraits.Water);
        TerrainSnapshot terrain = new TerrainSnapshot(80, 80, cells, "lakeshore-layout-v1");
        PhysicalPlanningInput input = new PhysicalPlanningInput(P2ProfileCatalog.IndustrialTemperate, terrain,
            91, new[] { "BlocksGranite" }, 6400, TerrainArchetype.Lakeshore);
        PhysicalPlanningResult result = PhysicalSettlementPlanner.Plan(input);
        True(result.Success, "lakeshore has a dry legal layout");
        True(result.Plan.TerrainMutations.PreservedWaterCells.Count >= 8, "water boundary is explicit");
        True(result.Plan.AttackRoute.All(c => !terrain[c].Has(TerrainTraits.Water)), "attack route does not assume water travel");
        True(result.Plan.RetreatRoute.All(c => !terrain[c].Has(TerrainTraits.Water)), "retreat route does not assume water travel");
        True(PhysicalSettlementValidator.Validate(input, result.Plan).Count == 0, "lakeshore plan validates");
    }

    private static void P4ConnectivityRejectsBlockedInsetWall()
    {
        PhysicalPlanningInput input = P2Input(P2ProfileCatalog.IndustrialTemperate);
        PhysicalSettlementPlan basePlan = PhysicalSettlementPlanner.Plan(input).Plan;
        GridRect inset = new GridRect(basePlan.Compound.MinX + 1, basePlan.Compound.MinZ + 1,
            basePlan.Compound.Width - 2, basePlan.Compound.Height - 2);
        List<GridPoint> blockedInset = inset.Points().Where(p => p.X == inset.MinX || p.X == inset.MaxX ||
            p.Z == inset.MinZ || p.Z == inset.MaxZ).ToList();
        PhysicalSettlementPlan invalid = new PhysicalSettlementPlan(basePlan.InputDigest, basePlan.ProfileDigest,
            basePlan.Compound, basePlan.Field, basePlan.Gates, basePlan.Perimeter, Enumerable.Empty<GridPoint>(), blockedInset,
            2, basePlan.Logistics, basePlan.FieldCells, basePlan.AttackRoute, basePlan.RetreatRoute, basePlan.Facilities,
            basePlan.Placements, basePlan.LocalWallStuff, basePlan.CandidatesConsidered, basePlan.CandidateLimit,
            basePlan.Archetype, basePlan.TerrainMutations);
        True(P4Connectivity.Validate(input.Terrain, invalid).Any(e => e == "public_root_blocked"),
            "inset wall is rejected by final resident cell graph");
    }

    private static void P4GatehouseLayersValidateConnectivity()
    {
        for (int layers = 1; layers <= 3; layers++)
        {
            PhysicalPlanningInput input = new PhysicalPlanningInput(P2ProfileCatalog.IndustrialTemperate,
                FertileOpen(100, 100), 91, new[] { "BlocksGranite" }, 6400, TerrainArchetype.Flat, layers);
            PhysicalPlanningResult result = PhysicalSettlementPlanner.Plan(input);
            True(result.Success, "wall layer " + layers + " has a legal plan");
            PhysicalSettlementPlan plan = result.Plan;
            Equal(2 * (layers - 1), plan.ReinforcementGates.Count, "every added layer has north and south doors");
            Equal(8 * (layers - 1), plan.ReinforcementPerimeter.Count, "gatehouse bands reserve four walls per crossing");
            True(P4Connectivity.Validate(input.Terrain, plan).Count == 0, "serial gatehouse crossings reach the rooted public graph");
            if (layers != 2) continue;

            GridPoint vestibule = new GridPoint(plan.Gates[0].X, plan.Gates[0].Z - 1);
            PhysicalSettlementPlan blocked = new PhysicalSettlementPlan(plan.InputDigest, plan.ProfileDigest,
                plan.Compound, plan.Field, plan.Gates, plan.Perimeter, plan.ReinforcementGates,
                plan.ReinforcementPerimeter.Concat(new[] { vestibule }), plan.WallLayers, plan.Logistics,
                plan.FieldCells, plan.AttackRoute, plan.RetreatRoute, plan.Facilities, plan.Placements,
                plan.LocalWallStuff, plan.CandidatesConsidered, plan.CandidateLimit, plan.Archetype, plan.TerrainMutations);
            True(P4Connectivity.Validate(input.Terrain, blocked).Any(e => e.StartsWith("gatehouse_crossing_disconnected_")),
                "blocked vestibule rejects a disconnected serial crossing");

            GridPoint shifted = new GridPoint(plan.ReinforcementGates[0].X + 1, plan.ReinforcementGates[0].Z);
            PhysicalSettlementPlan offset = new PhysicalSettlementPlan(plan.InputDigest, plan.ProfileDigest,
                plan.Compound, plan.Field, plan.Gates, plan.Perimeter,
                new[] { shifted, plan.ReinforcementGates[1] }, plan.ReinforcementPerimeter, plan.WallLayers,
                plan.Logistics, plan.FieldCells, plan.AttackRoute, plan.RetreatRoute, plan.Facilities, plan.Placements,
                plan.LocalWallStuff, plan.CandidatesConsidered, plan.CandidateLimit, plan.Archetype, plan.TerrainMutations);
            True(P4Connectivity.Validate(input.Terrain, offset).Any(e => e.StartsWith("gatehouse_crossing_disconnected_")),
                "offset gate is not accepted as a crossing");
        }
    }

    private static PhysicalPlanningInput P2Input(PhysicalSettlementProfile profile) =>
        new PhysicalPlanningInput(profile, FertileOpen(80, 80), 91, new[] { "BlocksGranite" }, 6400);

    private static void P3AssignmentsAreDeterministicAndExclusive()
    {
        GarrisonSituation situation = P3Situation();
        GarrisonPlan first = GarrisonDoctrine.Assign(situation);
        GarrisonPlan second = GarrisonDoctrine.Assign(situation);
        Equal(string.Join("|", first.Assignments.Select(AssignmentText)),
            string.Join("|", second.Assignments.Select(AssignmentText)), "deterministic assignments");
        Equal(situation.Defenders.Count, first.Assignments.Count, "one assignment per defender");
        True(first.Assignments.GroupBy(a => a.DefenderId).All(g => g.Count() == 1), "exclusive defender roles");
        True(first.Assignments.Where(a => a.PostId.Length > 0).GroupBy(a => a.PostId).All(g => g.Count() == 1), "exclusive posts");
    }

    private static void P3AssignmentIsWeaponAndPostAware()
    {
        GarrisonPlan plan = GarrisonDoctrine.Assign(P3Situation());
        Equal("north-long", plan.Assignments.Single(a => a.DefenderId == 1).PostId, "rifle takes long post");
        Equal("north-gate", plan.Assignments.Single(a => a.DefenderId == 2).PostId, "melee takes gate post");
        Equal(GarrisonRole.MortarOperator, plan.Assignments.Single(a => a.DefenderId == 3).Role, "operator takes mortar");
    }

    private static void P3PursuitValidatesDestinationAndFullRoute()
    {
        GridRect bounds = new GridRect(10, 10, 8, 8);
        GridPoint origin = new GridPoint(11, 11);
        GridPoint target = new GridPoint(15, 11);
        GridPoint[] safe = { origin, new GridPoint(12, 11), new GridPoint(13, 11), new GridPoint(14, 11), target };
        GridPoint[] leaves = { origin, new GridPoint(10, 11), new GridPoint(9, 11), new GridPoint(10, 11),
            new GridPoint(11, 11), new GridPoint(12, 11), new GridPoint(13, 11), new GridPoint(14, 11), target };
        True(GarrisonDoctrine.CanPursue(origin, target, safe, bounds, 8), "bounded route accepted");
        True(!GarrisonDoctrine.CanPursue(origin, target, leaves, bounds, 12), "route leaving bounds rejected");
        True(!GarrisonDoctrine.CanPursue(origin, new GridPoint(19, 11), safe, bounds, 8), "outside destination rejected");
    }

    private static void P3BreachResponseRemainsLocal()
    {
        GarrisonPlan plan = GarrisonDoctrine.Assign(P3Situation(gateBreached: true, threatenedSector: "north"));
        Equal(GarrisonState.LocalResponse, plan.State, "breach state");
        True(plan.Assignments.Where(a => a.PostId.Length > 0).Take(2).All(a => a.SectorId == "north"),
            "threatened sector receives first compatible defenders");
        True(plan.Assignments.All(a => a.Role != GarrisonRole.Support), "combat response has no routine support assignment");
    }

    private static void P3IntactPerimeterPrioritizesFront()
    {
        List<DefensivePost> posts = P3Posts().Concat(new[] {
            new DefensivePost("shore-corner", "south", new GridPoint(10, 18), 8, 25,
                true, true, true, false, false, true, new[] { "south", "west" }) }).ToList();
        GarrisonPlan plan = GarrisonDoctrine.Assign(P3Situation(posts: posts, gateBreached: false, threatenedSector: "west"));
        Equal(GarrisonState.HoldSectors, plan.State, "an unbreached hostile approach still holds the perimeter");
        Equal("shore-corner", plan.Assignments.Single(a => a.DefenderId == 1).PostId,
            "verified west-facing corner attracts a ranged defender before breach");
        Equal(1, plan.Assignments.Count(a => a.PostId == "shore-corner"),
            "shared two-front position never receives duplicate assignments");
        DefensivePost restored = new DefensivePost("shore-corner", "south", new GridPoint(10, 18), 8, 25,
            true, true, true, false, false, true, "south|west".Split('|'));
        True(restored.CoversSector("west") && restored.CoversSector("south"),
            "existing string save field can round-trip multiple supported fronts");
    }

    private static void P3SniperGuardRequiresRange()
    {
        List<DefensivePost> posts = new List<DefensivePost> {
            new DefensivePost("east-sniper-guard", "east", new GridPoint(20, 10), 35, 44,
                true, true, true, false, false, true),
            new DefensivePost("east-line", "east", new GridPoint(18, 10), 8, 25,
                true, true, true, false, false, true) };
        List<DefenderCandidate> defenders = new List<DefenderCandidate> {
            new DefenderCandidate(1, DefenderWeaponClass.LongRanged, 30, false, false, false, false, false),
            new DefenderCandidate(2, DefenderWeaponClass.LongRanged, 45, false, false, false, false, false) };
        GarrisonPlan plan = GarrisonDoctrine.Assign(P3Situation(defenders: defenders, posts: posts,
            threatenedSector: "east"));
        Equal("east-line", plan.Assignments.Single(a => a.DefenderId == 1).PostId,
            "a shorter-ranged defender cannot occupy the sniper guard position");
        Equal("east-sniper-guard", plan.Assignments.Single(a => a.DefenderId == 2).PostId,
            "the equipped long-range defender takes the turret guard position");
        int savedGuard = P3PostFlags.Encode(posts[0]);
        Equal(35, P3PostFlags.MinimumRange(savedGuard), "sniper eligibility survives the saved post flag");
        Equal(44, P3PostFlags.MaximumRange(savedGuard), "sniper range survives the saved post flag");
        Equal(8, P3PostFlags.MinimumRange(P3PostFlags.SecondaryScreen(savedGuard)),
            "adjacent screened cells accept ordinary shooters without claiming sniper coverage");
        Equal(8, P3PostFlags.MinimumRange(0), "older ordinary post flags retain legacy eligibility");
        Equal(48, P3PostFlags.MaximumRange(0), "older ordinary post flags retain legacy range");
    }

    private static void P3SafeNeedsReliefPreservesMinimumStaffing()
    {
        List<DefenderCandidate> defenders = P3Defenders().ToList();
        defenders[3] = new DefenderCandidate(4, DefenderWeaponClass.ShortRanged, 12, false, true, true, false, false);
        GarrisonPlan safe = GarrisonDoctrine.Assign(P3Situation(defenders: defenders, needsRouteSafe: true));
        Equal(GarrisonRole.NeedsRelief, safe.Assignments.Single(a => a.DefenderId == 4).Role, "safe relief granted");
        GarrisonPlan unsafePlan = GarrisonDoctrine.Assign(P3Situation(defenders: defenders, needsRouteSafe: false));
        True(unsafePlan.Assignments.Single(a => a.DefenderId == 4).Role != GarrisonRole.NeedsRelief, "unsafe route blocks relief");
    }
    private static void P5ReliefUsesRealRoster()
    {
        var hungry = Enumerable.Range(1, 10).Select(id => new DefenderCandidate(id,
            DefenderWeaponClass.LongRanged, 45, false, true, true, false, false)).ToList();
        var manyPosts = Enumerable.Range(0, 40).Select(id => new DefensivePost("p" + id, "east",
            new GridPoint(id, 0), 0, 44, true, true, true, false, false, true)).ToList();
        var plan = GarrisonDoctrine.Assign(P3Situation(defenders: hungry, posts: manyPosts, needsRouteSafe: true));
        Equal(8, plan.Assignments.Count(a => a.Role == GarrisonRole.NeedsRelief), "eight relieved, two real defenders retained");
        var unsafePlan = GarrisonDoctrine.Assign(P3Situation(defenders: hungry, posts: manyPosts, needsRouteSafe: false));
        Equal(0, unsafePlan.Assignments.Count(a => a.Role == GarrisonRole.NeedsRelief), "unsafe relief remains blocked");
        var routine = GarrisonDoctrine.Assign(P3Situation(defenders: hungry, posts: manyPosts, hostileThreat: false));
        Equal(9, routine.Assignments.Count(a => a.Role == GarrisonRole.NeedsRelief), "routine retains one awake rotating watch");
    }

    private static void P3InvalidOuterPostsTriggerInnerFallback()
    {
        List<DefensivePost> posts = P3Posts().Select(p => p.Inner ? p :
            new DefensivePost(p.Id, p.SectorId, p.Cell, p.MinimumRange, p.MaximumRange,
                p.HasCover, p.HasLineOfFire, false, p.Mortar, p.Inner, p.Usable)).ToList();
        GarrisonPlan plan = GarrisonDoctrine.Assign(P3Situation(posts: posts, outerDefenseViable: false));
        Equal(GarrisonState.InnerFallback, plan.State, "fallback state");
        True(plan.Assignments.Any(a => a.PostId == "inner-east"), "valid inner post occupied");
        True(plan.Assignments.All(a => a.PostId.Length == 0 || a.PostId.StartsWith("inner-")), "no invalid outer post retained");
    }

    private static void P3ValidAssignmentsResistOscillation()
    {
        List<DefenderCandidate> defenders = P3Defenders().ToList();
        defenders[0] = new DefenderCandidate(1, DefenderWeaponClass.LongRanged, 30, false, false,
            false, false, false, "south-long", GarrisonRole.RangedPost, 900);
        // No post covers the west, so the attacked side cannot pull the defender off its post.
        GarrisonPlan stable = GarrisonDoctrine.Assign(P3Situation(tick: 1000, defenders: defenders, threatenedSector: "west"));
        Equal("south-long", stable.Assignments.Single(a => a.DefenderId == 1).PostId, "cooldown retains valid post");
        List<DefensivePost> invalid = P3Posts().Select(p => p.Id == "south-long"
            ? new DefensivePost(p.Id, p.SectorId, p.Cell, p.MinimumRange, p.MaximumRange,
                p.HasCover, false, p.RouteWithinBounds, p.Mortar, p.Inner, p.Usable) : p).ToList();
        GarrisonPlan reassigned = GarrisonDoctrine.Assign(P3Situation(tick: 1000, defenders: defenders, posts: invalid));
        True(reassigned.Assignments.Single(a => a.DefenderId == 1).PostId != "south-long", "invalid post immediately replaced");
    }

    private static void P3RoutineReservesSupportWorker()
    {
        GarrisonPlan routine = GarrisonDoctrine.Assign(P3Situation(hostileThreat: false));
        Equal(GarrisonState.Routine, routine.State, "routine state");
        Equal(1, routine.Assignments.Count(a => a.Role == GarrisonRole.Support), "declared routine support count");
        Equal(4, routine.Assignments.Single(a => a.Role == GarrisonRole.Support).DefenderId,
            "support-capable short-range defender selected");
        GarrisonPlan combat = GarrisonDoctrine.Assign(P3Situation(hostileThreat: true));
        True(combat.Assignments.All(a => a.Role != GarrisonRole.Support), "support worker returns to combat");
    }

    // 2026-10-13 live combat: the selected support worker had Caring disabled
    // (backstory), so three wounded residents in bed were never tended.
    private static void P5WoundedRosterGetsTendCapableSupport()
    {
        var defenders = new[]
        {
            new DefenderCandidate(1, DefenderWeaponClass.LongRanged, 30, false, true, false, false, false),
            new DefenderCandidate(2, DefenderWeaponClass.ShortRanged, 12, false, true, false, false, false, canTend: false),
            new DefenderCandidate(3, DefenderWeaponClass.ShortRanged, 16, false, true, false, false, false),
            new DefenderCandidate(4, DefenderWeaponClass.ShortRanged, 14, false, true, false, false, false),
            new DefenderCandidate(5, DefenderWeaponClass.LongRanged, 30, false, true, false, false, true),
        };
        GarrisonPlan wounded = GarrisonDoctrine.Assign(P3Situation(defenders: defenders, hostileThreat: false));
        GarrisonAssignment support = wounded.Assignments.Single(a => a.Role == GarrisonRole.Support);
        True(support.DefenderId != 2, "a worker who cannot tend is not the only support while someone is wounded");
        var healthy = defenders.Select(d => d.Id == 5 ? new DefenderCandidate(5, DefenderWeaponClass.LongRanged, 30, false, true,
            false, false, false) : d).ToArray();
        Equal(2, GarrisonDoctrine.Assign(P3Situation(defenders: healthy, hostileThreat: false)).Assignments
            .Single(a => a.Role == GarrisonRole.Support).DefenderId, "without wounded the usual short-range choice stands");
    }

    private static void P3WallDamageOutranksDecoy()
    {
        P3PerimeterSegment[] segments = {
            new P3PerimeterSegment(new GridPoint(10, 20), "north", P3PerimeterRegion.Compound, false),
            new P3PerimeterSegment(new GridPoint(20, 10), "east", P3PerimeterRegion.Compound, true)
        };
        P3PerimeterObservation[] hits = {
            new P3PerimeterObservation(new GridPoint(10, 20), 900, false, 18)
        };
        GridPoint[] enemies = { new GridPoint(10, 22), new GridPoint(20, 11), new GridPoint(21, 10) };
        IReadOnlyList<P3SectorThreat> ranked = P3PerimeterThreat.Rank(1000, segments, hits, enemies, "east");
        Equal("north", ranked[0].Sector, "real attacked segment beats two undamaging decoys");
        Equal(1, ranked[0].Attacks, "actual wall hit is recorded");
    }

    private static void P3WallBreachAndMemory()
    {
        P3PerimeterSegment[] segments = {
            new P3PerimeterSegment(new GridPoint(10, 20), "north", P3PerimeterRegion.Field, false),
            new P3PerimeterSegment(new GridPoint(20, 10), "east", P3PerimeterRegion.Compound, true)
        };
        P3PerimeterObservation[] hits = {
            new P3PerimeterObservation(new GridPoint(10, 20), 999, true, 0)
        };
        IReadOnlyList<P3SectorThreat> ranked = P3PerimeterThreat.Rank(1000, segments, hits,
            new[] { new GridPoint(20, 11) });
        Equal("north", ranked[0].Sector, "ordinary field wall breach outranks lone hostile at intact gate");
        Equal(1, ranked[0].Breaches, "breach counted");
        IReadOnlyList<P3SectorThreat> expired = P3PerimeterThreat.Rank(1901, segments, hits,
            new[] { new GridPoint(20, 11) });
        Equal("east", expired[0].Sector, "old breach no longer dominates live threat");
        Equal("east", P3PerimeterThreat.Rank(1901, segments, hits,
            new[] { new GridPoint(20, 11) })[0].Sector, "ranking is stable");
    }

    private static void P3WallThreatUsesFiringLane()
    {
        DefenderCandidate[] defenders = {
            new DefenderCandidate(1, DefenderWeaponClass.LongRanged, 30, false, false,
                false, false, false, currentCell: new GridPoint(0, 0))
        };
        DefensivePost[] posts = {
            new DefensivePost("blocked", "north", new GridPoint(0, 0), 8, 25,
                true, true, true, false, false, true),
            new DefensivePost("clear", "north", new GridPoint(0, 2), 8, 25,
                true, true, true, false, false, true)
        };
        GarrisonSituation situation = new GarrisonSituation(1000, defenders, posts,
            true, "north", false, true, true, new GridPoint(0, 0), new GridPoint(1, 1),
            hostileCells: new[] { new GridPoint(0, 20) }, opaqueCells: new[] { new GridPoint(0, 1) });
        Equal("clear", GarrisonDoctrine.Assign(situation).Assignments.Single().PostId,
            "opaque wall blocks the nearer post despite matching sector and cover");
    }

    private static void P3FundedReserveAndMortar()
    {
        DefenderCandidate[] roster = {
            new DefenderCandidate(1, DefenderWeaponClass.LongRanged, 45, true, false, false, false, false),
            new DefenderCandidate(2, DefenderWeaponClass.LongRanged, 30, true, false, false, false, false),
            new DefenderCandidate(3, DefenderWeaponClass.LongRanged, 25, true, false, false, false, false),
            new DefenderCandidate(4, DefenderWeaponClass.ShortRanged, 15, true, false, false, false, false),
            new DefenderCandidate(5, DefenderWeaponClass.ShortRanged, 12, true, false, false, false, false)
        };
        DefensivePost[] posts = {
            new DefensivePost("north-sniper", "north", new GridPoint(10, 10), 35, 44, true, true, true, false, false, true),
            new DefensivePost("north-a", "north", new GridPoint(11, 10), 8, 25, true, true, true, false, false, true),
            new DefensivePost("north-b", "north", new GridPoint(12, 10), 8, 25, true, true, true, false, false, true),
            new DefensivePost("north-c", "north", new GridPoint(13, 10), 8, 25, true, true, true, false, false, true),
            new DefensivePost("mortar", "center", new GridPoint(20, 20), 0, 100, false, true, true, true, false, true)
        };
        GarrisonSituation stage = new GarrisonSituation(1000, roster, posts, true, "north", false, true, true,
            new GridPoint(20, 18), new GridPoint(21, 18), hostileCells: new[] { new GridPoint(12, 20) },
            safeTransferPosts: new[] { "north-a", "north-b", "north-c", "north-sniper" },
            reserveStage: new GridPoint(19, 17));
        GarrisonPlan staged = GarrisonDoctrine.Assign(stage);
        Equal(1, staged.Assignments.Count(a => a.Role == GarrisonRole.MortarOperator), "one funded mortar operator");
        Equal(1, staged.Assignments.Count(a => a.Role == GarrisonRole.GateReserve), "one funded mobile reserve staged");
        Equal(new GridPoint(19, 17), staged.Assignments.Single(a => a.Role == GarrisonRole.GateReserve).Destination,
            "reserve stages near an internal route");
        GarrisonSituation assault = new GarrisonSituation(1000, roster, posts, true, "north", false, true, true,
            new GridPoint(20, 18), new GridPoint(21, 18), safeTransferPosts: new[] { "north-c" },
            threatSeverity: 20);
        GarrisonPlan reinforced = GarrisonDoctrine.Assign(assault);
        Equal(5, reinforced.Assignments.Count, "no unfunded pawn added");
        Equal(1, reinforced.Assignments.Count(a => a.Reason == "mobile_reserve_reinforcement"),
            "observed wall assault activates one reserve");
        True(reinforced.Assignments.Where(a => a.PostId.Length > 0).GroupBy(a => a.PostId)
            .All(g => g.Count() == 1), "mortar, sniper and line posts have exclusive staff");
        GarrisonPlan lost = GarrisonDoctrine.Assign(new GarrisonSituation(1200, roster.Where(d => d.Id != 4),
            posts, true, "north", false, true, true, new GridPoint(20, 18), new GridPoint(21, 18)));
        Equal(4, lost.Assignments.Count, "operator loss does not create a replacement pawn");
        Equal(1, lost.Assignments.Count(a => a.Role == GarrisonRole.MortarOperator),
            "another compatible funded defender takes the mortar");
    }

    private static void P3MortarPostReservation()
    {
        DefenderCandidate[] defenders = {
            new DefenderCandidate(1, DefenderWeaponClass.LongRanged, 30, true, false, false, false, false),
            new DefenderCandidate(2, DefenderWeaponClass.ShortRanged, 12, true, false, false, false, false)
        };
        DefensivePost[] posts = {
            new DefensivePost("mortar", "center", new GridPoint(20, 20), 0, 100,
                false, true, true, true, false, true),
            new DefensivePost("north", "north", new GridPoint(15, 10), 8, 25,
                true, true, true, false, false, true)
        };
        GarrisonPlan plan = GarrisonDoctrine.Assign(new GarrisonSituation(1000, defenders, posts,
            true, "center", false, true, true, new GridPoint(16, 16), new GridPoint(17, 17)));
        Equal("mortar", plan.Assignments.Single(a => a.DefenderId == 2).PostId,
            "funded short-range operator retains reserved mortar post");
        True(plan.Assignments.Single(a => a.DefenderId == 1).PostId != "mortar",
            "earlier pawn id cannot claim the same physical mortar post");
    }

    private static void P4EMultipleCrews()
    {
        TerrainSnapshot clearanceTerrain = FertileOpen(10, 10);
        var clearance = P3DefenderRoute.TransitEnvelope(clearanceTerrain,
            new[] { new GridPoint(4, 4), new GridPoint(4, 5), new GridPoint(5, 5) }, new[] { new GridPoint(5, 4) });
        True(clearance.Contains(new GridPoint(3, 5)) && !clearance.Contains(new GridPoint(5, 4)), "bounded native corner clearance excludes opaque walls");
        True(!clearance.Contains(new GridPoint(2, 5)), "not an unbounded exterior permission");
        GeometryEncounterInput basis = GeometryInput(true);
        ArtilleryPosition[] artillery = Enumerable.Range(0, 3).Select(i => new ArtilleryPosition(i == 0 ? "mortar" : "mortar-" + i,
            new GridPoint(8 + i * 2, 10), new GridPoint(8 + i * 2, 9), new GridPoint(7 + i * 2, 8))).ToArray();
        GeometryEncounterInput Geometry(IEnumerable<ArtilleryPosition> positions) => new GeometryEncounterInput(basis.LayoutDigest,
            P3EncounterProfiles.ForArtillery(3), basis.Enclosure, basis.PerimeterWalls, basis.Gates,
            basis.PublicCirculation, basis.MagazineCandidates, artillery[0].Origin, artillery[0].Operating, positions);
        EncounterPlan geometry = GeometryEncounterPlanner.Plan(Geometry(artillery));
        Equal(3, geometry.Artillery.Count, "stable gun/post/magazine coordinates");
        Equal(3 * P3EncounterProfiles.HeShellsPerMortar, geometry.Supplies.Single(s => s.DefName == "Shell_HighExplosive").Count,
            "one shared ledger: 30 HE per gun, above two shots per hour over six hours");
        Equal(3 * P3EncounterProfiles.IncendiaryShellsPerMortar, geometry.Supplies.Single(s => s.DefName == "Shell_Incendiary").Count,
            "10 incendiary per gun");
        Equal(2, geometry.Supplies.Single(s => s.DefName == "ReinforcedBarrel").Count, "shared barrel reserve");
        Equal(geometry.Digest, GeometryEncounterPlanner.Plan(Geometry(artillery)).Digest, "multi-artillery replay");
        bool duplicateRejected = false;
        try { GeometryEncounterPlanner.Plan(Geometry(new[] { artillery[0], artillery[0], artillery[2] })); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        True(duplicateRejected, "duplicate gun/post/working identity rejected before mutation");
        DefenderCandidate[] roster = Enumerable.Range(1, 10).Select(id => new DefenderCandidate(id,
            id <= 2 ? DefenderWeaponClass.LongRanged : DefenderWeaponClass.ShortRanged,
            id <= 2 ? 45 : 25, true, true, false, false, false)).ToArray();
        DefensivePost[] guns = Enumerable.Range(0, 3).Select(i => new DefensivePost(i == 0 ? "mortar" : "mortar-" + i,
            "center", new GridPoint(20 + i * 4, 20), 0, 100, false, true, true, true, false, true)).ToArray();
        DefensivePost[] direct = Enumerable.Range(0, 6).Select(i => new DefensivePost("line-" + i, "north",
            new GridPoint(10 + i, 10), 8, 25, true, true, true, false, false, true)).ToArray();
        GarrisonPlan Assign(IEnumerable<DefenderCandidate> residents, IEnumerable<DefensivePost> posts) =>
            GarrisonDoctrine.Assign(new GarrisonSituation(1000, residents, posts, true, "north", false, true, true,
                new GridPoint(20, 18), new GridPoint(21, 18)));
        GarrisonPlan initial = Assign(roster, guns.Concat(direct));
        Equal(3, initial.Assignments.Count(a => a.Role == GarrisonRole.MortarOperator), "three funded crews");
        True(initial.Assignments.Where(a => a.Role == GarrisonRole.MortarOperator).All(a => a.DefenderId > 2), "snipers remain direct support");
        True(initial.Assignments.Where(a => a.PostId.Length > 0).GroupBy(a => a.PostId).All(g => g.Count() == 1), "one pawn per post");
        Equal(10, initial.Assignments.Count, "no added residents");
        int lostCrew = initial.Assignments.First(a => a.Role == GarrisonRole.MortarOperator).DefenderId;
        Equal(3, Assign(roster.Where(d => d.Id != lostCrew), guns.Concat(direct)).Assignments.Count(a => a.Role == GarrisonRole.MortarOperator), "reassign from survivors");
        var survivors = roster.Where(d => d.Id != lostCrew).Select(d =>
        {
            var previous = initial.Assignments.Single(a => a.DefenderId == d.Id);
            return new DefenderCandidate(d.Id, d.WeaponClass, d.WeaponRange, true, true, false, false, false,
                previous.PostId, previous.Role, previous.AssignedTick);
        }).ToArray();
        var replaced = Assign(survivors, guns.Concat(direct));
        foreach (var healthy in initial.Assignments.Where(a => a.Role == GarrisonRole.MortarOperator && a.DefenderId != lostCrew))
            Equal(healthy.PostId, replaced.Assignments.Single(a => a.DefenderId == healthy.DefenderId).PostId,
                "a vacant gun does not steal another healthy gun's operator");
        Equal(2, Assign(roster, guns.Skip(1).Concat(direct)).Assignments.Count(a => a.Role == GarrisonRole.MortarOperator), "destroyed gun releases one role");
        Equal(0, Assign(roster, direct).Assignments.Count(a => a.Role == GarrisonRole.MortarOperator), "exhaustion releases all crew");
        Equal(2, Assign(roster.Take(4), guns.Concat(direct)).Assignments.Count(a => a.Role == GarrisonRole.MortarOperator), "casualty staffing leaves two direct defenders");
        var logistics = GarrisonDoctrine.Assign(new GarrisonSituation(1000, roster, guns.Concat(direct), true, "north", false, true, true,
            new GridPoint(20, 18), new GridPoint(21, 18), criticalSupportRequired: true));
        Equal(1, logistics.Assignments.Count(a => a.Role == GarrisonRole.Support), "one existing funded reserve services low ammunition under threat");
        Equal(10, logistics.Assignments.Count, "logistics does not add residents");
        True(logistics.Assignments.Where(a => a.PostId.Length > 0).GroupBy(a => a.PostId).All(g => g.Count() == 1), "logistics cannot double-book guard or mortar posts");
        var smokeRoster = roster.Select(d => new DefenderCandidate(d.Id, d.WeaponClass, d.WeaponRange, true, true,
            false, false, false, hasSmokepop: d.Id == 10)).ToArray();
        var smokeLogistics = GarrisonDoctrine.Assign(new GarrisonSituation(1000, smokeRoster, guns.Concat(direct),
            true, "north", false, true, true, new GridPoint(20, 18), new GridPoint(21, 18), criticalSupportRequired: true));
        Equal(GarrisonRole.Support, smokeLogistics.Assignments.Single(a => a.DefenderId == 10).Role,
            "the existing smoke-equipped reserve supplies critical logistics");
    }

    private static void P3InnerGatePostsBeforeIdleReserve()
    {
        DefenderCandidate[] defenders = Enumerable.Range(1, 8)
            .Select(id => new DefenderCandidate(id, DefenderWeaponClass.ShortRanged, 25,
                false, false, false, false, false)).ToArray();
        DefensivePost[] posts = Enumerable.Range(0, 3)
            .Select(i => new DefensivePost("outer-" + i, "west", new GridPoint(i, 10), 8, 25,
                true, true, true, false, false, true))
            .Concat(Enumerable.Range(0, 4).Select(i => new DefensivePost("gate-" + i,
                "west", new GridPoint(i, 12), 0, 3, false, true, true, false, true, true)))
            .ToArray();
        GarrisonPlan plan = GarrisonDoctrine.Assign(new GarrisonSituation(1000, defenders, posts,
            true, "west", false, true, true, new GridPoint(4, 11), new GridPoint(4, 12),
            reserveStage: new GridPoint(5, 11)));
        Equal(3, plan.Assignments.Count(a => a.PostId.StartsWith("outer-", StringComparison.Ordinal)),
            "all exterior posts are staffed first");
        Equal(4, plan.Assignments.Count(a => a.PostId.StartsWith("gate-", StringComparison.Ordinal)),
            "spare shooters take distinct funded inner gate posts");
        Equal(1, plan.Assignments.Count(a => a.Role == GarrisonRole.GateReserve && a.PostId.Length == 0),
            "only the deliberate mobile reserve remains unposted");
    }

    private static void P3ReinforcementRouteUnderFire()
    {
        GridPoint[] route = Enumerable.Range(0, 16).Select(x => new GridPoint(x, 0)).ToArray();
        GridPoint[] defended = route;
        GridPoint[] interior = route.Take(12).ToArray();
        True(P3DefenderRoute.Audit(route, defended, interior, new GridPoint[0], new GridPoint[0]).Allowed,
            "short exterior crossing on a bounded internal route is legal");
        True(!P3DefenderRoute.Audit(route, defended, route.Take(2).ToArray(),
            new GridPoint[0], new GridPoint[0]).Allowed, "long exterior lap is refused");
        P3DefenderRouteAudit underFire = P3DefenderRoute.Audit(route, defended, interior,
            new[] { new GridPoint(7, 6) }, new GridPoint[0]);
        Equal("route_under_fire", underFire.Reason,
            "covered endpoints do not excuse a long exposed middle route");
        True(!P3DefenderRoute.Audit(route, route.Take(14).ToArray(), interior,
            new GridPoint[0], new GridPoint[0]).Allowed, "path cannot leave defended area");
        GridPoint[] layered = Enumerable.Range(0, 111).Select(x => new GridPoint(x, 0)).ToArray();
        True(P3DefenderRoute.Audit(layered, layered, layered, new GridPoint[0], new GridPoint[0]).Allowed,
            "live assignments accept the same bounded inter-wall detour as generation");
        GridPoint[] excessive = Enumerable.Range(0, P3DefenderRoute.MaximumCells + 1).Select(x => new GridPoint(x, 0)).ToArray();
        Equal("route_length", P3DefenderRoute.Audit(excessive, excessive, excessive,
            new GridPoint[0], new GridPoint[0]).Reason, "long internal routes still have a hard bound");
        GridPoint start = new GridPoint(0, 0), entry = new GridPoint(1, 0), center = new GridPoint(10, 0),
            exit = new GridPoint(10, 10), post = new GridPoint(12, 10);
        var safePairs = new[] { Tuple.Create(start, entry), Tuple.Create(entry, center),
            Tuple.Create(center, exit), Tuple.Create(exit, post) };
        GridPoint[] transferArea = new GridRect(0, 0, 13, 11).Points().ToArray();
        Func<GridPoint, GridPoint, IReadOnlyList<GridPoint>> paths = (from, to) =>
        {
            if (!safePairs.Contains(Tuple.Create(from, to))) return new[] { from, new GridPoint(-1, -1), to };
            var leg = new List<GridPoint> { from };
            GridPoint c = from;
            while (!c.Equals(to))
            {
                c = new GridPoint(c.X + Math.Sign(to.X - c.X), c.Z + Math.Sign(to.Z - c.Z));
                leg.Add(c);
            }
            return leg;
        };
        Equal(entry, P3DefenderRoute.NextWaypoint(start, post, center, exit, new[] { entry, exit }, paths,
            transferArea, transferArea, new GridPoint[0], new GridPoint[0]).Value,
            "cross-front transfer re-enters through its local gate before crossing the compound");
        True(!P3DefenderRoute.NextWaypoint(start, post, center, exit, new[] { entry, exit }, paths,
            transferArea, transferArea, new[] { new GridPoint(5, 3) }, new GridPoint[0]).HasValue,
            "waypoints do not relax exposure limits on the aggregate native path");
    }

    private static void P3FieldToCompoundFallback()
    {
        DefenderCandidate[] defenders = {
            new DefenderCandidate(1, DefenderWeaponClass.LongRanged, 30, false, false, false, false,
                false, "field-outer", GarrisonRole.RangedPost, 950),
            new DefenderCandidate(2, DefenderWeaponClass.Melee, 2, false, false, false, false, false),
            new DefenderCandidate(3, DefenderWeaponClass.ShortRanged, 16, false, false, false, false, false),
            new DefenderCandidate(4, DefenderWeaponClass.ShortRanged, 16, false, false, false, false, false)
        };
        DefensivePost[] posts = {
            new DefensivePost("field-outer", "north", new GridPoint(10, 20), 8, 30,
                true, true, true, false, false, true),
            new DefensivePost("south-outer", "south", new GridPoint(10, 5), 8, 30,
                true, true, true, false, false, true),
            new DefensivePost("gate-0-a", "north", new GridPoint(10, 12), 0, 4,
                true, true, true, false, true, true),
            new DefensivePost("gate-0-b", "north", new GridPoint(11, 12), 0, 4,
                true, true, true, false, true, true)
        };
        GarrisonSituation field = new GarrisonSituation(1000, defenders, posts, true, "north",
            false, true, true, new GridPoint(10, 10), new GridPoint(10, 9),
            fieldPenetrated: true, fieldFallbackPosts: new[] { "gate-0-a", "gate-0-b" });
        GarrisonPlan held = GarrisonDoctrine.Assign(field);
        Equal(GarrisonState.LocalResponse, held.State, "field intrusion prompts local compound defense");
        True(held.Assignments.Any(a => a.PostId.StartsWith("gate-0-")),
            "actual field gate receives a defender even inside reassignment cooldown");
        True(held.Assignments.Any(a => a.PostId == "south-outer" || a.PostId == "field-outer"),
            "field intrusion does not empty every viable exterior position");
        GarrisonPlan compound = GarrisonDoctrine.Assign(new GarrisonSituation(1000, defenders, posts, true,
            "north", true, true, true, new GridPoint(10, 10), new GridPoint(10, 9),
            fieldPenetrated: true, compoundPenetrated: true,
            fieldFallbackPosts: new[] { "gate-0-a", "gate-0-b" }));
        Equal(GarrisonState.InnerFallback, compound.State, "compound intrusion crosses the second defense boundary");
        True(compound.Assignments.All(a => a.PostId.Length == 0 || a.PostId.StartsWith("gate-0-")),
            "compound fallback no longer staffs exterior positions");
    }

    private static GarrisonSituation P3Situation(int tick = 1000, IEnumerable<DefenderCandidate> defenders = null,
        IEnumerable<DefensivePost> posts = null, bool gateBreached = false, string threatenedSector = "north",
        bool outerDefenseViable = true, bool needsRouteSafe = true, bool hostileThreat = true) =>
        new GarrisonSituation(tick, defenders ?? P3Defenders(), posts ?? P3Posts(), hostileThreat, threatenedSector,
            gateBreached, outerDefenseViable, needsRouteSafe, new GridPoint(14, 14), new GridPoint(15, 15));

    private static IEnumerable<DefenderCandidate> P3Defenders()
    {
        yield return new DefenderCandidate(1, DefenderWeaponClass.LongRanged, 30, false, false, false, false, false);
        yield return new DefenderCandidate(2, DefenderWeaponClass.Melee, 2, false, false, false, false, false);
        yield return new DefenderCandidate(3, DefenderWeaponClass.ShortRanged, 16, true, false, false, false, false);
        yield return new DefenderCandidate(4, DefenderWeaponClass.ShortRanged, 12, false, true, false, false, false);
    }

    private static IEnumerable<DefensivePost> P3Posts()
    {
        yield return new DefensivePost("north-long", "north", new GridPoint(12, 10), 18, 36, true, true, true, false, false, true);
        yield return new DefensivePost("south-long", "south", new GridPoint(12, 18), 18, 36, true, true, true, false, false, true);
        yield return new DefensivePost("north-gate", "north", new GridPoint(14, 11), 0, 3, true, true, true, false, false, true);
        yield return new DefensivePost("mortar", "center", new GridPoint(15, 15), 0, 100, false, true, true, true, false, true);
        yield return new DefensivePost("inner-east", "inner", new GridPoint(16, 15), 4, 20, true, true, true, false, true, true);
    }

    private static string AssignmentText(GarrisonAssignment assignment) => assignment.DefenderId + ":" +
        assignment.Role + ":" + assignment.PostId + ":" + assignment.Destination;

    private static void P3IndustrialEncounterPlanIsDeterministicAndValid()
    {
        PhysicalPlanningInput input = P2Input(P2ProfileCatalog.IndustrialTemperate);
        PhysicalSettlementPlan settlement = PhysicalSettlementPlanner.Plan(input).Plan;
        EncounterPlan first = EncounterPlanner.Plan(settlement, input.Profile, input.Terrain);
        EncounterPlan second = EncounterPlanner.Plan(settlement, input.Profile, input.Terrain);
        Equal(first.Digest, second.Digest, "encounter digest");
        Equal(0, EncounterValidator.Validate(settlement, input.Profile, input.Terrain, first).Count, "encounter validation");
    }

    private static void P3PostsAvoidOpaqueWallsAndCoverApproaches()
    {
        PhysicalPlanningInput input = P2Input(P2ProfileCatalog.IndustrialTemperate);
        PhysicalSettlementPlan settlement = PhysicalSettlementPlanner.Plan(input).Plan;
        EncounterPlan encounter = EncounterPlanner.Plan(settlement, input.Profile, input.Terrain);
        True(encounter.Posts.Where(p => !p.Inner && !p.Mortar).All(p => !settlement.Perimeter.Contains(p.Cell)),
            "no post occupies solid wall");
        True(encounter.Posts.Count(p => p.SectorId == "north" && p.MinimumRange > 0) >= 2, "north approach covered");
        True(encounter.Posts.Count(p => p.SectorId == "south" && p.MinimumRange > 0) >= 2, "south approach covered");
        True(encounter.Posts.Any(p => p.SectorId == "west" && p.MinimumRange > 0), "west approach covered");
        True(encounter.Posts.Any(p => p.SectorId == "east" && p.MinimumRange > 0), "east approach covered");
        True(encounter.Posts.All(p => p.IsValid), "all planned posts usable");
    }

    private static void P3MortarCourtIsUnroofedReachableAndSupplied()
    {
        PhysicalPlanningInput input = P2Input(P2ProfileCatalog.IndustrialTemperate);
        PhysicalSettlementPlan settlement = PhysicalSettlementPlanner.Plan(input).Plan;
        EncounterPlan encounter = EncounterPlanner.Plan(settlement, input.Profile, input.Terrain);
        True(encounter.MortarCell.X >= 0 && !input.Terrain[encounter.MortarCell].Has(TerrainTraits.Roofed), "mortar unroofed");
        True(input.Terrain[encounter.MortarInteractionCell].Has(TerrainTraits.Standable), "operator cell standable");
        True(encounter.Posts.Single(p => p.Mortar).Cell.Equals(encounter.MortarInteractionCell), "operator post matches interaction cell");
        Equal(30, encounter.Supplies.Single(s => s.DefName == "Shell_HighExplosive").Count, "30 HE per mortar");
        Equal(10, encounter.Supplies.Single(s => s.DefName == "Shell_Incendiary").Count, "10 incendiary per mortar");
        Equal(2, encounter.Supplies.Single(s => s.DefName == "ReinforcedBarrel").Count, "barrel reserve");
    }

    private static void P3CombatStockIsOperatingReserveOnly()
    {
        PhysicalPlanningInput input = P2Input(P2ProfileCatalog.IndustrialTemperate);
        EncounterPlan encounter = EncounterPlanner.Plan(PhysicalSettlementPlanner.Plan(input).Plan, input.Profile, input.Terrain);
        True(encounter.Supplies.Count > 0 && encounter.Supplies.All(s => s.Purpose == StockPurpose.OperatingReserve),
            "P3 stock is not presented as trade goods");
    }

    private static void P3MortarAddsNoElectricalLoad()
    {
        EncounterProfile profile = P3EncounterProfiles.Industrial;
        Equal(0, profile.AddedContinuousPowerWatts, "manned mortar is unpowered");
        Equal(12, profile.DeclaredMortarShots, "declared firing duty");
    }

    private static void P3TribalEncounterExcludesMortarContent()
    {
        PhysicalPlanningInput input = P2Input(P2ProfileCatalog.TribalTemperate);
        PhysicalSettlementPlan settlement = PhysicalSettlementPlanner.Plan(input).Plan;
        EncounterPlan encounter = EncounterPlanner.Plan(settlement, input.Profile, input.Terrain);
        True(!encounter.Profile.MortarCapable && encounter.MortarCell.X < 0, "no mortar facility");
        True(!encounter.Posts.Any(p => p.Mortar) && encounter.Supplies.Count == 0, "no illegal mortar stocks");
        Equal(0, EncounterValidator.Validate(settlement, input.Profile, input.Terrain, encounter).Count, "tribal encounter validation");
    }

    private static GeometryEncounterInput GeometryInput(bool mortar = false)
    {
        GridRect inside = new GridRect(5, 5, 10, 10);
        GridRect outside = new GridRect(4, 4, 12, 12);
        GridPoint north = new GridPoint(9, 15), south = new GridPoint(9, 4);
        return new GeometryEncounterInput("irregular-layout", mortar ? P3EncounterProfiles.Industrial : P3EncounterProfiles.Tribal,
            inside.Points(), outside.Points().Where(c => !inside.Contains(c) && !c.Equals(north) && !c.Equals(south)),
            new[] { new KeyValuePair<GridPoint, GridPoint>(north, new GridPoint(9, 14)),
                new KeyValuePair<GridPoint, GridPoint>(south, new GridPoint(9, 5)) },
            inside.Points(), new[] { new GridPoint(7, 8) },
            mortar ? (GridPoint?)new GridPoint(10, 10) : null,
            mortar ? (GridPoint?)new GridPoint(10, 9) : null);
    }

    private static void P3GeometryDerivesPosts()
    {
        EncounterPlan first = GeometryEncounterPlanner.Plan(GeometryInput());
        EncounterPlan second = GeometryEncounterPlanner.Plan(GeometryInput());
        Equal(first.Digest, second.Digest, "geometry encounter replay");
        True(first.Posts.Count(p => p.Id.StartsWith("gate-", StringComparison.Ordinal)) >= 2, "gate posts");
        True(first.Posts.Where(p => p.Id.StartsWith("gate-0-", StringComparison.Ordinal))
            .All(p => p.SectorId == "north"), "positive Z gate matches live north threat sector");
        True(first.Posts.Count(p => p.Id.StartsWith("ranged-", StringComparison.Ordinal)) >= 4, "perimeter posts");
        True(first.Posts.Where(p => !p.Mortar).All(p => p.Inner),
            "enclosed inherited posts are fallback, not an exterior firing line");
        True(first.Posts.All(p => first.DefendedCells.Contains(p.Cell)), "posts remain in defended cells");
        Equal(new GridPoint(7, 8), first.MagazineCell, "magazine chosen from supplied geometry");
        EncounterPlan armed = GeometryEncounterPlanner.Plan(GeometryInput(true));
        True(armed.Posts.Any(p => p.Mortar) && armed.Supplies.Count == 3, "optional court and stocks");
    }

    private static void P3GeometryRejectsInvalidInputs()
    {
        GeometryEncounterInput valid = GeometryInput();
        GeometryEncounterInput badGate = new GeometryEncounterInput(valid.LayoutDigest, valid.Profile,
            valid.Enclosure, valid.PerimeterWalls,
            new[] { new KeyValuePair<GridPoint, GridPoint>(new GridPoint(9, 4), new GridPoint(0, 0)) },
            valid.PublicCirculation, valid.MagazineCandidates);
        bool rejected = false;
        try { GeometryEncounterPlanner.Plan(badGate); } catch (InvalidOperationException) { rejected = true; }
        True(rejected, "invalid gate rejected");
        GeometryEncounterInput missingCourt = new GeometryEncounterInput(valid.LayoutDigest, P3EncounterProfiles.Industrial,
            valid.Enclosure, valid.PerimeterWalls, valid.Gates, valid.PublicCirculation, valid.MagazineCandidates);
        rejected = false;
        try { GeometryEncounterPlanner.Plan(missingCourt); } catch (InvalidOperationException) { rejected = true; }
        True(rejected, "industrial mortar profile requires court");
    }

    private static TerrainSnapshot FertileOpen(int width, int height)
    {
        TerrainCell[] cells = FertileOpenCells(width, height);
        return new TerrainSnapshot(width, height, cells, "fertile-synthetic-v1");
    }

    private static TerrainCell[] FertileOpenCells(int width, int height)
    {
        TerrainCell[] cells = OpenCells(width, height);
        for (int i = 0; i < cells.Length; i++) cells[i] = Cell(cells[i].Traits | TerrainTraits.Fertile);
        return cells;
    }

    private static IEnumerable<GridPoint> OrthogonalNeighbors(GridPoint point)
    {
        yield return new GridPoint(point.X, point.Z - 1);
        yield return new GridPoint(point.X - 1, point.Z);
        yield return new GridPoint(point.X + 1, point.Z);
        yield return new GridPoint(point.X, point.Z + 1);
    }

    private static void P4FieldsRequireDryGateAccess()
    {
        const int width = 7;
        const int height = 5;
        TerrainCell[] cells = OpenCells(width, height);
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                TerrainTraits traits = TerrainTraits.Standable | TerrainTraits.Buildable;
                if (x == 3) traits = TerrainTraits.Water;
                if (x >= 4 || (z == 2 && x <= 1)) traits |= TerrainTraits.Fertile;
                cells[z * width + x] = Cell(traits);
            }
        TerrainSnapshot terrain = new TerrainSnapshot(width, height, cells, "field-reachability");
        GridPoint start = new GridPoint(0, 2);
        IReadOnlyList<GridPoint> one = P4FieldPlacement.SelectReachable(terrain,
            new[] { start }, Array.Empty<GridPoint>(), 1);
        Equal(1, one.Count, "one reachable fertile cell");
        Equal(new GridPoint(1, 2), one[0], "uses dry side of water");
        Equal(1, P4FieldPlacement.SelectReachable(terrain,
            new[] { start }, Array.Empty<GridPoint>(), 2).Count, "unreachable shore cannot fill demand");
        Equal(0, P4FieldPlacement.SelectReachable(terrain,
            new[] { start }, new[] { new GridPoint(1, 2) }, 1).Count, "reserved crop cell is excluded");
        Equal(0, P4FieldPlacement.SelectReachable(terrain,
            new[] { new GridPoint(3, 2) }, Array.Empty<GridPoint>(), 1).Count, "water approach is invalid");
        Equal(0, P4FieldPlacement.SelectReachable(terrain,
            new[] { start }, Array.Empty<GridPoint>(), 1, new[] { new GridPoint(1, 2) }).Count,
            "planned roof excludes the only reachable crop cell");
        cells[2 * width + 1] = Cell(TerrainTraits.Standable | TerrainTraits.Buildable |
            TerrainTraits.Fertile | TerrainTraits.Roofed);
        TerrainSnapshot roofed = new TerrainSnapshot(width, height, cells, "field-natural-roof");
        Equal(0, P4FieldPlacement.SelectReachable(roofed,
            new[] { start }, Array.Empty<GridPoint>(), 1).Count,
            "existing roof excludes the only reachable crop cell");
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
