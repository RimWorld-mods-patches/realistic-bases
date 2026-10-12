using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RealisticBases.AI;
using RealisticBases.Core;
using RealisticBases.Planning;
using RimWorld;
using Verse;

namespace RealisticBases.Integration
{
    internal static class P5ReadinessAudit
    {
        private static string F(double number) => number.ToString("F2", CultureInfo.InvariantCulture);
        internal static void Audit(Map map)
        {
            var controller = map.GetComponent<P3GarrisonController>();
            var residents = controller.P5Residents.ToList();
            var owned = controller.P5OwnedThings.ToList();
            var stocks = owned.Where(t => t.def.category == ThingCategory.Item).Concat(residents.SelectMany(p =>
                p.inventory.innerContainer.Concat(p.carryTracker.CarriedThing == null ? Enumerable.Empty<Thing>() :
                    new[] { p.carryTracker.CarriedThing }))).Distinct().Where(t => !t.Destroyed).ToList();
            // Snapshot only owned operating inputs; native sale holder is never
            // credited. Quantities are physical, not an initial-stock counter.
            var loose = stocks.GroupBy(t => t.def.defName).ToDictionary(g => g.Key, g => g.Sum(t => t.stackCount));
            double dailyNutrition = residents.Sum(p => (p.needs?.food?.FoodFallPerTickAssumingCategory(HungerCategory.Fed) ?? 0) * 60000);
            double foodNutrition = 0, stableNutrition = 0;
            foreach (Thing food in stocks.Where(t => t.def.IsIngestible && t.def.ingestible.CachedNutrition > 0))
            {
                // Ready food only; raw ingredients requiring an unproved recipe
                // or any new harvest are not guaranteed ready nutrition.
                if (food.def.ingestible.preferability < FoodPreferability.RawTasty) continue;
                double nutrition = food.GetStatValue(StatDefOf.Nutrition) * food.stackCount;
                foodNutrition += nutrition;
                var rot = food.TryGetComp<CompRottable>();
                float temp = food.Spawned ? food.AmbientTemperature : map.mapTemperature.OutdoorTemp;
                if (rot == null || rot.TicksUntilRotAtTemp(temp) >= P5Readiness.RoutineDays * 60000)
                    stableNutrition += nutrition;
            }
            double requiredNutrition = dailyNutrition * P5Readiness.RoutineDays * P5Readiness.FoodSafetyFactor;
            var duties = new List<P5GunDuty>();
            var mortars = owned.OfType<Building_TurretGun>().Where(g => g.Spawned && g.TryGetComp<CompMannable>() != null).ToList();
            foreach (var gun in owned.OfType<Building_TurretGun>().Where(g => g.Spawned))
            {
                var fuel = gun.TryGetComp<CompRefuelable>();
                var verb = gun.AttackVerb.verbProps;
                double fuelPerProjectile = verb.consumeFuelPerShot +
                    verb.consumeFuelPerBurst / (double)Math.Max(1, verb.burstShotCount);
                if (mortars.Contains(gun))
                {
                    var projectile = gun.gun?.TryGetComp<CompChangeableProjectile>();
                    duties.Add(new P5GunDuty(gun.ThingID, "Shell_HighExplosive", P5Readiness.MortarShotsPerGunPerHour,
                        1, projectile?.LoadedShell == ThingDefOf.Shell_HighExplosive ? projectile.loadedCount : 0));
                    if (fuel != null && !Find.Storyteller.difficulty.classicMortars)
                        duties.Add(new P5GunDuty(gun.ThingID + "-barrel", "MortarBarrelCharges",
                            P5Readiness.MortarShotsPerGunPerHour, fuelPerProjectile, fuel.Fuel));
                }
                else if (fuel != null && fuel.Props.FuelMultiplierCurrentDifficulty > 0 &&
                    fuelPerProjectile > 0)
                    duties.Add(new P5GunDuty(gun.ThingID, fuel.Props.fuelFilter.AnyAllowedDef.defName,
                        P5Readiness.DirectFireShotsPerGunPerHour,
                        fuelPerProjectile / fuel.Props.FuelMultiplierCurrentDifficulty,
                        fuel.Fuel / fuel.Props.FuelMultiplierCurrentDifficulty));
                else P0Log.Message("P5_DUTY_UNSUPPORTED", "map=" + map.uniqueID + " gun=" + gun.ThingID);
            }
            if (mortars.Count > 0)
            {
                int spareBarrels = loose.TryGetValue("ReinforcedBarrel", out int barrels) ? barrels : 0;
                float multiplier = mortars[0].TryGetComp<CompRefuelable>()?.Props.FuelMultiplierCurrentDifficulty ?? 0;
                loose["MortarBarrelCharges"] = (int)Math.Floor(spareBarrels * multiplier);
            }
            var combat = new P5CombatCoverage(duties, loose);
            var power = owned.Where(t => t.Spawned).Select(t => t.TryGetComp<CompPowerTrader>()).Where(p => p != null).ToList();
            double actualLoad = -power.Where(p => p.PowerOn && p.PowerOutput < 0).Sum(p => p.PowerOutput);
            double ratedLoad = power.Sum(p => Math.Max(0, p.Props.PowerConsumption));
            var batteries = owned.Where(t => t.Spawned).Select(t => t.TryGetComp<CompPowerBattery>()).Where(b => b != null).ToList();
            double charge = batteries.Sum(b => b.StoredEnergy);
            var actualOutage = new P5OutageCoverage(actualLoad, 0, charge, batteries.Count);
            var ratedOutage = new P5OutageCoverage(ratedLoad, 0, charge, batteries.Count);
            P0Log.Message("P5_READINESS_AUDIT", "map=" + map.uniqueID + " residents=" + residents.Count +
                " routineDays=3 nutritionPerDay=" + F(dailyNutrition) + " foodNutrition=" + F(foodNutrition) +
                " stableAtCurrentTemp=" + F(stableNutrition) + " requiredNutrition=" + F(requiredNutrition) +
                " foodCovered=" + (stableNutrition + 0.00001 >= requiredNutrition) +
                " combatHours=6 dutyMortarShotsPerGunHour=2 dutyDirectProjectilesPerGunHour=20 guns=" +
                owned.OfType<Building_TurretGun>().Count(g => g.Spawned) + " combatCovered=" + combat.Covered +
                " powerNets=" + power.Select(p => p.PowerNet).Where(n => n != null).Distinct().Count() +
                " actualLoadW=" + F(actualLoad) + " ratedLoadW=" + F(ratedLoad) + " chargeWd=" + F(charge) +
                " outageGenerationW=0 outageHours=6 reserveFloorWd=0 actualOutageCovered=" + actualOutage.Covered +
                " ratedOutageCovered=" + ratedOutage.Covered + " measurementOnly=True futureProductionCredited=0");
            foreach (var input in combat.Inputs)
                P0Log.Message("P5_COMBAT_INPUT", "map=" + map.uniqueID + " input=" + input.Input +
                    " required=" + F(input.Demand) + " physical=" + F(input.Available) + " shortfall=" + F(input.Shortfall));
            foreach (var group in owned.Where(t => t.Spawned && !(t is Building_Turret)).Select(t => new { thing = t,
                fuel = t.TryGetComp<CompRefuelable>() }).Where(x => x.fuel != null && x.fuel.Props.FuelMultiplierCurrentDifficulty > 0)
                .GroupBy(x => x.fuel.Props.fuelFilter.AnyAllowedDef.defName))
            {
                double loaded = group.Sum(x => x.fuel.Fuel / x.fuel.Props.FuelMultiplierCurrentDifficulty);
                double rate = group.Sum(x => (x.fuel.Props.fuelConsumptionRate +
                    (!x.thing.Position.Roofed(map) ? x.fuel.Props.fuelConsumptionPerTickInRain * 60000 : 0)) /
                    x.fuel.Props.FuelMultiplierCurrentDifficulty);
                int stored = loose.TryGetValue(group.Key, out int fuelUnits) ? fuelUnits : 0;
                P0Log.Message("P5_ROUTINE_FUEL", "map=" + map.uniqueID + " input=" + group.Key +
                    " devices=" + group.Count() + " unitsPerDayWorst=" + F(rate) + " loaded=" + F(loaded) +
                    " loose=" + stored + " required=" + F(rate * 3) + " covered=" + (loaded + stored + 0.00001 >= rate * 3));
            }
        }
    }
}
