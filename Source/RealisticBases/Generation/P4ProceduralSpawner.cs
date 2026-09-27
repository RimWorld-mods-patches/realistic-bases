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
using Verse.AI;
using Verse.AI.Group;
using LayoutRoom = RealisticBases.Planning.LayoutRoom;

namespace RealisticBases.Generation
{
    // The live P4A path consumes ProceduralLayout directly. The P2 physical
    // plan and its adapter are deliberately absent from this mutation boundary.
    public static class P4ProceduralSpawner
    {
        public static bool TrySpawn(Map map, Settlement settlement, P4AMapPlanningRun run, out string failure,
            bool forceFailure = false)
        {
            failure = null;
            if (run?.Result?.Layout == null) { failure = "missing_procedural_layout"; return false; }
            ProceduralLayoutInput input = run.Input;
            ProceduralLayout layout = run.Result.Layout;
            P4BFieldWallPlan fieldWall = layout.FieldWall;
            if (layout.FieldCellsRequired > 0 && fieldWall == null)
            { failure = "missing_joint_field_wall"; return false; }
            IReadOnlyList<string> errors = ProceduralLayoutValidator.Validate(input, layout);
            if (errors.Count != 0) { failure = "invalid_layout:" + string.Join(",", errors); return false; }
            SettlementLoadout loadout = LayoutLoadoutModel.Build(input, layout);
            errors = ProceduralLayoutValidator.ValidateLoadout(input, layout, loadout);
            if (!loadout.Success || errors.Count != 0)
            { failure = "invalid_loadout:" + loadout.Rejection + ":" + string.Join(",", errors); return false; }

            List<GridPoint> roofsToBuild = layout.ConstructedRoof.Except(layout.RetainedThickRoof).ToList();
            TerrainMutationFootprint footprint = new TerrainMutationFootprint(layout.Excavation,
                layout.Excavation.Where(c => !layout.RetainedThickRoof.Contains(c) && input.Terrain[c].Has(TerrainTraits.Roofed)),
                roofsToBuild, layout.NaturalWalls, retainedThickRoofCells: layout.RetainedThickRoof);
            HashSet<GridPoint> coolers = new HashSet<GridPoint>(layout.AllFixtures
                .Where(f => f.Kind == LayoutFixtureKind.WallCooler).SelectMany(f => f.Cells));
            HashSet<GridPoint> natural = new HashSet<GridPoint>(layout.NaturalWalls.Concat(layout.Rooms.SelectMany(r => r.NaturalWalls)));
            // Cooler exhaust pockets are sealed by their own walls; their cleared roof is part of the
            // excavation-without-retained-roof clearance above.
            HashSet<GridPoint> walls = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.Gates.SelectMany(g => g.BandWalls))
                .Concat(layout.OuterWalls)
                .Concat(layout.ExhaustPocketWalls));
            HashSet<GridPoint> doors = new HashSet<GridPoint>(layout.Gates.Select(g => g.Cell)
                .Concat(layout.Gates.SelectMany(g => g.BandDoors)).Concat(layout.Rooms.Select(r => r.Door)));
            foreach (LayoutRoom room in layout.Rooms)
                foreach (GridPoint c in room.Bounds.Points())
                    if (room.IsEdge(c) && !natural.Contains(c) && !doors.Contains(c) && !coolers.Contains(c)) walls.Add(c);
            walls.ExceptWith(doors);
            walls.ExceptWith(coolers);
            walls.ExceptWith(natural);
            if (fieldWall != null) walls.UnionWith(fieldWall.Walls);
            if (fieldWall != null)
            {
                doors.Add(fieldWall.InnerGate.Value);
                doors.Add(fieldWall.OuterGate);
            }
            HashSet<GridPoint> structures = new HashSet<GridPoint>(walls.Concat(doors).Concat(layout.AllFixtures
                .Where(f => f.Kind != LayoutFixtureKind.Stockpile && f.Kind != LayoutFixtureKind.MortarCourt)
                .SelectMany(f => f.Cells)).Concat(layout.PowerYards.SelectMany(y => y.Cells))
                .Concat(fieldWall == null ? Enumerable.Empty<GridPoint>() : fieldWall.CoverCells));
            if (layout.Court != null) structures.UnionWith(layout.Court.Cells);
            List<GridPoint> serviceCells = layout.OuterSections
                .SelectMany(section => section.ServiceLane(layout.Gates[section.GateIndex])).Distinct().ToList();

            Dictionary<LayoutFixture, GridPoint> fixtureOrigins = new Dictionary<LayoutFixture, GridPoint>();
            Dictionary<LayoutPowerYard, GridPoint> yardOrigins = new Dictionary<LayoutPowerYard, GridPoint>();
            try
            {
                foreach (LayoutFixture fixture in layout.AllFixtures.Where(f =>
                    f.Kind != LayoutFixtureKind.Stockpile && f.Kind != LayoutFixtureKind.MortarCourt))
                    fixtureOrigins[fixture] = FindOrigin(Require(fixture.DefName), Rotation(fixture.Rotation), fixture.Cells);
                foreach (LayoutPowerYard yard in layout.PowerYards)
                    yardOrigins[yard] = FindOrigin(Require(yard.DefName), Rotation(yard.Rotation), yard.Cells);
            }
            catch (Exception ex) { failure = "spawn_origin_rejected:" + ex.Message; return false; }
            IReadOnlyList<GridPoint> conduitCells;
            try { conduitCells = PlanConduits(map, input, layout); }
            catch (Exception ex) { failure = "power_route_rejected:" + ex.Message; return false; }

            IReadOnlyList<GridPoint> fields = fieldWall == null ? SelectFields(input, layout) : fieldWall.Fields;
            if (fields.Count < layout.FieldCellsRequired)
            { failure = "insufficient_reachable_fertile_field:" + fields.Count + "/" + layout.FieldCellsRequired; return false; }
            if (fieldWall != null)
                P2Log.Message("PROCEDURAL_FIELD_WALL_PLAN", "map=" + map.uniqueID +
                    " digest=" + fieldWall.Digest + " fieldCells=" + fieldWall.Fields.Count +
                    " wallCells=" + fieldWall.Walls.Count + " gates=2 cover=" + fieldWall.CoverCells.Count +
                    " exteriorPosts=" + fieldWall.ExteriorPosts.Count);
            foreach (GridPoint c in structures.Concat(fields).Concat(serviceCells)
                .Concat(fieldWall == null ? Enumerable.Empty<GridPoint>() : fieldWall.ExteriorPosts).Distinct())
            {
                IntVec3 cell = ToCell(c);
                if (!cell.InBounds(map)) { failure = "spawn_cell_out_of_bounds:" + c; return false; }
                Building edifice = cell.GetEdifice(map);
                if (edifice != null && !(layout.Excavation.Contains(c) && edifice.def?.building?.isNaturalRock == true) &&
                    !(natural.Contains(c) && edifice.def?.building?.isNaturalRock == true))
                { failure = "unowned_edifice_on_spawn_cell:" + c + ":" + edifice.def.defName; return false; }
            }
            foreach (GridPoint c in conduitCells)
            {
                Building edifice = ToCell(c).GetEdifice(map);
                if (edifice != null && edifice.def?.building?.isNaturalRock == true && !layout.Excavation.Contains(c))
                { failure = "power_route_crosses_unowned_rock:" + c; return false; }
            }

            EncounterPlan encounter;
            try
            {
                IReadOnlyCollection<GridPoint> publicCells = ProceduralLayoutValidator.PublicCirculation(input, layout);
                LayoutRoom storage = layout.Rooms.First(r => r.Kind == LayoutRoomKind.Storage);
                EncounterProfile profile = layout.Court != null ? P3EncounterProfiles.Industrial :
                    input.Requirements.Capabilities.HasFlag(SettlementCapability.Electricity)
                        ? P3EncounterProfiles.IndustrialNoMortar : P3EncounterProfiles.Tribal;
                encounter = GeometryEncounterPlanner.Plan(new GeometryEncounterInput(layout.Digest, profile,
                    layout.Enclosure, walls.Concat(natural), layout.Gates.Select(g =>
                        new KeyValuePair<GridPoint, GridPoint>(g.Cell, g.Inside)), publicCells,
                    storage.Interior.Points(), layout.Court?.Cells.FirstOrDefault(), layout.Court?.Access.FirstOrDefault()));
                if (fieldWall != null)
                {
                    // The compound reservation also covers the short exterior
                    // crossing to the field. Vanilla pathfinding may leave the
                    // literal wall interior through a different compound gate.
                    List<GridPoint> defended = layout.Enclosure.Concat(layout.JointSite.Compound.Points())
                        .Concat(layout.JointSite.Field.Points())
                        .Concat(fieldWall.Access)
                        .Concat(fieldWall.ArrivalRoute.TakeWhile(c => !c.Equals(fieldWall.OuterGate)))
                        .Concat(fieldWall.ExteriorPosts).Concat(new[] { fieldWall.OuterGate }).Distinct().ToList();
                    GridRect bounds = new GridRect(defended.Min(c => c.X), defended.Min(c => c.Z),
                        defended.Max(c => c.X) - defended.Min(c => c.X) + 1,
                        defended.Max(c => c.Z) - defended.Min(c => c.Z) + 1);
                    encounter = new EncounterPlan(layout.Digest, profile, encounter.Posts.Concat(
                        fieldWall.ExteriorPosts.Select((c, i) => new DefensivePost("field-outer-" + i,
                            Sector(c, encounter.SupportCenter), c, 8, 38, true, true, true, false, false, true))),
                        bounds, encounter.SupportCenter, encounter.InnerFallback,
                        encounter.MortarCell, encounter.MortarInteractionCell, encounter.MagazineCell,
                        encounter.Supplies, defended);
                }
                // Diagnostic only until P4B selects exterior positions and cover.
                // Closed gates are opaque; proximity to a wall cannot assert a shot.
                List<GridPoint> approaches = layout.Gates.SelectMany(g => g.ExteriorRoute.Take(12)).Distinct().ToList();
                HashSet<GridPoint> opaque = new HashSet<GridPoint>(walls.Concat(natural).Concat(doors));
                List<DefensivePost> directPosts = encounter.Posts.Where(p => !p.Mortar).ToList();
                int usefulPosts = directPosts.Count(p => approaches.Any(c =>
                    P4BFireLane.CanEngage(p.Cell, c, p.MinimumRange, p.MaximumRange, opaque)));
                int coveredApproaches = approaches.Count(c => directPosts.Any(p =>
                    P4BFireLane.CanEngage(p.Cell, c, p.MinimumRange, p.MaximumRange, opaque)));
                P2Log.Message("PROCEDURAL_FIRE_LANE_PLAN", "map=" + map.uniqueID + " closedGate=true postsWithShot=" +
                    usefulPosts + "/" + directPosts.Count + " approachSamplesCovered=" + coveredApproaches +
                    "/" + approaches.Count);
            }
            catch (Exception ex) { failure = "geometry_encounter_rejected:" + ex.Message; return false; }

            P4SpawnTransaction tx;
            try { tx = new P4SpawnTransaction(map, footprint); }
            catch (Exception ex) { failure = "mutation_preflight_rejected:" + ex.Message; return false; }
            List<Pawn> defenders = new List<Pawn>();
            List<Thing> combatStocks = new List<Thing>();
            Thing mortar = null;
            int crops = 0;
            try
            {
                tx.Excavate();
                foreach (GridPoint c in layout.Enclosure.Concat(structures).Concat(fields).Concat(serviceCells)
                    .Concat(layout.Gates.SelectMany(g => g.OuterVestibule.Concat(g.ExteriorRoute)))
                    .Concat(fieldWall == null ? Enumerable.Empty<GridPoint>() :
                        layout.JointSite.Field.Points().Concat(fieldWall.ExteriorPosts).Concat(fieldWall.Access)).Distinct())
                    tx.ClearPlant(c);
                ThingDef wallStuff = Require(layout.WallStuff);
                foreach (GridPoint c in walls.OrderBy(c => c)) Spawn(tx, ThingDefOf.Wall, wallStuff, c, map, settlement.Faction, Rot4.North);
                foreach (GridPoint c in doors.OrderBy(c => c)) Spawn(tx, ThingDefOf.Door, wallStuff, c, map, settlement.Faction, Rot4.North);
                if (fieldWall != null)
                {
                    ThingDef coverDef = Require(fieldWall.CoverDefName);
                    foreach (GridPoint c in fieldWall.CoverCells)
                        Spawn(tx, coverDef, wallStuff, c, map, settlement.Faction, Rot4.North);
                }
                foreach (GridPoint c in roofsToBuild) tx.SetConstructedRoof(c);

                foreach (LayoutFixture fixture in layout.AllFixtures)
                {
                    if (fixture.Kind == LayoutFixtureKind.Stockpile || fixture.Kind == LayoutFixtureKind.MortarCourt) continue;
                    Thing thing = Spawn(tx, Require(fixture.DefName), ResolveStuff(fixture.Stuff), fixtureOrigins[fixture], map,
                        settlement.Faction, Rotation(fixture.Rotation));
                    if (!fixture.Cells.All(c => thing.OccupiedRect().Contains(ToCell(c))) || thing.OccupiedRect().Area != fixture.Cells.Count)
                        throw new InvalidOperationException("Fixture footprint differs from plan: " + fixture.DefName + " at " + fixture.Cells[0]);
                    LayoutRoom owner = layout.Rooms.FirstOrDefault(room => room.Id == fixture.RoomId);
                    Configure(thing, fixture.Kind, owner?.Kind);
                }
                foreach (LayoutPowerYard yard in layout.PowerYards)
                    Spawn(tx, Require(yard.DefName), null, yardOrigins[yard], map, settlement.Faction, Rotation(yard.Rotation));
                ThingDef conduitDef = Require("PowerConduit");
                foreach (GridPoint c in conduitCells)
                    if (ToCell(c).GetTransmitter(map) == null)
                        Spawn(tx, conduitDef, null, c, map, settlement.Faction, Rot4.North);
                if (layout.Court != null)
                    mortar = Spawn(tx, ThingDefOf.Turret_Mortar, ThingDefOf.Steel, encounter.MortarCell,
                        map, settlement.Faction, Rot4.North);

                ThingDef crop = DefDatabase<ThingDef>.GetNamedSilentFail(run.ProfileId == "tribal-temperate-v1" ? "Plant_Rice" : "Plant_Rice") ?? Require("Plant_Rice");
                foreach (GridPoint c in fields)
                {
                    Plant plant = (Plant)ThingMaker.MakeThing(crop);
                    plant.sown = true;
                    plant.Growth = 0.35f;
                    tx.Own(GenSpawn.Spawn(plant, ToCell(c), map, Rot4.North, WipeMode.Vanish));
                    crops++;
                }
                if (forceFailure) throw new InvalidOperationException("forced procedural partial-spawn failure");

                LayoutRoom food = layout.Rooms.First(r => r.Kind == LayoutRoomKind.Freezer || r.Kind == LayoutRoomKind.Pantry);
                LayoutRoom storageRoom = layout.Rooms.First(r => r.Kind == LayoutRoomKind.Storage);
                LayoutRoom medical = layout.Rooms.First(r => r.Kind == LayoutRoomKind.Medical);
                SpawnStacks(tx, Require(food.Kind == LayoutRoomKind.Freezer ? "MealSimple" : "Pemmican"),
                    input.Requirements.Residents * 12, food.Interior.Points(), map);
                int fuel = layout.Power?.FuelStock ?? 0;
                int internalFuel = 0;
                if (layout.Power != null)
                    foreach (Thing generator in tx.OwnedThings.Where(t => t.def.defName == layout.Power.GeneratorDef))
                    {
                        CompRefuelable refuelable = generator.TryGetComp<CompRefuelable>();
                        if (refuelable == null) continue;
                        int add = Math.Min(fuel, Math.Min(15, (int)Math.Ceiling(refuelable.Props.fuelCapacity)));
                        if (add <= 0) continue;
                        refuelable.Refuel(add);
                        fuel -= add;
                        internalFuel += add;
                    }
                if (fuel > 0) SpawnStacks(tx, Require(layout.Power.FuelDef), fuel, storageRoom.Interior.Points(), map);
                foreach (LoadoutItem item in loadout.Medicine) SpawnStacks(tx, Require(item.DefName), item.Count, medical.Interior.Points(), map);
                foreach (LoadoutItem item in loadout.Components.Concat(loadout.Armoury))
                    SpawnStacks(tx, Require(item.DefName), item.Count, storageRoom.Interior.Points(), map, item.Stuff, item.Quality);
                foreach (CombatSupply supply in encounter.Supplies)
                {
                    if (supply.DefName == "ReinforcedBarrel" && Find.Storyteller.difficulty.classicMortars) continue;
                    combatStocks.AddRange(SpawnStacks(tx, Require(supply.DefName), supply.Count, storageRoom.Interior.Points(), map));
                }

                map.powerNetManager.UpdatePowerNetsAndConnections_First();
                foreach (Thing battery in tx.OwnedThings.Where(t => t.TryGetComp<CompPowerBattery>() != null))
                    battery.TryGetComp<CompPowerBattery>().SetStoredEnergyPct(0.5f);
                PawnKindDef kind = settlement.Faction.def.basicMemberKind ?? PawnKindDefOf.Pirate;
                List<GridPoint> homes = layout.Rooms.Where(r => r.Kind == LayoutRoomKind.Barracks || r.Kind == LayoutRoomKind.Bedroom)
                    .SelectMany(r => r.Interior.Points()).Where(c => ToCell(c).Standable(map) && ToCell(c).GetEdifice(map) == null).ToList();
                if (homes.Count < input.Requirements.Residents) throw new InvalidOperationException("No resident spawn cells remain");
                for (int i = 0; i < input.Requirements.Residents; i++)
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(kind, settlement.Faction, map.Tile);
                    Equip(pawn, loadout.Residents[i]);
                    tx.Own(GenSpawn.Spawn(pawn, ToCell(homes[i]), map));
                    defenders.Add(pawn);
                }
                if (fieldWall != null)
                {
                    HashSet<IntVec3> defendedCells = new HashSet<IntVec3>(encounter.DefendedCells.Select(ToCell));
                    foreach (GridPoint post in fieldWall.ExteriorPosts)
                    {
                        int eligibleDefenders = 0;
                        List<string> routeDiagnostics = new List<string>();
                        foreach (Pawn pawn in defenders)
                            using (PawnPath path = map.pathFinder.FindPathNow(pawn.Position, ToCell(post),
                                TraverseParms.For(pawn), null, PathEndMode.OnCell))
                            {
                                IntVec3 firstOutside = path.Found
                                    ? path.NodesReversed.FirstOrDefault(c => !defendedCells.Contains(c)) : IntVec3.Invalid;
                                bool legal = path.Found && path.NodesReversed.Count <= 64 &&
                                    path.NodesReversed.All(defendedCells.Contains);
                                if (legal) eligibleDefenders++;
                                else routeDiagnostics.Add(pawn.LabelShort + ":found=" + path.Found +
                                    "/nodes=" + path.NodesReversed.Count + "/firstOutside=" + firstOutside);
                            }
                        if (eligibleDefenders < 2)
                            throw new InvalidOperationException("Outside field post lacks two P3-legal pawn paths: " +
                                post + " eligible=" + eligibleDefenders + " routes=" + string.Join(";", routeDiagnostics.Take(4)));
                    }
                    if (fieldWall.Fields.Any(c => map.roofGrid.RoofAt(ToCell(c)) != null ||
                        ToCell(c).GetPlant(map)?.def != crop))
                        throw new InvalidOperationException("Field crop missing or roofed after spawn");
                    int wildPlantsInField = layout.JointSite.Field.Points().Count(c =>
                        !fieldWall.Fields.Contains(c) && ToCell(c).GetPlant(map) != null);
                    if (wildPlantsInField != 0)
                        throw new InvalidOperationException("Established field retains wild plants: " + wildPlantsInField);
                    P2Log.Message("PROCEDURAL_FIELD_POST_PATH_AUDIT", "map=" + map.uniqueID +
                        " posts=" + fieldWall.ExteriorPosts.Count + " eachReachableByAtLeast=2" +
                        " cropsUnroofed=" + fieldWall.Fields.Count + " wildPlantsInField=0");
                }
                if (map.listerThings.AllThings.Any(P0MapAudit.IsProhibitedDefense))
                    throw new InvalidOperationException("Prohibited trap or IED detected");
                if (layout.Gates.Any(g => !(ToCell(g.Cell).GetEdifice(map) is Building_Door)))
                    throw new InvalidOperationException("A procedural gate did not spawn as a door");
                if (fieldWall != null)
                {
                    int builtFieldWalls = fieldWall.Walls.Count(c => ToCell(c).GetEdifice(map)?.def == ThingDefOf.Wall);
                    int builtFieldGates = new[] { fieldWall.InnerGate.Value, fieldWall.OuterGate }
                        .Count(c => ToCell(c).GetEdifice(map) is Building_Door);
                    int builtCover = fieldWall.CoverCells.Count(c => ToCell(c).GetThingList(map)
                        .Any(t => t.def.defName == fieldWall.CoverDefName));
                    int reachablePosts = fieldWall.ExteriorPosts.Count(c => map.reachability.CanReach(
                        ToCell(layout.Gates[fieldWall.GateIndex].Inside), ToCell(c), PathEndMode.OnCell,
                        TraverseParms.For(TraverseMode.PassDoors)));
                    if (builtFieldWalls != fieldWall.Walls.Count || builtFieldGates != 2 ||
                        builtCover != fieldWall.CoverCells.Count || reachablePosts != fieldWall.ExteriorPosts.Count)
                        throw new InvalidOperationException("Field enclosure realization failed: walls=" + builtFieldWalls +
                            "/" + fieldWall.Walls.Count + " gates=" + builtFieldGates + "/2 cover=" + builtCover +
                            "/" + fieldWall.CoverCells.Count + " reachablePosts=" + reachablePosts +
                            "/" + fieldWall.ExteriorPosts.Count);
                    P2Log.Message("PROCEDURAL_FIELD_WALL_AUDIT", "map=" + map.uniqueID + " walls=" + builtFieldWalls +
                        "/" + fieldWall.Walls.Count + " gates=" + builtFieldGates + "/2 cover=" + builtCover +
                        "/" + fieldWall.CoverCells.Count + " reachablePosts=" + reachablePosts +
                        "/" + fieldWall.ExteriorPosts.Count);
                }
                int realizedOuterWalls = layout.OuterWalls.Count(c => ToCell(c).GetEdifice(map)?.def == ThingDefOf.Wall);
                int plannedOuterWalls = layout.OuterWalls.Count();
                int plannedReturnCells = layout.OuterSections.Sum(section => section.LeftReturn.Count + section.RightReturn.Count);
                int realizedReturnCells = layout.OuterSections.SelectMany(section => section.LeftReturn.Concat(section.RightReturn))
                    .Count(c => ToCell(c).GetEdifice(map)?.def == ThingDefOf.Wall);
                int plannedAnchoredReturns = 2 * layout.OuterSections.Count;
                int realizedAnchoredReturns = layout.OuterSections.Sum(section =>
                {
                    LayoutGate gate = layout.Gates[section.GateIndex];
                    return new[] { -1, 1 }.Count(side =>
                    {
                        IReadOnlyList<GridPoint> arm = side < 0 ? section.Left : section.Right;
                        IReadOnlyList<GridPoint> path = side < 0 ? section.LeftReturn : section.RightReturn;
                        if (path.Count == 0 || path.Any(c => ToCell(c).GetEdifice(map)?.def != ThingDefOf.Wall)) return false;
                        GridPoint tangent = new GridPoint(gate.Outward.Z, gate.Outward.X);
                        GridPoint junction = arm.Count > 0 ? arm[arm.Count - 1] :
                            new GridPoint(gate.Cell.X + 2 * side * tangent.X + 2 * section.Layer * gate.Outward.X,
                                gate.Cell.Z + 2 * side * tangent.Z + 2 * section.Layer * gate.Outward.Z);
                        if (ToCell(junction).GetEdifice(map)?.def != ThingDefOf.Wall ||
                            Math.Abs(junction.X - path[0].X) + Math.Abs(junction.Z - path[0].Z) != 1) return false;
                        for (int j = 1; j < path.Count; j++)
                            if (Math.Abs(path[j - 1].X - path[j].X) + Math.Abs(path[j - 1].Z - path[j].Z) != 1) return false;
                        GridPoint terminal = path[path.Count - 1];
                        GridPoint parent = arm.Count == 0 && section.Layer == 1
                            ? new GridPoint(gate.Cell.X + side * tangent.X, gate.Cell.Z + side * tangent.Z)
                            : new GridPoint(terminal.X - gate.Outward.X, terminal.Z - gate.Outward.Z);
                        return Math.Abs(terminal.X - parent.X) + Math.Abs(terminal.Z - parent.Z) == 1 &&
                            ToCell(parent).GetEdifice(map) != null;
                    });
                });
                int realizedBandWalls = layout.Gates.SelectMany(g => g.BandWalls)
                    .Count(c => ToCell(c).GetEdifice(map)?.def == ThingDefOf.Wall);
                int plannedBandWalls = layout.Gates.Sum(g => g.BandWalls.Count);
                int realizedGateDoors = layout.Gates.SelectMany(g => new[] { g.Cell }.Concat(g.BandDoors))
                    .Count(c => ToCell(c).GetEdifice(map) is Building_Door);
                int plannedGateDoors = layout.Gates.Sum(g => 1 + g.BandDoors.Count);
                int reachableGates = layout.Gates.Count(g => map.reachability.CanReach(
                    ToCell(g.ExteriorRoute[g.ExteriorRoute.Count - 1]), ToCell(g.Inside), PathEndMode.OnCell,
                    TraverseParms.For(TraverseMode.PassDoors)));
                List<KeyValuePair<GridPoint, GridPoint>> serviceTermini = layout.OuterSections
                    .SelectMany(section => section.ServiceTermini(layout.Gates[section.GateIndex])
                        .Select(cell => new KeyValuePair<GridPoint, GridPoint>(layout.Gates[section.GateIndex].Inside, cell))).ToList();
                int clearServiceCells = serviceCells.Count(c => ToCell(c).Standable(map) && ToCell(c).GetEdifice(map) == null);
                int reachableServiceTermini = serviceTermini.Count(pair => map.reachability.CanReach(
                    ToCell(pair.Key), ToCell(pair.Value), PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)));
                if (realizedOuterWalls != plannedOuterWalls || realizedReturnCells != plannedReturnCells ||
                    realizedAnchoredReturns != plannedAnchoredReturns ||
                    realizedBandWalls != plannedBandWalls ||
                    realizedGateDoors != plannedGateDoors || reachableGates != layout.Gates.Count ||
                    clearServiceCells != serviceCells.Count || reachableServiceTermini != serviceTermini.Count)
                    throw new InvalidOperationException("Realized layered-wall or gate audit failed: outer=" + realizedOuterWalls +
                        "/" + plannedOuterWalls + " returnCells=" + realizedReturnCells + "/" + plannedReturnCells +
                        " anchoredReturns=" + realizedAnchoredReturns + "/" + plannedAnchoredReturns +
                        " bands=" + realizedBandWalls + "/" + plannedBandWalls +
                        " doors=" + realizedGateDoors + "/" + plannedGateDoors +
                        " reachable=" + reachableGates + "/" + layout.Gates.Count +
                        " clearService=" + clearServiceCells + "/" + serviceCells.Count +
                        " reachableService=" + reachableServiceTermini + "/" + serviceTermini.Count);
                P2Log.Message("PROCEDURAL_DEFENSE_AUDIT", "map=" + map.uniqueID + " outerWalls=" + realizedOuterWalls +
                    "/" + plannedOuterWalls + " returnCells=" + realizedReturnCells + "/" + plannedReturnCells +
                    " anchoredReturns=" + realizedAnchoredReturns + "/" + plannedAnchoredReturns +
                    " bandWalls=" + realizedBandWalls + "/" + plannedBandWalls +
                    " gateDoors=" + realizedGateDoors + "/" + plannedGateDoors +
                    " reachableGates=" + reachableGates + "/" + layout.Gates.Count +
                    " clearService=" + clearServiceCells + "/" + serviceCells.Count +
                    " reachableService=" + reachableServiceTermini + "/" + serviceTermini.Count);
                int outdoorExhausts = AuditCoolerExhaust(map, layout);
                tx.Commit();

                map.GetComponent<P3GarrisonController>().Initialize(settlement, layout.Gates.Select(g => g.Cell)
                    .Concat(fieldWall == null ? Enumerable.Empty<GridPoint>() :
                        new[] { fieldWall.InnerGate.Value, fieldWall.OuterGate }), encounter,
                    defenders, tx.OwnedThings, mortar, combatStocks);
                LordMaker.MakeNewLord(settlement.Faction, new LordJob_P3Garrison(settlement.ID), map, defenders);
                P0SettlementState state = Current.Game.GetComponent<P0WorldState>().GetOrCreate(settlement.ID, settlement.Faction);
                state.generationCount++;
                state.pawnIds = defenders.Select(p => p.thingIDNumber).ToList();
                state.proceduralLayoutDigest = layout.Digest;
                GridPoint marker = walls.First();
                Thing woodStock = tx.OwnedThings.FirstOrDefault(t => t.def == ThingDefOf.WoodLog);
                GridPoint stock = woodStock == null ? storageRoom.Interior.Points().First() :
                    new GridPoint(woodStock.Position.x, woodStock.Position.z);
                map.GetComponent<P0MapState>().Initialize(settlement, ToCell(layout.Anchor), ToCell(stock),
                    ToCell(marker), P0MapAudit.TerrainRoofChecksum(map));
                P2Log.Message("PROCEDURAL_SPAWN_COMPLETE", "map=" + map.uniqueID + " layout=" + layout.Digest +
                    " profile=" + run.ProfileId + " rooms=" + layout.Rooms.Count + " gates=" + layout.Gates.Count +
                    " requestedLayers=" + layout.WallLayers + " reinforcedSections=" + layout.OuterSections.Count +
                    " outerWallCells=" + layout.OuterWalls.Count() +
                    " maxReinforcedLayer=" + (layout.OuterSections.Count == 0 ? 0 : layout.OuterSections.Max(s => s.Layer)) +
                    " things=" + tx.OwnedCount + " residents=" + defenders.Count +
                    " crops=" + crops + " excavated=" + tx.ExcavatedCount + " retainedRoofs=" + layout.RetainedThickRoof.Count +
                    " plantsCleared=" + tx.ClearedPlantCount +
                    " conduits=" + conduitCells.Count +
                    " outdoorCoolerExhausts=" + outdoorExhausts +
                    " generatorStartupFuel=" + internalFuel + " storedFuel=" + fuel +
                    " encounter=" + encounter.Digest + " loadout=" + loadout.Digest);
                return true;
            }
            catch (Exception ex)
            {
                bool restored = false;
                try { restored = tx.Rollback(); } catch (Exception recovery) { P2Log.Error("PROCEDURAL_RECOVERY_ERROR", recovery.ToString()); }
                failure = ex.GetType().Name + ":" + ex.Message;
                P2Log.Error("PROCEDURAL_SPAWN_ABORT", "map=" + map.uniqueID + " error=" + failure +
                    " owned=" + tx.OwnedCount + " plantsRestored=" + tx.RestoredPlantCount +
                    "/" + tx.ClearedPlantCount + " recovered=" + restored);
                return false;
            }
        }

        private static IReadOnlyList<GridPoint> SelectFields(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            IEnumerable<GridPoint> reserved = layout.Enclosure.Concat(layout.BuiltWalls).Concat(layout.NaturalWalls)
                .Concat(layout.OuterWalls)
                .Concat(layout.ExhaustPocketWalls).Concat(layout.ExhaustPockets.Select(p => p.Cell))
                .Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(layout.Gates.SelectMany(g => g.BandDoors))
                .Concat(layout.Gates.SelectMany(g => g.OuterVestibule));
            return P4FieldPlacement.SelectReachable(input.Terrain, layout.Gates.Select(g => g.Staging),
                reserved, layout.FieldCellsRequired, layout.ConstructedRoof);
        }

        private static GridPoint FindOrigin(ThingDef def, Rot4 rotation, IReadOnlyList<GridPoint> footprint)
        {
            if (footprint.Count == 0) throw new InvalidOperationException("Empty footprint for " + def.defName);
            GridPoint first = footprint[0];
            for (int x = first.X - 3; x <= first.X + 3; x++)
                for (int z = first.Z - 3; z <= first.Z + 3; z++)
                {
                    CellRect rect = GenAdj.OccupiedRect(new IntVec3(x, 0, z), rotation, def.size);
                    if (rect.Area == footprint.Count && footprint.All(c => rect.Contains(ToCell(c))))
                        return new GridPoint(x, z);
                }
            throw new InvalidOperationException("No vanilla origin realizes " + def.defName + " " + rotation +
                " footprint at " + first);
        }

        private static IReadOnlyList<GridPoint> PlanConduits(Map map, ProceduralLayoutInput input, ProceduralLayout layout)
        {
            if (layout.Power == null) return new GridPoint[0];
            List<GridPoint> terminals = layout.AllFixtures.Where(f => f.Watts != 0 ||
                f.Kind == LayoutFixtureKind.Generator || f.Kind == LayoutFixtureKind.Battery)
                .Select(f => f.Cells[0]).Concat(layout.PowerYards.Select(y => y.Cells[0])).Distinct().ToList();
            if (terminals.Count == 0) throw new InvalidOperationException("Powered layout has no terminals");
            HashSet<GridPoint> intactRock = new HashSet<GridPoint>(layout.NaturalWalls.Concat(layout.Rooms.SelectMany(r => r.NaturalWalls)));
            HashSet<GridPoint> enclosed = new HashSet<GridPoint>(layout.Enclosure);
            Func<GridPoint, bool> legal = c => enclosed.Contains(c) && !intactRock.Contains(c) &&
                !input.Terrain[c].Has(TerrainTraits.Water) &&
                (!input.Terrain[c].Has(TerrainTraits.NaturalRock) || layout.Excavation.Contains(c)) &&
                GenConstruct.CanBuildOnTerrain(ThingDefOf.PowerConduit, ToCell(c), map, Rot4.North);
            HashSet<GridPoint> tree = new HashSet<GridPoint> { terminals[0] };
            GridPoint[] deltas = { new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1) };
            foreach (GridPoint terminal in terminals.Skip(1))
            {
                Queue<GridPoint> queue = new Queue<GridPoint>();
                Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
                queue.Enqueue(terminal);
                previous[terminal] = terminal;
                GridPoint reached = default(GridPoint);
                bool found = false;
                while (queue.Count > 0 && !found)
                {
                    GridPoint here = queue.Dequeue();
                    if (tree.Contains(here)) { reached = here; found = true; break; }
                    foreach (GridPoint d in deltas)
                    {
                        GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                        if (!legal(next) || previous.ContainsKey(next)) continue;
                        previous[next] = here;
                        queue.Enqueue(next);
                    }
                }
                if (!found) throw new InvalidOperationException("Power terminal disconnected at " + terminal);
                for (GridPoint c = reached; ; c = previous[c])
                {
                    tree.Add(c);
                    if (c.Equals(terminal)) break;
                }
            }
            return tree.OrderBy(c => c).ToList();
        }

        private static Thing Spawn(P4SpawnTransaction tx, ThingDef def, ThingDef stuff, GridPoint point,
            Map map, Faction faction, Rot4 rotation)
        {
            Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ?
                (stuff != null && GenStuff.AllowedStuffsFor(def).Contains(stuff) ? stuff : GenStuff.DefaultStuffFor(def)) : null);
            if (thing.def.CanHaveFaction) thing.SetFactionDirect(faction);
            thing = GenSpawn.Spawn(thing, ToCell(point), map, rotation, WipeMode.Vanish);
            tx.Own(thing);
            return thing;
        }

        private static List<Thing> SpawnStacks(P4SpawnTransaction tx, ThingDef def, int count,
            IEnumerable<GridPoint> cells, Map map, string stuff = null, LoadoutQuality? quality = null)
        {
            List<Thing> result = new List<Thing>();
            foreach (GridPoint point in cells)
            {
                if (count <= 0) break;
                IntVec3 cell = ToCell(point);
                if (!cell.Standable(map) || cell.GetEdifice(map) != null ||
                    cell.GetThingList(map).Any(t => t.def.category == ThingCategory.Item)) continue;
                Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? ResolveStuff(stuff) ?? GenStuff.DefaultStuffFor(def) : null);
                ApplyQuality(thing, quality);
                thing.stackCount = Math.Min(def.stackLimit, count);
                count -= thing.stackCount;
                result.Add(GenSpawn.Spawn(thing, cell, map));
                tx.Own(result[result.Count - 1]);
            }
            if (count != 0) throw new InvalidOperationException("Insufficient stock cells for " + def.defName + ":" + count);
            return result;
        }

        private static void Configure(Thing thing, LayoutFixtureKind kind, LayoutRoomKind? roomKind)
        {
            Building_Bed bed = thing as Building_Bed;
            if (bed != null)
            {
                if (kind == LayoutFixtureKind.PrisonBed) bed.ForPrisoners = true;
                if (kind == LayoutFixtureKind.MedicalBed) bed.Medical = true;
            }
            if (kind == LayoutFixtureKind.WallCooler)
            {
                if (!roomKind.HasValue) throw new InvalidOperationException("Cooler has no owning room");
                thing.TryGetComp<CompTempControl>().TargetTemperature = SettlementTemperatureTargets.ForRoom(roomKind.Value);
            }
            if (kind == LayoutFixtureKind.Stove && thing is IBillGiver giver)
            {
                RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimple");
                if (recipe != null) giver.BillStack.AddBill(new Bill_Production(recipe));
            }
        }

        private static void Equip(Pawn pawn, ResidentLoadout loadout)
        {
            if (loadout.Weapon != null)
            {
                ThingDef def = Require(loadout.Weapon.DefName);
                pawn.equipment.DestroyAllEquipment();
                ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(def,
                    def.MadeFromStuff ? ResolveStuff(loadout.Weapon.Stuff) ?? GenStuff.DefaultStuffFor(def) : null);
                ApplyQuality(weapon, loadout.Weapon.Quality);
                pawn.equipment.AddEquipment(weapon);
            }
            pawn.apparel.DestroyAll();
            foreach (LoadoutItem item in loadout.Apparel.Concat(loadout.Utility == null ?
                Enumerable.Empty<LoadoutItem>() : new[] { loadout.Utility }))
            {
                ThingDef def = Require(item.DefName);
                Apparel apparel = (Apparel)ThingMaker.MakeThing(def,
                    def.MadeFromStuff ? ResolveStuff(item.Stuff) ?? GenStuff.DefaultStuffFor(def) : null);
                ApplyQuality(apparel, item.Quality);
                if (apparel.PawnCanWear(pawn, true)) pawn.apparel.Wear(apparel, false);
                else apparel.Destroy();
            }
        }

        private static void ApplyQuality(Thing thing, LoadoutQuality? quality)
        {
            if (!quality.HasValue) return;
            CompQuality comp = thing.TryGetComp<CompQuality>();
            if (comp == null) throw new InvalidOperationException("Planned quality cannot be applied to " + thing.def.defName);
            comp.SetQuality((QualityCategory)(int)quality.Value, ArtGenerationContext.Outsider);
        }

        private static int AuditCoolerExhaust(Map map, ProceduralLayout layout)
        {
            int count = 0, freezerCount = 0, roomCount = 0;
            foreach (LayoutRoom room in layout.Rooms)
                foreach (LayoutFixture cooler in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.WallCooler))
                {
                    IntVec3 cold = ToCell(cooler.ColdCell.Value);
                    IntVec3 hot = ToCell(cooler.HotCell.Value);
                    Room served = cold.GetRoom(map);
                    Room exhaust = hot.GetRoom(map);
                    if (served == null || exhaust == null || served == exhaust ||
                        served.UsesOutdoorTemperature || !exhaust.UsesOutdoorTemperature)
                        throw new InvalidOperationException("Cooler lacks a sealed served room and outdoor exhaust: " +
                            room.Id + " cold=" + cold + " hot=" + hot);
                    CompTempControl control = ToCell(cooler.Cells[0]).GetEdifice(map)?.TryGetComp<CompTempControl>();
                    if (control == null || control.TargetTemperature != SettlementTemperatureTargets.ForRoom(room.Kind))
                        throw new InvalidOperationException("Cooler target temperature differs from room policy: " + room.Id);
                    if (room.Kind == LayoutRoomKind.Freezer) freezerCount++;
                    else roomCount++;
                    count++;
                }
            P2Log.Message("PROCEDURAL_COOLER_AUDIT", "map=" + map.uniqueID + " freezer=" + freezerCount +
                " targetC=" + SettlementTemperatureTargets.FreezerC + " rooms=" + roomCount +
                " targetC=" + SettlementTemperatureTargets.OccupiedRoomC);
            return count;
        }

        private static ThingDef Require(string name) => DefDatabase<ThingDef>.GetNamed(name, true);
        private static string Sector(GridPoint cell, GridPoint center)
        {
            int dx = cell.X - center.X, dz = cell.Z - center.Z;
            return Math.Abs(dx) > Math.Abs(dz) ? (dx < 0 ? "west" : "east") : (dz < 0 ? "south" : "north");
        }
        private static ThingDef ResolveStuff(string name) => string.IsNullOrEmpty(name) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(name);
        private static Rot4 Rotation(string name) => name == "East" ? Rot4.East : name == "South" ? Rot4.South :
            name == "West" ? Rot4.West : Rot4.North;
        private static IntVec3 ToCell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }
}
