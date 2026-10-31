using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void HousingSpawnFloor()
    {
        var climate = new LayoutClimate(0, 40);
        var requirements = LayoutRequirements.FromPopulation(8, Industrial, climate,
            stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate));
        var input = Input(Plains(140), requirements, 77);
        var context = new LayoutFurnishContext(input.Equipment.Kit, true, true, null);
        var tight = LayoutFurnishing.Furnish(LayoutRoomKind.Barracks, 6, 4, 3, 8, context);
        True(!tight.Success, "eight beds alone do not make a room with fewer than eight empty floor cells legal");
        Equal("housing_spawn_floor", tight.Failure, "the housing floor shortfall is explicit");

        var roomy = LayoutFurnishing.Furnish(LayoutRoomKind.Barracks, 6, 5, 3, 8, context);
        True(roomy.Success, "larger housing can retain all residents and equipment");
        Equal(8, roomy.Capacity, "all eight beds remain required");
        var occupied = new HashSet<GridPoint>(roomy.Fixtures.SelectMany(f => f.Cells));
        True(new GridRect(1, 1, 6, 5).Points().Count(c => !occupied.Contains(c)) >= 8,
            "floor remains empty after beds, heating, cooling and lighting");

        foreach (var room in input.Equipment.Demand.Rooms.Where(r => r.Kind == LayoutRoomKind.Barracks || r.Kind == LayoutRoomKind.Bedroom))
            foreach (var size in room.CandidateSizes)
            {
                var furnished = LayoutFurnishing.Furnish(room.Kind, size.Width, size.Height, (size.Width + 1) / 2,
                    room.Capacity, context);
                True(furnished.Success, "demand sizes include the housing floor requirement");
                var fixtures = new HashSet<GridPoint>(furnished.Fixtures.SelectMany(f => f.Cells));
                True(new GridRect(1, 1, size.Width, size.Height).Points().Count(c => !fixtures.Contains(c)) >= room.Capacity,
                    "every offered housing size leaves its residents distinct initial floor cells: " + size +
                    " capacity=" + room.Capacity + " available=" +
                    new GridRect(1, 1, size.Width, size.Height).Points().Count(c => !fixtures.Contains(c)));
            }
    }
}
