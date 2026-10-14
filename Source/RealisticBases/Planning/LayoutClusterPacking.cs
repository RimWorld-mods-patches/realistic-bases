using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Layout engine v2 front half (docs/plans/2026-10-12-layout-engine-v2.md):
    // a main hall plus comb branches, with room clusters packed along their
    // frontage and sharing walls. Furnishing, cooler exhaust, pockets and the
    // whole back half (yards, power, courts, enclosure, gates) live in
    // ProceduralLayoutPlanner.
    internal sealed partial class LayoutAttempt
    {
        private readonly Dictionary<GridPoint, string> wallOwner = new Dictionary<GridPoint, string>();
        private readonly HashSet<GridPoint> coolerCells = new HashSet<GridPoint>();
        private LayoutStyle style;
        private GridPoint frontPoint;
        private int branchSpacing;

        private LayoutStyle Style => style ?? (style = LayoutStyle.Select(doctrine, input.Equipment.Kit, input.Requirements));

        private bool PlaceClusteredRooms()
        {
            if (!BuildSpine()) return false;
            if (jointSite != null && input.Requirements.ReservePopulationCirculation && !ReserveFieldApproach()) return false;
            frontPoint = FrontEnd();
            int depth = LayoutClusters.TypicalDepth(demand.Rooms);
            // Two rooms back to back between parallel branches share one rear wall.
            branchSpacing = 2 * (depth + 2);
            notes.Add("engine=Clusters style=" + Style.Kind + " typicalDepth=" + depth + " branchSpacing=" + branchSpacing +
                " front=" + frontPoint);
            // Mountain bases lay out their comb up front and let rock shape
            // the clusters; open-ground bases lay corridors first (LayoutRibs).
            if (Style.Kind == LayoutStyleKind.Mountain)
            {
                int combs = Math.Max(1, demand.Rooms.Count / 4);
                for (int i = 0; i < combs; i++) AddCombBranch("comb-" + i, anchor);
                foreach (LayoutRoomDemand room in LayoutClusters.Order(demand.Rooms))
                    if (!PlaceWithRepairs(room)) return false;
                return true;
            }
            LayCorridors(out LayoutRoomDemand hub);
            foreach (LayoutRoomDemand room in LayoutClusters.Order(demand.Rooms))
            {
                if (hub != null && room.Id == hub.Id) continue;
                if (!PlaceInBlocks(room)) { ReleaseReserves(); return false; }
            }
            ReleaseReserves();
            return true;
        }

        // The spine end nearer the map edge faces arrivals; housing keeps away from it.
        private GridPoint FrontEnd()
        {
            Segment main = segments.First(s => s.Main);
            GridPoint first = main.Sections[0][0], last = main.Sections[main.Sections.Count - 1][0];
            Func<GridPoint, int> edge = c => Math.Min(Math.Min(c.X, c.Z), Math.Min(grid.Width - 1 - c.X, grid.Height - 1 - c.Z));
            return edge(first) <= edge(last) ? first : last;
        }

        private LayoutClusterKind ClusterOf(string roomId)
        {
            LayoutRoom room;
            if (placed.TryGetValue(roomId, out room)) return LayoutClusters.Of(room.Kind);
            LayoutRoomDemand d = demand.Rooms.FirstOrDefault(r => r.Id == roomId);
            return d == null ? LayoutClusterKind.Power : LayoutClusters.Of(d.Kind);
        }

        // Where a room should go. Superstructure and town bases grow as one
        // building: the first room aims at the middle of the main hallway and
        // every later room at the centre of all rooms placed so far.
        // Mountain bases keep per-cluster seed points, since rock shapes them.
        private GridPoint ClusterTarget(LayoutRoomDemand room)
        {
            if (Style.Kind == LayoutStyleKind.Mountain) return MountainClusterTarget(room);
            List<LayoutRoom> placedRooms = rooms.Where(r => r.Kind != LayoutRoomKind.MortarMagazine).ToList();
            if (placedRooms.Count == 0) return SpineMiddle();
            return Centre(placedRooms);
        }

        private GridPoint SpineMiddle()
        {
            Segment main = segments.First(s => s.Main);
            return main.Sections[main.Sections.Count / 2][0];
        }

        private static GridPoint Centre(List<LayoutRoom> group) =>
            new GridPoint((int)Math.Round(group.Average(r => r.Bounds.MinX + r.Bounds.Width / 2.0)),
                (int)Math.Round(group.Average(r => r.Bounds.MinZ + r.Bounds.Height / 2.0)));

        // Placed rooms of the same cluster, or null before the first one.
        private GridPoint? MatesCentre(LayoutRoomDemand room)
        {
            LayoutClusterKind kind = LayoutClusters.Of(room.Kind);
            List<LayoutRoom> mates = rooms.Where(r => r.Kind != LayoutRoomKind.MortarMagazine && LayoutClusters.Of(r.Kind) == kind).ToList();
            return mates.Count == 0 ? (GridPoint?)null : Centre(mates);
        }

        // Beside placed cluster mates, otherwise a cluster-specific seed point around the anchor.
        private GridPoint MountainClusterTarget(LayoutRoomDemand room)
        {
            LayoutClusterKind kind = LayoutClusters.Of(room.Kind);
            if (kind == LayoutClusterKind.Power)
                kind = rooms.Any(r => LayoutClusters.Of(r.Kind) == LayoutClusterKind.Production) ? LayoutClusterKind.Production : kind;
            List<LayoutRoom> mates = rooms.Where(r => r.Kind != LayoutRoomKind.MortarMagazine && LayoutClusters.Of(r.Kind) == kind).ToList();
            if (mates.Count > 0) return Centre(mates);
            GridPoint away = new GridPoint(Math.Sign(anchor.X - frontPoint.X), Math.Sign(anchor.Z - frontPoint.Z));
            switch (kind)
            {
                case LayoutClusterKind.Food:
                    return jointSite != null ? jointSite.InnerGate : anchor;
                case LayoutClusterKind.Living:
                    return new GridPoint(anchor.X + away.X * 10, anchor.Z + away.Z * 10);
                default:
                    return anchor;
            }
        }

        private List<GridPoint> ClusterFrontage(LayoutRoomDemand room)
        {
            if (Style.Kind != LayoutStyleKind.Mountain)
                return segments.SelectMany(s => s.Cells).Distinct().OrderBy(WalkCost)
                    .ThenBy(c => LayoutSeed.Key(LayoutSeed.Sub(seed, room.Id), c.X, c.Z, growths)).ToList();
            GridPoint target = ClusterTarget(room);
            return segments.SelectMany(s => s.Cells).Distinct()
                .OrderBy(c => Manhattan(c, target))
                .ThenBy(c => LayoutSeed.Key(LayoutSeed.Sub(seed, room.Id), c.X, c.Z, repairs)).ToList();
        }

        private bool SharableWall(GridPoint cell, out string owner) =>
            wallOwner.TryGetValue(cell, out owner) && roomWalls.Contains(cell) && !coolerCells.Contains(cell);

        private void RecordClusterRoom(LayoutRoom room, List<string> sharedWith)
        {
            foreach (GridPoint c in room.Bounds.Points())
                if (room.IsEdge(c) && !room.IsDoor(c) && !wallOwner.ContainsKey(c)) wallOwner[c] = room.Id;
            foreach (LayoutFixture cooler in room.Fixtures.Where(f => LayoutFixtureKinds.ThroughWall(f.Kind))) coolerCells.UnionWith(cooler.Cells);
            if (sharedWith != null && sharedWith.Count > 0)
                ProceduralLayoutPlanner.Note(diagnostics, "cluster", "shared_walls", room.Id + " cells=" + sharedWith.Count +
                    " with=" + string.Join("+", sharedWith.Distinct().OrderBy(s => s, StringComparer.Ordinal)));
        }

        // Clusters engine placement score (lower is better): reuse walls and,
        // on open ground, stay close to the dining hall over corridors and to
        // cluster mates; mountain bases keep per-cluster targets and housing
        // away from the arrival front.
        private double? ClusterScore(LayoutRoomDemand room, GridRect bounds, GridPoint outside, List<string> sharedWith)
        {
            LayoutClusterKind kind = LayoutClusters.Of(room.Kind);
            int same = sharedWith.Count(o => ClusterOf(o) == kind);
            int other = sharedWith.Count - same;
            if (Style.DetachedClusters && (other > 0 || TouchesOtherCluster(bounds, kind))) return null;
            GridPoint center = new GridPoint(bounds.MinX + bounds.Width / 2, bounds.MinZ + bounds.Height / 2);
            double score = 0.8 * Manhattan(center, ClusterTarget(room));
            score -= Style.SameClusterShare * same + Style.OtherClusterShare * other;
            if (Style.Kind == LayoutStyleKind.Mountain)
            {
                if (kind == LayoutClusterKind.Living || room.Kind == LayoutRoomKind.Jail)
                    score -= 0.3 * Math.Min(30, Manhattan(center, frontPoint));
                return score;
            }
            score = WalkCost(outside) - Style.SameClusterShare * same - Style.OtherClusterShare * other;
            GridPoint? mates = MatesCentre(room);
            if (mates.HasValue) score += 0.2 * Manhattan(center, mates.Value);
            return score;
        }

        // Detached buildings keep at least one open cell from other clusters' walls.
        private bool TouchesOtherCluster(GridRect bounds, LayoutClusterKind kind)
        {
            for (int z = bounds.MinZ - 1; z <= bounds.MaxZ + 1; z++)
                for (int x = bounds.MinX - 1; x <= bounds.MaxX + 1; x++)
                {
                    GridPoint c = new GridPoint(x, z);
                    if (bounds.Contains(c) && !(x == bounds.MinX || x == bounds.MaxX || z == bounds.MinZ || z == bounds.MaxZ)) continue;
                    string owner;
                    if (wallOwner.TryGetValue(c, out owner) && ClusterOf(owner) != kind) return true;
                }
            return false;
        }

        // A hallway branch perpendicular to an existing segment, nearest the
        // target first, kept branchSpacing away from parallel streets so the
        // blocks between branches fit two rooms back to back.
        private bool AddCombBranch(string id, GridPoint target)
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
            int length = Math.Max(6, branchSpacing + 2);
            int spacing = Math.Max(4, branchSpacing);
            foreach (KeyValuePair<GridPoint, GridPoint> root in roots
                .OrderBy(r => Manhattan(new GridPoint(r.Key.X + r.Value.X * 3, r.Key.Z + r.Value.Z * 3), target))
                .ThenBy(r => LayoutSeed.Key(branchSeed, r.Key.X, r.Key.Z, r.Value.X * 3 + r.Value.Z)))
            {
                if (stats.Evaluations >= input.Budget.MaxEvaluations) { stats.BudgetExhausted = true; return false; }
                stats.Evaluations++;
                GridPoint lateral = new GridPoint(root.Value.Z, root.Value.X);
                Segment branch = new Segment { Id = id, Main = false, Axis = root.Value, Root = root.Key };
                for (int k = 1; k <= length; k++)
                {
                    GridPoint c = new GridPoint(root.Key.X + k * root.Value.X, root.Key.Z + k * root.Value.Z);
                    if (!OkForStreet(c)) break;
                    GridPoint ahead = new GridPoint(c.X + root.Value.X, c.Z + root.Value.Z);
                    if (grid.InBounds(ahead) && occ[grid.Index(ahead)] == Street) break;
                    if (ParallelStreetWithin(c, lateral, spacing)) break;
                    branch.Sections.Add(new List<GridPoint> { c });
                }
                if (branch.Sections.Count < 3) continue;
                if (!Claim(branch)) continue;
                segments.Add(branch);
                return true;
            }
            return false;
        }

        private bool ParallelStreetWithin(GridPoint c, GridPoint lateral, int spacing)
        {
            for (int j = 1; j < spacing; j++)
                foreach (int sign in new[] { -1, 1 })
                {
                    GridPoint n = new GridPoint(c.X + sign * j * lateral.X, c.Z + sign * j * lateral.Z);
                    if (grid.InBounds(n) && occ[grid.Index(n)] == Street) return true;
                }
            return false;
        }
    }
}
