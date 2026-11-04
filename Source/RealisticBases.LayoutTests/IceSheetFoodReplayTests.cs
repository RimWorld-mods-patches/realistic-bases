using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    // Requested IceOasis tile 33970 actually logged Landforms: None. This is
    // frozen-map planner coverage, not confirmed Geological Landforms coverage.
    private static void IceSheet33970FoodFallback() => IceSheet33970FoodFallback(null);
    private static void IceSheet33970NoGrowingSeason() => IceSheet33970FoodFallback(0);

    private static void IceSheet33970FoodFallback(int? growingDays)
    {
        var terrain = CapturedTerrainWithDefinitionIds("ice-sheet-33970-terrain.rle",200,
            "1.7.4990 rev1069;generator=RB_P0_Base_Faction");
        const string terrainDigest = "8EEF9DB44A04E07F";
        Equal(terrainDigest, terrain.Digest, "all captured terrain traits and definition IDs are preserved");
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        var requirements = new LayoutRequirements(residents: 7, capabilities: (SettlementCapability)255, housing: (HousingStandard)0, climate: new LayoutClimate(-43, -2, outdoorGrowingDays: growingDays), mortarBiasPercent: 50, excavationBudgetCells: 550, fieldCells: 216, capacityFloors: new KeyValuePair<LayoutRoomKind,int>[] {new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)2,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)3,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)4,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)5,24),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)7,4),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)8,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)9,24)}, stage: stage, importValueLimit: 67375, equipmentValueLimit: 20160, stockpileRoomCapacityTarget: 120, compactQuestCamp: false, defenseReserveWatts: 4000, logisticsUtilityValueLimit: 300, saleStorageStacks: 108, salePerishableStacks: 19, retainedStorageStacks: 0, retainedPerishableStacks: 0, questArtillery: 0, defenseImportReserveValue: 60500, baseImportValueLimit: 6875);
        var budget = new LayoutSearchBudget(maxSites: 10, attemptsPerSite: 3, maxEvaluations: 1225000, maxCandidatesPerRoom: 3000, maxRepairsPerAttempt: 14, maxRouteNodes: 40000);
        var resources = new LayoutLocalResources(localWoodYield: 0, localStoneStuffs: new string[] {"BlocksGranite","BlocksLimestone"}, riverCells: new GridPoint[] {}, steamGeyserCells: new GridPoint[] {new GridPoint(108, 44),new GridPoint(52, 47),new GridPoint(91, 167)});
        var historicalCatalog = RuntimeFlatCatalogFixture.Build();
        var historical = new ProceduralLayoutInput(requirements, terrain, 0x0A8969492FFA0F47UL, historicalCatalog,
            new LayoutFortificationReservation(1), budget, resources);
        True(!LayoutFoodSupplyPlanner.CanImport(historical), "the old test catalog lacks the food-capture facts");
        var rejected = ProceduralLayoutPlanner.Plan(historical, requireJointSite:true);
        Equal(growingDays == 0 ? "no_legal_food_supply_for_zero_growing_season" : "no_feasible_field_compound_pair", rejected.RejectionCode,
            "the captured map cannot fit its original full natural-field demand");

        var catalog = CapturedFoodGrowthCatalog(historicalCatalog);
        var input = new ProceduralLayoutInput(requirements,terrain,historical.Seed,catalog,
            historical.Fortification,budget,resources);
        var result = ProceduralLayoutPlanner.Plan(input,requireJointSite:true);
        Accepted(result);
        var layout = result.Layout;
        if (growingDays == 0)
        {
            True(layout.HasOmittedMainField && layout.FieldWall == null,
                "a native zero-day growing period omits the entire outdoor field");
            Equal(0,layout.FoodSupply.NaturalCells,"unusable outdoor crops receive no food credit");
            True(!layout.JointSite.FieldReservation.Any() && !layout.JointSite.AllPostClearance.Any(),
                "the omitted field leaves no walls, lanes, gates or defender reservation");
            Equal(0,layout.BonusFields.Count,"unusable courtyard crops are also omitted");
        }

        Equal(7, input.Requirements.Residents, "the entire funded roster is retained");
        True(layout.FoodSupply != null && layout.FoodSupply.Covered, "the original full nutrition requirement is covered");
        True(layout.FoodSupply.NaturalCells < requirements.FieldCells, "a smaller natural field is allowed");
        True(layout.AllFixtures.Any(f=>f.Kind==LayoutFixtureKind.HydroponicsBasin) &&
            layout.AllFixtures.Any(f=>f.Kind==LayoutFixtureKind.SunLamp), "funded physically feasible hydroponics is used");
        Equal(125,layout.FoodSupply.TotalFoodUnits,"one physical stock funds all eight initial days without future harvest credit");
        True(layout.Materials.OrdinaryImportValue<=requirements.BaseImportValueLimit,"food and infrastructure keep the original allowance");
        True(layout.Power.FuelDays>=8,"grow-room power is funded through the first-harvest horizon");
        True(!result.Stats.BudgetExhausted,"the original evaluation budget is retained");
        True(RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input,layout).Accepted,
            "the completed layout passes the ordinary offline spawn gate");
        Equal(terrainDigest,terrain.Digest,"planning does not mutate the frozen terrain");
        Console.WriteLine("  ice-sheet33970 layout="+layout.Digest+" natural="+layout.FoodSupply.NaturalCells+
            " basins="+layout.AllFixtures.Count(f=>f.Kind==LayoutFixtureKind.HydroponicsBasin)+
            " imports="+layout.Materials.OrdinaryImportValue+"/"+requirements.BaseImportValueLimit+" "+result.Stats);
    }
}
