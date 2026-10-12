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
        internal static bool FailNextMagazineStocks;

        [DebugAction(Category, "Start P4E autocannon firing probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AutocannonFiring() => Find.CurrentMap.GetComponent<P4ETurretProbe>().StartFiring();
        [DebugAction(Category, "Start P4E autocannon rearm probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AutocannonRearm() => Find.CurrentMap.GetComponent<P4ETurretProbe>().StartRearm();
        [DebugAction(Category, "Start P4E autocannon power loss probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AutocannonPowerLoss() => Find.CurrentMap.GetComponent<P4ETurretProbe>().StartPowerLoss();
        [DebugAction(Category, "Start P4E autocannon close support probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AutocannonCloseSupport() => Find.CurrentMap.GetComponent<P4ETurretProbe>().StartCloseSupport();

        [DebugAction(Category, "P4E pop funded logistics smoke once", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SmokeOnce() => Find.CurrentMap.GetComponent<RealisticBases.AI.P3GarrisonController>().DebugLogisticsSmoke();

        [DebugAction(Category, "Capture rejected P4 terrain fixture", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void CaptureRejected()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            Map[] maps = Find.Maps.Where(m => !m.IsPlayerHome && !m.GetComponent<P0MapState>().Active &&
                m.Parent is Settlement s && Current.Game.GetComponent<P0WorldState>().FindExisting(s.ID) is P0SettlementState state &&
                state.generationCount == 0 && !string.IsNullOrEmpty(state.proceduralLayoutSeed)).ToArray();
            if (maps.Length != 1) throw new InvalidOperationException("Requires one loaded rejected settlement map");
            P0DebugActions.CaptureFlatTerrainFixture(maps[0]);
        }

        [DebugAction(Category, "Prefer open-ground P4E artillery once", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void PreferArtillery()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            P4AMapPlanner.DebugMortarBiasNext = 100;
            P2Log.Message("P4E_DOCTRINE_OVERRIDE_ARM", "preferOpenGround=True once=True legalityAndBudgetUnchanged=True");
        }

        [DebugAction(Category, "Use nine-resident P4E budget once", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void NineResidents() => FundedWealth(135);

        [DebugAction(Category, "Use ten-resident P4E budget once", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void TenResidents() => FundedWealth(150);

        private static void FundedWealth(int wealth)
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            P4AMapPlanner.DebugWealthNext = wealth;
            P2Log.Message("P4E_WEALTH_OVERRIDE_ARM", "wealth=" + wealth + " fundedRoster=True once=True");
        }

        [DebugAction(Category, "Show P4E mortar position", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void ShowMortar()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            Map map = Find.CurrentMap;
            Thing[] guns = map?.listerThings.AllThings.Where(t => t.def == ThingDefOf.Turret_Mortar).ToArray() ?? new Thing[0];
            if (guns.Length != 1) throw new InvalidOperationException("Requires exactly one mortar on the selected map");
            Find.World.renderer.wantedMode = WorldRenderMode.None;
            Find.CameraDriver.JumpToCurrentMapLoc(guns[0].InteractionCell);
        }

        [DebugAction(Category, "Start P4E mortar magazine loading probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LoadingProbe() => Find.CurrentMap?.GetComponent<P4EArtilleryProbe>().Start();

        [DebugAction(Category, "P4E move funded spare barrels to warehouse", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WarehouseBarrels()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("Disposable profile only");
            Map map = Find.CurrentMap;
            var barrels = map.listerThings.AllThings.Where(t => t.def.defName == "ReinforcedBarrel").ToArray();
            int before = barrels.Sum(t => t.stackCount);
            if (before != 2 || map.GetComponent<RealisticBases.AI.P3GarrisonController>().DebugOperatingStock("ReinforcedBarrel") != before)
                throw new InvalidOperationException("Requires unchanged funded two-barrel reserve");
            var shelves = map.listerThings.AllThings.OfType<Building_Storage>().Where(s => s.GetRoom()?.CellCount > 1 &&
                s.GetStoreSettings().AllowedToAccept(ThingDefOf.ReinforcedBarrel)).OrderBy(s => s.thingIDNumber).ToArray();
            foreach (Thing stock in barrels)
            {
                IntVec3 cell = shelves.SelectMany(s => s.OccupiedRect().Cells).FirstOrDefault(c =>
                    c.GetThingList(map).Count(t => t.def.category == ThingCategory.Item) < c.GetMaxItemsAllowedInCell(map));
                if (!cell.IsValid || cell.GetEdifice(map) == null) throw new InvalidOperationException("No funded warehouse shelf space");
                stock.DeSpawn(); GenSpawn.Spawn(stock, cell, map);
                P2Log.Message("P4E_WAREHOUSE_BARREL_PROBE", "map=" + map.uniqueID + " id=" + stock.ThingID +
                    " count=" + stock.stackCount + " cell=" + cell + " addedSupplies=0");
            }
            if (map.GetComponent<RealisticBases.AI.P3GarrisonController>().DebugOperatingStock("ReinforcedBarrel") != before)
                throw new InvalidOperationException("Funded barrel ledger changed during move");
        }

        [DebugAction(Category, "P4E fail next generation after magazine stocks", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void FailMagazine()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            FailNextMagazineStocks = true;
            P2Log.Message("P4E_MAGAZINE_FAILURE_ARM", "afterFiniteStocks=True");
        }

        [DebugAction(Category, "Audit P4E mortar logistics", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void MortarLogistics()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            foreach (Thing gun in map.listerThings.AllThings.Where(t => t.def == ThingDefOf.Turret_Mortar))
            {
                Pawn op = gun.TryGetComp<CompMannable>()?.ManningPawn;
                CompChangeableProjectile shell = (gun as Building_TurretGun)?.gun?.TryGetComp<CompChangeableProjectile>();
                Thing[] stocks = map.listerThings.AllThings.Where(t => (t.def.IsShell || t.def.defName == "ReinforcedBarrel") &&
                    t.Position.GetEdifice(map) is Building_Storage && t.GetRoom()?.CellCount == 1).OrderBy(t => t.thingIDNumber).ToArray();
                P2Log.Message("P4E_MORTAR_LOGISTICS", "map=" + map.uniqueID + " gun=" + gun.thingIDNumber +
                    " rotation=" + gun.Rotation + " operatingCell=" + gun.InteractionCell + " operator=" + (op?.thingIDNumber.ToString() ?? "none") +
                    " loaded=" + (shell?.LoadedShell?.defName ?? "none") + ":" + (shell?.loadedCount ?? 0) +
                    " barrelFuel=" + (gun.TryGetComp<CompRefuelable>()?.Fuel ?? 0) +
                    " stocks=" + string.Join(";", stocks.Select(t => t.def.defName + ":" + t.stackCount + ":" + t.thingIDNumber +
                        "@" + t.Position + ":roof=" + t.Position.Roofed(map) + ":radius=" + (t.TryGetComp<CompExplosive>()?.ExplosiveRadius() ?? 0) +
                        ":loadReach=" + map.reachability.CanReach(gun.InteractionCell, t, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)))));
            }
        }

        [DebugAction(Category, "P4E ignite magazine hazard probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MagazineHazard()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            Map map = Find.CurrentMap;
            if (!(map?.Parent is Settlement) || !map.GetComponent<P0MapState>().Active)
                throw new InvalidOperationException("Requires disposable procedural settlement");
            Thing[] shells = map.listerThings.AllThings.Where(t => t.def.defName == "Shell_HighExplosive" &&
                t.Position.GetEdifice(map) is Building_Storage && t.GetRoom()?.CellCount == 1).ToArray();
            shells = shells.OrderByDescending(t => t.stackCount).ThenBy(t => t.thingIDNumber).ToArray();
            if (shells.Length == 0) throw new InvalidOperationException("Requires an identifiable magazine HE stack");
            CompExplosive explosive = shells[0].TryGetComp<CompExplosive>() ?? throw new InvalidOperationException("No native explosive comp");
            P2Log.Message("P4E_MAGAZINE_HAZARD_STARTED", "map=" + map.uniqueID + " stack=" + shells[0].thingIDNumber +
                " count=" + shells[0].stackCount + " cell=" + shells[0].Position + " radius=" + explosive.ExplosiveRadius());
            map.GetComponent<P4EArtilleryProbe>().StartHazard(shells[0]);

        }

        [DebugAction(Category, "P4E destroy one mortar", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DestroyOneMortar()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            Thing gun = Find.CurrentMap?.listerThings.AllThings.Where(t => t.def == ThingDefOf.Turret_Mortar)
                .OrderBy(t => t.thingIDNumber).FirstOrDefault() ?? throw new InvalidOperationException("No mortar");
            int id = gun.thingIDNumber;
            gun.Destroy(DestroyMode.Vanish);
            P2Log.Message("P4E_MORTAR_REMOVED", "gun=" + id);
        }

        [DebugAction(Category, "P4E drain mortar barrel once", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DrainBarrel()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            Map map = Find.CurrentMap;
            if (!(map?.Parent is Settlement) || !map.GetComponent<P0MapState>().Active)
                throw new InvalidOperationException("Requires disposable procedural settlement");
            Thing[] guns = map.listerThings.AllThings.Where(t => t.def == ThingDefOf.Turret_Mortar).ToArray();
            if (guns.Length != 1 || Find.Storyteller.difficulty.classicMortars)
                throw new InvalidOperationException("Requires exactly one non-classic mortar");
            CompRefuelable fuel = guns[0].TryGetComp<CompRefuelable>();
            float before = fuel?.Fuel ?? 0;
            if (before <= 0) throw new InvalidOperationException("Barrel already empty");
            fuel.ConsumeFuel(before);
            P2Log.Message("P4E_BARREL_DRAINED", "map=" + map.uniqueID + " gun=" + guns[0].thingIDNumber + " fuel=" + before + "->" + fuel.Fuel);
        }

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
                        !s.Map.mapPawns.AllPawnsSpawned.Any(p => p.RaceProps.Humanlike || p.Faction != null)));
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
                    // Keep the selected override or ordinary seeded policy.
                    // A retry must not silently force a different layer budget.
                    map = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, new IntVec3(160, 1, 160), null);
                }
                finally { RealisticBasesMod.Settings.forceP4WallLayersNext = oldLayers; }
                if (!map.GetComponent<P0MapState>().Active) throw new InvalidOperationException("Exact rejected-tile retry failed");
                layers = 0; // The fresh procedural plan log records effective depth.
            }
            else
            {
                P4AMapPlanningRun run = P4AMapPlanner.Run(map, settlement, RealisticBasesMod.Settings.forceP4WallLayersNext);
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
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            Map map = Find.CurrentMap;
            if (map == null) return;
            foreach (GameCondition condition in map.gameConditionManager.ActiveConditions.ToArray())
                if (condition.def == GameConditionDefOf.HeatWave || condition.def == GameConditionDefOf.ColdSnap) condition.End();
            map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(def, 60000));
            P2Log.Message("P4E_CLIMATE_PROBE", "map=" + map.uniqueID + " condition=" + def.defName +
                " tick=" + Find.TickManager.TicksGame);
        }

        [DebugAction(Category, "Start P4E dining and medicine fetch probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        private static void SeatedProbe()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("P4E requires disposable profile");
            Find.CurrentMap?.GetComponent<P4ESeatedProbe>().Start();
        }
    }

    // Short-lived validation only: real vanilla eating/fetch jobs on generated
    // fixtures. Research benches are decorative; never change research state.
    public sealed class P4ESeatedProbe : MapComponent
    {
        private Pawn eater, hauler;
        private Building_ResearchBench bench;
        private Thing medicine;
        private IntVec3 medicineCell;
        private bool eatObserved, fetchObserved;
        private int deadline;

        public P4ESeatedProbe(Map map) : base(map) { }

        public void Start()
        {
            if (deadline != 0) throw new InvalidOperationException("Probe already active");
            bench = map.listerThings.AllThings.OfType<Building_ResearchBench>().FirstOrDefault(b =>
                b.InteractionCell.GetEdifice(map)?.def.building?.isSittable == true) ?? throw new InvalidOperationException("No seated research bench");
            medicine = map.listerThings.AllThings.FirstOrDefault(t => t.def.IsMedicine && t.Position.GetEdifice(map) is Building_Storage) ??
                throw new InvalidOperationException("No stocked medical shelf");
            Thing meal = map.listerThings.AllThings.FirstOrDefault(t => t.def == ThingDefOf.MealSimple) ??
                throw new InvalidOperationException("No finite test meal");
            eatObserved = fetchObserved = false;
            medicineCell = medicine.Position;
            try
            {
                eater = SpawnProbe(meal.Position);
                hauler = SpawnProbe(medicineCell + IntVec3.West * 4);
                eater.needs.food.CurLevel = 0.05f;
                Job eat = JobMaker.MakeJob(JobDefOf.Ingest, meal); eat.count = 1;
                eater.jobs.TryTakeOrderedJob(eat);
                Job fetch = JobMaker.MakeJob(JobDefOf.TakeInventory, medicine); fetch.count = medicine.stackCount;
                hauler.jobs.TryTakeOrderedJob(fetch);
                deadline = Find.TickManager.TicksGame + 1800;
                P2Log.Message("P4E_SEAT_PROBE_STARTED", "map=" + map.uniqueID + " bench=" + bench.thingIDNumber +
                    " eater=" + eater.thingIDNumber + " hauler=" + hauler.thingIDNumber +
                    " medicine=" + medicine.thingIDNumber + ":" + medicine.stackCount + " deadline=" + deadline);
            }
            catch { Cleanup(); throw; }
        }

        private Pawn SpawnProbe(IntVec3 near)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, bench.Faction,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, forceNoGear: true));
            IntVec3 cell = CellFinder.StandableCellNear(near, map, 8);
            GenSpawn.Spawn(pawn, cell, map);
            return pawn;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref bench, "p4eProbeBench");
            Scribe_References.Look(ref eater, "p4eProbeEater");
            Scribe_References.Look(ref hauler, "p4eProbeHauler");
            Scribe_References.Look(ref medicine, "p4eProbeMedicine");
            Scribe_Values.Look(ref medicineCell, "p4eProbeMedicineCell");
            Scribe_Values.Look(ref deadline, "p4eProbeDeadline");
            Scribe_Values.Look(ref eatObserved, "p4eProbeEatObserved");
            Scribe_Values.Look(ref fetchObserved, "p4eProbeFetchObserved");
        }

        public override void MapComponentTick()
        {
            if (deadline == 0) return;
            // Observe the actual chewing toil at a dining surface, not a pawn
            // merely walking over a workstation chair during an Ingest job.
            eatObserved |= eater?.jobs.curDriver is JobDriver_Ingest ingest && ingest.GainingNutritionNow &&
                eater.Position.GetEdifice(map)?.def.building?.isSittable == true &&
                eater.CurJob.targetB.IsValid && eater.CurJob.targetB.Cell.GetEdifice(map)?.def.surfaceType == SurfaceType.Eat;
            fetchObserved |= medicine?.holdingOwner == hauler?.inventory?.innerContainer;
            if ((!eatObserved || !fetchObserved) && Find.TickManager.TicksGame < deadline) return;
            P2Log.Message("P4E_SEAT_PROBE_COMPLETE", "map=" + map.uniqueID + " researchPerformed=False seatedEating=" + eatObserved +
                " medicineFetched=" + fetchObserved + " medicine=" + medicine?.thingIDNumber + ":" + medicine?.stackCount +
                " medicineIdentityPreserved=" + (medicine != null && !medicine.Destroyed) + " eaterCell=" + eater?.Position);
            Cleanup();
        }

        private void Cleanup()
        {
            eater?.jobs.StopAll(); hauler?.jobs.StopAll();
            if (medicine != null && !medicine.Destroyed && !medicine.Spawned)
            {
                medicine.holdingOwner?.Remove(medicine);
                GenSpawn.Spawn(medicine, medicineCell, map);
            }
            foreach (Pawn pawn in new[] { eater, hauler })
                if (pawn != null && !pawn.Destroyed) pawn.Destroy();
            eater = hauler = null;
            bench = null;
            medicine = null;
            deadline = 0;
        }
    }
}
