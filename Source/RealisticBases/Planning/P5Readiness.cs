using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public static class P5Readiness
    {
        public const int RoutineDays = 3;
        public const int CombatHours = 6;
        public const double FoodSafetyFactor = 1.25;
        public const double DefaultDailyNutrition = 1.6;
        public const int MortarShotsPerGunPerHour = 2;
        // Projectiles, not bursts. This is a declared moderate duty rather than
        // a promise to sustain continuous fire at the weapon's maximum rate.
        public const int DirectFireShotsPerGunPerHour = 20;

        public static int FoodUnits(int residents, double dailyNutrition, double nutritionPerUnit, int existingMinimum = 0)
        {
            if (residents <= 0 || !Positive(dailyNutrition) || !Positive(nutritionPerUnit) || existingMinimum < 0)
                throw new ArgumentOutOfRangeException();
            double required = residents * dailyNutrition * RoutineDays * FoodSafetyFactor / nutritionPerUnit;
            return Math.Max(existingMinimum, checked((int)Math.Ceiling(required - 0.000001)));
        }
        public static int ReloadReserve(double capacity, double startingFraction, double perShot, double fuelMultiplier, int existingMinimum)
        {
            if (!Positive(capacity) || !Nonnegative(startingFraction) || startingFraction > 1 ||
                !Positive(perShot) || !Positive(fuelMultiplier) || existingMinimum < 0) throw new ArgumentOutOfRangeException();
            double missing = DirectFireShotsPerGunPerHour * CombatHours * perShot - capacity * startingFraction;
            return Math.Max(existingMinimum, checked((int)Math.Ceiling(Math.Max(0, missing) / fuelMultiplier - 0.000001)));
        }
        internal static bool Positive(double value) => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool Nonnegative(double value) => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class P5GunDuty
    {
        public P5GunDuty(string id, string input, double shotsPerHour, double unitsPerShot, double loadedInputUnits)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(input) ||
                !P5Readiness.Positive(shotsPerHour) || !P5Readiness.Positive(unitsPerShot) ||
                !P5Readiness.Nonnegative(loadedInputUnits)) throw new ArgumentException("Invalid equipment duty");
            Id = id; Input = input; ShotsPerHour = shotsPerHour;
            UnitsPerShot = unitsPerShot; LoadedInputUnits = loadedInputUnits;
        }
        public string Id { get; }
        public string Input { get; }
        public double ShotsPerHour { get; }
        public double UnitsPerShot { get; }
        public double LoadedInputUnits { get; }
        public double RequiredInputUnits => ShotsPerHour * P5Readiness.CombatHours * UnitsPerShot;
    }

    public sealed class P5InputCoverage
    {
        internal P5InputCoverage(string input, double demand, double available)
        { Input = input; Demand = demand; Available = available; }
        public string Input { get; }
        public double Demand { get; }
        public double Available { get; }
        public double Shortfall => Math.Max(0, Demand - Available);
        public bool Covered => Available + 0.00001 >= Demand;
    }

    public sealed class P5CombatCoverage
    {
        public P5CombatCoverage(IEnumerable<P5GunDuty> guns, IReadOnlyDictionary<string, int> looseOperatingInputs)
        {
            var copy = (guns ?? throw new ArgumentNullException(nameof(guns))).ToList();
            if (copy.Select(g => g.Id).Distinct().Count() != copy.Count)
                throw new ArgumentException("Duplicate equipment identity");
            if (looseOperatingInputs == null || looseOperatingInputs.Any(p => p.Value < 0))
                throw new ArgumentException("Invalid finite operating input ledger");
            Guns = new ReadOnlyCollection<P5GunDuty>(copy);
            Inputs = new ReadOnlyCollection<P5InputCoverage>(copy.GroupBy(g => g.Input).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(group => new P5InputCoverage(group.Key, group.Sum(g => g.RequiredInputUnits),
                    group.Sum(g => g.LoadedInputUnits) + (looseOperatingInputs.TryGetValue(group.Key, out int value) ? value : 0))).ToList());
        }
        public IReadOnlyList<P5GunDuty> Guns { get; }
        public IReadOnlyList<P5InputCoverage> Inputs { get; }
        public bool Covered => Inputs.All(i => i.Covered);
    }

    public sealed class P5OutageCoverage
    {
        public P5OutageCoverage(double loadWatts, double reliableGenerationWatts, double startingWattDays,
            int batteries, double hours = 6, double reserveFloorWattDays = 0)
        {
            if (!P5Readiness.Nonnegative(loadWatts) || !P5Readiness.Nonnegative(reliableGenerationWatts) ||
                !P5Readiness.Nonnegative(startingWattDays) || batteries < 0 || !P5Readiness.Positive(hours) ||
                !P5Readiness.Nonnegative(reserveFloorWattDays)) throw new ArgumentOutOfRangeException();
            LoadWatts = loadWatts; GenerationWatts = reliableGenerationWatts; StartingWattDays = startingWattDays;
            Hours = hours; ReserveFloorWattDays = reserveFloorWattDays;
            // Native CompPowerBattery self-discharges at5W per battery. Do not
            // count recharge, variable renewables or a clamp-to-zero as success.
            RequiredWattDays = Math.Max(0, loadWatts + 5 * batteries - reliableGenerationWatts) * hours / 24;
        }
        public double LoadWatts { get; }
        public double GenerationWatts { get; }
        public double StartingWattDays { get; }
        public double Hours { get; }
        public double ReserveFloorWattDays { get; }
        public double RequiredWattDays { get; }
        public double UnclampedEndingWattDays => StartingWattDays - RequiredWattDays;
        public bool Covered => UnclampedEndingWattDays + 0.00001 >= ReserveFloorWattDays;
    }
}
