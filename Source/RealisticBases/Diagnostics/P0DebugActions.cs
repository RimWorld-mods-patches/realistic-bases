using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RealisticBases.AI;
using RealisticBases.Core;
using RealisticBases.Generation;
using RealisticBases.Integration;
using RealisticBases.Persistence;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RealisticBases.Diagnostics
{
    public static class P0DebugActions
    {
        private const string Category = "Realistic Bases P0";
        private const string DisposableSaveName = "RealisticBases-P0-Disposable";
        private const string P3ActiveSaveName = "RealisticBases-P3-Active";
        private static int lastDebugSettlementId = -1;

        [DebugAction(Category, "Save disposable P0 game", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void SaveDisposableGame()
        {
            GameDataSaveLoader.SaveGame(DisposableSaveName);
            P0Log.Message("DISPOSABLE_SAVE_CREATED", "name=" + DisposableSaveName +
                " tick=" + Find.TickManager.TicksGame);
            Messages.Message("Saved isolated P0 game as " + DisposableSaveName,
                MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Category, "Reload disposable P0 game", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void ReloadDisposableGame()
        {
            string path = GenFilePaths.FilePathForSavedGame(DisposableSaveName);
            if (!System.IO.File.Exists(path))
            {
                Messages.Message("No isolated P0 save exists at " + path,
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            P0Log.Message("DISPOSABLE_RELOAD_REQUEST", "name=" + DisposableSaveName +
                " tick=" + Find.TickManager.TicksGame);
            GameDataSaveLoader.CheckVersionAndLoadGame(DisposableSaveName);
        }

        [DebugAction(Category, "Save active P3 test game", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void SaveActiveP3Game()
        {
            GameDataSaveLoader.SaveGame(P3ActiveSaveName);
            P3Log.Message("ACTIVE_SAVE_CREATED", "name=" + P3ActiveSaveName +
                " tick=" + Find.TickManager.TicksGame);
            Messages.Message("Saved active P3 test game as " + P3ActiveSaveName,
                MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Category, "Reload active P3 test game", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void ReloadActiveP3Game()
        {
            string path = GenFilePaths.FilePathForSavedGame(P3ActiveSaveName);
            if (!System.IO.File.Exists(path))
            {
                Messages.Message("No active P3 test save exists at " + path,
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            P3Log.Message("ACTIVE_RELOAD_REQUEST", "name=" + P3ActiveSaveName +
                " tick=" + Find.TickManager.TicksGame);
            GameDataSaveLoader.CheckVersionAndLoadGame(P3ActiveSaveName);
        }

        [DebugAction(Category, "Generate isolated hostile P0 settlement", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void GenerateIsolatedHostileSettlement()
        {
            GenerateIsolatedHostileSettlement(movePlayerColonist: false);
        }

        [DebugAction(Category, "Generate playable hostile P0 settlement", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GeneratePlayableHostileSettlement()
        {
            GenerateIsolatedHostileSettlement(movePlayerColonist: true);
        }

        [DebugAction(Category, "Generate playable mountainous P4 settlement", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GeneratePlayableMountainousP4Settlement()
        {
            GenerateIsolatedHostileSettlement(movePlayerColonist: true, requireMountainous: true);
        }

        private static void GenerateIsolatedHostileSettlement(bool movePlayerColonist, bool requireMountainous = false)
        {
            Pawn arrivalPawn = movePlayerColonist
                ? Find.Maps.Where(m => m.IsPlayerHome)
                    .SelectMany(m => m.mapPawns.FreeColonistsSpawned)
                    .OrderBy(p => p.thingIDNumber)
                    .FirstOrDefault()
                : null;
            if (movePlayerColonist && arrivalPawn == null)
            {
                Messages.Message("No spawned player colonist is available for the playable-arrival test.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            Faction faction = Find.FactionManager.AllFactionsListForReading
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer))
                .OrderBy(f => f.loadID)
                .FirstOrDefault();
            if (faction == null)
            {
                Messages.Message("No active hostile humanlike faction is available.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            RealisticBasesMod.Settings.enableP0Generator = true;
            PlanetTile tile = TileFinder.RandomSettlementTileFor(Faction.OfPlayer, mustBeAutoChoosable: true,
                extraValidator: candidate => Find.WorldGrid[candidate].PrimaryBiome == BiomeDefOf.TemperateForest &&
                    (requireMountainous
                        ? Find.WorldGrid[candidate].hilliness == Hilliness.Mountainous
                        : Find.WorldGrid[candidate].hilliness != Hilliness.Mountainous &&
                            Find.WorldGrid[candidate].hilliness != Hilliness.Impassable));
            Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(tile.LayerDef.SettlementWorldObjectDef);
            settlement.Tile = tile;
            settlement.SetFaction(faction);
            Find.WorldObjects.Add(settlement);
            lastDebugSettlementId = settlement.ID;
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(tile, new IntVec3(80, 1, 80), null);
            Current.Game.CurrentMap = map;
            if (arrivalPawn != null)
            {
                P0MapState mapState = map.GetComponent<P0MapState>();
                IntVec3 arrivalTarget = mapState.Active ? mapState.ArrivalTarget : map.Center;
                List<IntVec3> reachableEdges = map.AllCells
                    .Where(c => c.OnEdge(map) && c.Standable(map) && !c.Fogged(map) && arrivalTarget.IsValid &&
                        map.reachability.CanReach(c, arrivalTarget, PathEndMode.OnCell,
                            TraverseParms.For(TraverseMode.PassDoors)))
                    .OrderBy(c => c.DistanceToSquared(arrivalTarget)).ToList();
                IntVec3 arrivalCell = reachableEdges.Count > 0
                    ? reachableEdges[0]
                    : map.AllCells.Where(c => c.OnEdge(map) && c.Standable(map) && !c.Fogged(map))
                        .OrderBy(c => c.DistanceToSquared(map.Center)).First();
                if (arrivalPawn.Spawned)
                {
                    arrivalPawn.DeSpawn();
                }
                GenSpawn.Spawn(arrivalPawn, arrivalCell, map);
                bool targetReachablePassDoors = arrivalTarget.IsValid && map.reachability.CanReach(arrivalCell, arrivalTarget,
                    PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors));
                P0Log.Message("PLAYABLE_ARRIVAL", "settlement=" + settlement.ID + " map=" + map.uniqueID +
                    " pawn=" + arrivalPawn.thingIDNumber + " cell=" + arrivalCell +
                    " target=" + arrivalTarget + " targetReachablePassDoors=" + targetReachablePassDoors +
                    " currentMap=" + (Current.Game.CurrentMap == map));
            }
            P0Log.Message("DEBUG_SETTLEMENT_READY", "settlement=" + settlement.ID + " map=" + map.uniqueID +
                " tile=" + tile + " faction=" + faction.def.defName + " generator=" + map.generatorDef?.defName);
        }

        [DebugAction(Category, "Reopen last isolated P0 settlement", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void ReopenLastIsolatedSettlement()
        {
            Settlement settlement = Find.WorldObjects.AllWorldObjects
                .OfType<Settlement>()
                .FirstOrDefault(s => s.ID == lastDebugSettlementId);
            if (settlement == null)
            {
                Messages.Message("No isolated P0 settlement from this session is available.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, new IntVec3(80, 1, 80), null);
            Current.Game.CurrentMap = map;
            P0Log.Message("DEBUG_SETTLEMENT_REOPENED", "settlement=" + settlement.ID + " map=" + map.uniqueID +
                " generator=" + map.generatorDef?.defName);
        }

        [DebugAction(Category, "Mark last isolated P0 settlement depleted/damaged", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void MarkLastIsolatedSettlementDepleted()
        {
            if (lastDebugSettlementId < 0)
            {
                Messages.Message("No isolated P0 settlement from this session is available.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            P0SettlementState state = Current.Game.GetComponent<P0WorldState>()
                .GetOrCreate(lastDebugSettlementId, null);
            state.woodRemaining = 0;
            state.markerWallDestroyed = true;
            P0Log.Message("DEBUG_LIFECYCLE_DELTA", "settlement=" + lastDebugSettlementId +
                " woodRemaining=0 markerWallDestroyed=true");
        }

        [DebugAction(Category, "Defeat current P0 settlement", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DefeatCurrentP0Settlement()
        {
            Map map = Find.CurrentMap;
            Settlement settlement = map.Parent as Settlement;
            if (settlement == null || map.generatorDef != RBDefOf.RB_P0_Base_Faction)
            {
                Messages.Message("The current map is not an active P0 settlement.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            Faction faction = settlement.Faction;
            List<Pawn> defenders = map.mapPawns.SpawnedPawnsInFaction(faction)
                .Where(p => p.RaceProps.Humanlike && !p.Dead)
                .ToList();
            foreach (Pawn defender in defenders)
            {
                defender.Kill(null);
            }
            SettlementDefeatUtility.CheckDefeated(settlement);
            P0Log.Message("DEBUG_DEFEAT_REQUEST", "map=" + map.uniqueID + " settlement=" + settlement.ID +
                " defendersKilled=" + defenders.Count + " parentNow=" + map.Parent?.GetType().Name);
        }

        [DebugAction(Category, "Toggle guarded settlement generator", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void ToggleGenerator()
        {
            RealisticBasesMod.Settings.enableP0Generator = !RealisticBasesMod.Settings.enableP0Generator;
            P0Log.Message("DEBUG_TOGGLE", "enableP0Generator=" + RealisticBasesMod.Settings.enableP0Generator);
            Messages.Message("Realistic Bases P0 generator: " +
                (RealisticBasesMod.Settings.enableP0Generator ? "enabled" : "disabled"), MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Category, "Arm next pre-spawn rejection", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void ArmRejection()
        {
            RealisticBasesMod.Settings.forceRejectNext = true;
            P0Log.Message("DEBUG_ARM", "forceRejectNext=true");
        }

        [DebugAction(Category, "Arm next partial-spawn recovery", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void ArmPartialFailure()
        {
            RealisticBasesMod.Settings.forcePartialFailureNext = true;
            P0Log.Message("DEBUG_ARM", "forcePartialFailureNext=true");
        }

        [DebugAction(Category, "Use industrial profile for next generation", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceIndustrialProfile()
        {
            RealisticBasesMod.Settings.forceP2ProfileNext = "industrial";
            P2Log.Message("DEBUG_ARM", "forceP2ProfileNext=industrial");
        }

        [DebugAction(Category, "Use tribal profile for next generation", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceTribalProfile()
        {
            RealisticBasesMod.Settings.forceP2ProfileNext = "tribal";
            P2Log.Message("DEBUG_ARM", "forceP2ProfileNext=tribal");
        }

        [DebugAction(Category, "Log current map generation audit", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogMapAudit()
        {
            Map map = Find.CurrentMap;
            IEnumerable<GenStepDef> baseSteps = map.generatorDef?.genSteps ?? Enumerable.Empty<GenStepDef>();
            IEnumerable<GenStepDef> mutatorSteps = map.TileInfo.Mutators.SelectMany(m => m.extraGenSteps);
            IEnumerable<GenStepDef> biomeSteps = map.Biome.extraGenSteps;
            string ordered = string.Join(",", baseSteps.Concat(mutatorSteps).Concat(biomeSteps)
                .Distinct().OrderBy(s => s.order).ThenBy(s => s.index)
                .Select(s => s.defName + "@" + s.order).ToArray());
            int prohibited = map.listerThings.AllThings.Count(P0MapAudit.IsProhibitedDefense);
            P0Log.Message("MAP_AUDIT", "map=" + map.uniqueID + " generator=" + map.generatorDef?.defName +
                " checksum=" + P0MapAudit.TerrainRoofChecksum(map) + " prohibitedDefenses=" + prohibited +
                " GL=" + ModsConfig.IsActive("m00nl1ght.geologicallandforms") +
                " MapPreview=" + ModsConfig.IsActive("m00nl1ght.mappreview") +
                " mutators=" + string.Join(",", map.TileInfo.Mutators.Select(m => m.defName).ToArray()) +
                " orderedSteps=" + ordered);
        }

        [DebugAction(Category, "Show current P1 planning overlay", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ShowP1PlanningOverlay()
        {
            P1PlanDiagnostics.ShowOverlay(Find.CurrentMap);
            Messages.Message("P1 overlay: suitability, rooms, interactions, routes, gate, roof support, and planned-only power/defense reservations.",
                MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction(Category, "Log current P1 generation report", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogP1GenerationReport()
        {
            P1PlanDiagnostics.LogReport(Find.CurrentMap);
        }

        [DebugAction(Category, "Show current P2 settlement overlay", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ShowP2PlanningOverlay()
        {
            P2PlanDiagnostics.ShowOverlay(Find.CurrentMap);
            Messages.Message("P2 overlay: facilities, interactions, gates, and independent attack/retreat routes.",
                MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction(Category, "Log current P2 settlement report", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogP2GenerationReport()
        {
            P2PlanDiagnostics.LogReport(Find.CurrentMap);
        }

        [DebugAction(Category, "Audit current P2 settlement runtime", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AuditP2Runtime()
        {
            P2RuntimeAudit.Log(Find.CurrentMap);
        }

        [DebugAction(Category, "Start P2 generator-service scenario", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StartP2GeneratorServiceScenario()
        {
            P2RuntimeAudit.StartGeneratorServiceScenario(Find.CurrentMap);
        }

        [DebugAction(Category, "Audit current P3 defensive encounter", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AuditP3Encounter()
        {
            Find.CurrentMap?.GetComponent<P3GarrisonController>()?.Audit("manual");
        }

        [DebugAction(Category, "P3 breach one generated gate", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void P3BreachGate() => Find.CurrentMap?.GetComponent<P3GarrisonController>()?.DebugBreachGate();

        [DebugAction(Category, "P3 invalidate outer defensive posts", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void P3InvalidateOuterPosts() =>
            Find.CurrentMap?.GetComponent<P3GarrisonController>()?.DebugInvalidateOuterPosts();

        [DebugAction(Category, "P3 exhaust mortar ammunition", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void P3ExhaustMortarAmmunition() =>
            Find.CurrentMap?.GetComponent<P3GarrisonController>()?.DebugExhaustMortarAmmunition();

        [DebugAction(Category, "P3 remove mortar operator", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void P3RemoveMortarOperator() =>
            Find.CurrentMap?.GetComponent<P3GarrisonController>()?.DebugRemoveMortarOperator();

        [DebugAction(Category, "P3 cut settlement power", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void P3CutPower() => Find.CurrentMap?.GetComponent<P3GarrisonController>()?.DebugCutPower();

        [DebugAction(Category, "P3 drain one defender needs", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void P3DrainNeeds() => Find.CurrentMap?.GetComponent<P3GarrisonController>()?.DebugDrainNeeds();

        [DebugAction(Category, "P3 set local time to 23h", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void P3SetNight()
        {
            Map map = Find.CurrentMap;
            int currentHour = GenLocalDate.HourOfDay(map);
            int deltaHours = (23 - currentHour + 24) % 24;
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame + deltaHours * 2500);
            P3Log.Message("SCENARIO_NIGHT", "map=" + map.uniqueID + " previousHour=" + currentHour +
                " currentHour=" + GenLocalDate.HourOfDay(map));
        }

        [DebugAction(Category, "Spawn hostile garrison/work probe", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnHostileWorkProbe()
        {
            Map map = Find.CurrentMap;
            IntVec3 center = UI.MouseCell();
            SpawnHostileWorkProbeAt(map, center);
        }

        [DebugAction(Category, "Spawn hostile garrison/work probe automatically", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnHostileWorkProbeAutomatically()
        {
            Map map = Find.CurrentMap;
            for (int z = 12; z < map.Size.z - 12; z += 4)
            {
                for (int x = 12; x < map.Size.x - 12; x += 4)
                {
                    IntVec3 center = new IntVec3(x, 0, z);
                    if (CanUseProbeArea(map, center))
                    {
                        SpawnHostileWorkProbeAt(map, center);
                        return;
                    }
                }
            }

            Messages.Message("No clear, standable 17x13 probe area exists on this map.",
                MessageTypeDefOf.RejectInput, false);
        }

        private static void SpawnHostileWorkProbeAt(Map map, IntVec3 center)
        {
            Faction faction = Find.FactionManager.AllFactionsListForReading
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer))
                .OrderBy(f => f.loadID)
                .FirstOrDefault();
            if (faction == null)
            {
                Messages.Message("No active hostile humanlike faction is available.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (!CanUseProbeArea(map, center))
            {
                Messages.Message("Choose a clear, standable 17x13 area.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            List<Thing> spawned = new List<Thing>();
            try
            {
                for (int x = -6; x <= 6; x++)
                {
                    Thing conduit = SpawnThing(ThingDefOf.PowerConduit, center + new IntVec3(x, 0, 0), map, faction, Rot4.North);
                    spawned.Add(conduit);
                }

                Thing generator = SpawnThing(DefDatabase<ThingDef>.GetNamed("WoodFiredGenerator"),
                    center + new IntVec3(-6, 0, 0), map, faction, Rot4.North);
                Thing stove = SpawnThing(DefDatabase<ThingDef>.GetNamed("FueledStove"),
                    center + new IntVec3(0, 0, 1), map, faction, Rot4.North);
                Building_PlantGrower grower = (Building_PlantGrower)SpawnThing(
                    DefDatabase<ThingDef>.GetNamed("HydroponicsBasin"), center + new IntVec3(6, 0, 0), map, faction, Rot4.North);
                spawned.Add(generator);
                spawned.Add(stove);
                spawned.Add(grower);

                generator.TryGetComp<CompRefuelable>()?.ConsumeFuel(9999f);
                stove.TryGetComp<CompRefuelable>()?.ConsumeFuel(9999f);
                grower.SetPlantDefToGrow(DefDatabase<ThingDef>.GetNamed("Plant_Rice"));
                ((IBillGiver)stove).BillStack.AddBill(new Bill_Production(DefDatabase<RecipeDef>.GetNamed("CookMealSimple")));

                Thing wood = ThingMaker.MakeThing(ThingDefOf.WoodLog);
                wood.stackCount = 60;
                GenSpawn.Spawn(wood, center + new IntVec3(-2, 0, -2), map);
                spawned.Add(wood);
                Thing rice = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RawRice"));
                rice.stackCount = 20;
                GenSpawn.Spawn(rice, center + new IntVec3(0, 0, -2), map);
                spawned.Add(rice);

                PawnKindDef kind = faction.def.basicMemberKind ?? PawnKindDefOf.Pirate;
                Pawn pawnA = PawnGenerator.GeneratePawn(kind, faction, map.Tile);
                Pawn pawnB = PawnGenerator.GeneratePawn(kind, faction, map.Tile);
                GenSpawn.Spawn(pawnA, center + new IntVec3(-1, 0, -4), map);
                GenSpawn.Spawn(pawnB, center + new IntVec3(1, 0, -4), map);
                spawned.Add(pawnA);
                spawned.Add(pawnB);
                foreach (Pawn pawn in new[] { pawnA, pawnB })
                {
                    pawn.skills?.GetSkill(SkillDefOf.Cooking)?.EnsureMinLevelWithMargin(10);
                    pawn.skills?.GetSkill(SkillDefOf.Plants)?.EnsureMinLevelWithMargin(10);
                }

                List<Pawn> pawns = new[] { pawnA, pawnB }.OrderBy(p => p.thingIDNumber).ToList();
                IntVec3 holdPost = center + new IntVec3(0, 0, 5);
                Lord lord = LordMaker.MakeNewLord(faction, new LordJob_P0Garrison(holdPost, center), map, pawns);
                map.powerNetManager.UpdatePowerNetsAndConnections_First();
                map.GetComponent<P0WorkProbe>().Initialize(pawns[0], pawns[1], generator, stove, grower, holdPost);

                P0Log.Message("WORK_PROBE_SPAWNED", "map=" + map.uniqueID + " faction=" + faction.def.defName +
                    " lord=" + lord.loadID + " finiteWood=60 finiteRice=20 noPlayerFactionChange=true spawnedThings=" + spawned.Count);
                Messages.Message("P0 hostile work probe created. Let the game run; use 'Damage P0 hold pawn' after it reaches its post.",
                    MessageTypeDefOf.TaskCompletion, false);
            }
            catch (Exception ex)
            {
                for (int i = spawned.Count - 1; i >= 0; i--)
                {
                    if (!spawned[i].Destroyed)
                    {
                        spawned[i].Destroy(DestroyMode.Vanish);
                    }
                }
                P0Log.Error("WORK_PROBE_SPAWN_FAILED", "exception=" + ex);
                Messages.Message("P0 work probe failed; spawned probe objects were removed. See log.", MessageTypeDefOf.NegativeEvent, false);
            }
        }

        [DebugAction(Category, "Damage P0 hold pawn", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DamageHoldPawn()
        {
            Find.CurrentMap.GetComponent<P0WorkProbe>().DamageHoldPawn();
        }

        [DebugAction(Category, "Deplete stock and destroy marker wall", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MutateLifecycleEvidence()
        {
            P0MapState state = Find.CurrentMap.GetComponent<P0MapState>();
            if (state == null || !state.Active)
            {
                Messages.Message("This is not an active P0 settlement map.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            P0Log.Message("LIFECYCLE_MUTATION_REQUEST", "map=" + Find.CurrentMap.uniqueID +
                " use normal play or destroy/loot tools; state is captured on map removal");
            Messages.Message("Destroy the north-west marker wall and remove the central wood stack, then retreat normally.",
                MessageTypeDefOf.NeutralEvent, false);
        }

        private static bool CanUseProbeArea(Map map, IntVec3 center)
        {
            CellRect rect = CellRect.CenteredOn(center, 8, 6);
            if (!rect.FullyContainedWithin(map.BoundsRect(5)))
            {
                return false;
            }
            return rect.All(c => c.Standable(map) && c.GetEdifice(map) == null && !c.Fogged(map));
        }

        private static Thing SpawnThing(ThingDef def, IntVec3 cell, Map map, Faction faction, Rot4 rotation)
        {
            ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            Thing thing = ThingMaker.MakeThing(def, stuff);
            thing.SetFactionDirect(faction);
            return GenSpawn.Spawn(thing, cell, map, rotation, WipeMode.Vanish);
        }
    }
}
