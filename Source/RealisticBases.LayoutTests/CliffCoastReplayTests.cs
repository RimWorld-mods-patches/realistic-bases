using System;
using System.Collections.Generic;
using RealisticBases.Planning;

internal static partial class Program
{
    // Shared modeled catalog; the exact native catalog and all original
    // captured input digests are checked by the separate fixture replay.
    private static void CliffCoast92330FairSearch()
    {
        var terrain = CapturedTerrainWithDefinitionIds("cliff-coast-92330-terrain.rle", 200,
            "1.7.4995 rev309;generator=RB_P0_Base_Faction");
        Equal("78D0DEF635C4DF6E", terrain.Digest, "preserve every captured cliff/coast terrain, roof and trait tuple");
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        var requirements = new LayoutRequirements(residents: 4, capabilities: (SettlementCapability)255, housing: (HousingStandard)0, climate: new LayoutClimate(0, 41, outdoorGrowingDays: 60), mortarBiasPercent: 50, excavationBudgetCells: 340, fieldCells: 124, capacityFloors: new KeyValuePair<LayoutRoomKind,int>[] {new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)2,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)3,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)4,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)5,24),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)7,4),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)8,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)9,24)}, stage: stage, importValueLimit: 40425, equipmentValueLimit: 12096, stockpileRoomCapacityTarget: 120, compactQuestCamp: false, defenseReserveWatts: 4000, logisticsUtilityValueLimit: 300, saleStorageStacks: 102, salePerishableStacks: 17, retainedStorageStacks: 0, retainedPerishableStacks: 0, questArtillery: 0, defenseImportReserveValue: 36300, baseImportValueLimit: 4125, dailyNutritionPerResident: 1.6d, firstHarvestDays: 8);
        var resources = new LayoutLocalResources(localWoodYield: 21808, localStoneStuffs: new string[] {"BlocksLimestone","BlocksMarble","BlocksSlate"}, riverCells: new GridPoint[] {}, steamGeyserCells: new GridPoint[] {new GridPoint(127, 79),new GridPoint(67, 110),new GridPoint(50, 177)});
        var input = new ProceduralLayoutInput(requirements, terrain, 0xE3E7F60788DC001AUL,
            CapturedFoodGrowthCatalog(RuntimeFlatCatalogFixture.Build()), new LayoutFortificationReservation(1),
            new LayoutSearchBudget(maxSites: 10, attemptsPerSite: 3, maxEvaluations: 400000,
                maxCandidatesPerRoom: 3000, maxRepairsPerAttempt: 8, maxRouteNodes: 40000), resources);
        string original = input.Digest;
        var result = ProceduralLayoutPlanner.Plan(input, true);
        Accepted(result);
        var layout = result.Layout;
        Equal(4, input.Requirements.Residents, "keep the whole captured roster");
        Equal(1, P4BPerimeterDepth.Minimum(terrain, layout), "keep the full requested wall depth");
        Equal(3, input.Fortification.PlacementMargin, "retain inside setback, wall and outside clearance");
        True(layout.FieldWall.Fields.Count >= requirements.FieldCells, "retain the full crop target");
        True(layout.Excavation.Count <= 340, "retain the captured excavation allowance");
        True(layout.Materials.OrdinaryImportValue <= requirements.BaseImportValueLimit, "retain the captured ordinary allowance");
        True(result.Stats.Evaluations <= 400000, "never grant more search work");
        True(result.Stats.SitesTried <= 10, "retain the site limit");
        True(ProceduralLayoutValidator.HousingFloorCells(input, layout).Count >= 4, "keep distinct resident spawn floor");
        True(RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input, layout).Accepted,
            "retain complete ordinary offline spawn preflight");
        Equal(original, input.Digest, "planning leaves the input unchanged");
        Console.WriteLine("  cliff-coast92330 modeledCatalog=True layout=" + layout.Digest + " crops=" + layout.FieldWall.Fields.Count +
            " excavation=" + layout.Excavation.Count + "/340 imports=" + layout.Materials.OrdinaryImportValue +
            "/" + requirements.BaseImportValueLimit + " " + result.Stats);
    }
}
