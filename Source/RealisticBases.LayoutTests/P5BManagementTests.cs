using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// P5B better management (docs/implementation-plan.md#p5b--better-management).
internal static partial class Program
{
    private static void RunP5BTests()
    {
        Run("P5B Neolithic storage: wooden small shelves without ComplexFurniture", NeolithicSmallShelves, 30);
        Run("ordinary floor lamps keep their light at the top in rotated rooms", FloorLightsStayUpright);
        Run("ordinary room lighting covers grow rooms and furnished interiors", RoomLightCoverage);
        Run("P5B lights hang on room walls and take no floor", WallLightsTakeNoFloor, 70);
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

    private static void RoomLightCoverage()
    {
        var input = new ProceduralLayoutInput(LayoutRequirements.FromPopulation(8,Industrial,Hot,stage:MidStage),
            Plains(120),7,FoodSupplyTestCatalog());
        var context = LayoutDemandModel.FurnishContext(input.Equipment.Kit,input.Requirements,false,_=>0);
        foreach (var example in new[] { (LayoutRoomKind.Hydroponics,23,11), (LayoutRoomKind.Dining,8,11),
            (LayoutRoomKind.Laboratory,1,11), (LayoutRoomKind.Barracks,4,11), (LayoutRoomKind.Storage,24,11),
            (LayoutRoomKind.Storage,24,17) })
        {
            int size=example.Item3;
            var furnished=LayoutFurnishing.Furnish(example.Item1,size,size,2,example.Item2,context);
            True(furnished.Success,example.Item1+" still fits: "+furnished.Failure);
            var lights=furnished.Fixtures.Where(f=>f.Role==SettlementRole.Light).ToArray();
            var interior=new GridRect(1,1,size,size);
            var blockers=furnished.Fixtures.Where(f=>input.Catalog.TryGet(f.DefName,out var d)&&d.BlocksLight)
                .SelectMany(f=>f.Cells).ToArray();
            var covered=new HashSet<GridPoint>(lights.SelectMany(f=>LayoutRoomLighting.CoveredCells(interior,
                f.Cells[0],input.Catalog.Defs.Single(d=>d.DefName==f.DefName),blockers)));
            var dark=interior.Points().Except(blockers).Except(covered).ToArray();
            Equal(0,dark.Length,example.Item1+" has no dark cells with the sun lamp off; first="+string.Join(";",dark.Take(4)));
            if(size==11) True(lights.All(f=>f.DefName=="WallLamp"),example.Item1+" uses feasible wall lamps before floor space");
            else True(lights.Any(f=>f.DefName=="StandingLamp"),"an interior beyond wall-light reach gets floor lighting");
            for(int i=0;i<lights.Length;i++)
            {
                var without=new HashSet<GridPoint>(lights.Where((_,j)=>i!=j).SelectMany(f=>LayoutRoomLighting.CoveredCells(interior,
                    f.Cells[0],input.Catalog.Defs.Single(d=>d.DefName==f.DefName),blockers)));
                True(interior.Points().Except(blockers).Any(c=>!without.Contains(c)),"no lamp remains wholly redundant in "+example.Item1);
            }
        }
        var lamp=new CatalogDef("custom-light",CatalogCategory.Building,glowRadius:12,glowStrength:214);
        var split=new GridRect(1,1,11,11);
        var opaque=Enumerable.Range(1,11).Select(z=>new GridPoint(6,z)).ToArray();
        True(!LayoutRoomLighting.CoveredCells(split,new GridPoint(3,5),lamp,opaque).Any(c=>c.X>6),
            "an opaque partition blocks light even when geometric radii overlap");
        Equal(0.0,LayoutRoomLighting.GroundGlow(new CatalogDef("unknown",CatalogCategory.Building),100),
            "unknown custom lamps receive no guessed coverage");
    }

    private static void FloorLightsStayUpright()
    {
        foreach (GridPoint facing in new[] {new GridPoint(0,1),new GridPoint(0,-1),new GridPoint(1,0),new GridPoint(-1,0)})
        {
            foreach (string def in new[] { "StandingLamp", "SunLamp", "TorchLamp" })
            {
                var fixture=new LayoutFixture(def=="SunLamp"?LayoutFixtureKind.SunLamp:LayoutFixtureKind.Lamp,def,
                    new[]{new GridPoint(5,5)},null,facing,false,30,false,"rotated-room");
                Equal("North",fixture.Rotation,def+" keeps its light at the top for room facing "+facing);
            }
            var wall=new LayoutFixture(LayoutFixtureKind.Lamp,"WallLamp",new[]{new GridPoint(5,5)},null,
                facing,false,30,false,"rotated-room");
            Equal(facing.Z>0?"South":facing.Z<0?"North":facing.X>0?"West":"East",wall.Rotation,
                "wall lamps still point toward their supporting wall");
        }
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
