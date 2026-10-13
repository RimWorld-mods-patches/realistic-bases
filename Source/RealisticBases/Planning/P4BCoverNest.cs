using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // One defensive post: a row of standing slots beside a perimeter wall,
    // reached from an access gate across two exposed gate-axis cells and
    // wrapped in cover on every side an attacker can shoot a slot from.
    // Compound-gate, sally-port and field-gate posts are the same kind.
    public sealed class P4BPost
    {
        internal P4BPost(string side, GridPoint outward, GridPoint gate, IEnumerable<GridPoint> route,
            IEnumerable<GridPoint> slots, IEnumerable<GridPoint> cover, GridPoint? wallReturn)
        {
            Side = side; Outward = outward; AccessGate = gate;
            Route = new ReadOnlyCollection<GridPoint>(route.ToList());
            Slots = new ReadOnlyCollection<GridPoint>(slots.ToList());
            CoverCells = P4BAttackSide.Copy(cover);
            WallReturn = wallReturn;
            if (Slots.Count == 0) throw new ArgumentException("A post needs a slot");
        }
        public string Side { get; }
        public GridPoint Outward { get; }
        public GridPoint AccessGate { get; }
        // Gate, the two exposed gate-axis cells, then every slot in order.
        public IReadOnlyList<GridPoint> Route { get; }
        public IReadOnlyList<GridPoint> Slots { get; }
        public IReadOnlyList<GridPoint> CoverCells { get; }
        // A wall closing the far end of the row against the perimeter, kept
        // only where that cell faces no threat; otherwise it is cover.
        public GridPoint? WallReturn { get; }
        public GridPoint Cell => Slots[0];
    }

    public static class P4BCoverNest
    {
        public const int MaxSlots = 4;
        // A slot is useful when it engages this many samples of one front.
        public const int MinimumShots = 4;
        public const int MinimumRange = 8, MaximumRange = 25;

        // Barricades are built from the base's own construction stuff;
        // sandbags only where barricades are not legal for the stage.
        public static string CoverDefName(ProceduralLayoutInput input) =>
            new[] { "Barricade", "Sandbags" }.FirstOrDefault(name =>
                input.Catalog.TryGet(name, out CatalogDef def) && input.Catalog.IsLegal(def, input.Equipment.Kit.Stage));

        // The 8-way neighbour direction nearest to the line from a slot to a
        // shooter. Vanilla cover protects a pawn from fire crossing an
        // adjacent cover object, so each such direction needs cover.
        internal static GridPoint Octant(GridPoint from, GridPoint to)
        {
            int dx = to.X - from.X, dz = to.Z - from.Z;
            // tan(22.5°) ≈ 0.4142: within that ratio a line counts as straight.
            int x = Math.Abs(dx) * 10000 >= Math.Abs(dz) * 4142 ? Math.Sign(dx) : 0;
            int z = Math.Abs(dz) * 10000 >= Math.Abs(dx) * 4142 ? Math.Sign(dz) : 0;
            return new GridPoint(x, z);
        }

        // Rows are measured from the gate: forward along the outward normal,
        // lateral along the flank. Slots stand in row 2, one cell from the
        // wall; the gate axis (lateral 0) stays an open, exposed crossing.
        internal static GridPoint Local(GridPoint gate, GridPoint outward, int flank, int forward, int lateral)
        {
            GridPoint tangent = new GridPoint(-outward.Z, outward.X);
            return new GridPoint(gate.X + outward.X * forward + tangent.X * flank * lateral,
                gate.Z + outward.Z * forward + tangent.Z * flank * lateral);
        }

        internal static List<GridPoint> RouteFor(GridPoint gate, GridPoint outward, int flank, int slots) =>
            new[] { gate, Local(gate, outward, flank, 1, 0), Local(gate, outward, flank, 2, 0) }
                .Concat(Enumerable.Range(1, slots).Select(l => Local(gate, outward, flank, 2, l))).ToList();

        // Cover required by a row of slots: every threat-facing neighbour
        // that is not itself a slot, a route cell, the gate-axis mouth or a wall.
        internal static HashSet<GridPoint> RequiredCover(GridPoint gate, GridPoint outward, int flank,
            IReadOnlyList<GridPoint> slots, Func<GridPoint, IEnumerable<GridPoint>> engaged, Func<GridPoint, bool> wall)
        {
            HashSet<GridPoint> mouth = new HashSet<GridPoint>(RouteFor(gate, outward, flank, slots.Count))
                { Local(gate, outward, flank, 3, 0) };
            HashSet<GridPoint> cover = new HashSet<GridPoint>();
            foreach (GridPoint slot in slots)
                foreach (GridPoint d in engaged(slot).Select(t => Octant(slot, t)).Distinct())
                {
                    GridPoint c = new GridPoint(slot.X + d.X, slot.Z + d.Z);
                    if (!mouth.Contains(c) && !wall(c)) cover.Add(c);
                }
            return cover;
        }

        // The largest nest of at most maxSlots slots on one flank of a gate.
        // A blocked cover cell drops the outermost slot; null when even the
        // first slot cannot be covered, reached or given a useful shot.
        //   engaged:   attacker samples a cell can engage (all fronts)
        //   useful:    whether a cell is a useful firing position
        //   open:      a route or slot cell is walkable and unclaimed
        //   coverable: a cover cell is buildable and unclaimed
        //   wall:      a cell is perimeter wall (opaque, already built)
        //   returnable: a wall return can be built on the cell
        public static P4BPost Build(string side, GridPoint gate, GridPoint outward, int flank, int maxSlots,
            Func<GridPoint, IEnumerable<GridPoint>> engaged, Func<GridPoint, bool> useful, Func<GridPoint, bool> open,
            Func<GridPoint, bool> coverable, Func<GridPoint, bool> wall, Func<GridPoint, bool> returnable)
        {
            List<GridPoint> route = RouteFor(gate, outward, flank, Math.Min(MaxSlots, maxSlots));
            for (int n = Math.Min(MaxSlots, maxSlots); n >= 1; n--)
            {
                List<GridPoint> slots = route.Skip(3).Take(n).ToList();
                if (route.Skip(1).Take(2 + n).Any(c => !open(c)) || slots.Any(c => !useful(c)) ||
                    Enumerable.Range(1, n).Any(l => !wall(Local(gate, outward, flank, 0, l)))) continue;
                HashSet<GridPoint> cover = RequiredCover(gate, outward, flank, slots, engaged, wall);
                if (cover.Count == 0 || cover.Any(c => !coverable(c))) continue;
                GridPoint end = Local(gate, outward, flank, 1, n + 1);
                GridPoint? wallReturn = !cover.Contains(end) && returnable(end) &&
                    wall(Local(gate, outward, flank, 0, n + 1)) ? end : (GridPoint?)null;
                return new P4BPost(side, outward, gate, route.Take(3 + n), slots, cover, wallReturn);
            }
            return null;
        }
    }
}
