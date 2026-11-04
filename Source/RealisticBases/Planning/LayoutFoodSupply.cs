using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // A finite food contract for an ordinary settlement that cannot fit its
    // whole natural field. Crop production is an estimate; initial inventory
    // covers the entire first-harvest horizon without crediting future output.
    public sealed class LayoutFoodSupply
    {
        internal LayoutFoodSupply(LayoutRequirements requirements, int naturalCells, double hydroponicFieldEquivalent,
            string foodDef, double nutritionPerUnit, int stackLimit, double marketValue,
            IEnumerable<string> diagnostics)
        {
            if (requirements == null || naturalCells < 0 || !P5Readiness.Nonnegative(hydroponicFieldEquivalent) ||
                !P5Readiness.Positive(nutritionPerUnit) || stackLimit <= 0 || !P5Readiness.Positive(marketValue))
                throw new ArgumentException("Invalid food supply input");
            NaturalCells = naturalCells;
            HydroponicFieldEquivalent = hydroponicFieldEquivalent;
            FoodDef = foodDef;
            NutritionPerUnit = nutritionPerUnit;
            Days = requirements.FirstHarvestDays;
            DailyNutrition = requirements.Residents * requirements.DailyNutritionPerResident;
            double fullField = requirements.FieldCells;
            ProductionNutritionPerDay = fullField <= 0 ? 0 :
                DailyNutrition * Math.Min(1, (naturalCells + hydroponicFieldEquivalent) / fullField);
            ImportNutritionPerDay = Math.Max(0, DailyNutrition - ProductionNutritionPerDay);
            ImportedFoodUnits = checked((int)Math.Ceiling(ImportNutritionPerDay * Days * P5Readiness.FoodSafetyFactor / nutritionPerUnit - 0.000001));
            BaselineFoodUnits = P5Readiness.FoodUnits(requirements.Residents, requirements.DailyNutritionPerResident,
                nutritionPerUnit, requirements.Residents * 12);
            TotalFoodUnits = Math.Max(BaselineFoodUnits, checked((int)Math.Ceiling(
                DailyNutrition * Days * P5Readiness.FoodSafetyFactor / nutritionPerUnit - 0.000001)));
            // Import coverage can use a subset of the same initial stock. The
            // horizon top-up and the deficit reserve are not two stock grants.
            PurchasedFoodUnits = Math.Max(ImportedFoodUnits, TotalFoodUnits - BaselineFoodUnits);
            ImportValue = checked((int)Math.Ceiling(PurchasedFoodUnits * marketValue));
            StorageCells = (TotalFoodUnits + stackLimit - 1) / stackLimit;
            Diagnostics = new ReadOnlyCollection<string>((diagnostics ?? Enumerable.Empty<string>()).ToList());
        }

        public int NaturalCells { get; }
        public double HydroponicFieldEquivalent { get; }
        public string FoodDef { get; }
        public double NutritionPerUnit { get; }
        public int Days { get; }
        public double DailyNutrition { get; }
        public double ProductionNutritionPerDay { get; }
        public double ImportNutritionPerDay { get; }
        public int ImportedFoodUnits { get; }
        public int BaselineFoodUnits { get; }
        public int TotalFoodUnits { get; }
        public int PurchasedFoodUnits { get; }
        public int ImportValue { get; }
        public int StorageCells { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool Covered => ProductionNutritionPerDay + ImportNutritionPerDay + 0.000001 >= DailyNutrition &&
            TotalFoodUnits * NutritionPerUnit + 0.000001 >= DailyNutrition * Days * P5Readiness.FoodSafetyFactor;

        internal ulong AddTo(ulong hash)
        {
            hash = StableHash.Add(hash, "food-supply.1");
            foreach (int value in new[] { NaturalCells, Days, ImportedFoodUnits, BaselineFoodUnits, TotalFoodUnits,
                PurchasedFoodUnits, ImportValue, StorageCells }) hash = StableHash.Add(hash, value);
            hash = StableHash.Add(hash, FoodDef);
            hash = StableHash.Add(hash, HydroponicFieldEquivalent.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            return hash;
        }
    }

    public static class LayoutFoodSupplyPlanner
    {
        public const string Basin = "HydroponicsBasin";
        public const string GrowLamp = "SunLamp";

        public static bool CanImport(ProceduralLayoutInput input) => !input.Requirements.CompactQuestCamp &&
            input.Catalog.TryGet(FoodDef(input), out CatalogDef food) && food.Nutrition > 0 && food.MarketValue > 0;

        public static string FoodDef(ProceduralLayoutInput input) =>
            input.Equipment.Kit.Refrigerated ? "MealSimple" : "Pemmican";

        public static bool CanHydroponics(ProceduralLayoutInput input, out string reason)
        {
            if (!input.Equipment.Kit.Powered) { reason = "unpowered_stage"; return false; }
            if (!input.Catalog.TryGet(Basin, out CatalogDef basin) || !input.Catalog.TryGet(GrowLamp, out CatalogDef lamp))
            { reason = "missing_basin_or_sun_lamp"; return false; }
            if (!input.Catalog.IsLegal(basin, input.Equipment.Kit.Stage) || !input.Catalog.IsLegal(lamp, input.Equipment.Kit.Stage))
            { reason = "stage_or_research_illegal"; return false; }
            if (basin.GrowFertility <= 0 || lamp.GrowLightRadius <= 0 || basin.BasePowerConsumption <= 0 || lamp.BasePowerConsumption <= 0)
            { reason = "missing_growth_light_or_power_facts"; return false; }
            if (P4EArtillery.ConstructionValue(input.Catalog, Basin) == int.MaxValue ||
                P4EArtillery.ConstructionValue(input.Catalog, GrowLamp) == int.MaxValue)
            { reason = "unpriced_growth_equipment"; return false; }
            reason = "eligible";
            return true;
        }

        public static LayoutFoodSupply Plan(ProceduralLayoutInput input, int naturalCells, IEnumerable<LayoutRoom> rooms,
            IEnumerable<string> diagnostics = null)
        {
            if (!CanImport(input)) return null;
            if (naturalCells > 0 && !input.Requirements.Climate.SupportsOutdoorCrops) return null;
            input.Catalog.TryGet(FoodDef(input), out CatalogDef food);
            double hydroponicEquivalent = 0;
            foreach (LayoutRoom room in rooms.Where(r => r.Kind == LayoutRoomKind.Hydroponics))
            {
                if (!CanHydroponics(input, out _)) return null;
                input.Catalog.TryGet(Basin, out CatalogDef basin);
                input.Catalog.TryGet(GrowLamp, out CatalogDef lamp);
                var lamps = room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.SunLamp).ToArray();
                if (room.Fixtures.Count(f => f.Kind == LayoutFixtureKind.HydroponicsBasin) < room.DemandedCapacity) return null;
                if (lamps.Length == 0 || lamps.Any(l => l.DefName != lamp.DefName || l.Watts != lamp.BasePowerConsumption ||
                    l.Cells.Count != lamp.SizeX * lamp.SizeZ)) return null;
                var used = new HashSet<GridPoint>();
                foreach (LayoutFixture fixture in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.HydroponicsBasin))
                {
                    if (fixture.DefName != basin.DefName || fixture.Watts != basin.BasePowerConsumption || fixture.Blocking != basin.Blocking ||
                        fixture.Cells.Count != basin.SizeX * basin.SizeZ || fixture.Cells.Any(c => !room.Interior.Contains(c) || !used.Add(c))) return null;
                    if (fixture.Cells.Any(c => !lamps.Any(l => l.Cells.Any(p =>
                        (c.X-p.X)*(c.X-p.X)+(c.Z-p.Z)*(c.Z-p.Z) <= lamp.GrowLightRadius*lamp.GrowLightRadius)))) return null;
                    hydroponicEquivalent += fixture.Cells.Count * basin.GrowFertility;
                }
            }
            return new LayoutFoodSupply(input.Requirements, naturalCells, hydroponicEquivalent, food.DefName,
                food.Nutrition, food.StackLimit, food.MarketValue, diagnostics);
        }

        internal static LayoutRoomDemand GrowRoom(ProceduralLayoutInput input, int basins, int index)
        {
            if (basins <= 0 || !CanHydroponics(input, out _)) return null;
            var sizes = new List<GridRect>();
            foreach (int width in new[] { 5, 7, 9, 11 })
                foreach (int height in new[] { 7, 9, 11 })
                {
                    var context = LayoutDemandModel.FurnishContext(input.Equipment.Kit, input.Requirements, false, _ => 0);
                    if (new[] { (width+1)/2, 2, width-1 }.Distinct().Any(door =>
                        LayoutFurnishing.Furnish(LayoutRoomKind.Hydroponics, width, height, door, basins, context).Success))
                        sizes.Add(new GridRect(0, 0, width, height));
                }
            var chosen = sizes.OrderBy(s => s.Width*s.Height).ThenBy(s => s.Width).ThenBy(s => s.Height)
                .Take(LayoutDemandModel.MaxSizesPerRoom).ToList();
            return chosen.Count == 0 ? null : new LayoutRoomDemand("hydroponics-" + index, LayoutRoomKind.Hydroponics,
                basins, "basins", chosen);
        }
    }
}
