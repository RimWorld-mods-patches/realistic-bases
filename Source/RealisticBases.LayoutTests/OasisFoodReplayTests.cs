using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    // Requested Oasis tile35518 logged GL Landforms: None. This is a captured
    // desert-terrain food regression, not confirmed named-landform coverage.
    private static void Oasis35518FoodFallback()
    {
        var terrain=CapturedTerrainWithDefinitionIds("oasis-candidate-35518-terrain.rle",200,
            "1.7.4990 rev1069;generator=RB_P0_Base_Faction");
        const string terrainDigest="EE173D1088B85086";
        Equal(terrainDigest,terrain.Digest,"preserve every captured terrain/roof record");
        var stage=TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        var requirements = new LayoutRequirements(residents: 11, capabilities: (SettlementCapability)255, housing: (HousingStandard)0, climate: new LayoutClimate(-4, 37), mortarBiasPercent: 50, excavationBudgetCells: 830, fieldCells: 339, capacityFloors: new KeyValuePair<LayoutRoomKind,int>[] {new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)2,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)3,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)4,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)5,24),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)7,4),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)8,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)9,24)}, stage: stage, importValueLimit: 111169, equipmentValueLimit: 33264, stockpileRoomCapacityTarget: 120, compactQuestCamp: false, defenseReserveWatts: 4000, logisticsUtilityValueLimit: 300, saleStorageStacks: 100, salePerishableStacks: 29, retainedStorageStacks: 0, retainedPerishableStacks: 0, questArtillery: 0, defenseImportReserveValue: 99825, baseImportValueLimit: 11344);
        var budget = new LayoutSearchBudget(maxSites: 10, attemptsPerSite: 3, maxEvaluations: 3025000, maxCandidatesPerRoom: 3000, maxRepairsPerAttempt: 22, maxRouteNodes: 40000);
        var resources = new LayoutLocalResources(localWoodYield: 243, localStoneStuffs: new string[] {"BlocksLimestone","BlocksMarble","BlocksSandstone"}, riverCells: new GridPoint[] {}, steamGeyserCells: new GridPoint[] {new GridPoint(88, 44),new GridPoint(20, 92),new GridPoint(99, 163),new GridPoint(34, 176)});
        var historicalCatalog=RuntimeFlatCatalogFixture.Build();
        var historical=new ProceduralLayoutInput(requirements,terrain,0x2EDF48E9E9D64E75UL,historicalCatalog,
            new LayoutFortificationReservation(3),budget,resources);
        Equal("no_feasible_field_compound_pair",ProceduralLayoutPlanner.Plan(historical,true).RejectionCode,
            "without food facts this map cannot fit its full natural-field demand");
        var input=new ProceduralLayoutInput(requirements,terrain,historical.Seed,
            CapturedFoodGrowthCatalog(historicalCatalog),historical.Fortification,budget,resources);
        var result=ProceduralLayoutPlanner.Plan(input,true);
        Accepted(result);
        var layout=result.Layout;
        Equal(11,input.Requirements.Residents,"keep all eleven funded residents");
        Equal(3,P4BPerimeterDepth.Minimum(terrain,layout),"retain the requested three-layer breach depth");
        True(layout.FoodSupply!=null && layout.FoodSupply.Covered,"retain the original complete food demand");
        True(layout.FoodSupply.NaturalCells>=150,"bounded field refinement finds the demonstrated 150-cell region");
        True(layout.FoodSupply.NaturalCells>=P4BJointSite.MinimumMainFieldCells &&
            layout.FoodSupply.NaturalCells<requirements.FieldCells,"use the available smaller natural field");
        True(layout.AllFixtures.Any(f=>f.Kind==LayoutFixtureKind.HydroponicsBasin) &&
            layout.AllFixtures.Any(f=>f.Kind==LayoutFixtureKind.SunLamp),"funded feasible hydroponics includes a sun lamp");
        Equal(196,layout.FoodSupply.TotalFoodUnits,"fund the entire eight-day initial stock once");
        True(layout.Materials.OrdinaryImportValue<=requirements.BaseImportValueLimit,"retain the captured import allowance");
        True(layout.Power.FuelDays>=8,"cultivation power is funded through the first harvest");
        True(!result.Stats.BudgetExhausted,"retain the original evaluation cap");
        True(RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input,layout).Accepted,
            "the whole plan passes ordinary offline spawn preflight");
        Equal(terrainDigest,terrain.Digest,"planning does not mutate the captured map");
        Console.WriteLine("  oasis-candidate35518 layout="+layout.Digest+" natural="+layout.FoodSupply.NaturalCells+
            " basins="+layout.AllFixtures.Count(f=>f.Kind==LayoutFixtureKind.HydroponicsBasin)+
            " imports="+layout.Materials.OrdinaryImportValue+"/"+requirements.BaseImportValueLimit+" "+result.Stats);
    }
}
