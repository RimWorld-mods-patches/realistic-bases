using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void CliffCorner3810Search()
    {
        // The exact native catalog is checked separately by the fixture replay.
        var terrain = CapturedTerrainWithDefinitionIds("cliff-corner-3810-terrain.rle", 200,
            "1.7.4995 rev309;generator=RB_P0_Base_Faction");
        Equal("9AD833647391184A", terrain.Digest, "retain all captured terrain, roof and trait tuples");
        var capacityFloors = new Dictionary<LayoutRoomKind, int>
        {
            [LayoutRoomKind.Jail] = 1,
            [LayoutRoomKind.Medical] = 1,
            [LayoutRoomKind.Kitchen] = 1,
            [LayoutRoomKind.Freezer] = 24,
            [LayoutRoomKind.Dining] = 4,
            [LayoutRoomKind.Workshop] = 1,
            [LayoutRoomKind.Storage] = 24
        };
        var requirements = new LayoutRequirements(
            residents: 6, capabilities: Industrial, housing: HousingStandard.Barracks,
            climate: new LayoutClimate(0, 41, outdoorGrowingDays: 60), mortarBiasPercent: 50,
            excavationBudgetCells: 480, fieldCells: 185, capacityFloors: capacityFloors,
            stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate),
            importValueLimit: 60638, equipmentValueLimit: 18144, stockpileRoomCapacityTarget: 120,
            defenseReserveWatts: 4000, logisticsUtilityValueLimit: 300,
            saleStorageStacks: 112, salePerishableStacks: 13, defenseImportReserveValue: 54450,
            baseImportValueLimit: 6188, dailyNutritionPerResident: 1.6d, firstHarvestDays: 8);
        var resources = new LayoutLocalResources(localWoodYield: 17021,
            localStoneStuffs: new[] { "BlocksSandstone", "BlocksSlate" },
            steamGeyserCells: new[] { new GridPoint(73, 56), new GridPoint(111, 71),
                new GridPoint(21, 91), new GridPoint(79, 150) });
        var input = new ProceduralLayoutInput(requirements, terrain, 0x5658E5B55B267A55UL,
            CapturedFoodGrowthCatalog(RuntimeFlatCatalogFixture.Build()), new LayoutFortificationReservation(1),
            new LayoutSearchBudget(maxSites: 10, attemptsPerSite: 3, maxEvaluations: 900000,
                maxCandidatesPerRoom: 3000, maxRepairsPerAttempt: 12, maxRouteNodes: 40000), resources);
        var result = ProceduralLayoutPlanner.Plan(input, true);
        Accepted(result);
        var layout = result.Layout;
        True(result.Diagnostics.Any(d => d.Stage == "doctrine" && d.Code == "terrain_protect"),
            "prefer mountain packing when the preferred sites have little connected room ground");
        Equal(LayoutDoctrine.RockProtected, layout.Doctrine, "complete the mountain settlement");
        Equal(1, P4BPerimeterDepth.Minimum(terrain, layout), "keep the requested wall layer");
        True(layout.FieldWall.Fields.Count >= requirements.FieldCells, "retain the full crop target");
        True(layout.Excavation.Count <= requirements.ExcavationBudgetCells, "stay within the excavation allowance");
        True(layout.Materials.OrdinaryImportValue <= requirements.BaseImportValueLimit, "stay within the import allowance");
        True(result.Stats.Evaluations <= input.Budget.MaxEvaluations, "stay within the evaluation cap");
        True(result.Stats.SitesTried <= input.Budget.MaxSites, "stay within the site cap");
        True(ProceduralLayoutValidator.HousingFloorCells(input, layout).Count >= 6, "reserve six distinct spawn positions");
        True(RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input, layout).Accepted,
            "pass ordinary offline spawn preflight");
        Console.WriteLine("  cliff-corner3810 modeledCatalog=True layout=" + layout.Digest + " crops=" + layout.FieldWall.Fields.Count +
            " excavation=" + layout.Excavation.Count + "/480 imports=" + layout.Materials.OrdinaryImportValue + "/6188 " + result.Stats);
    }

    private static void MountainValleyKeepsOpenGround()
    {
        var terrain = Build(180, 180, (x, z) =>
        {
            if (x < 50 || x >= 130) return RockRoofed;
            return x >= 79 && x < 102 && z >= 78 && z < 101 ? Ground : Ground & ~TerrainTraits.Fertile;
        });
        var requirements = new LayoutRequirements(6, Industrial, HousingStandard.Barracks, Temperate,
            mortarBiasPercent: 100, excavationBudgetCells: 480, fieldCells: 185,
            stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate));
        var input = new ProceduralLayoutInput(requirements, terrain, 0x5658E5B55B267A55UL,
            CapturedFoodGrowthCatalog(RuntimeFlatCatalogFixture.Build()), new LayoutFortificationReservation(1),
            LayoutSearchBudget.ForPopulation(6));
        var result = ProceduralLayoutPlanner.Plan(input, true);
        Accepted(result);
        True(result.Diagnostics.Any(d => d.Stage == "doctrine" && d.Code == "keep_mortar"),
            "a connected valley leaves both styles eligible and keeps the seeded preference");
        Equal(LayoutDoctrine.OpenGround, result.Layout.Doctrine, "build in the valley");
    }
}
