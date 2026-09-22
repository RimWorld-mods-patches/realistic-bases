using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public enum PhysicalPlacementKind
    {
        OrdinaryBed,
        PrisonBed,
        MedicalBed,
        Workbench,
        Dining,
        Recreation,
        StorageMarker,
        Generator,
        Battery,
        Conduit,
        Cooler,
        FoodStock,
        FuelStock,
        Cover,
        Turret
    }

    public sealed class PhysicalFacility
    {
        public PhysicalFacility(string id, FacilityKind kind, GridRect bounds, GridPoint door,
            IEnumerable<GridPoint> interactionCells, int capacity, bool roofed)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            Bounds = bounds;
            Door = door;
            InteractionCells = new ReadOnlyCollection<GridPoint>((interactionCells ?? Enumerable.Empty<GridPoint>()).ToList());
            Capacity = capacity;
            Roofed = roofed;
        }

        public string Id { get; }
        public FacilityKind Kind { get; }
        public GridRect Bounds { get; }
        public GridPoint Door { get; }
        public IReadOnlyList<GridPoint> InteractionCells { get; }
        public int Capacity { get; }
        public bool Roofed { get; }
    }

    public sealed class PhysicalPlacement
    {
        public PhysicalPlacement(PhysicalPlacementKind kind, string defName, string stuffDefName,
            GridPoint cell, string rotation, string facilityId, bool imported, int value)
        {
            Kind = kind;
            DefName = defName ?? string.Empty;
            StuffDefName = stuffDefName ?? string.Empty;
            Cell = cell;
            Rotation = rotation ?? "North";
            FacilityId = facilityId ?? string.Empty;
            Imported = imported;
            Value = value;
        }

        public PhysicalPlacementKind Kind { get; }
        public string DefName { get; }
        public string StuffDefName { get; }
        public GridPoint Cell { get; }
        public string Rotation { get; }
        public string FacilityId { get; }
        public bool Imported { get; }
        public int Value { get; }
    }

    public sealed class PhysicalPlanningInput
    {
        public PhysicalPlanningInput(PhysicalSettlementProfile profile, TerrainSnapshot terrain, ulong seed,
            IEnumerable<string> localStuffDefs, int maxCandidates, TerrainArchetype archetype = TerrainArchetype.Flat,
            int wallLayers = 1)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            if (maxCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
            if (wallLayers < 1 || wallLayers > 3) throw new ArgumentOutOfRangeException(nameof(wallLayers));
            Seed = seed;
            LocalStuffDefs = new ReadOnlyCollection<string>((localStuffDefs ?? Enumerable.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList());
            if (LocalStuffDefs.Count == 0) throw new ArgumentException("At least one local construction material is required.");
            MaxCandidates = maxCandidates;
            Archetype = archetype;
            WallLayers = wallLayers;
            Digest = ComputeDigest();
        }

        public PhysicalSettlementProfile Profile { get; }
        public TerrainSnapshot Terrain { get; }
        public ulong Seed { get; }
        public IReadOnlyList<string> LocalStuffDefs { get; }
        public int MaxCandidates { get; }
        public TerrainArchetype Archetype { get; }
        public int WallLayers { get; }
        public string Digest { get; }

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p2-layout-v1");
            hash = StableHash.Add(hash, Profile.Digest);
            hash = StableHash.Add(hash, Terrain.Digest);
            hash = StableHash.Add(hash, Seed);
            hash = StableHash.Add(hash, MaxCandidates);
            hash = StableHash.Add(hash, (int)Archetype);
            hash = StableHash.Add(hash, WallLayers);
            foreach (string value in LocalStuffDefs) hash = StableHash.Add(hash, value);
            return StableHash.Hex(hash);
        }
    }

    public sealed class PhysicalSettlementPlan
    {
        public PhysicalSettlementPlan(string inputDigest, string profileDigest, GridRect compound,
            GridRect field, IEnumerable<GridPoint> gates, IEnumerable<GridPoint> perimeter,
            IEnumerable<GridPoint> reinforcementGates, IEnumerable<GridPoint> reinforcementPerimeter,
            int wallLayers,
            IEnumerable<GridPoint> logistics, IEnumerable<GridPoint> fieldCells,
            IEnumerable<GridPoint> attackRoute, IEnumerable<GridPoint> retreatRoute,
            IEnumerable<PhysicalFacility> facilities, IEnumerable<PhysicalPlacement> placements,
            string localWallStuff, int candidatesConsidered, int candidateLimit, TerrainArchetype archetype,
            TerrainMutationFootprint terrainMutations)
        {
            InputDigest = inputDigest;
            ProfileDigest = profileDigest;
            Compound = compound;
            Field = field;
            Gates = Copy(gates);
            Perimeter = Copy(perimeter);
            ReinforcementGates = Copy(reinforcementGates);
            ReinforcementPerimeter = Copy(reinforcementPerimeter);
            WallLayers = wallLayers;
            Logistics = Copy(logistics);
            FieldCells = Copy(fieldCells);
            AttackRoute = Copy(attackRoute);
            RetreatRoute = Copy(retreatRoute);
            Facilities = new ReadOnlyCollection<PhysicalFacility>(facilities.ToList());
            Placements = new ReadOnlyCollection<PhysicalPlacement>(placements.ToList());
            LocalWallStuff = localWallStuff;
            CandidatesConsidered = candidatesConsidered;
            CandidateLimit = candidateLimit;
            Archetype = archetype;
            TerrainMutations = terrainMutations ?? throw new ArgumentNullException(nameof(terrainMutations));
            Digest = ComputeDigest();
        }

        public string InputDigest { get; }
        public string ProfileDigest { get; }
        public GridRect Compound { get; }
        public GridRect Field { get; }
        public IReadOnlyList<GridPoint> Gates { get; }
        public IReadOnlyList<GridPoint> Perimeter { get; }
        public IReadOnlyList<GridPoint> ReinforcementGates { get; }
        public IReadOnlyList<GridPoint> ReinforcementPerimeter { get; }
        public int WallLayers { get; }
        public IReadOnlyList<GridPoint> Logistics { get; }
        public IReadOnlyList<GridPoint> FieldCells { get; }
        public IReadOnlyList<GridPoint> AttackRoute { get; }
        public IReadOnlyList<GridPoint> RetreatRoute { get; }
        public IReadOnlyList<PhysicalFacility> Facilities { get; }
        public IReadOnlyList<PhysicalPlacement> Placements { get; }
        public string LocalWallStuff { get; }
        public int CandidatesConsidered { get; }
        public int CandidateLimit { get; }
        public TerrainArchetype Archetype { get; }
        public TerrainMutationFootprint TerrainMutations { get; }
        public string Digest { get; }

        private static IReadOnlyList<GridPoint> Copy(IEnumerable<GridPoint> values) =>
            new ReadOnlyCollection<GridPoint>((values ?? Enumerable.Empty<GridPoint>()).ToList());

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p2-physical-plan-v1");
            hash = StableHash.Add(hash, InputDigest);
            hash = StableHash.Add(hash, ProfileDigest);
            hash = AddRect(hash, Compound);
            hash = AddRect(hash, Field);
            foreach (GridPoint value in Gates) hash = AddPoint(hash, value);
            foreach (GridPoint value in Perimeter) hash = AddPoint(hash, value);
            foreach (GridPoint value in ReinforcementGates) hash = AddPoint(hash, value);
            foreach (GridPoint value in ReinforcementPerimeter) hash = AddPoint(hash, value);
            hash = StableHash.Add(hash, WallLayers);
            foreach (GridPoint value in Logistics) hash = AddPoint(hash, value);
            foreach (GridPoint value in FieldCells) hash = AddPoint(hash, value);
            foreach (GridPoint value in AttackRoute) hash = AddPoint(hash, value);
            foreach (GridPoint value in RetreatRoute) hash = AddPoint(hash, value);
            foreach (PhysicalFacility facility in Facilities.OrderBy(f => f.Id, StringComparer.Ordinal))
            {
                hash = StableHash.Add(hash, facility.Id);
                hash = StableHash.Add(hash, (int)facility.Kind);
                hash = AddRect(hash, facility.Bounds);
                hash = AddPoint(hash, facility.Door);
                hash = StableHash.Add(hash, facility.Capacity);
                hash = StableHash.Add(hash, facility.Roofed ? 1 : 0);
                foreach (GridPoint cell in facility.InteractionCells) hash = AddPoint(hash, cell);
            }
            foreach (PhysicalPlacement placement in Placements.OrderBy(p => p.Cell).ThenBy(p => p.Kind))
            {
                hash = StableHash.Add(hash, (int)placement.Kind);
                hash = StableHash.Add(hash, placement.DefName);
                hash = StableHash.Add(hash, placement.StuffDefName);
                hash = AddPoint(hash, placement.Cell);
                hash = StableHash.Add(hash, placement.Rotation);
                hash = StableHash.Add(hash, placement.FacilityId);
                hash = StableHash.Add(hash, placement.Imported ? 1 : 0);
                hash = StableHash.Add(hash, placement.Value);
            }
            hash = StableHash.Add(hash, LocalWallStuff);
            hash = StableHash.Add(hash, CandidatesConsidered);
            hash = StableHash.Add(hash, CandidateLimit);
            hash = StableHash.Add(hash, (int)Archetype);
            hash = StableHash.Add(hash, TerrainMutations.Digest);
            return StableHash.Hex(hash);
        }

        private static ulong AddPoint(ulong hash, GridPoint point)
        {
            hash = StableHash.Add(hash, point.X);
            return StableHash.Add(hash, point.Z);
        }

        private static ulong AddRect(ulong hash, GridRect rect)
        {
            hash = AddPoint(hash, new GridPoint(rect.MinX, rect.MinZ));
            hash = StableHash.Add(hash, rect.Width);
            return StableHash.Add(hash, rect.Height);
        }
    }

    public sealed class PhysicalPlanningResult
    {
        private PhysicalPlanningResult(PhysicalSettlementPlan plan, IEnumerable<string> rejections,
            int candidatesConsidered, bool limitReached)
        {
            Plan = plan;
            Rejections = new ReadOnlyCollection<string>((rejections ?? Enumerable.Empty<string>()).ToList());
            CandidatesConsidered = candidatesConsidered;
            LimitReached = limitReached;
        }

        public PhysicalSettlementPlan Plan { get; }
        public IReadOnlyList<string> Rejections { get; }
        public int CandidatesConsidered { get; }
        public bool LimitReached { get; }
        public bool Success => Plan != null;
        public static PhysicalPlanningResult Accepted(PhysicalSettlementPlan plan, IEnumerable<string> rejections) =>
            new PhysicalPlanningResult(plan, rejections, plan.CandidatesConsidered, false);
        public static PhysicalPlanningResult Rejected(IEnumerable<string> rejections, int considered, bool limitReached) =>
            new PhysicalPlanningResult(null, rejections, considered, limitReached);
    }

    public static class PhysicalSettlementPlanner
    {
        public const int CompoundWidth = 31;
        public const int CompoundHeight = 25;
        public const int FieldWidth = 10;
        public const int FieldHeight = 13;

        public static PhysicalPlanningResult Plan(PhysicalPlanningInput input)
        {
            List<GridPoint> origins = CandidateOrigins(input);
            List<string> rejections = new List<string>();
            int considered = 0;
            foreach (GridPoint origin in origins)
            {
                if (considered >= input.MaxCandidates) break;
                considered++;
                PhysicalSettlementPlan plan;
                string rejection;
                if (TryBuild(input, origin, considered, out plan, out rejection))
                {
                    IReadOnlyList<string> violations = PhysicalSettlementValidator.Validate(input, plan);
                    if (violations.Count == 0) return PhysicalPlanningResult.Accepted(plan, rejections);
                    rejection = "validation:" + string.Join("+", violations);
                }
                if (rejections.Count < 20) rejections.Add(origin + ":" + rejection);
            }
            bool limited = origins.Count > considered && considered >= input.MaxCandidates;
            if (rejections.Count == 0) rejections.Add("no_candidate_origins");
            return PhysicalPlanningResult.Rejected(rejections, considered, limited);
        }

        private static List<GridPoint> CandidateOrigins(PhysicalPlanningInput input)
        {
            TerrainSnapshot terrain = input.Terrain;
            int totalWidth = CompoundWidth + 1 + FieldWidth;
            int exteriorDepth = 2 * input.WallLayers - 1;
            List<GridPoint> origins = new List<GridPoint>();
            for (int z = exteriorDepth; z + CompoundHeight + exteriorDepth - 1 < terrain.Height; z++)
                for (int x = 1; x + totalWidth < terrain.Width; x++) origins.Add(new GridPoint(x, z));
            int centerX = terrain.Width / 2;
            int centerZ = terrain.Height / 2;
            return origins.OrderBy(p => DistanceSquared(p.X + totalWidth / 2, p.Z + CompoundHeight / 2, centerX, centerZ))
                .ThenBy(p => StableTie(input.Seed, p)).ThenBy(p => p).ToList();
        }

        private static bool TryBuild(PhysicalPlanningInput input, GridPoint origin, int considered,
            out PhysicalSettlementPlan plan, out string rejection)
        {
            GridRect compound = new GridRect(origin.X, origin.Z, CompoundWidth, CompoundHeight);
            foreach (GridPoint cell in compound.Points())
            {
                TerrainCell terrain = input.Terrain[cell];
                bool excavatableMountainRock = input.Archetype == TerrainArchetype.ExcavatedMountain &&
                    terrain.Has(TerrainTraits.NaturalRock);
                if ((!terrain.Has(TerrainTraits.Standable | TerrainTraits.Buildable) && !excavatableMountainRock) ||
                    terrain.Has(TerrainTraits.Water) || (terrain.Has(TerrainTraits.Occupied) && !excavatableMountainRock) ||
                    (terrain.Has(TerrainTraits.Roofed) && !excavatableMountainRock))
                {
                    plan = null;
                    rejection = "compound_cell_unusable";
                    return false;
                }
            }
            GridRect field;
            if (!TryFindField(input.Terrain, compound, input.WallLayers, out field))
            {
                plan = null;
                rejection = "field_cell_illegal";
                return false;
            }

            List<GridPoint> gates = new List<GridPoint>
            {
                new GridPoint(compound.MinX + 15, compound.MinZ),
                new GridPoint(compound.MinX + 15, compound.MaxZ)
            };
            List<GridPoint> attackRoute;
            List<GridPoint> retreatRoute;
            int routeLimit = Math.Min(input.Terrain.Width * input.Terrain.Height, 10000);
            int outerDistance = 2 * input.WallLayers - 1;
            if (!TryRouteToEdge(input.Terrain, new GridPoint(gates[0].X, gates[0].Z - outerDistance), routeLimit, out attackRoute) ||
                !TryRouteToEdge(input.Terrain, new GridPoint(gates[1].X, gates[1].Z + outerDistance), routeLimit, out retreatRoute))
            {
                plan = null;
                rejection = "gate_edge_route";
                return false;
            }
            List<PhysicalFacility> facilities = BuildFacilities(input.Profile, compound, field);
            TerrainMutationFootprint mutations = TerrainMutationPlanner.Plan(input.Archetype, input.Terrain, compound, facilities);
            if (input.Archetype == TerrainArchetype.ExcavatedMountain && mutations.ExcavationCells.Count == 0)
            {
                plan = null;
                rejection = "mountain_candidate_requires_excavation";
                return false;
            }
            if (TerrainMutationPlanner.Validate(input.Archetype, input.Terrain, mutations).Count != 0)
            {
                plan = null;
                rejection = "invalid_terrain_mutation_footprint";
                return false;
            }
            List<GridPoint> perimeter = compound.Points().Where(p =>
                (p.X == compound.MinX || p.X == compound.MaxX || p.Z == compound.MinZ || p.Z == compound.MaxZ) &&
                !gates.Contains(p)).ToList();
            List<GridPoint> reinforcementGates;
            List<GridPoint> reinforcementPerimeter;
            if (!TryBuildGatehouseBands(input.Terrain, compound, input.WallLayers, out reinforcementGates, out reinforcementPerimeter))
            {
                plan = null;
                rejection = "gatehouse_band_terrain";
                return false;
            }
            HashSet<GridPoint> roomCells = new HashSet<GridPoint>(facilities.Where(f => f.Roofed).SelectMany(f => f.Bounds.Points()));
            List<GridPoint> logistics = compound.Points().Where(p => !perimeter.Contains(p) && !roomCells.Contains(p)).ToList();
            List<PhysicalPlacement> placements = BuildPlacements(input.Profile, facilities, compound, input.LocalStuffDefs[0]);
            plan = new PhysicalSettlementPlan(input.Digest, input.Profile.Digest, compound, field, gates, perimeter,
                reinforcementGates, reinforcementPerimeter,
                input.WallLayers,
                logistics, field.Points(), attackRoute, retreatRoute, facilities, placements,
                input.LocalStuffDefs[0], considered, input.MaxCandidates, input.Archetype, mutations);
            // Persist only final, proven public circulation; it is not a room-complement label.
            IReadOnlyCollection<GridPoint> publicReachable = PlannedNavigationGrid.Build(input.Terrain, plan).PublicReachable
                .Where(compound.Contains).ToList();
            plan = new PhysicalSettlementPlan(input.Digest, input.Profile.Digest, compound, field, gates, perimeter,
                reinforcementGates, reinforcementPerimeter, input.WallLayers,
                publicReachable, field.Points(), attackRoute, retreatRoute, facilities, placements,
                input.LocalStuffDefs[0], considered, input.MaxCandidates, input.Archetype, mutations);
            rejection = null;
            return true;
        }

        private static List<PhysicalFacility> BuildFacilities(PhysicalSettlementProfile profile, GridRect c, GridRect field)
        {
            bool electric = profile.Has(SettlementCapability.Electricity);
            return new List<PhysicalFacility>
            {
                Facility("housing", FacilityKind.Housing, c, 2, 2, 7, 7, 4),
                Facility("jail", FacilityKind.Jail, c, 10, 2, 5, 7, 1),
                Facility("medical", FacilityKind.Medical, c, 17, 2, 5, 7, 1),
                Facility("kitchen", FacilityKind.Kitchen, c, 2, 11, 5, 6, 1),
                Facility(electric ? "freezer" : "pantry", electric ? FacilityKind.Freezer : FacilityKind.Pantry, c, 9, 11, 7, 6, 24),
                Facility("dining", FacilityKind.DiningRecreation, c, 16, 11, 7, 6, 4),
                Facility("workshop", FacilityKind.Workshop, c, 25, 11, 5, 6, 1),
                Facility("storage", FacilityKind.BulkStorage, c, 2, 18, 7, 5, 24),
                new PhysicalFacility("field", FacilityKind.Field,
                    field, new GridPoint(field.MinX, field.MinZ), Enumerable.Empty<GridPoint>(), profile.RequiredCropCells, false),
                new PhysicalFacility("gatehouse", FacilityKind.Gatehouse,
                    new GridRect(c.MinX + 13, c.MinZ, 5, c.Height), new GridPoint(c.MinX + 15, c.MinZ),
                    new[] { new GridPoint(c.MinX + 14, c.MinZ + 1), new GridPoint(c.MinX + 16, c.MinZ + 1) }, 2, false),
                new PhysicalFacility("defense", FacilityKind.Defense,
                    new GridRect(c.MinX + 10, c.MinZ + 1, 11, 3), new GridPoint(c.MinX + 15, c.MinZ),
                    new[] { new GridPoint(c.MinX + 12, c.MinZ + 1), new GridPoint(c.MinX + 18, c.MinZ + 1) },
                    electric ? 4 : 2, false)
            }.Concat(electric
                ? new[] { new PhysicalFacility("power", FacilityKind.Power, new GridRect(c.MinX + 24, c.MinZ + 2, 5, 7),
                    new GridPoint(c.MinX + 26, c.MinZ + 8), new[] { new GridPoint(c.MinX + 26, c.MinZ + 7) }, 1, false) }
                : Enumerable.Empty<PhysicalFacility>()).ToList();
        }

        private static bool TryFindField(TerrainSnapshot terrain, GridRect compound, int wallLayers, out GridRect field)
        {
            GridRect[] candidates =
            {
                new GridRect(compound.MaxX + 2, compound.MinZ + 6, FieldWidth, FieldHeight),
                new GridRect(compound.MinX - FieldWidth - 1, compound.MinZ + 6, FieldWidth, FieldHeight),
                new GridRect(compound.MinX + 10, compound.MinZ - FieldHeight - 1, FieldWidth, FieldHeight),
                new GridRect(compound.MinX + 10, compound.MaxZ + 2, FieldWidth, FieldHeight)
            };
            HashSet<GridPoint> gatehouseFootprint = GatehouseFootprint(compound, wallLayers);
            foreach (GridRect candidate in candidates)
            {
                if (candidate.MinX < 0 || candidate.MinZ < 0 || candidate.MaxX >= terrain.Width || candidate.MaxZ >= terrain.Height) continue;
                if (candidate.Points().Any(gatehouseFootprint.Contains)) continue;
                if (candidate.Points().All(cell => terrain[cell].Has(TerrainTraits.Standable | TerrainTraits.Fertile) &&
                    !terrain[cell].HasAny(TerrainTraits.Water | TerrainTraits.Roofed | TerrainTraits.Occupied)))
                {
                    field = candidate;
                    return true;
                }
            }
            field = default(GridRect);
            return false;
        }

        // Each additional layer is an external five-cell gatehouse band.  The
        // one-cell vestibule between bands is deliberately left empty, so a
        // pawn crosses every door in serial order instead of finding a side gap.
        private static bool TryBuildGatehouseBands(TerrainSnapshot terrain, GridRect compound, int wallLayers,
            out List<GridPoint> gates, out List<GridPoint> walls)
        {
            gates = new List<GridPoint>();
            walls = new List<GridPoint>();
            if (wallLayers == 1) return true;
            foreach (int direction in new[] { -1, 1 })
            {
                for (int layer = 1; layer < wallLayers; layer++)
                {
                    int z = direction < 0 ? compound.MinZ - 2 * layer : compound.MaxZ + 2 * layer;
                    for (int x = compound.MinX + 13; x <= compound.MinX + 17; x++)
                    {
                        GridPoint point = new GridPoint(x, z);
                        if (!GatehouseCellUsable(terrain, point)) return false;
                        if (x == compound.MinX + 15) gates.Add(point); else walls.Add(point);
                    }
                }
                for (int offset = 1; offset < 2 * wallLayers - 1; offset++)
                {
                    int z = direction < 0 ? compound.MinZ - offset : compound.MaxZ + offset;
                    if (!GatehouseCellUsable(terrain, new GridPoint(compound.MinX + 15, z))) return false;
                }
            }
            return true;
        }

        private static bool GatehouseCellUsable(TerrainSnapshot terrain, GridPoint point) =>
            terrain.InBounds(point) && terrain[point].Has(TerrainTraits.Standable | TerrainTraits.Buildable) &&
            !terrain[point].HasAny(TerrainTraits.Water | TerrainTraits.Roofed | TerrainTraits.Occupied);

        private static HashSet<GridPoint> GatehouseFootprint(GridRect compound, int wallLayers)
        {
            HashSet<GridPoint> result = new HashSet<GridPoint>();
            foreach (int direction in new[] { -1, 1 })
                for (int offset = 1; offset < 2 * wallLayers - 1; offset++)
                    result.Add(new GridPoint(compound.MinX + 15,
                        direction < 0 ? compound.MinZ - offset : compound.MaxZ + offset));
            foreach (int direction in new[] { -1, 1 })
                for (int layer = 1; layer < wallLayers; layer++)
                {
                    int z = direction < 0 ? compound.MinZ - 2 * layer : compound.MaxZ + 2 * layer;
                    for (int x = compound.MinX + 13; x <= compound.MinX + 17; x++) result.Add(new GridPoint(x, z));
                }
            return result;
        }

        private static PhysicalFacility Facility(string id, FacilityKind kind, GridRect compound,
            int dx, int dz, int width, int height, int capacity)
        {
            GridRect bounds = new GridRect(compound.MinX + dx, compound.MinZ + dz, width, height);
            GridPoint door = new GridPoint(bounds.MinX + width / 2, bounds.MaxZ);
            return new PhysicalFacility(id, kind, bounds, door,
                new[] { new GridPoint(bounds.MinX + 1, bounds.MinZ + 1) }, capacity, true);
        }

        private static List<PhysicalPlacement> BuildPlacements(PhysicalSettlementProfile profile,
            List<PhysicalFacility> facilities, GridRect compound, string stuff)
        {
            List<PhysicalPlacement> result = new List<PhysicalPlacement>();
            PhysicalFacility housing = facilities.Single(f => f.Id == "housing");
            GridPoint[] beds =
            {
                new GridPoint(housing.Bounds.MinX + 1, housing.Bounds.MinZ + 1),
                new GridPoint(housing.Bounds.MinX + 3, housing.Bounds.MinZ + 1),
                new GridPoint(housing.Bounds.MinX + 1, housing.Bounds.MinZ + 4),
                new GridPoint(housing.Bounds.MinX + 3, housing.Bounds.MinZ + 4)
            };
            foreach (GridPoint bed in beds) result.Add(Placement(PhysicalPlacementKind.OrdinaryBed, "Bed", stuff, bed, "North", "housing", false, 120));
            AddAt(result, facilities, "jail", PhysicalPlacementKind.PrisonBed, "Bed", stuff, false, 120);
            AddAt(result, facilities, "medical", PhysicalPlacementKind.MedicalBed, "Bed", stuff, false, 120);
            AddAt(result, facilities, "kitchen", PhysicalPlacementKind.Workbench,
                profile.Has(SettlementCapability.ElectricCooking) ? "ElectricStove" : "FueledStove", "Steel", true, 250);
            AddAt(result, facilities, "workshop", PhysicalPlacementKind.Workbench,
                profile.Has(SettlementCapability.Electricity) ? "ElectricSmithy" : "FueledSmithy", "Steel", true, 350);
            AddAt(result, facilities, "dining", PhysicalPlacementKind.Dining, "Table2x2c", stuff, false, 140);
            result.Add(Placement(PhysicalPlacementKind.Recreation,
                profile.Has(SettlementCapability.Electricity) ? "ChessTable" : "HorseshoesPin", stuff,
                new GridPoint(compound.MinX + 17, compound.MinZ + 12), "North", "dining", false, 100));
            AddAt(result, facilities, "storage", PhysicalPlacementKind.StorageMarker, "Shelf", stuff, false, 110);
            string preserve = profile.Has(SettlementCapability.Electricity) ? "freezer" : "pantry";
            AddAt(result, facilities, preserve, PhysicalPlacementKind.FoodStock,
                profile.Has(SettlementCapability.Electricity) ? "MealSimple" : "Pemmican", string.Empty, false, profile.Budget.StockValue);
            result.Add(Placement(PhysicalPlacementKind.FuelStock, "WoodLog", string.Empty,
                new GridPoint(compound.MinX + 7, compound.MinZ + 21), "North", "storage", false, 300));

            if (profile.Has(SettlementCapability.Electricity))
            {
                result.Add(Placement(PhysicalPlacementKind.Generator, "WoodFiredGenerator", string.Empty,
                    new GridPoint(compound.MinX + 26, compound.MinZ + 4), "North", "power", true, 1000));
                result.Add(Placement(PhysicalPlacementKind.Battery, "Battery", string.Empty,
                    new GridPoint(compound.MinX + 26, compound.MinZ + 6), "North", "power", true, 500));
                PhysicalFacility freezer = facilities.Single(f => f.Id == "freezer");
                result.Add(Placement(PhysicalPlacementKind.Cooler, "Cooler", string.Empty,
                    new GridPoint(freezer.Bounds.MinX, freezer.Bounds.MinZ + 2), "West", "freezer", true, 350));
                GridPoint generator = result.Single(p => p.Kind == PhysicalPlacementKind.Generator).Cell;
                GridPoint battery = result.Single(p => p.Kind == PhysicalPlacementKind.Battery).Cell;
                GridPoint[] powered = result.Where(p => p.Kind == PhysicalPlacementKind.Cooler ||
                    (p.Kind == PhysicalPlacementKind.Workbench && p.DefName.StartsWith("Electric", StringComparison.Ordinal)))
                    .Select(p => p.Cell).Concat(new[]
                    {
                        new GridPoint(generator.X - 1, generator.Z),
                        new GridPoint(battery.X - 1, battery.Z),
                        new GridPoint(compound.MinX + 12, compound.MinZ + 1),
                        new GridPoint(compound.MinX + 18, compound.MinZ + 1)
                    }).ToArray();
                HashSet<GridPoint> transmitterFootprints = new HashSet<GridPoint>
                {
                    generator,
                    new GridPoint(generator.X + 1, generator.Z),
                    new GridPoint(generator.X, generator.Z + 1),
                    new GridPoint(generator.X + 1, generator.Z + 1),
                    battery,
                    new GridPoint(battery.X, battery.Z + 1)
                };
                foreach (GridPoint cell in ConduitTree(powered).Where(c => !transmitterFootprints.Contains(c)))
                    result.Add(Placement(PhysicalPlacementKind.Conduit, "PowerConduit", string.Empty, cell, "North", "power", true, 5));
                result.Add(Placement(PhysicalPlacementKind.Turret, "Turret_MiniTurret", "Steel",
                    new GridPoint(compound.MinX + 12, compound.MinZ + 1), "South", "defense", true, 700));
                result.Add(Placement(PhysicalPlacementKind.Turret, "Turret_MiniTurret", "Steel",
                    new GridPoint(compound.MinX + 18, compound.MinZ + 1), "South", "defense", true, 700));
            }
            string coverDef = profile.Has(SettlementCapability.Electricity) ? "Sandbags" : "Barricade";
            string coverStuff = profile.Has(SettlementCapability.Electricity) ? "Steel" : stuff;
            foreach (int dx in new[] { 10, 14, 16, 20 })
                result.Add(Placement(PhysicalPlacementKind.Cover, coverDef, coverStuff,
                    new GridPoint(compound.MinX + dx, compound.MinZ + 1), "North", "defense", false, 40));
            return result;
        }

        private static bool TryRouteToEdge(TerrainSnapshot terrain, GridPoint start, int limit, out List<GridPoint> route)
        {
            route = new List<GridPoint>();
            if (!terrain.InBounds(start) || !RoutePassable(terrain[start])) return false;
            Queue<GridPoint> queue = new Queue<GridPoint>();
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            HashSet<GridPoint> visited = new HashSet<GridPoint>();
            queue.Enqueue(start);
            visited.Add(start);
            GridPoint end = start;
            bool found = terrain[start].Has(TerrainTraits.MapEdge);
            GridPoint[] offsets = { new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1) };
            while (queue.Count > 0 && !found && visited.Count < limit)
            {
                GridPoint current = queue.Dequeue();
                foreach (GridPoint offset in offsets)
                {
                    GridPoint next = new GridPoint(current.X + offset.X, current.Z + offset.Z);
                    if (!terrain.InBounds(next) || visited.Contains(next) || !RoutePassable(terrain[next])) continue;
                    visited.Add(next);
                    previous[next] = current;
                    queue.Enqueue(next);
                    if (terrain[next].Has(TerrainTraits.MapEdge)) { end = next; found = true; break; }
                    if (visited.Count >= limit) break;
                }
            }
            if (!found) return false;
            for (GridPoint current = end;; current = previous[current])
            {
                route.Add(current);
                if (current.Equals(start)) break;
            }
            route.Reverse();
            return true;
        }

        private static bool RoutePassable(TerrainCell cell) => cell.Has(TerrainTraits.Standable) &&
            !cell.HasAny(TerrainTraits.Water | TerrainTraits.Occupied);

        private static void AddAt(List<PhysicalPlacement> result, List<PhysicalFacility> facilities, string id,
            PhysicalPlacementKind kind, string defName, string stuff, bool imported, int value)
        {
            PhysicalFacility facility = facilities.Single(f => f.Id == id);
            GridPoint center = new GridPoint(facility.Bounds.MinX + facility.Bounds.Width / 2,
                facility.Bounds.MinZ + facility.Bounds.Height / 2);
            result.Add(Placement(kind, defName, stuff, center, "North", id, imported, value));
        }

        private static PhysicalPlacement Placement(PhysicalPlacementKind kind, string defName, string stuff,
            GridPoint cell, string rotation, string facility, bool imported, int value) =>
            new PhysicalPlacement(kind, defName, stuff, cell, rotation, facility, imported, value);

        private static IEnumerable<GridPoint> ConduitTree(IReadOnlyList<GridPoint> terminals)
        {
            HashSet<GridPoint> result = new HashSet<GridPoint>();
            GridPoint root = terminals[0];
            foreach (GridPoint terminal in terminals)
            {
                int x = root.X;
                int z = root.Z;
                result.Add(new GridPoint(x, z));
                while (x != terminal.X) { x += Math.Sign(terminal.X - x); result.Add(new GridPoint(x, z)); }
                while (z != terminal.Z) { z += Math.Sign(terminal.Z - z); result.Add(new GridPoint(x, z)); }
            }
            return result.OrderBy(p => p).ToList();
        }

        private static long DistanceSquared(int x, int z, int centerX, int centerZ)
        {
            long dx = x - centerX;
            long dz = z - centerZ;
            return dx * dx + dz * dz;
        }

        private static ulong StableTie(ulong seed, GridPoint point)
        {
            ulong hash = StableHash.Add(seed, point.X);
            return StableHash.Add(hash, point.Z);
        }
    }

    public static class PhysicalSettlementValidator
    {
        public static IReadOnlyList<string> Validate(PhysicalPlanningInput input, PhysicalSettlementPlan plan)
        {
            List<string> errors = new List<string>();
            if (plan.InputDigest != input.Digest || plan.ProfileDigest != input.Profile.Digest) errors.Add("input_digest");
            if (plan.Archetype != input.Archetype) errors.Add("archetype");
            errors.AddRange(TerrainMutationPlanner.Validate(input.Archetype, input.Terrain, plan.TerrainMutations));
            errors.AddRange(P4Connectivity.Validate(input.Terrain, plan));
            if (plan.CandidatesConsidered <= 0 || plan.CandidatesConsidered > input.MaxCandidates) errors.Add("candidate_bound");
            if (!input.LocalStuffDefs.Contains(plan.LocalWallStuff)) errors.Add("nonlocal_wall_material");
            foreach (FacilityDemand demand in input.Profile.Facilities)
            {
                List<PhysicalFacility> facilities = plan.Facilities.Where(f => f.Kind == demand.Kind).ToList();
                if (facilities.Count != 1 || facilities.Sum(f => f.Capacity) < demand.Capacity) errors.Add("facility_" + demand.Kind);
            }
            if (plan.Facilities.Single(f => f.Kind == FacilityKind.Housing).Capacity < input.Profile.Residents) errors.Add("housing_capacity");
            if (plan.Placements.Count(p => p.Kind == PhysicalPlacementKind.OrdinaryBed) < input.Profile.Residents) errors.Add("ordinary_beds");
            if (plan.Placements.Count(p => p.Kind == PhysicalPlacementKind.PrisonBed) != 1 ||
                plan.Placements.Count(p => p.Kind == PhysicalPlacementKind.MedicalBed) != 1) errors.Add("special_beds");
            if (plan.FieldCells.Count < input.Profile.RequiredCropCells || plan.FieldCells.Any(c => !input.Terrain[c].Has(TerrainTraits.Fertile))) errors.Add("field_capacity");
            if (plan.Gates.Count != 2 || plan.Gates.Any(g => !IsPerimeter(g, plan.Compound))) errors.Add("gates");
            if (plan.WallLayers != input.WallLayers || plan.WallLayers < 1 || plan.WallLayers > 3 ||
                (plan.WallLayers == 1 && (plan.ReinforcementGates.Any() || plan.ReinforcementPerimeter.Any())) ||
                (plan.WallLayers > 1 && plan.ReinforcementGates.Count != 2 * (plan.WallLayers - 1)) ||
                plan.ReinforcementPerimeter.Intersect(plan.ReinforcementGates).Any() ||
                !GatehouseGeometryMatches(plan)) errors.Add("wall_layers");
            ValidateEdgeRoute(input.Terrain, plan.AttackRoute, errors, "attack_route");
            ValidateEdgeRoute(input.Terrain, plan.RetreatRoute, errors, "retreat_route");
            List<PhysicalFacility> roofedFacilities = plan.Facilities.Where(f => f.Roofed).ToList();
            for (int i = 0; i < roofedFacilities.Count; i++)
                for (int j = i + 1; j < roofedFacilities.Count; j++)
                    if (roofedFacilities[i].Bounds.Points().Any(roofedFacilities[j].Bounds.Contains)) errors.Add("facility_overlap");
            if (plan.Placements.Where(p => p.Kind != PhysicalPlacementKind.Conduit)
                .GroupBy(p => p.Cell).Any(g => g.Count() > 1)) errors.Add("placement_overlap");
            HashSet<GridPoint> logistics = new HashSet<GridPoint>(plan.Logistics);
            foreach (PhysicalFacility facility in plan.Facilities.Where(f => f.Roofed))
            {
                if (!facility.Bounds.Contains(facility.Door) || !IsPerimeter(facility.Door, facility.Bounds)) errors.Add("door_" + facility.Id);
                if (!Neighbors(facility.Door).Any(logistics.Contains)) errors.Add("route_" + facility.Id);
                if (facility.InteractionCells.Any(c => !facility.Bounds.Contains(c))) errors.Add("interaction_" + facility.Id);
            }
            bool electric = input.Profile.Has(SettlementCapability.Electricity);
            if (electric)
            {
                PhysicalPlacement cooler = plan.Placements.SingleOrDefault(p => p.Kind == PhysicalPlacementKind.Cooler);
                if (cooler == null || cooler.Rotation != "West") errors.Add("cooler_orientation");
                else
                {
                    GridPoint exhaust = new GridPoint(cooler.Cell.X - 1, cooler.Cell.Z);
                    PhysicalFacility freezer = plan.Facilities.Single(f => f.Kind == FacilityKind.Freezer);
                    if (freezer.Bounds.Contains(exhaust) || input.Terrain[exhaust].Has(TerrainTraits.Roofed)) errors.Add("cooler_exhaust");
                }
                HashSet<GridPoint> conduits = new HashSet<GridPoint>(plan.Placements.Where(p => p.Kind == PhysicalPlacementKind.Conduit).Select(p => p.Cell));
                PhysicalPlacement generator = plan.Placements.SingleOrDefault(p => p.Kind == PhysicalPlacementKind.Generator);
                PhysicalPlacement battery = plan.Placements.SingleOrDefault(p => p.Kind == PhysicalPlacementKind.Battery);
                if (generator != null && battery != null)
                {
                    GridPoint[] generatorFootprint =
                    {
                        generator.Cell,
                        new GridPoint(generator.Cell.X + 1, generator.Cell.Z),
                        new GridPoint(generator.Cell.X, generator.Cell.Z + 1),
                        new GridPoint(generator.Cell.X + 1, generator.Cell.Z + 1)
                    };
                    HashSet<GridPoint> transmitterFootprints = new HashSet<GridPoint>(generatorFootprint)
                    {
                        battery.Cell,
                        new GridPoint(battery.Cell.X, battery.Cell.Z + 1)
                    };
                    if (transmitterFootprints.Any(conduits.Contains)) errors.Add("conduit_transmitter_overlap");
                    if (!generatorFootprint.Any(c => Neighbors(c).Any(conduits.Contains))) errors.Add("power_connection_generator");
                }
                foreach (PhysicalPlacement powered in plan.Placements.Where(p => p.Kind == PhysicalPlacementKind.Battery || p.Kind == PhysicalPlacementKind.Cooler ||
                    p.Kind == PhysicalPlacementKind.Turret || (p.Kind == PhysicalPlacementKind.Workbench && p.DefName.StartsWith("Electric", StringComparison.Ordinal))))
                    if (!conduits.Contains(powered.Cell) && !Neighbors(powered.Cell).Any(conduits.Contains)) errors.Add("power_connection_" + powered.Kind);
                if (!plan.Placements.Any(p => p.Kind == PhysicalPlacementKind.Generator) || !plan.Placements.Any(p => p.Kind == PhysicalPlacementKind.Battery)) errors.Add("power_components");
            }
            else if (plan.Placements.Any(p => p.Kind == PhysicalPlacementKind.Generator || p.Kind == PhysicalPlacementKind.Battery ||
                p.Kind == PhysicalPlacementKind.Conduit || p.Kind == PhysicalPlacementKind.Cooler || p.Kind == PhysicalPlacementKind.Turret ||
                p.DefName.StartsWith("Electric", StringComparison.Ordinal))) errors.Add("tribal_electric_content");
            if (plan.Placements.Any(p => p.DefName.IndexOf("Trap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.DefName.IndexOf("IED", StringComparison.OrdinalIgnoreCase) >= 0)) errors.Add("prohibited_defense");
            if (plan.Placements.Where(p => p.Imported).Sum(p => p.Value) > input.Profile.Budget.ImportValueLimit) errors.Add("import_budget");
            return new ReadOnlyCollection<string>(errors.Distinct().ToList());
        }

        private static void ValidateEdgeRoute(TerrainSnapshot terrain, IReadOnlyList<GridPoint> route,
            List<string> errors, string label)
        {
            if (route.Count == 0 || !terrain[route[route.Count - 1]].Has(TerrainTraits.MapEdge))
            {
                errors.Add(label);
                return;
            }
            for (int i = 1; i < route.Count; i++)
                if (Math.Abs(route[i].X - route[i - 1].X) + Math.Abs(route[i].Z - route[i - 1].Z) != 1)
                    errors.Add(label + "_continuity");
        }

        private static bool IsPerimeter(GridPoint point, GridRect rect) => rect.Contains(point) &&
            (point.X == rect.MinX || point.X == rect.MaxX || point.Z == rect.MinZ || point.Z == rect.MaxZ);

        private static IEnumerable<GridPoint> Neighbors(GridPoint point)
        {
            yield return new GridPoint(point.X, point.Z - 1);
            yield return new GridPoint(point.X - 1, point.Z);
            yield return new GridPoint(point.X + 1, point.Z);
            yield return new GridPoint(point.X, point.Z + 1);
        }

        private static bool GatehouseGeometryMatches(PhysicalSettlementPlan plan)
        {
            HashSet<GridPoint> expectedGates = new HashSet<GridPoint>();
            HashSet<GridPoint> expectedWalls = new HashSet<GridPoint>();
            foreach (int direction in new[] { -1, 1 })
                for (int layer = 1; layer < plan.WallLayers; layer++)
                {
                    int z = direction < 0 ? plan.Compound.MinZ - 2 * layer : plan.Compound.MaxZ + 2 * layer;
                    for (int x = plan.Compound.MinX + 13; x <= plan.Compound.MinX + 17; x++)
                    {
                        GridPoint cell = new GridPoint(x, z);
                        if (x == plan.Compound.MinX + 15) expectedGates.Add(cell); else expectedWalls.Add(cell);
                    }
                }
            return expectedGates.SetEquals(plan.ReinforcementGates) && expectedWalls.SetEquals(plan.ReinforcementPerimeter);
        }
    }
}
