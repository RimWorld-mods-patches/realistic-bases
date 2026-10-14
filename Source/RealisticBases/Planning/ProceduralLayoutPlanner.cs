using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class LayoutDiagnostic
    {
        public LayoutDiagnostic(string stage, string code, string detail)
        {
            Stage = stage ?? string.Empty;
            Code = code ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public string Stage { get; }
        public string Code { get; }
        public string Detail { get; }
        public override string ToString() => Stage + ":" + Code + (Detail.Length == 0 ? string.Empty : " " + Detail);
    }

    public sealed class LayoutSearchStats
    {
        internal int SitesTried;
        internal int Attempts;
        internal int Evaluations;
        internal int Repairs;
        internal int RouteNodes;
        internal bool BudgetExhausted;

        public int SitesTriedCount => SitesTried;
        public int AttemptCount => Attempts;
        public int EvaluationCount => Evaluations;
        public int RepairCount => Repairs;
        public int RouteNodeCount => RouteNodes;
        public bool IsBudgetExhausted => BudgetExhausted;
        public override string ToString() => "sites=" + SitesTried + " attempts=" + Attempts + " evaluations=" + Evaluations +
            " repairs=" + Repairs + " routeNodes=" + RouteNodes + " budgetExhausted=" + BudgetExhausted;
    }

    public sealed class LayoutFixture
    {
        internal LayoutFixture(LayoutFixtureKind kind, string defName, IEnumerable<GridPoint> cells, IEnumerable<GridPoint> access,
            GridPoint facing, bool blocking, int watts, bool needsFuel, string roomId, GridPoint? exhaust = null,
            SettlementRole? role = null, string stuff = null)
        {
            Role = role;
            Stuff = stuff;
            Kind = kind;
            DefName = defName;
            Cells = new ReadOnlyCollection<GridPoint>(cells.ToList());
            Access = new ReadOnlyCollection<GridPoint>((access ?? Enumerable.Empty<GridPoint>()).ToList());
            Facing = facing;
            Blocking = blocking;
            Watts = watts;
            NeedsFuel = needsFuel;
            RoomId = roomId ?? string.Empty;
            Exhaust = exhaust;
        }

        public LayoutFixtureKind Kind { get; }
        public string DefName { get; }
        public IReadOnlyList<GridPoint> Cells { get; }
        public IReadOnlyList<GridPoint> Access { get; }
        public GridPoint Facing { get; }
        public bool Blocking { get; }
        public int Watts { get; }
        public bool NeedsFuel { get; }
        public string RoomId { get; }
        public GridPoint? Exhaust { get; }
        // Equipment role, or null (generators, batteries, the court reservation).
        public SettlementRole? Role { get; }
        // Chosen stuff for stuffable defs (WoodLog or a stone block def), otherwise null.
        public string Stuff { get; }

        // Wall coolers only. Vanilla Building_Cooler cools Position + South.RotatedBy(rot)
        // and heats Position + North.RotatedBy(rot); Facing points into the cooled room.
        public GridPoint? ColdCell => LayoutFixtureKinds.ThroughWall(Kind)
            ? new GridPoint(Cells[0].X + Facing.X, Cells[0].Z + Facing.Z) : (GridPoint?)null;
        public GridPoint? HotCell => LayoutFixtureKinds.ThroughWall(Kind)
            ? new GridPoint(Cells[0].X - Facing.X, Cells[0].Z - Facing.Z) : (GridPoint?)null;

        // Vanilla rotation convention. Benches and campfires use interactionCellOffset
        // (0,0,-1): North puts the work cell south. Beds point their head at the wall.
        // A cooler's rotation names its hot side, so it points away from the cooled room.
        // Shelves show their open front in their rotation, so they face the room.
        public string Rotation
        {
            get
            {
                GridPoint d = Kind == LayoutFixtureKind.Shelf ? Facing : new GridPoint(-Facing.X, -Facing.Z);
                if (d.Z > 0) return "North";
                if (d.Z < 0) return "South";
                return d.X > 0 ? "East" : "West";
            }
        }
    }

    public sealed class LayoutRoom
    {
        internal LayoutRoom(LayoutRoomDemand demand, GridRect bounds, GridPoint door, GridPoint facing, int capacity,
            IEnumerable<LayoutFixture> fixtures, bool protectedByRock, IEnumerable<GridPoint> naturalWalls,
            IEnumerable<GridPoint> sideDoors = null)
        {
            Id = demand.Id;
            Kind = demand.Kind;
            DemandedCapacity = demand.Capacity;
            Unit = demand.Unit;
            Bounds = bounds;
            Door = door;
            Facing = facing;
            Capacity = capacity;
            Fixtures = new ReadOnlyCollection<LayoutFixture>(fixtures.ToList());
            ProtectedByRock = protectedByRock;
            NaturalWalls = new ReadOnlyCollection<GridPoint>(naturalWalls.OrderBy(c => c).ToList());
            SideDoors = new ReadOnlyCollection<GridPoint>((sideDoors ?? Enumerable.Empty<GridPoint>()).OrderBy(c => c).ToList());
            demandSource = demand;
        }

        private readonly LayoutRoomDemand demandSource;

        internal LayoutRoom WithSideDoors(IEnumerable<GridPoint> sideDoors) =>
            new LayoutRoom(demandSource, Bounds, Door, Facing, Capacity, Fixtures, ProtectedByRock, NaturalWalls, sideDoors);

        public string Id { get; }
        public LayoutRoomKind Kind { get; }
        public int DemandedCapacity { get; }
        public string Unit { get; }
        // Bounds include the walls, as in P2.
        public GridRect Bounds { get; }
        public GridPoint Door { get; }
        // Unit vector from the public side into the room.
        public GridPoint Facing { get; }
        public GridPoint DoorOutside => new GridPoint(Door.X - Facing.X, Door.Z - Facing.Z);
        public int Capacity { get; }
        public IReadOnlyList<LayoutFixture> Fixtures { get; }
        public bool ProtectedByRock { get; }
        public IReadOnlyList<GridPoint> NaturalWalls { get; }
        public GridRect Interior => new GridRect(Bounds.MinX + 1, Bounds.MinZ + 1, Bounds.Width - 2, Bounds.Height - 2);
        public bool IsEdge(GridPoint c) => Bounds.Contains(c) && (c.X == Bounds.MinX || c.X == Bounds.MaxX || c.Z == Bounds.MinZ || c.Z == Bounds.MaxZ);
        // Further doors of a dining hall onto the corridor loop around it.
        public IReadOnlyList<GridPoint> SideDoors { get; }
        public IEnumerable<GridPoint> Doors => new[] { Door }.Concat(SideDoors);
        public bool IsDoor(GridPoint c) => c.Equals(Door) || SideDoors.Contains(c);
        // The public cell in front of a door cell on this room's wall.
        public GridPoint OutsideOf(GridPoint door) =>
            door.X == Bounds.MinX ? new GridPoint(door.X - 1, door.Z) : door.X == Bounds.MaxX ? new GridPoint(door.X + 1, door.Z) :
            door.Z == Bounds.MinZ ? new GridPoint(door.X, door.Z - 1) : new GridPoint(door.X, door.Z + 1);
    }

    public sealed class LayoutCorridor
    {
        internal LayoutCorridor(string id, bool main, IEnumerable<GridPoint> cells)
        {
            Id = id;
            Main = main;
            Cells = new ReadOnlyCollection<GridPoint>(cells.ToList());
        }

        public string Id { get; }
        public bool Main { get; }
        public IReadOnlyList<GridPoint> Cells { get; }
    }

    public sealed class LayoutGate
    {
        internal LayoutGate(GridPoint cell, GridPoint inside, GridPoint outward, IEnumerable<GridPoint> bandDoors,
            IEnumerable<GridPoint> bandWalls, IEnumerable<GridPoint> outerVestibule, GridPoint staging,
            IEnumerable<GridPoint> exteriorRoute, IEnumerable<GridPoint> innerVestibule, bool rockFace)
        {
            Cell = cell;
            Inside = inside;
            Outward = outward;
            BandDoors = new ReadOnlyCollection<GridPoint>(bandDoors.ToList());
            BandWalls = new ReadOnlyCollection<GridPoint>(bandWalls.ToList());
            OuterVestibule = new ReadOnlyCollection<GridPoint>(outerVestibule.ToList());
            Staging = staging;
            ExteriorRoute = new ReadOnlyCollection<GridPoint>(exteriorRoute.ToList());
            InnerVestibule = new ReadOnlyCollection<GridPoint>(innerVestibule.ToList());
            RockFace = rockFace;
        }

        public GridPoint Cell { get; }
        public GridPoint Inside { get; }
        public GridPoint Outward { get; }
        // P4 gatehouse crossing reservation: one aligned door per extra layer,
        // four band walls each, and the one-cell vestibules between bands.
        public IReadOnlyList<GridPoint> BandDoors { get; }
        public IReadOnlyList<GridPoint> BandWalls { get; }
        public IReadOnlyList<GridPoint> OuterVestibule { get; }
        public GridPoint Staging { get; }
        public IReadOnlyList<GridPoint> ExteriorRoute { get; }
        public IReadOnlyList<GridPoint> InnerVestibule { get; }
        public bool RockFace { get; }
    }

    // A gate-adjacent reinforcement section with both flanks returned to the
    // baseline wall or preceding layer. It is a closed gatehouse surround,
    // not a complete second shell around the whole compound.
    public sealed class LayoutWallSection
    {
        internal LayoutWallSection(int gateIndex, int layer, IEnumerable<GridPoint> left, IEnumerable<GridPoint> right,
            IEnumerable<GridPoint> leftReturn, IEnumerable<GridPoint> rightReturn)
        {
            GateIndex = gateIndex;
            Layer = layer;
            Left = new ReadOnlyCollection<GridPoint>(left.ToList());
            Right = new ReadOnlyCollection<GridPoint>(right.ToList());
            LeftReturn = new ReadOnlyCollection<GridPoint>(leftReturn.ToList());
            RightReturn = new ReadOnlyCollection<GridPoint>(rightReturn.ToList());
        }

        public int GateIndex { get; }
        // 1 is the first wall outside the baseline enclosure, at offset 2.
        public int Layer { get; }
        public IReadOnlyList<GridPoint> Left { get; }
        public IReadOnlyList<GridPoint> Right { get; }
        public IReadOnlyList<GridPoint> LeftReturn { get; }
        public IReadOnlyList<GridPoint> RightReturn { get; }
        public IEnumerable<GridPoint> Cells => Left.Concat(Right).Concat(LeftReturn).Concat(RightReturn);

        // Walkable strip immediately inside this wall layer, ending just
        // before the return cap. The zero-distance crossing is shared.
        public IEnumerable<GridPoint> ServiceLane(LayoutGate gate)
        {
            GridPoint tangent = new GridPoint(gate.Outward.Z, gate.Outward.X);
            foreach (int side in new[] { -1, 1 })
            {
                int count = side < 0 ? Left.Count : Right.Count;
                if (count == 0) continue;
                for (int distance = 0; distance <= count + 1; distance++)
                    yield return new GridPoint(gate.Cell.X + distance * side * tangent.X + (2 * Layer - 1) * gate.Outward.X,
                        gate.Cell.Z + distance * side * tangent.Z + (2 * Layer - 1) * gate.Outward.Z);
            }
        }

        public IEnumerable<GridPoint> ServiceTermini(LayoutGate gate)
        {
            GridPoint tangent = new GridPoint(gate.Outward.Z, gate.Outward.X);
            foreach (int side in new[] { -1, 1 })
            {
                int count = side < 0 ? Left.Count : Right.Count;
                if (count > 0)
                    yield return new GridPoint(gate.Cell.X + (count + 1) * side * tangent.X + (2 * Layer - 1) * gate.Outward.X,
                        gate.Cell.Z + (count + 1) * side * tangent.Z + (2 * Layer - 1) * gate.Outward.Z);
            }
        }
    }

    public enum LayoutPortalKind
    {
        Gate,
        RoomDoor
    }

    public sealed class LayoutPortal
    {
        internal LayoutPortal(LayoutPortalKind kind, GridPoint cell, GridPoint publicSide, string owner)
        {
            Kind = kind;
            Cell = cell;
            PublicSide = publicSide;
            Owner = owner;
        }

        public LayoutPortalKind Kind { get; }
        public GridPoint Cell { get; }
        public GridPoint PublicSide { get; }
        public string Owner { get; }
    }

    // A sealed, doorless, unroofed cell that takes cooler exhaust (vanilla hot side).
    // Walls lists only the pocket walls built for it; other sides are room walls or rock.
    public sealed class LayoutExhaustPocket
    {
        internal LayoutExhaustPocket(GridPoint cell, IEnumerable<GridPoint> walls, bool roofCleared, bool excavated, IEnumerable<string> roomIds)
        {
            Cell = cell;
            Walls = new ReadOnlyCollection<GridPoint>(walls.Distinct().OrderBy(c => c).ToList());
            RoofCleared = roofCleared;
            Excavated = excavated;
            RoomIds = new ReadOnlyCollection<string>(roomIds.Distinct().OrderBy(r => r, StringComparer.Ordinal).ToList());
        }

        public GridPoint Cell { get; }
        public IReadOnlyList<GridPoint> Walls { get; }
        public bool RoofCleared { get; }
        public bool Excavated { get; }
        public IReadOnlyList<string> RoomIds { get; }
    }

    public sealed class ProceduralLayout
    {
        internal ProceduralLayout(string inputDigest, ulong seed, LayoutDoctrine doctrine, bool mortarCapable, GridPoint anchor,
            IEnumerable<LayoutRoom> rooms, IEnumerable<LayoutCorridor> corridors, LayoutFixture court,
            IEnumerable<GridPoint> enclosure, IEnumerable<GridPoint> builtWalls, IEnumerable<GridPoint> naturalWalls,
            IEnumerable<LayoutGate> gates, IEnumerable<GridPoint> excavation, IEnumerable<GridPoint> retainedRoof,
            IEnumerable<GridPoint> constructedRoof, int wallLayers, int fieldCells, IEnumerable<LayoutRelationship> relationships,
            IEnumerable<GridPoint> extraObstacles, IEnumerable<string> notes, SettlementTechStage techStage, string rolesDigest,
            string equipmentDigest, string wallStuff, LayoutMaterialLedger materials, LayoutPowerPlan power,
            IEnumerable<LayoutExhaustPocket> exhaustPockets = null, IEnumerable<LayoutWallSection> outerSections = null,
            P4BJointSite jointSite = null, P4BFieldWallPlan fieldWall = null, P4BTurretPlan turret = null,
            P4BApproachDefensePlan approachDefense = null, IEnumerable<GridPoint> perimeterReinforcement = null,
            IEnumerable<LayoutFixture> courts = null, IEnumerable<GridPoint> bonusFields = null)
        {
            BonusFields = Sorted(bonusFields);
            JointSite = jointSite;
            FieldWall = fieldWall;
            Turret = turret;
            ApproachDefense = approachDefense;
            PerimeterReinforcement = Sorted(perimeterReinforcement);
            Power = power;
            OuterSections = new ReadOnlyCollection<LayoutWallSection>((outerSections ?? Enumerable.Empty<LayoutWallSection>())
                .OrderBy(s => s.GateIndex).ThenBy(s => s.Layer).ToList());
            ExhaustPockets = new ReadOnlyCollection<LayoutExhaustPocket>((exhaustPockets ?? Enumerable.Empty<LayoutExhaustPocket>())
                .OrderBy(p => p.Cell).ToList());
            ExhaustRoofClearance = Sorted(ExhaustPockets.Where(p => p.RoofCleared).Select(p => p.Cell));
            TechStage = techStage;
            RolesDigest = rolesDigest ?? string.Empty;
            EquipmentDigest = equipmentDigest ?? string.Empty;
            WallStuff = wallStuff ?? string.Empty;
            Materials = materials;
            InputDigest = inputDigest;
            Seed = seed;
            Doctrine = doctrine;
            MortarCapable = mortarCapable;
            Anchor = anchor;
            Rooms = new ReadOnlyCollection<LayoutRoom>(rooms.OrderBy(r => r.Id, StringComparer.Ordinal).ToList());
            Corridors = new ReadOnlyCollection<LayoutCorridor>(corridors.ToList());
            Courts = new ReadOnlyCollection<LayoutFixture>((courts ?? (court == null ? Enumerable.Empty<LayoutFixture>() : new[] { court })).ToList());
            Enclosure = Sorted(enclosure);
            BuiltWalls = Sorted(builtWalls);
            NaturalWalls = Sorted(naturalWalls);
            Gates = new ReadOnlyCollection<LayoutGate>(gates.ToList());
            Excavation = Sorted(excavation);
            RetainedThickRoof = Sorted(retainedRoof);
            ConstructedRoof = Sorted(constructedRoof);
            WallLayers = wallLayers;
            FieldCellsRequired = fieldCells;
            Relationships = new ReadOnlyCollection<LayoutRelationship>(relationships.ToList());
            ExtraObstacles = Sorted(extraObstacles);
            Notes = new ReadOnlyCollection<string>(notes.ToList());
            Digest = ComputeDigest();
        }

        public string InputDigest { get; }
        public ulong Seed { get; }
        public LayoutDoctrine Doctrine { get; }
        public bool MortarCapable { get; }
        public GridPoint Anchor { get; }
        public IReadOnlyList<LayoutRoom> Rooms { get; }
        public IReadOnlyList<LayoutCorridor> Corridors { get; }
        // Unroofed P3 mortar court reservation (footprint blocks, access on a street); null when dropped.
        public LayoutFixture Court => Courts.FirstOrDefault(); // Legacy single-gun API.
        public IReadOnlyList<LayoutFixture> Courts { get; }
        public LayoutRoom MagazineFor(LayoutFixture gun) => Rooms.Single(r => r.Kind == LayoutRoomKind.MortarMagazine && r.Id == gun.RoomId);
        public IReadOnlyList<GridPoint> Enclosure { get; }
        public IReadOnlyList<GridPoint> BuiltWalls { get; }
        public IReadOnlyList<LayoutWallSection> OuterSections { get; }
        public IReadOnlyList<GridPoint> PerimeterReinforcement { get; }
        public IEnumerable<GridPoint> OuterWalls => OuterSections.SelectMany(s => s.Cells).Concat(PerimeterReinforcement).Distinct();
        public IReadOnlyList<GridPoint> NaturalWalls { get; }
        public IReadOnlyList<LayoutGate> Gates { get; }
        public IReadOnlyList<GridPoint> Excavation { get; }
        // P4A ledger extension: excavated cells that keep their natural thick roof.
        public IReadOnlyList<GridPoint> RetainedThickRoof { get; }
        public IReadOnlyList<GridPoint> ConstructedRoof { get; }
        public int WallLayers { get; }
        // Unresolved exterior interface: P4 integration must place a fertile field reachable from a gate.
        public int FieldCellsRequired { get; }
        // Explicit field/compound reservation in joint-site mode; null for
        // legacy pure-layout fixtures and supported no-outdoor-field profiles.
        public P4BJointSite JointSite { get; }
        public P4BFieldWallPlan FieldWall { get; }
        public P4BTurretPlan Turret { get; }
        public P4BApproachDefensePlan ApproachDefense { get; }
        public IReadOnlyList<LayoutRelationship> Relationships { get; }
        // Optional extra crops on enclosed, unroofed, fertile yard left over by
        // the final layout. Never counted toward FieldCellsRequired.
        public IReadOnlyList<GridPoint> BonusFields { get; }
        // Test-only planned blockers used to prove the validator sees final obstacles.
        public IReadOnlyList<GridPoint> ExtraObstacles { get; }
        public IReadOnlyList<string> Notes { get; }
        // Final tech stage the kit was resolved at (after step-downs and adaptations).
        public SettlementTechStage TechStage { get; }
        public string RolesDigest { get; }
        public string EquipmentDigest { get; }
        // Stuff of room walls, the outline wall and doors.
        public string WallStuff { get; }
        // Exact construction and fuel accounting of this layout.
        public LayoutMaterialLedger Materials { get; }
        // Power mix (null when the kit is unpowered).
        public LayoutPowerPlan Power { get; }
        // Cooler exhaust pockets and the roof cells cleared over them (roof-clearance ledger).
        public IReadOnlyList<LayoutExhaustPocket> ExhaustPockets { get; }
        public IReadOnlyList<GridPoint> ExhaustRoofClearance { get; }
        public IEnumerable<GridPoint> ExhaustPocketWalls => ExhaustPockets.SelectMany(p => p.Walls);
        public IReadOnlyList<LayoutPowerYard> PowerYards => Power == null ? (IReadOnlyList<LayoutPowerYard>)new LayoutPowerYard[0] : Power.Yards;
        public string Digest { get; }

        public IEnumerable<LayoutFixture> AllFixtures => Rooms.SelectMany(r => r.Fixtures).Concat(Courts);

        public IReadOnlyList<LayoutPortal> Portals => new ReadOnlyCollection<LayoutPortal>(
            Gates.Select((g, i) => new LayoutPortal(LayoutPortalKind.Gate, g.Cell, g.Inside, "gate-" + i))
                .Concat(Rooms.Select(r => new LayoutPortal(LayoutPortalKind.RoomDoor, r.Door, r.DoorOutside, r.Id)))
                .Concat(Rooms.SelectMany(r => r.SideDoors.Select(d => new LayoutPortal(LayoutPortalKind.RoomDoor, d, r.OutsideOf(d), r.Id)))).ToList());

        public GridRect BoundingRect
        {
            get
            {
                IEnumerable<GridPoint> cells = Enclosure.Concat(BuiltWalls).Concat(NaturalWalls).Concat(Gates.Select(g => g.Cell));
                int minX = cells.Min(c => c.X), minZ = cells.Min(c => c.Z), maxX = cells.Max(c => c.X), maxZ = cells.Max(c => c.Z);
                return new GridRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
            }
        }

        public ProceduralLayout WithAddedObstacles(IEnumerable<GridPoint> obstacles) =>
            new ProceduralLayout(InputDigest, Seed, Doctrine, MortarCapable, Anchor, Rooms, Corridors, Court, Enclosure, BuiltWalls,
                NaturalWalls, Gates, Excavation, RetainedThickRoof, ConstructedRoof, WallLayers, FieldCellsRequired, Relationships,
                ExtraObstacles.Concat(obstacles ?? Enumerable.Empty<GridPoint>()), Notes, TechStage, RolesDigest, EquipmentDigest, WallStuff, Materials, Power,
                ExhaustPockets, OuterSections, JointSite, FieldWall, Turret, ApproachDefense, PerimeterReinforcement, Courts, BonusFields);

        public ProceduralLayout WithBonusFields(IEnumerable<GridPoint> cells) =>
            new ProceduralLayout(InputDigest, Seed, Doctrine, MortarCapable, Anchor, Rooms, Corridors, Court, Enclosure, BuiltWalls,
                NaturalWalls, Gates, Excavation, RetainedThickRoof, ConstructedRoof, WallLayers, FieldCellsRequired, Relationships,
                ExtraObstacles, Notes, TechStage, RolesDigest, EquipmentDigest, WallStuff, Materials, Power,
                ExhaustPockets, OuterSections, JointSite, FieldWall, Turret, ApproachDefense, PerimeterReinforcement, Courts, cells);

        public ProceduralLayout WithFieldWall(ProceduralLayoutInput input, P4BFieldWallPlan fieldWall)
        {
            if (input == null || fieldWall == null) throw new ArgumentNullException(nameof(fieldWall));
            List<LayoutGate> gates = Gates.Select((g, index) => index == fieldWall.GateIndex
                ? new LayoutGate(g.Cell, g.Inside, g.Outward, g.BandDoors, g.BandWalls, g.OuterVestibule,
                    g.Staging, fieldWall.ArrivalRoute, g.InnerVestibule, g.RockFace)
                : g).ToList();
            LayoutMaterialLedger materials = input.Equipment.Count(Rooms,
                BuiltWalls.Concat(gates.SelectMany(g => g.BandWalls)).Concat(OuterWalls)
                    .Concat(ExhaustPocketWalls).Concat(fieldWall.Walls),
                gates.Sum(g => 1 + g.BandDoors.Count) + 2 + (ApproachDefense?.SallyPorts.Count ?? 0), Power);
            return new ProceduralLayout(InputDigest, Seed, Doctrine, MortarCapable, Anchor, Rooms, Corridors, Court,
                Enclosure, BuiltWalls, NaturalWalls, gates, Excavation, RetainedThickRoof, ConstructedRoof,
                WallLayers, FieldCellsRequired, Relationships, ExtraObstacles,
                Notes.Concat(new[] { "fieldWall=" + fieldWall.Digest + " materials " + materials }),
                TechStage, RolesDigest, EquipmentDigest, WallStuff, materials, Power,
                ExhaustPockets, OuterSections, JointSite, fieldWall, Turret, ApproachDefense, PerimeterReinforcement, Courts, BonusFields);
        }

        public ProceduralLayout WithApproachDefense(ProceduralLayoutInput input, P4BApproachDefensePlan defense)
        {
            if (input == null || defense == null || Turret != null) throw new ArgumentException("Defense precedes turret selection");
            LayoutMaterialLedger materials = P4BApproachDefensePlanner.Materials(input, this,
                defense.Posts, defense.SallyPorts);
            P4BFieldWallPlan fieldWall = FieldWall?.WithoutWalls(defense.SallyPorts.Where(p => p.Field).Select(p => p.Cell));
            return new ProceduralLayout(InputDigest, Seed, Doctrine, MortarCapable, Anchor, Rooms, Corridors, Court,
                Enclosure, BuiltWalls.Except(defense.SallyPorts.Select(p => p.Cell)).Concat(defense.WallReturns),
                NaturalWalls, Gates, Excavation, RetainedThickRoof, ConstructedRoof,
                WallLayers, FieldCellsRequired, Relationships, ExtraObstacles,
                Notes.Concat(new[] { "approachDefense=" + defense.Digest }), TechStage, RolesDigest,
                EquipmentDigest, WallStuff, materials, Power, ExhaustPockets, OuterSections, JointSite, fieldWall, null, defense, PerimeterReinforcement, Courts, BonusFields);
        }

        public ProceduralLayout WithTurret(ProceduralLayoutInput input, P4BTurretPlan turret)
        {
            // A quest camp has no field wall; its guns stand at the camp's sides.
            if (input == null || turret == null || FieldWall == null && input.QuestContract == null)
                throw new ArgumentNullException(nameof(turret));
            LayoutMaterialLedger materials = input.Equipment.Count(Rooms,
                BuiltWalls.Concat(Gates.SelectMany(g => g.BandWalls)).Concat(OuterWalls)
                    .Concat(ExhaustPocketWalls).Concat(FieldWall?.Walls ?? new GridPoint[0]),
                Gates.Sum(g => 1 + g.BandDoors.Count) + (FieldWall == null ? 0 : 2) +
                    (ApproachDefense?.SallyPorts.Count ?? 0), Power,
                Enumerable.Repeat(P4BCoverNest.CoverDefName(input), turret.All.Sum(t => t.CoverCells.Count))
                    .Concat(turret.All.Select(t => t.DefName)));
            return new ProceduralLayout(InputDigest, Seed, Doctrine, MortarCapable, Anchor, Rooms, Corridors, Court,
                Enclosure, BuiltWalls, NaturalWalls, Gates, Excavation, RetainedThickRoof, ConstructedRoof,
                WallLayers, FieldCellsRequired, Relationships, ExtraObstacles,
                Notes.Concat(new[] { "turret=" + turret.Digest + " import=" + turret.ImportValue }),
                TechStage, RolesDigest, EquipmentDigest, WallStuff, materials, Power,
                ExhaustPockets, OuterSections, JointSite, FieldWall, turret,
                ApproachDefense?.WithoutTransit(turret.All.SelectMany(t => t.Footprint)), PerimeterReinforcement, Courts, BonusFields);
        }

        private static IReadOnlyList<GridPoint> Sorted(IEnumerable<GridPoint> cells) =>
            new ReadOnlyCollection<GridPoint>((cells ?? Enumerable.Empty<GridPoint>()).Distinct().OrderBy(c => c).ToList());

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-layout");
            hash = StableHash.Add(hash, InputDigest);
            hash = StableHash.Add(hash, Seed);
            hash = StableHash.Add(hash, (int)Doctrine);
            hash = Point(hash, Anchor);
            foreach (LayoutRoom room in Rooms)
            {
                hash = StableHash.Add(hash, room.Id);
                hash = StableHash.Add(hash, room.Bounds.MinX);
                hash = StableHash.Add(hash, room.Bounds.MinZ);
                hash = StableHash.Add(hash, room.Bounds.Width);
                hash = StableHash.Add(hash, room.Bounds.Height);
                hash = Point(hash, room.Door);
                foreach (GridPoint door in room.SideDoors) hash = Point(hash, door);
                hash = StableHash.Add(hash, room.Capacity);
                foreach (LayoutFixture fixture in room.Fixtures) hash = Fixture(hash, fixture);
            }
            foreach (LayoutCorridor corridor in Corridors)
            {
                hash = StableHash.Add(hash, corridor.Id);
                foreach (GridPoint c in corridor.Cells) hash = Point(hash, c);
            }
            foreach (LayoutFixture gun in Courts) hash = Fixture(hash, gun);
            foreach (IReadOnlyList<GridPoint> list in new[] { Enclosure, BuiltWalls, NaturalWalls, Excavation, RetainedThickRoof, ConstructedRoof, ExtraObstacles, PerimeterReinforcement })
            {
                hash = StableHash.Add(hash, list.Count);
                foreach (GridPoint c in list) hash = Point(hash, c);
            }
            foreach (LayoutGate gate in Gates)
            {
                hash = Point(hash, gate.Cell);
                hash = Point(hash, gate.Inside);
                foreach (GridPoint c in gate.BandDoors.Concat(gate.BandWalls).Concat(gate.ExteriorRoute)) hash = Point(hash, c);
            }
            foreach (LayoutWallSection section in OuterSections)
            {
                hash = StableHash.Add(hash, section.GateIndex);
                hash = StableHash.Add(hash, section.Layer);
                foreach (GridPoint c in section.Left) hash = Point(hash, c);
                foreach (GridPoint c in section.Right) hash = Point(hash, c);
                foreach (GridPoint c in section.LeftReturn) hash = Point(hash, c);
                foreach (GridPoint c in section.RightReturn) hash = Point(hash, c);
            }
            hash = StableHash.Add(hash, WallLayers);
            hash = StableHash.Add(hash, JointSite == null ? string.Empty : JointSite.Digest);
            hash = StableHash.Add(hash, FieldWall == null ? string.Empty : FieldWall.Digest);
            hash = StableHash.Add(hash, ApproachDefense == null ? string.Empty : ApproachDefense.Digest);
            hash = StableHash.Add(hash, Turret == null ? string.Empty : Turret.Digest);
            hash = StableHash.Add(hash, TechStage == null ? string.Empty : TechStage.Digest);
            hash = StableHash.Add(hash, RolesDigest);
            hash = StableHash.Add(hash, EquipmentDigest);
            hash = StableHash.Add(hash, WallStuff);
            if (Materials != null) hash = Materials.AddTo(hash);
            hash = StableHash.Add(hash, Power == null ? string.Empty : Power.Digest);
            if (BonusFields.Count > 0)
            {
                hash = StableHash.Add(hash, "bonus-fields");
                foreach (GridPoint c in BonusFields) hash = Point(hash, c);
            }
            foreach (LayoutExhaustPocket pocket in ExhaustPockets)
            {
                hash = Point(hash, pocket.Cell);
                hash = StableHash.Add(hash, (pocket.RoofCleared ? 1 : 0) + (pocket.Excavated ? 2 : 0));
                foreach (GridPoint c in pocket.Walls) hash = Point(hash, c);
                foreach (string id in pocket.RoomIds) hash = StableHash.Add(hash, id);
            }
            return StableHash.Hex(hash);
        }

        private static ulong Fixture(ulong hash, LayoutFixture fixture)
        {
            hash = StableHash.Add(hash, (int)fixture.Kind);
            hash = StableHash.Add(hash, fixture.DefName);
            hash = StableHash.Add(hash, fixture.Role.HasValue ? (int)fixture.Role.Value : -1);
            hash = StableHash.Add(hash, fixture.Stuff ?? string.Empty);
            foreach (GridPoint c in fixture.Cells) hash = Point(hash, c);
            foreach (GridPoint c in fixture.Access) hash = Point(hash, c);
            return Point(hash, fixture.Facing);
        }

        private static ulong Point(ulong hash, GridPoint p) => StableHash.Add(StableHash.Add(hash, p.X), p.Z);
    }

    public sealed class ProceduralLayoutResult
    {
        internal ProceduralLayoutResult(ProceduralLayout layout, LayoutDemandSet demand, IEnumerable<LayoutDiagnostic> diagnostics, LayoutSearchStats stats)
        {
            Layout = layout;
            Demand = demand;
            Diagnostics = new ReadOnlyCollection<LayoutDiagnostic>(diagnostics.ToList());
            Stats = stats;
        }

        public ProceduralLayout Layout { get; }
        public LayoutDemandSet Demand { get; }
        public IReadOnlyList<LayoutDiagnostic> Diagnostics { get; }
        public LayoutSearchStats Stats { get; }
        public bool Success => Layout != null;
        public string RejectionCode => Success ? null : Diagnostics.Where(d => d.Stage == "result").Select(d => d.Code).LastOrDefault();
    }

    internal enum LayoutCellClass : byte
    {
        Bad,
        Open,
        Covered,
        Rock
    }

    // Ordinary-land usable mask plus diggable natural rock. Water, occupied
    // non-rock cells and map edges are never used or assumed traversable.
    internal sealed class LayoutTerrainGrid
    {
        public readonly TerrainSnapshot Terrain;
        public readonly int Width;
        public readonly int Height;
        public readonly LayoutCellClass[] Class;
        public readonly bool[] Placeable;
        public readonly int[] OpenClearance;

        private LayoutTerrainGrid(TerrainSnapshot terrain, int margin, IEnumerable<GridPoint> openOverrides)
        {
            HashSet<GridPoint> overrides = new HashSet<GridPoint>(openOverrides ?? Enumerable.Empty<GridPoint>());
            Terrain = terrain;
            Width = terrain.Width;
            Height = terrain.Height;
            Class = new LayoutCellClass[Width * Height];
            Placeable = new bool[Width * Height];
            int[] distance = new int[Width * Height];
            Queue<int> queue = new Queue<int>();
            for (int z = 0; z < Height; z++)
                for (int x = 0; x < Width; x++)
                {
                    int i = z * Width + x;
                    // Listed steam geyser cells count as open ground; the planner reserves them for geothermal only.
                    Class[i] = overrides.Contains(new GridPoint(x, z)) ? LayoutCellClass.Open : Classify(terrain[x, z]);
                    distance[i] = Math.Min(Math.Min(x, z), Math.Min(Width - 1 - x, Height - 1 - z)) + 1;
                    // The fortification setback is from terrain hazards and the map edge,
                    // not from every tree, ruin, or other occupied cell. Occupied cells
                    // themselves remain unplaceable; expanding each one by the full
                    // setback fragments otherwise spacious flat maps into tiny islands.
                    if (Class[i] == LayoutCellClass.Bad &&
                        (!terrain[x, z].Has(TerrainTraits.Occupied) ||
                         terrain[x, z].HasAny(TerrainTraits.MapEdge | TerrainTraits.Water)))
                    { distance[i] = 0; queue.Enqueue(i); }
                }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % Width, z = i / Width;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= Width || nz >= Height) continue;
                        int n = nz * Width + nx;
                        if (distance[n] <= distance[i] + 1) continue;
                        distance[n] = distance[i] + 1;
                        queue.Enqueue(n);
                    }
            }
            OpenClearance = new int[distance.Length];
            Queue<int> openQueue = new Queue<int>();
            for (int i = 0; i < OpenClearance.Length; i++)
            {
                int x = i % Width, z = i / Width;
                OpenClearance[i] = Math.Min(Math.Min(x, z), Math.Min(Width - 1 - x, Height - 1 - z)) + 1;
                if (Class[i] == LayoutCellClass.Bad || terrain[x, z].Has(TerrainTraits.ThickRoof))
                { OpenClearance[i] = 0; openQueue.Enqueue(i); }
            }
            while (openQueue.Count > 0)
            {
                int i = openQueue.Dequeue();
                int x = i % Width, z = i / Width;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= Width || nz >= Height) continue;
                        int n = nz * Width + nx;
                        if (OpenClearance[n] <= OpenClearance[i] + 1) continue;
                        OpenClearance[n] = OpenClearance[i] + 1;
                        openQueue.Enqueue(n);
                    }
            }
            for (int i = 0; i < distance.Length; i++) Placeable[i] = Class[i] != LayoutCellClass.Bad && distance[i] > margin;
        }

        private LayoutTerrainGrid(LayoutTerrainGrid source, GridRect allowed)
        {
            Terrain = source.Terrain;
            Width = source.Width;
            Height = source.Height;
            Class = source.Class;
            OpenClearance = source.OpenClearance;
            Placeable = (bool[])source.Placeable.Clone();
            for (int i = 0; i < Placeable.Length; i++)
                if (!allowed.Contains(new GridPoint(i % Width, i / Width))) Placeable[i] = false;
        }

        public LayoutTerrainGrid RestrictTo(GridRect allowed) => new LayoutTerrainGrid(this, allowed);

        public static LayoutTerrainGrid Build(TerrainSnapshot terrain, int margin, IEnumerable<GridPoint> openOverrides = null) =>
            new LayoutTerrainGrid(terrain, margin, openOverrides);

        public static LayoutCellClass Classify(TerrainCell cell)
        {
            if (cell.HasAny(TerrainTraits.MapEdge | TerrainTraits.Water)) return LayoutCellClass.Bad;
            if (cell.Has(TerrainTraits.NaturalRock)) return LayoutCellClass.Rock;
            if (cell.Has(TerrainTraits.Occupied) || !cell.Has(TerrainTraits.Buildable) ||
                (!cell.Has(TerrainTraits.Standable) && !cell.Has(TerrainTraits.ClearablePlant))) return LayoutCellClass.Bad;
            return cell.Has(TerrainTraits.Roofed) ? LayoutCellClass.Covered : LayoutCellClass.Open;
        }

        public bool InBounds(GridPoint c) => c.X >= 0 && c.Z >= 0 && c.X < Width && c.Z < Height;
        public int Index(GridPoint c) => c.Z * Width + c.X;
        public LayoutCellClass At(GridPoint c) => InBounds(c) ? Class[Index(c)] : LayoutCellClass.Bad;
        public bool CanPlace(GridPoint c) => InBounds(c) && Placeable[Index(c)];
        public bool Roofed(GridPoint c) => InBounds(c) && Terrain[c].Has(TerrainTraits.Roofed);
        public bool ThickCover(GridPoint c) => Terrain.InBounds(c) && Terrain[c].Has(TerrainTraits.ThickRoof);

        // Exterior walking: dry standable unoccupied ground only.
        public bool Passable(GridPoint c)
        {
            if (!InBounds(c)) return false;
            TerrainCell cell = Terrain[c];
            return (cell.Has(TerrainTraits.Standable) || cell.Has(TerrainTraits.ClearablePlant)) &&
                !cell.HasAny(TerrainTraits.Water | TerrainTraits.Occupied | TerrainTraits.NaturalRock);
        }

        // Pawn walking: like Passable, but shallow water counts. Arrivals and
        // attackers wade it, so a site joined to the edge only through shallow
        // water is still reachable.
        public bool Walkable(GridPoint c) => InBounds(c) && LayoutExposure.Walkable(Terrain[c]);
    }

    // Layout engine v2 (docs/plans/2026-10-12-layout-engine-v2.md): a hallway
    // comb with room clusters packed along it, sharing walls.
    public sealed class ClustersLayoutEngine : ILayoutEngine
    {
        public static readonly ClustersLayoutEngine Instance = new ClustersLayoutEngine();
        private ClustersLayoutEngine() { }
        public string Id => "Clusters";
        public ProceduralLayoutResult Plan(ProceduralLayoutInput input, bool requireJointSite) =>
            ProceduralLayoutPlanner.PlanClusters(input, requireJointSite);
    }

    public static class ProceduralLayoutPlanner
    {
        // Complete valid plans compared per site.
        public const int CandidatePoolSize = 3;
        internal static readonly GridPoint[] Cardinal = { new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1) };

        // Plans with the input's engine.
        public static ProceduralLayoutResult Plan(ProceduralLayoutInput input, bool requireJointSite = false)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return input.Engine.Plan(input, requireJointSite);
        }

        internal static ProceduralLayoutResult PlanClusters(ProceduralLayoutInput input, bool requireJointSite)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            string terrainDigest = input.Terrain.Digest;
            string inputDigest = input.Digest;
            List<LayoutDiagnostic> diagnostics = new List<LayoutDiagnostic>();
            LayoutSearchStats stats = new LayoutSearchStats();
            LayoutRequirements requirements = input.Requirements;
            LayoutEquipment equipment = input.Equipment;
            LayoutKit kit = equipment.Kit;
            LayoutDemandSet demand = equipment.Demand;
            Func<string, ProceduralLayoutResult> reject = code =>
            {
                if (input.Terrain.Digest != terrainDigest || input.Digest != inputDigest) throw new InvalidOperationException("P4A planner mutated its input.");
                diagnostics.Add(new LayoutDiagnostic("result", code, stats.ToString()));
                return new ProceduralLayoutResult(null, demand, diagnostics, stats);
            };
            diagnostics.Add(new LayoutDiagnostic("equipment", "stage", requirements.Stage.Level + "->" + kit.Stage + " roles=" + kit.Roles.Digest));
            foreach (string line in kit.Roles.StepDowns) diagnostics.Add(new LayoutDiagnostic("equipment", "step_down", line));
            foreach (string line in kit.Roles.Diagnostics) diagnostics.Add(new LayoutDiagnostic("equipment", "roles", line));
            foreach (string note in equipment.Notes) diagnostics.Add(new LayoutDiagnostic("equipment", "note", note));
            diagnostics.Add(new LayoutDiagnostic("equipment", "materials_estimate", equipment.Estimate + " limit=" + requirements.ImportValueLimit));
            if (equipment.Rejection != null) return reject(equipment.Rejection);
            foreach (string rejection in demand.Rejections) diagnostics.Add(new LayoutDiagnostic("demand", "rejected", rejection));
            if (demand.Rejections.Count > 0) return reject("room_demand_exceeds_legal_dimensions");
            diagnostics.Add(new LayoutDiagnostic("demand", "rooms", string.Join(",", demand.Rooms.Select(r => r.Id + "=" + r.Capacity + r.Unit))));
            diagnostics.Add(new LayoutDiagnostic("demand", "relationships", string.Join(",", demand.Relationships.Select(r => r.ToString()))));

            LayoutTerrainGrid grid = LayoutTerrainGrid.Build(input.Terrain, input.Fortification.PlacementMargin, input.Resources.GeyserFootprints());
            int requiredArea = demand.MinimumRoomArea * 3 / 2;
            P4BJointSiteResult jointSites = null;
            if (requireJointSite && requirements.FieldCells > 0)
            {
                jointSites = P4BJointSitePlanner.Find(input.Terrain, requirements.FieldCells, requiredArea,
                    input.Fortification.PlacementMargin, LayoutSeed.Sub(input.Seed, "joint-site"),
                    input.Budget.MaxSites, input.Budget.MaxSites * 1024, requirements.Residents,
                    attachmentDepth: requirements.ReservePopulationCirculation ? P4BJointSitePlanner.AttachmentDepth(input.Fortification) : 0,
                    attachmentHalfTip: P4BJointSitePlanner.AttachmentHalfTip(input.Fortification));
                diagnostics.Add(new LayoutDiagnostic("joint_site", "preflight", "pairs=" + jointSites.Candidates.Count +
                    " evaluated=" + jointSites.Evaluated + " fieldOriginsScanned=" + jointSites.FieldOriginsScanned +
                    " rejected=" + string.Join(",", jointSites.Rejections)));
                if (jointSites.Candidates.Count == 0) return reject("no_feasible_field_compound_pair");
            }
            List<List<int>> components = Components(grid).Where(c => c.Count >= requiredArea).ToList();
            int placeable = grid.Placeable.Count(p => p);
            if (components.Count == 0)
            {
                diagnostics.Add(new LayoutDiagnostic("site", "insufficient_usable_area", "required~" + requiredArea + " placeableCells=" + placeable +
                    " largestComponent=" + Components(grid).Select(c => c.Count).DefaultIfEmpty(0).Max()));
                return reject("insufficient_usable_area");
            }

            int protectedCells = components.Sum(c => c.Count(i => grid.ThickCover(new GridPoint(i % grid.Width, i / grid.Width))));
            int contiguousProtection = LargestThickRockCluster(grid, components);
            bool protectionPossible = requirements.ExcavationBudgetCells > 0 && contiguousProtection >= requiredArea / 2;
            List<KeyValuePair<LayoutDoctrine, bool>> doctrines = new List<KeyValuePair<LayoutDoctrine, bool>>();
            if (!protectionPossible) doctrines.Add(new KeyValuePair<LayoutDoctrine, bool>(LayoutDoctrine.OpenGround, kit.MortarCapable));
            else if (!kit.MortarCapable)
            {
                doctrines.Add(new KeyValuePair<LayoutDoctrine, bool>(LayoutDoctrine.RockProtected, false));
                doctrines.Add(new KeyValuePair<LayoutDoctrine, bool>(LayoutDoctrine.OpenGround, false));
            }
            else
            {
                int roll = LayoutSeed.Roll(input.Seed, "doctrine", 100);
                bool keepMortar = roll < requirements.MortarBiasPercent;
                diagnostics.Add(new LayoutDiagnostic("doctrine", keepMortar ? "keep_mortar" : "protect", "roll=" + roll + " mortarBias=" + requirements.MortarBiasPercent));
                KeyValuePair<LayoutDoctrine, bool> open = new KeyValuePair<LayoutDoctrine, bool>(LayoutDoctrine.OpenGround, true);
                KeyValuePair<LayoutDoctrine, bool> rock = new KeyValuePair<LayoutDoctrine, bool>(LayoutDoctrine.RockProtected, false);
                doctrines.Add(keepMortar ? open : rock);
                doctrines.Add(keepMortar ? rock : open);
            }
            if (!protectionPossible) diagnostics.Add(new LayoutDiagnostic("doctrine", "open_ground_only",
                "protectedCells=" + protectedCells + " largestThickRockCluster=" + contiguousProtection));

            // Layout engine v2: keep a few validated room layouts per site, then
            // finish (field wall, defenses, turrets) the best-scoring first, so the
            // expensive P4B passes still run about once per accepted plan.
            List<PoolEntry> pool = new List<PoolEntry>();
            Func<ProceduralLayoutResult> finishBest = () =>
            {
                List<PoolEntry> ordered = pool.OrderBy(p => p.Score.Total).ThenBy(p => p.Layout.Digest, StringComparer.Ordinal).ToList();
                int considered = pool.Count;
                pool.Clear();
                foreach (PoolEntry entry in ordered)
                {
                    int gaps;
                    ProceduralLayout finished = Finish(input, entry.Run, entry.Layout, entry.Site, entry.Anchor, diagnostics, out gaps);
                    if (finished == null) continue;
                    if (input.QuestContract == null)
                    {
                        IReadOnlyList<GridPoint> bonus = LayoutBonusFields.Select(input, finished);
                        if (bonus.Count > 0)
                        {
                            ProceduralLayout planted = finished.WithBonusFields(bonus);
                            IReadOnlyList<string> bonusErrors = ProceduralLayoutValidator.Validate(input, planted);
                            if (bonusErrors.Count == 0) finished = planted;
                            Note(diagnostics, "bonus_field", bonusErrors.Count == 0 ? "planted" : "omitted",
                                "cells=" + bonus.Count + (bonusErrors.Count == 0 ? string.Empty : " " + string.Join("+", bonusErrors.Take(4))));
                        }
                    }
                    LayoutCandidateScore final = LayoutCandidateScore.Evaluate(input, finished, gaps, Downgrades(finished));
                    if (input.Terrain.Digest != terrainDigest || input.Digest != inputDigest) throw new InvalidOperationException("P4A planner mutated its input.");
                    diagnostics.Add(new LayoutDiagnostic("result", "accepted", stats + " digest=" + finished.Digest +
                        " candidates=" + considered + " preDefense=" + entry.Score.Total.ToString("F1") + " " + final));
                    return new ProceduralLayoutResult(finished, demand, diagnostics, stats);
                }
                return null;
            };
            for (int d = 0; d < doctrines.Count; d++)
            {
                LayoutDoctrine doctrine = doctrines[d].Key;
                bool wantsCourt = doctrines[d].Value;
                if (d > 0) diagnostics.Add(new LayoutDiagnostic("doctrine", "fallback", doctrine + " court=" + wantsCourt));
                int sitesRemaining = input.Budget.MaxSites - stats.SitesTried;
                int siteQuota = d == doctrines.Count - 1 ? sitesRemaining : Math.Max(1, sitesRemaining / (doctrines.Count - d));
                int siteLimit = stats.SitesTried + siteQuota;
                // Tight explicit harness budgets keep the proven frontage
                // strategy. Larger ordinary rosters use envelope-first packing
                // when the configured budget can fund its broader search.
                bool envelopeStrategy = requirements.Residents >= 9 && !requirements.CompactQuestCamp &&
                    input.Budget.MaxEvaluations >= 1000000;
                List<P4BJointSite> paired = jointSites == null ? null : jointSites.Candidates
                    .Skip(stats.SitesTried).Take(siteQuota).ToList();
                List<GridPoint> anchors = paired == null
                    ? Anchors(grid, components, doctrine, LayoutSeed.Sub(input.Seed, "site"), siteQuota)
                    : paired.Select(s => s.Anchor).ToList();
                if (anchors.Count == 0) diagnostics.Add(new LayoutDiagnostic("site", "no_anchor", doctrine.ToString()));
                for (int siteIndex = 0; siteIndex < anchors.Count; siteIndex++)
                {
                    if (stats.SitesTried >= siteLimit) break;
                    stats.SitesTried++;
                    int siteEvaluationStart = stats.Evaluations;
                    // Every already-ranked same-tile candidate gets a fair
                    // share. Capping the divisor at four allowed those first
                    // four to consume the entire budget and starve a viable
                    // fifth field/compound pair. Reuse unspent early shares.
                    int siteAllowance = Math.Max(1, (input.Budget.MaxEvaluations - stats.Evaluations) /
                        Math.Max(1, anchors.Count - siteIndex));
                    P4BJointSite originalSite = paired == null ? null : paired[siteIndex];
                    IEnumerable<P4BJointSite> variants = originalSite == null
                        ? new P4BJointSite[] { null }
                        : P4BJointSitePlanner.GrowthVariants(originalSite, grid, input.Fortification.PlacementMargin,
                            allowClippedGrowth: input.Requirements.ReservePopulationCirculation,
                            extendedGrowth: envelopeStrategy);
                    foreach (P4BJointSite site in variants)
                    {
                        if (envelopeStrategy && stats.Evaluations - siteEvaluationStart >= siteAllowance && siteIndex + 1 < anchors.Count)
                        { Note(diagnostics, "site", "fair_budget_yield", "evaluations=" + (stats.Evaluations - siteEvaluationStart)); break; }
                        GridPoint anchor = site == null ? anchors[siteIndex] : site.Anchor;
                        LayoutTerrainGrid siteGrid = site == null ? grid : grid.RestrictTo(site.Compound);
                        if (site != null && site != originalSite)
                            Note(diagnostics, "joint_site", "grow_compound",
                                "field=" + site.Field + " compound=" + site.Compound + " anchor=" + anchor);
                        for (int attempt = 0; attempt < input.Budget.AttemptsPerSite; attempt++)
                        {
                            if (envelopeStrategy && stats.Evaluations - siteEvaluationStart >= siteAllowance && siteIndex + 1 < anchors.Count) break;
                            if (stats.BudgetExhausted) return (pool.Count > 0 ? finishBest() : null) ?? reject("search_budget_exhausted");
                            stats.Attempts++;
                            ulong attemptSeed = LayoutSeed.Key(LayoutSeed.Sub(input.Seed, "attempt"), anchor.X * 4099 + anchor.Z, attempt, (int)doctrine);
                            LayoutAttempt run = new LayoutAttempt(input, demand, siteGrid, anchor, doctrine, wantsCourt, attemptSeed, stats, diagnostics,
                                jointSite: site, envelopeSearch: envelopeStrategy);
                            ProceduralLayout layout = run.Run();
                            if (layout == null && run.NeedsPocketsOnly && !stats.BudgetExhausted)
                            {
                                // The public room failed vanilla's outdoor rule: replan this attempt with pocket exhaust only.
                                stats.Attempts++;
                                run = new LayoutAttempt(input, demand, siteGrid, anchor, doctrine, wantsCourt, attemptSeed, stats, diagnostics, true, site,
                                    envelopeSearch: envelopeStrategy);
                                layout = run.Run();
                            }
                            if (stats.BudgetExhausted && layout == null) return (pool.Count > 0 ? finishBest() : null) ?? reject("search_budget_exhausted");
                            if (layout == null) continue;
                            IReadOnlyList<string> violations = ProceduralLayoutValidator.Validate(input, layout);
                            if (violations.Count != 0)
                            {
                                Note(diagnostics, "validate", "rejected", anchor + " " + string.Join("+", violations.Take(6)));
                                continue;
                            }
                            LayoutCandidateScore score = LayoutCandidateScore.Evaluate(input, layout, 0, Downgrades(layout));
                            Note(diagnostics, "candidate", "valid", anchor + " " + score + " digest=" + layout.Digest);
                            pool.Add(new PoolEntry { Score = score, Run = run, Layout = layout, Site = site, Anchor = anchor });
                            if (pool.Count < CandidatePoolSize) continue;
                            ProceduralLayoutResult best = finishBest();
                            if (best != null) return best;
                        }
                    }
                    // A site with a valid candidate ends the search; farther sites are not better.
                    if (pool.Count > 0)
                    {
                        ProceduralLayoutResult best = finishBest();
                        if (best != null) return best;
                    }
                }
            }
            return (pool.Count > 0 ? finishBest() : null) ?? reject("no_valid_layout");
        }

        private sealed class PoolEntry
        {
            public LayoutCandidateScore Score;
            public LayoutAttempt Run;
            public ProceduralLayout Layout;
            public P4BJointSite Site;
            public GridPoint Anchor;
        }

        private static int Downgrades(ProceduralLayout layout) =>
            layout.Notes.Count(n => n.Contains("_dropped") || n.Contains("_infeasible")) +
            (layout.FieldWall != null && layout.Turret == null ? 1 : 0);

        // P4B finishing of one validated attempt: field wall, defense access,
        // approach defense and optional turrets (joint sites), then quest
        // defense. Null when a required stage fails; gaps counts weak fronts.
        private static ProceduralLayout Finish(ProceduralLayoutInput input, LayoutAttempt run, ProceduralLayout layout, P4BJointSite site,
            GridPoint anchor, List<LayoutDiagnostic> diagnostics, out int gapFronts)
        {
            gapFronts = 0;
            IReadOnlyList<string> violations;
            if (site != null)
            {
                HashSet<GridPoint> occupied = new HashSet<GridPoint>(layout.Enclosure
                    .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points()))
                    .Concat(layout.PowerYards.SelectMany(y => y.Cells))
                    .Concat(layout.AllFixtures.SelectMany(f => f.Cells))
                    .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex]))));
                string wallDiagnostic;
                P4BFieldWallPlan fieldWall = P4BFieldWallPlanner.PlanJoint(input, layout, occupied,
                    out wallDiagnostic);
                if (fieldWall == null)
                {
                    Note(diagnostics, "joint_site", "field_wall_rejected", anchor + " " + wallDiagnostic);
                    return null;
                }
                layout = layout.WithFieldWall(input, fieldWall);
                violations = ProceduralLayoutValidator.Validate(input, layout);
                if (violations.Count != 0)
                {
                    Note(diagnostics, "joint_site", "final_rejected", anchor + " " + string.Join("+", violations.Take(6)));
                    return null;
                }
                layout = run.FitDefenseAccess(layout);
                fieldWall = layout.FieldWall;
                occupied = new HashSet<GridPoint>(layout.Enclosure
                    .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points()))
                    .Concat(layout.PowerYards.SelectMany(y => y.Cells))
                    .Concat(layout.AllFixtures.SelectMany(f => f.Cells))
                    .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex]))));
                layout = layout.WithApproachDefense(input, P4BApproachDefensePlanner.Plan(input, layout));
                fieldWall = layout.FieldWall;
                violations = ProceduralLayoutValidator.Validate(input, layout);
                if (violations.Count != 0)
                {
                    Note(diagnostics, "defense", "final_rejected", anchor + " " + string.Join("+", violations.Take(6)));
                    return null;
                }
                string turretDiagnostic;
                P4BTurretPlan turret = P4BTurretPlanner.Plan(input, layout, fieldWall,
                    occupied, out turretDiagnostic);
                if (turret != null)
                {
                    ProceduralLayout armed = layout.WithTurret(input, turret);
                    IReadOnlyList<string> armedErrors = ProceduralLayoutValidator.Validate(input, armed);
                    // One final equipment decision is retained beyond the
                    // search-detail cap; returning immediately bounds it.
                    if (armedErrors.Count == 0)
                    {
                        layout = armed;
                        diagnostics.Add(new LayoutDiagnostic("turret", "accepted", turretDiagnostic));
                    }
                    else diagnostics.Add(new LayoutDiagnostic("turret", "optional_rejected",
                        anchor + " " + string.Join("+", armedErrors.Take(6))));
                }
                else diagnostics.Add(new LayoutDiagnostic("turret", "unplaced", anchor + " " + turretDiagnostic));
            }
            if (input.QuestContract != null)
            {
                layout = layout.WithApproachDefense(input, P4BApproachDefensePlanner.Plan(input, layout));
                if (input.QuestContract.QuestTurrets > 0)
                {
                    P4BTurretPlan guns = P4BTurretPlanner.PlanQuestGuns(input, layout, input.QuestContract.QuestTurrets,
                        out string gunDiagnostic);
                    Note(diagnostics, "quest_guns", guns == null ? "rejected" : "accepted", anchor + " " + gunDiagnostic);
                    if (guns == null) return null;
                    layout = layout.WithTurret(input, guns);
                }
                violations = ProceduralLayoutValidator.Validate(input, layout)
                    .Concat(input.QuestContract.ValidateReservations(layout)).ToList();
                if (violations.Count != 0)
                {
                    Note(diagnostics, "quest_defense", "rejected", string.Join("+", violations.Take(6)));
                    return null;
                }
            }
            if (layout.ApproachDefense != null)
            {
                HashSet<GridPoint> finalOpaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
                finalOpaque.UnionWith(layout.Turret?.All.SelectMany(t => t.Footprint) ?? new GridPoint[0]);
                foreach (P4BAttackSide side in layout.ApproachDefense.Sides.Where(s => s.Exposed))
                {
                    int covered = P4BApproachDefensePlanner.CoveredSamples(side, finalOpaque);
                    if (covered < P4BApproachDefensePlanner.UsefulArc(side.Samples.Count)) gapFronts++;
                    diagnostics.Add(new LayoutDiagnostic("defense_front", covered >=
                        P4BApproachDefensePlanner.UsefulArc(side.Samples.Count) ? "useful" : "gap",
                        side.Id + " posts=" + side.Positions.Count + " samples=" + covered + "/" + side.Samples.Count));
                    int pack = layout.ApproachDefense.Pack;
                    diagnostics.Add(new LayoutDiagnostic("defense_front", side.Positions.Count >= pack ? "side_capacity" : "side_capacity_short",
                        side.Id + "=" + side.Positions.Count + "/" + pack));
                }
            }
            return layout;
        }

        internal static void Note(List<LayoutDiagnostic> diagnostics, string stage, string code, string detail)
        {
            // Bounded diagnostics: counters stay exact, entries are capped.
            if (diagnostics.Count < 240) diagnostics.Add(new LayoutDiagnostic(stage, code, detail));
        }

        private static List<List<int>> Components(LayoutTerrainGrid grid)
        {
            List<List<int>> result = new List<List<int>>();
            bool[] seen = new bool[grid.Placeable.Length];
            for (int start = 0; start < seen.Length; start++)
            {
                if (seen[start] || !grid.Placeable[start]) continue;
                List<int> cells = new List<int>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(start);
                seen[start] = true;
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    cells.Add(i);
                    GridPoint c = new GridPoint(i % grid.Width, i / grid.Width);
                    foreach (GridPoint step in Cardinal)
                    {
                        GridPoint n = new GridPoint(c.X + step.X, c.Z + step.Z);
                        if (!grid.InBounds(n)) continue;
                        int ni = grid.Index(n);
                        if (seen[ni] || !grid.Placeable[ni]) continue;
                        seen[ni] = true;
                        queue.Enqueue(ni);
                    }
                }
                result.Add(cells);
            }
            return result.OrderByDescending(c => c.Count).ThenBy(c => c[0]).ToList();
        }

        private static int LargestThickRockCluster(LayoutTerrainGrid grid, IEnumerable<List<int>> components)
        {
            HashSet<int> eligible = new HashSet<int>(components.SelectMany(c => c).Where(i =>
                grid.Class[i] == LayoutCellClass.Rock &&
                grid.ThickCover(new GridPoint(i % grid.Width, i / grid.Width))));
            int largest = 0;
            while (eligible.Count > 0)
            {
                int start = eligible.Min();
                eligible.Remove(start);
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(start);
                int size = 0;
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    size++;
                    GridPoint cell = new GridPoint(i % grid.Width, i / grid.Width);
                    foreach (GridPoint step in Cardinal)
                    {
                        GridPoint next = new GridPoint(cell.X + step.X, cell.Z + step.Z);
                        if (grid.InBounds(next) && eligible.Remove(grid.Index(next))) queue.Enqueue(grid.Index(next));
                    }
                }
                if (size > largest) largest = size;
            }
            return largest;
        }

        private static List<GridPoint> Anchors(LayoutTerrainGrid grid, List<List<int>> components, LayoutDoctrine doctrine, ulong siteSeed, int max)
        {
            List<GridPoint> result = new List<GridPoint>();
            int[] toOpen = null;
            if (doctrine == LayoutDoctrine.RockProtected)
            {
                toOpen = Enumerable.Repeat(int.MaxValue, grid.Class.Length).ToArray();
                Queue<int> queue = new Queue<int>();
                for (int i = 0; i < grid.Class.Length; i++) if (grid.Class[i] == LayoutCellClass.Open && grid.Placeable[i]) { toOpen[i] = 0; queue.Enqueue(i); }
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    GridPoint c = new GridPoint(i % grid.Width, i / grid.Width);
                    foreach (GridPoint step in Cardinal)
                    {
                        GridPoint n = new GridPoint(c.X + step.X, c.Z + step.Z);
                        if (!grid.InBounds(n) || grid.Class[grid.Index(n)] == LayoutCellClass.Bad) continue;
                        int ni = grid.Index(n);
                        if (toOpen[ni] <= toOpen[i] + 1) continue;
                        toOpen[ni] = toOpen[i] + 1;
                        queue.Enqueue(ni);
                    }
                }
            }
            IEnumerable<int> candidates;
            if (doctrine == LayoutDoctrine.OpenGround)
                // The centroid of an irregular usable component can sit on a narrow edge. Prefer
                // the widest genuinely open patches across all qualifying components.
                candidates = components.SelectMany(c => c).Where(i => grid.Class[i] == LayoutCellClass.Open)
                    .OrderByDescending(i => grid.OpenClearance[i]).ThenBy(i => LayoutSeed.Key(siteSeed, i));
            else
                // A rock face: covered cells a few steps from open ground, preferring more overhead cover nearby.
                candidates = components.SelectMany(c => c)
                    .Where(i => grid.ThickCover(new GridPoint(i % grid.Width, i / grid.Width)) && toOpen[i] >= 3 && toOpen[i] <= 8)
                    .OrderByDescending(i => CoverAround(grid, i, 5) / 8).ThenBy(i => toOpen[i])
                    .ThenBy(i => LayoutSeed.Key(siteSeed, i));
            foreach (int i in candidates)
            {
                GridPoint p = new GridPoint(i % grid.Width, i / grid.Width);
                if (result.Any(a => Math.Abs(a.X - p.X) + Math.Abs(a.Z - p.Z) < 12)) continue;
                result.Add(p);
                if (result.Count >= max) return result;
            }
            return result;
        }

        private static int CoverAround(LayoutTerrainGrid grid, int index, int radius)
        {
            int x0 = index % grid.Width, z0 = index / grid.Width, count = 0;
            for (int z = z0 - radius; z <= z0 + radius; z++)
                for (int x = x0 - radius; x <= x0 + radius; x++)
                    if (grid.ThickCover(new GridPoint(x, z))) count++;
            return count;
        }
    }

    // One seeded attempt at a site: grow streets, place rooms on frontage with
    // repair branches, add the court, trim, enclose, and choose gates.
    internal sealed partial class LayoutAttempt
    {
        private const byte Free = 0;
        private const byte Street = 1;
        private const byte RoomCell = 2;
        private const byte CourtCell = 3;
        private const byte Plaza = 4;
        private const byte YardCell = 5;
        private const byte YardZone = 6;
        private const byte GeyserReserve = 7;
        private const byte GeothermalReserve = 8;
        private const byte Pocket = 9;
        private const byte PocketWall = 10;
        private const byte PocketSeal = 11;
        private const byte FieldApproachReserve = 12;
        private const int MainWidth = 2;

        private readonly ProceduralLayoutInput input;
        private readonly LayoutDemandSet demand;
        private readonly LayoutTerrainGrid grid;
        private readonly GridPoint anchor;
        private readonly LayoutDoctrine doctrine;
        private readonly bool wantsCourt;
        private readonly bool envelopeSearch;
        private readonly ulong seed;
        private readonly LayoutSearchStats stats;
        private readonly List<LayoutDiagnostic> diagnostics;
        private readonly byte[] occ;
        private readonly HashSet<GridPoint> dug = new HashSet<GridPoint>();
        private readonly List<Segment> segments = new List<Segment>();
        private readonly List<LayoutRoom> rooms = new List<LayoutRoom>();
        private readonly Dictionary<string, LayoutRoom> placed = new Dictionary<string, LayoutRoom>();
        private readonly Dictionary<string, LayoutFurnishResult> furnishCache = new Dictionary<string, LayoutFurnishResult>();
        private readonly List<string> notes = new List<string>();
        private readonly HashSet<GridPoint> plaza = new HashSet<GridPoint>();
        private readonly List<LayoutFixture> courts = new List<LayoutFixture>();
        private int repairs;
        private readonly List<LayoutPowerYard> yards = new List<LayoutPowerYard>();
        private readonly List<string> powerDiagnostics = new List<string>();
        private LayoutPowerPlan power;
        private readonly bool pocketsOnly;
        private readonly P4BJointSite jointSite;
        private GridPoint? reservedCompoundGate;
        private readonly int roofHoleBudget;
        private readonly Dictionary<GridPoint, PocketBuild> pockets = new Dictionary<GridPoint, PocketBuild>();
        private readonly HashSet<GridPoint> roomWalls = new HashSet<GridPoint>();

        // Set when the final public room fails vanilla's outdoor rule for a public-exhaust cooler.
        public bool NeedsPocketsOnly { get; private set; }

        private sealed class PocketBuild
        {
            public GridPoint Cell;
            public HashSet<GridPoint> Walls = new HashSet<GridPoint>();
            public HashSet<GridPoint> Seals = new HashSet<GridPoint>();
            public bool RoofCleared;
            public bool Excavated;
            public HashSet<string> Rooms = new HashSet<string>();
        }

        private sealed class Segment
        {
            public string Id;
            public bool Main;
            public bool Retain;
            // Sections that trimming keeps, such as a corridor loop's sides.
            public int Keep;
            public GridPoint Axis;
            public GridPoint Root;
            public List<List<GridPoint>> Sections = new List<List<GridPoint>>();
            public IEnumerable<GridPoint> Cells => Sections.SelectMany(s => s);
        }

        public LayoutAttempt(ProceduralLayoutInput input, LayoutDemandSet demand, LayoutTerrainGrid grid, GridPoint anchor,
            LayoutDoctrine doctrine, bool wantsCourt, ulong seed, LayoutSearchStats stats, List<LayoutDiagnostic> diagnostics,
            bool pocketsOnly = false, P4BJointSite jointSite = null, bool envelopeSearch = false)
        {
            this.pocketsOnly = pocketsOnly;
            this.jointSite = jointSite;
            this.input = input;
            this.demand = demand;
            this.grid = grid;
            this.anchor = anchor;
            this.doctrine = doctrine;
            this.wantsCourt = wantsCourt;
            this.envelopeSearch = envelopeSearch;
            this.seed = seed;
            this.stats = stats;
            this.diagnostics = diagnostics;
            occ = new byte[grid.Width * grid.Height];
            // Rock-protect doctrine: at most one cleared exhaust hole per two cooled rooms (provisional).
            int cooled = demand.Rooms.Count(r => LayoutDemandModel.NeedsOutdoorAir(r.Kind, input.Requirements, input.Equipment.Kit));
            roofHoleBudget = doctrine == LayoutDoctrine.RockProtected ? (cooled + 1) / 2 : int.MaxValue;
            foreach (GridPoint c in input.Resources.GeyserFootprints())
                if (grid.InBounds(c)) occ[grid.Index(c)] = GeyserReserve;
            // On open ground, a legal geothermal generator keeps its 6x6 site around each listed
            // geyser until the yards are placed; unused sites are released afterwards.
            CatalogDef geothermal;
            LayoutKit kit = input.Equipment.Kit;
            if (doctrine == LayoutDoctrine.OpenGround && kit.Powered && input.Catalog.TryGet(LayoutPowerModel.Geothermal, out geothermal) &&
                input.Catalog.IsLegal(geothermal, kit.Stage))
                foreach (GridPoint geyser in input.Resources.SteamGeyserCells)
                    foreach (GridPoint c in GeothermalFootprint(geothermal, geyser))
                        if (grid.InBounds(c) && occ[grid.Index(c)] == Free) occ[grid.Index(c)] = GeothermalReserve;
        }

        internal static List<GridPoint> GeothermalFootprint(CatalogDef def, GridPoint geyser)
        {
            GridPoint min = new GridPoint(geyser.X - (def.SizeX - 1) / 2, geyser.Z - (def.SizeZ - 1) / 2);
            List<GridPoint> cells = new List<GridPoint>();
            for (int dz = 0; dz < def.SizeZ; dz++)
                for (int dx = 0; dx < def.SizeX; dx++) cells.Add(new GridPoint(min.X + dx, min.Z + dz));
            return cells;
        }

        private bool CanExcavate(GridPoint cell) => doctrine == LayoutDoctrine.RockProtected || !grid.ThickCover(cell);
        private int Budget => input.Requirements.ExcavationBudgetCells;
        private string Site => anchor + "/" + doctrine;

        private void Fail(string code, string detail = "") => ProceduralLayoutPlanner.Note(diagnostics, "attempt", code, Site + " " + detail);

        public ProceduralLayout Run()
        {
            if (!PlaceClusteredRooms()) return null;
            if (input.Equipment.Kit.Powered)
            {
                PlaceYards();
                for (int i = 0; i < occ.Length; i++) if (occ[i] == GeothermalReserve) occ[i] = Free;
                if (!PlacePower()) return null;
            }
            // Optional artillery must not consume the required power room's
            // frontage or reserve a magazine beside a future fuel store.
            if (wantsCourt)
            {
                SettlementLoadout staffing = LayoutLoadoutModel.Build(input.Requirements, input.Equipment.Kit, input.Catalog,
                    LayoutSeed.Sub(input.Seed, "loadout"), power?.Generators ?? 0, true);
                int ranged = staffing.Success ? staffing.Residents.Count(r => r.Weapon != null &&
                    input.Catalog.TryGet(r.Weapon.DefName, out CatalogDef weapon) && weapon.MaximumRange > 0) : 0;
                // A quest camp's advertised mortars bring their own crew.
                int capacity = input.Requirements.CompactQuestCamp ? P4EArtillery.Capacity(input.Requirements, input.Equipment.Kit)
                    : Math.Min(P4EArtillery.Capacity(input.Requirements, input.Equipment.Kit), Math.Max(0, ranged - 2));
                for (int i = 0; i < capacity; i++)
                    if (!PlaceCourt()) { notes.Add("mortar_court_or_magazine_infeasible: count=" + courts.Count); break; }
            }
            if (!wantsCourt && input.Equipment.Kit.MortarCapable) notes.Add("mortar_court_dropped_for_rock_protection");
            Trim();
            ProceduralLayout result = Enclose();
            if (result != null || courts.Count == 0) return result;
            // A new optional annex can make the outline cross bad terrain or
            // a geyser. Retry the SAME complete required-room pack without it.
            while (courts.Count > 0)
            {
                LayoutFixture last = courts[courts.Count - 1];
                LayoutRoom store = rooms.Single(r => r.Id == last.RoomId);
                foreach (GridPoint cell in last.Cells) occ[grid.Index(cell)] = Free;
                foreach (GridPoint cell in store.Bounds.Points()) occ[grid.Index(cell)] = Free;
                rooms.Remove(store);
                courts.RemoveAt(courts.Count - 1);
                notes.Add("mortar_court_dropped_for_outline: same required rooms retained count=" + courts.Count);
                NeedsPocketsOnly = false;
                Trim();
                result = Enclose();
                if (result != null) return result;
            }
            return null;
        }

        internal ProceduralLayout FitDefenseAccess(ProceduralLayout layout)
        {
            P4BApproachDefensePlan defense = P4BApproachDefensePlanner.Plan(input, layout);
            string[] sides = defense.Sides.Where(s => s.Exposed)
                .OrderByDescending(s => s.OpenGroundArea).ThenBy(s => s.Id, StringComparer.Ordinal)
                .Select(s => s.Id).ToArray();
            foreach (string id in sides)
            {
                P4BAttackSide side = defense.Sides.Single(s => s.Id == id);
                HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
                int covered = P4BApproachDefensePlanner.CoveredSamples(side, opaque);
                if (!side.Exposed || covered >= P4BApproachDefensePlanner.UsefulArc(side.Samples.Count)) continue;
                bool fitted = false;
                IReadOnlyList<P4BAccessFrontage> options = P4BAccessFrontagePlanner.Candidates(input, layout, side);
                List<string> failures = new List<string>();
                foreach (P4BAccessFrontage frontage in options)
                {
                    if (frontage.Cells.Any(c => occ[grid.Index(c)] != Free &&
                        occ[grid.Index(c)] != Street && occ[grid.Index(c)] != Plaza)) continue;
                    Dictionary<GridPoint, byte> before = frontage.Cells.ToDictionary(c => c, c => occ[grid.Index(c)]);
                    Segment access = new Segment { Id = "defense-access-" + id, Retain = true,
                        Axis = frontage.Outward, Root = frontage.Join };
                    foreach (GridPoint c in frontage.Cells)
                    {
                        access.Sections.Add(new List<GridPoint> { c });
                        occ[grid.Index(c)] = Street;
                    }
                    segments.Add(access);
                    notes.Add("defenseAccess=" + id + " terminal=" + frontage.Terminal +
                        " publicCells=" + frontage.Cells.Count);
                    int diagnosticStart = diagnostics.Count;
                    ProceduralLayout trial = Enclose();
                    if (trial != null && layout.FieldWall != null)
                    {
                        HashSet<GridPoint> occupied = new HashSet<GridPoint>(trial.Enclosure
                            .Concat(trial.Rooms.SelectMany(r => r.Bounds.Points()))
                            .Concat(trial.PowerYards.SelectMany(y => y.Cells))
                            .Concat(trial.AllFixtures.SelectMany(f => f.Cells))
                            .Concat(trial.OuterSections.SelectMany(s => s.ServiceLane(trial.Gates[s.GateIndex]))));
                        P4BFieldWallPlan fieldWall = P4BFieldWallPlanner.PlanJoint(input, trial, occupied, out string reason);
                        if (fieldWall == null)
                        { failures.Add("field_connection=" + reason); trial = null; }
                        else trial = trial.WithFieldWall(input, fieldWall);
                    }
                    P4BApproachDefensePlan trialDefense = null;
                    IReadOnlyList<string> errors = trial == null ? new string[0] : ProceduralLayoutValidator.Validate(input, trial);
                    if (trial != null && errors.Count == 0)
                    {
                        trialDefense = P4BApproachDefensePlanner.Plan(input, trial);
                        P4BAttackSide improved = trialDefense.Sides.Single(s => s.Id == id);
                        HashSet<GridPoint> trialOpaque = P4BApproachDefensePlanner.Opaque(trial, input.Terrain);
                        int shots = P4BApproachDefensePlanner.CoveredSamples(improved, trialOpaque);
                        bool retainedCoverage = defense.Sides.Where(s => s.Exposed &&
                            P4BApproachDefensePlanner.CoveredSamples(s, opaque) >= P4BApproachDefensePlanner.UsefulArc(s.Samples.Count))
                            .All(s =>
                            {
                                P4BAttackSide next = trialDefense.Sides.Single(t => t.Id == s.Id);
                                return !next.Exposed || P4BApproachDefensePlanner.CoveredSamples(next, trialOpaque) >=
                                    P4BApproachDefensePlanner.UsefulArc(next.Samples.Count);
                            });
                        if (retainedCoverage && shots >= P4BApproachDefensePlanner.UsefulArc(improved.Samples.Count))
                        {
                            layout = trial; defense = trialDefense; fitted = true;
                            ProceduralLayoutPlanner.Note(diagnostics, "defense_access", "frontage_fitted",
                                id + " postSamples=" + shots + "/" + improved.Samples.Count +
                                " publicCells=" + frontage.Cells.Count);
                            break;
                        }
                        failures.Add("arc=" + shots + "/" + improved.Samples.Count +
                            (retainedCoverage ? string.Empty : "/regresses_other_front"));
                    }
                    else failures.Add(trial == null ? string.Join("+", diagnostics.Skip(diagnosticStart).Select(d => d.Code).Distinct()) :
                        string.Join("+", errors.Take(3)));
                    // An optional frontage cannot discard a complete room
                    // layout when terrain, gate layers or spending prevent it.
                    foreach (KeyValuePair<GridPoint, byte> cell in before) occ[grid.Index(cell.Key)] = cell.Value;
                    segments.Remove(access);
                    notes.RemoveAt(notes.Count - 1);
                }
                if (!fitted) ProceduralLayoutPlanner.Note(diagnostics, "defense_access", "frontage_unavailable",
                    id + " postSamples=" + covered + "/" + side.Samples.Count + " candidates=" + options.Count +
                    " reasons=" + string.Join(";", failures));
            }
            return layout;
        }

        private bool PlaceWithRepairs(LayoutRoomDemand room)
        {
            bool done = TryPlaceRoom(room);
            if (!done && envelopeSearch && !stats.BudgetExhausted)
                done = PlaceByConnectedExtension(room, true);
            while (!done && !stats.BudgetExhausted && repairs < input.Budget.MaxRepairsPerAttempt)
            {
                repairs++;
                stats.Repairs++;
                if (!AddCombBranch("repair-" + repairs, ClusterTarget(room)))
                {
                    Fail("repair_failed", "no branch for " + room.Id);
                    break;
                }
                ProceduralLayoutPlanner.Note(diagnostics, "repair", "branch_added", Site + " for " + room.Id);
                done = TryPlaceRoom(room);
            }
            if (!done && !stats.BudgetExhausted) done = PlaceByConnectedExtension(room, envelopeSearch);
            if (!done) Fail(stats.BudgetExhausted ? "budget_exhausted" : "room_unplaceable", room.Id + " repairs=" + repairs);
            return done;
        }

        // A room that has no frontage is not evidence of insufficient land.
        // Search the SAME compound's reachable free space and add a public
        // corridor to a proven room envelope, without moving existing rooms or
        // consuming the field/interface reservations. Work remains input-bounded.
        private bool PlaceByConnectedExtension(LayoutRoomDemand room, bool broad = false)
        {
            Queue<GridPoint> queue = new Queue<GridPoint>();
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            Dictionary<GridPoint, int> distance = new Dictionary<GridPoint, int>();
            foreach (GridPoint root in segments.SelectMany(s => s.Cells).Distinct().OrderBy(c => c))
            { queue.Enqueue(root); previous[root] = root; distance[root] = 0; }
            int nodes = 0;
            while (queue.Count > 0 && nodes++ < input.Budget.MaxRouteNodes)
            {
                GridPoint here = queue.Dequeue();
                stats.RouteNodes++;
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + step.X, here.Z + step.Z);
                    if (previous.ContainsKey(next) || !OkForStreet(next)) continue;
                    previous[next] = here; distance[next] = distance[here] + 1; queue.Enqueue(next);
                }
            }
            int evaluated = 0;
            // Integral occupancy rejects tiny gaps in constant time. Counting
            // blocked rectangles against the furnishing-candidate cap made a
            // large reachable envelope effectively invisible behind near holes.
            int stride = grid.Terrain.Width + 1;
            int[] blocked = new int[stride * (grid.Terrain.Height + 1)];
            for (int z = 0; z < grid.Terrain.Height; z++)
                for (int x = 0; x < grid.Terrain.Width; x++)
                {
                    GridPoint c = new GridPoint(x, z);
                    int bad = !grid.CanPlace(c) || occ[grid.Index(c)] != Free ||
                        grid.At(c) == LayoutCellClass.Rock && !CanExcavate(c) ? 1 : 0;
                    int index = (z + 1) * stride + x + 1;
                    blocked[index] = bad + blocked[index - 1] + blocked[index - stride] - blocked[index - stride - 1];
                }
            bool FreeEnvelope(GridPoint door, GridPoint facing, int w, int h)
            {
                int doorX = (w + 1) / 2;
                GridPoint a = ToWorld(door, facing, doorX, new GridPoint(0, 0));
                GridPoint b = ToWorld(door, facing, doorX, new GridPoint(w + 1, h + 1));
                int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X) + 1;
                int z0 = Math.Min(a.Z, b.Z), z1 = Math.Max(a.Z, b.Z) + 1;
                return x0 >= 0 && z0 >= 0 && x1 <= grid.Terrain.Width && z1 <= grid.Terrain.Height &&
                    blocked[z1 * stride + x1] - blocked[z0 * stride + x1] -
                        blocked[z1 * stride + x0] + blocked[z0 * stride + x0] == 0;
            }
            foreach (GridPoint frontage in previous.Keys.Where(c => distance[c] > 0)
                .OrderBy(c => distance[c]).ThenByDescending(c => Math.Min(12, grid.OpenClearance[grid.Index(c)]))
                .ThenBy(c => LayoutSeed.Key(LayoutSeed.Sub(seed, "extension:" + room.Id), c.X, c.Z)))
                foreach (GridPoint facing in ProceduralLayoutPlanner.Cardinal)
                    foreach (GridRect size in room.CandidateSizes.OrderBy(s => s.Area))
                    {
                        GridPoint door = new GridPoint(frontage.X + facing.X, frontage.Z + facing.Z);
                        if (broad && !FreeEnvelope(door, facing, size.Width, size.Height)) continue;
                        if (evaluated++ >= input.Budget.MaxCandidatesPerRoom) return false;
                        if (stats.Evaluations >= input.Budget.MaxEvaluations) { stats.BudgetExhausted = true; return false; }
                        stats.Evaluations++;
                        Candidate candidate = Evaluate(room, door, facing, (size.Width + 1) / 2, size.Width, size.Height);
                        if (candidate == null) continue;
                        List<GridPoint> path = new List<GridPoint>();
                        GridPoint joined = frontage;
                        while (distance[joined] > 0) { path.Add(joined); joined = previous[joined]; }
                        if (path.Any(candidate.Bounds.Contains))
                        {
                            if (!broad) continue;
                            // The shortest provisional walk may cross the
                            // room we are about to build. Route around that
                            // proven envelope instead of discarding usable land.
                            path = ConnectEnvelope(frontage, candidate.Bounds, out joined);
                            if (path == null) continue;
                        }
                        // The corridor is claimed before the room commits its
                        // exhaust pockets; a corridor beside a new pocket would
                        // leave that pocket open to public circulation.
                        HashSet<GridPoint> newPockets = new HashSet<GridPoint>(candidate.Furnish.Fixtures
                            .Where(f => LayoutFixtureKinds.ThroughWall(f.Kind))
                            .Select(f => ToWorld(candidate.Door, candidate.Facing, candidate.DoorX,
                                new GridPoint(f.Cells[0].X - f.Facing.X, f.Cells[0].Z - f.Facing.Z)))
                            .Where(h => occ[grid.Index(h)] == Free));
                        if (path.Any(c => newPockets.Contains(c) ||
                            ProceduralLayoutValidator.Neighbors(c).Any(newPockets.Contains))) continue;
                        int extraDigs = path.Count(c => grid.At(c) == LayoutCellClass.Rock);
                        if (dug.Count + candidate.Dig.Count + extraDigs > Budget && extraDigs > 0) continue;
                        Segment extension = new Segment { Id = "room-extension:" + room.Id, Retain = true, Root = joined, Axis = facing };
                        foreach (GridPoint cell in path) extension.Sections.Add(new List<GridPoint> { cell });
                        if (!Claim(extension)) continue;
                        segments.Add(extension);
                        Commit(room, candidate);
                        ProceduralLayoutPlanner.Note(diagnostics, "repair", "connected_room_extension", Site +
                            " room=" + room.Id + " corridor=" + path.Count + " bounds=" + candidate.Bounds);
                        return true;
                    }
            return false;
        }

        private List<GridPoint> ConnectEnvelope(GridPoint frontage, GridRect bounds, out GridPoint joined)
        {
            var streets = new HashSet<GridPoint>(segments.SelectMany(s => s.Cells));
            var previous = new Dictionary<GridPoint, GridPoint> { [frontage] = frontage };
            var queue = new Queue<GridPoint>(); queue.Enqueue(frontage);
            joined = frontage;
            int visited = 0;
            while (queue.Count > 0 && visited++ < input.Budget.MaxRouteNodes)
            {
                GridPoint here = queue.Dequeue(); stats.RouteNodes++;
                if (streets.Contains(here))
                {
                    joined = here;
                    var path = new List<GridPoint>();
                    while (!here.Equals(frontage)) { here = previous[here]; path.Add(here); }
                    path.Reverse();
                    // Exclude the already claimed joining street; include the
                    // new frontage which serves the room door.
                    return path;
                }
                foreach (GridPoint d in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (bounds.Contains(next) || previous.ContainsKey(next) ||
                        (!streets.Contains(next) && !OkForStreet(next))) continue;
                    previous[next] = here; queue.Enqueue(next);
                }
            }
            return null;
        }

        // Renewables cover only the non-critical load, from open-sky yards inside the
        // enclosure; the rock-protect doctrine is fuel only.
        private void PlaceYards()
        {
            if (doctrine == LayoutDoctrine.RockProtected)
            {
                powerDiagnostics.Add("renewables_skipped_rock_protected");
                return;
            }
            int critical, nonCritical;
            LayoutPowerModel.Loads(rooms, out critical, out nonCritical);
            int remaining = nonCritical;
            LayoutKit kit = input.Equipment.Kit;
            foreach (string name in LayoutPowerModel.RenewableOrder)
            {
                CatalogDef def;
                if (!input.Catalog.TryGet(name, out def) || !input.Catalog.IsLegal(def, kit.Stage)) continue;
                if (remaining <= 0) break;
                int units = 0;
                int accessExtensions = 0;
                string failure = null;
                while (remaining > 0 && units < LayoutPowerModel.MaxUnitsPerSource && failure == null)
                {
                    LayoutPowerYard yard;
                    switch (name)
                    {
                        case LayoutPowerModel.Geothermal:
                            if (input.Resources.SteamGeyserCells.Count == 0) { failure = "no_geyser"; continue; }
                            yard = PlaceGeothermal(def);
                            break;
                        case LayoutPowerModel.Watermill:
                            // Deferred to the P4 river archetype: the mill's front row stands on moving water,
                            // which the P4A grid never encloses.
                            failure = input.Resources.RiverCells.Count == 0 ? "no_river" : "river_outside_enclosure";
                            continue;
                        case LayoutPowerModel.Wind:
                            yard = PlaceWindTurbine(def);
                            break;
                        default:
                            yard = PlaceRectYard(def);
                            break;
                    }
                    if (yard == null)
                    {
                        if ((name == LayoutPowerModel.Wind || name == LayoutPowerModel.Solar) &&
                            accessExtensions < 2 && !stats.BudgetExhausted &&
                            AddBranch("utility-access:" + name + ":" + accessExtensions++)) continue;
                        if (units == 0) failure = name == LayoutPowerModel.Wind ? "no_clear_zone" : "no_fit";
                        break;
                    }
                    yards.Add(yard);
                    remaining -= yard.EffectiveWatts;
                    units++;
                }
                if (failure != null) powerDiagnostics.Add(LayoutPowerModel.Unplaceable + ":" + name + ":" + failure);
            }
        }

        private bool YardCellFree(GridPoint c) => grid.CanPlace(c) && occ[grid.Index(c)] == Free && grid.At(c) == LayoutCellClass.Open && !dug.Contains(c);

        private bool IsStreet(GridPoint c) => grid.InBounds(c) && occ[grid.Index(c)] == Street;

        private GridPoint? StreetAccess(IEnumerable<GridPoint> footprint)
        {
            foreach (GridPoint c in footprint.OrderBy(p => p))
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint n = new GridPoint(c.X + step.X, c.Z + step.Z);
                    if (IsStreet(n)) return n;
                }
            return null;
        }

        private LayoutPowerYard Commit(CatalogDef def, List<GridPoint> cells, GridPoint access, List<GridPoint> zone)
        {
            foreach (GridPoint c in cells) occ[grid.Index(c)] = YardCell;
            foreach (GridPoint c in zone) if (occ[grid.Index(c)] == Free) occ[grid.Index(c)] = YardZone;
            return new LayoutPowerYard(def.DefName, cells, access, zone, "North", -def.BasePowerConsumption, LayoutPowerModel.CapacityFactor(def.DefName));
        }

        // Vanilla PlaceWorker_OnSteamGeyser: generator Position == geyser Position, so the 6x6
        // footprint (GenAdj.OccupiedRect) is x in [gx-2, gx+3], z in [gz-2, gz+3].
        private LayoutPowerYard PlaceGeothermal(CatalogDef def)
        {
            foreach (GridPoint geyser in input.Resources.SteamGeyserCells)
            {
                List<GridPoint> cells = GeothermalFootprint(def, geyser);
                if (cells.Any(c => !grid.CanPlace(c) || grid.At(c) != LayoutCellClass.Open || dug.Contains(c) ||
                    (occ[grid.Index(c)] != Free && occ[grid.Index(c)] != GeyserReserve && occ[grid.Index(c)] != GeothermalReserve))) continue;
                GridPoint? access = StreetAccess(cells);
                if (!access.HasValue) continue;
                return Commit(def, cells, access.Value, new List<GridPoint>());
            }
            return null;
        }

        // Solar-style yard: a rectangle whose near row touches a street cell, like the mortar court.
        private LayoutPowerYard PlaceRectYard(CatalogDef def)
        {
            List<GridPoint> best = null;
            GridPoint bestAccess = default(GridPoint);
            double bestScore = double.MaxValue;
            foreach (GridPoint street in segments.SelectMany(x => x.Cells).Distinct().OrderBy(p => LayoutSeed.Key(LayoutSeed.Sub(seed, def.DefName), p.X, p.Z, yards.Count)))
                foreach (GridPoint f in ProceduralLayoutPlanner.Cardinal)
                {
                    if (stats.Evaluations >= input.Budget.MaxEvaluations) { stats.BudgetExhausted = true; goto done; }
                    stats.Evaluations++;
                    GridPoint p = new GridPoint(f.Z, -f.X);
                    List<GridPoint> cells = new List<GridPoint>();
                    for (int b = 1; b <= def.SizeZ; b++)
                        for (int a = 0; a < def.SizeX; a++) cells.Add(new GridPoint(street.X + b * f.X + a * p.X, street.Z + b * f.Z + a * p.Z));
                    if (!cells.All(YardCellFree)) continue;
                    double score = Manhattan(street, anchor) + (int)(LayoutSeed.Key(seed, street.X, street.Z, f.X * 3 + f.Z) % 5);
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = cells;
                    bestAccess = street;
                }
            done:
            return best == null ? null : Commit(def, best, bestAccess, new List<GridPoint>());
        }

        // Vanilla WindTurbineUtility.CalculateWindCells for rotation North: the 7x2 footprint is
        // x in [cx-3, cx+3], z in [cz, cz+1]; wind cells span the same x with z in [cz+2, cz+11]
        // and [cz-6, cz-1]. They must stay unroofed and free of wind-blocking buildings.
        // North only: other rotations are unverified (AdjustForRotation's even-size shift).
        private LayoutPowerYard PlaceWindTurbine(CatalogDef def)
        {
            List<GridPoint> bestCells = null, bestZone = null;
            GridPoint bestAccess = default(GridPoint);
            double bestScore = double.MaxValue;
            foreach (GridPoint street in segments.SelectMany(x => x.Cells).Distinct().OrderBy(p => LayoutSeed.Key(LayoutSeed.Sub(seed, def.DefName), p.X, p.Z, yards.Count)))
                foreach (GridPoint center in WindCenters(street))
                {
                    if (stats.Evaluations >= input.Budget.MaxEvaluations) { stats.BudgetExhausted = true; goto done; }
                    stats.Evaluations++;
                    List<GridPoint> cells = new List<GridPoint>();
                    for (int z = center.Z; z <= center.Z + 1; z++)
                        for (int x = center.X - 3; x <= center.X + 3; x++) cells.Add(new GridPoint(x, z));
                    if (!cells.All(YardCellFree)) continue;
                    List<GridPoint> zone = WindZone(center);
                    if (!zone.All(c => grid.CanPlace(c) && grid.At(c) == LayoutCellClass.Open && !dug.Contains(c) &&
                        (occ[grid.Index(c)] == Free || occ[grid.Index(c)] == Street || occ[grid.Index(c)] == Plaza || occ[grid.Index(c)] == YardZone))) continue;
                    GridPoint? access = StreetAccess(cells);
                    if (!access.HasValue) continue;
                    double score = Manhattan(street, anchor) + (int)(LayoutSeed.Key(seed, street.X, street.Z, center.X - street.X) % 5);
                    if (score >= bestScore) continue;
                    bestScore = score;
                    bestCells = cells;
                    bestZone = zone;
                    bestAccess = access.Value;
                }
            done:
            return bestCells == null ? null : Commit(def, bestCells, bestAccess, bestZone);
        }

        // Footprints touching the street cell: every lateral offset above and below it, and both sides.
        private static IEnumerable<GridPoint> WindCenters(GridPoint street)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                yield return new GridPoint(street.X + dx, street.Z + 1);
                yield return new GridPoint(street.X + dx, street.Z - 2);
            }
            foreach (int dz in new[] { -1, 0 })
            {
                yield return new GridPoint(street.X + 4, street.Z + dz);
                yield return new GridPoint(street.X - 4, street.Z + dz);
            }
        }

        internal static List<GridPoint> WindZone(GridPoint center)
        {
            List<GridPoint> zone = new List<GridPoint>();
            for (int x = center.X - 3; x <= center.X + 3; x++)
            {
                for (int z = center.Z + 2; z <= center.Z + 11; z++) zone.Add(new GridPoint(x, z));
                for (int z = center.Z - 6; z <= center.Z - 1; z++) zone.Add(new GridPoint(x, z));
            }
            return zone;
        }

        // Dispatchable floor for the critical load plus the non-critical shortfall (including the
        // power room's own draw, bounded by its largest candidate), batteries for the bridge.
        private bool PlacePower()
        {
            LayoutEquipment equipment = input.Equipment;
            LayoutKit kit = equipment.Kit;
            int critical, nonCritical;
            LayoutPowerModel.Loads(rooms, out critical, out nonCritical);
            int renewable = yards.Sum(y => y.EffectiveWatts);
            int reserve = input.Requirements.DefenseReserveWatts;
            int batteries = LayoutPowerModel.Batteries(critical + reserve + nonCritical);
            int generators = LayoutPowerModel.Generators(critical + reserve, nonCritical, renewable, kit.GeneratorWatts);
            LayoutRoomDemand demand = null;
            int ownLoad = 0;
            for (int guard = 0; guard <= LayoutDemandModel.MaxSplit; guard++)
            {
                demand = equipment.PowerRoom(input.Requirements, generators, batteries, input.Seed);
                if (demand == null) { Fail("power_room_unbuildable", generators + " generators"); return false; }
                int own = LayoutDemandModel.MaxLoad(demand, input.Requirements, kit);
                ownLoad = own;
                int needed = LayoutPowerModel.Generators(critical + reserve, nonCritical + own, renewable, kit.GeneratorWatts);
                int bridge = LayoutPowerModel.Batteries(critical + reserve + nonCritical + own);
                if (needed <= generators && bridge <= batteries) break;
                generators = Math.Max(generators, needed);
                batteries = Math.Max(batteries, bridge);
            }
            if (!(Style.Kind == LayoutStyleKind.Mountain ? PlaceWithRepairs(demand) : PlaceLate(demand))) return false;
            power = new LayoutPowerPlan(critical, nonCritical, kit.Generator, generators, batteries, yards, kit.GeneratorFuel,
                LayoutPowerModel.FuelStock(kit.Generator, generators), powerDiagnostics, reserve,
                Math.Max(0, generators - LayoutPowerModel.Generators(critical, nonCritical + ownLoad, renewable, kit.GeneratorWatts)));
            if (reserve > 0) notes.Add("ordinary_defense_reserve=" + reserve + "W extraGenerators=" + power.DefenseGenerators);
            return true;
        }

        private bool OkForStreet(GridPoint c) => grid.CanPlace(c) && occ[grid.Index(c)] == Free &&
            (grid.At(c) != LayoutCellClass.Rock || CanExcavate(c));

        // Reserve the attachment before rooms are packed. A larger envelope
        // alone cannot make a randomly chosen gate line up with the field.
        // The terminal is public street; its outward continuation is kept
        // empty rather than becoming a long, artificial enclosure projection.
        private bool ReserveFieldApproach()
        {
            int setback = input.Fortification.Setback;
            int terminalDistance = P4BJointSitePlanner.AttachmentDepth(input.Fortification);
            // Layered gate bands need a real straight frontage with returning
            // arms, not just a narrow corridor whose walls form two piers.
            int halfTip = P4BJointSitePlanner.AttachmentHalfTip(input.Fortification);
            int halfReserve = Math.Max(setback + 3, halfTip + setback + 1);
            List<string> why = new List<string>();
            foreach (P4BFieldInterface option in jointSite.Interfaces.OrderBy(i => Manhattan(i.InnerGate, anchor)))
            {
                GridPoint outward = new GridPoint(Math.Sign(option.OuterGate.X - option.InnerGate.X),
                    Math.Sign(option.OuterGate.Z - option.InnerGate.Z));
                GridPoint tangent = new GridPoint(outward.Z, outward.X);
                GridPoint terminal = new GridPoint(option.InnerGate.X - outward.X * terminalDistance,
                    option.InnerGate.Z - outward.Z * terminalDistance);
                List<GridPoint> tip = Enumerable.Range(-halfTip, 2 * halfTip + 1).Select(j => new GridPoint(
                    terminal.X + tangent.X * j, terminal.Z + tangent.Z * j)).ToList();
                GridPoint? blockedTip = tip.Where(c => !grid.CanPlace(c) || grid.At(c) != LayoutCellClass.Open ||
                    occ[grid.Index(c)] != Free).Cast<GridPoint?>().FirstOrDefault();
                if (blockedTip.HasValue)
                {
                    why.Add(option.InnerGate + ":tip@" + blockedTip.Value + ":" + (grid.InBounds(blockedTip.Value)
                        ? grid.At(blockedTip.Value) + "/" + occ[grid.Index(blockedTip.Value)] : "out"));
                    continue;
                }
                List<GridPoint> reserve = new List<GridPoint>();
                bool clear = true;
                for (int k = 1; k < terminalDistance && clear; k++)
                    for (int j = -halfReserve; j <= halfReserve; j++)
                    {
                        GridPoint c = new GridPoint(terminal.X + outward.X * k + tangent.X * j,
                            terminal.Z + outward.Z * k + tangent.Z * j);
                        if (Math.Abs(j) <= 1 && (!input.Terrain.InBounds(c) ||
                            LayoutTerrainGrid.Classify(input.Terrain[c]) != LayoutCellClass.Open))
                        { clear = false; why.Add(option.InnerGate + ":lane@" + c + ":" + (input.Terrain.InBounds(c) ?
                            LayoutTerrainGrid.Classify(input.Terrain[c]).ToString() : "out")); break; }
                        if (!grid.InBounds(c)) continue;
                        if (occ[grid.Index(c)] != Free) { clear = false; why.Add(option.InnerGate + ":reserve@" + c + ":occ" + occ[grid.Index(c)]); break; }
                        if (k > setback) reserve.Add(c);
                    }
                if (!clear) continue;
                Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint> { [terminal] = terminal };
                Queue<GridPoint> queue = new Queue<GridPoint>();
                queue.Enqueue(terminal);
                GridPoint? joined = null;
                int visited = 0;
                HashSet<GridPoint> forbidden = new HashSet<GridPoint>(reserve);
                while (queue.Count > 0 && visited < input.Budget.MaxRouteNodes)
                {
                    visited++;
                    GridPoint here = queue.Dequeue();
                    if (occ[grid.Index(here)] == Street) { joined = here; break; }
                    foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                    {
                        GridPoint next = new GridPoint(here.X + step.X, here.Z + step.Z);
                        if (!grid.CanPlace(next) || grid.At(next) != LayoutCellClass.Open ||
                            forbidden.Contains(next) || previous.ContainsKey(next)) continue;
                        byte use = occ[grid.Index(next)];
                        if (use != Free && use != Street && use != Plaza) continue;
                        previous[next] = here;
                        queue.Enqueue(next);
                    }
                }
                stats.RouteNodes += visited;
                if (!joined.HasValue) { why.Add(option.InnerGate + ":no_street_route:" + visited); continue; }
                List<GridPoint> path = new List<GridPoint>();
                for (GridPoint c = joined.Value; !c.Equals(terminal); c = previous[c]) path.Add(c);
                path.AddRange(tip);
                Segment approach = new Segment { Id = "field-approach", Retain = true, Axis = outward, Root = joined.Value };
                foreach (GridPoint c in path.Distinct())
                {
                    approach.Sections.Add(new List<GridPoint> { c });
                    occ[grid.Index(c)] = Street;
                }
                segments.Add(approach);
                foreach (GridPoint c in reserve) occ[grid.Index(c)] = FieldApproachReserve;
                reservedCompoundGate = new GridPoint(terminal.X + outward.X * (setback + 1),
                    terminal.Z + outward.Z * (setback + 1));
                notes.Add("fieldApproachReserved=" + option.InnerGate + " terminal=" + terminal);
                fieldTip = new KeyValuePair<GridPoint, GridPoint>(terminal, tangent);
                fieldHalfTip = halfTip;
                return true;
            }
            Fail("field_approach_unavailable", string.Join(" ", why.Take(6)));
            return false;
        }

        private bool BuildSpine()
        {
            // In joint-site mode the principal public street reaches toward
            // the reserved field interface; branch/room geometry remains seeded.
            bool vertical = jointSite == null ? LayoutSeed.Roll(seed, "axis", 2) == 0 :
                Math.Abs(jointSite.InnerGate.Z - anchor.Z) >= Math.Abs(jointSite.InnerGate.X - anchor.X);
            GridPoint axis = vertical ? new GridPoint(0, 1) : new GridPoint(1, 0);
            GridPoint width = vertical ? new GridPoint(1, 0) : new GridPoint(0, 1);
            double[] factors = { 1.0, 1.25, 1.5 };
            int target = Math.Max(10, Math.Min(70, (int)Math.Round(Math.Sqrt(demand.MinimumRoomArea) * factors[LayoutSeed.Roll(seed, "length", 3)])));
            // Open ground: room for the dining hub and one rib slot either side
            // of it. A field counts as spine length; its wall ribs are the
            // slot on that side.
            if (Style.Kind != LayoutStyleKind.Mountain)
            {
                LayoutRoomDemand dining = demand.Rooms.FirstOrDefault(r => r.Kind == LayoutRoomKind.Dining);
                int hub = dining == null ? 1 : dining.CandidateSizes.Max(z => z.Width) + 4;
                int slot = 2 * (LayoutClusters.TypicalDepth(demand.Rooms) + 2);
                target = Math.Max(target, hub + 2 * slot + 2);
                if (jointSite != null && jointSite.Wall.Count > 0)
                    target = Math.Max(hub + slot + 2, target - FieldLength(axis));
                target = Math.Min(70, target);
            }
            Func<int, List<GridPoint>> section = t => Enumerable.Range(0, MainWidth)
                .Select(k => new GridPoint(anchor.X + t * axis.X + k * width.X, anchor.Z + t * axis.Z + k * width.Z)).ToList();
            List<int> positions = new List<int>();
            if (!section(0).All(OkForStreet))
            {
                Fail("spine_blocked", string.Join(",", section(0).Select(c => c + ":" +
                    (grid.CanPlace(c) ? "placeable" : "unplaceable") + "/" +
                    (grid.InBounds(c) ? occ[grid.Index(c)].ToString() : "outside") + "/" + grid.At(c) +
                    (grid.InBounds(c) && grid.ThickCover(c) ? "/thick" : string.Empty))));
                return false;
            }
            positions.Add(0);
            int up = 1, down = -1;
            bool upOpen = true, downOpen = true;
            while (positions.Count < target && (upOpen || downOpen))
            {
                bool takeUp = upOpen && (!downOpen || up <= -down);
                int t = takeUp ? up : down;
                if (section(t).All(OkForStreet))
                {
                    positions.Add(t);
                    if (takeUp) up++; else down--;
                }
                else if (takeUp) upOpen = false; else downOpen = false;
            }
            if (positions.Count < 6) { Fail("spine_too_short", positions.Count.ToString()); return false; }
            Segment main = new Segment { Id = "main", Main = true, Axis = axis, Root = anchor };
            foreach (int t in positions.OrderBy(t => t)) main.Sections.Add(section(t));
            if (!Claim(main)) { Fail("spine_excavation_budget"); return false; }
            segments.Add(main);
            // Entrance plazas beyond both spine ends keep room walls from sealing
            // the street tips, so gates emerge from the circulation network.
            foreach (int end in new[] { -1, 1 })
            {
                List<GridPoint> tip = end < 0 ? main.Sections[0] : main.Sections[main.Sections.Count - 1];
                for (int k = 1; k <= 2; k++)
                    for (int j = -1; j <= MainWidth; j++)
                    {
                        GridPoint c = new GridPoint(tip[0].X + end * k * axis.X + j * width.X, tip[0].Z + end * k * axis.Z + j * width.Z);
                        LayoutCellClass cls = grid.At(c);
                        if (!grid.CanPlace(c) || occ[grid.Index(c)] != Free || (cls != LayoutCellClass.Open && cls != LayoutCellClass.Covered)) continue;
                        occ[grid.Index(c)] = Plaza;
                        plaza.Add(c);
                    }
            }
            return true;
        }

        private bool Claim(Segment segment)
        {
            List<GridPoint> cells = segment.Cells.ToList();
            int newDig = cells.Count(c => grid.At(c) == LayoutCellClass.Rock);
            if (dug.Count + newDig > Budget && newDig > 0) return false;
            streetVersion++;
            foreach (GridPoint c in cells)
            {
                occ[grid.Index(c)] = Street;
                if (grid.At(c) == LayoutCellClass.Rock) dug.Add(c);
            }
            return true;
        }

        private bool AddBranch(string id)
        {
            List<KeyValuePair<GridPoint, GridPoint>> roots = new List<KeyValuePair<GridPoint, GridPoint>>();
            foreach (Segment segment in segments)
            {
                GridPoint perp = new GridPoint(segment.Axis.Z, segment.Axis.X);
                foreach (GridPoint cell in segment.Cells)
                    foreach (GridPoint dir in new[] { perp, new GridPoint(-perp.X, -perp.Z) })
                    {
                        GridPoint first = new GridPoint(cell.X + dir.X, cell.Z + dir.Z);
                        if (OkForStreet(first)) roots.Add(new KeyValuePair<GridPoint, GridPoint>(cell, dir));
                    }
            }
            ulong branchSeed = LayoutSeed.Sub(seed, id);
            foreach (KeyValuePair<GridPoint, GridPoint> root in roots.OrderBy(r => LayoutSeed.Key(branchSeed, r.Key.X, r.Key.Z, r.Value.X * 3 + r.Value.Z)))
            {
                int length = 4 + LayoutSeed.Roll(LayoutSeed.Key(branchSeed, root.Key.X, root.Key.Z), "length", 9);
                Segment branch = new Segment { Id = id, Main = false, Axis = root.Value, Root = root.Key };
                for (int k = 1; k <= length; k++)
                {
                    GridPoint c = new GridPoint(root.Key.X + k * root.Value.X, root.Key.Z + k * root.Value.Z);
                    if (!OkForStreet(c)) break;
                    GridPoint ahead = new GridPoint(c.X + root.Value.X, c.Z + root.Value.Z);
                    if (grid.InBounds(ahead) && occ[grid.Index(ahead)] == Street) break;
                    branch.Sections.Add(new List<GridPoint> { c });
                }
                if (branch.Sections.Count < 3) continue;
                if (!Claim(branch)) continue;
                segments.Add(branch);
                return true;
            }
            return false;
        }

        private GridPoint ToWorld(GridPoint door, GridPoint facing, int doorX, GridPoint local)
        {
            GridPoint p = new GridPoint(facing.Z, -facing.X);
            int a = local.X - doorX, b = local.Z;
            return new GridPoint(door.X + a * p.X + b * facing.X, door.Z + a * p.Z + b * facing.Z);
        }

        private GridPoint DirToWorld(GridPoint facing, GridPoint local)
        {
            GridPoint p = new GridPoint(facing.Z, -facing.X);
            return new GridPoint(local.X * p.X + local.Z * facing.X, local.X * p.Z + local.Z * facing.Z);
        }

        private sealed class Candidate
        {
            public double Score;
            public GridRect Bounds;
            public GridPoint Door;
            public GridPoint Facing;
            public int DoorX;
            public int Width;
            public LayoutFurnishResult Furnish;
            public List<GridPoint> Dig;
            public bool Protected;
            // Wall cells reused from already placed rooms.
            public List<string> SharedWith;
        }

        private bool TryPlaceRoom(LayoutRoomDemand room)
        {
            Candidate best = null;
            int evaluated = 0;
            foreach (GridPoint street in ClusterFrontage(room))
            {
                foreach (GridPoint facing in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint door = new GridPoint(street.X + facing.X, street.Z + facing.Z);
                    if (!grid.CanPlace(door) || occ[grid.Index(door)] != Free) continue;
                    foreach (GridRect size in room.CandidateSizes)
                    {
                        int w = size.Width, h = size.Height;
                        int seeded = 1 + LayoutSeed.Roll(LayoutSeed.Key(seed, street.X, street.Z, w * 16 + h), "door", w);
                        foreach (int doorX in new[] { (w + 1) / 2, seeded }.Distinct())
                        {
                            if (evaluated >= input.Budget.MaxCandidatesPerRoom) goto done;
                            if (stats.Evaluations >= input.Budget.MaxEvaluations) { stats.BudgetExhausted = true; goto done; }
                            evaluated++;
                            stats.Evaluations++;
                            Candidate candidate = Evaluate(room, door, facing, doorX, w, h);
                            if (candidate != null && (best == null || candidate.Score < best.Score)) best = candidate;
                        }
                    }
                }
            }
            done:
            if (best == null) return false;
            Commit(room, best);
            return true;
        }

        private Candidate Evaluate(LayoutRoomDemand room, GridPoint door, GridPoint facing, int doorX, int w, int h)
        {
            GridPoint a = ToWorld(door, facing, doorX, new GridPoint(0, 0));
            GridPoint b = ToWorld(door, facing, doorX, new GridPoint(w + 1, h + 1));
            GridRect bounds = new GridRect(Math.Min(a.X, b.X), Math.Min(a.Z, b.Z), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Z - b.Z) + 1);
            List<GridPoint> dig = new List<GridPoint>();
            List<string> sharedWith = new List<string>();
            int protectedCells = 0;
            for (int z = bounds.MinZ; z <= bounds.MaxZ; z++)
                for (int x = bounds.MinX; x <= bounds.MaxX; x++)
                {
                    GridPoint c = new GridPoint(x, z);
                    bool edge = x == bounds.MinX || x == bounds.MaxX || z == bounds.MinZ || z == bounds.MaxZ;
                    byte o = grid.CanPlace(c) ? occ[grid.Index(c)] : (byte)255;
                    // A pocket's reserved wall or rock seal may become this room's wall (never its door).
                    bool absorbable = edge && !c.Equals(door) && (o == PocketWall || o == PocketSeal);
                    // Clusters engine: a plain wall of a placed room may also be this room's wall.
                    string owner = null;
                    bool shared = sharedWith != null && edge && !c.Equals(door) && o == RoomCell && SharableWall(c, out owner);
                    if (shared) sharedWith.Add(owner);
                    if (o != Free && !absorbable && !shared) return null;
                    if (grid.At(c) == LayoutCellClass.Rock)
                    {
                        if (!CanExcavate(c)) return null;
                        if (!edge || c.Equals(door)) dig.Add(c);
                    }
                    if (!edge && grid.ThickCover(c)) protectedCells++;
                }
            if (dug.Count + dig.Count > Budget && dig.Count > 0) return null;
            bool thick = protectedCells == w * h;
            GridPoint outside = new GridPoint(door.X - facing.X, door.Z - facing.Z);
            foreach (LayoutRelationship rel in demand.Relationships.Where(r => r.Hard && (r.A == room.Id || r.B == room.Id)))
            {
                LayoutRoom partner;
                if (placed.TryGetValue(rel.A == room.Id ? rel.B : rel.A, out partner) && Manhattan(outside, partner.DoorOutside) > rel.MaxDistance) return null;
            }
            bool needsCoolers = LayoutDemandModel.NeedsOutdoorAir(room.Kind, input.Requirements, input.Equipment.Kit) &&
                (room.Kind != LayoutRoomKind.Freezer && room.Kind != LayoutRoomKind.Power ? !thick : true);
            Dictionary<GridPoint, int> exhaustCosts = new Dictionary<GridPoint, int>();
            string costKey = string.Empty;
            if (needsCoolers)
            {
                foreach (KeyValuePair<GridPoint, GridPoint> wall in LocalWalls(w, h))
                {
                    GridPoint world = ToWorld(door, facing, doorX, wall.Key);
                    GridPoint hotCell = ToWorld(door, facing, doorX, new GridPoint(wall.Key.X - wall.Value.X, wall.Key.Z - wall.Value.Z));
                    int cost = ExhaustCost(world, hotCell, bounds, door);
                    exhaustCosts[wall.Key] = cost;
                    costKey += cost + ",";
                }
            }
            string key = room.Id + "|" + w + "|" + h + "|" + doorX + "|" + thick + "|" + costKey;
            LayoutFurnishResult furnish;
            if (!furnishCache.TryGetValue(key, out furnish))
            {
                furnish = LayoutFurnishing.Furnish(room.Kind, w, h, doorX, room.Capacity,
                    LayoutDemandModel.FurnishContext(input.Equipment.Kit, input.Requirements, thick,
                        needsCoolers ? (Func<GridPoint, int>)(l => { int v; return exhaustCosts.TryGetValue(l, out v) ? v : -1; }) : null, room.Batteries));
                furnishCache[key] = furnish;
            }
            if (!furnish.Success) return null;
            int newHoles = 0, newDigs = 0;
            foreach (LayoutLocalFixture cooler in furnish.Fixtures.Where(f => LayoutFixtureKinds.ThroughWall(f.Kind)))
            {
                GridPoint hotCell = ToWorld(door, facing, doorX, new GridPoint(cooler.Cells[0].X - cooler.Facing.X, cooler.Cells[0].Z - cooler.Facing.Z));
                if (occ[grid.Index(hotCell)] != Free) continue;
                if (grid.Roofed(hotCell)) newHoles++;
                if (grid.At(hotCell) == LayoutCellClass.Rock) newDigs++;
            }
            if (pockets.Values.Count(p => p.RoofCleared) + newHoles > roofHoleBudget) return null;
            foreach (LayoutLocalFixture fixture in furnish.Fixtures.Where(f => LayoutFixtureKinds.ThroughWall(f.Kind)))
            {
                GridPoint wall = ToWorld(door, facing, doorX, fixture.Cells[0]);
                if (grid.At(wall) == LayoutCellClass.Rock) dig.Add(wall);
            }
            if (dug.Count + dig.Count + newDigs > Budget && dig.Count + newDigs > 0) return null;

            double score = 0;
            foreach (LayoutRelationship rel in demand.Relationships.Where(r => r.A == room.Id || r.B == room.Id))
            {
                LayoutRoom partner;
                if (!placed.TryGetValue(rel.A == room.Id ? rel.B : rel.A, out partner)) continue;
                int distance = Manhattan(outside, partner.DoorOutside);
                score += rel.Kind == LayoutRelationKind.Near ? rel.Weight * distance : -rel.Weight * Math.Min(distance, 20);
            }
            double? clustered = ClusterScore(room, bounds, outside, sharedWith);
            if (!clustered.HasValue) return null;
            score += clustered.Value;
            if (doctrine == LayoutDoctrine.RockProtected) score += dig.Count - 4.0 * protectedCells;
            score += (int)(LayoutSeed.Key(seed, door.X * 7 + facing.X, door.Z * 7 + facing.Z, w * 16 + h) % 3UL);
            return new Candidate { Score = score, Bounds = bounds, Door = door, Facing = facing, DoorX = doorX, Width = w,
                Furnish = furnish, Dig = dig, Protected = thick, SharedWith = sharedWith };
        }

        private void Commit(LayoutRoomDemand room, Candidate c)
        {
            List<LayoutFixture> fixtures = new List<LayoutFixture>();
            foreach (LayoutLocalFixture f in c.Furnish.Fixtures)
            {
                GridPoint? exhaust = null;
                if (LayoutFixtureKinds.ThroughWall(f.Kind))
                    exhaust = ToWorld(c.Door, c.Facing, c.DoorX, new GridPoint(f.Cells[0].X - f.Facing.X, f.Cells[0].Z - f.Facing.Z));
                CatalogDef def;
                string stuff = input.Catalog.TryGet(f.DefName, out def) ? input.Equipment.StuffFor(def) : null;
                fixtures.Add(new LayoutFixture(f.Kind, f.DefName, f.Cells.Select(p => ToWorld(c.Door, c.Facing, c.DoorX, p)),
                    f.Access.Select(p => ToWorld(c.Door, c.Facing, c.DoorX, p)), DirToWorld(c.Facing, f.Facing), f.Blocking, f.Watts,
                    f.NeedsFuel, room.Id, exhaust, f.Role, stuff));
            }
            HashSet<GridPoint> digSet = new HashSet<GridPoint>(c.Dig);
            List<GridPoint> naturalWalls = c.Bounds.Points().Where(p => grid.At(p) == LayoutCellClass.Rock && !digSet.Contains(p)).ToList();
            LayoutRoom placedRoom = new LayoutRoom(room, c.Bounds, c.Door, c.Facing, c.Furnish.Capacity, fixtures, c.Protected, naturalWalls);
            foreach (GridPoint p in c.Bounds.Points())
            {
                if (occ[grid.Index(p)] == PocketWall || occ[grid.Index(p)] == PocketSeal)
                    foreach (PocketBuild pocket in pockets.Values) { pocket.Walls.Remove(p); pocket.Seals.Remove(p); }
                occ[grid.Index(p)] = RoomCell;
                if (placedRoom.IsEdge(p) && !p.Equals(c.Door)) roomWalls.Add(p);
            }
            foreach (GridPoint p in c.Dig) dug.Add(p);
            foreach (LayoutFixture cooler in fixtures.Where(f => LayoutFixtureKinds.ThroughWall(f.Kind))) ClaimExhaust(cooler, room.Id);
            rooms.Add(placedRoom);
            placed[room.Id] = placedRoom;
            RecordClusterRoom(placedRoom, c.SharedWith);
        }

        // Local wall cells (never corners or the door cell's neighbours are filtered by
        // furnishing) with the inward direction of their wall.
        private static IEnumerable<KeyValuePair<GridPoint, GridPoint>> LocalWalls(int w, int h)
        {
            for (int x = 1; x <= w; x++) yield return new KeyValuePair<GridPoint, GridPoint>(new GridPoint(x, 0), new GridPoint(0, 1));
            for (int z = 1; z <= h; z++) yield return new KeyValuePair<GridPoint, GridPoint>(new GridPoint(0, z), new GridPoint(1, 0));
            for (int z = 1; z <= h; z++) yield return new KeyValuePair<GridPoint, GridPoint>(new GridPoint(w + 1, z), new GridPoint(-1, 0));
            for (int x = 1; x <= w; x++) yield return new KeyValuePair<GridPoint, GridPoint>(new GridPoint(x, h + 1), new GridPoint(0, -1));
        }

        // Exhaust options for a cooler in wall cell `wall` whose vanilla hot cell is `hot`:
        // 0 public open-sky street (provisional until the partition check), 1 existing
        // pocket, 3 new sealed pocket (+5 when a roof must be cleared, +1 when rock is dug),
        // -1 when the hot cell cannot become outdoor space.
        private int ExhaustCost(GridPoint wall, GridPoint hot, GridRect bounds, GridPoint door)
        {
            if (!grid.InBounds(hot) || bounds.Contains(hot)) return -1;
            byte o = occ[grid.Index(hot)];
            if (!pocketsOnly && o == Street && grid.At(hot) == LayoutCellClass.Open && !dug.Contains(hot)) return 0;
            if (o == Pocket) return 1;
            if (o != Free || !grid.CanPlace(hot)) return -1;
            LayoutCellClass cls = grid.At(hot);
            if (cls == LayoutCellClass.Bad || (cls == LayoutCellClass.Rock && !CanExcavate(hot))) return -1;
            foreach (GridPoint n in ProceduralLayoutValidator.Neighbors(hot))
            {
                if (n.Equals(wall)) continue;
                if (!grid.InBounds(n)) return -1;
                if (bounds.Contains(n))
                {
                    bool edge = n.X == bounds.MinX || n.X == bounds.MaxX || n.Z == bounds.MinZ || n.Z == bounds.MaxZ;
                    if (!edge || n.Equals(door)) return -1;
                    continue;
                }
                // The cell outside this room's door stays walkable, so it can
                // never become the pocket's wall: such a pocket is never sealed.
                if (Manhattan(n, door) == 1) return -1;
                byte on = occ[grid.Index(n)];
                if (on == RoomCell) { if (!roomWalls.Contains(n)) return -1; continue; }
                if (on == PocketWall || on == PocketSeal) continue;
                if (on != Free) return -1;
                if (!grid.CanPlace(n) || grid.At(n) == LayoutCellClass.Bad) return -1;
            }
            int cost = 3;
            if (grid.Roofed(hot))
            {
                // P4's mutation ledger clears roof only over excavated rock.
                if (cls != LayoutCellClass.Rock) return -1;
                if (pockets.Values.Count(p => p.RoofCleared) >= roofHoleBudget) return -1;
                cost += 5;
            }
            if (cls == LayoutCellClass.Rock)
            {
                if (dug.Count + 1 > Budget) return -1;
                cost += 1;
            }
            return cost;
        }

        private void ClaimExhaust(LayoutFixture cooler, string roomId)
        {
            GridPoint hot = cooler.HotCell.Value;
            byte o = occ[grid.Index(hot)];
            PocketBuild existing;
            if (o == Pocket && pockets.TryGetValue(hot, out existing)) { existing.Rooms.Add(roomId); return; }
            if (o != Free) return; // public street exhaust
            PocketBuild pocket = new PocketBuild { Cell = hot, RoofCleared = grid.Roofed(hot), Excavated = grid.At(hot) == LayoutCellClass.Rock };
            pocket.Rooms.Add(roomId);
            occ[grid.Index(hot)] = Pocket;
            if (pocket.Excavated) dug.Add(hot);
            foreach (GridPoint n in ProceduralLayoutValidator.Neighbors(hot))
            {
                if (occ[grid.Index(n)] != Free) continue;
                if (grid.At(n) == LayoutCellClass.Rock && !dug.Contains(n)) { occ[grid.Index(n)] = PocketSeal; pocket.Seals.Add(n); }
                else { occ[grid.Index(n)] = PocketWall; pocket.Walls.Add(n); }
            }
            pockets[hot] = pocket;
        }

        private bool PlaceCourt(int magazineRepairs = 0)
        {
            CatalogDef shelf = input.Equipment.Kit.DefFor(SettlementRole.MortarStorage);
            if (shelf == null || shelf.SizeX != 1 || shelf.SizeZ != 1 || shelf.Passability == CatalogPassability.Impassable)
            { notes.Add("mortar_magazine_no_legal_one_cell_shelf"); return false; }
            Candidate best = null;
            List<GridPoint> access = null;
            LayoutRoom magazine = null;
            foreach (GridPoint street in segments.SelectMany(s => s.Cells).Distinct()
                .OrderBy(p => LayoutSeed.Key(LayoutSeed.Sub(seed, "court"), p.X, p.Z)).Take(128))
                foreach (GridPoint f in ProceduralLayoutPlanner.Cardinal)
                {
                    if (stats.Evaluations >= input.Budget.MaxEvaluations) { stats.BudgetExhausted = true; break; }
                    stats.Evaluations++;
                    GridPoint p = new GridPoint(f.Z, -f.X);
                    GridPoint[] cells =
                    {
                        new GridPoint(street.X + f.X, street.Z + f.Z), new GridPoint(street.X + f.X + p.X, street.Z + f.Z + p.Z),
                        new GridPoint(street.X + 2 * f.X, street.Z + 2 * f.Z), new GridPoint(street.X + 2 * f.X + p.X, street.Z + 2 * f.Z + p.Z)
                    };
                    if (courts.Any(c => c.Access.Contains(street)) || grid.At(street) != LayoutCellClass.Open || !cells.All(x => grid.CanPlace(x) && occ[grid.Index(x)] == Free && grid.At(x) == LayoutCellClass.Open &&
                        grid.Terrain[x].Has(TerrainTraits.SupportsHeavy))) continue;
                    double score = Manhattan(street, anchor) + (int)(LayoutSeed.Key(seed, street.X, street.Z, f.X * 3 + f.Z) % 5);
                    if (best != null && score >= best.Score) continue;
                    LayoutRoom store = FindMagazine(street, cells, shelf, f);
                    if (store == null) continue;
                    best = new Candidate { Score = score, Door = street, Facing = f, Bounds = new GridRect(Math.Min(cells[0].X, cells[3].X),
                        Math.Min(cells[0].Z, cells[3].Z), 2, 2) };
                    access = new List<GridPoint> { street };
                    magazine = store;
                }
            if (best == null)
                return magazineRepairs < 2 && AddBranch("mortar-magazine-" + magazineRepairs) && PlaceCourt(magazineRepairs + 1);
            int artilleryValue = P4EArtillery.ImportValue(input.Catalog, courts.Count + 1);
            if (artilleryValue > input.Requirements.ImportValueLimit)
            { notes.Add("mortar_import_budget:" + artilleryValue); return false; }
            courts.Add(new LayoutFixture(LayoutFixtureKind.MortarCourt, "MortarCourtReservation", best.Bounds.Points(), access,
                new GridPoint(-best.Facing.X, -best.Facing.Z), true, 0, false, magazine.Id));
            foreach (GridPoint c in best.Bounds.Points()) occ[grid.Index(c)] = CourtCell;
            foreach (GridPoint c in magazine.Bounds.Points()) occ[grid.Index(c)] = RoomCell;
            rooms.Add(magazine);
            notes.Add("mortar_magazine=" + magazine.Interior.Points().Single() + " operator=" + access[0] +
                " loadingManhattan=" + Manhattan(access[0], magazine.DoorOutside));
            return true;
        }

        private LayoutRoom FindMagazine(GridPoint operating, GridPoint[] gun, CatalogDef shelf, GridPoint gunForward)
        {
            // One usable storage cell, seven walls and one door: 3x3 total.
            // Door access belongs beside the working position, not merely near
            // the gun. Finite stocks and separation from sensitive rooms remain;
            // the enclosure is not a promise of blast immunity for the operator.
            IEnumerable<GridPoint> sensitive = rooms.Where(r => r.Kind == LayoutRoomKind.Barracks ||
                r.Kind == LayoutRoomKind.Bedroom || r.Kind == LayoutRoomKind.Medical ||
                r.Kind == LayoutRoomKind.Power || r.Kind == LayoutRoomKind.Storage).SelectMany(r => r.Bounds.Points());
            GridPoint[] sensitiveCells = sensitive.ToArray();
            foreach (GridPoint street in new[] { operating }.Concat(ProceduralLayoutPlanner.Cardinal.Select(d =>
                new GridPoint(operating.X + d.X, operating.Z + d.Z)))
                .Where(c => grid.InBounds(c) && grid.Passable(c) &&
                    (occ[grid.Index(c)] == Free || occ[grid.Index(c)] == Street || occ[grid.Index(c)] == Plaza)))
                foreach (GridPoint facing in ProceduralLayoutPlanner.Cardinal)
                {
                    if (stats.Evaluations >= input.Budget.MaxEvaluations) return null;
                    stats.Evaluations++;
                    GridPoint center = new GridPoint(street.X + 2 * facing.X, street.Z + 2 * facing.Z);
                    GridRect bounds = new GridRect(center.X - 1, center.Z - 1, 3, 3);
                    GridPoint[] footprint = bounds.Points().ToArray();
                    // Stock must be behind/beside the operator, not beyond the
                    // muzzle or on the opposite side of the firing footprint.
                    if ((center.X - operating.X) * gunForward.X + (center.Z - operating.Z) * gunForward.Z > 0 ||
                        sensitiveCells.Any(c => Math.Max(Math.Abs(c.X - center.X), Math.Abs(c.Z - center.Z)) < 3) ||
                        footprint.Contains(operating) || footprint.Any(gun.Contains) || !footprint.All(c => grid.CanPlace(c) &&
                            occ[grid.Index(c)] == Free && grid.At(c) == LayoutCellClass.Open)) continue;
                    GridPoint door = new GridPoint(street.X + facing.X, street.Z + facing.Z);
                    LayoutRoomDemand demand = new LayoutRoomDemand(courts.Count == 0 ? "mortar-magazine" : "mortar-magazine-" + courts.Count, LayoutRoomKind.MortarMagazine,
                        1, "shelf_cells", new[] { new GridRect(0, 0, 3, 3) });
                    // The shelf front faces the magazine door.
                    var fixture = new LayoutFixture(LayoutFixtureKind.Shelf, shelf.DefName, new[] { center }, null,
                        new GridPoint(-facing.X, -facing.Z), true, 0, false, demand.Id, role: SettlementRole.MortarStorage,
                        stuff: input.Equipment.StuffFor(shelf));
                    return new LayoutRoom(demand, bounds, door, facing, 1, new[] { fixture }, false, new GridPoint[0]);
                }
            return null;
        }

        // Remove street sections that serve nothing: no door, cooler exhaust,
        // court access, or attached branch. Branch roots are never trimmed.
        private void Trim()
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                HashSet<GridPoint> used = new HashSet<GridPoint>(rooms.SelectMany(r => r.Doors.Select(r.OutsideOf)));
                foreach (LayoutFixture f in rooms.SelectMany(r => r.Fixtures).Where(f => f.Exhaust.HasValue)) used.Add(f.Exhaust.Value);
                used.UnionWith(courts.SelectMany(c => c.Access));
                foreach (LayoutPowerYard yard in yards)
                {
                    used.Add(yard.Access);
                    used.UnionWith(yard.ZoneCells);
                }
                foreach (Segment branch in segments.Where(s => !s.Main && s.Sections.Count > 0)) used.Add(branch.Root);
                foreach (Segment segment in segments)
                {
                    if (segment.Main || segment.Retain) continue;
                    while (segment.Sections.Count > Math.Max(1, segment.Keep) && !segment.Sections[segment.Sections.Count - 1].Any(used.Contains))
                    { Release(segment.Sections[segment.Sections.Count - 1]); segment.Sections.RemoveAt(segment.Sections.Count - 1); changed = true; }
                    if (segment.Sections.Count == 1 && segment.Keep == 0 && !segment.Sections[0].Any(used.Contains))
                    { Release(segment.Sections[0]); segment.Sections.Clear(); changed = true; }
                }
                segments.RemoveAll(s => s.Sections.Count == 0);
            }
        }

        private void Release(List<GridPoint> cells)
        {
            foreach (GridPoint c in cells)
            {
                occ[grid.Index(c)] = Free;
                dug.Remove(c);
            }
        }

        private ProceduralLayout Enclose()
        {
            int s = input.Fortification.Setback;
            bool[] inE = new bool[occ.Length];
            List<GridPoint> core = new List<GridPoint>();
            for (int i = 0; i < occ.Length; i++) if (occ[i] != Free && occ[i] != GeyserReserve && occ[i] != FieldApproachReserve) { inE[i] = true; core.Add(new GridPoint(i % grid.Width, i / grid.Width)); }
            foreach (GridPoint c in core)
                for (int dz = -s; dz <= s; dz++)
                    for (int dx = -s; dx <= s; dx++)
                    {
                        GridPoint n = new GridPoint(c.X + dx, c.Z + dz);
                        LayoutCellClass cls = grid.At(n);
                        if (cls == LayoutCellClass.Open || cls == LayoutCellClass.Covered) inE[grid.Index(n)] = true;
                    }
            // A pre-existing edifice cannot become a perimeter wall. Absorb nearby
            // occupied cells into the enclosure instead of letting the perimeter
            // collide with one and rejecting an otherwise usable site.
            bool absorbed;
            do
            {
                absorbed = false;
                List<int> neighboringOccupied = new List<int>();
                for (int i = 0; i < inE.Length; i++)
                {
                    if (inE[i] || grid.Class[i] != LayoutCellClass.Bad) continue;
                    GridPoint c = new GridPoint(i % grid.Width, i / grid.Width);
                    if (!grid.Terrain[c].Has(TerrainTraits.Occupied) ||
                        grid.Terrain[c].HasAny(TerrainTraits.MapEdge | TerrainTraits.Water)) continue;
                    bool adjacent = false;
                    for (int dz = -1; dz <= 1 && !adjacent; dz++)
                        for (int dx = -1; dx <= 1 && !adjacent; dx++)
                        {
                            GridPoint n = new GridPoint(c.X + dx, c.Z + dz);
                            adjacent = grid.InBounds(n) && inE[grid.Index(n)];
                        }
                    if (adjacent) neighboringOccupied.Add(i);
                }
                foreach (int i in neighboringOccupied) { inE[i] = true; absorbed = true; }
            } while (absorbed);
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            for (int i = 0; i < inE.Length; i++)
                if (inE[i]) { int x = i % grid.Width, z = i / grid.Width; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z); }
            // Fill enclosed holes (a rock pillar or pond inside stays a blocked obstacle, not a second wall ring).
            minX = Math.Max(0, minX - 1); minZ = Math.Max(0, minZ - 1); maxX = Math.Min(grid.Width - 1, maxX + 1); maxZ = Math.Min(grid.Height - 1, maxZ + 1);
            bool[] outside = new bool[inE.Length];
            Queue<GridPoint> queue = new Queue<GridPoint>();
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    if ((x == minX || x == maxX || z == minZ || z == maxZ) && !inE[z * grid.Width + x]) { outside[z * grid.Width + x] = true; queue.Enqueue(new GridPoint(x, z)); }
            while (queue.Count > 0)
            {
                GridPoint c = queue.Dequeue();
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint n = new GridPoint(c.X + step.X, c.Z + step.Z);
                    if (n.X < minX || n.Z < minZ || n.X > maxX || n.Z > maxZ) continue;
                    int ni = grid.Index(n);
                    if (outside[ni] || inE[ni]) continue;
                    outside[ni] = true;
                    queue.Enqueue(n);
                }
            }
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    if (!outside[z * grid.Width + x]) inE[z * grid.Width + x] = true;

            HashSet<GridPoint> built = new HashSet<GridPoint>();
            HashSet<GridPoint> natural = new HashSet<GridPoint>();
            List<GridPoint> enclosure = new List<GridPoint>();
            for (int i = 0; i < inE.Length; i++)
            {
                if (!inE[i]) continue;
                GridPoint c = new GridPoint(i % grid.Width, i / grid.Width);
                enclosure.Add(c);
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        GridPoint n = new GridPoint(c.X + dx, c.Z + dz);
                        if (!grid.InBounds(n)) { Fail("perimeter_out_of_bounds"); return null; }
                        if (inE[grid.Index(n)]) continue;
                        if (occ[grid.Index(n)] == GeyserReserve) { Fail("perimeter_on_geyser", n.ToString()); return null; }
                        LayoutCellClass cls = grid.At(n);
                        if (cls == LayoutCellClass.Rock) natural.Add(n);
                        else if (cls == LayoutCellClass.Open || cls == LayoutCellClass.Covered) built.Add(n);
                        else { Fail("perimeter_cell_illegal", n.ToString()); return null; }
                    }
            }

            HashSet<GridPoint> streets = new HashSet<GridPoint>(segments.SelectMany(x => x.Cells).Concat(plaza));
            Func<GridPoint, bool> isPublic = c => grid.InBounds(c) && inE[grid.Index(c)] && occ[grid.Index(c)] != RoomCell &&
                occ[grid.Index(c)] != CourtCell && occ[grid.Index(c)] != YardCell && occ[grid.Index(c)] != Pocket &&
                occ[grid.Index(c)] != PocketWall && occ[grid.Index(c)] != PocketSeal && grid.At(c) != LayoutCellClass.Bad && (grid.At(c) != LayoutCellClass.Rock || dug.Contains(c));
            List<LayoutGate> gates = ChooseGates(inE, built, natural, streets, isPublic);
            if (gates == null) return null;
            foreach (LayoutGate gate in gates) built.Remove(gate.Cell);
            // The complete shells below enclose every side, gates included, so
            // gate-adjacent wall sections are no longer built.
            List<LayoutWallSection> outerSections = new List<LayoutWallSection>();
            IReadOnlyList<GridPoint> reinforcement = CompletePerimeter(enclosure, built, natural, gates, outerSections);
            if (reinforcement == null) return null;

            HashSet<GridPoint> cleared = new HashSet<GridPoint>(pockets.Values.Where(p => p.RoofCleared).Select(p => p.Cell));
            List<GridPoint> retained = dug.Where(c => grid.ThickCover(c) && !cleared.Contains(c)).ToList();
            IReadOnlyList<GridPoint> unsupported = LayoutRoofSupport.Unsupported(input.Terrain, dug, retained);
            if (unsupported.Count > 0) { Fail("roof_support_insufficient", unsupported.Count + " cells e.g. " + unsupported[0]); return null; }
            HashSet<GridPoint> retainedSet = new HashSet<GridPoint>(retained);
            List<GridPoint> constructed = rooms.SelectMany(r => r.Bounds.Points())
                .Where(c => !retainedSet.Contains(c) && !(grid.At(c) == LayoutCellClass.Rock && !dug.Contains(c))).ToList();

            List<string> layoutNotes = new List<string>(notes);
            layoutNotes.Add("excavated=" + dug.Count + "/" + Budget + " retainedThickRoof=" + retained.Count);
            layoutNotes.Add("gates=" + gates.Count + " desired=" + input.Fortification.DesiredGates);
            layoutNotes.Add("perimeterShellCells=" + reinforcement.Count);
            LayoutEquipment equipment = input.Equipment;
            List<LayoutExhaustPocket> exhaustPockets = pockets.Values.OrderBy(p => p.Cell)
                .Select(p => new LayoutExhaustPocket(p.Cell, p.Walls, p.RoofCleared, p.Excavated, p.Rooms)).ToList();
            if (exhaustPockets.Count > 0)
                layoutNotes.Add("exhaustPockets=" + exhaustPockets.Count + " roofHoles=" + exhaustPockets.Count(p => p.RoofCleared) +
                    (roofHoleBudget == int.MaxValue ? string.Empty : "/" + roofHoleBudget) + (pocketsOnly ? " pocketsOnly" : string.Empty));
            LayoutMaterialLedger materials = equipment.Count(rooms,
                built.Concat(gates.SelectMany(g => g.BandWalls)).Concat(outerSections.SelectMany(section => section.Cells)).Concat(reinforcement)
                    .Concat(exhaustPockets.SelectMany(p => p.Walls)),
                gates.Sum(g => 1 + g.BandDoors.Count), power);
            layoutNotes.Add("materials " + materials);
            // Exact accounting is enforced per attempt; the search continues on overrun.
            if (materials.ImportValue > input.Requirements.ImportValueLimit)
            {
                Fail(LayoutEquipment.MaterialBudgetExceeded, materials.ImportValue + ">" + input.Requirements.ImportValueLimit);
                return null;
            }
            ProceduralLayout layout = new ProceduralLayout(input.Digest, input.Seed, doctrine, equipment.Kit.MortarCapable, anchor, rooms,
                segments.Select(x => new LayoutCorridor(x.Id, x.Main, x.Cells)), courts.FirstOrDefault(), enclosure, built, natural, gates, dug,
                retained, constructed, input.Fortification.WallLayers, input.Requirements.FieldCells, demand.Relationships,
                Enumerable.Empty<GridPoint>(), layoutNotes, equipment.Kit.Stage, equipment.Kit.Roles.Digest, equipment.Digest,
                equipment.ConstructionStuff, materials, power, exhaustPockets, outerSections, jointSite,
                perimeterReinforcement: reinforcement, courts: courts);
            // Vanilla temperature rule on the final plan: every cooler cools its own room and
            // exhausts into a room that uses outdoor temperature.
            LayoutRoomPartition partition = LayoutRoomPartition.Build(input.Terrain, layout);
            foreach (LayoutRoom room in rooms)
                foreach (LayoutFixture cooler in room.Fixtures.Where(f => LayoutFixtureKinds.ThroughWall(f.Kind)))
                {
                    GridPoint hot = cooler.HotCell.Value;
                    GridPoint reference = new GridPoint(room.Interior.MinX, room.Interior.MinZ);
                    if (partition.RoomAt(cooler.ColdCell.Value) != partition.RoomAt(reference))
                    {
                        Fail("cooler_served_room_mismatch", room.Id + " cold " + cooler.ColdCell.Value + " " +
                            partition.Describe(cooler.ColdCell.Value) + " reference " + reference + " " + partition.Describe(reference));
                        return null;
                    }
                    if (!partition.IsOutdoor(hot))
                    {
                        bool publicExhaust = !pockets.ContainsKey(hot);
                        if (publicExhaust && !pocketsOnly) NeedsPocketsOnly = true;
                        Fail("cooler_exhaust_not_outdoor", room.Id + " hot " + hot + " " + partition.Describe(hot) + (NeedsPocketsOnly ? " -> pockets-only replan" : string.Empty));
                        return null;
                    }
                }
            return layout;
        }

        private IReadOnlyList<GridPoint> CompletePerimeter(List<GridPoint> enclosure, HashSet<GridPoint> built,
            HashSet<GridPoint> natural, List<LayoutGate> gates, List<LayoutWallSection> sections)
        {
            int layers = input.Fortification.WallLayers;
            if (layers == 1) return new GridPoint[0];
            // Chebyshev offsets close diagonal corners as well as straight
            // frontages. Radius 1 is the existing boundary; the extra shells
            // stand directly outside it at radius 2..layers, with no lane.
            int radius = layers;
            int[] distance = Enumerable.Repeat(int.MaxValue, occ.Length).ToArray();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint c in enclosure) { distance[grid.Index(c)] = 0; queue.Enqueue(c); }
            while (queue.Count > 0)
            {
                GridPoint c = queue.Dequeue();
                int next = distance[grid.Index(c)] + 1;
                if (next > radius) continue;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        GridPoint n = new GridPoint(c.X + dx, c.Z + dz);
                        if (!grid.InBounds(n) || distance[grid.Index(n)] <= next) continue;
                        distance[grid.Index(n)] = next;
                        queue.Enqueue(n);
                    }
            }
            HashSet<GridPoint> existing = new HashSet<GridPoint>(built.Concat(natural)
                .Concat(sections.SelectMany(s => s.Cells)).Concat(gates.SelectMany(g =>
                    g.BandWalls.Concat(g.BandDoors).Concat(new[] { g.Cell }))));
            // A gate's halls between its serial doors stay open; inside the
            // shell band the shells themselves flank them.
            HashSet<GridPoint> halls = new HashSet<GridPoint>(gates.SelectMany(g => g.OuterVestibule));
            HashSet<GridPoint> access = new HashSet<GridPoint>(gates.SelectMany(g =>
                g.ExteriorRoute.Concat(new[] { g.Staging })));
            HashSet<GridPoint> field = new HashSet<GridPoint>(jointSite == null ? Enumerable.Empty<GridPoint>() :
                jointSite.Field.Points().Concat(jointSite.AllPostClearance));
            List<GridPoint> cells = new List<GridPoint>();
            for (int i = 0; i < distance.Length; i++)
            {
                if (distance[i] < 2 || distance[i] > radius) continue;
                GridPoint c = new GridPoint(i % grid.Width, i / grid.Width);
                if (existing.Contains(c) || halls.Contains(c)) continue;
                if (grid.At(c) == LayoutCellClass.Rock && !dug.Contains(c)) continue;
                if ((grid.At(c) != LayoutCellClass.Open && grid.At(c) != LayoutCellClass.Covered) ||
                    access.Contains(c) || field.Contains(c) || occ[i] == GeyserReserve)
                { Fail("complete_perimeter_unavailable", "layer=" + distance[i] + " cell=" + c); return null; }
                cells.Add(c);
            }
            // A hall beyond the shell band is closed on both sides, so the
            // gatehouse stands out as a short walled passage.
            HashSet<GridPoint> placed = new HashSet<GridPoint>(cells);
            foreach (LayoutGate gate in gates)
            {
                GridPoint across = new GridPoint(gate.Outward.Z, gate.Outward.X);
                foreach (GridPoint hall in gate.OuterVestibule)
                {
                    if (distance[grid.Index(hall)] <= radius) continue;
                    foreach (int sign in new[] { -1, 1 })
                    {
                        GridPoint side = new GridPoint(hall.X + sign * across.X, hall.Z + sign * across.Z);
                        if (existing.Contains(side) || placed.Contains(side)) continue;
                        if (!grid.InBounds(side) || (grid.At(side) != LayoutCellClass.Open && grid.At(side) != LayoutCellClass.Covered) ||
                            access.Contains(side) || halls.Contains(side) || field.Contains(side) || occ[grid.Index(side)] == GeyserReserve)
                        { Fail("gatehouse_side_unavailable", "cell=" + side); return null; }
                        placed.Add(side);
                        cells.Add(side);
                    }
                }
            }
            return cells;
        }

        private List<LayoutGate> ChooseGates(bool[] inE, HashSet<GridPoint> built, HashSet<GridPoint> natural,
            HashSet<GridPoint> streets, Func<GridPoint, bool> isPublic)
        {
            HashSet<GridPoint> walls = new HashSet<GridPoint>(built.Concat(natural));
            Func<GridPoint, bool> exteriorFree = c => grid.InBounds(c) && !inE[grid.Index(c)] && !walls.Contains(c);
            int[] reach = ExteriorDistances(exteriorFree);
            int layers = input.Fortification.WallLayers;
            int s = input.Fortification.Setback;
            List<KeyValuePair<double, LayoutGate>> candidates = new List<KeyValuePair<double, LayoutGate>>();
            int noPublicSide = 0, noFlankWalls = 0, farFromStreet = 0, blockedLine = 0, unreachable = 0;
            foreach (GridPoint g in built.OrderBy(c => c))
            {
                List<GridPoint> inner = ProceduralLayoutPlanner.Cardinal.Select(d => new GridPoint(g.X + d.X, g.Z + d.Z)).Where(isPublic).ToList();
                if (inner.Count != 1) { noPublicSide++; continue; }
                GridPoint inside = inner[0];
                GridPoint outward = new GridPoint(g.X - inside.X, g.Z - inside.Z);
                GridPoint perp = new GridPoint(outward.Z, outward.X);
                if (!walls.Contains(new GridPoint(g.X + perp.X, g.Z + perp.Z)) || !walls.Contains(new GridPoint(g.X - perp.X, g.Z - perp.Z)))
                {
                    noFlankWalls++;
                    continue;
                }
                bool nearStreet = false;
                for (int dz = -s; dz <= s && !nearStreet; dz++)
                    for (int dx = -s; dx <= s && !nearStreet; dx++)
                        nearStreet = streets.Contains(new GridPoint(inside.X + dx, inside.Z + dz));
                if (!nearStreet) { farFromStreet++; continue; }
                List<GridPoint> line = new List<GridPoint>();
                bool ok = true;
                for (int k = 1; k <= 2 * layers - 1 && ok; k++)
                {
                    GridPoint c = new GridPoint(g.X + k * outward.X, g.Z + k * outward.Z);
                    ok = exteriorFree(c) && grid.Passable(c) && grid.At(c) == LayoutCellClass.Open;
                    line.Add(c);
                }
                List<GridPoint> bandDoors = new List<GridPoint>(), bandWalls = new List<GridPoint>();
                for (int layer = 1; layer < layers && ok; layer++)
                {
                    GridPoint center = new GridPoint(g.X + 2 * layer * outward.X, g.Z + 2 * layer * outward.Z);
                    bandDoors.Add(center);
                    for (int j = -2; j <= 2 && ok; j++)
                    {
                        if (j == 0) continue;
                        GridPoint c = new GridPoint(center.X + j * perp.X, center.Z + j * perp.Z);
                        ok = exteriorFree(c) && grid.At(c) == LayoutCellClass.Open;
                        bandWalls.Add(c);
                    }
                }
                if (!ok) { blockedLine++; continue; }
                GridPoint staging = line[line.Count - 1];
                int distance = reach[grid.Index(staging)];
                if (distance == int.MaxValue) { unreachable++; continue; }
                bool rockFace = natural.Contains(new GridPoint(g.X + perp.X, g.Z + perp.Z)) || natural.Contains(new GridPoint(g.X - perp.X, g.Z - perp.Z));
                double score = distance + (streets.Contains(inside) ? 0 : 3) + (doctrine == LayoutDoctrine.RockProtected && rockFace ? -20 : 0) +
                    (int)(LayoutSeed.Key(LayoutSeed.Sub(seed, "gate"), g.X, g.Z) % 6);
                if (jointSite != null && !input.Requirements.ReservePopulationCirculation)
                {
                    // Retain the established legacy layout choices. Larger
                    // rosters use the pre-reserved exact crossing below.
                    GridPoint fieldGate = jointSite.InnerGate;
                    int forward = (fieldGate.X - g.X) * outward.X + (fieldGate.Z - g.Z) * outward.Z;
                    int lateral = Math.Abs((fieldGate.X - g.X) * perp.X + (fieldGate.Z - g.Z) * perp.Z);
                    if (forward > 2 * layers && lateral <= jointSite.Field.Width / 2)
                        score -= 1000 - forward - lateral * 2;
                }
                else if (jointSite != null)
                {
                    // Prefer an actual reserved crossing, not an approximate
                    // bearing toward the field's preferred gate. A large base
                    // can otherwise choose an offset or rock-blocked gate even
                    // when another aligned, clear crossing is available.
                    foreach (P4BFieldInterface fieldInterface in jointSite.Interfaces)
                    {
                        GridPoint fieldGate = fieldInterface.InnerGate;
                        int forward = (fieldGate.X - g.X) * outward.X + (fieldGate.Z - g.Z) * outward.Z;
                        int lateral = Math.Abs((fieldGate.X - g.X) * perp.X + (fieldGate.Z - g.Z) * perp.Z);
                        if (forward <= 2 * layers || lateral != 0 ||
                            !fieldInterface.OuterGate.Equals(new GridPoint(fieldGate.X + outward.X * (jointSite.Field.Width - 1),
                                fieldGate.Z + outward.Z * (jointSite.Field.Height - 1)))) continue;
                        bool clearCrossing = Enumerable.Range(1, forward - 1).All(k =>
                        {
                            GridPoint cell = new GridPoint(g.X + k * outward.X, g.Z + k * outward.Z);
                            return input.Terrain.InBounds(cell) &&
                                LayoutTerrainGrid.Classify(input.Terrain[cell]) == LayoutCellClass.Open &&
                                !built.Contains(cell) && !natural.Contains(cell) &&
                                (occ[grid.Index(cell)] == Free || occ[grid.Index(cell)] == FieldApproachReserve);
                        });
                        if (clearCrossing) { score -= 1000 - forward; break; }
                    }
                    if (reservedCompoundGate.HasValue && g.Equals(reservedCompoundGate.Value)) score -= 10000;
                }
                HashSet<GridPoint> bandSet = new HashSet<GridPoint>(bandDoors.Concat(bandWalls));
                List<GridPoint> outerVestibule = line.Where(c => !bandSet.Contains(c) && !c.Equals(staging)).ToList();
                List<GridPoint> innerVestibule = new List<GridPoint>();
                for (int k = 0; k < input.Fortification.VestibuleDepth; k++)
                {
                    GridPoint c = new GridPoint(inside.X - k * outward.X, inside.Z - k * outward.Z);
                    if (!isPublic(c)) break;
                    innerVestibule.Add(c);
                }
                candidates.Add(new KeyValuePair<double, LayoutGate>(score, new LayoutGate(g, inside, outward, bandDoors, bandWalls,
                    outerVestibule, staging, Enumerable.Empty<GridPoint>(), innerVestibule, rockFace)));
            }
            List<LayoutGate> chosen = new List<LayoutGate>();
            HashSet<GridPoint> reserved = new HashSet<GridPoint>();
            HashSet<GridPoint> attempted = new HashSet<GridPoint>();
            foreach (KeyValuePair<double, LayoutGate> first in candidates.OrderBy(c => c.Key).ThenBy(c => c.Value.Cell))
            {
                if (chosen.Count >= input.Fortification.DesiredGates) break;
                LayoutGate gate = first.Value;
                if (chosen.Count > 0)
                {
                    // Prefer separated gates: pick the best-scoring remaining candidate adjusted by separation.
                    gate = candidates.Where(c => (!input.Requirements.ReservePopulationCirculation || !attempted.Contains(c.Value.Cell)) &&
                        chosen.All(o => Manhattan(o.Cell, c.Value.Cell) >= 10) && !Footprint(c.Value).Any(reserved.Contains))
                        .OrderBy(c => c.Key - Math.Min(40, chosen.Min(o => Manhattan(o.Cell, c.Value.Cell)))).ThenBy(c => c.Value.Cell)
                        .Select(c => c.Value).FirstOrDefault();
                    if (gate == null) break;
                }
                attempted.Add(gate.Cell);
                List<GridPoint> route = Route(gate.Staging, exteriorFree, reserved, Footprint(gate));
                if (route == null) continue;
                LayoutGate routed = new LayoutGate(gate.Cell, gate.Inside, gate.Outward, gate.BandDoors, gate.BandWalls, gate.OuterVestibule,
                    gate.Staging, route, gate.InnerVestibule, gate.RockFace);
                chosen.Add(routed);
                reserved.UnionWith(Footprint(routed));
            }
            if (chosen.Count < input.Fortification.MinimumGates)
            {
                Fail("no_gate_candidate", "candidates=" + candidates.Count + " noPublicSide=" + noPublicSide +
                    " noFlankWalls=" + noFlankWalls + " farFromStreet=" + farFromStreet + " blockedLine=" + blockedLine +
                    " unreachable=" + unreachable);
                return null;
            }
            return chosen;
        }

        private static IEnumerable<GridPoint> Footprint(LayoutGate gate) =>
            new[] { gate.Cell, gate.Staging }.Concat(gate.BandDoors).Concat(gate.BandWalls).Concat(gate.OuterVestibule);

        private int[] ExteriorDistances(Func<GridPoint, bool> exteriorFree)
        {
            int[] distance = Enumerable.Repeat(int.MaxValue, occ.Length).ToArray();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            for (int i = 0; i < occ.Length; i++)
            {
                GridPoint c = new GridPoint(i % grid.Width, i / grid.Width);
                if (grid.Terrain[c].Has(TerrainTraits.MapEdge) && grid.Walkable(c) && exteriorFree(c)) { distance[i] = 0; queue.Enqueue(c); }
            }
            // One linear flood, each cell at most once: a distance transform
            // bounded by the map, not a search. The 40k search budget left
            // inland gates on 250x250 tiles unreached and unselectable.
            int visited = 0;
            while (queue.Count > 0)
            {
                if (++visited > occ.Length) { ProceduralLayoutPlanner.Note(diagnostics, "gate", "route_budget_exhausted", Site); break; }
                GridPoint c = queue.Dequeue();
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint n = new GridPoint(c.X + step.X, c.Z + step.Z);
                    if (!exteriorFree(n) || !grid.Walkable(n)) continue;
                    int ni = grid.Index(n);
                    if (distance[ni] != int.MaxValue) continue;
                    distance[ni] = distance[grid.Index(c)] + 1;
                    queue.Enqueue(n);
                }
            }
            stats.RouteNodes += visited;
            return distance;
        }

        private List<GridPoint> Route(GridPoint start, Func<GridPoint, bool> exteriorFree, HashSet<GridPoint> reserved, IEnumerable<GridPoint> own)
        {
            HashSet<GridPoint> blocked = new HashSet<GridPoint>(reserved);
            blocked.UnionWith(own.Where(c => !c.Equals(start)));
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            previous[start] = start;
            int visited = 0;
            while (queue.Count > 0 && visited++ < input.Budget.MaxRouteNodes)
            {
                GridPoint c = queue.Dequeue();
                if (grid.Terrain[c].Has(TerrainTraits.MapEdge))
                {
                    List<GridPoint> route = new List<GridPoint>();
                    for (GridPoint p = c; ; p = previous[p]) { route.Add(p); if (p.Equals(start)) break; }
                    route.Reverse();
                    stats.RouteNodes += visited;
                    return route;
                }
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint n = new GridPoint(c.X + step.X, c.Z + step.Z);
                    if (previous.ContainsKey(n) || blocked.Contains(n) || !exteriorFree(n) || !grid.Walkable(n)) continue;
                    previous[n] = c;
                    queue.Enqueue(n);
                }
            }
            stats.RouteNodes += visited;
            return null;
        }

        private static int Manhattan(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);
    }
}
