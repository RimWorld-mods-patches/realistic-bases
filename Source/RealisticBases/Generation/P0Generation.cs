using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Core;
using RealisticBases.Persistence;
using RealisticBases.AI;
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

            IntVec3 origin;
            if (!TryFindSite(map, 9, out origin))
            {
                P0Log.Message("PLAN_REJECT", "reason=no_9x9_buildable_edge_reachable_site mutationCount=0");
                if (!TryFindSite(map, 5, out origin))
                {
                    P0Log.Error("FALLBACK_REJECT", "reason=no_5x5_buildable_edge_reachable_site mutationCount=0");
                    return;
                }
                SpawnAndInitialize(map, settlement, origin, 5, before, "site_rejection_fallback");
                return;
            }

            if (RealisticBasesMod.Settings.forceRejectNext)
            {
                RealisticBasesMod.Settings.forceRejectNext = false;
                P0Log.Message("PLAN_REJECT", "reason=forced_before_spawn mutationCount=0");
                IntVec3 fallback;
                if (TryFindSite(map, 5, out fallback))
                {
                    SpawnAndInitialize(map, settlement, fallback, 5, before, "forced_rejection_fallback");
                }
                return;
            }

            if (RealisticBasesMod.Settings.forcePartialFailureNext)
            {
                RealisticBasesMod.Settings.forcePartialFailureNext = false;
                RunControlledFailure(map, settlement, origin, before);
                return;
            }

            SpawnAndInitialize(map, settlement, origin, 9, before, "primary");
        }

        private static void RunControlledFailure(Map map, Settlement settlement, IntVec3 origin, string before)
        {
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

            IntVec3 fallback;
            if (TryFindSite(map, 5, out fallback))
            {
                SpawnAndInitialize(map, settlement, fallback, 5, before, "partial_failure_fallback");
            }
        }

        private static void SpawnAndInitialize(Map map, Settlement settlement, IntVec3 origin, int size, string before, string path)
        {
            IntVec3 woodCell = origin + new IntVec3(size / 2, 0, size / 2);
            IntVec3 markerWall = origin;
            int spawnedCount = 0;
            for (int dx = 0; dx < size; dx++)
            {
                for (int dz = 0; dz < size; dz++)
                {
                    bool perimeter = dx == 0 || dz == 0 || dx == size - 1 || dz == size - 1;
                    if (!perimeter)
                    {
                        continue;
                    }
                    IntVec3 cell = origin + new IntVec3(dx, 0, dz);
                    bool gate = dz == 0 && dx == size / 2;
                    SpawnBuilding(gate ? ThingDefOf.Door : ThingDefOf.Wall, ThingDefOf.Steel, cell, map, settlement.Faction);
                    spawnedCount++;
                }
            }

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
            GenSpawn.Spawn(pawnA, origin + new IntVec3(1, 0, 1), map);
            GenSpawn.Spawn(pawnB, origin + new IntVec3(size - 2, 0, size - 2), map);
            List<Pawn> defenders = new[] { pawnA, pawnB }.OrderBy(p => p.thingIDNumber).ToList();
            LordMaker.MakeNewLord(settlement.Faction,
                new LordJob_P0Garrison(origin + new IntVec3(size / 2, 0, size / 2), origin + new IntVec3(size / 2, 0, size / 2)),
                map, defenders);
            bool rosterReused = revisit && defenders.Any(p => state.pawnIds.Contains(p.thingIDNumber));
            state.pawnIds = defenders.Select(p => p.thingIDNumber).ToList();

            string after = P0MapAudit.TerrainRoofChecksum(map);
            map.GetComponent<P0MapState>().Initialize(settlement, origin, woodCell, markerWall, after);
            P0Log.Message("SPAWN_COMPLETE", "path=" + path + " layoutSize=" + size + " things=" +
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

        private static bool TryFindSite(Map map, int size, out IntVec3 origin)
        {
            IntVec3 center = map.Center;
            foreach (IntVec3 candidate in map.AllCells.OrderBy(c => c.DistanceToSquared(center)))
            {
                IntVec3 proposedOrigin = candidate - new IntVec3(size / 2, 0, size / 2);
                CellRect rect = new CellRect(proposedOrigin.x, proposedOrigin.z, size, size);
                if (!rect.FullyContainedWithin(map.BoundsRect(12)))
                {
                    continue;
                }
                bool valid = true;
                foreach (IntVec3 cell in rect)
                {
                    if (cell.Roofed(map) || cell.GetEdifice(map) != null || !cell.Standable(map) ||
                        !GenConstruct.CanBuildOnTerrain(ThingDefOf.Wall, cell, map, Rot4.North, null, ThingDefOf.Steel))
                    {
                        valid = false;
                        break;
                    }
                }
                IntVec3 gateOutside = proposedOrigin + new IntVec3(size / 2, 0, -1);
                if (valid && gateOutside.InBounds(map) && gateOutside.Standable(map) &&
                    map.reachability.CanReachMapEdge(gateOutside, TraverseParms.For(TraverseMode.PassDoors)))
                {
                    origin = proposedOrigin;
                    return true;
                }
            }
            origin = IntVec3.Invalid;
            return false;
        }
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
