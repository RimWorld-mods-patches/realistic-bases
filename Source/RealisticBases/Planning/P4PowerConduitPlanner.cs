using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Deterministic power connection tree shared by the runtime spawner and
    // outcome diagnostics. The terrain callback lets the runtime apply the
    // selected conduit def's actual vanilla terrain affordance.
    public static class P4PowerConduitPlanner
    {
        public static IReadOnlyList<GridPoint> Plan(ProceduralLayoutInput input, ProceduralLayout layout,
            IEnumerable<GridPoint> allowedCells = null, Func<GridPoint, bool> canBuildOnTerrain = null)
        {
            if (layout.Power == null) return Array.Empty<GridPoint>();
            List<GridPoint> terminals = layout.AllFixtures.Where(f => f.Watts != 0 ||
                f.Kind == LayoutFixtureKind.Generator || f.Kind == LayoutFixtureKind.Battery)
                .Select(f => f.Cells[0]).Concat(layout.PowerYards.Select(y => y.Cells[0]))
                .Concat(layout.Turret == null ? Enumerable.Empty<GridPoint>() :
                    layout.Turret.All.SelectMany(t => t.Footprint.Take(1)))
                .Distinct().ToList();
            if (terminals.Count == 0) throw new InvalidOperationException("Powered layout has no terminals");

            HashSet<GridPoint> intactRock = new HashSet<GridPoint>(layout.NaturalWalls.Concat(
                layout.Rooms.SelectMany(r => r.NaturalWalls)));
            HashSet<GridPoint> allowed = new HashSet<GridPoint>(allowedCells ?? P4BTurretPlanner.PowerAccessCells(layout));
            bool Legal(GridPoint cell)
            {
                if (!allowed.Contains(cell) || !input.Terrain.InBounds(cell) || intactRock.Contains(cell) ||
                    input.Terrain[cell].Has(TerrainTraits.Water) ||
                    input.Terrain[cell].Has(TerrainTraits.NaturalRock) && !layout.Excavation.Contains(cell)) return false;
                return canBuildOnTerrain == null
                    ? input.Terrain[cell].Has(TerrainTraits.Buildable)
                    : canBuildOnTerrain(cell);
            }

            HashSet<GridPoint> tree = new HashSet<GridPoint> { terminals[0] };
            GridPoint[] deltas = { new GridPoint(0, -1), new GridPoint(-1, 0),
                new GridPoint(1, 0), new GridPoint(0, 1) };
            foreach (GridPoint terminal in terminals.Skip(1))
            {
                Queue<GridPoint> queue = new Queue<GridPoint>();
                Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
                queue.Enqueue(terminal);
                previous[terminal] = terminal;
                GridPoint reached = default(GridPoint);
                bool found = false;
                while (queue.Count > 0 && !found)
                {
                    GridPoint here = queue.Dequeue();
                    if (tree.Contains(here)) { reached = here; found = true; break; }
                    foreach (GridPoint delta in deltas)
                    {
                        GridPoint next = new GridPoint(here.X + delta.X, here.Z + delta.Z);
                        if (!Legal(next) || previous.ContainsKey(next)) continue;
                        previous[next] = here;
                        queue.Enqueue(next);
                    }
                }
                if (!found) throw new InvalidOperationException("Power terminal disconnected at " + terminal);
                for (GridPoint cell = reached; ; cell = previous[cell])
                {
                    tree.Add(cell);
                    if (cell.Equals(terminal)) break;
                }
            }
            return tree.OrderBy(c => c).ToList();
        }

        public static int ConduitCellCount(ProceduralLayout layout, IEnumerable<GridPoint> routeCells)
        {
            HashSet<GridPoint> transmitterCells = new HashSet<GridPoint>(layout.PowerYards.SelectMany(y => y.Cells)
                .Concat(layout.AllFixtures.Where(f => f.Kind == LayoutFixtureKind.Generator ||
                    f.Kind == LayoutFixtureKind.Battery).SelectMany(f => f.Cells)));
            return routeCells.Count(cell => !transmitterCells.Contains(cell));
        }
    }
}
