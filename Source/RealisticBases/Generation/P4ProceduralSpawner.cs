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
            P4BApproachDefensePlan approachDefense = layout.ApproachDefense;
            P4BTurretPlan turretPlan = layout.Turret;
            List<P4BTurretPlan> turretPlans = turretPlan?.All.ToList() ?? new List<P4BTurretPlan>();
            if (layout.FieldCellsRequired > 0 && fieldWall == null)
            { failure = "missing_joint_field_wall"; return false; }
            IReadOnlyList<string> errors = ProceduralLayoutValidator.Validate(input, layout);
            if (errors.Count != 0) { failure = "invalid_layout:" + string.Join(",", errors); return false; }
            SettlementLoadout loadout = LayoutLoadoutModel.Build(input, layout);
            errors = ProceduralLayoutValidator.ValidateLoadout(input, layout, loadout);
            if (!loadout.Success || errors.Count != 0)
            { failure = "invalid_loadout:" + loadout.Rejection + ":" + string.Join(",", errors); return false; }
            Dictionary<GridPoint, HashSet<string>> sniperGuardSectors = new Dictionary<GridPoint, HashSet<string>>();
            if (approachDefense != null && turretPlans.Count > 0)
            {
                HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
                opaque.UnionWith(turretPlans.SelectMany(t => t.Footprint));
                foreach (P4BTurretPlan gun in turretPlans)
                {
                    P4BTurretPlanner.SniperGuardCoverage(approachDefense, gun, opaque,
                        out int threats, out int answered, out GridPoint? guard);
                    if (!guard.HasValue || threats == 0 || answered * 10 < threats * 9)
                    { failure = "unanswered_turret_sniper_threat:" + gun.Origin; return false; }
                    if (!sniperGuardSectors.TryGetValue(guard.Value, out HashSet<string> sectors))
                        sniperGuardSectors.Add(guard.Value, sectors = new HashSet<string>(StringComparer.Ordinal));
                    sectors.Add(gun.Side);
                    P2Log.Message("PROCEDURAL_TURRET_GUARD_PLAN", "map=" + map.uniqueID +
                        " gun=" + gun.DefName + "@" + gun.Origin + " side=" + gun.Side +
                        " sniperPost=" + guard.Value + " answered=" + answered + "/" + threats);
                }
            }

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
                .Concat(layout.Gates.SelectMany(g => g.BandDoors)).Concat(layout.Rooms.Select(r => r.Door))
                .Concat(approachDefense?.SallyPorts.Select(p => p.Cell) ?? new GridPoint[0]));
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
                .Concat(fieldWall == null ? Enumerable.Empty<GridPoint>() : fieldWall.CoverCells)
                .Concat(approachDefense?.CoverCells ?? new GridPoint[0])
                .Concat(turretPlans.SelectMany(t => t.Footprint.Concat(t.CoverCells))));
            if (layout.Court != null) structures.UnionWith(layout.Court.Cells);
            List<GridPoint> layerServiceCells = layout.OuterSections
                .SelectMany(section => section.ServiceLane(layout.Gates[section.GateIndex])).Distinct().ToList();
            List<GridPoint> serviceCells = new List<GridPoint>(layerServiceCells);
            serviceCells.AddRange(turretPlans.SelectMany(t => t.ServiceRoute));
            serviceCells.AddRange(approachDefense?.Posts.SelectMany(p => p.Route) ?? new GridPoint[0]);

            Dictionary<LayoutFixture, GridPoint> fixtureOrigins = new Dictionary<LayoutFixture, GridPoint>();
            Dictionary<LayoutPowerYard, GridPoint> yardOrigins = new Dictionary<LayoutPowerYard, GridPoint>();
            Dictionary<P4BTurretPlan, GridPoint> turretOrigins = new Dictionary<P4BTurretPlan, GridPoint>();
            try
            {
                foreach (LayoutFixture fixture in layout.AllFixtures.Where(f =>
                    f.Kind != LayoutFixtureKind.Stockpile && f.Kind != LayoutFixtureKind.MortarCourt))
                    fixtureOrigins[fixture] = FindOrigin(Require(fixture.DefName), Rotation(fixture.Rotation), fixture.Cells);
                foreach (LayoutPowerYard yard in layout.PowerYards)
                    yardOrigins[yard] = FindOrigin(Require(yard.DefName), Rotation(yard.Rotation), yard.Cells);
                foreach (P4BTurretPlan plan in turretPlans)
                {
                    ThingDef def = Require(plan.DefName);
                    GridPoint origin = FindOrigin(def, Rot4.North, plan.Footprint);
                    turretOrigins[plan] = origin;
                    if (!GenConstruct.CanBuildOnTerrain(def, ToCell(origin), map, Rot4.North))
                        throw new InvalidOperationException("Turret terrain rejected at " + origin);
                }
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
                    " exteriorPosts=" + fieldWall.ExteriorPosts.Count + " usablePosts=" + fieldWall.UsablePosts.Count());
            if (fieldWall != null)
                foreach (P4BPostAccessPlan post in fieldWall.PostAccess)
                    P2Log.Message("PROCEDURAL_FIELD_POST_ACCESS_PLAN", "map=" + map.uniqueID +
                        " post=" + post.Post + " usable=" + post.Usable + " return=" + post.WallReturn +
                        " routeCells=" + post.Route.Count + " screens=" + post.Screens.Count +
                        " exposedCrossingCells=" + post.ExposedCells.Count + " omission=" + post.OmissionReason);
            if (turretPlan != null)
                P2Log.Message("PROCEDURAL_DEFENSE_NETWORK_PLAN", "map=" + map.uniqueID +
                    " groups=" + turretPlan.Groups.Count() + " turrets=" + turretPlans.Count +
                    " watts=" + turretPlan.TotalWatts + " import=" + turretPlan.ImportValue);
            foreach (P4BTurretPlan plan in turretPlans)
                P2Log.Message("PROCEDURAL_TURRET_PLAN", "map=" + map.uniqueID +
                    " digest=" + plan.Digest + " def=" + plan.DefName +
                    " origin=" + turretOrigins[plan] + " watts=" + plan.Watts +
                    " loaded=" + plan.FuelDefName + ":" + plan.LoadedFuelUnits +
                    " reserve=" + plan.ReserveFuelUnits + " cover=" + plan.CoverCells.Count +
                    " import=" + (plan.ImportValue - (plan.Support?.ImportValue ?? 0) -
                        plan.AdditionalGroups.Sum(g => g.ImportValue)) + " side=" + plan.Side + " accessGate=" + plan.AccessGate);
            if (approachDefense != null)
            {
                HashSet<GridPoint> attackOpaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
                attackOpaque.UnionWith(turretPlans.SelectMany(t => t.Footprint));
                foreach (P4BAttackSide side in approachDefense.Sides)
                {
                    int covered = P4BApproachDefensePlanner.CoveredSamples(side, attackOpaque);
                    P2Log.Message("PROCEDURAL_ATTACK_SIDE_PLAN", "map=" + map.uniqueID + " side=" + side.Id +
                        " exposed=" + side.Exposed + " approachSamples=" + side.Samples.Count +
                        " openGroundArea=" + side.OpenGroundArea +
                        " post=" + (side.Post?.ToString() ?? "none") + " positions=" + string.Join("|", side.Positions) +
                        " postCoveredSamples=" + covered + " coverage=" + (!side.Exposed ? "not_exposed" :
                            covered >= P4BApproachDefensePlanner.UsefulArc(side.Samples.Count) ? "useful" : "gap") +
                        " omission=" + side.OmissionReason);
                }
            }
            foreach (P4BSallyPort port in approachDefense?.SallyPorts ?? Enumerable.Empty<P4BSallyPort>())
                P2Log.Message("PROCEDURAL_SALLY_PORT_PLAN", "map=" + map.uniqueID + " gate=" + port.Cell +
                    " inside=" + port.Inside + " outward=" + port.Outward + " field=" + port.Field +
                    " interiorRouteCells=" + port.InteriorRoute.Count + " reinforced=false");
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
                EncounterProfile profile = layout.Court != null ? P3EncounterProfiles.Industrial :
                    input.Requirements.Capabilities.HasFlag(SettlementCapability.Electricity)
                        ? P3EncounterProfiles.IndustrialNoMortar : P3EncounterProfiles.Tribal;
                encounter = GeometryEncounterPlanner.Plan(new GeometryEncounterInput(layout.Digest, profile,
                    layout.Enclosure, walls.Concat(natural), layout.Gates.Select(g =>
                        new KeyValuePair<GridPoint, GridPoint>(g.Cell, g.Inside))
                        .Concat(approachDefense?.SallyPorts.Where(p => !p.Field).Select(p => new KeyValuePair<GridPoint, GridPoint>(p.Cell, p.Inside)) ??
                            Enumerable.Empty<KeyValuePair<GridPoint, GridPoint>>()), publicCells,
                    LayoutStockCells.ForRooms(layout.Rooms, LayoutRoomKind.Storage),
                    layout.Court?.Cells.FirstOrDefault(), layout.Court?.Access.FirstOrDefault()));
                if (fieldWall != null)
                {
                    // The compound reservation also covers the short exterior
                    // crossing to the field. Vanilla pathfinding may leave the
                    // literal wall interior through a different compound gate.
                    List<GridPoint> defended = layout.Enclosure.Concat(layout.JointSite.Compound.Points())
                        .Concat(layout.JointSite.Field.Points())
                        .Concat(fieldWall.Access)
                        // Low cover is passable; vanilla paths can step over a
                        // screen. This permits transit, not a protected post.
                        .Concat(fieldWall.CoverCells)
                        .Concat(approachDefense?.CoverCells ?? new GridPoint[0])
                        .Concat(approachDefense?.TransitCells ?? new GridPoint[0])
                        .Concat(approachDefense?.Posts.SelectMany(p => p.Route) ?? new GridPoint[0])
                        .Concat(approachDefense?.AllPosts ?? new GridPoint[0])
                        .Concat(fieldWall.ArrivalRoute.TakeWhile(c => !c.Equals(fieldWall.OuterGate)))
                        .Concat(fieldWall.ExteriorPosts).Concat(new[] { fieldWall.OuterGate })
                        .Concat(turretPlans.SelectMany(t => t.ServiceRoute))
                        .Distinct().ToList();
                    GridRect bounds = new GridRect(defended.Min(c => c.X), defended.Min(c => c.Z),
                        defended.Max(c => c.X) - defended.Min(c => c.X) + 1,
                        defended.Max(c => c.Z) - defended.Min(c => c.Z) + 1);
                    // Once verified exterior positions exist, opaque-wall
                    // legacy ranged posts must not compete for their staff.
                    // Retain mortar, close gate reserves and inner fallback.
                    encounter = new EncounterPlan(layout.Digest, profile, encounter.Posts
                        .Where(p => approachDefense == null || p.Inner || p.Mortar || p.MaximumRange <= 4).Concat(
                        fieldWall.UsablePosts.Select((c, i) => new DefensivePost("field-outer-" + i,
                            Sector(c, encounter.SupportCenter), c, sniperGuardSectors.ContainsKey(c) ? 35 : 8,
                            sniperGuardSectors.ContainsKey(c) ? 44 : 38, true, true, true, false, false, true,
                            approachDefense?.Sides.Where(s => s.Positions.Contains(c)).Select(s => s.Id)
                                .Concat(sniperGuardSectors.TryGetValue(c, out HashSet<string> fieldSectors)
                                    ? fieldSectors : Enumerable.Empty<string>()), fieldWall.OuterGate)))
                        .Concat(approachDefense?.Posts.Select(p => new DefensivePost("perimeter-" + p.Side,
                            p.Side, p.Cell, sniperGuardSectors.ContainsKey(p.Cell) ? 35 : 8,
                            sniperGuardSectors.ContainsKey(p.Cell) ? 44 : 25, true, true, true, false, false, true,
                            approachDefense.Sides.Where(s => s.Positions.Contains(p.Cell)).Select(s => s.Id)
                                .Concat(sniperGuardSectors.TryGetValue(p.Cell, out HashSet<string> perimeterSectors)
                                    ? perimeterSectors : Enumerable.Empty<string>()), p.AccessGate))
                            ?? Enumerable.Empty<DefensivePost>()),
                        bounds, encounter.SupportCenter, encounter.InnerFallback,
                        encounter.MortarCell, encounter.MortarInteractionCell, encounter.MagazineCell,
                        encounter.Supplies, defended);
                }
                // Audit the compound and field approaches separately. Field
                // posts are intentionally too far from the compound gates to
                // cover those samples; counting only compound approaches made
                // an effective field defense appear to have no shot at all.
                List<GridPoint> compoundApproaches = layout.Gates.SelectMany(g =>
                    g.ExteriorRoute.Take(12)).Distinct().ToList();
                List<GridPoint> fieldApproaches = fieldWall == null
                    ? new List<GridPoint>()
                    : fieldWall.ArrivalRoute.SkipWhile(c => !c.Equals(fieldWall.OuterGate))
                        .Skip(1).Take(20).Distinct().ToList();
                HashSet<GridPoint> opaque = new HashSet<GridPoint>(walls.Concat(natural).Concat(doors));
                List<DefensivePost> directPosts = encounter.Posts.Where(p => !p.Mortar).ToList();
                List<DefensivePost> fieldPosts = directPosts.Where(p =>
                    p.Id.StartsWith("field-outer-", StringComparison.Ordinal)).ToList();
                List<DefensivePost> compoundPosts = directPosts.Except(fieldPosts).ToList();
                int usefulCompoundPosts = compoundPosts.Count(p => compoundApproaches.Any(c =>
                    P4BFireLane.CanEngage(p.Cell, c, p.MinimumRange, p.MaximumRange, opaque)));
                int coveredCompoundApproaches = compoundApproaches.Count(c => compoundPosts.Any(p =>
                    P4BFireLane.CanEngage(p.Cell, c, p.MinimumRange, p.MaximumRange, opaque)));
                int usefulFieldPosts = fieldPosts.Count(p => fieldApproaches.Any(c =>
                    P4BFireLane.CanEngage(p.Cell, c, p.MinimumRange, p.MaximumRange, opaque)));
                int coveredFieldApproaches = fieldApproaches.Count(c => fieldPosts.Any(p =>
                    P4BFireLane.CanEngage(p.Cell, c, p.MinimumRange, p.MaximumRange, opaque)));
                P2Log.Message("PROCEDURAL_FIRE_LANE_PLAN", "map=" + map.uniqueID + " closedGate=true" +
                    " compoundPostsWithShot=" + usefulCompoundPosts + "/" + compoundPosts.Count +
                    " compoundApproachesCovered=" + coveredCompoundApproaches + "/" + compoundApproaches.Count +
                    " fieldPostsWithShot=" + usefulFieldPosts + "/" + fieldPosts.Count +
                    " fieldApproachesCovered=" + coveredFieldApproaches + "/" + fieldApproaches.Count);
            }
            catch (Exception ex) { failure = "geometry_encounter_rejected:" + ex.Message; return false; }
            IReadOnlyList<string> fieldFallbackPosts = fieldWall == null ? new string[0] :
                P4BFieldFallback.Select(input, layout, encounter);
            if (fieldWall != null)
                P2Log.Message("PROCEDURAL_FIELD_FALLBACK_PLAN", "map=" + map.uniqueID +
                    " fieldGate=" + fieldWall.InnerGate + " compoundGate=" +
                    layout.Gates[fieldWall.GateIndex].Cell + " usablePosts=" +
                    string.Join(",", fieldFallbackPosts.ToArray()) +
                    " omission=" + (fieldFallbackPosts.Count == 0 ? "no_compound_gate_firing_lane" : "none"));

            P4SpawnTransaction tx;
            try { tx = new P4SpawnTransaction(map, footprint); }
            catch (Exception ex) { failure = "mutation_preflight_rejected:" + ex.Message; return false; }
            List<Pawn> defenders = new List<Pawn>();
            List<Thing> combatStocks = new List<Thing>();
            Thing mortar = null;
            Dictionary<P4BTurretPlan, Thing> turrets = new Dictionary<P4BTurretPlan, Thing>();
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
                    foreach (GridPoint c in fieldWall.CoverCells.Concat(approachDefense?.CoverCells ?? new GridPoint[0])
                        .Concat(turretPlans.SelectMany(t => t.CoverCells)).Distinct())
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
                foreach (P4BTurretPlan plan in turretPlans)
                {
                    Thing turret = Spawn(tx, Require(plan.DefName), ThingDefOf.Steel, turretOrigins[plan],
                        map, settlement.Faction, Rot4.North);
                    turrets[plan] = turret;
                    if (!plan.Footprint.All(c => turret.OccupiedRect().Contains(ToCell(c))) ||
                        turret.OccupiedRect().Area != plan.Footprint.Count)
                        throw new InvalidOperationException("Turret footprint differs from plan: " + plan.DefName);
                }
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
                IReadOnlyList<GridPoint> storageCells = LayoutStockCells.ForRooms(layout.Rooms, LayoutRoomKind.Storage);
                IReadOnlyList<GridPoint> foodCells = LayoutStockCells.ForRooms(layout.Rooms, LayoutRoomKind.Freezer, LayoutRoomKind.Pantry);
                IReadOnlyList<GridPoint> medicalCells = LayoutStockCells.ForRooms(layout.Rooms, LayoutRoomKind.Medical);
                SpawnStacks(tx, Require(food.Kind == LayoutRoomKind.Freezer ? "MealSimple" : "Pemmican"),
                    input.Requirements.Residents * 12, foodCells, map);
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
                if (fuel > 0) SpawnStacks(tx, Require(layout.Power.FuelDef), fuel, storageCells, map);
                foreach (LoadoutItem item in loadout.Medicine) SpawnStacks(tx, Require(item.DefName), item.Count, medicalCells, map);
                foreach (LoadoutItem item in loadout.Components.Concat(loadout.Armoury))
                    SpawnStacks(tx, Require(item.DefName), item.Count, storageCells, map, item.Stuff, item.Quality);
                foreach (CombatSupply supply in encounter.Supplies)
                {
                    if (supply.DefName == "ReinforcedBarrel" && Find.Storyteller.difficulty.classicMortars) continue;
                    combatStocks.AddRange(SpawnStacks(tx, Require(supply.DefName), supply.Count, storageCells, map));
                }
                foreach (IGrouping<string, P4BTurretPlan> group in turretPlans.GroupBy(t => t.FuelDefName))
                    combatStocks.AddRange(SpawnStacks(tx, Require(group.Key), group.Sum(t => t.ReserveFuelUnits),
                        storageCells, map));
                Thing[] shelfStocks = tx.OwnedThings.Where(t => t.def.category == ThingCategory.Item &&
                    t.Position.GetEdifice(map) is Building_Storage).ToArray();
                P2Log.Message("PROCEDURAL_STOCK_PLACEMENT", "map=" + map.uniqueID +
                    " storageRooms=" + layout.Rooms.Count(r => r.Kind == LayoutRoomKind.Storage) +
                    " storageCells=" + storageCells.Count +
                    " shelves=" + tx.OwnedThings.Count(t => t is Building_Storage) + " shelfStacks=" + shelfStocks.Length +
                    " shelfUnits=" + shelfStocks.Sum(t => t.stackCount));

                // MapGenerator suppresses incremental path-cost updates. Flush
                // final geometry before synchronous pawn/service audits, then
                // restore its batching mode on success or transactional failure.
                bool pathUpdatesBatched = map.pathing.IncrementalDirtyingDisabled;
                Dictionary<IntVec3, bool> previousWalkability = layout.Enclosure.Concat(structures).Concat(fields)
                    .Concat(serviceCells).Concat(fieldWall?.Access ?? new GridPoint[0]).Distinct().Select(ToCell)
                    .ToDictionary(c => c, c => map.pathing.Normal.pathGrid.Walkable(c));
                using var pathAudit = new P4BFinalPathAuditScope(pathUpdatesBatched,
                    () => map.pathing.ReEnableIncrementalDirtying(),
                    () => map.pathing.DisableIncrementalDirtying());
                // Newer 1.6 runtimes cache gathered data by tick; the bundled
                // 4871 reference lacks this invalidation hook. Use it when present.
                HarmonyLib.AccessTools.Method(map.pathFinder.MapData.GetType(), "Notify_MapDirtied")
                    ?.Invoke(map.pathFinder.MapData, null);
                map.reachability.ClearCache();
                P2Log.Message("PROCEDURAL_PATH_GRID_SYNC", "map=" + map.uniqueID + " finalGeometry=true" +
                    " batchingWasDisabled=" + pathUpdatesBatched + " changedWalkability=" + previousWalkability.Count(pair =>
                        map.pathing.Normal.pathGrid.Walkable(pair.Key) != pair.Value));
                map.powerNetManager.UpdatePowerNetsAndConnections_First();
                foreach (P4BTurretPlan plan in turretPlans)
                {
                    Thing turret = turrets[plan];
                    CompRefuelable magazine = turret.TryGetComp<CompRefuelable>();
                    CompPowerTrader power = turret.TryGetComp<CompPowerTrader>();
                    if (magazine == null || power?.PowerNet == null ||
                        Math.Abs(magazine.Fuel - magazine.Props.fuelCapacity * magazine.Props.initialFuelPercent) > 0.01f ||
                        !map.reachability.CanReach(ToCell(plan.AccessGate ?? fieldWall.OuterGate), ToCell(plan.ServiceCell),
                            PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)) ||
                        plan.CoverCells.Any(c => !ToCell(c).GetThingList(map)
                            .Any(t => t.def.defName == fieldWall.CoverDefName)))
                        throw new InvalidOperationException("Turret fuel, net or service realization differs from plan");
                    P2Log.Message("PROCEDURAL_TURRET_AUDIT", "map=" + map.uniqueID +
                        " def=" + turret.def.defName + " powerNet=" + power.PowerNet.GetHashCode() +
                        " fuel=" + magazine.Fuel + "/" + magazine.Props.fuelCapacity +
                        " reserve=" + plan.FuelDefName + ":" + plan.ReserveFuelUnits +
                        " cover=" + plan.CoverCells.Count + " serviceReachable=true");
                }
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
                    int longestPostPath = 0;
                    int viaGatePaths = 0;
                    foreach (GridPoint post in fieldWall.UsablePosts.Concat(approachDefense?.AllPosts ?? new GridPoint[0]).Distinct())
                    {
                        GridPoint gate = approachDefense?.Posts.FirstOrDefault(p => p.Cell.Equals(post))?.AccessGate ??
                            fieldWall.PostAccess.FirstOrDefault(p => p.Post.Equals(post))?.Route.FirstOrDefault() ??
                            fieldWall.OuterGate;
                        int eligibleDefenders = 0;
                        List<string> routeDiagnostics = new List<string>();
                        foreach (Pawn pawn in defenders)
                            using (PawnPath path = map.pathFinder.FindPathNow(pawn.Position, ToCell(post),
                                TraverseParms.For(pawn), null, PathEndMode.OnCell))
                            {
                                IntVec3 firstOutside = path.Found
                                    ? path.NodesReversed.FirstOrDefault(c => !defendedCells.Contains(c)) : IntVec3.Invalid;
                                bool legal = path.Found && path.NodesReversed.Count <= 160 &&
                                    path.NodesReversed.All(defendedCells.Contains);
                                int length = path.NodesReversed.Count;
                                if (!legal && defendedCells.Contains(ToCell(gate)))
                                {
                                    // Vanilla's shortest direct path can cut through open ground
                                    // outside the declared corridor. The live controller will use
                                    // this same access gate as a waypoint instead of broadening
                                    // its movement authority to include that shortcut.
                                    using (PawnPath toGate = map.pathFinder.FindPathNow(pawn.Position, ToCell(gate),
                                        TraverseParms.For(pawn), null, PathEndMode.OnCell))
                                    using (PawnPath fromGate = map.pathFinder.FindPathNow(ToCell(gate), ToCell(post),
                                        TraverseParms.For(pawn), null, PathEndMode.OnCell))
                                    {
                                        length = toGate.NodesReversed.Count + fromGate.NodesReversed.Count - 1;
                                        legal = toGate.Found && fromGate.Found && length <= 160 &&
                                            toGate.NodesReversed.All(defendedCells.Contains) &&
                                            fromGate.NodesReversed.All(defendedCells.Contains);
                                        if (legal) viaGatePaths++;
                                    }
                                }
                                if (legal)
                                {
                                    eligibleDefenders++;
                                    longestPostPath = Math.Max(longestPostPath, length);
                                }
                                else routeDiagnostics.Add(pawn.LabelShort + ":found=" + path.Found +
                                    "/nodes=" + path.NodesReversed.Count + "/firstOutside=" + firstOutside +
                                    "/via=" + gate);
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
                        " posts=" + fieldWall.UsablePosts.Concat(approachDefense?.AllPosts ?? new GridPoint[0]).Distinct().Count() +
                        " eachReachableByAtLeast=2" +
                        " omittedPosts=" + fieldWall.PostAccess.Count(p => !p.Usable) +
                        " exposedGateCrossingCells=2" +
                        " longestLegalPathNodes=" + longestPostPath +
                        " viaGatePaths=" + viaGatePaths +
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
                    int usablePostCount = fieldWall.UsablePosts.Count();
                    int reachablePosts = fieldWall.UsablePosts.Count(c => map.reachability.CanReach(
                        ToCell(layout.Gates[fieldWall.GateIndex].Inside), ToCell(c), PathEndMode.OnCell,
                        TraverseParms.For(TraverseMode.PassDoors)));
                    if (builtFieldWalls != fieldWall.Walls.Count || builtFieldGates != 2 ||
                        builtCover != fieldWall.CoverCells.Count || reachablePosts != usablePostCount)
                        throw new InvalidOperationException("Field enclosure realization failed: walls=" + builtFieldWalls +
                            "/" + fieldWall.Walls.Count + " gates=" + builtFieldGates + "/2 cover=" + builtCover +
                            "/" + fieldWall.CoverCells.Count + " reachablePosts=" + reachablePosts +
                            "/" + usablePostCount);
                    P2Log.Message("PROCEDURAL_FIELD_WALL_AUDIT", "map=" + map.uniqueID + " walls=" + builtFieldWalls +
                        "/" + fieldWall.Walls.Count + " gates=" + builtFieldGates + "/2 cover=" + builtCover +
                        "/" + fieldWall.CoverCells.Count + " reachablePosts=" + reachablePosts +
                        "/" + usablePostCount);
                }
                if (approachDefense != null)
                {
                    foreach (P4BPerimeterPost post in approachDefense.Posts)
                    {
                        if (!ToCell(post.Cell).Standable(map) ||
                            ToCell(post.WallReturn).GetEdifice(map)?.def != ThingDefOf.Wall ||
                            post.CoverCells.Any(c => !ToCell(c).GetThingList(map).Any(t => t.def.defName == fieldWall.CoverDefName)) ||
                            !map.reachability.CanReach(ToCell(post.AccessGate), ToCell(post.Cell), PathEndMode.OnCell,
                                TraverseParms.For(TraverseMode.PassDoors)))
                            throw new InvalidOperationException("Perimeter post realization failed: " + post.Side);
                        P2Log.Message("PROCEDURAL_PERIMETER_POST_AUDIT", "map=" + map.uniqueID + " side=" + post.Side +
                            " post=" + post.Cell + " gate=" + post.AccessGate + " return=" + post.WallReturn +
                            " cover=" + post.CoverCells.Count + " routeCells=" + post.Route.Count +
                            " reachable=true exposedCrossingCells=2 routeFullyCovered=false");
                    }
                    foreach (P4BSallyPort port in approachDefense.SallyPorts)
                        if (!(ToCell(port.Cell).GetEdifice(map) is Building_Door) || !map.reachability.CanReach(
                            ToCell(port.Inside), ToCell(port.Cell), PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)))
                            throw new InvalidOperationException("Defense sally port realization failed: " + port.Cell);
                }
                P4BRealizedCoverAudit.Run(map, approachDefense, turrets);
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
                int clearServiceCells = layerServiceCells.Count(c => ToCell(c).Standable(map) && ToCell(c).GetEdifice(map) == null);
                int reachableServiceTermini = serviceTermini.Count(pair => map.reachability.CanReach(
                    ToCell(pair.Key), ToCell(pair.Value), PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)));
                if (realizedOuterWalls != plannedOuterWalls || realizedReturnCells != plannedReturnCells ||
                    realizedAnchoredReturns != plannedAnchoredReturns ||
                    realizedBandWalls != plannedBandWalls ||
                    realizedGateDoors != plannedGateDoors || reachableGates != layout.Gates.Count ||
                    clearServiceCells != layerServiceCells.Count || reachableServiceTermini != serviceTermini.Count)
                    throw new InvalidOperationException("Realized layered-wall or gate audit failed: outer=" + realizedOuterWalls +
                        "/" + plannedOuterWalls + " returnCells=" + realizedReturnCells + "/" + plannedReturnCells +
                        " anchoredReturns=" + realizedAnchoredReturns + "/" + plannedAnchoredReturns +
                        " bands=" + realizedBandWalls + "/" + plannedBandWalls +
                        " doors=" + realizedGateDoors + "/" + plannedGateDoors +
                        " reachable=" + reachableGates + "/" + layout.Gates.Count +
                        " clearService=" + clearServiceCells + "/" + layerServiceCells.Count +
                        " reachableService=" + reachableServiceTermini + "/" + serviceTermini.Count);
                P2Log.Message("PROCEDURAL_DEFENSE_AUDIT", "map=" + map.uniqueID + " outerWalls=" + realizedOuterWalls +
                    "/" + plannedOuterWalls + " returnCells=" + realizedReturnCells + "/" + plannedReturnCells +
                    " anchoredReturns=" + realizedAnchoredReturns + "/" + plannedAnchoredReturns +
                    " bandWalls=" + realizedBandWalls + "/" + plannedBandWalls +
                    " gateDoors=" + realizedGateDoors + "/" + plannedGateDoors +
                    " reachableGates=" + reachableGates + "/" + layout.Gates.Count +
                    " clearService=" + clearServiceCells + "/" + layerServiceCells.Count +
                    " reachableService=" + reachableServiceTermini + "/" + serviceTermini.Count);
                int outdoorExhausts = AuditCoolerExhaust(map, layout);
                tx.Commit();

                map.GetComponent<P3GarrisonController>().Initialize(settlement, layout.Gates.Select(g => g.Cell)
                    .Concat(approachDefense?.SallyPorts.Select(p => p.Cell) ?? new GridPoint[0])
                    .Concat(fieldWall == null ? Enumerable.Empty<GridPoint>() :
                        new[] { fieldWall.InnerGate.Value, fieldWall.OuterGate }), encounter,
                    defenders, tx.OwnedThings, mortar, combatStocks, PerimeterSegments(layout, encounter.SupportCenter),
                    layout.JointSite.Compound.Points().Concat(layout.JointSite.Field.Points()),
                    fieldWall == null ? Enumerable.Empty<GridPoint>() : layout.JointSite.Field.Points()
                        .Except(fieldWall.Walls).Except(new[] { fieldWall.InnerGate.Value, fieldWall.OuterGate }),
                    layout.Enclosure, fieldFallbackPosts);
                LordMaker.MakeNewLord(settlement.Faction, new LordJob_P3Garrison(settlement.ID), map, defenders);
                P0SettlementState state = Current.Game.GetComponent<P0WorldState>().GetOrCreate(settlement.ID, settlement.Faction);
                state.generationCount++;
                state.pawnIds = defenders.Select(p => p.thingIDNumber).ToList();
                state.proceduralLayoutDigest = layout.Digest;
                GridPoint marker = walls.First();
                Thing woodStock = tx.OwnedThings.FirstOrDefault(t => t.def == ThingDefOf.WoodLog);
                GridPoint stock = woodStock == null ? storageCells.First() :
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
                    " turret=" + (turretPlan == null ? "none" : turretPlan.DefName) +
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
                .Select(f => f.Cells[0]).Concat(layout.PowerYards.Select(y => y.Cells[0]))
                .Concat(layout.Turret == null ? Enumerable.Empty<GridPoint>() :
                    layout.Turret.All.SelectMany(t => t.Footprint.Take(1)))
                .Distinct().ToList();
            if (terminals.Count == 0) throw new InvalidOperationException("Powered layout has no terminals");
            HashSet<GridPoint> intactRock = new HashSet<GridPoint>(layout.NaturalWalls.Concat(layout.Rooms.SelectMany(r => r.NaturalWalls)));
            HashSet<GridPoint> enclosed = P4BTurretPlanner.PowerAccessCells(layout);
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
                Building edifice = cell.GetEdifice(map);
                Building_Storage shelf = edifice as Building_Storage;
                // Shelves are PassThroughOnly, not Standable. They are valid
                // item surfaces, but must never admit a stack their filters reject.
                if (shelf == null ? !cell.Standable(map) || edifice != null :
                    !cell.Walkable(map) || !shelf.GetStoreSettings().AllowedToAccept(def)) continue;
                int occupied = cell.GetThingList(map).Count(t => t.def.category == ThingCategory.Item);
                int slots = shelf == null ? 1 : cell.GetMaxItemsAllowedInCell(map);
                while (count > 0 && occupied++ < slots)
                {
                    Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? ResolveStuff(stuff) ?? GenStuff.DefaultStuffFor(def) : null);
                    ApplyQuality(thing, quality);
                    thing.stackCount = Math.Min(def.stackLimit, count);
                    count -= thing.stackCount;
                    result.Add(GenSpawn.Spawn(thing, cell, map));
                    tx.Own(result[result.Count - 1]);
                }
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
        private static IEnumerable<P3PerimeterSegment> PerimeterSegments(ProceduralLayout layout, GridPoint center)
        {
            HashSet<GridPoint> field = new HashSet<GridPoint>(layout.FieldWall?.Walls ?? new GridPoint[0]);
            HashSet<GridPoint> gates = new HashSet<GridPoint>(layout.Gates.SelectMany(g =>
                new[] { g.Cell }.Concat(g.BandDoors)));
            gates.UnionWith(layout.ApproachDefense?.SallyPorts.Select(p => p.Cell) ?? new GridPoint[0]);
            if (layout.FieldWall != null)
            {
                field.Add(layout.FieldWall.InnerGate.Value);
                field.Add(layout.FieldWall.OuterGate);
                gates.Add(layout.FieldWall.InnerGate.Value);
                gates.Add(layout.FieldWall.OuterGate);
            }
            HashSet<GridPoint> cells = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.OuterWalls)
                .Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(field).Concat(gates));
            return cells.OrderBy(c => c).Select(c => new P3PerimeterSegment(c,
                layout.ApproachDefense?.Sides.FirstOrDefault(s => s.Faces.Contains(c))?.Id ?? Sector(c, center),
                field.Contains(c) ? P3PerimeterRegion.Field : P3PerimeterRegion.Compound,
                gates.Contains(c)));
        }
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
