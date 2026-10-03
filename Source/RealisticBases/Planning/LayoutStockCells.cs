using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Pool every split room, prefer its shelves, and never use circulation or
    // interaction cells as overflow storage. Shelf multiplicity comes from the
    // live building def; the pure layout conservatively reserves one stack/cell.
    public static class LayoutStockCells
    {
        public static IReadOnlyList<GridPoint> ForArtillerySupply(string defName,
            IEnumerable<GridPoint> magazine, IEnumerable<GridPoint> warehouse) =>
            magazine.Concat(defName == "ReinforcedBarrel" ? warehouse : Enumerable.Empty<GridPoint>())
                .Distinct().ToList().AsReadOnly();

        public static IReadOnlyList<GridPoint> ForRooms(IEnumerable<LayoutRoom> rooms, params LayoutRoomKind[] kinds)
        {
            LayoutRoom[] selected = rooms.Where(r => kinds.Contains(r.Kind)).ToArray();
            IEnumerable<GridPoint> shelves = selected.SelectMany(r => r.Fixtures)
                .Where(f => f.Kind == LayoutFixtureKind.Shelf).SelectMany(f => f.Cells);
            IEnumerable<GridPoint> ground = selected.SelectMany(r =>
                LayoutDemandModel.IsStockpile(r.Kind)
                    ? r.Fixtures.Where(f => f.Kind == LayoutFixtureKind.Stockpile).SelectMany(f => f.Cells)
                    : r.Interior.Points().Except(r.Fixtures.SelectMany(f => f.Cells.Concat(f.Access)))
                        .Except(new[] { new GridPoint(r.Door.X + r.Facing.X, r.Door.Z + r.Facing.Z) }));
            return shelves.Concat(ground).Distinct().ToList().AsReadOnly();
        }
    }
}
