using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Generation;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace RealisticBases.Integration
{
    public sealed class P2RuntimeState
    {
        public Settlement Settlement;
        public P2PlanningRun Run;
        public List<int> SpawnedThingIds;
        public List<int> ResidentIds;
        public int InternalFuel;
        public int InitializedCrops;
        public int ServiceScenarioStartTick = -1;
    }

    public static class P2RuntimeAudit
    {
        private static readonly Dictionary<int, P2RuntimeState> States = new Dictionary<int, P2RuntimeState>();

        public static void Initialize(Map map, Settlement settlement, P2PlanningRun run, IEnumerable<Thing> spawned,
            IEnumerable<Pawn> residents, int internalFuel, int initializedCrops)
        {
            States[map.uniqueID] = new P2RuntimeState
            {
                Settlement = settlement,
                Run = run,
                SpawnedThingIds = spawned.Select(t => t.thingIDNumber).ToList(),
                ResidentIds = residents.Select(p => p.thingIDNumber).ToList(),
                InternalFuel = internalFuel,
                InitializedCrops = initializedCrops
            };
            Log(map, "initial");
        }

        public static void Log(Map map, string phase = "manual")
        {
            P2RuntimeState state;
            if (map == null || !States.TryGetValue(map.uniqueID, out state))
            {
                P2Log.Message("RUNTIME_AUDIT", "map=" + map?.uniqueID + " phase=" + phase + " unavailable=no_state");
                return;
            }
            PhysicalSettlementPlan plan = state.Run.Result.Plan;
            PhysicalSettlementProfile profile = state.Run.Input.Profile;
            Faction faction = state.Settlement.Faction;
            List<Thing> things = map.listerThings.AllThings.Where(t => state.SpawnedThingIds.Contains(t.thingIDNumber)).ToList();
            List<Building_Bed> beds = things.OfType<Building_Bed>().ToList();
            int ordinaryBeds = beds.Count(b => !b.ForPrisoners && !b.Medical);
            int prisonBeds = beds.Count(b => b.ForPrisoners);
            int medicalBeds = beds.Count(b => b.Medical);
            int wrongFactionBuildings = things.OfType<Building>().Count(b => b.def.CanHaveFaction && b.Faction != faction);
            int gateDoors = plan.Gates.Count(g =>
            {
                Building_Door door = ToCell(g).GetEdifice(map) as Building_Door;
                return door != null && door.Faction == faction;
            });
            int usableInteractions = 0;
            int interactionBuildings = 0;
            foreach (Thing thing in things.Where(t => t.def.hasInteractionCell))
            {
                interactionBuildings++;
                IntVec3 cell = thing.InteractionCell;
                if (cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null) usableInteractions++;
            }
            int reachableFacilities = 0;
            IntVec3 entry = ToCell(plan.Gates[0]);
            foreach (PhysicalFacility facility in plan.Facilities)
            {
                IntVec3 target = facility.InteractionCells.Count > 0 ? ToCell(facility.InteractionCells[0]) : ToCell(facility.Door);
                if (target.InBounds(map) && map.reachability.CanReach(entry, target, PathEndMode.OnCell,
                    TraverseParms.For(TraverseMode.PassDoors))) reachableFacilities++;
            }
            bool attackTopology = RouteEndpointReachable(map, plan.AttackRoute, plan.Gates[0]);
            bool retreatTopology = RouteEndpointReachable(map, plan.RetreatRoute, plan.Gates[1]);
            int roofedFacilityCells = plan.Facilities.Where(f => f.Roofed).SelectMany(f => f.Bounds.Points())
                .Count(c => map.roofGrid.Roofed(ToCell(c)));
            int requiredRoofedCells = plan.Facilities.Where(f => f.Roofed).Sum(f => f.Bounds.Area);
            Thing generator = things.FirstOrDefault(t => t.def.defName == "WoodFiredGenerator");
            Thing battery = things.FirstOrDefault(t => t.TryGetComp<CompPowerBattery>() != null);
            Thing cooler = things.FirstOrDefault(t => t.def.defName == "Cooler");
            CompPowerTrader generatorPower = generator?.TryGetComp<CompPowerTrader>();
            CompPowerBattery batteryComp = battery?.TryGetComp<CompPowerBattery>();
            CompPowerTrader coolerPower = cooler?.TryGetComp<CompPowerTrader>();
            bool onePowerNetwork = !profile.Has(SettlementCapability.Electricity) ||
                (generatorPower?.PowerNet != null && generatorPower.PowerNet == batteryComp?.PowerNet &&
                 generatorPower.PowerNet == coolerPower?.PowerNet);
            PhysicalFacility preserve = plan.Facilities.Single(f => f.Kind ==
                (profile.Has(SettlementCapability.Electricity) ? FacilityKind.Freezer : FacilityKind.Pantry));
            IntVec3 preserveCenter = ToCell(new GridPoint(preserve.Bounds.MinX + preserve.Bounds.Width / 2,
                preserve.Bounds.MinZ + preserve.Bounds.Height / 2));
            float preserveTemperature = preserveCenter.GetTemperature(map);
            bool exhaustValid = true;
            if (cooler != null)
            {
                IntVec3 cold = cooler.Position + IntVec3.South.RotatedBy(cooler.Rotation);
                IntVec3 hot = cooler.Position + IntVec3.North.RotatedBy(cooler.Rotation);
                exhaustValid = preserve.Bounds.Contains(new GridPoint(cold.x, cold.z)) &&
                    !preserve.Bounds.Contains(new GridPoint(hot.x, hot.z)) && !map.roofGrid.Roofed(hot) && hot.Standable(map);
            }
            int foodUnits = things.Where(t => t.def.defName == (profile.Has(SettlementCapability.Electricity) ? "MealSimple" : "Pemmican"))
                .Sum(t => t.stackCount);
            int looseFuel = things.Where(t => t.def == ThingDefOf.WoodLog).Sum(t => t.stackCount);
            int crops = things.OfType<Plant>().Count(p => p.def.defName == profile.CropDef && p.sown);
            float averageGrowth = crops == 0 ? 0f : things.OfType<Plant>().Where(p => p.def.defName == profile.CropDef && p.sown).Average(p => p.Growth);
            int turrets = things.Count(t => t.def.defName == "Turret_MiniTurret");
            int cover = things.Count(t => t.def.defName == "Sandbags" || t.def.defName == "Barricade");
            int prohibited = map.listerThings.AllThings.Count(P0MapAudit.IsProhibitedDefense);
            int equippedResidents = map.mapPawns.AllPawnsSpawned.Count(p => state.ResidentIds.Contains(p.thingIDNumber) && p.equipment?.Primary != null);
            bool tribalClean = profile.Has(SettlementCapability.Electricity) || !things.Any(t =>
                t.TryGetComp<CompPower>() != null || t.def.defName == "Cooler" || t.def.defName == "Turret_MiniTurret");
            float netGainWatts = generatorPower?.PowerNet == null ? 0f :
                generatorPower.PowerNet.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
            int serviceElapsed = state.ServiceScenarioStartTick < 0 ? 0 : GenTicks.TicksGame - state.ServiceScenarioStartTick;
            bool servicePassed = state.ServiceScenarioStartTick >= 0 && serviceElapsed >= 15000 &&
                (batteryComp?.StoredEnergy ?? 0f) >= 400f && (generator?.TryGetComp<CompRefuelable>()?.Fuel ?? 1f) <= 0f;
            P2Log.Message("RUNTIME_AUDIT", "map=" + map.uniqueID + " phase=" + phase + " profile=" + profile.Id +
                " hour=" + GenLocalDate.HourOfDay(map) + " ticks=" + GenTicks.TicksGame +
                " facilitiesReachable=" + reachableFacilities + "/" + plan.Facilities.Count +
                " ordinaryBeds=" + ordinaryBeds + "/" + profile.Residents + " prisonBeds=" + prisonBeds +
                " medicalBeds=" + medicalBeds + " gateFactionDoors=" + gateDoors + "/" + plan.Gates.Count +
                " attackTopology=" + attackTopology + " retreatTopology=" + retreatTopology +
                " interactionsUsable=" + usableInteractions + "/" + interactionBuildings +
                " roofedCells=" + roofedFacilityCells + "/" + requiredRoofedCells +
                " wrongFactionBuildings=" + wrongFactionBuildings + " onePowerNetwork=" + onePowerNetwork +
                " generatorFuel=" + (generator?.TryGetComp<CompRefuelable>()?.Fuel.ToString("F1") ?? "none") +
                " generatorPowerOn=" + (generatorPower?.PowerOn.ToString() ?? "none") +
                " netGainWatts=" + netGainWatts.ToString("F1") +
                " batteryWd=" + (batteryComp?.StoredEnergy.ToString("F1") ?? "none") +
                " serviceElapsed=" + serviceElapsed + "/15000 servicePassed=" + servicePassed +
                " coolerPowerOn=" + (coolerPower?.PowerOn.ToString() ?? "none") +
                " preserveTempC=" + preserveTemperature.ToString("F1") + " exhaustValid=" + exhaustValid +
                " foodUnits=" + foodUnits + "/" + profile.StartupFoodUnits +
                " nutrition=" + (foodUnits * profile.NutritionPerFoodUnit).ToString("F1") +
                "/" + profile.RequiredStartupNutrition.ToString("F1") + " looseFuel=" + looseFuel +
                " internalFuel=" + state.InternalFuel + " crops=" + crops + "/" + profile.RequiredCropCells +
                " averageGrowth=" + averageGrowth.ToString("F2") + " equippedResidents=" + equippedResidents +
                "/" + profile.Residents + " turrets=" + turrets + " cover=" + cover +
                " tribalClean=" + tribalClean + " prohibitedDefenses=" + prohibited);
        }

        public static void StartGeneratorServiceScenario(Map map)
        {
            P2RuntimeState state;
            if (map == null || !States.TryGetValue(map.uniqueID, out state) ||
                !state.Run.Input.Profile.Has(SettlementCapability.Electricity))
            {
                P2Log.Error("POWER_SCENARIO", "map=" + map?.uniqueID + " startFailed=no_industrial_state");
                return;
            }
            List<Thing> things = map.listerThings.AllThings.Where(t => state.SpawnedThingIds.Contains(t.thingIDNumber)).ToList();
            Thing generator = things.FirstOrDefault(t => t.def.defName == "WoodFiredGenerator");
            CompRefuelable fuel = generator?.TryGetComp<CompRefuelable>();
            CompPowerBattery battery = things.Select(t => t.TryGetComp<CompPowerBattery>()).FirstOrDefault(c => c != null);
            if (fuel == null || battery == null)
            {
                P2Log.Error("POWER_SCENARIO", "map=" + map.uniqueID + " startFailed=missing_generator_or_battery");
                return;
            }
            battery.SetStoredEnergyPct(1f);
            fuel.allowAutoRefuel = false;
            fuel.ConsumeFuel(99999f);
            int shedLoads = 0;
            foreach (Thing thing in things.Where(t => t.def.defName == "ElectricStove" || t.def.defName == "ElectricSmithy"))
            {
                CompFlickable flickable = thing.TryGetComp<CompFlickable>();
                if (flickable == null || !flickable.SwitchIsOn) continue;
                flickable.SwitchIsOn = false;
                shedLoads++;
            }
            state.ServiceScenarioStartTick = GenTicks.TicksGame;
            P2Log.Message("POWER_SCENARIO", "map=" + map.uniqueID + " scenario=generator-service-buffer startTick=" +
                state.ServiceScenarioStartTick + " durationTicks=15000 startingBatteryWd=" + battery.StoredEnergy.ToString("F1") +
                " generatorFuel=" + fuel.Fuel.ToString("F1") + " autoRefuel=" + fuel.allowAutoRefuel +
                " shedNoncriticalLoads=" + shedLoads);
        }

        private static bool RouteEndpointReachable(Map map, IReadOnlyList<GridPoint> route, GridPoint gate)
        {
            if (route.Count == 0) return false;
            IntVec3 edge = ToCell(route[route.Count - 1]);
            IntVec3 target = ToCell(gate);
            return edge.OnEdge(map) && map.reachability.CanReach(edge, target, PathEndMode.OnCell,
                TraverseParms.For(TraverseMode.PassDoors));
        }

        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }
}
