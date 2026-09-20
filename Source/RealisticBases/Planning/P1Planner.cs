using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public struct GridRect
    {
        public readonly int MinX;
        public readonly int MinZ;
        public readonly int Width;
        public readonly int Height;

        public GridRect(int minX, int minZ, int width, int height)
        {
            MinX = minX;
            MinZ = minZ;
            Width = width;
            Height = height;
        }

        public int MaxX => MinX + Width - 1;
        public int MaxZ => MinZ + Height - 1;
        public int Area => Width * Height;
        public bool Contains(GridPoint point) => point.X >= MinX && point.X <= MaxX && point.Z >= MinZ && point.Z <= MaxZ;

        public IEnumerable<GridPoint> Points()
        {
            for (int z = MinZ; z <= MaxZ; z++)
                for (int x = MinX; x <= MaxX; x++)
                    yield return new GridPoint(x, z);
        }

        public override string ToString() => MinX + "," + MinZ + "," + Width + "x" + Height;
    }

    public enum ReservationKind
    {
        Structure,
        Room,
        Interaction,
        Route,
        GateOpening,
        RoofSupport,
        PowerNetwork,
        DefenseSector
    }

    public sealed class PlanReservation
    {
        public PlanReservation(GridPoint cell, ReservationKind kind, string owner)
        {
            Cell = cell;
            Kind = kind;
            Owner = owner ?? string.Empty;
        }

        public GridPoint Cell { get; }
        public ReservationKind Kind { get; }
        public string Owner { get; }
    }

    public sealed class RoomEnvelope
    {
        public RoomEnvelope(string id, string label, GridRect bounds, GridPoint interactionCell, int plannedCapacity)
        {
            Id = id;
            Label = label;
            Bounds = bounds;
            InteractionCell = interactionCell;
            PlannedCapacity = plannedCapacity;
        }

        public string Id { get; }
        public string Label { get; }
        public GridRect Bounds { get; }
        public GridPoint InteractionCell { get; }
        public int PlannedCapacity { get; }
    }

    public sealed class CirculationLink
    {
        public CirculationLink(string from, string to, string kind)
        {
            From = from;
            To = to;
            Kind = kind;
        }

        public string From { get; }
        public string To { get; }
        public string Kind { get; }
    }

    public sealed class PlanValidation
    {
        public PlanValidation(IEnumerable<string> violations)
        {
            Violations = new ReadOnlyCollection<string>((violations ?? Enumerable.Empty<string>()).ToList());
        }

        public IReadOnlyList<string> Violations { get; }
        public bool IsValid => Violations.Count == 0;
    }

    public sealed class SettlementPlan
    {
        public SettlementPlan(string inputDigest, ulong seed, string archetype, GridRect compound,
            GridPoint gate, IEnumerable<GridPoint> route, IEnumerable<RoomEnvelope> rooms,
            IEnumerable<CirculationLink> circulation, IEnumerable<PlanReservation> reservations,
            int plannedCapacity, int candidatesConsidered, int candidateLimit, int routeNodesVisited,
            IEnumerable<string> repairs, string supplyStatus)
        {
            InputDigest = inputDigest;
            Seed = seed;
            Archetype = archetype;
            Compound = compound;
            Gate = gate;
            Route = new ReadOnlyCollection<GridPoint>(route.ToList());
            Rooms = new ReadOnlyCollection<RoomEnvelope>(rooms.ToList());
            Circulation = new ReadOnlyCollection<CirculationLink>(circulation.ToList());
            Reservations = new ReadOnlyCollection<PlanReservation>(reservations.ToList());
            PlannedCapacity = plannedCapacity;
            CandidatesConsidered = candidatesConsidered;
            CandidateLimit = candidateLimit;
            RouteNodesVisited = routeNodesVisited;
            Repairs = new ReadOnlyCollection<string>((repairs ?? Enumerable.Empty<string>()).ToList());
            SupplyStatus = supplyStatus;
            Digest = ComputeDigest();
        }

        public int SchemaVersion => P1Versions.PlanSchema;
        public string InputDigest { get; }
        public ulong Seed { get; }
        public string Archetype { get; }
        public GridRect Compound { get; }
        public GridPoint Gate { get; }
        public IReadOnlyList<GridPoint> Route { get; }
        public IReadOnlyList<RoomEnvelope> Rooms { get; }
        public IReadOnlyList<CirculationLink> Circulation { get; }
        public IReadOnlyList<PlanReservation> Reservations { get; }
        public int PlannedCapacity { get; }
        public int CandidatesConsidered { get; }
        public int CandidateLimit { get; }
        public int RouteNodesVisited { get; }
        public IReadOnlyList<string> Repairs { get; }
        public string SupplyStatus { get; }
        public string Digest { get; }

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, SchemaVersion);
            hash = StableHash.Add(hash, InputDigest);
            hash = StableHash.Add(hash, Seed);
            hash = StableHash.Add(hash, Archetype);
            hash = AddRect(hash, Compound);
            hash = AddPoint(hash, Gate);
            foreach (GridPoint point in Route) hash = AddPoint(hash, point);
            foreach (RoomEnvelope room in Rooms.OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                hash = StableHash.Add(hash, room.Id);
                hash = StableHash.Add(hash, room.Label);
                hash = AddRect(hash, room.Bounds);
                hash = AddPoint(hash, room.InteractionCell);
                hash = StableHash.Add(hash, room.PlannedCapacity);
            }
            foreach (CirculationLink link in Circulation.OrderBy(l => l.From + "\0" + l.To + "\0" + l.Kind, StringComparer.Ordinal))
            {
                hash = StableHash.Add(hash, link.From);
                hash = StableHash.Add(hash, link.To);
                hash = StableHash.Add(hash, link.Kind);
            }
            foreach (PlanReservation reservation in Reservations.OrderBy(r => r.Cell).ThenBy(r => r.Kind).ThenBy(r => r.Owner, StringComparer.Ordinal))
            {
                hash = AddPoint(hash, reservation.Cell);
                hash = StableHash.Add(hash, (int)reservation.Kind);
                hash = StableHash.Add(hash, reservation.Owner);
            }
            hash = StableHash.Add(hash, PlannedCapacity);
            hash = StableHash.Add(hash, CandidatesConsidered);
            hash = StableHash.Add(hash, CandidateLimit);
            hash = StableHash.Add(hash, RouteNodesVisited);
            foreach (string repair in Repairs) hash = StableHash.Add(hash, repair);
            hash = StableHash.Add(hash, SupplyStatus);
            return StableHash.Hex(hash);
        }

        private static ulong AddPoint(ulong hash, GridPoint point)
        {
            hash = StableHash.Add(hash, point.X);
            return StableHash.Add(hash, point.Z);
        }

        private static ulong AddRect(ulong hash, GridRect rect)
        {
            hash = StableHash.Add(hash, rect.MinX);
            hash = StableHash.Add(hash, rect.MinZ);
            hash = StableHash.Add(hash, rect.Width);
            return StableHash.Add(hash, rect.Height);
        }
    }

    public sealed class PlanningResult
    {
        private PlanningResult(SettlementPlan plan, IEnumerable<string> rejections, int candidatesConsidered, bool limitReached)
        {
            Plan = plan;
            Rejections = new ReadOnlyCollection<string>((rejections ?? Enumerable.Empty<string>()).ToList());
            CandidatesConsidered = candidatesConsidered;
            LimitReached = limitReached;
        }

        public SettlementPlan Plan { get; }
        public IReadOnlyList<string> Rejections { get; }
        public int CandidatesConsidered { get; }
        public bool LimitReached { get; }
        public bool Success => Plan != null;

        public static PlanningResult Accepted(SettlementPlan plan, IEnumerable<string> rejections) =>
            new PlanningResult(plan, rejections, plan.CandidatesConsidered, false);

        public static PlanningResult Rejected(IEnumerable<string> rejections, int candidatesConsidered, bool limitReached) =>
            new PlanningResult(null, rejections, candidatesConsidered, limitReached);
    }

    public static class SettlementPlanner
    {
        private static readonly GridPoint[] Neighbors =
        {
            new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1)
        };

        public static PlanningResult Plan(PlannerInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            TerrainSnapshot terrain = input.Terrain;
            SettlementProfile profile = input.Profile;
            string originalDigest = terrain.Digest;
            int interiorCapacity = Math.Max(0, (profile.CompoundWidth - 2) * (profile.CompoundHeight - 3));
            if (interiorCapacity < profile.RequiredCapacity)
            {
                return PlanningResult.Rejected(new[] { "impossible_capacity required=" + profile.RequiredCapacity + " available=" + interiorCapacity }, 0, false);
            }

            IReadOnlyList<ConnectedRegion> regions = ConnectedRegionAnalyzer.Analyze(terrain, IsPassable);
            int[] regionByCell = Enumerable.Repeat(-1, terrain.Width * terrain.Height).ToArray();
            foreach (ConnectedRegion region in regions)
                foreach (GridPoint point in region.Cells)
                    regionByCell[point.Z * terrain.Width + point.X] = region.Id;

            List<GridPoint> origins = CandidateOrigins(terrain, profile, input.Seed);
            List<string> rejections = new List<string>();
            int considered = 0;
            foreach (GridPoint origin in origins)
            {
                if (considered >= profile.MaxCandidates) break;
                considered++;
                SettlementPlan plan;
                string reason;
                if (TryCandidate(input, origin, regionByCell, considered, out plan, out reason))
                {
                    if (terrain.Digest != originalDigest) throw new InvalidOperationException("Planner mutated the terrain snapshot.");
                    PlanValidation validation = SettlementPlanValidator.Validate(input, plan);
                    if (validation.IsValid) return PlanningResult.Accepted(plan, rejections);
                    reason = "validation:" + string.Join("+", validation.Violations);
                }
                if (rejections.Count < 16) rejections.Add(origin + ":" + reason);
            }

            if (terrain.Digest != originalDigest) throw new InvalidOperationException("Planner mutated the terrain snapshot.");
            bool limitReached = origins.Count > considered && considered >= profile.MaxCandidates;
            if (rejections.Count == 0) rejections.Add("no_candidate_origins");
            return PlanningResult.Rejected(rejections, considered, limitReached);
        }

        private static List<GridPoint> CandidateOrigins(TerrainSnapshot terrain, SettlementProfile profile, ulong seed)
        {
            int centerX = terrain.Width / 2;
            int centerZ = terrain.Height / 2;
            List<GridPoint> origins = new List<GridPoint>();
            for (int z = 1; z + profile.CompoundHeight < terrain.Height; z++)
                for (int x = 1; x + profile.CompoundWidth < terrain.Width; x++)
                    origins.Add(new GridPoint(x, z));
            return origins.OrderBy(p => DistanceSquared(p.X + profile.CompoundWidth / 2, p.Z + profile.CompoundHeight / 2, centerX, centerZ))
                .ThenBy(p => CandidateTie(seed, p)).ThenBy(p => p).ToList();
        }

        private static bool TryCandidate(PlannerInput input, GridPoint origin, int[] regionByCell, int candidatesConsidered,
            out SettlementPlan plan, out string reason)
        {
            TerrainSnapshot terrain = input.Terrain;
            SettlementProfile profile = input.Profile;
            GridRect compound = new GridRect(origin.X, origin.Z, profile.CompoundWidth, profile.CompoundHeight);
            GridPoint gate = new GridPoint(compound.MinX + compound.Width / 2, compound.MinZ);
            GridPoint gateOutside = new GridPoint(gate.X, gate.Z - 1);
            if (!terrain.InBounds(gateOutside) || !IsPassable(terrain[gateOutside]))
            {
                plan = null;
                reason = "gate_outside_blocked";
                return false;
            }

            int regionId = regionByCell[gateOutside.Z * terrain.Width + gateOutside.X];
            if (regionId < 0)
            {
                plan = null;
                reason = "gate_has_no_passable_region";
                return false;
            }

            int corridorZ = compound.MinZ + compound.Height / 2;
            GridRect northRoom = new GridRect(compound.MinX + 1, compound.MinZ + 1,
                compound.Width - 2, corridorZ - compound.MinZ - 1);
            GridRect southRoom = new GridRect(compound.MinX + 1, corridorZ + 1,
                compound.Width - 2, compound.MaxZ - corridorZ - 1);
            if (northRoom.Height <= 0 || southRoom.Height <= 0)
            {
                plan = null;
                reason = "narrow_corridor_no_room_depth";
                return false;
            }
            GridPoint northInteraction = new GridPoint(northRoom.MinX + northRoom.Width / 2, northRoom.MaxZ);
            GridPoint southInteraction = new GridPoint(southRoom.MinX + southRoom.Width / 2, southRoom.MinZ);
            if (terrain[northInteraction].HasAny(TerrainTraits.Occupied) || terrain[southInteraction].HasAny(TerrainTraits.Occupied))
            {
                plan = null;
                reason = "interaction_obstructed";
                return false;
            }

            foreach (GridPoint point in compound.Points())
            {
                TerrainCell cell = terrain[point];
                if (!IsBuildable(cell) || cell.HasAny(TerrainTraits.Occupied | TerrainTraits.Water | TerrainTraits.Roofed))
                {
                    plan = null;
                    reason = "compound_cell_unusable";
                    return false;
                }
                if (regionByCell[point.Z * terrain.Width + point.X] != regionId)
                {
                    plan = null;
                    reason = "compound_crosses_disconnected_region";
                    return false;
                }
            }

            List<GridPoint> route;
            int routeVisited;
            if (!TryRouteToEdge(terrain, gateOutside, regionId, regionByCell, profile.MaxRouteNodes, out route, out routeVisited))
            {
                plan = null;
                reason = routeVisited >= profile.MaxRouteNodes ? "route_limit" : "no_edge_route";
                return false;
            }

            List<RoomEnvelope> rooms = new List<RoomEnvelope>
            {
                new RoomEnvelope("operations", "Operations (planned)", northRoom, northInteraction, northRoom.Area),
                new RoomEnvelope("stores", "Stores (planned)", southRoom, southInteraction, southRoom.Area)
            };
            List<CirculationLink> links = new List<CirculationLink>
            {
                new CirculationLink("gate", "corridor", "entry"),
                new CirculationLink("corridor", "operations", "adjacent"),
                new CirculationLink("corridor", "stores", "adjacent")
            };
            List<PlanReservation> reservations = BuildReservations(compound, gate, corridorZ, route, rooms);
            plan = new SettlementPlan(input.Digest, input.Seed, "rectangular-courtyard-v1", compound, gate,
                route, rooms, links, reservations, northRoom.Area + southRoom.Area, 0,
                profile.MaxCandidates, routeVisited, Enumerable.Empty<string>(), "not-modeled-p1");
            plan = new SettlementPlan(input.Digest, input.Seed, plan.Archetype, compound, gate, route, rooms,
                links, reservations, plan.PlannedCapacity, candidatesConsidered, profile.MaxCandidates,
                routeVisited, plan.Repairs, plan.SupplyStatus);
            reason = null;
            return true;
        }

        private static List<PlanReservation> BuildReservations(GridRect compound, GridPoint gate, int corridorZ,
            IEnumerable<GridPoint> route, IEnumerable<RoomEnvelope> rooms)
        {
            List<PlanReservation> result = new List<PlanReservation>();
            foreach (GridPoint point in compound.Points())
            {
                bool perimeter = point.X == compound.MinX || point.X == compound.MaxX || point.Z == compound.MinZ || point.Z == compound.MaxZ;
                if (perimeter && !point.Equals(gate)) result.Add(new PlanReservation(point, ReservationKind.Structure, "outer-wall"));
            }
            result.Add(new PlanReservation(gate, ReservationKind.GateOpening, "primary-gate"));
            for (int x = compound.MinX + 1; x < compound.MaxX; x++)
                result.Add(new PlanReservation(new GridPoint(x, corridorZ), ReservationKind.Route, "main-corridor"));
            foreach (GridPoint point in route) result.Add(new PlanReservation(point, ReservationKind.Route, "edge-approach"));
            foreach (RoomEnvelope room in rooms)
            {
                foreach (GridPoint point in room.Bounds.Points()) result.Add(new PlanReservation(point, ReservationKind.Room, room.Id));
                result.Add(new PlanReservation(room.InteractionCell, ReservationKind.Interaction, room.Id));
            }
            GridPoint roofSupport = new GridPoint(compound.MinX + compound.Width / 2, compound.MinZ + 1);
            result.Add(new PlanReservation(roofSupport, ReservationKind.RoofSupport, "planned-support"));
            for (int x = compound.MinX + 1; x < compound.MaxX; x++)
                result.Add(new PlanReservation(new GridPoint(x, compound.MaxZ - 1), ReservationKind.PowerNetwork, "planned-grid"));
            result.Add(new PlanReservation(new GridPoint(compound.MinX + 1, compound.MinZ + 1), ReservationKind.DefenseSector, "west-sector"));
            result.Add(new PlanReservation(new GridPoint(compound.MaxX - 1, compound.MinZ + 1), ReservationKind.DefenseSector, "east-sector"));
            return result;
        }

        private static bool TryRouteToEdge(TerrainSnapshot terrain, GridPoint start, int regionId, int[] regionByCell,
            int maxNodes, out List<GridPoint> route, out int visitedCount)
        {
            Queue<GridPoint> pending = new Queue<GridPoint>();
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            HashSet<GridPoint> visited = new HashSet<GridPoint>();
            pending.Enqueue(start);
            visited.Add(start);
            GridPoint end = start;
            bool found = terrain[start].Has(TerrainTraits.MapEdge);
            while (pending.Count > 0 && !found && visited.Count < maxNodes)
            {
                GridPoint current = pending.Dequeue();
                for (int i = 0; i < Neighbors.Length; i++)
                {
                    GridPoint next = new GridPoint(current.X + Neighbors[i].X, current.Z + Neighbors[i].Z);
                    if (!terrain.InBounds(next) || visited.Contains(next)) continue;
                    if (regionByCell[next.Z * terrain.Width + next.X] != regionId) continue;
                    visited.Add(next);
                    previous[next] = current;
                    pending.Enqueue(next);
                    if (terrain[next].Has(TerrainTraits.MapEdge))
                    {
                        end = next;
                        found = true;
                        break;
                    }
                }
            }
            visitedCount = visited.Count;
            route = new List<GridPoint>();
            if (!found) return false;
            GridPoint step = end;
            route.Add(step);
            while (!step.Equals(start))
            {
                step = previous[step];
                route.Add(step);
            }
            route.Reverse();
            return true;
        }

        private static bool IsPassable(TerrainCell cell) => cell.Has(TerrainTraits.Standable) && !cell.HasAny(TerrainTraits.Water | TerrainTraits.Occupied);
        private static bool IsBuildable(TerrainCell cell) => cell.Has(TerrainTraits.Standable | TerrainTraits.Buildable);
        private static int DistanceSquared(int x, int z, int targetX, int targetZ) { int dx = x - targetX; int dz = z - targetZ; return dx * dx + dz * dz; }
        private static ulong CandidateTie(ulong seed, GridPoint point) { ulong hash = StableHash.Add(seed, point.X); return StableHash.Add(hash, point.Z); }
    }

    public static class SettlementPlanValidator
    {
        public static PlanValidation Validate(PlannerInput input, SettlementPlan plan)
        {
            List<string> violations = new List<string>();
            if (input == null || plan == null) return new PlanValidation(new[] { "missing_input_or_plan" });
            if (plan.InputDigest != input.Digest) violations.Add("input_digest_mismatch");
            if (plan.PlannedCapacity < input.Profile.RequiredCapacity) violations.Add("capacity_shortfall");
            if (plan.CandidatesConsidered > input.Profile.MaxCandidates) violations.Add("candidate_limit_exceeded");
            if (plan.RouteNodesVisited > input.Profile.MaxRouteNodes) violations.Add("route_limit_exceeded");
            if (plan.Route.Count == 0 || !plan.Route[0].Equals(new GridPoint(plan.Gate.X, plan.Gate.Z - 1))) violations.Add("route_not_at_gate");
            if (plan.Route.Count == 0 || !input.Terrain[plan.Route[plan.Route.Count - 1]].Has(TerrainTraits.MapEdge)) violations.Add("route_not_at_edge");
            for (int i = 1; i < plan.Route.Count; i++)
            {
                int distance = Math.Abs(plan.Route[i].X - plan.Route[i - 1].X) + Math.Abs(plan.Route[i].Z - plan.Route[i - 1].Z);
                if (distance != 1) { violations.Add("route_not_contiguous"); break; }
            }
            foreach (RoomEnvelope room in plan.Rooms)
            {
                if (!room.Bounds.Contains(room.InteractionCell)) violations.Add("interaction_outside_room:" + room.Id);
                if (input.Terrain[room.InteractionCell].HasAny(TerrainTraits.Occupied | TerrainTraits.Water)) violations.Add("interaction_obstructed:" + room.Id);
            }
            HashSet<GridPoint> structures = new HashSet<GridPoint>(plan.Reservations.Where(r => r.Kind == ReservationKind.Structure).Select(r => r.Cell));
            foreach (PlanReservation reservation in plan.Reservations)
            {
                if (!input.Terrain.InBounds(reservation.Cell)) violations.Add("reservation_out_of_bounds:" + reservation.Kind);
                if ((reservation.Kind == ReservationKind.Route || reservation.Kind == ReservationKind.GateOpening || reservation.Kind == ReservationKind.Interaction) && structures.Contains(reservation.Cell))
                    violations.Add("reservation_structure_conflict:" + reservation.Kind);
            }
            if (!plan.Reservations.Any(r => r.Kind == ReservationKind.GateOpening)) violations.Add("missing_gate");
            if (!plan.Reservations.Any(r => r.Kind == ReservationKind.RoofSupport)) violations.Add("missing_roof_support");
            if (!plan.Reservations.Any(r => r.Kind == ReservationKind.PowerNetwork)) violations.Add("missing_power_reservation");
            if (!plan.Reservations.Any(r => r.Kind == ReservationKind.DefenseSector)) violations.Add("missing_defense_sector");
            foreach (RoomEnvelope room in plan.Rooms)
                if (!plan.Circulation.Any(link => link.From == room.Id || link.To == room.Id)) violations.Add("room_disconnected:" + room.Id);
            return new PlanValidation(violations.Distinct().OrderBy(v => v, StringComparer.Ordinal));
        }
    }
}
