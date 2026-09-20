using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Core;
using RealisticBases.Persistence;
using RealisticBases.AI;
using RealisticBases.Integration;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace RealisticBases.Generation
{
    public static class P0MapAudit
    {
        public static string TerrainRoofChecksum(Map map)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                foreach (IntVec3 cell in map.AllCells)
                {
                    hash ^= cell.GetTerrain(map).shortHash;
                    hash *= 1099511628211UL;
                    RoofDef roof = map.roofGrid.RoofAt(cell);
                    hash ^= roof?.shortHash ?? 0;
                    hash *= 1099511628211UL;
                }
                return hash.ToString("X16");
            }
        }

        public static bool IsProhibitedDefense(Thing thing)
        {
            if (thing is Building_Trap)
            {
                return true;
            }
            string name = thing.def?.defName ?? string.Empty;
            return name.IndexOf("IED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Trap", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public sealed class GenStep_P0Settlement : GenStep
    {
        public override int SeedPart => 1741196301;

        public override void Generate(Map map, GenStepParams parms)
        {
            string before = P0MapAudit.TerrainRoofChecksum(map);
            Settlement settlement = map.Parent as Settlement;
            P0Log.Message("GEN_STEP", "name=RB_P0_Settlement order=" + def.order + " map=" + map.uniqueID +
                " terrainBefore=" + before + " parent=" + map.Parent?.GetType().Name);

            if (settlement == null || settlement.Faction == null)
            {
                P0Log.Error("GEN_GUARD", "dedicated generator reached without a faction settlement; leaving terrain untouched");
                return;
            }

            P1PlanningRun primary = P1MapPlanner.Run(map, P1MapPlanner.PrimaryProfile, "primary");
            if (!primary.Result.Success)
            {
                P0Log.Message("PLAN_REJECT", "reason=p1_primary_rejected mutationCount=0 constraints=" +
                    P1MapPlanner.Compact(primary.Result.Rejections));
                P1PlanningRun fallback = P1MapPlanner.Run(map, P1MapPlanner.CompactProfile, "site_rejection_fallback");
                if (!fallback.Result.Success)
                {
                    P0Log.Error("FALLBACK_REJECT", "reason=p1_compact_rejected mutationCount=0 constraints=" +
                        P1MapPlanner.Compact(fallback.Result.Rejections));
                    P1PlanDiagnostics.Record(map, fallback, "untouched-map-exit", new[] { "primary-rejected", "compact-rejected" });
                    return;
                }
                P1PlanDiagnostics.Record(map, fallback, "compact-fallback", new[] { "primary-rejected" });
                SpawnAndInitialize(map, settlement, fallback.Result.Plan, before, "site_rejection_fallback");
                return;
            }

            if (RealisticBasesMod.Settings.forceRejectNext)
            {
                RealisticBasesMod.Settings.forceRejectNext = false;
                P0Log.Message("PLAN_REJECT", "reason=forced_before_spawn mutationCount=0");
                P1PlanningRun fallback = P1MapPlanner.Run(map, P1MapPlanner.CompactProfile, "forced_rejection_fallback");
                if (fallback.Result.Success)
                {
                    P1PlanDiagnostics.Record(map, fallback, "compact-fallback", new[] { "forced-primary-rejection" });
                    SpawnAndInitialize(map, settlement, fallback.Result.Plan, before, "forced_rejection_fallback");
                }
                return;
            }

            if (RealisticBasesMod.Settings.forcePartialFailureNext)
            {
                RealisticBasesMod.Settings.forcePartialFailureNext = false;
                RunControlledFailure(map, settlement, primary.Result.Plan, before);
                return;
            }

            P1PlanDiagnostics.Record(map, primary, "primary", Array.Empty<string>());
            SpawnAndInitialize(map, settlement, primary.Result.Plan, before, "primary");
        }

        private static void RunControlledFailure(Map map, Settlement settlement, SettlementPlan primaryPlan, string before)
        {
            IntVec3 origin = ToCell(new GridPoint(primaryPlan.Compound.MinX, primaryPlan.Compound.MinZ));
            List<Thing> spawned = new List<Thing>();
            Dictionary<IntVec3, TerrainDef> changedTerrain = new Dictionary<IntVec3, TerrainDef>();
            try
            {
                Thing wall = SpawnBuilding(ThingDefOf.Wall, ThingDefOf.Steel, origin, map, settlement.Faction);
                spawned.Add(wall);
                changedTerrain[origin + IntVec3.East] = (origin + IntVec3.East).GetTerrain(map);
                map.terrainGrid.SetTerrain(origin + IntVec3.East, TerrainDefOf.Concrete);
                throw new InvalidOperationException("forced P0 partial-spawn failure");
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
                foreach (KeyValuePair<IntVec3, TerrainDef> pair in changedTerrain)
                {
                    map.terrainGrid.SetTerrain(pair.Key, pair.Value);
                }
                string rolledBack = P0MapAudit.TerrainRoofChecksum(map);
                P0Log.Message("SPAWN_RECOVERY", "exception=" + ex.GetType().Name +
                    " ownedThingsRemoved=" + spawned.Count + " terrainCellsRestored=" + changedTerrain.Count +
                    " checksumRestored=" + (rolledBack == before));
            }

            P1PlanningRun fallback = P1MapPlanner.Run(map, P1MapPlanner.CompactProfile, "partial_failure_fallback");
            if (fallback.Result.Success)
            {
                P1PlanDiagnostics.Record(map, fallback, "compact-fallback", new[] { "partial-spawn-rollback" });
                SpawnAndInitialize(map, settlement, fallback.Result.Plan, before, "partial_failure_fallback");
            }
        }

        private static void SpawnAndInitialize(Map map, Settlement settlement, SettlementPlan plan, string before, string path)
        {
            IntVec3 origin = ToCell(new GridPoint(plan.Compound.MinX, plan.Compound.MinZ));
            int size = plan.Compound.Width;
            RoomEnvelope stores = plan.Rooms.First(r => r.Id == "stores");
            IntVec3 woodCell = ToCell(new GridPoint(stores.Bounds.MinX, stores.Bounds.MinZ));
            PlanReservation markerReservation = plan.Reservations.First(r => r.Kind == ReservationKind.Structure);
            IntVec3 markerWall = ToCell(markerReservation.Cell);
            int spawnedCount = 0;
            foreach (PlanReservation reservation in plan.Reservations.Where(r => r.Kind == ReservationKind.Structure))
            {
                SpawnBuilding(ThingDefOf.Wall, ThingDefOf.Steel, ToCell(reservation.Cell), map, settlement.Faction);
                spawnedCount++;
            }
            SpawnBuilding(ThingDefOf.Door, ThingDefOf.Steel, ToCell(plan.Gate), map, settlement.Faction);
            spawnedCount++;

            Thing wood = ThingMaker.MakeThing(ThingDefOf.WoodLog);
            wood.stackCount = 30;
            GenSpawn.Spawn(wood, woodCell, map);

            P0SettlementState state = Current.Game.GetComponent<P0WorldState>().GetOrCreate(settlement.ID, settlement.Faction);
            bool revisit = state.generationCount > 0;
            if (revisit)
            {
                if (state.woodRemaining <= 0)
                {
                    wood.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    wood.stackCount = Math.Min(wood.stackCount, state.woodRemaining);
                }
                if (state.markerWallDestroyed)
                {
                    Thing wall = markerWall.GetFirstThing(map, ThingDefOf.Wall);
                    wall?.Destroy(DestroyMode.Vanish);
                }
            }
            state.generationCount++;

            PawnKindDef kind = settlement.Faction.def.basicMemberKind ?? PawnKindDefOf.Pirate;
            Pawn pawnA = PawnGenerator.GeneratePawn(kind, settlement.Faction, map.Tile);
            Pawn pawnB = PawnGenerator.GeneratePawn(kind, settlement.Faction, map.Tile);
            GenSpawn.Spawn(pawnA, ToCell(plan.Rooms[0].InteractionCell), map);
            GenSpawn.Spawn(pawnB, ToCell(plan.Rooms[1].InteractionCell), map);
            List<Pawn> defenders = new[] { pawnA, pawnB }.OrderBy(p => p.thingIDNumber).ToList();
            LordMaker.MakeNewLord(settlement.Faction,
                new LordJob_P0Garrison(origin + new IntVec3(size / 2, 0, size / 2), origin + new IntVec3(size / 2, 0, size / 2)),
                map, defenders);
            bool rosterReused = revisit && defenders.Any(p => state.pawnIds.Contains(p.thingIDNumber));
            state.pawnIds = defenders.Select(p => p.thingIDNumber).ToList();

            string after = P0MapAudit.TerrainRoofChecksum(map);
            map.GetComponent<P0MapState>().Initialize(settlement, origin, woodCell, markerWall, after);
            P0Log.Message("SPAWN_COMPLETE", "path=" + path + " layoutSize=" + plan.Compound.Width + "x" +
                plan.Compound.Height + " planDigest=" + plan.Digest + " archetype=" + plan.Archetype +
                " rooms=" + plan.Rooms.Count + " things=" +
                (spawnedCount + (wood.Destroyed ? 0 : 1)) + " terrainBefore=" + before + " terrainAfter=" + after +
                " terrainPreserved=" + (before == after) + " revisit=" + revisit +
                " restoredWood=" + (wood.Destroyed ? 0 : wood.stackCount) +
                " restoredMarkerWallDestroyed=" + (revisit && state.markerWallDestroyed) +
                " defenderIds=" + string.Join(",", defenders.Select(p => p.thingIDNumber.ToString()).ToArray()) +
                " rosterReused=" + rosterReused);
        }

        private static Thing SpawnBuilding(ThingDef def, ThingDef stuff, IntVec3 cell, Map map, Faction faction)
        {
            Thing thing = ThingMaker.MakeThing(def, stuff);
            thing.SetFactionDirect(faction);
            return GenSpawn.Spawn(thing, cell, map, Rot4.North, WipeMode.Vanish);
        }

        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }

    public sealed class GenStep_P0SettlementPower : GenStep
    {
        public override int SeedPart => 1741196302;

        public override void Generate(Map map, GenStepParams parms)
        {
            P0Log.Message("GEN_STEP", "name=RB_P0_SettlementPower order=" + def.order + " map=" + map.uniqueID);
            new GenStep_Power().Generate(map, parms);
        }
    }
}
