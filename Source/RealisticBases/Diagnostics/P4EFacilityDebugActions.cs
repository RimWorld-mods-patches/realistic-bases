using System.Linq;
using System;
using LudeonTK;
using RealisticBases.Integration;
using RealisticBases.Generation;
using RealisticBases.Persistence;
using RealisticBases.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace RealisticBases.Diagnostics
{
    // Explicit disposable-playtest probes; no autonomous NPC research/crafting.
    public static class P4EFacilityDebugActions
    {
        private const string Category = "Realistic Bases P4E";

        [DebugAction(Category, "P4E move test arrival to east edge", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EastArrival()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            Map map = Find.CurrentMap;
            if (!(map?.Parent is Settlement) || !map.GetComponent<P0MapState>().Active)
                throw new InvalidOperationException("Requires an active disposable procedural settlement");
            Pawn pawn = map.mapPawns.FreeColonistsSpawned.OrderBy(p => p.thingIDNumber).FirstOrDefault() ??
                throw new InvalidOperationException("No test arrival pawn on this map");
            IntVec3 edge = map.AllCells.Where(c => c.x == map.Size.x - 1 && c.Standable(map))
                .OrderBy(c => c.DistanceToSquared(map.Center)).First();
            pawn.jobs.StopAll(); pawn.DeSpawn(); GenSpawn.Spawn(pawn, edge, map);
            P2Log.Message("P4E_EAST_ARRIVAL", "map=" + map.uniqueID + " player=" + pawn.thingIDNumber + " cell=" + edge);
        }

        [DebugAction(Category, "Retry empty rejected P4 settlement", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void RetryRejectedSettlement()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            P0WorldState world = Current.Game.GetComponent<P0WorldState>();
            Settlement[] candidates = Find.WorldObjects.AllWorldObjects.OfType<Settlement>().Where(s =>
            {
                P0SettlementState saved = world.FindExisting(s.ID);
                return saved != null && !string.IsNullOrEmpty(saved.proceduralLayoutSeed) && saved.generationCount == 0 &&
                    saved.pawnIds.Count == 0 && string.IsNullOrEmpty(saved.proceduralLayoutDigest) &&
                    (s.Map == null || (!s.Map.IsPlayerHome && !s.Map.GetComponent<P0MapState>().Active &&
                        !s.Map.mapPawns.AllPawnsSpawned.Any()));
            }).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("Expected exactly one empty rejected settlement map");
            Settlement settlement = candidates[0];
            P0SettlementState state = world.FindExisting(settlement.ID);
            Pawn arrival = Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.FreeColonistsSpawned)
                .OrderBy(p => p.thingIDNumber).FirstOrDefault() ?? throw new InvalidOperationException("No disposable arrival colonist");
            P2Log.Message("P4E_RETRY_REQUEST", "settlement=" + settlement.ID + " seed=" + state.proceduralLayoutSeed);
            Map map = settlement.Map;
            int layers;
            if (map == null)
            {
                // A rejected pawnless map may already have unloaded. Recreate
                // the same world tile/seed, never select another location.
                int oldLayers = RealisticBasesMod.Settings.forceP4WallLayersNext;
                try
                {
                    RealisticBasesMod.Settings.enableP0Generator = true;
                    RealisticBasesMod.Settings.forceP4WallLayersNext = 3;
                    map = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, new IntVec3(160, 1, 160), null);
                }
                finally { RealisticBasesMod.Settings.forceP4WallLayersNext = oldLayers; }
                if (!map.GetComponent<P0MapState>().Active) throw new InvalidOperationException("Exact rejected-tile retry failed");
                layers = 0; // The fresh procedural plan log records effective depth.
            }
            else
            {
                P4AMapPlanningRun run = P4AMapPlanner.Run(map, settlement, 3);
                if (!P4ProceduralSpawner.TrySpawn(map, settlement, run, out string failure))
                    throw new InvalidOperationException("Exact rejected-map retry failed: " + failure);
                layers = run.Result.Layout.WallLayers;
            }
            foreach (IntVec3 cell in map.AllCells) if (cell.Fogged(map)) map.fogGrid.Unfog(cell);
            IntVec3 target = map.GetComponent<P0MapState>().ArrivalTarget;
            IntVec3 edge = map.AllCells.Where(c => c.OnEdge(map) && c.Standable(map) &&
                map.reachability.CanReach(c, target, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)))
                .OrderBy(c => c.DistanceToSquared(target)).First();
            arrival.DeSpawn();
            GenSpawn.Spawn(arrival, edge, map);
            Current.Game.CurrentMap = map;
            P2Log.Message("P4E_RETRY_COMPLETE", "map=" + map.uniqueID + " seed=" + state.proceduralLayoutSeed +
                " layers=" + (layers == 0 ? "see_plan" : layers.ToString()) + " player=" + arrival.thingIDNumber);
        }

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
