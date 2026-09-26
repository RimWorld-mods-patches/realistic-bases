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
        public GridPoint? ColdCell => Kind == LayoutFixtureKind.WallCooler
            ? new GridPoint(Cells[0].X + Facing.X, Cells[0].Z + Facing.Z) : (GridPoint?)null;
        public GridPoint? HotCell => Kind == LayoutFixtureKind.WallCooler
            ? new GridPoint(Cells[0].X - Facing.X, Cells[0].Z - Facing.Z) : (GridPoint?)null;

        // Vanilla rotation convention. Benches and campfires use interactionCellOffset
        // (0,0,-1): North puts the work cell south. Beds point their head at the wall.
        // A cooler's rotation names its hot side, so it points away from the cooled room.
        public string Rotation
        {
            get
            {
                GridPoint d = new GridPoint(-Facing.X, -Facing.Z);
                if (d.Z > 0) return "North";
                if (d.Z < 0) return "South";
                return d.X > 0 ? "East" : "West";
            }
        }
    }

    public sealed class LayoutRoom
    {
        internal LayoutRoom(LayoutRoomDemand demand, GridRect bounds, GridPoint door, GridPoint facing, int capacity,
            IEnumerable<LayoutFixture> fixtures, bool protectedByRock, IEnumerable<GridPoint> naturalWalls)
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
        }

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
            P4BJointSite jointSite = null, P4BFieldWallPlan fieldWall = null)
        {
            JointSite = jointSite;
            FieldWall = fieldWall;
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
            Court = court;
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
        public LayoutFixture Court { get; }
        public IReadOnlyList<GridPoint> Enclosure { get; }
        public IReadOnlyList<GridPoint> BuiltWalls { get; }
        public IReadOnlyList<LayoutWallSection> OuterSections { get; }
        public IEnumerable<GridPoint> OuterWalls => OuterSections.SelectMany(s => s.Cells);
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
        public IReadOnlyList<LayoutRelationship> Relationships { get; }
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

        public IEnumerable<LayoutFixture> AllFixtures => Rooms.SelectMany(r => r.Fixtures).Concat(Court == null ? Enumerable.Empty<LayoutFixture>() : new[] { Court });

        public IReadOnlyList<LayoutPortal> Portals => new ReadOnlyCollection<LayoutPortal>(
            Gates.Select((g, i) => new LayoutPortal(LayoutPortalKind.Gate, g.Cell, g.Inside, "gate-" + i))
                .Concat(Rooms.Select(r => new LayoutPortal(LayoutPortalKind.RoomDoor, r.Door, r.DoorOutside, r.Id))).ToList());

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
                ExhaustPockets, OuterSections, JointSite, FieldWall);

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
                gates.Sum(g => 1 + g.BandDoors.Count) + 2, Power);
            return new ProceduralLayout(InputDigest, Seed, Doctrine, MortarCapable, Anchor, Rooms, Corridors, Court,
                Enclosure, BuiltWalls, NaturalWalls, gates, Excavation, RetainedThickRoof, ConstructedRoof,
                WallLayers, FieldCellsRequired, Relationships, ExtraObstacles,
                Notes.Concat(new[] { "fieldWall=" + fieldWall.Digest + " materials " + materials }),
                TechStage, RolesDigest, EquipmentDigest, WallStuff, materials, Power,
                ExhaustPockets, OuterSections, JointSite, fieldWall);
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
                hash = StableHash.Add(hash, room.Capacity);
                foreach (LayoutFixture fixture in room.Fixtures) hash = Fixture(hash, fixture);
            }
            foreach (LayoutCorridor corridor in Corridors)
            {
                hash = StableHash.Add(hash, corridor.Id);
                foreach (GridPoint c in corridor.Cells) hash = Point(hash, c);
            }
            if (Court != null) hash = Fixture(hash, Court);
            foreach (IReadOnlyList<GridPoint> list in new[] { Enclosure, BuiltWalls, NaturalWalls, Excavation, RetainedThickRoof, ConstructedRoof, ExtraObstacles })
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
            hash = StableHash.Add(hash, TechStage == null ? string.Empty : TechStage.Digest);
            hash = StableHash.Add(hash, RolesDigest);
            hash = StableHash.Add(hash, EquipmentDigest);
            hash = StableHash.Add(hash, WallStuff);
            if (Materials != null) hash = Materials.AddTo(hash);
            hash = StableHash.Add(hash, Power == null ? string.Empty : Power.Digest);
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
    }

    public static class ProceduralLayoutPlanner
    {
        internal static readonly GridPoint[] Cardinal = { new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1) };

        public static ProceduralLayoutResult Plan(ProceduralLayoutInput input, bool requireJointSite = false)
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
                    input.Budget.MaxSites, input.Budget.MaxSites * 1024);
                diagnostics.Add(new LayoutDiagnostic("joint_site", "preflight", "pairs=" + jointSites.Candidates.Count +
                    " evaluated=" + jointSites.Evaluated + " rejected=" + string.Join(",", jointSites.Rejections)));
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

            for (int d = 0; d < doctrines.Count; d++)
            {
                LayoutDoctrine doctrine = doctrines[d].Key;
                bool wantsCourt = doctrines[d].Value;
                if (d > 0) diagnostics.Add(new LayoutDiagnostic("doctrine", "fallback", doctrine + " court=" + wantsCourt));
                int sitesRemaining = input.Budget.MaxSites - stats.SitesTried;
                int siteQuota = d == doctrines.Count - 1 ? sitesRemaining : Math.Max(1, sitesRemaining / (doctrines.Count - d));
                int siteLimit = stats.SitesTried + siteQuota;
                List<P4BJointSite> paired = jointSites == null ? null : jointSites.Candidates
                    .Skip(stats.SitesTried).Take(siteQuota).ToList();
                List<GridPoint> anchors = paired == null
                    ? Anchors(grid, components, doctrine, LayoutSeed.Sub(input.Seed, "site"), siteQuota)
                    : paired.Select(s => s.Anchor).ToList();
                if (anchors.Count == 0) diagnostics.Add(new LayoutDiagnostic("site", "no_anchor", doctrine.ToString()));
                for (int siteIndex = 0; siteIndex < anchors.Count; siteIndex++)
                {
                    GridPoint anchor = anchors[siteIndex];
                    P4BJointSite site = paired == null ? null : paired[siteIndex];
                    LayoutTerrainGrid siteGrid = site == null ? grid : grid.RestrictTo(site.Compound);
                    if (stats.SitesTried >= siteLimit) break;
                    stats.SitesTried++;
                    for (int attempt = 0; attempt < input.Budget.AttemptsPerSite; attempt++)
                    {
                        if (stats.BudgetExhausted) return reject("search_budget_exhausted");
                        stats.Attempts++;
                        ulong attemptSeed = LayoutSeed.Key(LayoutSeed.Sub(input.Seed, "attempt"), anchor.X * 4099 + anchor.Z, attempt, (int)doctrine);
                        LayoutAttempt run = new LayoutAttempt(input, demand, siteGrid, anchor, doctrine, wantsCourt, attemptSeed, stats, diagnostics,
                            jointSite: site);
                        ProceduralLayout layout = run.Run();
                        if (layout == null && run.NeedsPocketsOnly && !stats.BudgetExhausted)
                        {
                            // The public room failed vanilla's outdoor rule: replan this attempt with pocket exhaust only.
                            stats.Attempts++;
                            run = new LayoutAttempt(input, demand, siteGrid, anchor, doctrine, wantsCourt, attemptSeed, stats, diagnostics, true, site);
                            layout = run.Run();
                        }
                        if (stats.BudgetExhausted && layout == null) return reject("search_budget_exhausted");
                        if (layout == null) continue;
                        IReadOnlyList<string> violations = ProceduralLayoutValidator.Validate(input, layout);
                        if (violations.Count == 0)
                        {
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
                                    continue;
                                }
                                layout = layout.WithFieldWall(input, fieldWall);
                                violations = ProceduralLayoutValidator.Validate(input, layout);
                                if (violations.Count != 0)
                                {
                                    Note(diagnostics, "joint_site", "final_rejected", anchor + " " + string.Join("+", violations.Take(6)));
                                    continue;
                                }
                            }
                            if (input.Terrain.Digest != terrainDigest || input.Digest != inputDigest) throw new InvalidOperationException("P4A planner mutated its input.");
                            diagnostics.Add(new LayoutDiagnostic("result", "accepted", stats + " digest=" + layout.Digest));
                            return new ProceduralLayoutResult(layout, demand, diagnostics, stats);
                        }
                        Note(diagnostics, "validate", "rejected", anchor + " " + string.Join("+", violations.Take(6)));
                    }
                }
            }
            return reject("no_valid_layout");
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
    internal sealed class LayoutAttempt
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
        private const int MainWidth = 2;

        private readonly ProceduralLayoutInput input;
        private readonly LayoutDemandSet demand;
        private readonly LayoutTerrainGrid grid;
        private readonly GridPoint anchor;
        private readonly LayoutDoctrine doctrine;
        private readonly bool wantsCourt;
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
        private LayoutFixture court;
        private int repairs;
        private readonly List<LayoutPowerYard> yards = new List<LayoutPowerYard>();
        private readonly List<string> powerDiagnostics = new List<string>();
        private LayoutPowerPlan power;
        private readonly bool pocketsOnly;
        private readonly P4BJointSite jointSite;
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
            public GridPoint Axis;
            public GridPoint Root;
            public List<List<GridPoint>> Sections = new List<List<GridPoint>>();
            public IEnumerable<GridPoint> Cells => Sections.SelectMany(s => s);
        }

        public LayoutAttempt(ProceduralLayoutInput input, LayoutDemandSet demand, LayoutTerrainGrid grid, GridPoint anchor,
            LayoutDoctrine doctrine, bool wantsCourt, ulong seed, LayoutSearchStats stats, List<LayoutDiagnostic> diagnostics,
            bool pocketsOnly = false, P4BJointSite jointSite = null)
        {
            this.pocketsOnly = pocketsOnly;
            this.jointSite = jointSite;
            this.input = input;
            this.demand = demand;
            this.grid = grid;
            this.anchor = anchor;
            this.doctrine = doctrine;
            this.wantsCourt = wantsCourt;
            this.seed = seed;
            this.stats = stats;
            this.diagnostics = diagnostics;
            occ = new byte[grid.Width * grid.Height];
            // Rock-protect doctrine: at most one cleared exhaust hole per two cooled rooms (provisional).
            int cooled = demand.Rooms.Count(r => r.Kind == LayoutRoomKind.Freezer ||
                (input.Requirements.Climate.CoolingRequired && LayoutDemandModel.IsLiving(r.Kind)));
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
            if (!BuildSpine()) return null;
            int branches = LayoutSeed.Roll(seed, "branches", 3) + demand.Rooms.Count / 6;
            for (int i = 0; i < branches; i++) AddBranch("seed-branch-" + i);
            foreach (LayoutRoomDemand room in Order())
                if (!PlaceWithRepairs(room)) return null;
            if (wantsCourt && !PlaceCourt()) notes.Add("mortar_court_infeasible: capability lost in this layout");
            if (!wantsCourt && input.Equipment.Kit.MortarCapable) notes.Add("mortar_court_dropped_for_rock_protection");
            if (input.Equipment.Kit.Powered)
            {
                PlaceYards();
                for (int i = 0; i < occ.Length; i++) if (occ[i] == GeothermalReserve) occ[i] = Free;
                if (!PlacePower()) return null;
            }
            Trim();
            return Enclose();
        }

        private bool PlaceWithRepairs(LayoutRoomDemand room)
        {
            bool done = TryPlaceRoom(room);
            while (!done && !stats.BudgetExhausted && repairs < input.Budget.MaxRepairsPerAttempt)
            {
                repairs++;
                stats.Repairs++;
                if (!AddBranch("repair-" + repairs))
                {
                    Fail("repair_failed", "no branch for " + room.Id);
                    break;
                }
                ProceduralLayoutPlanner.Note(diagnostics, "repair", "branch_added", Site + " for " + room.Id);
                done = TryPlaceRoom(room);
            }
            if (!done) Fail(stats.BudgetExhausted ? "budget_exhausted" : "room_unplaceable", room.Id + " repairs=" + repairs);
            return done;
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
            int batteries = LayoutPowerModel.Batteries(critical);
            int generators = LayoutPowerModel.Generators(critical, nonCritical, renewable, kit.GeneratorWatts);
            LayoutRoomDemand demand = null;
            for (int guard = 0; guard <= LayoutDemandModel.MaxSplit; guard++)
            {
                demand = equipment.PowerRoom(input.Requirements, generators, batteries, input.Seed);
                if (demand == null) { Fail("power_room_unbuildable", generators + " generators"); return false; }
                int own = LayoutDemandModel.MaxLoad(demand, input.Requirements, kit);
                int needed = LayoutPowerModel.Generators(critical, nonCritical + own, renewable, kit.GeneratorWatts);
                if (needed <= generators) break;
                generators = needed;
            }
            if (!PlaceWithRepairs(demand)) return false;
            power = new LayoutPowerPlan(critical, nonCritical, kit.Generator, generators, batteries, yards, kit.GeneratorFuel,
                LayoutPowerModel.FuelStock(kit.Generator, generators), powerDiagnostics);
            return true;
        }

        private bool OkForStreet(GridPoint c) => grid.CanPlace(c) && occ[grid.Index(c)] == Free &&
            (grid.At(c) != LayoutCellClass.Rock || CanExcavate(c));

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

        private IEnumerable<LayoutRoomDemand> Order()
        {
            List<LayoutRoomDemand> remaining = demand.Rooms.Where(r => r.Kind != LayoutRoomKind.Power).ToList();
            List<LayoutRoomDemand> result = new List<LayoutRoomDemand>();
            HashSet<string> done = new HashSet<string>();
            while (remaining.Count > 0)
            {
                LayoutRoomDemand next = remaining
                    .OrderByDescending(r => done.Count == 0 ? 0 : demand.Relationships
                        .Where(rel => (rel.A == r.Id && done.Contains(rel.B)) || (rel.B == r.Id && done.Contains(rel.A))).Sum(rel => rel.Weight))
                    .ThenByDescending(r => r.MinimumBoundsArea)
                    .ThenBy(r => LayoutSeed.Key(LayoutSeed.Sub(seed, r.Id), done.Count)).First();
                result.Add(next);
                done.Add(next.Id);
                remaining.Remove(next);
            }
            // The power room is placed separately, after the yards (PlacePower).
            return result;
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
        }

        private bool TryPlaceRoom(LayoutRoomDemand room)
        {
            Candidate best = null;
            int evaluated = 0;
            List<GridPoint> frontage = segments.SelectMany(s => s.Cells).Distinct()
                .OrderBy(c => LayoutSeed.Key(LayoutSeed.Sub(seed, room.Id), c.X, c.Z, repairs)).ToList();
            foreach (GridPoint street in frontage)
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
            int protectedCells = 0;
            for (int z = bounds.MinZ; z <= bounds.MaxZ; z++)
                for (int x = bounds.MinX; x <= bounds.MaxX; x++)
                {
                    GridPoint c = new GridPoint(x, z);
                    bool edge = x == bounds.MinX || x == bounds.MaxX || z == bounds.MinZ || z == bounds.MaxZ;
                    byte o = grid.CanPlace(c) ? occ[grid.Index(c)] : (byte)255;
                    // A pocket's reserved wall or rock seal may become this room's wall (never its door).
                    bool absorbable = edge && !c.Equals(door) && (o == PocketWall || o == PocketSeal);
                    if (o != Free && !absorbable) return null;
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
            bool needsCoolers = room.Kind == LayoutRoomKind.Freezer ||
                (input.Requirements.Climate.CoolingRequired && LayoutDemandModel.IsLiving(room.Kind) && !thick);
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
            foreach (LayoutLocalFixture cooler in furnish.Fixtures.Where(f => f.Kind == LayoutFixtureKind.WallCooler))
            {
                GridPoint hotCell = ToWorld(door, facing, doorX, new GridPoint(cooler.Cells[0].X - cooler.Facing.X, cooler.Cells[0].Z - cooler.Facing.Z));
                if (occ[grid.Index(hotCell)] != Free) continue;
                if (grid.Roofed(hotCell)) newHoles++;
                if (grid.At(hotCell) == LayoutCellClass.Rock) newDigs++;
            }
            if (pockets.Values.Count(p => p.RoofCleared) + newHoles > roofHoleBudget) return null;
            foreach (LayoutLocalFixture fixture in furnish.Fixtures.Where(f => f.Kind == LayoutFixtureKind.WallCooler))
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
            GridPoint center = new GridPoint(bounds.MinX + bounds.Width / 2, bounds.MinZ + bounds.Height / 2);
            score += 0.5 * Manhattan(center, anchor);
            if (doctrine == LayoutDoctrine.RockProtected) score += dig.Count - 4.0 * protectedCells;
            score += (int)(LayoutSeed.Key(seed, door.X * 7 + facing.X, door.Z * 7 + facing.Z, w * 16 + h) % 7);
            return new Candidate { Score = score, Bounds = bounds, Door = door, Facing = facing, DoorX = doorX, Width = w,
                Furnish = furnish, Dig = dig, Protected = thick };
        }

        private void Commit(LayoutRoomDemand room, Candidate c)
        {
            List<LayoutFixture> fixtures = new List<LayoutFixture>();
            foreach (LayoutLocalFixture f in c.Furnish.Fixtures)
            {
                GridPoint? exhaust = null;
                if (f.Kind == LayoutFixtureKind.WallCooler)
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
            foreach (LayoutFixture cooler in fixtures.Where(f => f.Kind == LayoutFixtureKind.WallCooler)) ClaimExhaust(cooler, room.Id);
            rooms.Add(placedRoom);
            placed[room.Id] = placedRoom;
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

        private bool PlaceCourt()
        {
            Candidate best = null;
            List<GridPoint> access = null;
            foreach (GridPoint street in segments.SelectMany(s => s.Cells).OrderBy(p => LayoutSeed.Key(LayoutSeed.Sub(seed, "court"), p.X, p.Z)))
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
                    if (!cells.All(x => grid.CanPlace(x) && occ[grid.Index(x)] == Free && grid.At(x) == LayoutCellClass.Open &&
                        grid.Terrain[x].Has(TerrainTraits.SupportsHeavy))) continue;
                    double score = Manhattan(street, anchor) + (int)(LayoutSeed.Key(seed, street.X, street.Z, f.X * 3 + f.Z) % 5);
                    if (best != null && score >= best.Score) continue;
                    best = new Candidate { Score = score, Door = street, Facing = f, Bounds = new GridRect(Math.Min(cells[0].X, cells[3].X),
                        Math.Min(cells[0].Z, cells[3].Z), 2, 2) };
                    access = new List<GridPoint> { street };
                }
            if (best == null) return false;
            court = new LayoutFixture(LayoutFixtureKind.MortarCourt, "MortarCourtReservation", best.Bounds.Points(), access,
                new GridPoint(-best.Facing.X, -best.Facing.Z), true, 0, false, "court");
            foreach (GridPoint c in best.Bounds.Points()) occ[grid.Index(c)] = CourtCell;
            return true;
        }

        // Remove street sections that serve nothing: no door, cooler exhaust,
        // court access, or attached branch. Branch roots are never trimmed.
        private void Trim()
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                HashSet<GridPoint> used = new HashSet<GridPoint>(rooms.Select(r => r.DoorOutside));
                foreach (LayoutFixture f in rooms.SelectMany(r => r.Fixtures).Where(f => f.Exhaust.HasValue)) used.Add(f.Exhaust.Value);
                if (court != null) used.UnionWith(court.Access);
                foreach (LayoutPowerYard yard in yards)
                {
                    used.Add(yard.Access);
                    used.UnionWith(yard.ZoneCells);
                }
                foreach (Segment branch in segments.Where(s => !s.Main && s.Sections.Count > 0)) used.Add(branch.Root);
                foreach (Segment segment in segments)
                {
                    if (segment.Main) continue;
                    while (segment.Sections.Count > 1 && !segment.Sections[segment.Sections.Count - 1].Any(used.Contains))
                    { Release(segment.Sections[segment.Sections.Count - 1]); segment.Sections.RemoveAt(segment.Sections.Count - 1); changed = true; }
                    if (segment.Sections.Count == 1 && !segment.Sections[0].Any(used.Contains))
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
            for (int i = 0; i < occ.Length; i++) if (occ[i] != Free && occ[i] != GeyserReserve) { inE[i] = true; core.Add(new GridPoint(i % grid.Width, i / grid.Width)); }
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
            List<LayoutWallSection> outerSections = ExtendGateBands(inE, built, natural, gates);
            if (outerSections.Count != gates.Count * (input.Fortification.WallLayers - 1))
            {
                Fail("unreturned_outer_wall_section", "requestedLayers=" + input.Fortification.WallLayers +
                    " gates=" + gates.Count + " returnedSections=" + outerSections.Count);
                return null;
            }

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
            layoutNotes.Add("outerSections=" + outerSections.Count + "/" + (gates.Count * (input.Fortification.WallLayers - 1)) +
                " outerWallCells=" + outerSections.Sum(section => section.Cells.Count()));
            LayoutEquipment equipment = input.Equipment;
            List<LayoutExhaustPocket> exhaustPockets = pockets.Values.OrderBy(p => p.Cell)
                .Select(p => new LayoutExhaustPocket(p.Cell, p.Walls, p.RoofCleared, p.Excavated, p.Rooms)).ToList();
            if (exhaustPockets.Count > 0)
                layoutNotes.Add("exhaustPockets=" + exhaustPockets.Count + " roofHoles=" + exhaustPockets.Count(p => p.RoofCleared) +
                    (roofHoleBudget == int.MaxValue ? string.Empty : "/" + roofHoleBudget) + (pocketsOnly ? " pocketsOnly" : string.Empty));
            LayoutMaterialLedger materials = equipment.Count(rooms,
                built.Concat(gates.SelectMany(g => g.BandWalls)).Concat(outerSections.SelectMany(section => section.Cells))
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
                segments.Select(x => new LayoutCorridor(x.Id, x.Main, x.Cells)), court, enclosure, built, natural, gates, dug,
                retained, constructed, input.Fortification.WallLayers, input.Requirements.FieldCells, demand.Relationships,
                Enumerable.Empty<GridPoint>(), layoutNotes, equipment.Kit.Stage, equipment.Kit.Roles.Digest, equipment.Digest,
                equipment.ConstructionStuff, materials, power, exhaustPockets, outerSections, jointSite);
            // Vanilla temperature rule on the final plan: every cooler cools its own room and
            // exhausts into a room that uses outdoor temperature.
            LayoutRoomPartition partition = LayoutRoomPartition.Build(input.Terrain, layout);
            foreach (LayoutRoom room in rooms)
                foreach (LayoutFixture cooler in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.WallCooler))
                {
                    GridPoint hot = cooler.HotCell.Value;
                    if (partition.RoomAt(cooler.ColdCell.Value) != partition.RoomAt(new GridPoint(room.Interior.MinX, room.Interior.MinZ)) ||
                        !partition.IsOutdoor(hot))
                    {
                        bool publicExhaust = !pockets.ContainsKey(hot);
                        if (publicExhaust && !pocketsOnly) NeedsPocketsOnly = true;
                        Fail("cooler_exhaust_not_outdoor", room.Id + " hot " + hot + " " + partition.Describe(hot) + (NeedsPocketsOnly ? " -> pockets-only replan" : string.Empty));
                        return null;
                    }
                }
            return layout;
        }

        private List<LayoutWallSection> ExtendGateBands(bool[] inE, HashSet<GridPoint> built,
            HashSet<GridPoint> natural, List<LayoutGate> gates)
        {
            // Only extend a straight, already enclosed wall run whose parallel
            // exterior terrain is clear. A short band remains a band, not a
            // declared layer section. One-cell lane spacing preserves the serial
            // gate crossing, with bounded side arms for material discipline.
            const int MaxArm = 8;
            HashSet<GridPoint> baseline = new HashSet<GridPoint>(built.Concat(natural).Concat(gates.Select(g => g.Cell)));
            HashSet<GridPoint> reserved = new HashSet<GridPoint>(gates.SelectMany(g => Footprint(g).Concat(g.ExteriorRoute)));
            List<LayoutWallSection> result = new List<LayoutWallSection>();
            for (int i = 0; i < gates.Count; i++)
            {
                LayoutGate gate = gates[i];
                GridPoint tangent = new GridPoint(gate.Outward.Z, gate.Outward.X);
                for (int layer = 1; layer < input.Fortification.WallLayers; layer++)
                {
                    LayoutWallSection inner = layer > 1 ? result.FirstOrDefault(s => s.GateIndex == i && s.Layer == layer - 1) : null;
                    if (layer > 1 && inner == null) break;
                    List<GridPoint> left = Arm(-1), right = Arm(1);
                    // A side with no straight arm still needs a short return
                    // around the gate-band corner; otherwise it is a pier.
                    if (left.Count + right.Count < 3) break;
                    List<GridPoint> leftReturn = Return(-1, left), rightReturn = Return(1, right);
                    if (leftReturn == null || rightReturn == null) break;
                    LayoutWallSection section = new LayoutWallSection(i, layer, left, right, leftReturn, rightReturn);
                    result.Add(section);
                    reserved.UnionWith(section.Cells);

                    List<GridPoint> Arm(int sign)
                    {
                        List<GridPoint> cells = new List<GridPoint>();
                        // The first two lane cells sit behind the gate-band
                        // junction; without them the apparent arm has no
                        // walkable access from its own crossing.
                        if (!LaneClear(1) || !LaneClear(2)) return cells;
                        for (int j = 3; j <= MaxArm + 2; j++)
                        {
                            // A farther-out run cannot extend beyond its inner run;
                            // the intervening cell is its continuous service and
                            // retreat lane back to the gate crossing.
                            if (inner != null && j - 3 >= (sign < 0 ? inner.Left.Count : inner.Right.Count)) break;
                            GridPoint baseCell = new GridPoint(gate.Cell.X + j * sign * tangent.X,
                                gate.Cell.Z + j * sign * tangent.Z);
                            GridPoint cell = new GridPoint(baseCell.X + 2 * layer * gate.Outward.X,
                                baseCell.Z + 2 * layer * gate.Outward.Z);
                            GridPoint inside = new GridPoint(baseCell.X - gate.Outward.X, baseCell.Z - gate.Outward.Z);
                            if (!baseline.Contains(baseCell) || !grid.InBounds(inside) || !inE[grid.Index(inside)] ||
                                !grid.InBounds(cell) || grid.At(cell) != LayoutCellClass.Open ||
                                grid.Terrain[cell].Has(TerrainTraits.MapEdge) || reserved.Contains(cell) ||
                                !LaneClear(j)) break;
                            cells.Add(cell);
                        }
                        return cells;

                        bool LaneClear(int distance)
                        {
                            GridPoint lane = new GridPoint(gate.Cell.X + distance * sign * tangent.X + (2 * layer - 1) * gate.Outward.X,
                                gate.Cell.Z + distance * sign * tangent.Z + (2 * layer - 1) * gate.Outward.Z);
                            return grid.InBounds(lane) && grid.At(lane) == LayoutCellClass.Open && grid.Passable(lane) &&
                                !grid.Terrain[lane].Has(TerrainTraits.MapEdge) && !reserved.Contains(lane) && !inE[grid.Index(lane)];
                        }
                    }

                    List<GridPoint> Return(int sign, List<GridPoint> arm)
                    {
                        List<GridPoint> path = new List<GridPoint>();
                        if (arm.Count > 0)
                        {
                            GridPoint end = arm[arm.Count - 1];
                            path.Add(new GridPoint(end.X - gate.Outward.X, end.Z - gate.Outward.Z));
                        }
                        else
                        {
                            // At the five-wide band's tip, layer 1 bends one
                            // cell sideways to the known baseline wall at j=1.
                            // Farther layers return to the preceding band at j=2.
                            path.Add(new GridPoint(gate.Cell.X + 2 * sign * tangent.X + (2 * layer - 1) * gate.Outward.X,
                                gate.Cell.Z + 2 * sign * tangent.Z + (2 * layer - 1) * gate.Outward.Z));
                            if (layer == 1)
                                path.Add(new GridPoint(gate.Cell.X + sign * tangent.X + gate.Outward.X,
                                    gate.Cell.Z + sign * tangent.Z + gate.Outward.Z));
                        }
                        GridPoint terminal = path[path.Count - 1];
                        GridPoint parent = new GridPoint(terminal.X - gate.Outward.X, terminal.Z - gate.Outward.Z);
                        if (arm.Count == 0 && layer == 1)
                            parent = new GridPoint(gate.Cell.X + sign * tangent.X, gate.Cell.Z + sign * tangent.Z);
                        if (!(layer == 1 ? baseline.Contains(parent) :
                            arm.Count == 0 ? gate.BandWalls.Contains(parent) :
                            (sign < 0 ? inner.Left : inner.Right).Contains(parent))) return null;
                        foreach (GridPoint c in path)
                            if (!grid.InBounds(c) || grid.At(c) != LayoutCellClass.Open ||
                                grid.Terrain[c].Has(TerrainTraits.MapEdge) || inE[grid.Index(c)] || reserved.Contains(c)) return null;
                        return path;
                    }
                }
            }
            return result;
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
            foreach (GridPoint g in built.OrderBy(c => c))
            {
                List<GridPoint> inner = ProceduralLayoutPlanner.Cardinal.Select(d => new GridPoint(g.X + d.X, g.Z + d.Z)).Where(isPublic).ToList();
                if (inner.Count != 1) continue;
                GridPoint inside = inner[0];
                GridPoint outward = new GridPoint(g.X - inside.X, g.Z - inside.Z);
                GridPoint perp = new GridPoint(outward.Z, outward.X);
                if (!walls.Contains(new GridPoint(g.X + perp.X, g.Z + perp.Z)) || !walls.Contains(new GridPoint(g.X - perp.X, g.Z - perp.Z))) continue;
                bool nearStreet = false;
                for (int dz = -s; dz <= s && !nearStreet; dz++)
                    for (int dx = -s; dx <= s && !nearStreet; dx++)
                        nearStreet = streets.Contains(new GridPoint(inside.X + dx, inside.Z + dz));
                if (!nearStreet) continue;
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
                if (!ok) continue;
                GridPoint staging = line[line.Count - 1];
                int distance = reach[grid.Index(staging)];
                if (distance == int.MaxValue) continue;
                bool rockFace = natural.Contains(new GridPoint(g.X + perp.X, g.Z + perp.Z)) || natural.Contains(new GridPoint(g.X - perp.X, g.Z - perp.Z));
                double score = distance + (streets.Contains(inside) ? 0 : 3) + (doctrine == LayoutDoctrine.RockProtected && rockFace ? -20 : 0) +
                    (int)(LayoutSeed.Key(LayoutSeed.Sub(seed, "gate"), g.X, g.Z) % 6);
                if (jointSite != null)
                {
                    GridPoint fieldGate = jointSite.InnerGate;
                    int forward = (fieldGate.X - g.X) * outward.X + (fieldGate.Z - g.Z) * outward.Z;
                    int lateral = Math.Abs((fieldGate.X - g.X) * perp.X + (fieldGate.Z - g.Z) * perp.Z);
                    if (forward > 2 * layers && lateral <= jointSite.Field.Width / 2)
                        score -= 1000 - forward - lateral * 2;
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
            foreach (KeyValuePair<double, LayoutGate> first in candidates.OrderBy(c => c.Key).ThenBy(c => c.Value.Cell))
            {
                if (chosen.Count >= input.Fortification.DesiredGates) break;
                LayoutGate gate = first.Value;
                if (chosen.Count > 0)
                {
                    // Prefer separated gates: pick the best-scoring remaining candidate adjusted by separation.
                    gate = candidates.Where(c => chosen.All(o => Manhattan(o.Cell, c.Value.Cell) >= 10) && !Footprint(c.Value).Any(reserved.Contains))
                        .OrderBy(c => c.Key - Math.Min(40, chosen.Min(o => Manhattan(o.Cell, c.Value.Cell)))).ThenBy(c => c.Value.Cell)
                        .Select(c => c.Value).FirstOrDefault();
                    if (gate == null) break;
                }
                List<GridPoint> route = Route(gate.Staging, exteriorFree, reserved, Footprint(gate));
                if (route == null) continue;
                LayoutGate routed = new LayoutGate(gate.Cell, gate.Inside, gate.Outward, gate.BandDoors, gate.BandWalls, gate.OuterVestibule,
                    gate.Staging, route, gate.InnerVestibule, gate.RockFace);
                chosen.Add(routed);
                reserved.UnionWith(Footprint(routed));
            }
            if (chosen.Count < input.Fortification.MinimumGates)
            {
                Fail("no_gate_candidate", "candidates=" + candidates.Count);
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
                if (grid.Terrain[c].Has(TerrainTraits.MapEdge) && grid.Passable(c) && exteriorFree(c)) { distance[i] = 0; queue.Enqueue(c); }
            }
            int visited = 0;
            while (queue.Count > 0)
            {
                if (++visited > input.Budget.MaxRouteNodes) { ProceduralLayoutPlanner.Note(diagnostics, "gate", "route_budget_exhausted", Site); break; }
                GridPoint c = queue.Dequeue();
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint n = new GridPoint(c.X + step.X, c.Z + step.Z);
                    if (!exteriorFree(n) || !grid.Passable(n)) continue;
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
                    if (previous.ContainsKey(n) || blocked.Contains(n) || !exteriorFree(n) || !grid.Passable(n)) continue;
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
