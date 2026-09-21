using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // Final post-construction resident navigation model. Cardinal movement only:
    // diagonal corner contact never proves access.
    public sealed class PlannedNavigationGrid
    {
        private static readonly GridPoint[] Steps = { new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1) };
        private readonly TerrainSnapshot terrain;
        private readonly PhysicalSettlementPlan plan;
        private readonly HashSet<GridPoint> blocked;
        private readonly HashSet<GridPoint> reachable;
        private readonly HashSet<GridPoint> doors;

        private PlannedNavigationGrid(TerrainSnapshot terrain, PhysicalSettlementPlan plan, HashSet<GridPoint> blocked,
            HashSet<GridPoint> reachable, HashSet<GridPoint> doors)
        { this.terrain = terrain; this.plan = plan; this.blocked = blocked; this.reachable = reachable; this.doors = doors; }

        public IReadOnlyCollection<GridPoint> PublicReachable => new ReadOnlyCollection<GridPoint>(reachable.OrderBy(c => c).ToList());
        public bool IsBlocked(GridPoint cell) => blocked.Contains(cell);
        public bool IsPublicReachable(GridPoint cell) => reachable.Contains(cell);

        public static PlannedNavigationGrid Build(TerrainSnapshot terrain, PhysicalSettlementPlan plan)
        {
            HashSet<GridPoint> doors = new HashSet<GridPoint>(plan.Gates.Concat(plan.ReinforcementGates).Concat(plan.Facilities.Where(f => f.Roofed).Select(f => f.Door)));
            HashSet<GridPoint> blocked = new HashSet<GridPoint>();
            HashSet<GridPoint> excavated = new HashSet<GridPoint>(plan.TerrainMutations.ExcavationCells);
            foreach (GridPoint cell in terrain.Points()) if (terrain[cell].Has(TerrainTraits.Water | TerrainTraits.Occupied) && !excavated.Contains(cell)) blocked.Add(cell);
            foreach (GridPoint cell in plan.Perimeter.Concat(plan.ReinforcementPerimeter)) blocked.Add(cell);
            foreach (PhysicalFacility facility in plan.Facilities.Where(f => f.Roofed))
                foreach (GridPoint cell in facility.Bounds.Points()) if (IsEdge(facility.Bounds, cell) && !doors.Contains(cell)) blocked.Add(cell);
            foreach (PhysicalPlacement placement in plan.Placements.Where(BlocksMovement)) blocked.Add(placement.Cell);
            foreach (GridPoint door in doors) blocked.Remove(door);
            HashSet<GridPoint> roomCells = new HashSet<GridPoint>(plan.Facilities.Where(f => f.Roofed).SelectMany(f => f.Bounds.Points()));
            HashSet<GridPoint> publicCells = new HashSet<GridPoint>(plan.Compound.Points().Where(c => !roomCells.Contains(c) || doors.Contains(c)));
            publicCells.ExceptWith(blocked);
            publicCells.UnionWith(doors.Where(c => plan.Compound.Contains(c) && !blocked.Contains(c)));
            GridPoint root = InsideGate(plan.Gates[0], plan.Compound);
            return new PlannedNavigationGrid(terrain, plan, blocked, Flood(terrain, root, publicCells.Contains), doors);
        }

        public IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (plan.Gates.Count == 0) return new[] { "no_primary_gate" };
            GridPoint root = InsideGate(plan.Gates[0], plan.Compound);
            if (!reachable.Contains(root)) errors.Add("public_root_blocked");
            foreach (GridPoint gate in plan.Gates) if (!reachable.Contains(InsideGate(gate, plan.Compound))) errors.Add("gate_disconnected_" + gate);
            foreach (PhysicalFacility facility in plan.Facilities.Where(f => f.Roofed)) ValidateFacility(facility, errors);
            return new ReadOnlyCollection<string>(errors.Distinct().ToList());
        }

        private void ValidateFacility(PhysicalFacility facility, List<string> errors)
        {
            if (!Neighbors(facility.Door).Any(reachable.Contains)) { errors.Add("facility_door_disconnected_" + facility.Id); return; }
            HashSet<GridPoint> roomWalkable = new HashSet<GridPoint>(facility.Bounds.Points().Where(c => !blocked.Contains(c) && (!IsEdge(facility.Bounds, c) || doors.Contains(c))));
            roomWalkable.Add(facility.Door);
            HashSet<GridPoint> local = Flood(terrain, facility.Door, roomWalkable.Contains);
            IEnumerable<GridPoint> requirements = facility.InteractionCells.Concat(plan.Placements.Where(p => p.FacilityId == facility.Id && BlocksMovement(p)).Select(p => p.Cell));
            foreach (GridPoint target in requirements.Distinct())
                if (!local.Contains(target) && !Neighbors(target).Any(c => roomWalkable.Contains(c) && local.Contains(c))) errors.Add("facility_target_disconnected_" + facility.Id + "_" + target);
        }

        private static bool BlocksMovement(PhysicalPlacement placement) => placement.Kind != PhysicalPlacementKind.Conduit && placement.Kind != PhysicalPlacementKind.FoodStock && placement.Kind != PhysicalPlacementKind.FuelStock;
        private static HashSet<GridPoint> Flood(TerrainSnapshot terrain, GridPoint start, Func<GridPoint, bool> allowed)
        {
            HashSet<GridPoint> result = new HashSet<GridPoint>(); if (!allowed(start)) return result;
            Queue<GridPoint> queue = new Queue<GridPoint>(); queue.Enqueue(start); result.Add(start);
            while (queue.Count > 0) foreach (GridPoint next in Neighbors(queue.Dequeue())) if (terrain.InBounds(next) && allowed(next) && result.Add(next)) queue.Enqueue(next);
            return result;
        }
        private static GridPoint InsideGate(GridPoint gate, GridRect compound) => gate.Z == compound.MinZ ? new GridPoint(gate.X, gate.Z + 1) : gate.Z == compound.MaxZ ? new GridPoint(gate.X, gate.Z - 1) : gate.X == compound.MinX ? new GridPoint(gate.X + 1, gate.Z) : new GridPoint(gate.X - 1, gate.Z);
        private static bool IsEdge(GridRect rect, GridPoint cell) => cell.X == rect.MinX || cell.X == rect.MaxX || cell.Z == rect.MinZ || cell.Z == rect.MaxZ;
        private static IEnumerable<GridPoint> Neighbors(GridPoint c) { foreach (GridPoint step in Steps) yield return new GridPoint(c.X + step.X, c.Z + step.Z); }
    }

    public static class P4Connectivity
    {
        public static IReadOnlyList<string> Validate(TerrainSnapshot terrain, PhysicalSettlementPlan plan) => PlannedNavigationGrid.Build(terrain, plan).Validate();
    }
}
