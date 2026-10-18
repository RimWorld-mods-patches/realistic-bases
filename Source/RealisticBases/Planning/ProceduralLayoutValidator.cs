using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace RealisticBases.Planning
{
    // Planner-internal acceptance check over the final planned occupancy. It
    // applies the P4 circulation semantics (one public root, cardinal floods,
    // rooms never public transit, room-local job access) to the P4A irregular
    // model. It does not replace PlannedNavigationGrid: after P4 closes, that grid
    // is generalized and stays the single acceptance authority (see docs).
    public static class ProceduralLayoutValidator
    {
        public static IReadOnlyList<string> Validate(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            List<string> errors = new List<string>();
            if (input == null || layout == null) return new[] { "missing_input_or_layout" };
            TerrainSnapshot terrain = input.Terrain;
            LayoutRequirements requirements = input.Requirements;
            LayoutTerrainGrid grid = LayoutTerrainGrid.Build(terrain, 0, input.Resources.GeyserFootprints());
            if (layout.InputDigest != input.Digest) errors.Add("input_digest");

            HashSet<GridPoint> enclosure = new HashSet<GridPoint>(layout.Enclosure);
            HashSet<GridPoint> built = new HashSet<GridPoint>(layout.BuiltWalls);
            HashSet<GridPoint> outer = new HashSet<GridPoint>(layout.OuterWalls);
            HashSet<GridPoint> natural = new HashSet<GridPoint>(layout.NaturalWalls);
            HashSet<GridPoint> gateCells = new HashSet<GridPoint>(layout.Gates.Select(g => g.Cell)
                .Concat(layout.ApproachDefense?.SallyPorts.SelectMany(p => p.Doors) ?? new GridPoint[0]));
            HashSet<GridPoint> excavation = new HashSet<GridPoint>(layout.Excavation);
            HashSet<GridPoint> retained = new HashSet<GridPoint>(layout.RetainedThickRoof);
            HashSet<GridPoint> extra = new HashSet<GridPoint>(layout.ExtraObstacles);

            if (layout.JointSite != null)
            {
                P4BJointSite site = layout.JointSite;
                if (site.Crops.Count < layout.FieldCellsRequired ||
                    site.Crops.Any(c => !terrain.InBounds(c) || !terrain[c].Has(TerrainTraits.Fertile) ||
                        terrain[c].Has(TerrainTraits.Roofed) || !site.Field.Contains(c)))
                    errors.Add("joint_field_capacity_or_roof");
                HashSet<GridPoint> fieldReserve = new HashSet<GridPoint>(site.Field.Points());
                fieldReserve.UnionWith(site.AllPostClearance);
                if (site.Crops.Intersect(site.Lane.Concat(site.Wall).Concat(site.PostClearance)).Any())
                    errors.Add("joint_field_reservation_overlap");
                IEnumerable<GridPoint> compoundFootprint = layout.Enclosure.Concat(layout.BuiltWalls)
                    .Concat(layout.NaturalWalls).Concat(layout.OuterWalls)
                    .Concat(layout.Gates.Select(g => g.Cell))
                    .Concat(layout.Gates.SelectMany(g => g.BandDoors.Concat(g.BandWalls)))
                    .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points()))
                    .Concat(layout.PowerYards.SelectMany(y => y.Cells))
                    .Concat(layout.ApproachDefense?.SallyPorts.Where(p => !p.Field)
                        .SelectMany(p => p.Doors) ?? new GridPoint[0]);
                if (compoundFootprint.Any(fieldReserve.Contains) ||
                    layout.ConstructedRoof.Any(fieldReserve.Contains))
                    errors.Add("joint_compound_consumes_field_or_defense");
                if (layout.Rooms.SelectMany(r => r.Bounds.Points()).Any(c => !site.Compound.Contains(c)) ||
                    layout.PowerYards.SelectMany(y => y.Cells).Any(c => !site.Compound.Contains(c)))
                    errors.Add("joint_packing_outside_compound_envelope");
                if (layout.FieldWall != null)
                {
                    P4BFieldWallPlan fieldWall = layout.FieldWall;
                    GridPoint fieldEntry = fieldWall.InnerGate ?? fieldWall.OuterGate;
                    if (!fieldWall.InnerGate.HasValue || fieldWall.Fields.Count < layout.FieldCellsRequired || !site.Interfaces.Any(option =>
                        option.InnerGate.Equals(fieldEntry) && option.OuterGate.Equals(fieldWall.OuterGate) &&
                        option.Crops.SequenceEqual(fieldWall.Fields) &&
                        option.Lane.Concat(option.PostAccess).Except(fieldWall.Walls).Distinct().OrderBy(c => c)
                            .SequenceEqual(fieldWall.Access) &&
                        option.PostClearance.SequenceEqual(fieldWall.ExteriorPosts)))
                        errors.Add("joint_field_interface_not_reserved");
                    HashSet<GridPoint> baselineWalls = new HashSet<GridPoint>(layout.BuiltWalls
                        .Concat(layout.NaturalWalls).Concat(layout.OuterWalls)
                        .Concat(layout.Gates.SelectMany(g => g.BandWalls)));
                    IReadOnlyList<string> fieldErrors = P4BFieldWall.Validate(terrain, fieldWall.Fields,
                        baselineWalls, fieldWall.Walls,
                        layout.Gates.Select(g => g.Cell).Concat(layout.Gates.SelectMany(g => g.BandDoors))
                            .Concat(fieldWall.AccessPorts.SelectMany(p => p.Doors))
                            .Concat(layout.ApproachDefense?.SallyPorts.SelectMany(p => p.Doors) ?? new GridPoint[0])
                            .Concat(new[] { fieldEntry, fieldWall.OuterGate }),
                        fieldWall.ExteriorPosts, layout.Gates[fieldWall.GateIndex].Inside, layout.ConstructedRoof);
                    foreach (string error in fieldErrors) errors.Add("joint_" + error);
                    if (!layout.Gates[fieldWall.GateIndex].ExteriorRoute.SequenceEqual(fieldWall.ArrivalRoute))
                        errors.Add("joint_arrival_route_not_registered_on_compound_gate");
                    string[] fieldWallCrossings = layout.Gates.Select((g, index) => new { Gate = g, Index = index })
                        .Where(item => item.Index != fieldWall.GateIndex && item.Gate.ExteriorRoute.Any(fieldWall.Walls.Contains))
                        .Select(item => item.Index + "@" + item.Gate.ExteriorRoute.First(fieldWall.Walls.Contains))
                        .ToArray();
                    if (fieldWallCrossings.Length > 0)
                        errors.Add("joint_other_gate_route_crosses_field_wall:" + string.Join(",", fieldWallCrossings));
                    if (!fieldWall.ArrivalRoute.Contains(fieldEntry) || !fieldWall.ArrivalRoute.Contains(fieldWall.OuterGate))
                        errors.Add("joint_arrival_route_missing_field_gate");
                    if (fieldWall.Walls.Any(c => enclosure.Contains(c) ||
                        layout.Rooms.Any(r => r.Bounds.Contains(c)) ||
                        layout.PowerYards.Any(y => y.Cells.Contains(c)) ||
                        layout.ConstructedRoof.Contains(c)) ||
                        fieldWall.Fields.Any(c => layout.ConstructedRoof.Contains(c)))
                        errors.Add("joint_field_compound_occupancy");
                }
            }

            if (layout.ApproachDefense != null)
                foreach (string error in P4BApproachDefensePlanner.Validate(input, layout)) errors.Add(error);
            foreach (string error in P4BApproachDefensePlanner.ValidateGateCaps(layout)) errors.Add(error);

            // Terrain legality and the excavation ledger.
            if (built.Any(c => grid.At(c) != LayoutCellClass.Open && grid.At(c) != LayoutCellClass.Covered)) errors.Add("built_wall_illegal_terrain");
            if (outer.Count != layout.OuterWalls.Count() || outer.Any(c => (grid.At(c) != LayoutCellClass.Open && grid.At(c) != LayoutCellClass.Covered) ||
                terrain[c].Has(TerrainTraits.MapEdge) || built.Contains(c))) errors.Add("outer_wall_illegal_or_duplicate");
            if (natural.Any(c => grid.At(c) != LayoutCellClass.Rock || excavation.Contains(c))) errors.Add("natural_wall_not_intact_rock");
            if (excavation.Any(c => grid.At(c) != LayoutCellClass.Rock)) errors.Add("excavation_not_natural_rock");
            if (excavation.Count > requirements.ExcavationBudgetCells) errors.Add("excavation_budget");
            if (layout.Doctrine == LayoutDoctrine.OpenGround && excavation.Any(c => grid.ThickCover(c)))
                errors.Add("open_ground_excavated_thick_roof_rock");
            HashSet<GridPoint> clearedRoof = new HashSet<GridPoint>(layout.ExhaustRoofClearance);
            if (retained.Any(c => !excavation.Contains(c) || !grid.ThickCover(c) || clearedRoof.Contains(c)) ||
                excavation.Any(c => grid.ThickCover(c) && !retained.Contains(c) && !clearedRoof.Contains(c)))
                errors.Add("retained_roof_ledger");
            if (LayoutRoofSupport.Unsupported(terrain, excavation, retained).Count > 0) errors.Add("roof_unsupported_without_built_walls");

            // Closed perimeter: every enclosure cell's cardinal neighbour is inside,
            // a blocking wall/rock, or a gate door. Nothing leaks to the exterior.
            HashSet<GridPoint> boundary = new HashSet<GridPoint>(built.Concat(natural).Concat(gateCells));
            if (enclosure.Any(c => Neighbors(c).Any(n => !enclosure.Contains(n) && !boundary.Contains(n)))) errors.Add("enclosure_not_closed");
            if (boundary.Any(enclosure.Contains)) errors.Add("wall_inside_enclosure");
            if (P4BPerimeterDepth.Minimum(terrain, layout) < layout.WallLayers)
                errors.Add("perimeter_breach_depth_below_declared_layers");

            // Rooms and corridors.
            HashSet<GridPoint> roomCells = new HashSet<GridPoint>();
            foreach (string overlap in SharedWallViolations(layout.Rooms)) errors.Add(overlap);
            foreach (LayoutRoom room in layout.Rooms) roomCells.UnionWith(room.Bounds.Points());
            HashSet<GridPoint> courtCells = new HashSet<GridPoint>(layout.Courts.SelectMany(c => c.Cells));
            HashSet<GridPoint> yardCells = new HashSet<GridPoint>();
            foreach (LayoutPowerYard yard in layout.PowerYards)
                foreach (GridPoint c in yard.Cells)
                    if (!yardCells.Add(c) || roomCells.Contains(c) || courtCells.Contains(c)) errors.Add("power_yard_overlap_" + yard.DefName);
            // Geyser cells serve a geothermal footprint only.
            HashSet<GridPoint> geysers = new HashSet<GridPoint>(input.Resources.GeyserFootprints());
            if (geysers.Any(c => roomCells.Contains(c) || courtCells.Contains(c) || built.Contains(c) || natural.Contains(c) ||
                layout.Corridors.Any(k => k.Cells.Contains(c)) || (yardCells.Contains(c) && !layout.PowerYards.Any(y => y.DefName == LayoutPowerModel.Geothermal && y.Cells.Contains(c)))))
                errors.Add("geyser_cell_used");
            foreach (LayoutCorridor corridor in layout.Corridors)
                if (corridor.Cells.Any(c => roomCells.Contains(c) || courtCells.Contains(c) || yardCells.Contains(c) || !enclosure.Contains(c))) errors.Add("corridor_overlap_" + corridor.Id);
            if (roomCells.Any(c => !enclosure.Contains(c)) || courtCells.Any(c => !enclosure.Contains(c) || roomCells.Contains(c)) ||
                yardCells.Any(c => !enclosure.Contains(c))) errors.Add("footprint_outside_enclosure");

            Func<GridPoint, bool> walkableTerrain = c => grid.InBounds(c) && !extra.Contains(c) &&
                (excavation.Contains(c) || grid.At(c) == LayoutCellClass.Open || grid.At(c) == LayoutCellClass.Covered);
            HashSet<GridPoint> pocketCells = new HashSet<GridPoint>(layout.ExhaustPockets.Select(p => p.Cell).Concat(layout.ExhaustPocketWalls));
            Func<GridPoint, bool> isPublic = c => enclosure.Contains(c) && !roomCells.Contains(c) && !courtCells.Contains(c) && !yardCells.Contains(c) &&
                !pocketCells.Contains(c) && walkableTerrain(c);
            HashSet<GridPoint> reach = new HashSet<GridPoint>();
            if (layout.Gates.Count < input.Fortification.MinimumGates || layout.Gates.Count == 0) errors.Add("gate_count");
            else reach = Flood(layout.Gates[0].Inside, isPublic);
            if (layout.Gates.Count > 0 && !reach.Contains(layout.Gates[0].Inside)) errors.Add("public_root_blocked");

            List<LayoutFixture> fixtures = layout.AllFixtures.ToList();
            if (fixtures.Any(f => f.DefName.IndexOf("Trap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.DefName.IndexOf("IED", StringComparison.OrdinalIgnoreCase) >= 0)) errors.Add("prohibited_defense");
            LayoutEquipment equipment = input.Equipment;
            LayoutKit kit = equipment.Kit;
            foreach (LayoutFixture fixture in fixtures)
            {
                CatalogDef def;
                if (!input.Catalog.TryGet(fixture.DefName, out def)) continue;
                if (ProhibitedContent.IsProhibited(def)) errors.Add("prohibited_defense");
                else if (!input.Catalog.Evaluate(def, kit.Stage).Legal) errors.Add("illegal_for_stage_" + fixture.DefName);
            }
            if (layout.TechStage == null || layout.TechStage.Digest != kit.Stage.Digest || layout.RolesDigest != kit.Roles.Digest) errors.Add("equipment_mismatch");
            LayoutMaterialLedger materials = equipment.Count(layout.Rooms,
                layout.BuiltWalls.Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(layout.OuterWalls)
                    .Concat(layout.ExhaustPocketWalls).Concat(layout.FieldWall == null
                        ? Enumerable.Empty<GridPoint>() : layout.FieldWall.Walls),
                layout.Gates.Sum(g => 1 + g.BandDoors.Count) + (layout.FieldWall == null ? 0 : 2) +
                    (layout.ApproachDefense?.SallyPorts.Sum(p => p.Doors.Count) ?? 0), layout.Power,
                layout.Turret == null ? Enumerable.Empty<string>() :
                    Enumerable.Repeat(P4BCoverNest.CoverDefName(input), layout.Turret.All.Sum(t => t.CoverCells.Count))
                        .Concat(layout.Turret.All.Select(t => t.DefName)));
            if (layout.Materials == null || layout.Materials.ToString() != materials.ToString()) errors.Add("materials_ledger_mismatch");
            LayoutMaterialLedger baseMaterials = layout.Turret == null ? materials :
                input.Equipment.Count(layout.Rooms,
                    layout.BuiltWalls.Concat(layout.Gates.SelectMany(g => g.BandWalls)).Concat(layout.OuterWalls)
                        .Concat(layout.ExhaustPocketWalls).Concat(layout.FieldWall?.Walls ?? Enumerable.Empty<GridPoint>()),
                    layout.Gates.Sum(g => 1 + g.BandDoors.Count) + (layout.FieldWall == null ? 0 : 2) +
                        (layout.ApproachDefense?.SallyPorts.Sum(p => p.Doors.Count) ?? 0), layout.Power);
            // Turret.ImportValue contains its construction and finite fuel.
            // The stored materials ledger already contains construction, so
            // budget from the pre-turret ledger to avoid charging it twice.
            if (baseMaterials.OrdinaryImportValue > requirements.BaseImportValueLimit ||
                baseMaterials.ImportValue + (layout.Turret?.ImportValue ?? 0) > requirements.ImportValueLimit)
                errors.Add(LayoutEquipment.MaterialBudgetExceeded);
            if (layout.Turret != null)
                foreach (string turretError in P4BTurretPlanner.Validate(input, layout, layout.Turret))
                    errors.Add("turret_" + turretError);

            // Inner-yard crops: enclosed, open-sky, fertile and off every
            // building, street, yard, pocket, roof and required field cell.
            if (layout.BonusFields.Count > 0)
            {
                HashSet<GridPoint> occupiedByLayout = new HashSet<GridPoint>(roomCells.Concat(courtCells).Concat(yardCells)
                    .Concat(layout.Corridors.SelectMany(k => k.Cells)).Concat(layout.ConstructedRoof)
                    .Concat(layout.ExhaustPockets.Select(p => p.Cell)).Concat(layout.ExhaustPocketWalls)
                    .Concat(layout.AllFixtures.SelectMany(f => f.Cells.Concat(f.Access)))
                    .Concat(layout.FieldWall?.Fields ?? new GridPoint[0]));
                if (layout.BonusFields.Any(c => !enclosure.Contains(c) || !terrain[c].Has(TerrainTraits.Fertile) ||
                    grid.At(c) != LayoutCellClass.Open || terrain[c].Has(TerrainTraits.Roofed) || occupiedByLayout.Contains(c)))
                    errors.Add("bonus_field_illegal_cell");
            }

            CatalogDef wallLight = input.Equipment.Kit.DefFor(SettlementRole.WallLight);
            ValidateWallLights(layout, wallLight, errors);
            foreach (LayoutRoom room in layout.Rooms)
            {
                ValidateRoom(room, requirements, grid, excavation, retained, extra, reach, errors, wallLight?.DefName);
                ValidateSeatsAndMedicalShelf(input, room, errors);
                if (room.Kind == LayoutRoomKind.Power && input.Equipment.Kit.Vent != null &&
                    room.Fixtures.Count(f => f.Kind == LayoutFixtureKind.WallVent) <
                    (room.Fixtures.Count(f => f.Kind == LayoutFixtureKind.Generator) + 1) / 2)
                    errors.Add("generator_vent_" + room.Id);
            }
            ValidateCoolersAndPockets(input, layout, grid, enclosure, roomCells, reach, errors);

            if (layout.Courts.Count > P4EArtillery.Capacity(requirements, input.Equipment.Kit)) errors.Add("mortar_capacity");
            if (layout.Courts.Select(c => c.RoomId).Distinct().Count() != layout.Courts.Count ||
                layout.Courts.SelectMany(c => c.Access).Distinct().Count() != layout.Courts.Count) errors.Add("mortar_duplicate_identity");
            if (layout.Rooms.Count(r => r.Kind == LayoutRoomKind.MortarMagazine) != layout.Courts.Count) errors.Add("mortar_magazine_count");
            foreach (LayoutFixture court in layout.Courts)
            {
                if (court.Cells.Any(c => grid.At(c) != LayoutCellClass.Open || !terrain[c].Has(TerrainTraits.SupportsHeavy)))
                    errors.Add("court_not_open_sky_heavy");
                if (!court.Access.All(reach.Contains)) errors.Add("court_access_disconnected");
                LayoutRoom[] stores = layout.Rooms.Where(r => r.Kind == LayoutRoomKind.MortarMagazine && r.Id == court.RoomId).ToArray();
                if (stores.Length != 1) errors.Add("mortar_magazine_count");
                else
                {
                    LayoutRoom store = stores[0];
                    if (store.Bounds.Width != 3 || store.Bounds.Height != 3 || store.Fixtures.Count != 1 ||
                        store.Fixtures[0].Kind != LayoutFixtureKind.Shelf || store.Fixtures[0].DefName != "ShelfSmall" ||
                        !store.Fixtures[0].Cells.SequenceEqual(store.Interior.Points())) errors.Add("mortar_magazine_geometry");
                    if (!store.Bounds.Points().All(layout.ConstructedRoof.Contains) ||
                        court.Cells.Concat(court.Access).Any(layout.ConstructedRoof.Contains)) errors.Add("mortar_magazine_roof");
                    if (store.Interior.Width == 1 && store.Interior.Height == 1 && court.Access.Count == 1)
                    {
                        GridPoint stock = store.Interior.Points().First(), op = court.Access.First();
                        if (Math.Abs(store.DoorOutside.X - op.X) + Math.Abs(store.DoorOutside.Z - op.Z) > 1 ||
                            (stock.X - op.X) * court.Facing.X + (stock.Z - op.Z) * court.Facing.Z < 0 ||
                            (op.X - store.Door.X) * -store.Facing.X + (op.Z - store.Door.Z) * -store.Facing.Z <= 0)
                            errors.Add("mortar_magazine_not_beside_operating_position");
                    }
                    if (MagazineLoadingRoute(input, layout, court).Count == 0) errors.Add("mortar_magazine_loading_route");
                }
            }

            // Gates, P4 gatehouse crossings and exterior approach routes.
            int layers = input.Fortification.WallLayers;
            if (layout.WallLayers != layers) errors.Add("wall_layers");
            HashSet<GridPoint> exteriorReserved = new HashSet<GridPoint>();
            for (int i = 0; i < layout.Gates.Count; i++)
            {
                LayoutGate gate = layout.Gates[i];
                string id = "gate" + i;
                if (grid.At(gate.Cell) != LayoutCellClass.Open && grid.At(gate.Cell) != LayoutCellClass.Covered) errors.Add(id + "_terrain");
                if (!reach.Contains(gate.Inside)) errors.Add(id + "_disconnected");
                if (!gate.Inside.Equals(new GridPoint(gate.Cell.X - gate.Outward.X, gate.Cell.Z - gate.Outward.Z))) errors.Add(id + "_inside_misaligned");
                if (gate.InnerVestibule.Any(c => !reach.Contains(c))) errors.Add(id + "_vestibule_blocked");
                if (gate.BandDoors.Count != layers - 1 || gate.BandWalls.Count != 4 * (layers - 1)) errors.Add(id + "_band_count");
                for (int k = 0; k < gate.BandDoors.Count; k++)
                    if (!gate.BandDoors[k].Equals(new GridPoint(gate.Cell.X + 2 * (k + 1) * gate.Outward.X, gate.Cell.Z + 2 * (k + 1) * gate.Outward.Z)))
                        errors.Add(id + "_band_misaligned");
                List<GridPoint> footprint = gate.BandDoors.Concat(gate.BandWalls).Concat(gate.OuterVestibule).Concat(new[] { gate.Staging }).ToList();
                if (footprint.Any(c => enclosure.Contains(c) || boundary.Contains(c) || grid.At(c) != LayoutCellClass.Open)) errors.Add(id + "_band_terrain");
                if (footprint.Any(exteriorReserved.Contains)) errors.Add(id + "_band_overlap");
                exteriorReserved.UnionWith(footprint);
                IReadOnlyList<GridPoint> route = gate.ExteriorRoute;
                if (route.Count == 0 || !route[0].Equals(gate.Staging) || !terrain[route[route.Count - 1]].Has(TerrainTraits.MapEdge)) errors.Add(id + "_route_endpoints");
                for (int k = 0; k < route.Count; k++)
                {
                    if (k > 0 && Math.Abs(route[k].X - route[k - 1].X) + Math.Abs(route[k].Z - route[k - 1].Z) != 1) { errors.Add(id + "_route_continuity"); break; }
                    if (!grid.Walkable(route[k]) || enclosure.Contains(route[k]) || boundary.Contains(route[k])) { errors.Add(id + "_route_reenters_or_impassable"); break; }
                }
            }
            if (layout.Gates.Select(g => g.Cell).Distinct().Count() != layout.Gates.Count) errors.Add("duplicate_gate");
            HashSet<GridPoint> outerReserved = new HashSet<GridPoint>(exteriorReserved);
            outerReserved.UnionWith(layout.Gates.SelectMany(g => g.ExteriorRoute));
            HashSet<GridPoint> seenOuter = new HashSet<GridPoint>();
            // Complete adjacent shells replace gate-adjacent sections; layouts
            // that still carry sections need one per gate and extra layer.
            if ((layout.OuterSections.Count != 0 && layout.OuterSections.Count != layout.Gates.Count * Math.Max(0, layers - 1)) ||
                layout.OuterSections.Select(s => (s.GateIndex, s.Layer)).Distinct().Count() != layout.OuterSections.Count)
                errors.Add("outer_sections_missing_or_duplicate");
            foreach (LayoutWallSection section in layout.OuterSections)
            {
                if (section.GateIndex < 0 || section.GateIndex >= layout.Gates.Count || section.Layer < 1 || section.Layer >= layers ||
                    section.Left.Count + section.Right.Count < 3)
                { errors.Add("outer_section_invalid"); continue; }
                LayoutGate gate = layout.Gates[section.GateIndex];
                GridPoint tangent = new GridPoint(gate.Outward.Z, gate.Outward.X);
                for (int side = -1; side <= 1; side += 2)
                {
                    IReadOnlyList<GridPoint> arm = side < 0 ? section.Left : section.Right;
                    IReadOnlyList<GridPoint> returned = side < 0 ? section.LeftReturn : section.RightReturn;
                    GridPoint junction = new GridPoint(gate.Cell.X + 2 * side * tangent.X + 2 * section.Layer * gate.Outward.X,
                        gate.Cell.Z + 2 * side * tangent.Z + 2 * section.Layer * gate.Outward.Z);
                    if (!gate.BandWalls.Contains(junction)) errors.Add("outer_section_disconnected_band");
                    LayoutWallSection inner = section.Layer > 1
                        ? layout.OuterSections.FirstOrDefault(s => s.GateIndex == section.GateIndex && s.Layer == section.Layer - 1)
                        : null;
                    if (section.Layer > 1 && (inner == null || arm.Count > (side < 0 ? inner.Left.Count : inner.Right.Count)))
                        errors.Add("outer_section_unsupported_layer");
                    for (int distance = 1; arm.Count > 0 && distance <= arm.Count + 1; distance++)
                    {
                        GridPoint lane = new GridPoint(gate.Cell.X + distance * side * tangent.X + (2 * section.Layer - 1) * gate.Outward.X,
                            gate.Cell.Z + distance * side * tangent.Z + (2 * section.Layer - 1) * gate.Outward.Z);
                        if (!grid.InBounds(lane) || grid.At(lane) != LayoutCellClass.Open || !grid.Passable(lane) ||
                            enclosure.Contains(lane) || boundary.Contains(lane) || outer.Contains(lane) ||
                            exteriorReserved.Contains(lane) || terrain[lane].Has(TerrainTraits.MapEdge))
                        { errors.Add("outer_section_service_lane_blocked"); break; }
                    }
                    for (int j = 0; j < arm.Count; j++)
                    {
                        GridPoint baseline = new GridPoint(gate.Cell.X + (j + 3) * side * tangent.X,
                            gate.Cell.Z + (j + 3) * side * tangent.Z);
                        GridPoint expected = new GridPoint(baseline.X + 2 * section.Layer * gate.Outward.X,
                            baseline.Z + 2 * section.Layer * gate.Outward.Z);
                        GridPoint inside = new GridPoint(baseline.X - gate.Outward.X, baseline.Z - gate.Outward.Z);
                        if (!arm[j].Equals(expected) || !boundary.Contains(baseline) || !enclosure.Contains(inside) ||
                            outerReserved.Contains(expected) || !seenOuter.Add(expected)) errors.Add("outer_section_geometry_or_overlap");
                    }
                    List<GridPoint> expectedReturn = new List<GridPoint>();
                    if (arm.Count > 0)
                    {
                        GridPoint end = arm[arm.Count - 1];
                        expectedReturn.Add(new GridPoint(end.X - gate.Outward.X, end.Z - gate.Outward.Z));
                    }
                    else
                    {
                        expectedReturn.Add(new GridPoint(gate.Cell.X + 2 * side * tangent.X + (2 * section.Layer - 1) * gate.Outward.X,
                            gate.Cell.Z + 2 * side * tangent.Z + (2 * section.Layer - 1) * gate.Outward.Z));
                        if (section.Layer == 1)
                            expectedReturn.Add(new GridPoint(gate.Cell.X + side * tangent.X + gate.Outward.X,
                                gate.Cell.Z + side * tangent.Z + gate.Outward.Z));
                    }
                    GridPoint terminal = expectedReturn[expectedReturn.Count - 1];
                    GridPoint parent = new GridPoint(terminal.X - gate.Outward.X, terminal.Z - gate.Outward.Z);
                    if (arm.Count == 0 && section.Layer == 1)
                        parent = new GridPoint(gate.Cell.X + side * tangent.X, gate.Cell.Z + side * tangent.Z);
                    IReadOnlyList<GridPoint> parentArm = inner == null ? null : side < 0 ? inner.Left : inner.Right;
                    bool anchored = section.Layer == 1 ? boundary.Contains(parent) :
                        arm.Count == 0 ? gate.BandWalls.Contains(parent) : parentArm != null && parentArm.Contains(parent);
                    if (!returned.SequenceEqual(expectedReturn) || !anchored || returned.Any(c => !grid.InBounds(c) ||
                        grid.At(c) != LayoutCellClass.Open || terrain[c].Has(TerrainTraits.MapEdge) ||
                        enclosure.Contains(c) || outerReserved.Contains(c) || !seenOuter.Add(c)))
                        errors.Add("outer_section_unreturned");
                }
                if (section.Layer > 1 && !layout.OuterSections.Any(s => s.GateIndex == section.GateIndex && s.Layer == section.Layer - 1))
                    errors.Add("outer_section_missing_inner_layer");
            }

            // Hard relationships use real public walking distance between door outsides.
            foreach (LayoutRelationship rel in layout.Relationships.Where(r => r.Hard))
            {
                LayoutRoom a = layout.Rooms.FirstOrDefault(r => r.Id == rel.A), b = layout.Rooms.FirstOrDefault(r => r.Id == rel.B);
                if (a == null || b == null) { errors.Add("relationship_missing_" + rel.A + "_" + rel.B); continue; }
                int distance = Distance(a.DoorOutside, b.DoorOutside, isPublic);
                if (distance > rel.MaxDistance) errors.Add("hard_relationship_" + rel.A + "_" + rel.B + "_" + distance);
            }

            // Capacity versus demand and power budget.
            LayoutDemandSet demand = equipment.Demand;
            foreach (LayoutRoomDemand room in demand.Rooms)
            {
                LayoutRoom realized = layout.Rooms.FirstOrDefault(r => r.Id == room.Id);
                // The power room is sized per attempt: it must hold the layout's own generator count.
                int needed = room.Kind == LayoutRoomKind.Power && layout.Power != null ? layout.Power.Generators : room.Capacity;
                if (room.Kind == LayoutRoomKind.Storage) needed = Math.Max(0, needed - (realized?.ReleasedCapacity ?? 0));
                if (realized == null || realized.Capacity < needed) errors.Add("capacity_" + room.Id);
            }
            if (layout.Rooms.Count(r => r.Kind != LayoutRoomKind.MortarMagazine) != demand.Rooms.Count) errors.Add("room_count");
            int beds = layout.Rooms.Where(r => r.Kind == LayoutRoomKind.Barracks || r.Kind == LayoutRoomKind.Bedroom).Sum(r => r.Capacity);
            if (beds < requirements.Residents) errors.Add("housing_beds");
            if (kit.Powered)
                ValidatePower(input, layout, grid, enclosure, roomCells, courtCells, yardCells, built, errors);
            else if (fixtures.Any(f => f.Watts > 0 || f.Kind == LayoutFixtureKind.Generator || f.Kind == LayoutFixtureKind.Battery) || layout.Power != null)
                errors.Add("unpowered_electric_content");
            return new ReadOnlyCollection<string>(errors.Distinct().ToList());
        }

        // Neighbouring rooms may share a common wall: a cell may belong to several
        // rooms only when it is a plain wall of each. Interiors, doors and wall
        // coolers are never shared (a cooler's sides would face two rooms).
        public static IReadOnlyList<string> SharedWallViolations(IEnumerable<LayoutRoom> rooms)
        {
            Dictionary<GridPoint, List<LayoutRoom>> owners = new Dictionary<GridPoint, List<LayoutRoom>>();
            foreach (LayoutRoom room in rooms)
                foreach (GridPoint c in room.Bounds.Points())
                {
                    List<LayoutRoom> list;
                    if (!owners.TryGetValue(c, out list)) owners[c] = list = new List<LayoutRoom>();
                    list.Add(room);
                }
            List<string> errors = new List<string>();
            foreach (KeyValuePair<GridPoint, List<LayoutRoom>> cell in owners.Where(o => o.Value.Count > 1))
                foreach (LayoutRoom room in cell.Value)
                    if (!IsSharableWall(room, cell.Key)) errors.Add("room_overlap_" + room.Id);
            return errors.Distinct().ToList();
        }

        internal static bool IsSharableWall(LayoutRoom room, GridPoint cell) =>
            room.IsEdge(cell) && !room.IsDoor(cell) &&
            !room.Fixtures.Any(f => LayoutFixtureKinds.ThroughWall(f.Kind) && f.Cells.Contains(cell));

        // Loadout rules (plan step 6): catalog legality and no prohibited content for every item and
        // stuff, role and climate rules, apparel layering, and the equipment budget.
        public static IReadOnlyList<string> ValidateLoadout(ProceduralLayoutInput input, ProceduralLayout layout, SettlementLoadout loadout)
        {
            List<string> errors = new List<string>();
            if (input == null || loadout == null) return new[] { "missing_input_or_loadout" };
            if (input.QuestContract != null)
            {
                // Native gear is descriptive. Do not invent replacement gear,
                // roles or climate equipment requirements for a scripted group.
                if (loadout.Digest != input.QuestContract.DescribeLoadout(input.Equipment.Kit.Stage).Digest)
                    errors.Add("native_roster_gear_changed");
                if (layout != null) errors.AddRange(input.QuestContract.ValidateReservations(layout));
                return new ReadOnlyCollection<string>(errors);
            }
            LayoutKit kit = input.Equipment.Kit;
            TechCatalog catalog = input.Catalog;
            LayoutClimate climate = input.Requirements.Climate;
            if (loadout.Stage.Digest != kit.Stage.Digest) errors.Add("loadout_stage_mismatch");
            foreach (LoadoutItem item in loadout.AllItems)
            {
                CatalogDef def;
                if (!catalog.TryGet(item.DefName, out def)) { errors.Add("loadout_unknown_" + item.DefName); continue; }
                if (ProhibitedContent.IsProhibited(def)) errors.Add("loadout_prohibited_" + item.DefName);
                else if (!catalog.IsLegal(def, kit.Stage)) errors.Add("loadout_illegal_" + item.DefName);
                if (item.Stuff != null && !catalog.IsLegal(item.Stuff, kit.Stage)) errors.Add("loadout_illegal_stuff_" + item.DefName + "_" + item.Stuff);
                if (item.Count <= 0) errors.Add("loadout_count_" + item.DefName);
                if (item.Quality.HasValue && !def.SupportsQuality) errors.Add("loadout_quality_unsupported_" + item.DefName);
            }
            if (loadout.Residents.Count != input.Requirements.Residents) errors.Add("loadout_resident_count");
            if (!loadout.Residents.Any(r => r.Role == LoadoutRole.RangedLine)) errors.Add("loadout_no_ranged_line");
            if (kit.Stage.Level < SettlementTechStageLevel.IndustrialMid && loadout.Residents.Any(r => r.Role == LoadoutRole.Heavy)) errors.Add("loadout_heavy_below_mid");
            if (layout != null && layout.Court != null && input.Requirements.Residents > 1 && !loadout.Residents.Any(r => r.Role == LoadoutRole.Support))
                errors.Add("loadout_no_mortar_crew");
            if (layout != null && layout.Courts.Count > 0 && loadout.Residents.Count(r => r.Weapon != null &&
                catalog.TryGet(r.Weapon.DefName, out CatalogDef gun) && gun.MaximumRange > 0) < layout.Courts.Count + 2)
                errors.Add("loadout_artillery_direct_staffing");
            if (loadout.Residents.Count(r => r.Elite) > 1) errors.Add("loadout_elite_count");
            bool cold = climate.HeatingRequired && !climate.CoolingRequired;
            foreach (ResidentLoadout resident in loadout.Residents)
            {
                if (resident.Utility != null && resident.Utility.DefName == "Apparel_ShieldBelt" &&
                    (resident.Weapon == null || !LayoutLoadoutModel.IsMelee(resident.Weapon.DefName))) errors.Add("shield_belt_on_ranged_" + resident.Index);
                List<CatalogDef> worn = resident.Apparel.Select(a => { CatalogDef d; catalog.TryGet(a.DefName, out d); return d; }).Where(d => d != null).ToList();
                for (int i = 0; i < worn.Count; i++)
                    for (int j = i + 1; j < worn.Count; j++)
                        if (LayoutLoadoutModel.Conflicts(worn[i], worn[j])) errors.Add("apparel_layer_conflict_" + resident.Index);
                if (climate.CoolingRequired && resident.Apparel.Any(a => a.DefName == "Apparel_Parka" || a.DefName == "Apparel_Duster"))
                    errors.Add("hot_climate_cold_gear_" + resident.Index);
                if (cold && !worn.Any(d => d.ApparelLayers.Contains("Shell"))) errors.Add("cold_without_shell_" + resident.Index);
            }
            int utilityTopUp = loadout.EquipmentValueLimit - input.Requirements.EquipmentValueLimit;
            int smokeValue = loadout.Residents.Where(r => r.Utility?.DefName == "Apparel_SmokepopBelt").Sum(r => r.Utility.Value);
            if (loadout.Success && loadout.EquipmentValue > loadout.EquipmentValueLimit) errors.Add("equipment_budget");
            if (utilityTopUp < 0 || utilityTopUp > input.Requirements.LogisticsUtilityValueLimit || utilityTopUp > smokeValue)
                errors.Add("equipment_limit_mismatch");
            return new ReadOnlyCollection<string>(errors.Distinct().ToList());
        }

        private static void ValidatePower(ProceduralLayoutInput input, ProceduralLayout layout, LayoutTerrainGrid grid, HashSet<GridPoint> enclosure,
            HashSet<GridPoint> roomCells, HashSet<GridPoint> courtCells, HashSet<GridPoint> yardCells, HashSet<GridPoint> built, List<string> errors)
        {
            LayoutKit kit = input.Equipment.Kit;
            LayoutPowerPlan plan = layout.Power;
            if (plan == null) { errors.Add("power_plan_missing"); return; }
            List<LayoutFixture> fixtures = layout.AllFixtures.ToList();
            int generators = fixtures.Count(f => f.Kind == LayoutFixtureKind.Generator);
            int batteries = fixtures.Count(f => f.Kind == LayoutFixtureKind.Battery);
            int critical, nonCritical;
            LayoutPowerModel.Loads(layout.Rooms, out critical, out nonCritical);
            int renewable = layout.PowerYards.Sum(y => y.EffectiveWatts);
            int turretWatts = layout.Turret?.TotalWatts ?? 0;
            if (generators != plan.Generators || plan.GeneratorDef != (kit.Generator == null ? string.Empty : kit.Generator.DefName) ||
                fixtures.Any(f => f.Kind == LayoutFixtureKind.Generator && f.DefName != plan.GeneratorDef)) errors.Add("power_generator_mismatch");
            if ((long)generators * kit.GeneratorWatts + plan.FirmWatts < critical + turretWatts) errors.Add("power_critical_uncovered");
            if ((long)generators * kit.GeneratorWatts + plan.FirmWatts < critical + nonCritical + turretWatts) errors.Add("power_generation_below_load");
            if (batteries != plan.Batteries || batteries < LayoutPowerModel.Batteries(critical + nonCritical + turretWatts) + plan.RenewableStorageBatteries) errors.Add("power_battery_count");
            int directFirm = LayoutPowerModel.FirmWatts(plan.Yards.Where(y =>
                y.DefName == LayoutPowerModel.Geothermal || y.DefName == LayoutPowerModel.Watermill));
            if (critical + turretWatts > directFirm && plan.RenewableStorageBatteries > 0 && generators == 0)
                errors.Add("power_buffered_critical_without_fuel_backup");
            if (layout.Rooms.Sum(r => r.ReleasedCapacity) != plan.ReleasedFuelStorageCells)
                errors.Add("power_fuel_storage_trim");
            int fuelStackLimit = Math.Max(1, kit.GeneratorFuel?.StackLimit ?? 1);
            int reservedFuelCells = (LayoutPowerModel.WarehouseFuel(kit.Generator, plan.ReservedGenerators) + fuelStackLimit - 1) / fuelStackLimit;
            int keptFuelCells = (LayoutPowerModel.WarehouseFuel(kit.Generator, generators) + fuelStackLimit - 1) / fuelStackLimit;
            if (plan.ReleasedFuelStorageCells < 0 || plan.ReleasedFuelStorageCells > reservedFuelCells - keptFuelCells ||
                layout.Rooms.Any(r => r.ReleasedCapacity < 0 || r.ReleasedCapacity > 0 && r.Kind != LayoutRoomKind.Storage))
                errors.Add("power_fuel_storage_trim_bound");
            if (fixtures.Any(f => f.Kind == LayoutFixtureKind.Battery && !layout.Rooms.Any(r => r.Kind == LayoutRoomKind.Power && r.Id == f.RoomId)))
                errors.Add("battery_outdoors");
            if (plan.FuelStock != LayoutPowerModel.FuelStock(kit.Generator, generators)) errors.Add("power_fuel_stock");
            if (layout.Doctrine == LayoutDoctrine.RockProtected && plan.Yards.Count > 0) errors.Add("power_yard_under_rock_doctrine");
            HashSet<GridPoint> geysers = new HashSet<GridPoint>(input.Resources.GeyserFootprints());
            HashSet<GridPoint> solarCells = new HashSet<GridPoint>(layout.PowerYards
                .Where(y => y.DefName == LayoutPowerModel.Solar).SelectMany(y => y.Cells));
            foreach (LayoutPowerYard yard in plan.Yards)
            {
                CatalogDef def;
                if (!input.Catalog.TryGet(yard.DefName, out def) || !input.Catalog.IsLegal(def, kit.Stage)) { errors.Add("power_yard_illegal_" + yard.DefName); continue; }
                if (yard.Cells.Count != def.SizeX * def.SizeZ) errors.Add("power_yard_footprint_" + yard.DefName);
                if (yard.Cells.Any(c => grid.At(c) != LayoutCellClass.Open)) errors.Add("power_yard_not_open_sky_" + yard.DefName);
                if (!yard.Cells.Any(c => Neighbors(c).Contains(yard.Access)) || !enclosure.Contains(yard.Access) || roomCells.Contains(yard.Access) ||
                    yardCells.Contains(yard.Access) || courtCells.Contains(yard.Access)) errors.Add("power_yard_access_" + yard.DefName);
                if (yard.DefName == LayoutPowerModel.Geothermal && !input.Resources.SteamGeyserCells.Any(g =>
                    yard.Cells.Contains(g) && yard.Cells.Min(c => c.X) == g.X - (def.SizeX - 1) / 2 && yard.Cells.Min(c => c.Z) == g.Z - (def.SizeZ - 1) / 2))
                    errors.Add("geothermal_not_on_geyser");
                if (yard.DefName == LayoutPowerModel.Wind)
                {
                    int minX = yard.Cells.Min(c => c.X), minZ = yard.Cells.Min(c => c.Z);
                    List<GridPoint> zone = LayoutAttempt.WindZone(new GridPoint(minX + 3, minZ));
                    if (!zone.OrderBy(c => c).SequenceEqual(yard.ZoneCells)) errors.Add("wind_zone_geometry");
                    if (zone.Any(c => !enclosure.Contains(c) || grid.At(c) != LayoutCellClass.Open || roomCells.Contains(c) || courtCells.Contains(c) ||
                        (yardCells.Contains(c) && !solarCells.Contains(c)) || built.Contains(c) || geysers.Contains(c))) errors.Add("wind_zone_obstructed");
                }
            }
        }

        private static void ValidateRoom(LayoutRoom room, LayoutRequirements requirements, LayoutTerrainGrid grid, HashSet<GridPoint> excavation,
            HashSet<GridPoint> retained, HashSet<GridPoint> extra, HashSet<GridPoint> reach, List<string> errors, string wallLight)
        {
            string id = room.Id;
            GridRect interior = room.Interior;
            int minimum = room.Kind == LayoutRoomKind.MortarMagazine ? 1 : LayoutDemandModel.MinInterior;
            int maximum = room.Kind == LayoutRoomKind.Power ? LayoutDemandModel.MaxPowerInteriorLength : LayoutDemandModel.MaxInterior;
            if (interior.Width < minimum || interior.Height < minimum ||
                Math.Max(interior.Width, interior.Height) > maximum ||
                Math.Min(interior.Width, interior.Height) > LayoutDemandModel.MaxInterior) errors.Add("room_size_" + id);
            bool corner = (room.Door.X == room.Bounds.MinX || room.Door.X == room.Bounds.MaxX) && (room.Door.Z == room.Bounds.MinZ || room.Door.Z == room.Bounds.MaxZ);
            if (!room.IsEdge(room.Door) || corner) errors.Add("door_not_on_wall_" + id);
            if (!interior.Contains(new GridPoint(room.Door.X + room.Facing.X, room.Door.Z + room.Facing.Z)) || room.Bounds.Contains(room.DoorOutside))
                errors.Add("door_facing_" + id);
            if (extra.Contains(room.Door) || !reach.Contains(room.DoorOutside)) errors.Add("room_door_disconnected_" + id);
            if (room.SideDoors.Count > 0 && room.Kind != LayoutRoomKind.Dining) errors.Add("side_doors_not_dining_" + id);
            foreach (GridPoint door in room.SideDoors)
            {
                bool sideCorner = (door.X == room.Bounds.MinX || door.X == room.Bounds.MaxX) && (door.Z == room.Bounds.MinZ || door.Z == room.Bounds.MaxZ);
                if (!room.IsEdge(door) || sideCorner || door.Equals(room.Door)) errors.Add("side_door_not_on_wall_" + id);
                if (extra.Contains(door) || !reach.Contains(room.OutsideOf(door))) errors.Add("side_door_disconnected_" + id);
                GridPoint inside = new GridPoint(2 * door.X - room.OutsideOf(door).X, 2 * door.Z - room.OutsideOf(door).Z);
                if (room.Fixtures.Any(f => f.Cells.Contains(inside) || f.Cells.Contains(door))) errors.Add("side_door_blocked_" + id);
            }
            if (room.Capacity < room.DemandedCapacity) errors.Add("room_capacity_" + id);
            foreach (GridPoint c in room.Bounds.Points())
            {
                LayoutCellClass cls = grid.At(c);
                bool edge = room.IsEdge(c);
                if (cls == LayoutCellClass.Bad) errors.Add("room_on_illegal_terrain_" + id);
                else if (cls == LayoutCellClass.Rock && !excavation.Contains(c) && (!edge || room.IsDoor(c))) errors.Add("room_unexcavated_rock_" + id);
            }
            if (room.ProtectedByRock && interior.Points().Any(c => !retained.Contains(c) && grid.At(c) != LayoutCellClass.Covered)) errors.Add("protection_claim_" + id);

            HashSet<GridPoint> blocking = new HashSet<GridPoint>(room.Fixtures.Where(f => f.Blocking && !LayoutFixtureKinds.ThroughWall(f.Kind)).SelectMany(f => f.Cells));
            blocking.UnionWith(extra);
            HashSet<GridPoint> local = Flood(room.Door, c => c.Equals(room.Door) || (interior.Contains(c) && !blocking.Contains(c)));
            foreach (LayoutFixture fixture in room.Fixtures)
            {
                string label = id + "_" + fixture.Kind;
                if (LayoutFixtureKinds.ThroughWall(fixture.Kind))
                {
                    GridPoint cell = fixture.Cells[0];
                    bool coolerCorner = (cell.X == room.Bounds.MinX || cell.X == room.Bounds.MaxX) && (cell.Z == room.Bounds.MinZ || cell.Z == room.Bounds.MaxZ);
                    GridPoint exhaust = new GridPoint(cell.X - fixture.Facing.X, cell.Z - fixture.Facing.Z);
                    if (!room.IsEdge(cell) || coolerCorner || room.IsDoor(cell)) errors.Add("cooler_not_in_wall_" + label);
                    // Recomputed from the rotation string with vanilla's formula, independently of Facing.
                    GridPoint hotStep = VanillaNorthRotatedBy(fixture.Rotation);
                    GridPoint vanillaHot = new GridPoint(cell.X + hotStep.X, cell.Z + hotStep.Z);
                    GridPoint vanillaCold = new GridPoint(cell.X - hotStep.X, cell.Z - hotStep.Z);
                    if (!interior.Contains(vanillaCold) || room.Bounds.Contains(vanillaHot) || !vanillaHot.Equals(exhaust))
                        errors.Add("cooler_orientation_" + label);
                    if (!fixture.Exhaust.HasValue || !fixture.Exhaust.Value.Equals(exhaust)) errors.Add("cooler_exhaust_mismatch_" + label);
                }
                else if (fixture.Cells.Any(c => !interior.Contains(c))) errors.Add("fixture_outside_interior_" + label);
                if (fixture.Access.Any(c => blocking.Contains(c) || !local.Contains(c))) errors.Add("interaction_unreachable_" + label);
                if (fixture.Blocking && !LayoutFixtureKinds.ThroughWall(fixture.Kind) && !fixture.Cells.Any(c => Neighbors(c).Any(local.Contains)))
                    errors.Add("job_access_" + label);
                // A wall light hangs over racks and furniture; the wall-light check covers it.
                if (!fixture.Blocking && fixture.Cells.Any(c => !local.Contains(c)) &&
                    !(fixture.Role == SettlementRole.Light && fixture.DefName == wallLight)) errors.Add("zone_unreachable_" + label);
                if ((fixture.Kind == LayoutFixtureKind.Stove || fixture.Kind == LayoutFixtureKind.Workbench || fixture.Kind == LayoutFixtureKind.Generator) &&
                    fixture.Cells.Any(c => !excavation.Contains(c) && !grid.Terrain[c].Has(TerrainTraits.SupportsHeavy))) errors.Add("heavy_floor_" + label);
                if ((fixture.Kind == LayoutFixtureKind.Stove || fixture.Kind == LayoutFixtureKind.Workbench) && !BackToWall(room, fixture))
                    errors.Add("workbench_not_against_wall_" + label);
            }
            if (room.Kind != LayoutRoomKind.MortarMagazine && !room.Fixtures.Any(f => f.Role == SettlementRole.Light)) errors.Add("lighting_" + id);
            if (room.Kind == LayoutRoomKind.Freezer && !room.Fixtures.Any(f => f.Kind == LayoutFixtureKind.WallCooler)) errors.Add("freezer_cooling_" + id);
            if (LayoutDemandModel.IsLiving(room.Kind))
            {
                if (requirements.Climate.HeatingRequired && !room.Fixtures.Any(f => f.Role == SettlementRole.Heat))
                    errors.Add("heating_" + id);
                if (requirements.Climate.CoolingRequired && !room.ProtectedByRock && !room.Fixtures.Any(f => f.Role == SettlementRole.Cool))
                    errors.Add("cooling_" + id);
            }
            if (room.Kind != LayoutRoomKind.Power && room.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Battery || f.Kind == LayoutFixtureKind.Generator))
                errors.Add("power_equipment_outside_power_room_" + id);
        }

        // Vanilla Placeworker_AttachedToWall: the cell the light faces must stay a
        // wall. The spawner builds every room edge that is not a door, gate, sally
        // port or through-wall fixture, so that is the support a wall light needs.
        private static void ValidateWallLights(ProceduralLayout layout, CatalogDef wallLight, List<string> errors)
        {
            if (wallLight == null) return;
            HashSet<GridPoint> openings = new HashSet<GridPoint>(layout.Gates.Select(g => g.Cell)
                .Concat(layout.Gates.SelectMany(g => g.BandDoors)).Concat(layout.Rooms.SelectMany(r => r.Doors))
                .Concat(layout.ApproachDefense?.SallyPorts.SelectMany(p => p.Doors) ?? new GridPoint[0])
                .Concat(layout.Rooms.SelectMany(r => r.Fixtures).Where(f => LayoutFixtureKinds.ThroughWall(f.Kind)).SelectMany(f => f.Cells)));
            HashSet<KeyValuePair<GridPoint, string>> mounts = new HashSet<KeyValuePair<GridPoint, string>>();
            foreach (LayoutRoom room in layout.Rooms)
                foreach (LayoutFixture light in room.Fixtures.Where(f => f.Role == SettlementRole.Light && f.DefName == wallLight.DefName))
                {
                    GridPoint cell = light.Cells[0], support = new GridPoint(cell.X - light.Facing.X, cell.Z - light.Facing.Z);
                    if (light.Cells.Count != 1 || !room.Interior.Contains(cell) || !room.IsEdge(support) || openings.Contains(support))
                        errors.Add("wall_light_unsupported_" + room.Id);
                    if (!mounts.Add(new KeyValuePair<GridPoint, string>(cell, light.Rotation))) errors.Add("wall_light_duplicate_" + room.Id);
                }
        }

        private static void ValidateSeatsAndMedicalShelf(ProceduralLayoutInput input, LayoutRoom room, List<string> errors)
        {
            LayoutFixture[] seats = room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.Chair).ToArray();
            if (seats.SelectMany(f => f.Cells).Distinct().Count() != seats.Length) errors.Add("duplicate_seat_" + room.Id);
            foreach (LayoutFixture seat in seats)
                if (!input.Catalog.TryGet(seat.DefName, out CatalogDef def) || !def.IsSittable ||
                    def.SizeX != 1 || def.SizeZ != 1 || def.Passability != CatalogPassability.Standable)
                    errors.Add("invalid_seat_" + room.Id);
            foreach (LayoutFixture bench in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.Workbench || f.Kind == LayoutFixtureKind.Stove))
            {
                if (!input.Catalog.TryGet(bench.DefName, out CatalogDef def) || !def.SupportsSeatedWork ||
                    !def.HasInteractionCell || input.Equipment.Kit.DefFor(SettlementRole.Seat) == null) continue;
                if (bench.Access.Count != 1 || seats.Count(s => s.Cells.Contains(bench.Access[0]) && s.Rotation == bench.Rotation) != 1)
                    errors.Add("work_seat_missing_or_misoriented_" + room.Id);
            }
            foreach (LayoutFixture table in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.Table))
                foreach (GridPoint access in table.Access)
                    if (seats.Count(s => s.Cells.Contains(access) && table.Cells.Contains(
                        new GridPoint(access.X - s.Facing.X, access.Z - s.Facing.Z))) != 1)
                        errors.Add("dining_seat_missing_or_misoriented_" + room.Id);
            if (room.Kind == LayoutRoomKind.Medical && input.Equipment.Kit.DefFor(SettlementRole.MedicalStorage) != null &&
                !room.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Shelf && f.Role == SettlementRole.MedicalStorage))
                errors.Add("medical_shelf_missing_" + room.Id);
        }

        // Vanilla temperature semantics on the final plan: each cooler cools its own room
        // and exhausts into a room with UsesOutdoorTemperature; exhaust pockets are sealed,
        // doorless, unroofed, off the public circulation, and within the roof-hole budget.
        private static void ValidateCoolersAndPockets(ProceduralLayoutInput input, ProceduralLayout layout, LayoutTerrainGrid grid,
            HashSet<GridPoint> enclosure, HashSet<GridPoint> roomCells, HashSet<GridPoint> reach, List<string> errors)
        {
            LayoutRoomPartition partition = LayoutRoomPartition.Build(input.Terrain, layout);
            foreach (LayoutRoom room in layout.Rooms)
                foreach (LayoutFixture cooler in room.Fixtures.Where(f => LayoutFixtureKinds.ThroughWall(f.Kind)))
                {
                    string label = room.Id + "_" + cooler.Cells[0];
                    int served = partition.RoomAt(new GridPoint(room.Interior.MinX, room.Interior.MinZ));
                    if (partition.RoomAt(cooler.ColdCell.Value) != served || served < 0) errors.Add("cooler_cold_side_not_served_room_" + label);
                    if (!partition.IsOutdoor(cooler.HotCell.Value)) errors.Add("cooler_exhaust_not_outdoor_" + label);
                    if (roomCells.Contains(cooler.HotCell.Value)) errors.Add("cooler_exhaust_into_room_" + label);
                }
            HashSet<GridPoint> roomWalls = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Bounds.Points().Where(c => r.IsEdge(c) && !r.IsDoor(c))));
            HashSet<GridPoint> doors = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Doors).Concat(layout.Gates.Select(g => g.Cell)));
            HashSet<GridPoint> excavation = new HashSet<GridPoint>(layout.Excavation);
            HashSet<GridPoint> retained = new HashSet<GridPoint>(layout.RetainedThickRoof);
            HashSet<GridPoint> constructed = new HashSet<GridPoint>(layout.ConstructedRoof);
            HashSet<GridPoint> allPocketWalls = new HashSet<GridPoint>(layout.ExhaustPocketWalls);
            foreach (LayoutExhaustPocket pocket in layout.ExhaustPockets)
            {
                string label = "pocket_" + pocket.Cell;
                GridPoint c = pocket.Cell;
                if (!enclosure.Contains(c)) errors.Add(label + "_outside_enclosure");
                if (reach.Contains(c) || roomCells.Contains(c)) errors.Add(label + "_not_sealed");
                foreach (GridPoint n in Neighbors(c))
                {
                    bool intactRock = grid.At(n) == LayoutCellClass.Rock && !excavation.Contains(n);
                    if (doors.Contains(n)) errors.Add(label + "_has_door");
                    else if (!allPocketWalls.Contains(n) && !roomWalls.Contains(n) && !intactRock) errors.Add(label + "_not_sealed");
                }
                if (constructed.Contains(c) || retained.Contains(c)) errors.Add(label + "_roofed");
                if (grid.Roofed(c) != pocket.RoofCleared || (pocket.RoofCleared && !pocket.Excavated)) errors.Add(label + "_roof_ledger");
                if (pocket.Excavated != (grid.At(c) == LayoutCellClass.Rock) || (pocket.Excavated && !excavation.Contains(c))) errors.Add(label + "_excavation_ledger");
                if (pocket.Walls.Any(w => grid.At(w) != LayoutCellClass.Open && grid.At(w) != LayoutCellClass.Covered)) errors.Add(label + "_wall_terrain");
                if (!layout.Rooms.Any(r => r.Fixtures.Any(f => LayoutFixtureKinds.ThroughWall(f.Kind) && f.HotCell.Value.Equals(c)))) errors.Add(label + "_unused");
            }
            if (layout.Doctrine == LayoutDoctrine.RockProtected)
            {
                int cooled = input.Equipment.Demand.Rooms.Count(r => LayoutDemandModel.NeedsOutdoorAir(r.Kind, input.Requirements, input.Equipment.Kit));
                if (layout.ExhaustRoofClearance.Count > (cooled + 1) / 2) errors.Add("exhaust_roof_hole_budget");
            }
        }

        // IntVec3.North.RotatedBy(rot) for the four vanilla rotations.
        internal static GridPoint VanillaNorthRotatedBy(string rotation)
        {
            switch (rotation)
            {
                case "East": return new GridPoint(1, 0);
                case "South": return new GridPoint(0, -1);
                case "West": return new GridPoint(-1, 0);
                default: return new GridPoint(0, 1);
            }
        }

        // The fixture's back row touches the wall and its interaction cell is in front, facing the room.
        private static bool BackToWall(LayoutRoom room, LayoutFixture fixture)
        {
            if (fixture.Access.Count != 1) return false;
            GridPoint back = new GridPoint(-fixture.Facing.X, -fixture.Facing.Z);
            // Multi-row benches touch the wall with their back row only. The
            // older all-cells check rejected every legal deep fabrication bench.
            int width = fixture.Facing.X != 0 ? fixture.Cells.Select(c => c.Z).Distinct().Count() :
                fixture.Cells.Select(c => c.X).Distinct().Count();
            bool touchesWall = fixture.Cells.Count(c => room.IsEdge(new GridPoint(c.X + back.X, c.Z + back.Z))) == width;
            GridPoint access = fixture.Access[0];
            return touchesWall && fixture.Cells.Any(c => new GridPoint(c.X + fixture.Facing.X, c.Z + fixture.Facing.Z).Equals(access));
        }

        public static IReadOnlyCollection<GridPoint> PublicCirculation(ProceduralLayoutInput input, ProceduralLayout layout,
            bool includeDefenseConnectors = true)
        {
            LayoutTerrainGrid grid = LayoutTerrainGrid.Build(input.Terrain, 0, input.Resources.GeyserFootprints());
            HashSet<GridPoint> enclosure = new HashSet<GridPoint>(layout.Enclosure);
            HashSet<GridPoint> excavation = new HashSet<GridPoint>(layout.Excavation);
            HashSet<GridPoint> blocked = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Bounds.Points()).Concat(layout.ExtraObstacles)
                .Concat(layout.Courts.SelectMany(c => c.Cells))
                .Concat(layout.ExhaustPockets.Select(p => p.Cell)).Concat(layout.ExhaustPocketWalls));
            if (layout.Gates.Count == 0) return new GridPoint[0];
            HashSet<GridPoint> result = Flood(layout.Gates[0].Inside, c => enclosure.Contains(c) && !blocked.Contains(c) &&
                (excavation.Contains(c) || grid.At(c) == LayoutCellClass.Open || grid.At(c) == LayoutCellClass.Covered));
            if (includeDefenseConnectors)
                result.UnionWith(layout.ApproachDefense?.SallyPorts.Where(p => !p.Field)
                    .SelectMany(p => p.InteriorRoute) ?? Enumerable.Empty<GridPoint>());
            return new ReadOnlyCollection<GridPoint>(result.OrderBy(c => c).ToList());
        }

        public static IReadOnlyList<GridPoint> MagazineLoadingRoute(ProceduralLayoutInput input, ProceduralLayout layout, LayoutFixture court = null)
        {
            court = court ?? layout.Court;
            LayoutRoom store = layout.Rooms.FirstOrDefault(r => r.Kind == LayoutRoomKind.MortarMagazine && r.Id == court?.RoomId);
            if (store == null || court == null || court.Access.Count != 1) return new GridPoint[0];
            var allowed = new HashSet<GridPoint>(PublicCirculation(input, layout));
            GridPoint start = court.Access.Single(), end = store.DoorOutside;
            if (!allowed.Contains(start) || !allowed.Contains(end)) return new GridPoint[0];
            var previous = new Dictionary<GridPoint, GridPoint> { [start] = start };
            var queue = new Queue<GridPoint>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                GridPoint c = queue.Dequeue();
                if (c.Equals(end))
                {
                    var route = new List<GridPoint> { c };
                    while (!c.Equals(start)) { c = previous[c]; route.Add(c); }
                    route.Reverse();
                    return route.Count <= 2 ? route.AsReadOnly() : (IReadOnlyList<GridPoint>)new GridPoint[0];
                }
                foreach (GridPoint n in Neighbors(c))
                    if (allowed.Contains(n) && !previous.ContainsKey(n)) { previous[n] = c; queue.Enqueue(n); }
            }
            return new GridPoint[0];
        }

        private static HashSet<GridPoint> Flood(GridPoint start, Func<GridPoint, bool> allowed)
        {
            HashSet<GridPoint> result = new HashSet<GridPoint>();
            if (!allowed(start)) return result;
            Queue<GridPoint> queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            result.Add(start);
            while (queue.Count > 0)
                foreach (GridPoint next in Neighbors(queue.Dequeue()))
                    if (allowed(next) && result.Add(next)) queue.Enqueue(next);
            return result;
        }

        private static int Distance(GridPoint from, GridPoint to, Func<GridPoint, bool> allowed)
        {
            if (!allowed(from)) return int.MaxValue;
            Dictionary<GridPoint, int> distance = new Dictionary<GridPoint, int> { { from, 0 } };
            Queue<GridPoint> queue = new Queue<GridPoint>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                GridPoint current = queue.Dequeue();
                if (current.Equals(to)) return distance[current];
                foreach (GridPoint next in Neighbors(current))
                    if (!distance.ContainsKey(next) && allowed(next)) { distance[next] = distance[current] + 1; queue.Enqueue(next); }
            }
            return int.MaxValue;
        }

        internal static IEnumerable<GridPoint> Neighbors(GridPoint c)
        {
            yield return new GridPoint(c.X, c.Z - 1);
            yield return new GridPoint(c.X - 1, c.Z);
            yield return new GridPoint(c.X + 1, c.Z);
            yield return new GridPoint(c.X, c.Z + 1);
        }
    }

    // Vanilla RoofCollapseUtility: a roofed cell survives while a roof holder is
    // within 6.9 cells through roofed cells. Here only intact natural rock counts
    // as a holder, so the retained thick roof stays up even if every built wall is lost.
    public static class LayoutRoofSupport
    {
        public const double MaxSupportDistance = 6.9;

        public static IReadOnlyList<GridPoint> Unsupported(TerrainSnapshot terrain, IEnumerable<GridPoint> excavated, IEnumerable<GridPoint> retainedRoof)
        {
            HashSet<GridPoint> dug = new HashSet<GridPoint>(excavated ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> retained = new HashSet<GridPoint>(retainedRoof ?? Enumerable.Empty<GridPoint>());
            Func<GridPoint, bool> holder = c => terrain.InBounds(c) && terrain[c].Has(TerrainTraits.NaturalRock) && !dug.Contains(c);
            double limit = MaxSupportDistance * MaxSupportDistance;
            List<GridPoint> result = new List<GridPoint>();
            foreach (GridPoint cell in retained.OrderBy(c => c))
            {
                Func<GridPoint, bool> within = c => (c.X - cell.X) * (c.X - cell.X) + (c.Z - cell.Z) * (c.Z - cell.Z) <= limit;
                HashSet<GridPoint> seen = new HashSet<GridPoint> { cell };
                Queue<GridPoint> queue = new Queue<GridPoint>();
                queue.Enqueue(cell);
                bool supported = false;
                while (queue.Count > 0 && !supported)
                {
                    GridPoint current = queue.Dequeue();
                    if (holder(current) || ProceduralLayoutValidator.Neighbors(current).Any(n => within(n) && holder(n))) { supported = true; break; }
                    foreach (GridPoint next in ProceduralLayoutValidator.Neighbors(current))
                        if (within(next) && retained.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                }
                if (!supported) result.Add(cell);
            }
            return new ReadOnlyCollection<GridPoint>(result);
        }
    }

    // Converts a P4A layout into the committed P4 plan type so P4Connectivity can
    // cross-check it. Losses are reported, never hidden; the result is not a
    // spawnable plan and PhysicalSettlementValidator is intentionally not applied.
    public static class ProceduralLayoutAdapter
    {
        public static PhysicalSettlementPlan ToPhysicalPlan(ProceduralLayoutInput input, ProceduralLayout layout, string wallStuff,
            out IReadOnlyList<string> incompatibilities, out IReadOnlyList<string> losses)
        {
            List<string> blockers = new List<string>();
            List<string> lost = new List<string>
            {
                "multi_cell_fixtures_reduced_to_one_anchor_placement",
                "heater_lamp_torch_campfire_passive_cooler_mapped_to_blocking_workbench_kind",
                "mortar_court_mapped_to_blocking_cover_placements",
                "retained_thick_roof_not_representable_in_TerrainMutationFootprint",
                "multiple_rooms_per_facility_kind_violate_P2_one_per_kind_rule",
                "power_yard_cells_mapped_to_blocking_generator_placements"
            };
            GridRect compound = layout.BoundingRect;
            foreach (LayoutGate gate in layout.Gates)
            {
                bool onEdge = gate.Cell.Z == compound.MinZ || gate.Cell.Z == compound.MaxZ || gate.Cell.X == compound.MinX || gate.Cell.X == compound.MaxX;
                if (!onEdge || !InsideGate(gate.Cell, compound).Equals(gate.Inside)) blockers.Add("gate_endpoint_not_inferable_from_compound_" + gate.Cell);
                if (layout.WallLayers > 1 && gate.Outward.Z == 0) blockers.Add("east_west_gatehouse_band_unsupported_" + gate.Cell);
            }
            incompatibilities = new ReadOnlyCollection<string>(blockers);
            losses = new ReadOnlyCollection<string>(lost);
            if (blockers.Count > 0) return null;

            List<PhysicalFacility> facilities = layout.Rooms.Select(r => new PhysicalFacility(r.Id, FacilityKindOf(r.Kind), r.Bounds, r.Door,
                r.Fixtures.SelectMany(f => f.Access).Distinct(), r.Capacity, true)).ToList();
            List<PhysicalPlacement> placements = new List<PhysicalPlacement>();
            foreach (LayoutRoom room in layout.Rooms)
            {
                HashSet<GridPoint> occupied = new HashSet<GridPoint>(room.Fixtures.Where(f => f.Blocking).SelectMany(f => f.Cells));
                // Wall lights hang over other fixtures and never block movement.
                foreach (LayoutFixture fixture in room.Fixtures.Where(f => f.Kind != LayoutFixtureKind.Stockpile && f.Kind != LayoutFixtureKind.Chair &&
                    !(f.Role == SettlementRole.Light && !f.Blocking)))
                {
                    GridPoint anchor = fixture.Cells.OrderBy(c => ProceduralLayoutValidator.Neighbors(c)
                        .Any(n => fixture.Access.Contains(n) || (room.Interior.Contains(n) && !occupied.Contains(n))) ? 0 : 1).ThenBy(c => c).First();
                    placements.Add(new PhysicalPlacement(PlacementKindOf(fixture.Kind), fixture.DefName, string.Empty, anchor, fixture.Rotation,
                        room.Id, false, 0));
                }
            }
            foreach (LayoutFixture court in layout.Courts)
                foreach (GridPoint cell in court.Cells)
                    placements.Add(new PhysicalPlacement(PhysicalPlacementKind.Cover, "MortarCourtReservation", string.Empty, cell, "North", "court", false, 0));
            foreach (LayoutPowerYard yard in layout.PowerYards)
                foreach (GridPoint cell in yard.Cells)
                    placements.Add(new PhysicalPlacement(PhysicalPlacementKind.Generator, yard.DefName, string.Empty, cell, yard.Rotation, "yard", false, 0));
            IReadOnlyCollection<GridPoint> publicCells = ProceduralLayoutValidator.PublicCirculation(input, layout);
            TerrainMutationFootprint mutations = new TerrainMutationFootprint(layout.Excavation, Enumerable.Empty<GridPoint>(), layout.ConstructedRoof,
                layout.NaturalWalls.Concat(layout.Rooms.SelectMany(r => r.NaturalWalls)));
            LayoutGate first = layout.Gates[0];
            LayoutGate second = layout.Gates.Count > 1 ? layout.Gates[1] : first;
            return new PhysicalSettlementPlan(layout.Digest, input.Requirements.Digest, compound, new GridRect(0, 0, 0, 0),
                layout.Gates.Select(g => g.Cell), layout.BuiltWalls.Concat(layout.NaturalWalls).Concat(layout.ExhaustPocketWalls),
                layout.Gates.SelectMany(g => g.BandDoors), layout.Gates.SelectMany(g => g.BandWalls), layout.WallLayers,
                publicCells.Where(compound.Contains).OrderBy(c => c), Enumerable.Empty<GridPoint>(), first.ExteriorRoute, second.ExteriorRoute,
                facilities, placements, wallStuff, 1, 1,
                layout.Excavation.Count > 0 ? TerrainArchetype.ExcavatedMountain : TerrainArchetype.Flat, mutations);
        }

        // Mirrors PlannedNavigationGrid.InsideGate at a41477e.
        private static GridPoint InsideGate(GridPoint gate, GridRect compound) => gate.Z == compound.MinZ ? new GridPoint(gate.X, gate.Z + 1)
            : gate.Z == compound.MaxZ ? new GridPoint(gate.X, gate.Z - 1) : gate.X == compound.MinX ? new GridPoint(gate.X + 1, gate.Z) : new GridPoint(gate.X - 1, gate.Z);

        public static FacilityKind FacilityKindOf(LayoutRoomKind kind)
        {
            switch (kind)
            {
                case LayoutRoomKind.Barracks:
                case LayoutRoomKind.Bedroom: return FacilityKind.Housing;
                case LayoutRoomKind.Jail: return FacilityKind.Jail;
                case LayoutRoomKind.Medical: return FacilityKind.Medical;
                case LayoutRoomKind.Kitchen: return FacilityKind.Kitchen;
                case LayoutRoomKind.Freezer: return FacilityKind.Freezer;
                case LayoutRoomKind.Pantry: return FacilityKind.Pantry;
                case LayoutRoomKind.Dining: return FacilityKind.DiningRecreation;
                case LayoutRoomKind.Workshop: return FacilityKind.Workshop;
                case LayoutRoomKind.Laboratory: return FacilityKind.Laboratory;
                case LayoutRoomKind.Storage: return FacilityKind.BulkStorage;
                default: return FacilityKind.Power;
            }
        }

        private static PhysicalPlacementKind PlacementKindOf(LayoutFixtureKind kind)
        {
            switch (kind)
            {
                case LayoutFixtureKind.Bed: return PhysicalPlacementKind.OrdinaryBed;
                case LayoutFixtureKind.PrisonBed: return PhysicalPlacementKind.PrisonBed;
                case LayoutFixtureKind.MedicalBed: return PhysicalPlacementKind.MedicalBed;
                case LayoutFixtureKind.Table: return PhysicalPlacementKind.Dining;
                case LayoutFixtureKind.Recreation: return PhysicalPlacementKind.Recreation;
                case LayoutFixtureKind.Generator: return PhysicalPlacementKind.Generator;
                case LayoutFixtureKind.Battery: return PhysicalPlacementKind.Battery;
                case LayoutFixtureKind.WallCooler: return PhysicalPlacementKind.Cooler;
                case LayoutFixtureKind.Shelf: return PhysicalPlacementKind.StorageMarker;
                default: return PhysicalPlacementKind.Workbench;
            }
        }
    }

    // Lightweight inspectable export. North (max Z) is at the top.
    public static class LayoutAsciiExport
    {
        public const string Legend =
            "legend: '#' built wall  '^' intact rock  'G' compound arrival gate  'Q' compound defense crossing  'q' field crossing  'D' room door  '=' street  ',' public yard  '.' room floor  'i' interaction/access\n" +
            "        B bed  P prison bed  H medical bed  S stove  W workbench  T table  c chair  R recreation  _ stockpile  h heater\n" +
            "        f campfire  C wall cooler  p passive cooler  L lamp  t torch  E generator  A battery  M mortar court  V vitals monitor  k shelf\n" +
            "        Y power yard (solar/wind/geothermal)  w wind clear zone  x cooler exhaust pocket (sealed, unroofed)\n" +
            "        g band door  | band wall  v outer vestibule  s staging  * exterior route  X injected obstacle  \" inner-yard crops\n" +
            "        F field crops  @ defender slot  b cover (barricade/sandbags)  U turret\n" +
            "        outside: ' ' open ground  ':' roofed floor  '~' water  'o' occupied  '%' other unusable\n" +
            "roof view: 'R' retained thick rock roof  'r' constructed roof  '^' intact rock  'O' roof cleared over an exhaust pocket  ' ' open sky";

        public static string Render(TerrainSnapshot terrain, ProceduralLayout layout, int margin = 3)
        {
            GridRect rect = Expand(layout, terrain, margin);
            Dictionary<GridPoint, char> map = new Dictionary<GridPoint, char>();
            foreach (LayoutGate gate in layout.Gates)
            {
                foreach (GridPoint c in gate.ExteriorRoute) map[c] = '*';
                foreach (GridPoint c in gate.OuterVestibule) map[c] = 'v';
                map[gate.Staging] = 's';
                foreach (GridPoint c in gate.BandWalls) map[c] = '|';
                foreach (GridPoint c in gate.BandDoors) map[c] = 'g';
            }
            foreach (GridPoint c in layout.Enclosure) map[c] = ',';
            foreach (GridPoint c in layout.BonusFields) map[c] = '"';
            foreach (GridPoint c in layout.Corridors.SelectMany(x => x.Cells)) map[c] = '=';
            foreach (GridPoint c in layout.Enclosure.Where(c => terrain[c].Has(TerrainTraits.NaturalRock) && !layout.Excavation.Contains(c))) map[c] = '^';
            foreach (GridPoint c in layout.BuiltWalls.Concat(layout.ExhaustPocketWalls)) map[c] = '#';
            foreach (GridPoint c in layout.OuterWalls) map[c] = 'H';
            foreach (LayoutExhaustPocket pocket in layout.ExhaustPockets) map[pocket.Cell] = 'x';
            foreach (GridPoint c in layout.NaturalWalls) map[c] = '^';
            foreach (LayoutRoom room in layout.Rooms)
            {
                foreach (GridPoint c in room.Bounds.Points()) map[c] = room.NaturalWalls.Contains(c) ? '^' : room.IsEdge(c) ? '#' : '.';
                foreach (GridPoint door in room.Doors) map[door] = 'D';
            }
            foreach (LayoutFixture fixture in layout.AllFixtures)
            {
                foreach (GridPoint c in fixture.Access) if (fixture.Kind != LayoutFixtureKind.Table) map[c] = 'i';
                foreach (GridPoint c in fixture.Cells) map[c] = Symbol(fixture.Kind);
            }
            foreach (LayoutPowerYard yard in layout.PowerYards)
            {
                foreach (GridPoint c in yard.ZoneCells) if (map.ContainsKey(c) && (map[c] == ',' || map[c] == ' ')) map[c] = 'w';
                foreach (GridPoint c in yard.Cells) map[c] = 'Y';
                map[yard.Access] = 'i';
            }
            if (layout.FieldWall != null)
            {
                foreach (GridPoint c in layout.FieldWall.Fields) map[c] = 'F';
                foreach (GridPoint c in layout.FieldWall.Walls) map[c] = '#';
                foreach (GridPoint c in new[] { layout.FieldWall.OuterGate }.Concat(layout.FieldWall.InnerGate.HasValue
                    ? new[] { layout.FieldWall.InnerGate.Value } : new GridPoint[0])) map[c] = 'q';
                foreach (P4BSallyPort port in layout.FieldWall.AccessPorts)
                    foreach (GridPoint door in port.Doors) map[door] = 'q';
            }
            foreach (P4BPost post in layout.ApproachDefense?.Posts ?? Enumerable.Empty<P4BPost>())
            {
                foreach (GridPoint c in post.CoverCells) map[c] = 'b';
                foreach (GridPoint c in post.Slots) map[c] = '@';
            }
            foreach (P4BSallyPort port in layout.ApproachDefense?.SallyPorts ?? Enumerable.Empty<P4BSallyPort>())
                foreach (GridPoint door in port.Doors) map[door] = port.Field ? 'q' : 'Q';
            foreach (P4BTurretPlan turret in layout.Turret?.All ?? Enumerable.Empty<P4BTurretPlan>())
            {
                foreach (GridPoint c in turret.CoverCells) map[c] = 'b';
                foreach (GridPoint c in turret.Footprint) map[c] = 'U';
            }
            foreach (LayoutGate gate in layout.Gates) map[gate.Cell] = 'G';
            foreach (GridPoint c in layout.ExtraObstacles) map[c] = 'X';
            StringBuilder text = new StringBuilder();
            AppendHeader(text, layout);
            for (int z = rect.MaxZ; z >= rect.MinZ; z--)
            {
                for (int x = rect.MinX; x <= rect.MaxX; x++)
                {
                    GridPoint c = new GridPoint(x, z);
                    char ch;
                    text.Append(map.TryGetValue(c, out ch) ? ch : Terrain(terrain[c]));
                }
                text.Append('\n');
            }
            return text.ToString();
        }

        public static string RenderRoof(TerrainSnapshot terrain, ProceduralLayout layout, int margin = 3)
        {
            GridRect rect = Expand(layout, terrain, margin);
            HashSet<GridPoint> retained = new HashSet<GridPoint>(layout.RetainedThickRoof);
            HashSet<GridPoint> constructed = new HashSet<GridPoint>(layout.ConstructedRoof);
            HashSet<GridPoint> dug = new HashSet<GridPoint>(layout.Excavation);
            HashSet<GridPoint> cleared = new HashSet<GridPoint>(layout.ExhaustRoofClearance);
            StringBuilder text = new StringBuilder();
            for (int z = rect.MaxZ; z >= rect.MinZ; z--)
            {
                for (int x = rect.MinX; x <= rect.MaxX; x++)
                {
                    GridPoint c = new GridPoint(x, z);
                    TerrainCell cell = terrain[c];
                    text.Append(cleared.Contains(c) ? 'O' : retained.Contains(c) ? 'R' : constructed.Contains(c) ? 'r' : cell.Has(TerrainTraits.NaturalRock) && !dug.Contains(c) ? '^' :
                        cell.Has(TerrainTraits.Water) ? '~' : cell.Has(TerrainTraits.Roofed) ? ':' : ' ');
                }
                text.Append('\n');
            }
            return text.ToString();
        }

        private static void AppendHeader(StringBuilder text, ProceduralLayout layout)
        {
            text.Append("digest=").Append(layout.Digest).Append(" doctrine=").Append(layout.Doctrine).Append(" gates=").Append(layout.Gates.Count)
                .Append(" wallLayers=").Append(layout.WallLayers).Append(" court=").Append(layout.Court != null)
                .Append(" excavated=").Append(layout.Excavation.Count).Append(" retainedRoof=").Append(layout.RetainedThickRoof.Count)
                .Append(" enclosure=").Append(layout.Enclosure.Count).Append('\n');
            foreach (LayoutRoom room in layout.Rooms)
                text.Append("  ").Append(room.Id.PadRight(11)).Append(" bounds=").Append(room.Bounds.ToString().PadRight(12))
                    .Append(" door=").Append(room.Door.ToString().PadRight(7)).Append(" ").Append(room.Capacity).Append('/').Append(room.DemandedCapacity)
                    .Append(' ').Append(room.Unit).Append(room.ProtectedByRock ? " rock-protected" : string.Empty).Append('\n');
            foreach (string note in layout.Notes) text.Append("  note: ").Append(note).Append('\n');
            if (layout.Power != null)
            {
                text.Append("  power: ").Append(layout.Power).Append('\n');
                foreach (string line in layout.Power.Diagnostics) text.Append("  power: ").Append(line).Append('\n');
            }
        }

        private static GridRect Expand(ProceduralLayout layout, TerrainSnapshot terrain, int margin)
        {
            GridRect b = layout.BoundingRect;
            IEnumerable<GridPoint> gateCells = layout.Gates.SelectMany(g => g.BandWalls.Concat(new[] { g.Staging })).Concat(layout.OuterWalls)
                .Concat(layout.FieldWall?.Walls ?? new GridPoint[0]).Concat(layout.ApproachDefense?.CoverCells ?? new GridPoint[0])
                .Concat(layout.Turret?.All.SelectMany(t => t.Footprint.Concat(t.CoverCells)) ?? new GridPoint[0]).ToList();
            int minX = Math.Min(b.MinX, gateCells.Select(c => c.X).DefaultIfEmpty(b.MinX).Min()) - margin;
            int minZ = Math.Min(b.MinZ, gateCells.Select(c => c.Z).DefaultIfEmpty(b.MinZ).Min()) - margin;
            int maxX = Math.Max(b.MaxX, gateCells.Select(c => c.X).DefaultIfEmpty(b.MaxX).Max()) + margin;
            int maxZ = Math.Max(b.MaxZ, gateCells.Select(c => c.Z).DefaultIfEmpty(b.MaxZ).Max()) + margin;
            minX = Math.Max(0, minX); minZ = Math.Max(0, minZ); maxX = Math.Min(terrain.Width - 1, maxX); maxZ = Math.Min(terrain.Height - 1, maxZ);
            return new GridRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        }

        private static char Terrain(TerrainCell cell)
        {
            if (cell.Has(TerrainTraits.NaturalRock)) return '^';
            if (cell.Has(TerrainTraits.Water)) return '~';
            if (cell.Has(TerrainTraits.Occupied)) return 'o';
            if (!cell.Has(TerrainTraits.Standable)) return '%';
            return cell.Has(TerrainTraits.Roofed) ? ':' : ' ';
        }

        private static char Symbol(LayoutFixtureKind kind)
        {
            switch (kind)
            {
                case LayoutFixtureKind.Bed: return 'B';
                case LayoutFixtureKind.PrisonBed: return 'P';
                case LayoutFixtureKind.MedicalBed: return 'H';
                case LayoutFixtureKind.Stove: return 'S';
                case LayoutFixtureKind.Workbench: return 'W';
                case LayoutFixtureKind.Table: return 'T';
                case LayoutFixtureKind.Chair: return 'c';
                case LayoutFixtureKind.Recreation: return 'R';
                case LayoutFixtureKind.Stockpile: return '_';
                case LayoutFixtureKind.Heater: return 'h';
                case LayoutFixtureKind.Campfire: return 'f';
                case LayoutFixtureKind.WallCooler: return 'C';
                case LayoutFixtureKind.WallVent: return 'v';
                case LayoutFixtureKind.PassiveCooler: return 'p';
                case LayoutFixtureKind.Lamp: return 'L';
                case LayoutFixtureKind.Torch: return 't';
                case LayoutFixtureKind.Generator: return 'E';
                case LayoutFixtureKind.Battery: return 'A';
                case LayoutFixtureKind.MedicalMonitor: return 'V';
                case LayoutFixtureKind.Shelf: return 'k';
                default: return 'M';
            }
        }
    }
}
