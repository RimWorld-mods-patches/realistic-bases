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

        public static bool TrySpawn(Map map, Settlement settlement, P2PlanningRun run, string terrainBefore,
            string path, bool forceFailure, out string failure)
        {
            PhysicalSettlementPlan plan = run.Result.Plan;
            PhysicalSettlementProfile profile = run.Input.Profile;
            List<Thing> spawned = new List<Thing>();
            Dictionary<IntVec3, RoofDef> changedRoofs = new Dictionary<IntVec3, RoofDef>();
            List<PlantRestore> removedPlants = new List<PlantRestore>();
            string terrainOnlyBefore = P0MapAudit.TerrainChecksum(map);
            try
            {
                foreach (GridPoint point in plan.Compound.Points().Concat(plan.FieldCells).Distinct())
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

                map.powerNetManager.UpdatePowerNetsAndConnections_First();
                Thing battery = spawned.FirstOrDefault(t => t.TryGetComp<CompPowerBattery>() != null);
                battery?.TryGetComp<CompPowerBattery>()?.SetStoredEnergyPct(0.5f);

                List<Pawn> defenders = SpawnResidents(map, settlement, profile, plan, spawned);
                IntVec3 hold = ToCell(plan.Facilities.Single(f => f.Kind == FacilityKind.Defense).InteractionCells[0]);
                LordMaker.MakeNewLord(settlement.Faction, new LordJob_P0Garrison(hold, ToCell(plan.Compound.Points().ElementAt(plan.Compound.Area / 2))), map, defenders);

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
                    " startupFoodUnits=" + profile.StartupFoodUnits + " startupFuelUnits=" + profile.StartupFuelUnits +
                    " internalFuel=" + internalFuel + " terrainBefore=" + terrainBefore + " terrainAfter=" + after +
                    " terrainPreserved=" + (terrainOnlyBefore == terrainOnlyAfter) +
                    " plannedRoofChange=" + (terrainBefore != after) + " roofsAdded=" + changedRoofs.Count +
                    " noTrapsOrIEDs=" + !map.listerThings.AllThings.Any(P0MapAudit.IsProhibitedDefense));
                failure = null;
                return true;
            }
            catch (Exception ex)
            {
                for (int i = spawned.Count - 1; i >= 0; i--)
                    if (!spawned[i].Destroyed) spawned[i].Destroy(DestroyMode.Vanish);
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
                    " plantsRestored=" + removedPlants.Count +
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

        private static ThingDef RequireDef(string name) => DefDatabase<ThingDef>.GetNamed(name, true);
        private static Rot4 ParseRotation(string value) => value == "East" ? Rot4.East : value == "South" ? Rot4.South : value == "West" ? Rot4.West : Rot4.North;
        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }
}
