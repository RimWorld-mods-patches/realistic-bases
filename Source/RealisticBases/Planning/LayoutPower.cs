using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // An open-sky renewable power reservation inside the enclosure. Footprint cells
    // block; the wind clear zone stays unroofed and building-free but walkable.
    public sealed class LayoutPowerYard
    {
        internal LayoutPowerYard(string defName, IEnumerable<GridPoint> cells, GridPoint access, IEnumerable<GridPoint> zone,
            string rotation, int ratedWatts, double capacityFactor)
        {
            DefName = defName;
            Cells = new ReadOnlyCollection<GridPoint>(cells.OrderBy(c => c).ToList());
            Access = access;
            ZoneCells = new ReadOnlyCollection<GridPoint>((zone ?? Enumerable.Empty<GridPoint>()).Distinct().OrderBy(c => c).ToList());
            Rotation = rotation;
            RatedWatts = ratedWatts;
            CapacityFactor = capacityFactor;
        }

        public string DefName { get; }
        public IReadOnlyList<GridPoint> Cells { get; }
        // Repair access: a public street cell next to the footprint.
        public GridPoint Access { get; }
        // Wind turbine clear zone outside the footprint (vanilla wind cells); empty for other sources.
        public IReadOnlyList<GridPoint> ZoneCells { get; }
        public string Rotation { get; }
        public int RatedWatts { get; }
        public double CapacityFactor { get; }
        public int EffectiveWatts => (int)Math.Floor(RatedWatts * CapacityFactor);

        internal ulong AddTo(ulong hash)
        {
            hash = StableHash.Add(hash, DefName);
            foreach (GridPoint c in Cells.Concat(ZoneCells).Concat(new[] { Access }))
            {
                hash = StableHash.Add(hash, c.X);
                hash = StableHash.Add(hash, c.Z);
            }
            hash = StableHash.Add(hash, Rotation);
            return StableHash.Add(hash, EffectiveWatts);
        }
    }

    // The power mix of one layout: a dispatchable floor for the critical load,
    // renewables for the non-critical rest, batteries for the outage bridge and a
    // fuel stock for the horizon.
    public sealed class LayoutPowerPlan
    {
        internal LayoutPowerPlan(int criticalWatts, int nonCriticalWatts, CatalogDef generator, int generators, int batteries,
            IEnumerable<LayoutPowerYard> yards, CatalogDef fuel, int fuelStock, IEnumerable<string> diagnostics)
        {
            CriticalWatts = criticalWatts;
            NonCriticalWatts = nonCriticalWatts;
            GeneratorDef = generator == null ? string.Empty : generator.DefName;
            GeneratorWatts = generator == null ? 0 : -generator.BasePowerConsumption;
            Generators = generators;
            Batteries = batteries;
            Yards = new ReadOnlyCollection<LayoutPowerYard>(yards.ToList());
            FuelDef = fuel == null ? string.Empty : fuel.DefName;
            FuelStock = fuelStock;
            Diagnostics = new ReadOnlyCollection<string>(diagnostics.ToList());
            Digest = ComputeDigest();
        }

        // Load of the non-power rooms at planning time (the power room's own draw is covered
        // by the generator count, see LayoutPowerModel.Generators).
        public int CriticalWatts { get; }
        public int NonCriticalWatts { get; }
        public string GeneratorDef { get; }
        public int GeneratorWatts { get; }
        public int Generators { get; }
        public int Batteries { get; }
        public IReadOnlyList<LayoutPowerYard> Yards { get; }
        public int RenewableWatts => Yards.Sum(y => y.EffectiveWatts);
        // Generator fuel def and the units stocked for LayoutEquipment.FuelHorizonDays.
        public string FuelDef { get; }
        public int FuelStock { get; }
        // power_source_unplaceable:<def>:<reason>, renewables_skipped_rock_protected, ...
        public IReadOnlyList<string> Diagnostics { get; }
        public string Digest { get; }

        public override string ToString() => "critical=" + CriticalWatts + "W nonCritical=" + NonCriticalWatts + "W " + GeneratorDef + "x" + Generators +
            " batteries=" + Batteries + " renewables=" + string.Join(",", Yards.Select(y => y.DefName)) + " (" + RenewableWatts + "W) fuel=" +
            FuelDef + ":" + FuelStock;

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-power");
            hash = StableHash.Add(hash, CriticalWatts);
            hash = StableHash.Add(hash, NonCriticalWatts);
            hash = StableHash.Add(hash, GeneratorDef);
            hash = StableHash.Add(hash, Generators);
            hash = StableHash.Add(hash, Batteries);
            foreach (LayoutPowerYard yard in Yards) hash = yard.AddTo(hash);
            hash = StableHash.Add(hash, FuelDef);
            hash = StableHash.Add(hash, FuelStock);
            foreach (string line in Diagnostics) hash = StableHash.Add(hash, line);
            return StableHash.Hex(hash);
        }
    }

    public static class LayoutPowerModel
    {
        // PROVISIONAL capacity factors (share of rated output counted against load).
        public const double SolarCapacityFactor = 0.5;
        public const double WindCapacityFactor = 0.4;
        public const double WatermillCapacityFactor = 1.0;
        public const double GeothermalCapacityFactor = 1.0;
        // PROVISIONAL: at most this many units of one renewable source.
        public const int MaxUnitsPerSource = 4;
        // PROVISIONAL: batteries bridge the critical load this long (P2 generator-service buffer).
        public const int BatteryBridgeHours = 6;
        // Vanilla battery capacity (Wd) from the plan's evidence.
        public const int BatteryStoredWattDays = 600;

        public const string Geothermal = "GeothermalGenerator";
        public const string Watermill = "WatermillGenerator";
        public const string Solar = "SolarGenerator";
        public const string Wind = "WindTurbine";
        public const string WoodGenerator = "WoodFiredGenerator";
        public const string ChemfuelGenerator = "ChemfuelPoweredGenerator";
        public const string Unplaceable = "power_source_unplaceable";

        // Renewables in preference order.
        public static IReadOnlyList<string> RenewableOrder { get; } = new ReadOnlyCollection<string>(new[] { Geothermal, Watermill, Solar, Wind });
        // Dispatchable generators in preference order.
        public static IReadOnlyList<string> DispatchableOrder { get; } = new ReadOnlyCollection<string>(new[] { WoodGenerator, ChemfuelGenerator });

        public static double CapacityFactor(string defName)
        {
            switch (defName)
            {
                case Solar: return SolarCapacityFactor;
                case Wind: return WindCapacityFactor;
                case Watermill: return WatermillCapacityFactor;
                case Geothermal: return GeothermalCapacityFactor;
                default: return 0;
            }
        }

        // Critical load: freezer coolers, heaters (only placed when the climate needs heat),
        // vitals monitors and powered turrets (none are placed by P4A yet).
        public static bool IsCritical(LayoutRoomKind room, LayoutFixtureKind kind, SettlementRole? role, int watts)
        {
            if (watts <= 0) return false;
            if (kind == LayoutFixtureKind.WallCooler && room == LayoutRoomKind.Freezer) return true;
            if (role == SettlementRole.Heat) return true;
            if (kind == LayoutFixtureKind.MedicalMonitor) return true;
            return role == SettlementRole.Defense;
        }

        public static void Loads(IEnumerable<LayoutRoom> rooms, out int critical, out int nonCritical)
        {
            critical = 0;
            nonCritical = 0;
            foreach (LayoutRoom room in rooms)
                foreach (LayoutFixture fixture in room.Fixtures)
                {
                    if (fixture.Watts <= 0) continue;
                    if (IsCritical(room.Kind, fixture.Kind, fixture.Role, fixture.Watts)) critical += fixture.Watts;
                    else nonCritical += fixture.Watts;
                }
        }

        // Dispatchable units: the critical load plus whatever non-critical load renewables leave uncovered.
        public static int Generators(int critical, int nonCritical, int renewable, int generatorWatts)
        {
            if (generatorWatts <= 0) return 0;
            long need = critical + Math.Max(0, nonCritical - renewable);
            return Math.Max(1, (int)((need + generatorWatts - 1) / generatorWatts));
        }

        public static int Batteries(int criticalWatts) =>
            Math.Max(1, (int)Math.Ceiling(criticalWatts * (double)BatteryBridgeHours / (24.0 * BatteryStoredWattDays)));

        public static int FuelStock(CatalogDef generator, int generators) =>
            generator == null ? 0 : (int)Math.Ceiling(generators * generator.FuelPerDay * LayoutEquipment.FuelHorizonDays);
    }
}
