using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Integration;
using RealisticBases.Planning;
using RimWorld;
using Verse;

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
        }

        private readonly Map map;
        private readonly TerrainMutationFootprint footprint;
        private readonly List<Thing> owned = new List<Thing>();
        private readonly List<PlantSnapshot> plants = new List<PlantSnapshot>();
        private readonly List<RockSnapshot> rocks = new List<RockSnapshot>();
        private readonly Dictionary<IntVec3, RoofDef> roofs = new Dictionary<IntVec3, RoofDef>();
        private readonly string terrainBefore;
        private readonly string rockBefore;
        private bool completed;

        public P4SpawnTransaction(Map map, TerrainMutationFootprint footprint)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
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
            rockBefore = P0MapAudit.NaturalRockChecksum(map, footprint.ExcavationCells.Select(ToCell));
        }

        public string TerrainBefore => terrainBefore;
        public int OwnedCount => owned.Count;
        public IReadOnlyList<Thing> OwnedThings => owned;
        public int ExcavatedCount => rocks.Count;
        public int RoofChangedCount => roofs.Count;
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
                rocks.Add(new RockSnapshot { Cell = cell, Def = edifice.def });
                edifice.Destroy(DestroyMode.Vanish);
                if (footprint.RoofClearanceCells.Contains(point)) map.roofGrid.SetRoof(cell, null);
            }
            foreach (GridPoint point in footprint.RetainedThickRoofCells)
                if (map.roofGrid.RoofAt(ToCell(point)) != RoofDefOf.RoofRockThick)
                    throw new InvalidOperationException("Retained thick roof collapsed at " + point);
            TerrainSnapshot after = P1MapPlanner.Capture(map);
            if (LayoutRoofSupport.Unsupported(after, Enumerable.Empty<GridPoint>(), footprint.RetainedThickRoofCells).Count != 0)
                throw new InvalidOperationException("Post-excavation natural-rock roof support failed");
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
            if (rockBefore != P0MapAudit.NaturalRockChecksum(map, footprint.ExcavationCells.Select(ToCell)))
                throw new InvalidOperationException("Unowned natural rock changed during P4 spawn");
            foreach (GridPoint point in footprint.RetainedThickRoofCells)
                if (map.roofGrid.RoofAt(ToCell(point)) != RoofDefOf.RoofRockThick)
                    throw new InvalidOperationException("Retained thick roof changed at " + point);
            completed = true;
        }

        public bool Rollback()
        {
            if (completed) throw new InvalidOperationException("Committed P4 transaction cannot roll back");
            for (int i = owned.Count - 1; i >= 0; i--)
                if (!owned[i].Destroyed) owned[i].Destroy(DestroyMode.Vanish);
            foreach (RockSnapshot rock in rocks)
                if (rock.Cell.GetEdifice(map) == null)
                    GenSpawn.Spawn(ThingMaker.MakeThing(rock.Def), rock.Cell, map, Rot4.North, WipeMode.Vanish);
            foreach (KeyValuePair<IntVec3, RoofDef> roof in roofs) map.roofGrid.SetRoof(roof.Key, roof.Value);
            foreach (PlantSnapshot original in plants)
            {
                if (original.Cell.GetPlant(map) != null) continue;
                Plant plant = (Plant)ThingMaker.MakeThing(original.Def);
                plant.Growth = original.Growth;
                plant.sown = original.Sown;
                GenSpawn.Spawn(plant, original.Cell, map, Rot4.North, WipeMode.Vanish);
            }
            completed = true;
            return terrainBefore == P0MapAudit.TerrainRoofChecksum(map) &&
                rockBefore == P0MapAudit.NaturalRockChecksum(map, footprint.ExcavationCells.Select(ToCell));
        }

        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }
}
