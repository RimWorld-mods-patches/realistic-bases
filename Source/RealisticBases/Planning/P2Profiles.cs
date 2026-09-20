using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public static class P2Versions
    {
        public const int ProfileSchema = 1;
        public const int BudgetSchema = 1;
        public const string Rules = "p2.1";
    }

    [Flags]
    public enum SettlementCapability
    {
        None = 0,
        Electricity = 1 << 0,
        Refrigeration = 1 << 1,
        ElectricCooking = 1 << 2,
        FueledCooking = 1 << 3,
        Batteries = 1 << 4,
        Turrets = 1 << 5,
        Firearms = 1 << 6,
        PreservedFood = 1 << 7
    }

    public enum FacilityKind
    {
        Housing,
        Jail,
        Kitchen,
        Freezer,
        Pantry,
        Field,
        DiningRecreation,
        Workshop,
        BulkStorage,
        Medical,
        Power,
        Gatehouse,
        Defense
    }

    public sealed class FacilityDemand
    {
        public FacilityDemand(FacilityKind kind, int capacity, bool requiresPower, bool mustBeRoofed)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Kind = kind;
            Capacity = capacity;
            RequiresPower = requiresPower;
            MustBeRoofed = mustBeRoofed;
        }

        public FacilityKind Kind { get; }
        public int Capacity { get; }
        public bool RequiresPower { get; }
        public bool MustBeRoofed { get; }
    }

    public sealed class StartupScenario
    {
        public StartupScenario(string id, int durationHours, int generationWatts, int loadWatts,
            int startingBatteryWattDays, int minimumEndingBatteryWattDays)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Scenario id is required.", nameof(id));
            if (durationHours <= 0 || generationWatts < 0 || loadWatts < 0 ||
                startingBatteryWattDays < 0 || minimumEndingBatteryWattDays < 0)
                throw new ArgumentOutOfRangeException("Power scenario values must be non-negative and duration positive.");
            Id = id;
            DurationHours = durationHours;
            GenerationWatts = generationWatts;
            LoadWatts = loadWatts;
            StartingBatteryWattDays = startingBatteryWattDays;
            MinimumEndingBatteryWattDays = minimumEndingBatteryWattDays;
        }

        public string Id { get; }
        public int DurationHours { get; }
        public int GenerationWatts { get; }
        public int LoadWatts { get; }
        public int StartingBatteryWattDays { get; }
        public int MinimumEndingBatteryWattDays { get; }

        public int EndingBatteryWattDays
        {
            get
            {
                int delta = (int)Math.Floor((GenerationWatts - LoadWatts) * DurationHours / 24d);
                return Math.Max(0, StartingBatteryWattDays + delta);
            }
        }

        public bool Passes => GenerationWatts >= LoadWatts || EndingBatteryWattDays >= MinimumEndingBatteryWattDays;
    }

    public sealed class SettlementResourceBudget
    {
        public SettlementResourceBudget(int localValueLimit, int importValueLimit, int constructionValue,
            int importedValue, int equipmentValue, int stockValue, int initializedCropValue)
        {
            LocalValueLimit = NonNegative(localValueLimit, nameof(localValueLimit));
            ImportValueLimit = NonNegative(importValueLimit, nameof(importValueLimit));
            ConstructionValue = NonNegative(constructionValue, nameof(constructionValue));
            ImportedValue = NonNegative(importedValue, nameof(importedValue));
            EquipmentValue = NonNegative(equipmentValue, nameof(equipmentValue));
            StockValue = NonNegative(stockValue, nameof(stockValue));
            InitializedCropValue = NonNegative(initializedCropValue, nameof(initializedCropValue));
        }

        public int LocalValueLimit { get; }
        public int ImportValueLimit { get; }
        public int ConstructionValue { get; }
        public int ImportedValue { get; }
        public int EquipmentValue { get; }
        public int StockValue { get; }
        public int InitializedCropValue { get; }
        public int TotalPlannedValue => ConstructionValue + ImportedValue + EquipmentValue + StockValue + InitializedCropValue;
        public bool IsWithinLimits => ConstructionValue <= LocalValueLimit && ImportedValue <= ImportValueLimit;

        private static int NonNegative(int value, string name)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public sealed class PhysicalSettlementProfile
    {
        public PhysicalSettlementProfile(string id, int version, int residents, SettlementCapability capabilities,
            string wallMaterial, string cropDef, int cropSkill, double dailyNutritionPerResident,
            double firstHarvestDays, double foodSafetyFactor, double effectiveNutritionPerCropCellPerDay,
            int startupFoodUnits, double nutritionPerFoodUnit, int startupFuelUnits,
            IEnumerable<FacilityDemand> facilities, IEnumerable<StartupScenario> powerScenarios,
            SettlementResourceBudget budget, IEnumerable<string> weaponDefs, IEnumerable<string> armorDefs)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Profile id is required.", nameof(id));
            if (version <= 0 || residents <= 0 || cropSkill < 0 || dailyNutritionPerResident <= 0 ||
                firstHarvestDays <= 0 || foodSafetyFactor < 1 || effectiveNutritionPerCropCellPerDay <= 0 ||
                startupFoodUnits <= 0 || nutritionPerFoodUnit <= 0 || startupFuelUnits < 0)
                throw new ArgumentOutOfRangeException("Invalid physical profile value.");
            Id = id;
            Version = version;
            Residents = residents;
            Capabilities = capabilities;
            WallMaterial = wallMaterial ?? string.Empty;
            CropDef = cropDef ?? string.Empty;
            CropSkill = cropSkill;
            DailyNutritionPerResident = dailyNutritionPerResident;
            FirstHarvestDays = firstHarvestDays;
            FoodSafetyFactor = foodSafetyFactor;
            EffectiveNutritionPerCropCellPerDay = effectiveNutritionPerCropCellPerDay;
            StartupFoodUnits = startupFoodUnits;
            NutritionPerFoodUnit = nutritionPerFoodUnit;
            StartupFuelUnits = startupFuelUnits;
            Facilities = new ReadOnlyCollection<FacilityDemand>((facilities ?? throw new ArgumentNullException(nameof(facilities))).ToList());
            PowerScenarios = new ReadOnlyCollection<StartupScenario>((powerScenarios ?? Enumerable.Empty<StartupScenario>()).ToList());
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
            WeaponDefs = new ReadOnlyCollection<string>((weaponDefs ?? Enumerable.Empty<string>()).ToList());
            ArmorDefs = new ReadOnlyCollection<string>((armorDefs ?? Enumerable.Empty<string>()).ToList());
            Validate();
            Digest = ComputeDigest();
        }

        public int SchemaVersion => P2Versions.ProfileSchema;
        public string Id { get; }
        public int Version { get; }
        public int Residents { get; }
        public SettlementCapability Capabilities { get; }
        public string WallMaterial { get; }
        public string CropDef { get; }
        public int CropSkill { get; }
        public double DailyNutritionPerResident { get; }
        public double FirstHarvestDays { get; }
        public double FoodSafetyFactor { get; }
        public double EffectiveNutritionPerCropCellPerDay { get; }
        public int StartupFoodUnits { get; }
        public double NutritionPerFoodUnit { get; }
        public int StartupFuelUnits { get; }
        public IReadOnlyList<FacilityDemand> Facilities { get; }
        public IReadOnlyList<StartupScenario> PowerScenarios { get; }
        public SettlementResourceBudget Budget { get; }
        public IReadOnlyList<string> WeaponDefs { get; }
        public IReadOnlyList<string> ArmorDefs { get; }
        public string Digest { get; }
        public double RequiredStartupNutrition => Residents * DailyNutritionPerResident * FirstHarvestDays * FoodSafetyFactor;
        public double StartupNutrition => StartupFoodUnits * NutritionPerFoodUnit;
        public int RequiredCropCells => (int)Math.Ceiling(Residents * DailyNutritionPerResident / EffectiveNutritionPerCropCellPerDay);
        public bool Has(SettlementCapability capability) => (Capabilities & capability) == capability;

        private void Validate()
        {
            FacilityKind[] common = { FacilityKind.Housing, FacilityKind.Jail, FacilityKind.Kitchen, FacilityKind.Field,
                FacilityKind.DiningRecreation, FacilityKind.Workshop, FacilityKind.BulkStorage, FacilityKind.Medical,
                FacilityKind.Gatehouse, FacilityKind.Defense };
            foreach (FacilityKind kind in common)
                if (Facilities.Count(f => f.Kind == kind) != 1) throw new ArgumentException("Profile requires exactly one " + kind + " facility demand.");
            FacilityDemand housing = Facilities.Single(f => f.Kind == FacilityKind.Housing);
            if (housing.Capacity < Residents) throw new ArgumentException("Housing capacity does not cover residents.");
            if (StartupNutrition + 0.0001 < RequiredStartupNutrition) throw new ArgumentException("Startup food does not bridge first harvest.");
            if (!Budget.IsWithinLimits) throw new ArgumentException("Resource budget exceeds its local or import allowance.");
            bool electric = Has(SettlementCapability.Electricity);
            if (electric)
            {
                if (!Has(SettlementCapability.Refrigeration | SettlementCapability.Batteries))
                    throw new ArgumentException("Electric profile requires refrigeration and batteries.");
                if (Facilities.Count(f => f.Kind == FacilityKind.Power) != 1 || Facilities.Count(f => f.Kind == FacilityKind.Freezer) != 1)
                    throw new ArgumentException("Electric profile requires power and freezer facilities.");
                if (Facilities.Any(f => f.Kind == FacilityKind.Pantry) || PowerScenarios.Count == 0 || PowerScenarios.Any(s => !s.Passes))
                    throw new ArgumentException("Electric profile has invalid preservation or power scenarios.");
            }
            else
            {
                SettlementCapability prohibited = SettlementCapability.Refrigeration | SettlementCapability.ElectricCooking |
                    SettlementCapability.Batteries | SettlementCapability.Turrets;
                if ((Capabilities & prohibited) != 0 || Facilities.Any(f => f.Kind == FacilityKind.Power || f.Kind == FacilityKind.Freezer) ||
                    Facilities.Count(f => f.Kind == FacilityKind.Pantry) != 1 || PowerScenarios.Count != 0)
                    throw new ArgumentException("Non-electric profile contains an electric substitute.");
            }
        }

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, P2Versions.Rules);
            hash = StableHash.Add(hash, SchemaVersion);
            hash = StableHash.Add(hash, Id);
            hash = StableHash.Add(hash, Version);
            hash = StableHash.Add(hash, Residents);
            hash = StableHash.Add(hash, (int)Capabilities);
            hash = StableHash.Add(hash, WallMaterial);
            hash = StableHash.Add(hash, CropDef);
            hash = StableHash.Add(hash, CropSkill);
            hash = StableHash.Add(hash, unchecked((ulong)BitConverter.DoubleToInt64Bits(DailyNutritionPerResident)));
            hash = StableHash.Add(hash, unchecked((ulong)BitConverter.DoubleToInt64Bits(FirstHarvestDays)));
            hash = StableHash.Add(hash, unchecked((ulong)BitConverter.DoubleToInt64Bits(FoodSafetyFactor)));
            hash = StableHash.Add(hash, unchecked((ulong)BitConverter.DoubleToInt64Bits(EffectiveNutritionPerCropCellPerDay)));
            hash = StableHash.Add(hash, StartupFoodUnits);
            hash = StableHash.Add(hash, unchecked((ulong)BitConverter.DoubleToInt64Bits(NutritionPerFoodUnit)));
            hash = StableHash.Add(hash, StartupFuelUnits);
            foreach (FacilityDemand facility in Facilities.OrderBy(f => f.Kind))
            {
                hash = StableHash.Add(hash, (int)facility.Kind);
                hash = StableHash.Add(hash, facility.Capacity);
                hash = StableHash.Add(hash, facility.RequiresPower ? 1 : 0);
                hash = StableHash.Add(hash, facility.MustBeRoofed ? 1 : 0);
            }
            foreach (StartupScenario scenario in PowerScenarios.OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                hash = StableHash.Add(hash, scenario.Id);
                hash = StableHash.Add(hash, scenario.DurationHours);
                hash = StableHash.Add(hash, scenario.GenerationWatts);
                hash = StableHash.Add(hash, scenario.LoadWatts);
                hash = StableHash.Add(hash, scenario.StartingBatteryWattDays);
                hash = StableHash.Add(hash, scenario.MinimumEndingBatteryWattDays);
            }
            hash = StableHash.Add(hash, Budget.LocalValueLimit);
            hash = StableHash.Add(hash, Budget.ImportValueLimit);
            hash = StableHash.Add(hash, Budget.TotalPlannedValue);
            foreach (string value in WeaponDefs.OrderBy(v => v, StringComparer.Ordinal)) hash = StableHash.Add(hash, value);
            foreach (string value in ArmorDefs.OrderBy(v => v, StringComparer.Ordinal)) hash = StableHash.Add(hash, value);
            return StableHash.Hex(hash);
        }
    }

    public static class P2ProfileCatalog
    {
        public static readonly PhysicalSettlementProfile IndustrialTemperate = new PhysicalSettlementProfile(
            "industrial-temperate-small", 1, 4,
            SettlementCapability.Electricity | SettlementCapability.Refrigeration | SettlementCapability.ElectricCooking |
            SettlementCapability.FueledCooking | SettlementCapability.Batteries | SettlementCapability.Turrets |
            SettlementCapability.Firearms | SettlementCapability.PreservedFood,
            "BlocksGranite", "Plant_Rice", 4, 1.6, 8, 1.25, 0.052,
            72, 0.9, 150,
            Facilities(true),
            new[]
            {
                new StartupScenario("day-normal", 12, 1000, 850, 300, 200),
                new StartupScenario("night-critical", 12, 1000, 650, 300, 200),
                new StartupScenario("generator-service-buffer", 6, 0, 650, 600, 400)
            },
            new SettlementResourceBudget(18500, 5500, 17600, 4800, 3200, 1800, 600),
            new[] { "Gun_Revolver", "Gun_BoltActionRifle" }, new[] { "Apparel_FlakVest", "Apparel_SimpleHelmet" });

        public static readonly PhysicalSettlementProfile TribalTemperate = new PhysicalSettlementProfile(
            "tribal-temperate-small", 1, 4,
            SettlementCapability.FueledCooking | SettlementCapability.PreservedFood,
            "WoodLog", "Plant_Rice", 4, 1.6, 8, 1.25, 0.052,
            1280, 0.05, 240,
            Facilities(false), Enumerable.Empty<StartupScenario>(),
            new SettlementResourceBudget(12500, 1400, 11800, 900, 1300, 1250, 500),
            new[] { "Bow_Short", "MeleeWeapon_Club" }, new[] { "Apparel_TribalA" });

        private static IEnumerable<FacilityDemand> Facilities(bool electric)
        {
            List<FacilityDemand> result = new List<FacilityDemand>
            {
                new FacilityDemand(FacilityKind.Housing, 4, false, true),
                new FacilityDemand(FacilityKind.Jail, 1, false, true),
                new FacilityDemand(FacilityKind.Kitchen, 1, electric, true),
                new FacilityDemand(FacilityKind.Field, 124, false, false),
                new FacilityDemand(FacilityKind.DiningRecreation, 4, false, true),
                new FacilityDemand(FacilityKind.Workshop, 1, electric, true),
                new FacilityDemand(FacilityKind.BulkStorage, 24, false, true),
                new FacilityDemand(FacilityKind.Medical, 1, false, true),
                new FacilityDemand(FacilityKind.Gatehouse, 2, false, false),
                new FacilityDemand(FacilityKind.Defense, electric ? 4 : 2, electric, false)
            };
            if (electric)
            {
                result.Add(new FacilityDemand(FacilityKind.Freezer, 24, true, true));
                result.Add(new FacilityDemand(FacilityKind.Power, 1, false, false));
            }
            else result.Add(new FacilityDemand(FacilityKind.Pantry, 24, false, true));
            return result;
        }
    }
}
