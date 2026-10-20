using System.Collections.Generic;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void CellCollections()
    {
        CellSet cells = new CellSet(4, 3);
        True(cells.Add(new GridPoint(2, 1)), "first cell insert succeeds");
        True(cells.Add(new GridPoint(0, 0)), "second cell insert succeeds");
        True(cells.Add(new GridPoint(-1, 0)), "out-of-bounds cell is retained");
        True(cells.Add(new GridPoint(4, 2)), "second overflow cell is retained");
        True(!cells.Add(new GridPoint(2, 1)), "duplicate insert is ignored");
        Equal(4, cells.Count, "cell set count includes overflow cells");
        List<GridPoint> copied = new List<GridPoint>();
        cells.CopyTo(copied);
        Equal(new GridPoint(0, 0), copied[0], "copy uses GridPoint order");
        Equal(new GridPoint(2, 1), copied[1], "copy uses GridPoint order");
        Equal(new GridPoint(-1, 0), copied[2], "overflow copy keeps insertion order");
        Equal(new GridPoint(4, 2), copied[3], "overflow copy keeps insertion order");

        CellSet more = new CellSet(4, 3);
        more.Add(new GridPoint(2, 1));
        more.Add(new GridPoint(3, 2));
        cells.UnionWith(more);
        Equal(5, cells.Count, "cell set union adds only new cells");
        cells.ExceptWith(more);
        True(!cells.Contains(new GridPoint(2, 1)) && !cells.Contains(new GridPoint(3, 2)),
            "cell set subtraction removes matching cells");
        CellSet copy = new CellSet(4, 3);
        copy.CopyFrom(cells);
        Equal(cells.Count, copy.Count, "copy preserves count");
        True(copy.Contains(new GridPoint(-1, 0)) && copy.Contains(new GridPoint(4, 2)),
            "copy preserves overflow membership");
        cells.Clear();
        Equal(0, cells.Count, "clear removes dense and overflow cells");

        CellMap<int> integers = new CellMap<int>(4, 3);
        True(!integers.TryGetValue(new GridPoint(2, 1), out int missing), "missing map value is absent");
        integers[new GridPoint(2, 1)] = 0;
        True(integers.ContainsKey(new GridPoint(2, 1)) &&
            integers.TryGetValue(new GridPoint(2, 1), out int zero) && zero == 0,
            "default integer value remains present");
        integers.Set(new GridPoint(-1, 0), 17);
        True(integers.TryGetValue(new GridPoint(-1, 0), out int overflow) && overflow == 17,
            "map overflow preserves dictionary semantics");
        True(integers.Remove(new GridPoint(-1, 0)) && !integers.ContainsKey(new GridPoint(-1, 0)),
            "map overflow removal works");

        CellSet blockers = new CellSet(4, 1);
        GridPoint origin = new GridPoint(0, 0), target = new GridPoint(3, 0), wall = new GridPoint(1, 0);
        blockers.Add(wall);
        True(!P4BFireLane.CanEngage(origin, target, 0, 4, blockers),
            "CellSet blocker rejects a blocked shot");
        blockers.Remove(wall);
        True(P4BFireLane.CanEngage(origin, target, 0, 4, blockers),
            "CellSet blocker accepts a clear shot");
        HashSet<GridPoint> legacy = new HashSet<GridPoint>();
        True(P4BFireLane.CanEngage(origin, target, 0, 4, legacy),
            "HashSet blocker behavior remains available");
    }
}
