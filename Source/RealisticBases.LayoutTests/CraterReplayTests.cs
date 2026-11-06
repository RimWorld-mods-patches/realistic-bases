using System;
using System.Collections.Generic;
using RealisticBases.Planning;

internal static partial class Program
{
    // The old capture's long native paths were summaries only. This protects
    // the current accepted map/roster/layers; it does not replay those PawnPaths.
    private static void Crater47593CurrentMap()
    {
        var terrain = CapturedTerrainWithDefinitionIds("crater-candidate-47593-terrain.rle", 200,
            "1.7.4995 rev309;generator=RB_P0_Base_Faction");
        Equal("4A46270FDB6B2886", terrain.Digest, "preserve every current native terrain/roof/trait tuple");
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        var requirements = new LayoutRequirements(residents: 4, capabilities: (SettlementCapability)255, housing: (HousingStandard)0, climate: new LayoutClimate(-5, 36, outdoorGrowingDays: 60), mortarBiasPercent: 50, excavationBudgetCells: 340, fieldCells: 124, capacityFloors: new KeyValuePair<LayoutRoomKind,int>[] {new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)2,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)3,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)4,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)5,24),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)7,4),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)8,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)9,24)}, stage: stage, importValueLimit: 40425, equipmentValueLimit: 12096, stockpileRoomCapacityTarget: 120, compactQuestCamp: false, defenseReserveWatts: 4000, logisticsUtilityValueLimit: 300, saleStorageStacks: 110, salePerishableStacks: 46, retainedStorageStacks: 0, retainedPerishableStacks: 0, questArtillery: 0, defenseImportReserveValue: 36300, baseImportValueLimit: 4125, dailyNutritionPerResident: 1.6d, firstHarvestDays: 8);
        var resources = new LayoutLocalResources(localWoodYield: 31790, localStoneStuffs: new string[] {"BlocksGranite","BlocksSlate"}, riverCells: new GridPoint[] {}, steamGeyserCells: new GridPoint[] {new GridPoint(165, 86),new GridPoint(88, 87),new GridPoint(83, 167)});
        var input = new ProceduralLayoutInput(requirements, terrain, 0x5497EAD20E330643UL,
            CapturedFoodGrowthCatalog(RuntimeFlatCatalogFixture.Build()), new LayoutFortificationReservation(2),
            new LayoutSearchBudget(maxSites: 10, attemptsPerSite: 3, maxEvaluations: 400000,
                maxCandidatesPerRoom: 3000, maxRepairsPerAttempt: 8, maxRouteNodes: 40000), resources);
        var result = ProceduralLayoutPlanner.Plan(input, true);
        Accepted(result);
        var layout = result.Layout;
        Equal(4, input.Requirements.Residents, "keep all four funded residents");
        Equal(2, P4BPerimeterDepth.Minimum(terrain, layout), "retain the original requested two layers without fallback");
        True(layout.FieldWall != null && layout.FieldWall.Fields.Count >= requirements.FieldCells,
            "retain the complete natural crop demand");
        True(layout.Excavation.Count <= requirements.ExcavationBudgetCells, "retain the excavation allowance");
        True(layout.Materials.OrdinaryImportValue <= requirements.BaseImportValueLimit, "retain the ordinary import allowance");
        True(result.Stats.Evaluations <= input.Budget.MaxEvaluations, "retain the evaluation cap");
        True(ProceduralLayoutValidator.HousingFloorCells(input, layout).Count >= 4, "reserve distinct resident spawn positions");
        var preflight = RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input, layout);
        True(preflight.Accepted, "keep complete ordinary offline spawn preflight: " + preflight.Failure);
        Equal("4A46270FDB6B2886", terrain.Digest, "planning leaves the captured map unchanged");
        Console.WriteLine("  crater-current modeledCatalog=True layout=" + layout.Digest +
            " crops=" + layout.FieldWall.Fields.Count + " excavation=" + layout.Excavation.Count +
            " imports=" + layout.Materials.OrdinaryImportValue + "/" + requirements.BaseImportValueLimit + " " + result.Stats);
    }
}
