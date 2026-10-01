using System.Linq;
using LudeonTK;
using RealisticBases.Integration;
using RimWorld;
using Verse;

namespace RealisticBases.Diagnostics
{
    // Explicit disposable-playtest probes; no autonomous NPC research/crafting.
    public static class P4EFacilityDebugActions
    {
        private const string Category = "Realistic Bases P4E";

        [DebugAction(Category, "Audit P4E facilities", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void Audit()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            foreach (Thing bench in map.listerThings.AllThings.Where(t => t is Building_ResearchBench || t is Building_WorkTable)
                .OrderBy(t => t.thingIDNumber))
            {
                Room room = bench.GetRoom();
                if (room == null) continue;
                Thing[] climate = room.ContainedAndAdjacentThings.Where(t => t.TryGetComp<CompTempControl>() != null).ToArray();
                CompAffectedByFacilities facilities = bench.TryGetComp<CompAffectedByFacilities>();
                Thing[] linked = facilities?.LinkedFacilitiesListForReading.ToArray() ?? new Thing[0];
                P2Log.Message("P4E_FACILITY_AUDIT", "map=" + map.uniqueID + " tick=" + Find.TickManager.TicksGame +
                    " bench=" + bench.def.defName + " id=" + bench.thingIDNumber + " rotation=" + bench.Rotation +
                    " working=" + bench.InteractionCell + " workCellStandable=" + bench.InteractionCell.Standable(map) +
                    " seat=" + (bench.InteractionCell.GetEdifice(map)?.def.building?.isSittable == true) +
                    " temperature=" + room.Temperature.ToString("F2") + " outdoor=" + map.mapTemperature.OutdoorTemp.ToString("F2") +
                    " outdoors=" + room.UsesOutdoorTemperature + " powered=" + (bench.TryGetComp<CompPowerTrader>()?.PowerOn.ToString() ?? "not_required") +
                    " climate=[" + string.Join(";", climate.Select(t => t.def.defName + "@" + t.Position +
                        ":target=" + t.TryGetComp<CompTempControl>().targetTemperature +
                        ":power=" + (t.TryGetComp<CompPowerTrader>()?.PowerOn.ToString() ?? "fuel") +
                        ":fuel=" + (t.TryGetComp<CompRefuelable>()?.Fuel.ToString("F2") ?? "none"))) + "]" +
                    " analyzers=" + linked.Count(t => t.def.defName == "MultiAnalyzer") +
                    " activeAnalyzers=" + linked.Count(t => t.def.defName == "MultiAnalyzer" && facilities.IsFacilityActive(t)));
            }
            Thing[] medicine = map.listerThings.AllThings.Where(t => t.def.IsMedicine).ToArray();
            P2Log.Message("P4E_MEDICINE_AUDIT", "map=" + map.uniqueID + " units=" + medicine.Sum(t => t.stackCount) +
                " shelves=" + medicine.Count(t => t.Position.GetEdifice(map) is Building_Storage) +
                " stacks=" + medicine.Length + " ids=" + string.Join(",", medicine.Select(t => t.thingIDNumber + ":" + t.stackCount)));
        }

        [DebugAction(Category, "Start P4E hot climate probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void Hot() => StartClimate(GameConditionDefOf.HeatWave);

        [DebugAction(Category, "Start P4E cold climate probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void Cold() => StartClimate(GameConditionDefOf.ColdSnap);

        private static void StartClimate(GameConditionDef def)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            foreach (GameCondition condition in map.gameConditionManager.ActiveConditions.ToArray())
                if (condition.def == GameConditionDefOf.HeatWave || condition.def == GameConditionDefOf.ColdSnap) condition.End();
            map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(def, 60000));
            P2Log.Message("P4E_CLIMATE_PROBE", "map=" + map.uniqueID + " condition=" + def.defName +
                " tick=" + Find.TickManager.TicksGame);
        }
    }
}
