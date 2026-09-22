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
            LayoutTerrainGrid grid = LayoutTerrainGrid.Build(terrain, 0);
            if (layout.InputDigest != input.Digest) errors.Add("input_digest");

            HashSet<GridPoint> enclosure = new HashSet<GridPoint>(layout.Enclosure);
            HashSet<GridPoint> built = new HashSet<GridPoint>(layout.BuiltWalls);
            HashSet<GridPoint> natural = new HashSet<GridPoint>(layout.NaturalWalls);
            HashSet<GridPoint> gateCells = new HashSet<GridPoint>(layout.Gates.Select(g => g.Cell));
            HashSet<GridPoint> excavation = new HashSet<GridPoint>(layout.Excavation);
            HashSet<GridPoint> retained = new HashSet<GridPoint>(layout.RetainedThickRoof);
            HashSet<GridPoint> extra = new HashSet<GridPoint>(layout.ExtraObstacles);

            // Terrain legality and the excavation ledger.
            if (built.Any(c => grid.At(c) != LayoutCellClass.Open && grid.At(c) != LayoutCellClass.Covered)) errors.Add("built_wall_illegal_terrain");
            if (natural.Any(c => grid.At(c) != LayoutCellClass.Rock || excavation.Contains(c))) errors.Add("natural_wall_not_intact_rock");
            if (excavation.Any(c => grid.At(c) != LayoutCellClass.Rock)) errors.Add("excavation_not_natural_rock");
            if (excavation.Count > requirements.ExcavationBudgetCells) errors.Add("excavation_budget");
            if (layout.Doctrine == LayoutDoctrine.OpenGround && excavation.Count > 0) errors.Add("open_ground_excavated");
            if (retained.Any(c => !excavation.Contains(c) || !grid.Roofed(c)) || excavation.Any(c => grid.Roofed(c) && !retained.Contains(c)))
                errors.Add("retained_roof_ledger");
            if (LayoutRoofSupport.Unsupported(terrain, excavation, retained).Count > 0) errors.Add("roof_unsupported_without_built_walls");

            // Closed perimeter: every enclosure cell's cardinal neighbour is inside,
            // a blocking wall/rock, or a gate door. Nothing leaks to the exterior.
            HashSet<GridPoint> boundary = new HashSet<GridPoint>(built.Concat(natural).Concat(gateCells));
            if (enclosure.Any(c => Neighbors(c).Any(n => !enclosure.Contains(n) && !boundary.Contains(n)))) errors.Add("enclosure_not_closed");
            if (boundary.Any(enclosure.Contains)) errors.Add("wall_inside_enclosure");

            // Rooms and corridors.
            HashSet<GridPoint> roomCells = new HashSet<GridPoint>();
            foreach (LayoutRoom room in layout.Rooms)
                foreach (GridPoint c in room.Bounds.Points())
                    if (!roomCells.Add(c)) errors.Add("room_overlap_" + room.Id);
            HashSet<GridPoint> courtCells = new HashSet<GridPoint>(layout.Court == null ? Enumerable.Empty<GridPoint>() : layout.Court.Cells);
            foreach (LayoutCorridor corridor in layout.Corridors)
                if (corridor.Cells.Any(c => roomCells.Contains(c) || courtCells.Contains(c) || !enclosure.Contains(c))) errors.Add("corridor_overlap_" + corridor.Id);
            if (roomCells.Any(c => !enclosure.Contains(c)) || courtCells.Any(c => !enclosure.Contains(c) || roomCells.Contains(c))) errors.Add("footprint_outside_enclosure");

            Func<GridPoint, bool> walkableTerrain = c => grid.InBounds(c) && !extra.Contains(c) &&
                (excavation.Contains(c) || grid.At(c) == LayoutCellClass.Open || grid.At(c) == LayoutCellClass.Covered);
            Func<GridPoint, bool> isPublic = c => enclosure.Contains(c) && !roomCells.Contains(c) && !courtCells.Contains(c) && walkableTerrain(c);
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
            LayoutMaterialLedger materials = equipment.Count(layout.Rooms, layout.BuiltWalls, layout.Gates.Count);
            if (layout.Materials == null || layout.Materials.ToString() != materials.ToString()) errors.Add("materials_ledger_mismatch");
            if (materials.ImportValue > requirements.ImportValueLimit) errors.Add(LayoutEquipment.MaterialBudgetExceeded);

            foreach (LayoutRoom room in layout.Rooms) ValidateRoom(room, requirements, grid, excavation, retained, extra, reach, errors);

            if (layout.Court != null)
            {
                if (layout.Court.Cells.Any(c => grid.At(c) != LayoutCellClass.Open || !terrain[c].Has(TerrainTraits.SupportsHeavy)))
                    errors.Add("court_not_open_sky_heavy");
                if (!layout.Court.Access.All(reach.Contains)) errors.Add("court_access_disconnected");
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
                    if (!grid.Passable(route[k]) || enclosure.Contains(route[k]) || boundary.Contains(route[k])) { errors.Add(id + "_route_reenters_or_impassable"); break; }
                }
            }
            if (layout.Gates.Select(g => g.Cell).Distinct().Count() != layout.Gates.Count) errors.Add("duplicate_gate");

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
                if (realized == null || realized.Capacity < room.Capacity) errors.Add("capacity_" + room.Id);
            }
            if (layout.Rooms.Count != demand.Rooms.Count) errors.Add("room_count");
            int beds = layout.Rooms.Where(r => r.Kind == LayoutRoomKind.Barracks || r.Kind == LayoutRoomKind.Bedroom).Sum(r => r.Capacity);
            if (beds < requirements.Residents) errors.Add("housing_beds");
            if (kit.Powered)
            {
                int generators = fixtures.Count(f => f.Kind == LayoutFixtureKind.Generator);
                if (generators * kit.GeneratorWatts < fixtures.Sum(f => f.Watts)) errors.Add("power_generation_below_load");
                if (!fixtures.Any(f => f.Kind == LayoutFixtureKind.Battery)) errors.Add("power_battery");
            }
            else if (fixtures.Any(f => f.Watts > 0 || f.Kind == LayoutFixtureKind.Generator || f.Kind == LayoutFixtureKind.Battery))
                errors.Add("unpowered_electric_content");
            return new ReadOnlyCollection<string>(errors.Distinct().ToList());
        }

        private static void ValidateRoom(LayoutRoom room, LayoutRequirements requirements, LayoutTerrainGrid grid, HashSet<GridPoint> excavation,
            HashSet<GridPoint> retained, HashSet<GridPoint> extra, HashSet<GridPoint> reach, List<string> errors)
        {
            string id = room.Id;
            GridRect interior = room.Interior;
            if (interior.Width < LayoutDemandModel.MinInterior || interior.Height < LayoutDemandModel.MinInterior ||
                Math.Max(interior.Width, interior.Height) > LayoutDemandModel.MaxInterior) errors.Add("room_size_" + id);
            bool corner = (room.Door.X == room.Bounds.MinX || room.Door.X == room.Bounds.MaxX) && (room.Door.Z == room.Bounds.MinZ || room.Door.Z == room.Bounds.MaxZ);
            if (!room.IsEdge(room.Door) || corner) errors.Add("door_not_on_wall_" + id);
            if (!interior.Contains(new GridPoint(room.Door.X + room.Facing.X, room.Door.Z + room.Facing.Z)) || room.Bounds.Contains(room.DoorOutside))
                errors.Add("door_facing_" + id);
            if (extra.Contains(room.Door) || !reach.Contains(room.DoorOutside)) errors.Add("room_door_disconnected_" + id);
            if (room.Capacity < room.DemandedCapacity) errors.Add("room_capacity_" + id);
            foreach (GridPoint c in room.Bounds.Points())
            {
                LayoutCellClass cls = grid.At(c);
                bool edge = room.IsEdge(c);
                if (cls == LayoutCellClass.Bad) errors.Add("room_on_illegal_terrain_" + id);
                else if (cls == LayoutCellClass.Rock && !excavation.Contains(c) && (!edge || c.Equals(room.Door))) errors.Add("room_unexcavated_rock_" + id);
            }
            if (room.ProtectedByRock && interior.Points().Any(c => !retained.Contains(c) && grid.At(c) != LayoutCellClass.Covered)) errors.Add("protection_claim_" + id);

            HashSet<GridPoint> blocking = new HashSet<GridPoint>(room.Fixtures.Where(f => f.Blocking && f.Kind != LayoutFixtureKind.WallCooler).SelectMany(f => f.Cells));
            blocking.UnionWith(extra);
            HashSet<GridPoint> local = Flood(room.Door, c => c.Equals(room.Door) || (interior.Contains(c) && !blocking.Contains(c)));
            foreach (LayoutFixture fixture in room.Fixtures)
            {
                string label = id + "_" + fixture.Kind;
                if (fixture.Kind == LayoutFixtureKind.WallCooler)
                {
                    GridPoint cell = fixture.Cells[0];
                    bool coolerCorner = (cell.X == room.Bounds.MinX || cell.X == room.Bounds.MaxX) && (cell.Z == room.Bounds.MinZ || cell.Z == room.Bounds.MaxZ);
                    GridPoint exhaust = new GridPoint(cell.X - fixture.Facing.X, cell.Z - fixture.Facing.Z);
                    if (!room.IsEdge(cell) || coolerCorner || cell.Equals(room.Door)) errors.Add("cooler_not_in_wall_" + label);
                    if (!fixture.Exhaust.HasValue || !fixture.Exhaust.Value.Equals(exhaust) || !reach.Contains(exhaust) ||
                        grid.At(exhaust) != LayoutCellClass.Open || excavation.Contains(exhaust)) errors.Add("cooler_exhaust_not_open_public_" + label);
                }
                else if (fixture.Cells.Any(c => !interior.Contains(c))) errors.Add("fixture_outside_interior_" + label);
                if (fixture.Access.Any(c => blocking.Contains(c) || !local.Contains(c))) errors.Add("interaction_unreachable_" + label);
                if (fixture.Blocking && fixture.Kind != LayoutFixtureKind.WallCooler && !fixture.Cells.Any(c => Neighbors(c).Any(local.Contains)))
                    errors.Add("job_access_" + label);
                if (!fixture.Blocking && fixture.Cells.Any(c => !local.Contains(c))) errors.Add("zone_unreachable_" + label);
                if ((fixture.Kind == LayoutFixtureKind.Stove || fixture.Kind == LayoutFixtureKind.Workbench || fixture.Kind == LayoutFixtureKind.Generator) &&
                    fixture.Cells.Any(c => !excavation.Contains(c) && !grid.Terrain[c].Has(TerrainTraits.SupportsHeavy))) errors.Add("heavy_floor_" + label);
                if ((fixture.Kind == LayoutFixtureKind.Stove || fixture.Kind == LayoutFixtureKind.Workbench) && !BackToWall(room, fixture))
                    errors.Add("workbench_not_against_wall_" + label);
            }
            if (!room.Fixtures.Any(f => f.Role == SettlementRole.Light)) errors.Add("lighting_" + id);
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

        // The fixture's back row touches the wall and its interaction cell is in front, facing the room.
        private static bool BackToWall(LayoutRoom room, LayoutFixture fixture)
        {
            if (fixture.Access.Count != 1) return false;
            GridPoint back = new GridPoint(-fixture.Facing.X, -fixture.Facing.Z);
            bool touchesWall = fixture.Cells.All(c => room.IsEdge(new GridPoint(c.X + back.X, c.Z + back.Z)));
            GridPoint access = fixture.Access[0];
            return touchesWall && fixture.Cells.Any(c => new GridPoint(c.X + fixture.Facing.X, c.Z + fixture.Facing.Z).Equals(access));
        }

        public static IReadOnlyCollection<GridPoint> PublicCirculation(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            LayoutTerrainGrid grid = LayoutTerrainGrid.Build(input.Terrain, 0);
            HashSet<GridPoint> enclosure = new HashSet<GridPoint>(layout.Enclosure);
            HashSet<GridPoint> excavation = new HashSet<GridPoint>(layout.Excavation);
            HashSet<GridPoint> blocked = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Bounds.Points()).Concat(layout.ExtraObstacles)
                .Concat(layout.Court == null ? Enumerable.Empty<GridPoint>() : layout.Court.Cells));
            if (layout.Gates.Count == 0) return new GridPoint[0];
            return Flood(layout.Gates[0].Inside, c => enclosure.Contains(c) && !blocked.Contains(c) &&
                (excavation.Contains(c) || grid.At(c) == LayoutCellClass.Open || grid.At(c) == LayoutCellClass.Covered));
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
                "multiple_rooms_per_facility_kind_violate_P2_one_per_kind_rule"
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
                foreach (LayoutFixture fixture in room.Fixtures.Where(f => f.Kind != LayoutFixtureKind.Stockpile && f.Kind != LayoutFixtureKind.Chair))
                {
                    GridPoint anchor = fixture.Cells.OrderBy(c => ProceduralLayoutValidator.Neighbors(c)
                        .Any(n => fixture.Access.Contains(n) || (room.Interior.Contains(n) && !occupied.Contains(n))) ? 0 : 1).ThenBy(c => c).First();
                    placements.Add(new PhysicalPlacement(PlacementKindOf(fixture.Kind), fixture.DefName, string.Empty, anchor, fixture.Rotation,
                        room.Id, false, 0));
                }
            }
            if (layout.Court != null)
                foreach (GridPoint cell in layout.Court.Cells)
                    placements.Add(new PhysicalPlacement(PhysicalPlacementKind.Cover, "MortarCourtReservation", string.Empty, cell, "North", "court", false, 0));
            IReadOnlyCollection<GridPoint> publicCells = ProceduralLayoutValidator.PublicCirculation(input, layout);
            TerrainMutationFootprint mutations = new TerrainMutationFootprint(layout.Excavation, Enumerable.Empty<GridPoint>(), layout.ConstructedRoof,
                layout.NaturalWalls.Concat(layout.Rooms.SelectMany(r => r.NaturalWalls)));
            LayoutGate first = layout.Gates[0];
            LayoutGate second = layout.Gates.Count > 1 ? layout.Gates[1] : first;
            return new PhysicalSettlementPlan(layout.Digest, input.Requirements.Digest, compound, new GridRect(0, 0, 0, 0),
                layout.Gates.Select(g => g.Cell), layout.BuiltWalls.Concat(layout.NaturalWalls),
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
            "legend: '#' built wall  '^' intact rock  'G' gate  'D' door  '=' street  ',' public yard  '.' room floor  'i' interaction/access\n" +
            "        B bed  P prison bed  H medical bed  S stove  W workbench  T table  c chair  R recreation  _ stockpile  h heater\n" +
            "        f campfire  C wall cooler  p passive cooler  L lamp  t torch  E generator  A battery  M mortar court  V vitals monitor  k shelf\n" +
            "        g band door  | band wall  v outer vestibule  s staging  * exterior route  X injected obstacle\n" +
            "        outside: ' ' open ground  ':' roofed floor  '~' water  'o' occupied  '%' other unusable\n" +
            "roof view: 'R' retained thick rock roof  'r' constructed roof  '^' intact rock  ' ' open sky";

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
            foreach (GridPoint c in layout.Corridors.SelectMany(x => x.Cells)) map[c] = '=';
            foreach (GridPoint c in layout.BuiltWalls) map[c] = '#';
            foreach (GridPoint c in layout.NaturalWalls) map[c] = '^';
            foreach (LayoutRoom room in layout.Rooms)
            {
                foreach (GridPoint c in room.Bounds.Points()) map[c] = room.NaturalWalls.Contains(c) ? '^' : room.IsEdge(c) ? '#' : '.';
                map[room.Door] = 'D';
            }
            foreach (LayoutFixture fixture in layout.AllFixtures)
            {
                foreach (GridPoint c in fixture.Access) if (fixture.Kind != LayoutFixtureKind.Table) map[c] = 'i';
                foreach (GridPoint c in fixture.Cells) map[c] = Symbol(fixture.Kind);
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
            StringBuilder text = new StringBuilder();
            for (int z = rect.MaxZ; z >= rect.MinZ; z--)
            {
                for (int x = rect.MinX; x <= rect.MaxX; x++)
                {
                    GridPoint c = new GridPoint(x, z);
                    TerrainCell cell = terrain[c];
                    text.Append(retained.Contains(c) ? 'R' : constructed.Contains(c) ? 'r' : cell.Has(TerrainTraits.NaturalRock) && !dug.Contains(c) ? '^' :
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
        }

        private static GridRect Expand(ProceduralLayout layout, TerrainSnapshot terrain, int margin)
        {
            GridRect b = layout.BoundingRect;
            IEnumerable<GridPoint> gateCells = layout.Gates.SelectMany(g => g.BandWalls.Concat(new[] { g.Staging }));
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
