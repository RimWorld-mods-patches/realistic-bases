using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// P5B better management (docs/implementation-plan.md#p5b--better-management).
internal static partial class Program
{
    private static void RunP5BTests()
    {
        Run("P5B Neolithic storage: wooden small shelves without ComplexFurniture", NeolithicSmallShelves, 20);
        Run("P5B lights hang on room walls and take no floor", WallLightsTakeNoFloor, 60);
    }

    private static void NeolithicSmallShelves()
    {
        SettlementTechStage neolithic = TechStageModel.Create(SettlementTechStageLevel.Neolithic);
        True(!neolithic.HasResearch("ComplexFurniture"), "Neolithic lacks ComplexFurniture");
        True(CoreCatalog.IsLegal("ShelfSmall", neolithic), "the small shelf is the Neolithic exception");
        True(!CoreCatalog.IsLegal("Shelf", neolithic), "the double shelf keeps the research rule");

        LayoutRequirements requirements = V2Funded(P2ProfileCatalog.TribalTemperate, SettlementTechStageLevel.Neolithic, Temperate, 100);
        ProceduralLayoutInput input = Input(Plains(180), requirements, 3, new LayoutFortificationReservation(1));
        Equal("ShelfSmall", input.Equipment.Kit.Roles.ChosenDefName(SettlementRole.Storage), "Neolithic storage role");
        CatalogDef small;
        True(CoreCatalog.TryGet("ShelfSmall", out small), "catalog has ShelfSmall");
        Equal(LayoutEquipment.Wood, input.Equipment.StuffFor(small), "the exception shelf is wooden even with stone walls");

        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Accepted(result);
        List<LayoutFixture> shelves = result.Layout.Rooms.SelectMany(r => r.Fixtures)
            .Where(f => f.Kind == LayoutFixtureKind.Shelf && f.Role == SettlementRole.Storage).ToList();
        True(shelves.Count > 0, "warehouses get small shelves");
        True(shelves.All(f => f.DefName == "ShelfSmall" && f.Cells.Count == 1), "every storage shelf is a one-cell ShelfSmall");
        Console.WriteLine("  neolithic shelves=" + shelves.Count + " stuff=" + input.Equipment.ConstructionStuff);

        SettlementTechStage medieval = TechStageModel.Create(SettlementTechStageLevel.Medieval);
        LayoutRequirements medievalRequirements = V2Funded(P2ProfileCatalog.TribalTemperate, SettlementTechStageLevel.Medieval, Temperate, 100);
        True(medieval.HasResearch("ComplexFurniture"), "Medieval researches ComplexFurniture");
        Equal("Shelf", Input(Plains(180), medievalRequirements, 3).Equipment.Kit.Roles.ChosenDefName(SettlementRole.Storage),
            "with the research the double shelf stays first");
    }

    private static void WallLightsTakeNoFloor()
    {
        foreach (var stage in new[] { SettlementTechStageLevel.IndustrialMid, SettlementTechStageLevel.Neolithic })
        {
            PhysicalSettlementProfile profile = stage == SettlementTechStageLevel.Neolithic ? P2ProfileCatalog.TribalTemperate : P2ProfileCatalog.IndustrialTemperate;
            ProceduralLayoutInput input = Input(Plains(180), V2Funded(profile, stage, Temperate, 100), 5, new LayoutFortificationReservation(1));
            string wall = stage == SettlementTechStageLevel.Neolithic ? "TorchWallLamp" : "WallLamp";
            Equal(wall, input.Equipment.Kit.Roles.ChosenDefName(SettlementRole.WallLight), stage + " wall light");
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            Accepted(result);
            int onWall = 0, onFloor = 0;
            foreach (LayoutRoom room in result.Layout.Rooms.Where(r => r.Kind != LayoutRoomKind.MortarMagazine))
            {
                List<LayoutFixture> lights = room.Fixtures.Where(f => f.Role == SettlementRole.Light).ToList();
                True(lights.Count > 0, room.Id + " is lit");
                foreach (LayoutFixture light in lights.Where(f => f.DefName == wall))
                {
                    GridPoint support = new GridPoint(light.Cells[0].X - light.Facing.X, light.Cells[0].Z - light.Facing.Z);
                    True(room.IsEdge(support) && !support.Equals(room.Door), room.Id + " light hangs on its own wall");
                    onWall++;
                }
                onFloor += lights.Count(f => f.DefName != wall);
            }
            Console.WriteLine("  " + stage + " wallLights=" + onWall + " floorLights=" + onFloor);
            True(onWall > 0 && onFloor * 4 <= onWall, stage + ": wall lights replace most floor lights");
        }
    }
}
