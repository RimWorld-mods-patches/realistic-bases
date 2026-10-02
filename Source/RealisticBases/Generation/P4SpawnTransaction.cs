using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Integration;
using RealisticBases.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace RealisticBases.Generation
{
    // The mutation boundary is independent of either physical plan type. A
    // caller supplies the owned footprint, registers every spawned thing, and
    // commits only after its final map audit has passed.
    public sealed class P4SpawnTransaction
    {
        private sealed class PlantSnapshot
        {
            public IntVec3 Cell;
            public ThingDef Def;
            public float Growth;
            public bool Sown;
        }

        private sealed class RockSnapshot
        {
            public IntVec3 Cell;
            public ThingDef Def;
            public int HitPoints;
            public Thing Smoothed;
            public TerrainDef Top;
            public TerrainDef Under;
        }

        private sealed class FloorSnapshot
        {
            public IntVec3 Cell;
            public TerrainDef Top;
            public TerrainDef Under;
            public TerrainDef ExpectedTop;
            public TerrainDef ExpectedUnder;
        }

        private readonly Map map;
        private readonly TerrainMutationFootprint footprint;
        private readonly List<Thing> owned = new List<Thing>();
        private readonly List<Action> rollbackActions = new List<Action>();
        private readonly List<PlantSnapshot> plants = new List<PlantSnapshot>();
        private readonly List<RockSnapshot> rocks = new List<RockSnapshot>();
        private readonly List<RockSnapshot> smoothed = new List<RockSnapshot>();
        private readonly List<FloorSnapshot> floors = new List<FloorSnapshot>();
        private readonly Dictionary<IntVec3, RoofDef> roofs = new Dictionary<IntVec3, RoofDef>();
        private readonly string terrainBefore;
        private readonly string terrainLayersBefore;
        private readonly string unownedTerrainBefore;
        private readonly Dictionary<IntVec3, Tuple<TerrainDef, TerrainDef, RoofDef>> unownedLayers;
        private readonly string rockBefore;
        private readonly P4CFinishPlan finishes;
        private bool completed;

        public P4SpawnTransaction(Map map, TerrainMutationFootprint footprint, P4CFinishPlan finishes = null)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            this.finishes = finishes;
            TerrainSnapshot snapshot = P1MapPlanner.Capture(map);
            TerrainArchetype archetype = footprint.ExcavationCells.Count > 0 ? TerrainArchetype.ExcavatedMountain : TerrainArchetype.Flat;
            IReadOnlyList<string> errors = TerrainMutationPlanner.Validate(archetype, snapshot, footprint);
            if (errors.Count != 0) throw new InvalidOperationException("Invalid P4 mutation footprint: " + string.Join(",", errors));
            if (LayoutRoofSupport.Unsupported(snapshot, footprint.ExcavationCells, footprint.RetainedThickRoofCells).Count != 0)
                throw new InvalidOperationException("Retained thick roof has no intact natural-rock support");
            foreach (GridPoint point in footprint.RetainedThickRoofCells)
                if (map.roofGrid.RoofAt(ToCell(point)) != RoofDefOf.RoofRockThick)
                    throw new InvalidOperationException("Retained roof is not RoofRockThick at " + point);
            terrainBefore = P0MapAudit.TerrainRoofChecksum(map);
            terrainLayersBefore = P0MapAudit.TerrainLayerRoofChecksum(map);
            HashSet<IntVec3> ownedTerrain = new HashSet<IntVec3>(footprint.RoofClearanceCells
                .Concat(footprint.ConstructedRoofCells).Select(ToCell));
            ownedTerrain.UnionWith(finishes?.Floors.Keys.Select(ToCell) ?? Enumerable.Empty<IntVec3>());
            unownedTerrainBefore = P0MapAudit.TerrainLayerRoofChecksum(map, ownedTerrain);
            unownedLayers = map.AllCells.Where(c => !ownedTerrain.Contains(c)).ToDictionary(c => c,
                c => Tuple.Create(map.terrainGrid.TerrainAt(c), map.terrainGrid.UnderTerrainAt(c), map.roofGrid.RoofAt(c)));
            rockBefore = P0MapAudit.NaturalRockChecksum(map, footprint.ExcavationCells
                .Concat(finishes?.Walls.Keys ?? Enumerable.Empty<GridPoint>()).Select(ToCell));
        }

        public string TerrainBefore => terrainBefore;
        public int OwnedCount => owned.Count;
        public IReadOnlyList<Thing> OwnedThings => owned;
        public int ExcavatedCount => rocks.Count;
        public int ClearedPlantCount => plants.Count;
        public int RestoredPlantCount { get; private set; }
        public int RoofChangedCount => roofs.Count;
        public int FloorChangedCount => floors.Count;
        public int SmoothedWallCount => smoothed.Count;
        public void OnRollback(Action action)
        {
            if (completed) throw new InvalidOperationException("P4 transaction has completed");
            rollbackActions.Add(action ?? throw new ArgumentNullException(nameof(action)));
        }

        // Borrowed objects are returned before deleting owned structures. They
        // never enter OwnedThings and are never destroyed by this transaction.
        public void Borrow(Thing thing, ThingOwner source)
        {
            if (completed || thing == null || source == null || !source.Contains(thing) || thing.Spawned)
                throw new InvalidOperationException("Invalid borrowed P4 object ownership");
            IntVec3 position = thing.Position;
            Rot4 rotation = thing.Rotation;
            Pawn pawn = thing as Pawn;
            bool join = pawn?.mindState.WillJoinColonyIfRescued ?? false;
            PawnDuty duty = pawn?.mindState.duty;
            source.Remove(thing);
            OnRollback(() =>
            {
                if (thing.Destroyed) throw new InvalidOperationException("Borrowed quest object destroyed: " + thing.ThingID);
                if (thing.Spawned) thing.DeSpawn();
                if (pawn != null)
                {
                    pawn.jobs?.StopAll();
                    pawn.mindState.WillJoinColonyIfRescued = join;
                    pawn.mindState.duty = duty;
                }
                thing.Position = position; thing.Rotation = rotation;
                if (!source.TryAdd(thing, canMergeWithExistingStacks: false) || thing.holdingOwner != source)
                    throw new InvalidOperationException("Borrowed quest holder restore failed: " + thing.ThingID);
            });
        }
        public void Own(Thing thing)
        {
            if (completed) throw new InvalidOperationException("P4 transaction has completed");
            if (thing == null) throw new ArgumentNullException(nameof(thing));
            owned.Add(thing);
        }

        public void ClearPlant(GridPoint point)
        {
            IntVec3 cell = ToCell(point);
            Plant plant = cell.GetPlant(map);
            if (plant == null) return;
            plants.Add(new PlantSnapshot { Cell = cell, Def = plant.def, Growth = plant.Growth, Sown = plant.sown });
            plant.Destroy(DestroyMode.Vanish);
        }

        public void Excavate()
        {
            foreach (GridPoint point in footprint.ExcavationCells)
            {
                IntVec3 cell = ToCell(point);
                Building edifice = cell.GetEdifice(map);
                if (edifice?.def?.building?.isNaturalRock != true)
                    throw new InvalidOperationException("P4 excavation source is not natural rock: " + cell);
                if (!roofs.ContainsKey(cell)) roofs[cell] = map.roofGrid.RoofAt(cell);
                var source = new RockSnapshot { Cell = cell, Def = edifice.def, HitPoints = edifice.HitPoints,
                    Top = map.terrainGrid.TerrainAt(cell), Under = map.terrainGrid.UnderTerrainAt(cell) };
                rocks.Add(source);
                edifice.Destroy(DestroyMode.Vanish);
                // Building.DeSpawn applies leaveTerrain even for Vanish. Keep
                // the original substrate; only explicit finish choices own floors.
                RestoreSourceTerrain(source);
                if (footprint.RoofClearanceCells.Contains(point)) map.roofGrid.SetRoof(cell, null);
            }
            foreach (GridPoint point in footprint.RetainedThickRoofCells)
                if (map.roofGrid.RoofAt(ToCell(point)) != RoofDefOf.RoofRockThick)
                    throw new InvalidOperationException("Retained thick roof collapsed at " + point);
            TerrainSnapshot after = P1MapPlanner.Capture(map);
            if (LayoutRoofSupport.Unsupported(after, Enumerable.Empty<GridPoint>(), footprint.RetainedThickRoofCells).Count != 0)
                throw new InvalidOperationException("Post-excavation natural-rock roof support failed");
        }

        public void ApplyFloors()
        {
            if (completed) throw new InvalidOperationException("P4 transaction has completed");
            foreach (KeyValuePair<GridPoint, string> choice in finishes?.Floors ??
                (IEnumerable<KeyValuePair<GridPoint, string>>)Enumerable.Empty<KeyValuePair<GridPoint, string>>())
            {
                IntVec3 cell = ToCell(choice.Key);
                TerrainDef target = DefDatabase<TerrainDef>.GetNamedSilentFail(choice.Value);
                TerrainDef original = map.terrainGrid.TerrainAt(cell);
                if (target == null || original == null || original.IsWater || original.layerable ||
                    !original.affordances.Contains(TerrainAffordanceDefOf.Heavy) ||
                    cell.GetPlant(map) != null || cell.GetEdifice(map) != null)
                    throw new InvalidOperationException("P4 finish floor source changed at " + choice.Key);
                FloorSnapshot snapshot = new FloorSnapshot { Cell = cell, Top = original,
                    Under = map.terrainGrid.UnderTerrainAt(cell) };
                floors.Add(snapshot);
                map.terrainGrid.SetTerrain(cell, target);
                snapshot.ExpectedTop = map.terrainGrid.TerrainAt(cell);
                snapshot.ExpectedUnder = map.terrainGrid.UnderTerrainAt(cell);
                if (snapshot.ExpectedTop != target)
                    throw new InvalidOperationException("P4 finish floor did not realize at " + choice.Key);
            }
        }

        public void SmoothWalls()
        {
            if (completed) throw new InvalidOperationException("P4 transaction has completed");
            foreach (KeyValuePair<GridPoint, string> choice in finishes?.Walls ??
                (IEnumerable<KeyValuePair<GridPoint, string>>)Enumerable.Empty<KeyValuePair<GridPoint, string>>())
            {
                IntVec3 cell = ToCell(choice.Key);
                Building original = cell.GetEdifice(map);
                ThingDef target = DefDatabase<ThingDef>.GetNamedSilentFail(choice.Value);
                if (original?.def?.building?.isNaturalRock != true || target == null ||
                    original.def.building.smoothedThing != target || original.def.holdsRoof != target.holdsRoof ||
                    original.def.passability != target.passability || original.def.blockLight != target.blockLight ||
                    original.def.fillPercent != target.fillPercent)
                    throw new InvalidOperationException("P4 finish wall source changed at " + choice.Key);
                RockSnapshot snapshot = new RockSnapshot { Cell = cell, Def = original.def, HitPoints = original.HitPoints,
                    Top = map.terrainGrid.TerrainAt(cell), Under = map.terrainGrid.UnderTerrainAt(cell) };
                if (!roofs.ContainsKey(cell)) roofs[cell] = map.roofGrid.RoofAt(cell);
                smoothed.Add(snapshot);
                Thing replacement = GenSpawn.Spawn(ThingMaker.MakeThing(target), cell, map, Rot4.North, WipeMode.Vanish);
                replacement.HitPoints = Math.Min(snapshot.HitPoints, replacement.MaxHitPoints);
                snapshot.Smoothed = replacement;
                RestoreSourceTerrain(snapshot);
                if (cell.GetEdifice(map) != replacement || map.roofGrid.RoofAt(cell) != roofs[cell])
                    throw new InvalidOperationException("P4 finish wall or roof did not realize at " + choice.Key);
            }
        }

        public void SetConstructedRoof(GridPoint point)
        {
            if (!footprint.ConstructedRoofCells.Contains(point)) throw new InvalidOperationException("Unowned constructed roof at " + point);
            IntVec3 cell = ToCell(point);
            if (!roofs.ContainsKey(cell)) roofs[cell] = map.roofGrid.RoofAt(cell);
            if (!RoofCollapseUtility.WithinRangeOfRoofHolder(cell, map, assumeNonNoRoofCellsAreRoofed: true))
                throw new InvalidOperationException("Constructed roof lacks holder at " + point);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
        }

        public void Commit()
        {
            if (completed) throw new InvalidOperationException("P4 transaction has completed");
            if (rockBefore != P0MapAudit.NaturalRockChecksum(map, footprint.ExcavationCells
                .Concat(finishes?.Walls.Keys ?? Enumerable.Empty<GridPoint>()).Select(ToCell)))
                throw new InvalidOperationException("Unowned natural rock changed during P4 spawn");
            HashSet<IntVec3> ownedTerrain = new HashSet<IntVec3>(footprint.RoofClearanceCells
                .Concat(footprint.ConstructedRoofCells).Select(ToCell));
            ownedTerrain.UnionWith(finishes?.Floors.Keys.Select(ToCell) ?? Enumerable.Empty<IntVec3>());
            if (unownedTerrainBefore != P0MapAudit.TerrainLayerRoofChecksum(map, ownedTerrain))
                throw new InvalidOperationException("Unowned terrain layer or roof changed during P4 spawn: " +
                    string.Join(";", unownedLayers.Where(p => p.Value.Item1 != map.terrainGrid.TerrainAt(p.Key) ||
                        p.Value.Item2 != map.terrainGrid.UnderTerrainAt(p.Key) || p.Value.Item3 != map.roofGrid.RoofAt(p.Key))
                        .Take(12).Select(p => p.Key + ":" + p.Value.Item1?.defName + "/" + p.Value.Item2?.defName + "/" +
                            p.Value.Item3?.defName + "->" + map.terrainGrid.TerrainAt(p.Key)?.defName + "/" +
                            map.terrainGrid.UnderTerrainAt(p.Key)?.defName + "/" + map.roofGrid.RoofAt(p.Key)?.defName)));
            if (floors.Any(f => map.terrainGrid.TerrainAt(f.Cell) != f.ExpectedTop ||
                map.terrainGrid.UnderTerrainAt(f.Cell) != f.ExpectedUnder))
                throw new InvalidOperationException("Owned floor changed after P4 finish application");
            if (smoothed.Any(s => s.Cell.GetEdifice(map) != s.Smoothed || s.Smoothed.Destroyed ||
                map.roofGrid.RoofAt(s.Cell) != roofs[s.Cell]))
                throw new InvalidOperationException("Owned smooth wall changed after P4 finish application");
            foreach (GridPoint point in footprint.RetainedThickRoofCells)
                if (map.roofGrid.RoofAt(ToCell(point)) != RoofDefOf.RoofRockThick)
                    throw new InvalidOperationException("Retained thick roof changed at " + point);
            completed = true;
        }

        public bool Rollback()
        {
            if (completed) throw new InvalidOperationException("Committed P4 transaction cannot roll back");
            for (int i = rollbackActions.Count - 1; i >= 0; i--) rollbackActions[i]();
            for (int i = owned.Count - 1; i >= 0; i--)
                if (!owned[i].Destroyed) owned[i].Destroy(DestroyMode.Vanish);
            for (int i = smoothed.Count - 1; i >= 0; i--)
            {
                RockSnapshot rock = smoothed[i];
                if (rock.Smoothed != null && !rock.Smoothed.Destroyed) rock.Smoothed.Destroy(DestroyMode.Vanish);
                if (rock.Cell.GetEdifice(map) == null)
                {
                    Thing restored = GenSpawn.Spawn(ThingMaker.MakeThing(rock.Def), rock.Cell, map, Rot4.North, WipeMode.Vanish);
                    restored.HitPoints = rock.HitPoints;
                }
            }
            foreach (RockSnapshot rock in rocks)
                if (rock.Cell.GetEdifice(map) == null)
                {
                    Thing restored = GenSpawn.Spawn(ThingMaker.MakeThing(rock.Def), rock.Cell, map, Rot4.North, WipeMode.Vanish);
                    restored.HitPoints = rock.HitPoints;
                }
            for (int i = floors.Count - 1; i >= 0; i--)
            {
                FloorSnapshot floor = floors[i];
                // Removing the layer first restores and clears the original
                // substrate. SetUnderTerrain(null) is invalid in RimWorld 1.6.
                if (map.terrainGrid.TerrainAt(floor.Cell)?.layerable == true)
                    map.terrainGrid.RemoveTopLayer(floor.Cell);
                if (map.terrainGrid.TerrainAt(floor.Cell) != floor.Top)
                    map.terrainGrid.SetTerrain(floor.Cell, floor.Top);
                if (floor.Under != null && map.terrainGrid.UnderTerrainAt(floor.Cell) != floor.Under)
                    map.terrainGrid.SetUnderTerrain(floor.Cell, floor.Under);
            }
            foreach (KeyValuePair<IntVec3, RoofDef> roof in roofs) map.roofGrid.SetRoof(roof.Key, roof.Value);
            foreach (RockSnapshot rock in rocks.Concat(smoothed)) RestoreSourceTerrain(rock);
            foreach (PlantSnapshot original in plants)
            {
                if (original.Cell.GetPlant(map) == null)
                {
                    Plant plant = (Plant)ThingMaker.MakeThing(original.Def);
                    plant.Growth = original.Growth;
                    plant.sown = original.Sown;
                    GenSpawn.Spawn(plant, original.Cell, map, Rot4.North, WipeMode.Vanish);
                }
                Plant restored = original.Cell.GetPlant(map);
                if (restored?.def == original.Def &&
                    Math.Abs(restored.Growth - original.Growth) < 0.0001f &&
                    restored.sown == original.Sown) RestoredPlantCount++;
            }
            completed = true;
            return RestoredPlantCount == plants.Count &&
                terrainBefore == P0MapAudit.TerrainRoofChecksum(map) &&
                terrainLayersBefore == P0MapAudit.TerrainLayerRoofChecksum(map) &&
                rockBefore == P0MapAudit.NaturalRockChecksum(map, footprint.ExcavationCells
                    .Concat(finishes?.Walls.Keys ?? Enumerable.Empty<GridPoint>()).Select(ToCell)) &&
                smoothed.All(s => s.Cell.GetEdifice(map)?.def == s.Def &&
                    s.Cell.GetEdifice(map).HitPoints == s.HitPoints);
        }

        private void RestoreSourceTerrain(RockSnapshot source)
        {
            if (map.terrainGrid.TerrainAt(source.Cell) != source.Top)
                map.terrainGrid.SetTerrain(source.Cell, source.Top);
            if (source.Under != null && map.terrainGrid.UnderTerrainAt(source.Cell) != source.Under)
                map.terrainGrid.SetUnderTerrain(source.Cell, source.Under);
        }

        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }
}
