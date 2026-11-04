using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static TerrainSnapshot CapturedTerrainWithDefinitionIds(string file, int size, string source)
    {
        var cells = new List<TerrainCell>();
        var rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, file));
        Equal(size, rows.Length, "captured height");
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
            Equal(size, width, "captured width");
        }
        return new TerrainSnapshot(size,size,cells,source);
    }

    private static TechCatalog CapturedFoodGrowthCatalog(TechCatalog historicalCatalog)
    {
        // These four definitions are copied from the 2026-11-04 resolved native
        // 1.7.4995 catalog capture. The rest uses the existing runtime test catalog.
        // See docs/plans/2026-11-04-ice-sheet-food-replay.md for provenance/limits.
        var replacements = new CatalogDef[] {
            new CatalogDef(defName: "MealSimple", category: (CatalogCategory)3, techLevel: (TechTier)0, research: new string[] {}, sizeX: 1, sizeZ: 1, stuffCategories: new string[] {}, costStuffCount: 0, costList: new KeyValuePair<string,int>[] {}, stuffProvides: new string[] {}, weaponTags: new string[] {}, apparelTags: new string[] {}, apparelLayers: new string[] {}, apparelBodyParts: new string[] {}, basePowerConsumption: 0, fuel: new string[] {}, fuelPerDay: 0d, hasInteractionCell: false, interactionOffset: new GridPoint(0, 0), passability: (CatalogPassability)0, marketValue: 15d, thingClass: "ThingWithComps", designationCategory: "", isTrap: false, producedAt: new string[] {"Campfire","ElectricStove","FueledStove"}, raw: false, stackLimit: 10, workToMake: 300d, turretGunDefName: "", minimumRange: 0d, maximumRange: 0d, fuelCapacity: 0d, fuelMultiplier: 0d, initialFuelPercent: 0d, fuelPerShot: 0d, fuelFactorByDifficulty: false, explosionRadius: 0d, combatPower: 0d, linkableFacilities: new string[] {}, facilityMaxDistance: 0d, isSittable: false, supportsSeatedWork: false, supportsQuality: false, maxItemsInCell: 1, nutrition: 0.8999999761581421d, growFertility: 0d, growLightRadius: 0d, glowRadius: 0d, glowStrength: 0, blocksLight: false),
            new CatalogDef(defName: "Pemmican", category: (CatalogCategory)3, techLevel: (TechTier)0, research: new string[] {"Pemmican"}, sizeX: 1, sizeZ: 1, stuffCategories: new string[] {}, costStuffCount: 0, costList: new KeyValuePair<string,int>[] {}, stuffProvides: new string[] {}, weaponTags: new string[] {}, apparelTags: new string[] {}, apparelLayers: new string[] {}, apparelBodyParts: new string[] {}, basePowerConsumption: 0, fuel: new string[] {}, fuelPerDay: 0d, hasInteractionCell: false, interactionOffset: new GridPoint(0, 0), passability: (CatalogPassability)0, marketValue: 1.399999976158142d, thingClass: "ThingWithComps", designationCategory: "", isTrap: false, producedAt: new string[] {"Campfire","ElectricStove","FueledStove"}, raw: false, stackLimit: 75, workToMake: 700d, turretGunDefName: "", minimumRange: 0d, maximumRange: 0d, fuelCapacity: 0d, fuelMultiplier: 0d, initialFuelPercent: 0d, fuelPerShot: 0d, fuelFactorByDifficulty: false, explosionRadius: 0d, combatPower: 0d, linkableFacilities: new string[] {}, facilityMaxDistance: 0d, isSittable: false, supportsSeatedWork: false, supportsQuality: false, maxItemsInCell: 1, nutrition: 0.05000000074505806d, growFertility: 0d, growLightRadius: 0d, glowRadius: 0d, glowStrength: 0, blocksLight: false),
            new CatalogDef(defName: "HydroponicsBasin", category: (CatalogCategory)0, techLevel: (TechTier)0, research: new string[] {"Hydroponics"}, sizeX: 1, sizeZ: 4, stuffCategories: new string[] {}, costStuffCount: 0, costList: new KeyValuePair<string,int>[] {new KeyValuePair<string,int>("Steel",100),new KeyValuePair<string,int>("ComponentIndustrial",1)}, stuffProvides: new string[] {}, weaponTags: new string[] {}, apparelTags: new string[] {}, apparelLayers: new string[] {}, apparelBodyParts: new string[] {}, basePowerConsumption: 70, fuel: new string[] {}, fuelPerDay: 0d, hasInteractionCell: false, interactionOffset: new GridPoint(0, 0), passability: (CatalogPassability)1, marketValue: 0d, thingClass: "Building_PlantGrower", designationCategory: "Production", isTrap: false, producedAt: new string[] {}, raw: false, stackLimit: 1, workToMake: 2800d, turretGunDefName: "", minimumRange: 0d, maximumRange: 0d, fuelCapacity: 0d, fuelMultiplier: 0d, initialFuelPercent: 0d, fuelPerShot: 0d, fuelFactorByDifficulty: false, explosionRadius: 0d, combatPower: 0d, linkableFacilities: new string[] {}, facilityMaxDistance: 0d, isSittable: false, supportsSeatedWork: false, supportsQuality: false, maxItemsInCell: 1, nutrition: 0d, growFertility: 2.799999952316284d, growLightRadius: 0d, glowRadius: 0d, glowStrength: 0, blocksLight: false),
            new CatalogDef(defName: "SunLamp", category: (CatalogCategory)0, techLevel: (TechTier)0, research: new string[] {"Electricity"}, sizeX: 1, sizeZ: 1, stuffCategories: new string[] {}, costStuffCount: 0, costList: new KeyValuePair<string,int>[] {new KeyValuePair<string,int>("Steel",40)}, stuffProvides: new string[] {}, weaponTags: new string[] {}, apparelTags: new string[] {}, apparelLayers: new string[] {}, apparelBodyParts: new string[] {}, basePowerConsumption: 2900, fuel: new string[] {}, fuelPerDay: 0d, hasInteractionCell: false, interactionOffset: new GridPoint(0, 0), passability: (CatalogPassability)1, marketValue: 0d, thingClass: "Building_SunLamp", designationCategory: "Furniture", isTrap: false, producedAt: new string[] {}, raw: false, stackLimit: 1, workToMake: 330d, turretGunDefName: "", minimumRange: 0d, maximumRange: 0d, fuelCapacity: 0d, fuelMultiplier: 0d, initialFuelPercent: 0d, fuelPerShot: 0d, fuelFactorByDifficulty: false, explosionRadius: 0d, combatPower: 0d, linkableFacilities: new string[] {}, facilityMaxDistance: 0d, isSittable: false, supportsSeatedWork: false, supportsQuality: false, maxItemsInCell: 1, nutrition: 0d, growFertility: 0d, growLightRadius: 5.800000190734863d, glowRadius: 14d, glowStrength: 370, blocksLight: false)
        };
        return new TechCatalog(historicalCatalog.Source + ";captured-food-growth-1.7.4995",
            historicalCatalog.Defs.Where(d=>!replacements.Any(r=>r.DefName==d.DefName)).Concat(replacements),
            historicalCatalog.Research);
    }
}
