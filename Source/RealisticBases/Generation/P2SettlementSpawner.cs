using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.AI;
using RealisticBases.Integration;
using RealisticBases.Persistence;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace RealisticBases.Generation
{
    public static class P2SettlementSpawner
    {
        private sealed class PlantRestore
        {
            public IntVec3 Cell;
            public ThingDef Def;
            public float Growth;
            public bool Sown;
        }

        private sealed class RockRestore
        {
            public IntVec3 Cell;
            public ThingDef Def;
        }

        public static bool TrySpawn(Map map, Settlement settlement, P2PlanningRun run, string terrainBefore,
            string path, bool forceFailure, out string failure)
        {
            PhysicalSettlementPlan plan = run.Result.Plan;
            PhysicalSettlementProfile profile = run.Input.Profile;
            EncounterPlan encounter;
            try
            {
                encounter = EncounterPlanner.Plan(plan, profile, run.Snapshot);
            }
            catch (Exception ex)
            {
                failure = "encounter_plan_rejected:" + ex.Message;
                return false;
            }
            List<Thing> spawned = new List<Thing>();
            Dictionary<IntVec3, RoofDef> changedRoofs = new Dictionary<IntVec3, RoofDef>();
            List<PlantRestore> removedPlants = new List<PlantRestore>();
            List<RockRestore> removedRocks = new List<RockRestore>();
            string terrainOnlyBefore = P0MapAudit.TerrainChecksum(map);
            IEnumerable<IntVec3> ownedExcavation = plan.TerrainMutations.ExcavationCells.Select(ToCell);
            string unownedRockBefore = P0MapAudit.NaturalRockChecksum(map, ownedExcavation);
            try
            {
                foreach (GridPoint point in plan.TerrainMutations.ExcavationCells)
                {
                    IntVec3 cell = ToCell(point);
                    Building edifice = cell.GetEdifice(map);
                    if (edifice == null || edifice.def?.building?.isNaturalRock != true)
                        throw new InvalidOperationException("P4 excavation ledger does not match a natural rock cell: " + cell);
                    removedRocks.Add(new RockRestore { Cell = cell, Def = edifice.def });
                    edifice.Destroy(DestroyMode.Vanish);
                    if (plan.TerrainMutations.RoofClearanceCells.Contains(point))
                    {
                        if (!changedRoofs.ContainsKey(cell)) changedRoofs[cell] = map.roofGrid.RoofAt(cell);
                        map.roofGrid.SetRoof(cell, null);
                    }
                }
                foreach (GridPoint point in plan.TerrainMutations.RetainedThickRoofCells)
                {
                    IntVec3 cell = ToCell(point);
                    if (!map.roofGrid.Roofed(cell) || !HasNaturalRockRoofHolder(map, cell))
                        throw new InvalidOperationException("P4 retained thick roof lost natural-rock support: " + cell);
                }
                IEnumerable<GridPoint> encounterMutationCells = EncounterCoverCells(encounter, plan)
                    .Concat(encounter.Profile.MortarCapable ? MortarFootprint(encounter.MortarCell) : Enumerable.Empty<GridPoint>());
                foreach (GridPoint point in plan.Compound.Points().Concat(plan.FieldCells).Concat(encounterMutationCells).Distinct())
                {
                    Plant plant = ToCell(point).GetPlant(map);
                    if (plant == null) continue;
                    removedPlants.Add(new PlantRestore { Cell = plant.Position, Def = plant.def, Growth = plant.Growth, Sown = plant.sown });
                    plant.Destroy(DestroyMode.Vanish);
                }
                ThingDef wallStuff = RequireDef(plan.LocalWallStuff);
                HashSet<GridPoint> coolerCells = new HashSet<GridPoint>(plan.Placements
                    .Where(p => p.Kind == PhysicalPlacementKind.Cooler).Select(p => p.Cell));
                foreach (GridPoint cell in plan.Perimeter)
                    spawned.Add(SpawnBuilding(ThingDefOf.Wall, wallStuff, ToCell(cell), map, settlement.Faction, Rot4.North));
                foreach (GridPoint gate in plan.Gates)
                    spawned.Add(SpawnBuilding(ThingDefOf.Door, wallStuff, ToCell(gate), map, settlement.Faction, Rot4.North));
                foreach (GridPoint cell in plan.ReinforcementPerimeter)
                    spawned.Add(SpawnBuilding(ThingDefOf.Wall, wallStuff, ToCell(cell), map, settlement.Faction, Rot4.North));
                foreach (GridPoint gate in plan.ReinforcementGates)
                    spawned.Add(SpawnBuilding(ThingDefOf.Door, wallStuff, ToCell(gate), map, settlement.Faction, Rot4.North));

                foreach (PhysicalFacility facility in plan.Facilities.Where(f => f.Roofed))
                {
                    foreach (GridPoint point in facility.Bounds.Points())
                    {
                        bool edge = point.X == facility.Bounds.MinX || point.X == facility.Bounds.MaxX ||
                            point.Z == facility.Bounds.MinZ || point.Z == facility.Bounds.MaxZ;
                        if (edge && !point.Equals(facility.Door) && !coolerCells.Contains(point))
                            spawned.Add(SpawnBuilding(ThingDefOf.Wall, wallStuff, ToCell(point), map, settlement.Faction, Rot4.North));
                    }
                    spawned.Add(SpawnBuilding(ThingDefOf.Door, wallStuff, ToCell(facility.Door), map, settlement.Faction, Rot4.North));
                    foreach (GridPoint point in facility.Bounds.Points())
                    {
                        IntVec3 cell = ToCell(point);
                        if (!changedRoofs.ContainsKey(cell)) changedRoofs[cell] = map.roofGrid.RoofAt(cell);
                        if (!RoofCollapseUtility.WithinRangeOfRoofHolder(cell, map, assumeNonNoRoofCellsAreRoofed: true))
                            throw new InvalidOperationException("P4 constructed roof lacks a verified roof holder: " + cell);
                        map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                    }
                }

                List<PhysicalPlacement> conduits = plan.Placements.Where(p => p.Kind == PhysicalPlacementKind.Conduit).ToList();
                foreach (PhysicalPlacement placement in plan.Placements.Where(p => p.Kind != PhysicalPlacementKind.Conduit &&
                    p.Kind != PhysicalPlacementKind.FoodStock && p.Kind != PhysicalPlacementKind.FuelStock))
                {
                    Thing thing = SpawnPlacement(placement, map, settlement.Faction, wallStuff);
                    spawned.Add(thing);
                    ConfigurePlacement(thing, placement);
                }
                foreach (PhysicalPlacement placement in conduits)
                {
                    IntVec3 cell = ToCell(placement.Cell);
                    if (cell.GetTransmitter(map) != null) continue;
                    spawned.Add(SpawnPlacement(placement, map, settlement.Faction, wallStuff));
                }

                string coverDefName = profile.Has(SettlementCapability.Electricity) ? "Sandbags" : "Barricade";
                ThingDef coverDef = RequireDef(coverDefName);
                ThingDef coverStuff = profile.Has(SettlementCapability.Electricity) ? ThingDefOf.Steel : wallStuff;
                foreach (GridPoint coverCell in EncounterCoverCells(encounter, plan))
                    spawned.Add(SpawnBuilding(coverDef, coverStuff, ToCell(coverCell), map, settlement.Faction, Rot4.North));

                Thing mortar = null;
                List<Thing> combatStocks = new List<Thing>();
                if (encounter.Profile.MortarCapable)
                {
                    mortar = SpawnBuilding(ThingDefOf.Turret_Mortar, ThingDefOf.Steel, ToCell(encounter.MortarCell),
                        map, settlement.Faction, Rot4.North);
                    spawned.Add(mortar);
                    IntVec3 actualInteraction = mortar.InteractionCell;
                    if (!actualInteraction.InBounds(map) || !actualInteraction.Standable(map) || map.roofGrid.Roofed(actualInteraction))
                        throw new InvalidOperationException("Mortar interaction cell is not reachable and unroofed: " + actualInteraction);
                }

                int cropCount = 0;
                ThingDef cropDef = RequireDef(profile.CropDef);
                foreach (GridPoint point in plan.FieldCells.Take(profile.RequiredCropCells))
                {
                    IntVec3 cell = ToCell(point);
                    Plant plant = (Plant)ThingMaker.MakeThing(cropDef);
                    plant.sown = true;
                    plant.Growth = 0.35f;
                    spawned.Add(GenSpawn.Spawn(plant, cell, map, Rot4.North, WipeMode.Vanish));
                    cropCount++;
                }
                if (forceFailure) throw new InvalidOperationException("forced P2 partial-spawn failure");

                PhysicalFacility foodFacility = plan.Facilities.Single(f => f.Kind ==
                    (profile.Has(SettlementCapability.Electricity) ? FacilityKind.Freezer : FacilityKind.Pantry));
                SpawnStacks(RequireDef(profile.Has(SettlementCapability.Electricity) ? "MealSimple" : "Pemmican"),
                    profile.StartupFoodUnits, InteriorCells(foodFacility), map, spawned);
                int internalFuel = 0;
                foreach (Thing thing in spawned)
                {
                    CompRefuelable refuelable = thing.TryGetComp<CompRefuelable>();
                    if (refuelable == null) continue;
                    int add = Math.Min(5, profile.StartupFuelUnits - internalFuel);
                    if (thing.def.defName == "WoodFiredGenerator") add = Math.Min(15, profile.StartupFuelUnits - internalFuel);
                    if (add > 0) { refuelable.Refuel(add); internalFuel += add; }
                }
                PhysicalFacility storage = plan.Facilities.Single(f => f.Kind == FacilityKind.BulkStorage);
                SpawnStacks(ThingDefOf.WoodLog, profile.StartupFuelUnits - internalFuel,
                    InteriorCells(storage), map, spawned);
                PhysicalFacility medical = plan.Facilities.Single(f => f.Kind == FacilityKind.Medical);
                SpawnStacks(RequireDef(profile.Has(SettlementCapability.Electricity) ? "MedicineIndustrial" : "MedicineHerbal"),
                    10, InteriorCells(medical), map, spawned);

                if (encounter.Profile.MortarCapable)
                {
                    Queue<IntVec3> magazineCells = new Queue<IntVec3>(InteriorCells(storage)
                        .Where(c => c.Standable(map) && c.GetEdifice(map) == null &&
                            !c.GetThingList(map).Any(t => t.def.category == ThingCategory.Item)));
                    foreach (CombatSupply supply in encounter.Supplies)
                    {
                        if (supply.DefName == "ReinforcedBarrel" && Find.Storyteller.difficulty.classicMortars) continue;
                        if (magazineCells.Count == 0) throw new InvalidOperationException("No P3 magazine cell remains for " + supply.DefName);
                        Thing stock = ThingMaker.MakeThing(RequireDef(supply.DefName));
                        stock.stackCount = Math.Min(stock.def.stackLimit, supply.Count);
                        stock = GenSpawn.Spawn(stock, magazineCells.Dequeue(), map, Rot4.North, WipeMode.Vanish);
                        spawned.Add(stock);
                        combatStocks.Add(stock);
                    }
                }

                map.powerNetManager.UpdatePowerNetsAndConnections_First();
                Thing battery = spawned.FirstOrDefault(t => t.TryGetComp<CompPowerBattery>() != null);
                battery?.TryGetComp<CompPowerBattery>()?.SetStoredEnergyPct(0.5f);

                List<Pawn> defenders = SpawnResidents(map, settlement, profile, plan, spawned);
                map.GetComponent<P3GarrisonController>().Initialize(settlement, plan, encounter, defenders, spawned, mortar, combatStocks);
                LordMaker.MakeNewLord(settlement.Faction, new LordJob_P3Garrison(settlement.ID), map, defenders);

                Thing wood = spawned.FirstOrDefault(t => t.def == ThingDefOf.WoodLog);
                IntVec3 woodCell = wood?.Position ?? ToCell(storage.Door);
                IntVec3 markerWall = ToCell(plan.Perimeter[0]);
                P0SettlementState state = Current.Game.GetComponent<P0WorldState>().GetOrCreate(settlement.ID, settlement.Faction);
                state.generationCount++;
                state.woodRemaining = wood?.stackCount ?? 0;
                state.pawnIds = defenders.Select(p => p.thingIDNumber).ToList();
                map.GetComponent<P0MapState>().Initialize(settlement, ToCell(new GridPoint(plan.Compound.MinX, plan.Compound.MinZ)),
                    woodCell, markerWall, P0MapAudit.TerrainRoofChecksum(map));
                P2RuntimeAudit.Initialize(map, settlement, run, spawned, defenders, internalFuel, cropCount);

                string after = P0MapAudit.TerrainRoofChecksum(map);
                string terrainOnlyAfter = P0MapAudit.TerrainChecksum(map);
                P2Log.Message("SPAWN_COMPLETE", "path=" + path + " profile=" + profile.Id + " planDigest=" + plan.Digest +
                    " things=" + spawned.Count + " residents=" + defenders.Count + " crops=" + cropCount +
                    " encounterDigest=" + encounter.Digest + " posts=" + encounter.Posts.Count +
                    " mortar=" + encounter.Profile.MortarCapable + " combatStocks=" + combatStocks.Sum(t => t.stackCount) +
                    " startupFoodUnits=" + profile.StartupFoodUnits + " startupFuelUnits=" + profile.StartupFuelUnits +
                    " internalFuel=" + internalFuel + " terrainBefore=" + terrainBefore + " terrainAfter=" + after +
                    " terrainPreserved=" + (terrainOnlyBefore == terrainOnlyAfter) +
                    " plannedRoofChange=" + (terrainBefore != after) + " roofsAdded=" + changedRoofs.Count +
                    " archetype=" + plan.Archetype + " excavatedRocks=" + removedRocks.Count +
                    " unownedRockPreserved=" + (unownedRockBefore == P0MapAudit.NaturalRockChecksum(map, ownedExcavation)) +
                    " retainedThickRoofs=" + plan.TerrainMutations.RetainedThickRoofCells.Count +
                    " noTrapsOrIEDs=" + !map.listerThings.AllThings.Any(P0MapAudit.IsProhibitedDefense));
                failure = null;
                return true;
            }
            catch (Exception ex)
            {
                for (int i = spawned.Count - 1; i >= 0; i--)
                    if (!spawned[i].Destroyed) spawned[i].Destroy(DestroyMode.Vanish);
                foreach (RockRestore restore in removedRocks)
                {
                    if (restore.Cell.GetEdifice(map) != null) continue;
                    GenSpawn.Spawn(ThingMaker.MakeThing(restore.Def), restore.Cell, map, Rot4.North, WipeMode.Vanish);
                }
                foreach (KeyValuePair<IntVec3, RoofDef> pair in changedRoofs) map.roofGrid.SetRoof(pair.Key, pair.Value);
                foreach (PlantRestore restore in removedPlants)
                {
                    if (restore.Cell.GetPlant(map) != null) continue;
                    Plant plant = (Plant)ThingMaker.MakeThing(restore.Def);
                    plant.Growth = restore.Growth;
                    plant.sown = restore.Sown;
                    GenSpawn.Spawn(plant, restore.Cell, map, Rot4.North, WipeMode.Vanish);
                }
                failure = ex.GetType().Name + ":" + ex.Message;
                P2Log.Error("SPAWN_RECOVERY", "path=" + path + " exception=" + failure +
                    " ownedThingsRemoved=" + spawned.Count + " roofsRestored=" + changedRoofs.Count +
                    " plantsRestored=" + removedPlants.Count + " rocksRestored=" + removedRocks.Count +
                    " unownedRockRestored=" + (unownedRockBefore == P0MapAudit.NaturalRockChecksum(map, ownedExcavation)) +
                    " checksumRestored=" + (P0MapAudit.TerrainRoofChecksum(map) == terrainBefore));
                return false;
            }
        }

        private static List<Pawn> SpawnResidents(Map map, Settlement settlement, PhysicalSettlementProfile profile,
            PhysicalSettlementPlan plan, List<Thing> spawned)
        {
            PawnKindDef kind = settlement.Faction.def.basicMemberKind ?? PawnKindDefOf.Pirate;
            List<IntVec3> cells = plan.Logistics.Select(ToCell).Where(c => c.Standable(map) && c.GetEdifice(map) == null).ToList();
            List<Pawn> residents = new List<Pawn>();
            for (int i = 0; i < profile.Residents; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kind, settlement.Faction, map.Tile);
                Equip(pawn, profile, i);
                GenSpawn.Spawn(pawn, cells[i * Math.Max(1, cells.Count / profile.Residents)], map);
                spawned.Add(pawn);
                residents.Add(pawn);
            }
            return residents.OrderBy(p => p.thingIDNumber).ToList();
        }

        private static void Equip(Pawn pawn, PhysicalSettlementProfile profile, int index)
        {
            ThingDef weaponDef = DefDatabase<ThingDef>.GetNamedSilentFail(profile.WeaponDefs[index % profile.WeaponDefs.Count]);
            if (weaponDef != null && weaponDef.IsWeapon)
            {
                pawn.equipment.DestroyAllEquipment();
                pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(weaponDef,
                    weaponDef.MadeFromStuff ? GenStuff.DefaultStuffFor(weaponDef) : null));
            }
            pawn.apparel.DestroyAll();
            foreach (string armorName in profile.ArmorDefs)
            {
                ThingDef armorDef = DefDatabase<ThingDef>.GetNamedSilentFail(armorName);
                if (armorDef == null) continue;
                Apparel apparel = (Apparel)ThingMaker.MakeThing(armorDef,
                    armorDef.MadeFromStuff ? GenStuff.DefaultStuffFor(armorDef) : null);
                if (apparel.PawnCanWear(pawn, ignoreGender: true)) pawn.apparel.Wear(apparel, false);
                else apparel.Destroy();
            }
        }

        private static Thing SpawnPlacement(PhysicalPlacement placement, Map map, Faction faction, ThingDef localStuff)
        {
            ThingDef def = RequireDef(placement.DefName);
            ThingDef requested = string.IsNullOrEmpty(placement.StuffDefName) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(placement.StuffDefName);
            ThingDef stuff = ResolveStuff(def, requested ?? localStuff);
            Thing thing = ThingMaker.MakeThing(def, stuff);
            if (thing.def.CanHaveFaction) thing.SetFactionDirect(faction);
            return GenSpawn.Spawn(thing, ToCell(placement.Cell), map, ParseRotation(placement.Rotation), WipeMode.Vanish);
        }

        private static void ConfigurePlacement(Thing thing, PhysicalPlacement placement)
        {
            Building_Bed bed = thing as Building_Bed;
            if (bed != null)
            {
                if (placement.Kind == PhysicalPlacementKind.PrisonBed) bed.ForPrisoners = true;
                if (placement.Kind == PhysicalPlacementKind.MedicalBed) bed.Medical = true;
            }
            if (thing.def.defName == "Cooler") thing.TryGetComp<CompTempControl>().TargetTemperature = -5f;
            if (thing is IBillGiver billGiver && (thing.def.defName == "ElectricStove" || thing.def.defName == "FueledStove"))
            {
                RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimple");
                if (recipe != null) billGiver.BillStack.AddBill(new Bill_Production(recipe));
            }
        }

        private static Thing SpawnBuilding(ThingDef def, ThingDef stuff, IntVec3 cell, Map map, Faction faction, Rot4 rotation)
        {
            Thing thing = ThingMaker.MakeThing(def, ResolveStuff(def, stuff));
            thing.SetFactionDirect(faction);
            return GenSpawn.Spawn(thing, cell, map, rotation, WipeMode.Vanish);
        }

        private static ThingDef ResolveStuff(ThingDef def, ThingDef requested)
        {
            if (!def.MadeFromStuff) return null;
            if (requested != null && GenStuff.AllowedStuffsFor(def).Contains(requested)) return requested;
            return GenStuff.DefaultStuffFor(def);
        }

        private static void SpawnStacks(ThingDef def, int count, IEnumerable<IntVec3> candidateCells,
            Map map, List<Thing> spawned)
        {
            Queue<IntVec3> cells = new Queue<IntVec3>(candidateCells.Where(c => c.Standable(map) && c.GetEdifice(map) == null));
            int remaining = count;
            while (remaining > 0)
            {
                if (cells.Count == 0) throw new InvalidOperationException("No stock cell remains for " + def.defName);
                Thing thing = ThingMaker.MakeThing(def);
                thing.stackCount = Math.Min(def.stackLimit, remaining);
                remaining -= thing.stackCount;
                spawned.Add(GenSpawn.Spawn(thing, cells.Dequeue(), map, Rot4.North, WipeMode.Vanish));
            }
        }

        private static IEnumerable<IntVec3> InteriorCells(PhysicalFacility facility)
        {
            for (int z = facility.Bounds.MinZ + 1; z < facility.Bounds.MaxZ; z++)
                for (int x = facility.Bounds.MinX + 1; x < facility.Bounds.MaxX; x++)
                    yield return new IntVec3(x, 0, z);
        }

        private static IEnumerable<GridPoint> EncounterCoverCells(EncounterPlan encounter, PhysicalSettlementPlan plan)
        {
            foreach (DefensivePost post in encounter.Posts.Where(p => !p.Inner && !p.Mortar && p.MinimumRange > 0))
            {
                if (post.Cell.Z < plan.Compound.MinZ) yield return new GridPoint(post.Cell.X, post.Cell.Z - 1);
                else if (post.Cell.Z > plan.Compound.MaxZ) yield return new GridPoint(post.Cell.X, post.Cell.Z + 1);
                else if (post.Cell.X < plan.Compound.MinX) yield return new GridPoint(post.Cell.X - 1, post.Cell.Z);
                else if (post.Cell.X > plan.Compound.MaxX) yield return new GridPoint(post.Cell.X + 1, post.Cell.Z);
            }
        }

        private static IEnumerable<GridPoint> MortarFootprint(GridPoint origin)
        {
            yield return origin;
            yield return new GridPoint(origin.X + 1, origin.Z);
            yield return new GridPoint(origin.X, origin.Z + 1);
            yield return new GridPoint(origin.X + 1, origin.Z + 1);
        }

        private static ThingDef RequireDef(string name) => DefDatabase<ThingDef>.GetNamed(name, true);
        private static Rot4 ParseRotation(string value) => value == "East" ? Rot4.East : value == "South" ? Rot4.South : value == "West" ? Rot4.West : Rot4.North;
        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);

        // Kept thick roofs are safe only when an intact natural rock holder is
        // within vanilla's 6.9-cell radius. Constructed walls never satisfy this
        // audit, even if vanilla's broader roof-holder scan would accept them.
        private static bool HasNaturalRockRoofHolder(Map map, IntVec3 roofedCell)
        {
            const float radiusSquared = 6.9f * 6.9f;
            int radius = 7;
            for (int dx = -radius; dx <= radius; dx++)
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (dx * dx + dz * dz > radiusSquared) continue;
                    IntVec3 candidate = new IntVec3(roofedCell.x + dx, 0, roofedCell.z + dz);
                    Building edifice = candidate.InBounds(map) ? candidate.GetEdifice(map) : null;
                    if (edifice?.def?.building?.isNaturalRock == true) return true;
                }
            return false;
        }
    }
}
