using System;
using System.Linq;
using System.Collections.Generic;
using RealisticBases.Planning;

internal static partial class Program
{
    // Full native terrain; the exact native catalog/input anchors are replayed
    // separately with the immutable investigation fixture.
    private static void Swamp110256FieldExcavation()
    {
        var terrain = CapturedTerrainWithDefinitionIds("swamp-candidate-110256-terrain.rle", 200,
            "1.7.4995 rev309;generator=RB_P0_Base_Faction");
        Equal("13C34CB83909B4F5", terrain.Digest, "preserve every captured terrain, roof and trait tuple");
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        var requirements = new LayoutRequirements(residents: 10, capabilities: (SettlementCapability)255, housing: (HousingStandard)0, climate: new LayoutClimate(-16, 25, outdoorGrowingDays: 30), mortarBiasPercent: 50, excavationBudgetCells: 760, fieldCells: 308, capacityFloors: new KeyValuePair<LayoutRoomKind,int>[] {new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)2,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)3,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)4,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)5,24),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)7,4),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)8,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)9,24)}, stage: stage, importValueLimit: 101063, equipmentValueLimit: 30240, stockpileRoomCapacityTarget: 120, compactQuestCamp: false, defenseReserveWatts: 4000, logisticsUtilityValueLimit: 300, saleStorageStacks: 101, salePerishableStacks: 16, retainedStorageStacks: 0, retainedPerishableStacks: 0, questArtillery: 0, defenseImportReserveValue: 90750, baseImportValueLimit: 10313, dailyNutritionPerResident: 1.6d, firstHarvestDays: 8);
        var resources = new LayoutLocalResources(localWoodYield: 207749, localStoneStuffs: new string[] {"BlocksLimestone","BlocksSlate"}, riverCells: new GridPoint[] {}, steamGeyserCells: new GridPoint[] {new GridPoint(35, 26),new GridPoint(138, 93),new GridPoint(92, 107),new GridPoint(167, 124)});
        var input = new ProceduralLayoutInput(requirements, terrain, 0xFBDFB3080E69E976UL,
            CapturedFoodGrowthCatalog(RuntimeFlatCatalogFixture.Build()), new LayoutFortificationReservation(1),
            new LayoutSearchBudget(maxSites: 10, attemptsPerSite: 3, maxEvaluations: 2500000,
                maxCandidatesPerRoom: 3000, maxRepairsPerAttempt: 20, maxRouteNodes: 40000), resources);
        string before = input.Digest;
        var result = ProceduralLayoutPlanner.Plan(input, true);
        Accepted(result);
        var layout = result.Layout;
        Equal(LayoutDoctrine.OpenGround, layout.Doctrine, "exercise ordinary field excavation");
        Equal(10, input.Requirements.Residents, "retain every resident");
        Equal(1, P4BPerimeterDepth.Minimum(terrain, layout), "retain the final captured wall-layer attempt");
        Equal(3, input.Fortification.PlacementMargin, "retain setback, walls and outer clearance");
        True(layout.Notes.Any(n => n.StartsWith("fieldApproachExcavation=")), "the actual field connector must charge excavation");
        True(layout.Excavation.Count > 0 && layout.Excavation.Count <= 760, "retain the excavation allowance");
        True(layout.Excavation.All(c => !terrain[c].Has(TerrainTraits.ThickRoof)), "ordinary excavation cannot enter thick-roof rock");
        True(layout.FieldWall.Fields.Count >= P4BJointSite.MinimumMainFieldCells, "retain a useful natural field");
        True(layout.FoodSupply != null && layout.FoodSupply.ImportNutritionPerDay > 0, "fund the remaining food shortfall");
        True(layout.Materials.OrdinaryImportValue <= requirements.BaseImportValueLimit, "retain the ordinary import allowance");
        True(result.Stats.Evaluations <= input.Budget.MaxEvaluations, "retain the search cap");
        True(result.Stats.SitesTried <= input.Budget.MaxSites, "retain the site cap");
        True(ProceduralLayoutValidator.HousingFloorCells(input, layout).Count >= 10, "retain ten distinct housing positions");
        True(RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input, layout).Accepted, "retain complete offline spawn preflight");
        Equal(before, input.Digest, "planning leaves the capture unchanged");
        Console.WriteLine("  swamp110256 modeledCatalog=True layout=" + layout.Digest + " crops=" + layout.FieldWall.Fields.Count +
            " excavation=" + layout.Excavation.Count + "/760 imports=" + layout.Materials.OrdinaryImportValue + "/" +
            requirements.BaseImportValueLimit + " " + result.Stats);
    }
}
