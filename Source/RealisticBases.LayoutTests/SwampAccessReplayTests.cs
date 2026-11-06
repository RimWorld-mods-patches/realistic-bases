using System;
using System.Linq;
using System.Collections.Generic;
using RealisticBases.Planning;

internal static partial class Program
{
    // Full captured terrain with the shared modeled catalog. The immutable
    // native fixture is replayed separately with its exact input anchors.
    private static void Swamp18364AccessSettlement()
    {
        var terrain = CapturedTerrainWithDefinitionIds("swamp-candidate-18364-terrain.rle", 200,
            "1.7.4995 rev309;generator=RB_P0_Base_Faction");
        Equal("227B20B5C2AD3C95", terrain.Digest, "preserve the original map's terrain, roof and trait tuples");
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        var requirements = new LayoutRequirements(residents: 5, capabilities: (SettlementCapability)255, housing: (HousingStandard)0, climate: new LayoutClimate(-4, 37, outdoorGrowingDays: 45), mortarBiasPercent: 50, excavationBudgetCells: 410, fieldCells: 154, capacityFloors: new KeyValuePair<LayoutRoomKind,int>[] {new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)2,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)3,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)4,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)5,24),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)7,4),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)8,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)9,24)}, stage: stage, importValueLimit: 50531, equipmentValueLimit: 15120, stockpileRoomCapacityTarget: 120, compactQuestCamp: false, defenseReserveWatts: 4000, logisticsUtilityValueLimit: 300, saleStorageStacks: 104, salePerishableStacks: 23, retainedStorageStacks: 0, retainedPerishableStacks: 0, questArtillery: 0, defenseImportReserveValue: 45375, baseImportValueLimit: 5156, dailyNutritionPerResident: 1.6d, firstHarvestDays: 8);
        var resources = new LayoutLocalResources(localWoodYield: 251067, localStoneStuffs: new string[] {"BlocksGranite","BlocksLimestone","BlocksSandstone"}, riverCells: new GridPoint[] {}, steamGeyserCells: new GridPoint[] {new GridPoint(99, 20),new GridPoint(175, 60),new GridPoint(143, 83),new GridPoint(129, 167)});
        var input = new ProceduralLayoutInput(requirements, terrain, 0x77252C5858B54160UL,
            CapturedFoodGrowthCatalog(RuntimeFlatCatalogFixture.Build()), new LayoutFortificationReservation(1),
            new LayoutSearchBudget(maxSites: 10, attemptsPerSite: 3, maxEvaluations: 625000,
                maxCandidatesPerRoom: 3000, maxRepairsPerAttempt: 10, maxRouteNodes: 40000), resources);
        string original = input.Digest;
        var result = ProceduralLayoutPlanner.Plan(input, true);
        Accepted(result);
        var layout = result.Layout;
        Equal(5, input.Requirements.Residents, "keep all five funded residents");
        Equal(1, P4BPerimeterDepth.Minimum(terrain, layout), "retain the captured final wall-layer attempt");
        Equal(3, input.Fortification.PlacementMargin, "keep the inside setback, wall and outside clearance");
        True(layout.FieldWall.Fields.Count >= requirements.FieldCells, "keep the complete natural crop demand");
        True(result.Stats.Evaluations <= input.Budget.MaxEvaluations, "keep the original search cap");
        True(layout.Excavation.Count <= requirements.ExcavationBudgetCells, "keep the excavation allowance");
        True(layout.Materials.OrdinaryImportValue <= requirements.BaseImportValueLimit, "keep the ordinary import allowance");
        True(ProceduralLayoutValidator.HousingFloorCells(input, layout).Count >= 5, "reserve five distinct housing positions");
        True(RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input, layout).Accepted,
            "keep the complete ordinary offline spawn preflight");
        Equal(original, input.Digest, "planning never changes the capture input");
        Console.WriteLine("  swamp18364 modeledCatalog=True layout=" + layout.Digest + " crops=" + layout.FieldWall.Fields.Count +
            " imports=" + layout.Materials.OrdinaryImportValue + "/" + requirements.BaseImportValueLimit + " " + result.Stats);
    }
}
