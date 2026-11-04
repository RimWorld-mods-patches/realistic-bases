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
        var cells = new List<TerrainCell>();
        var rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "ice-sheet-33970-terrain.rle"));
        Equal(200, rows.Length, "captured height");
        foreach (string row in rows)
        {
            int width = 0;
            foreach (string token in row.Split(','))
            {
                var run = token.Split('*');
                var values = run[0].Split(':');
                int count = int.Parse(run[1]);
                width += count;
                var cell = new TerrainCell((TerrainTraits)Convert.ToUInt16(values[0],16),
                    ushort.Parse(values[1]), ushort.Parse(values[2]));
                for (int i=0; i<count; i++) cells.Add(cell);
            }
            Equal(200, width, "captured width");
        }
        var terrain = new TerrainSnapshot(200,200,cells,"1.7.4990 rev1069;generator=RB_P0_Base_Faction");
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

        // These four definitions are copied from the 2026-11-04 resolved native
        // 1.7.4995 catalog capture. The rest uses the existing runtime test catalog.
        // See docs/plans/2026-11-04-ice-sheet-food-replay.md for provenance/limits.
        var replacements = new CatalogDef[] {
            new CatalogDef(defName: "MealSimple", category: (CatalogCategory)3, techLevel: (TechTier)0, research: new string[] {}, sizeX: 1, sizeZ: 1, stuffCategories: new string[] {}, costStuffCount: 0, costList: new KeyValuePair<string,int>[] {}, stuffProvides: new string[] {}, weaponTags: new string[] {}, apparelTags: new string[] {}, apparelLayers: new string[] {}, apparelBodyParts: new string[] {}, basePowerConsumption: 0, fuel: new string[] {}, fuelPerDay: 0d, hasInteractionCell: false, interactionOffset: new GridPoint(0, 0), passability: (CatalogPassability)0, marketValue: 15d, thingClass: "ThingWithComps", designationCategory: "", isTrap: false, producedAt: new string[] {"Campfire","ElectricStove","FueledStove"}, raw: false, stackLimit: 10, workToMake: 300d, turretGunDefName: "", minimumRange: 0d, maximumRange: 0d, fuelCapacity: 0d, fuelMultiplier: 0d, initialFuelPercent: 0d, fuelPerShot: 0d, fuelFactorByDifficulty: false, explosionRadius: 0d, combatPower: 0d, linkableFacilities: new string[] {}, facilityMaxDistance: 0d, isSittable: false, supportsSeatedWork: false, supportsQuality: false, maxItemsInCell: 1, nutrition: 0.8999999761581421d, growFertility: 0d, growLightRadius: 0d, glowRadius: 0d, glowStrength: 0, blocksLight: false),
            new CatalogDef(defName: "Pemmican", category: (CatalogCategory)3, techLevel: (TechTier)0, research: new string[] {"Pemmican"}, sizeX: 1, sizeZ: 1, stuffCategories: new string[] {}, costStuffCount: 0, costList: new KeyValuePair<string,int>[] {}, stuffProvides: new string[] {}, weaponTags: new string[] {}, apparelTags: new string[] {}, apparelLayers: new string[] {}, apparelBodyParts: new string[] {}, basePowerConsumption: 0, fuel: new string[] {}, fuelPerDay: 0d, hasInteractionCell: false, interactionOffset: new GridPoint(0, 0), passability: (CatalogPassability)0, marketValue: 1.399999976158142d, thingClass: "ThingWithComps", designationCategory: "", isTrap: false, producedAt: new string[] {"Campfire","ElectricStove","FueledStove"}, raw: false, stackLimit: 75, workToMake: 700d, turretGunDefName: "", minimumRange: 0d, maximumRange: 0d, fuelCapacity: 0d, fuelMultiplier: 0d, initialFuelPercent: 0d, fuelPerShot: 0d, fuelFactorByDifficulty: false, explosionRadius: 0d, combatPower: 0d, linkableFacilities: new string[] {}, facilityMaxDistance: 0d, isSittable: false, supportsSeatedWork: false, supportsQuality: false, maxItemsInCell: 1, nutrition: 0.05000000074505806d, growFertility: 0d, growLightRadius: 0d, glowRadius: 0d, glowStrength: 0, blocksLight: false),
            new CatalogDef(defName: "HydroponicsBasin", category: (CatalogCategory)0, techLevel: (TechTier)0, research: new string[] {"Hydroponics"}, sizeX: 1, sizeZ: 4, stuffCategories: new string[] {}, costStuffCount: 0, costList: new KeyValuePair<string,int>[] {new KeyValuePair<string,int>("Steel",100),new KeyValuePair<string,int>("ComponentIndustrial",1)}, stuffProvides: new string[] {}, weaponTags: new string[] {}, apparelTags: new string[] {}, apparelLayers: new string[] {}, apparelBodyParts: new string[] {}, basePowerConsumption: 70, fuel: new string[] {}, fuelPerDay: 0d, hasInteractionCell: false, interactionOffset: new GridPoint(0, 0), passability: (CatalogPassability)1, marketValue: 0d, thingClass: "Building_PlantGrower", designationCategory: "Production", isTrap: false, producedAt: new string[] {}, raw: false, stackLimit: 1, workToMake: 2800d, turretGunDefName: "", minimumRange: 0d, maximumRange: 0d, fuelCapacity: 0d, fuelMultiplier: 0d, initialFuelPercent: 0d, fuelPerShot: 0d, fuelFactorByDifficulty: false, explosionRadius: 0d, combatPower: 0d, linkableFacilities: new string[] {}, facilityMaxDistance: 0d, isSittable: false, supportsSeatedWork: false, supportsQuality: false, maxItemsInCell: 1, nutrition: 0d, growFertility: 2.799999952316284d, growLightRadius: 0d, glowRadius: 0d, glowStrength: 0, blocksLight: false),
            new CatalogDef(defName: "SunLamp", category: (CatalogCategory)0, techLevel: (TechTier)0, research: new string[] {"Electricity"}, sizeX: 1, sizeZ: 1, stuffCategories: new string[] {}, costStuffCount: 0, costList: new KeyValuePair<string,int>[] {new KeyValuePair<string,int>("Steel",40)}, stuffProvides: new string[] {}, weaponTags: new string[] {}, apparelTags: new string[] {}, apparelLayers: new string[] {}, apparelBodyParts: new string[] {}, basePowerConsumption: 2900, fuel: new string[] {}, fuelPerDay: 0d, hasInteractionCell: false, interactionOffset: new GridPoint(0, 0), passability: (CatalogPassability)1, marketValue: 0d, thingClass: "Building_SunLamp", designationCategory: "Furniture", isTrap: false, producedAt: new string[] {}, raw: false, stackLimit: 1, workToMake: 330d, turretGunDefName: "", minimumRange: 0d, maximumRange: 0d, fuelCapacity: 0d, fuelMultiplier: 0d, initialFuelPercent: 0d, fuelPerShot: 0d, fuelFactorByDifficulty: false, explosionRadius: 0d, combatPower: 0d, linkableFacilities: new string[] {}, facilityMaxDistance: 0d, isSittable: false, supportsSeatedWork: false, supportsQuality: false, maxItemsInCell: 1, nutrition: 0d, growFertility: 0d, growLightRadius: 5.800000190734863d, glowRadius: 14d, glowStrength: 370, blocksLight: false)
        };
        var catalog = new TechCatalog(historicalCatalog.Source + ";captured-food-growth-1.7.4995",
            historicalCatalog.Defs.Where(d=>!replacements.Any(r=>r.DefName==d.DefName)).Concat(replacements),
            historicalCatalog.Research);
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
