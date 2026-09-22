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
            GridPoint facing, bool blocking, int watts, bool needsFuel, string roomId, GridPoint? exhaust = null)
        {
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

        // Vanilla rotation convention. Benches and campfires use interactionCellOffset
        // (0,0,-1): North puts the work cell south. Beds point their head at the wall.
        public string Rotation
        {
            get
            {
                GridPoint d = Kind == LayoutFixtureKind.Stove || Kind == LayoutFixtureKind.Workbench || Kind == LayoutFixtureKind.Campfire
                    ? new GridPoint(-Facing.X, -Facing.Z)
                    : Kind == LayoutFixtureKind.WallCooler ? Facing : new GridPoint(-Facing.X, -Facing.Z);
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

    public sealed class ProceduralLayout
    {
        internal ProceduralLayout(string inputDigest, ulong seed, LayoutDoctrine doctrine, bool mortarCapable, GridPoint anchor,
            IEnumerable<LayoutRoom> rooms, IEnumerable<LayoutCorridor> corridors, LayoutFixture court,
            IEnumerable<GridPoint> enclosure, IEnumerable<GridPoint> builtWalls, IEnumerable<GridPoint> naturalWalls,
            IEnumerable<LayoutGate> gates, IEnumerable<GridPoint> excavation, IEnumerable<GridPoint> retainedRoof,
            IEnumerable<GridPoint> constructedRoof, int wallLayers, int fieldCells, IEnumerable<LayoutRelationship> relationships,
            IEnumerable<GridPoint> extraObstacles, IEnumerable<string> notes)
        {
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
        public IReadOnlyList<GridPoint> NaturalWalls { get; }
        public IReadOnlyList<LayoutGate> Gates { get; }
        public IReadOnlyList<GridPoint> Excavation { get; }
        // P4A ledger extension: excavated cells that keep their natural thick roof.
        public IReadOnlyList<GridPoint> RetainedThickRoof { get; }
        public IReadOnlyList<GridPoint> ConstructedRoof { get; }
        public int WallLayers { get; }
        // Unresolved exterior interface: P4 integration must place a fertile field reachable from a gate.
        public int FieldCellsRequired { get; }
        public IReadOnlyList<LayoutRelationship> Relationships { get; }
        // Test-only planned blockers used to prove the validator sees final obstacles.
        public IReadOnlyList<GridPoint> ExtraObstacles { get; }
        public IReadOnlyList<string> Notes { get; }
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
                ExtraObstacles.Concat(obstacles ?? Enumerable.Empty<GridPoint>()), Notes);

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
            hash = StableHash.Add(hash, WallLayers);
            return StableHash.Hex(hash);
        }

        private static ulong Fixture(ulong hash, LayoutFixture fixture)
        {
            hash = StableHash.Add(hash, (int)fixture.Kind);
            hash = StableHash.Add(hash, fixture.DefName);
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

        private LayoutTerrainGrid(TerrainSnapshot terrain, int margin)
        {
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
                    Class[i] = Classify(terrain[x, z]);
                    distance[i] = Math.Min(Math.Min(x, z), Math.Min(Width - 1 - x, Height - 1 - z)) + 1;
                    if (Class[i] == LayoutCellClass.Bad) { distance[i] = 0; queue.Enqueue(i); }
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
            for (int i = 0; i < distance.Length; i++) Placeable[i] = Class[i] != LayoutCellClass.Bad && distance[i] > margin;
        }

        public static LayoutTerrainGrid Build(TerrainSnapshot terrain, int margin) => new LayoutTerrainGrid(terrain, margin);

        public static LayoutCellClass Classify(TerrainCell cell)
        {
            if (cell.HasAny(TerrainTraits.MapEdge | TerrainTraits.Water)) return LayoutCellClass.Bad;
            if (cell.Has(TerrainTraits.NaturalRock)) return LayoutCellClass.Rock;
            if (cell.Has(TerrainTraits.Occupied) || !cell.Has(TerrainTraits.Standable | TerrainTraits.Buildable)) return LayoutCellClass.Bad;
            return cell.Has(TerrainTraits.Roofed) ? LayoutCellClass.Covered : LayoutCellClass.Open;
        }

        public bool InBounds(GridPoint c) => c.X >= 0 && c.Z >= 0 && c.X < Width && c.Z < Height;
        public int Index(GridPoint c) => c.Z * Width + c.X;
        public LayoutCellClass At(GridPoint c) => InBounds(c) ? Class[Index(c)] : LayoutCellClass.Bad;
        public bool CanPlace(GridPoint c) => InBounds(c) && Placeable[Index(c)];
        public bool Roofed(GridPoint c) => InBounds(c) && Terrain[c].Has(TerrainTraits.Roofed);
        public bool ThickCover(GridPoint c) => At(c) == LayoutCellClass.Covered || (At(c) == LayoutCellClass.Rock && Roofed(c));

        // Exterior walking: dry standable unoccupied ground only.
        public bool Passable(GridPoint c)
        {
            if (!InBounds(c)) return false;
            TerrainCell cell = Terrain[c];
            return cell.Has(TerrainTraits.Standable) && !cell.HasAny(TerrainTraits.Water | TerrainTraits.Occupied | TerrainTraits.NaturalRock);
        }
    }

    public static class ProceduralLayoutPlanner
    {
        internal static readonly GridPoint[] Cardinal = { new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1) };

        public static ProceduralLayoutResult Plan(ProceduralLayoutInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            string terrainDigest = input.Terrain.Digest;
            string inputDigest = input.Digest;
            List<LayoutDiagnostic> diagnostics = new List<LayoutDiagnostic>();
            LayoutSearchStats stats = new LayoutSearchStats();
            LayoutRequirements requirements = input.Requirements;
            LayoutDemandSet demand = LayoutDemandModel.Derive(requirements, LayoutSeed.Sub(input.Seed, "rooms"));
            Func<string, ProceduralLayoutResult> reject = code =>
            {
                if (input.Terrain.Digest != terrainDigest || input.Digest != inputDigest) throw new InvalidOperationException("P4A planner mutated its input.");
                diagnostics.Add(new LayoutDiagnostic("result", code, stats.ToString()));
                return new ProceduralLayoutResult(null, demand, diagnostics, stats);
            };
            foreach (string rejection in demand.Rejections) diagnostics.Add(new LayoutDiagnostic("demand", "rejected", rejection));
            if (demand.Rejections.Count > 0) return reject("room_demand_exceeds_legal_dimensions");
            diagnostics.Add(new LayoutDiagnostic("demand", "rooms", string.Join(",", demand.Rooms.Select(r => r.Id + "=" + r.Capacity + r.Unit))));
            diagnostics.Add(new LayoutDiagnostic("demand", "relationships", string.Join(",", demand.Relationships.Select(r => r.ToString()))));

            LayoutTerrainGrid grid = LayoutTerrainGrid.Build(input.Terrain, input.Fortification.PlacementMargin);
            int requiredArea = demand.MinimumRoomArea * 3 / 2;
            List<List<int>> components = Components(grid).Where(c => c.Count >= requiredArea).ToList();
            int placeable = grid.Placeable.Count(p => p);
            if (components.Count == 0)
            {
                diagnostics.Add(new LayoutDiagnostic("site", "insufficient_usable_area", "required~" + requiredArea + " placeableCells=" + placeable +
                    " largestComponent=" + Components(grid).Select(c => c.Count).DefaultIfEmpty(0).Max()));
                return reject("insufficient_usable_area");
            }

            int protectedCells = components.Sum(c => c.Count(i => grid.ThickCover(new GridPoint(i % grid.Width, i / grid.Width))));
            bool protectionPossible = requirements.ExcavationBudgetCells > 0 && protectedCells >= requiredArea / 2;
            List<KeyValuePair<LayoutDoctrine, bool>> doctrines = new List<KeyValuePair<LayoutDoctrine, bool>>();
            if (!protectionPossible) doctrines.Add(new KeyValuePair<LayoutDoctrine, bool>(LayoutDoctrine.OpenGround, requirements.MortarCapable));
            else if (!requirements.MortarCapable)
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
            if (!protectionPossible) diagnostics.Add(new LayoutDiagnostic("doctrine", "open_ground_only", "protectedCells=" + protectedCells));

            for (int d = 0; d < doctrines.Count; d++)
            {
                LayoutDoctrine doctrine = doctrines[d].Key;
                bool wantsCourt = doctrines[d].Value;
                if (d > 0) diagnostics.Add(new LayoutDiagnostic("doctrine", "fallback", doctrine + " court=" + wantsCourt));
                List<GridPoint> anchors = Anchors(grid, components, doctrine, LayoutSeed.Sub(input.Seed, "site"), input.Budget.MaxSites);
                if (anchors.Count == 0) diagnostics.Add(new LayoutDiagnostic("site", "no_anchor", doctrine.ToString()));
                foreach (GridPoint anchor in anchors)
                {
                    if (stats.SitesTried >= input.Budget.MaxSites) break;
                    stats.SitesTried++;
                    for (int attempt = 0; attempt < input.Budget.AttemptsPerSite; attempt++)
                    {
                        if (stats.BudgetExhausted) return reject("search_budget_exhausted");
                        stats.Attempts++;
                        ulong attemptSeed = LayoutSeed.Key(LayoutSeed.Sub(input.Seed, "attempt"), anchor.X * 4099 + anchor.Z, attempt, (int)doctrine);
                        LayoutAttempt run = new LayoutAttempt(input, demand, grid, anchor, doctrine, wantsCourt, attemptSeed, stats, diagnostics);
                        ProceduralLayout layout = run.Run();
                        if (stats.BudgetExhausted && layout == null) return reject("search_budget_exhausted");
                        if (layout == null) continue;
                        IReadOnlyList<string> violations = ProceduralLayoutValidator.Validate(input, layout);
                        if (violations.Count == 0)
                        {
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
            foreach (List<int> component in components)
            {
                double cx = component.Average(i => i % grid.Width), cz = component.Average(i => i / grid.Width);
                IEnumerable<int> candidates;
                if (doctrine == LayoutDoctrine.OpenGround)
                    candidates = component.Where(i => grid.Class[i] == LayoutCellClass.Open)
                        .OrderBy(i => Math.Round(Math.Abs(i % grid.Width - cx) + Math.Abs(i / grid.Width - cz)))
                        .ThenBy(i => LayoutSeed.Key(siteSeed, i));
                else
                    // A rock face: covered cells a few steps from open ground, preferring more overhead cover nearby.
                    candidates = component.Where(i => grid.ThickCover(new GridPoint(i % grid.Width, i / grid.Width)) && toOpen[i] >= 3 && toOpen[i] <= 8)
                        .OrderByDescending(i => CoverAround(grid, i, 5) / 8).ThenBy(i => toOpen[i])
                        .ThenBy(i => LayoutSeed.Key(siteSeed, i));
                foreach (int i in candidates)
                {
                    GridPoint p = new GridPoint(i % grid.Width, i / grid.Width);
                    if (result.Any(a => Math.Abs(a.X - p.X) + Math.Abs(a.Z - p.Z) < 8)) continue;
                    result.Add(p);
                    if (result.Count >= max) return result;
                }
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
            LayoutDoctrine doctrine, bool wantsCourt, ulong seed, LayoutSearchStats stats, List<LayoutDiagnostic> diagnostics)
        {
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
        }

        private bool AllowDig => doctrine == LayoutDoctrine.RockProtected;
        private int Budget => input.Requirements.ExcavationBudgetCells;
        private string Site => anchor + "/" + doctrine;

        private void Fail(string code, string detail = "") => ProceduralLayoutPlanner.Note(diagnostics, "attempt", code, Site + " " + detail);

        public ProceduralLayout Run()
        {
            if (!BuildSpine()) return null;
            int branches = LayoutSeed.Roll(seed, "branches", 3) + demand.Rooms.Count / 6;
            for (int i = 0; i < branches; i++) AddBranch("seed-branch-" + i);
            foreach (LayoutRoomDemand room in Order())
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
                if (!done)
                {
                    Fail(stats.BudgetExhausted ? "budget_exhausted" : "room_unplaceable", room.Id + " repairs=" + repairs);
                    return null;
                }
            }
            if (wantsCourt && !PlaceCourt()) notes.Add("mortar_court_infeasible: capability lost in this layout");
            if (!wantsCourt && input.Requirements.MortarCapable) notes.Add("mortar_court_dropped_for_rock_protection");
            Trim();
            return Enclose();
        }

        private bool OkForStreet(GridPoint c) => grid.CanPlace(c) && occ[grid.Index(c)] == Free &&
            (AllowDig || grid.At(c) != LayoutCellClass.Rock);

        private bool BuildSpine()
        {
            bool vertical = LayoutSeed.Roll(seed, "axis", 2) == 0;
            GridPoint axis = vertical ? new GridPoint(0, 1) : new GridPoint(1, 0);
            GridPoint width = vertical ? new GridPoint(1, 0) : new GridPoint(0, 1);
            double[] factors = { 1.0, 1.25, 1.5 };
            int target = Math.Max(10, Math.Min(70, (int)Math.Round(Math.Sqrt(demand.MinimumRoomArea) * factors[LayoutSeed.Roll(seed, "length", 3)])));
            Func<int, List<GridPoint>> section = t => Enumerable.Range(0, MainWidth)
                .Select(k => new GridPoint(anchor.X + t * axis.X + k * width.X, anchor.Z + t * axis.Z + k * width.Z)).ToList();
            List<int> positions = new List<int>();
            if (!section(0).All(OkForStreet)) { Fail("spine_blocked"); return false; }
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
            // Power last: its generators serve whatever the other rooms draw.
            return result.Concat(demand.Rooms.Where(r => r.Kind == LayoutRoomKind.Power));
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
                    if (!grid.CanPlace(c) || occ[grid.Index(c)] != Free) return null;
                    bool edge = x == bounds.MinX || x == bounds.MaxX || z == bounds.MinZ || z == bounds.MaxZ;
                    if (grid.At(c) == LayoutCellClass.Rock)
                    {
                        if (!AllowDig) return null;
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
            int exhaustMask = 0;
            for (int x = 1; x <= w; x++)
            {
                GridPoint exhaust = ToWorld(door, facing, doorX, new GridPoint(x, -1));
                if (grid.InBounds(exhaust) && occ[grid.Index(exhaust)] == Street && grid.At(exhaust) == LayoutCellClass.Open && !dug.Contains(exhaust))
                    exhaustMask |= 1 << x;
            }
            string key = room.Id + "|" + w + "|" + h + "|" + doorX + "|" + thick + "|" + exhaustMask;
            LayoutFurnishResult furnish;
            if (!furnishCache.TryGetValue(key, out furnish))
            {
                furnish = LayoutFurnishing.Furnish(room.Kind, w, h, doorX, room.Capacity,
                    LayoutDemandModel.FurnishContext(input.Requirements, thick, x => (exhaustMask & (1 << x)) != 0));
                furnishCache[key] = furnish;
            }
            if (!furnish.Success) return null;
            foreach (LayoutLocalFixture fixture in furnish.Fixtures.Where(f => f.Kind == LayoutFixtureKind.WallCooler))
            {
                GridPoint wall = ToWorld(door, facing, doorX, fixture.Cells[0]);
                if (grid.At(wall) == LayoutCellClass.Rock) dig.Add(wall);
            }
            if (dug.Count + dig.Count > Budget && dig.Count > 0) return null;

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
                    exhaust = ToWorld(c.Door, c.Facing, c.DoorX, new GridPoint(f.Cells[0].X, -1));
                fixtures.Add(new LayoutFixture(f.Kind, f.DefName, f.Cells.Select(p => ToWorld(c.Door, c.Facing, c.DoorX, p)),
                    f.Access.Select(p => ToWorld(c.Door, c.Facing, c.DoorX, p)), DirToWorld(c.Facing, f.Facing), f.Blocking, f.Watts,
                    f.NeedsFuel, room.Id, exhaust));
            }
            HashSet<GridPoint> digSet = new HashSet<GridPoint>(c.Dig);
            List<GridPoint> naturalWalls = c.Bounds.Points().Where(p => grid.At(p) == LayoutCellClass.Rock && !digSet.Contains(p)).ToList();
            LayoutRoom placedRoom = new LayoutRoom(room, c.Bounds, c.Door, c.Facing, c.Furnish.Capacity, fixtures, c.Protected, naturalWalls);
            foreach (GridPoint p in c.Bounds.Points()) occ[grid.Index(p)] = RoomCell;
            foreach (GridPoint p in c.Dig) dug.Add(p);
            rooms.Add(placedRoom);
            placed[room.Id] = placedRoom;
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
            for (int i = 0; i < occ.Length; i++) if (occ[i] != Free) { inE[i] = true; core.Add(new GridPoint(i % grid.Width, i / grid.Width)); }
            foreach (GridPoint c in core)
                for (int dz = -s; dz <= s; dz++)
                    for (int dx = -s; dx <= s; dx++)
                    {
                        GridPoint n = new GridPoint(c.X + dx, c.Z + dz);
                        LayoutCellClass cls = grid.At(n);
                        if (cls == LayoutCellClass.Open || cls == LayoutCellClass.Covered) inE[grid.Index(n)] = true;
                    }
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
                        LayoutCellClass cls = grid.At(n);
                        if (cls == LayoutCellClass.Rock) natural.Add(n);
                        else if (cls == LayoutCellClass.Open || cls == LayoutCellClass.Covered) built.Add(n);
                        else { Fail("perimeter_cell_illegal", n.ToString()); return null; }
                    }
            }

            HashSet<GridPoint> streets = new HashSet<GridPoint>(segments.SelectMany(x => x.Cells).Concat(plaza));
            Func<GridPoint, bool> isPublic = c => grid.InBounds(c) && inE[grid.Index(c)] && occ[grid.Index(c)] != RoomCell &&
                occ[grid.Index(c)] != CourtCell && grid.At(c) != LayoutCellClass.Bad && (grid.At(c) != LayoutCellClass.Rock || dug.Contains(c));
            List<LayoutGate> gates = ChooseGates(inE, built, natural, streets, isPublic);
            if (gates == null) return null;
            foreach (LayoutGate gate in gates) built.Remove(gate.Cell);

            List<GridPoint> retained = dug.Where(grid.Roofed).ToList();
            IReadOnlyList<GridPoint> unsupported = LayoutRoofSupport.Unsupported(input.Terrain, dug, retained);
            if (unsupported.Count > 0) { Fail("roof_support_insufficient", unsupported.Count + " cells e.g. " + unsupported[0]); return null; }
            HashSet<GridPoint> retainedSet = new HashSet<GridPoint>(retained);
            List<GridPoint> constructed = rooms.SelectMany(r => r.Bounds.Points())
                .Where(c => !retainedSet.Contains(c) && !(grid.At(c) == LayoutCellClass.Rock && !dug.Contains(c))).ToList();

            List<string> layoutNotes = new List<string>(notes);
            layoutNotes.Add("excavated=" + dug.Count + "/" + Budget + " retainedThickRoof=" + retained.Count);
            layoutNotes.Add("gates=" + gates.Count + " desired=" + input.Fortification.DesiredGates);
            return new ProceduralLayout(input.Digest, input.Seed, doctrine, input.Requirements.MortarCapable, anchor, rooms,
                segments.Select(x => new LayoutCorridor(x.Id, x.Main, x.Cells)), court, enclosure, built, natural, gates, dug,
                retained, constructed, input.Fortification.WallLayers, input.Requirements.FieldCells, demand.Relationships,
                Enumerable.Empty<GridPoint>(), layoutNotes);
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
