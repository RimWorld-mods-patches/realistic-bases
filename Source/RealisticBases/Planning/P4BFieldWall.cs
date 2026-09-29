using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // Checks a field-facing wall physically returned into the main perimeter.
    // The caller owns all final footprints; validation itself is pure.
    public static class P4BFieldWall
    {
        private static readonly GridPoint[] Neighbors =
        {
            new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1)
        };

        public static IReadOnlyList<string> Validate(TerrainSnapshot terrain, IEnumerable<GridPoint> fields,
            IEnumerable<GridPoint> baselineWalls, IEnumerable<GridPoint> fieldWalls,
            IEnumerable<GridPoint> gates, IEnumerable<GridPoint> posts,
            GridPoint accessOrigin, IEnumerable<GridPoint> constructedRoof = null)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            HashSet<GridPoint> crop = new HashSet<GridPoint>(fields ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> baseline = new HashSet<GridPoint>(baselineWalls ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> fieldWall = new HashSet<GridPoint>(fieldWalls ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> solid = new HashSet<GridPoint>(baseline.Concat(fieldWall));
            HashSet<GridPoint> doors = new HashSet<GridPoint>(gates ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> outsidePosts = new HashSet<GridPoint>(posts ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> roofs = new HashSet<GridPoint>(constructedRoof ?? Enumerable.Empty<GridPoint>());
            List<string> errors = new List<string>();
            if (crop.Count == 0) errors.Add("field_missing");
            if (baseline.Count == 0 || fieldWall.Count == 0 || doors.Count == 0)
                errors.Add("field_wall_or_gate_missing");
            if (outsidePosts.Count == 0) errors.Add("field_exterior_posts_missing");
            if (crop.Any(c => !terrain.InBounds(c) || !Walkable(c) ||
                !terrain[c].Has(TerrainTraits.Fertile) || terrain[c].Has(TerrainTraits.Roofed) || roofs.Contains(c)))
                errors.Add("field_not_fertile_unroofed_ground");
            if (solid.Overlaps(doors) || crop.Overlaps(solid) || crop.Overlaps(doors) ||
                outsidePosts.Overlaps(solid) || outsidePosts.Overlaps(doors) || outsidePosts.Overlaps(crop))
                errors.Add("field_wall_occupancy_overlap");
            if (fieldWall.Concat(doors).Any(c => !terrain.InBounds(c) ||
                !terrain[c].Has(TerrainTraits.Buildable) || !Walkable(c)))
                errors.Add("field_wall_terrain_invalid");
            foreach (GridPoint door in doors)
            {
                if (!((solid.Contains(new GridPoint(door.X - 1, door.Z)) &&
                       solid.Contains(new GridPoint(door.X + 1, door.Z))) ||
                      (solid.Contains(new GridPoint(door.X, door.Z - 1)) &&
                       solid.Contains(new GridPoint(door.X, door.Z + 1)))))
                    errors.Add("field_gate_not_in_wall");
            }
            HashSet<GridPoint> attached = Flood(baseline, c => solid.Contains(c) || doors.Contains(c));
            if (fieldWall.Any(c => !attached.Contains(c))) errors.Add("field_wall_detached");
            HashSet<GridPoint> closed = new HashSet<GridPoint>(solid);
            closed.UnionWith(doors);
            HashSet<GridPoint> edge = Flood(terrain.Points().Where(c => terrain[c].Has(TerrainTraits.MapEdge)),
                c => Walkable(c) && !closed.Contains(c));
            if (crop.Any(edge.Contains)) errors.Add("field_exposed_behind_closed_wall");
            if (outsidePosts.Any(c => !edge.Contains(c))) errors.Add("post_not_outside_field_wall");
            HashSet<GridPoint> open = Flood(new[] { accessOrigin }, c => Walkable(c) && !solid.Contains(c));
            if (!crop.Any(open.Contains)) errors.Add("field_inaccessible_with_gates_open");
            return new ReadOnlyCollection<string>(errors.Distinct().ToList());

            bool Walkable(GridPoint c) => terrain.InBounds(c) &&
                (terrain[c].Has(TerrainTraits.Standable) || terrain[c].Has(TerrainTraits.ClearablePlant)) &&
                !terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.NaturalRock | TerrainTraits.Occupied);
        }

        private static HashSet<GridPoint> Flood(IEnumerable<GridPoint> starts, Func<GridPoint, bool> legal)
        {
            HashSet<GridPoint> seen = new HashSet<GridPoint>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            foreach (GridPoint start in starts)
                if (legal(start) && seen.Add(start)) queue.Enqueue(start);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                foreach (GridPoint delta in Neighbors)
                {
                    GridPoint next = new GridPoint(here.X + delta.X, here.Z + delta.Z);
                    if (legal(next) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return seen;
        }
    }
}
