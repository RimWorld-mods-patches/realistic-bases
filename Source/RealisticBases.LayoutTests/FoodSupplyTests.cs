using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void MainFieldMinimumBoundary()
    {
        var field = new GridRect(80, 80, 12, 12);
        var compound = new GridRect(20, 20, 50, 50);
        var inner = new GridPoint(85, 80);
        var outer = new GridPoint(85, 91);
        var lane = new[] { new GridPoint(85, 81) };
        var posts = new[] { new GridPoint(84, 92), new GridPoint(86, 92) };
        var wall = field.Points().Where(c => c.X==80 || c.X==91 || c.Z==80 || c.Z==91).ToArray();
        foreach (int count in new[] { 0, 24, 25 })
        {
            var crops = field.Points().Where(c => c.X>81 && c.X<90 && c.Z>81 && c.Z<90).Take(count).ToArray();
            var option = new P4BFieldInterface(inner, outer, crops, lane, posts, posts, posts);
            var undersizedAlternative = new P4BFieldInterface(inner, outer, crops.Take(24), lane, posts, posts, posts);
            var site = new P4BJointSite(field, compound, new GridPoint(45,45), crops, lane, wall,
                inner, outer, posts, 0, new[] { option, undersizedAlternative }, partialFoodSupply:true);
            bool retained = count >= 25;
            Equal(retained ? count : 0, site.Crops.Count, "only retained main-field crops receive credit at " + count);
            Equal(retained ? wall.Length : 0, site.Wall.Count, "omitted fields have no walls at " + count);
            Equal(retained ? lane.Length : 0, site.Lane.Count, "omitted fields have no hauling lane at " + count);
            Equal(retained ? posts.Length : 0, site.AllPostClearance.Count(), "omitted fields have no defense reservations at " + count);
            Equal(retained ? 1 : 0, site.Interfaces.Count, "omitted fields have no gate interfaces at " + count);
        }
    }

    private static void HydroponicLampDensity()
    {
        var requirements = LayoutRequirements.FromPopulation(8, Industrial, new LayoutClimate(7,48), stage: MidStage);
        var input = new ProceduralLayoutInput(requirements, Plains(120), 7, FoodSupplyTestCatalog());
        var context = LayoutDemandModel.FurnishContext(input.Equipment.Kit, requirements, false, _=>0);
        var packed = LayoutFurnishing.Furnish(LayoutRoomKind.Hydroponics,11,11,2,24,context);
        True(packed.Success, "one lamp footprint fits 24 basins with an empty entrance and climate fixtures: " + packed.Failure);
        var basins = packed.Fixtures.Where(f=>f.Kind==LayoutFixtureKind.HydroponicsBasin).ToArray();
        var lamp = packed.Fixtures.Single(f=>f.Kind==LayoutFixtureKind.SunLamp);
        Equal(24,basins.Length,"maximum fully lit basin count");
        var crops=basins.SelectMany(f=>f.Cells).ToArray();
        Equal(96,crops.Length,"four planted cells per basin");
        Equal(96,crops.Distinct().Count(),"no overlapping basins");
        True(crops.All(c=>(c.X-lamp.Cells[0].X)*(c.X-lamp.Cells[0].X)+
            (c.Z-lamp.Cells[0].Z)*(c.Z-lamp.Cells[0].Z)<=5.8*5.8),"all crops receive full-growth light");
        True(!crops.Contains(new GridPoint(2,1)),"the door threshold remains clear in an unlit corner");
        True(!crops.Contains(lamp.Cells[0]),"the lamp is not replaced by crop cells");
        True(basins.All(f=>!f.Blocking),"native traversable basins remain traversable in room-local planning");
        True(basins.Any(f=>f.Cells.Select(c=>c.X).Distinct().Count()==4) &&
            basins.Any(f=>f.Cells.Select(c=>c.Z).Distinct().Count()==4),"packing uses both basin orientations");
        True(packed.Fixtures.Any(f=>f.Kind==LayoutFixtureKind.Heater) &&
            packed.Fixtures.Any(f=>f.Kind==LayoutFixtureKind.WallCooler),"heating and cooling still fit");
        Equal(4580,basins.Sum(f=>f.Watts)+lamp.Watts,"one lamp plus 24 basins has explicit peak power");
        True(!LayoutFurnishing.Furnish(LayoutRoomKind.Hydroponics,11,11,6,24,context).Success,
            "a blocked central entrance cannot be ignored just to claim the maximum");
    }

    private static void HydroponicFoodPackage()
    {
        True(TechStageModel.Create(SettlementTechStageLevel.IndustrialEarly).HasResearch("Hydroponics"),
            "Hydroponics follows its vanilla Electricity prerequisite in the early-industrial stage");
        True(!TechStageModel.Create(SettlementTechStageLevel.Medieval).HasResearch("Hydroponics"),
            "unpowered stages do not receive Hydroponics research");
        var catalog = FoodSupplyTestCatalog();
        var input = new ProceduralLayoutInput(LayoutRequirements.FromPopulation(8, Industrial, Hot, stage: MidStage), Plains(120), 7, catalog);
        True(LayoutFoodSupplyPlanner.CanHydroponics(input, out string why), "legal stage has a complete grow kit: " + why);
        var demand = LayoutFoodSupplyPlanner.GrowRoom(input, 8, 1);
        True(demand != null, "a greenhouse can fit eight basins plus its sun lamp and climate fixtures");
        GridRect size = demand.CandidateSizes[0];
        int doorX = (size.Width+1)/2;
        var furnished = LayoutFurnishing.Furnish(LayoutRoomKind.Hydroponics,size.Width,size.Height,doorX,8,
            LayoutDemandModel.FurnishContext(input.Equipment.Kit,input.Requirements,false,_=>0));
        True(furnished.Success, "the advertised grow-room envelope remains furnishable");
        var fixtures = furnished.Fixtures.Select(f => new LayoutFixture(f.Kind,f.DefName,f.Cells,f.Access,f.Facing,f.Blocking,f.Watts,
            f.NeedsFuel,demand.Id,LayoutFixtureKinds.ThroughWall(f.Kind)?new GridPoint(f.Cells[0].X-f.Facing.X,f.Cells[0].Z-f.Facing.Z):(GridPoint?)null,f.Role)).ToArray();
        var room = new LayoutRoom(demand,new GridRect(0,0,size.Width+2,size.Height+2),new GridPoint(doorX,0),new GridPoint(0,1),8,
            fixtures,false,Array.Empty<GridPoint>());
        Equal(8,fixtures.Count(f=>f.Kind==LayoutFixtureKind.HydroponicsBasin),"all eight physical basins are present");
        Equal(1,fixtures.Count(f=>f.Kind==LayoutFixtureKind.SunLamp),"the grow room has a physical sun lamp");
        var plan = LayoutFoodSupplyPlanner.Plan(input,100,new[]{room});
        True(plan != null && plan.HydroponicFieldEquivalent > 89 && plan.HydroponicFieldEquivalent < 90,
            "production credit is from the 32 planted cells at captured basin fertility");
        var dark = new LayoutRoom(demand,room.Bounds,room.Door,room.Facing,8,
            fixtures.Where(f=>f.Kind!=LayoutFixtureKind.SunLamp),false,Array.Empty<GridPoint>());
        True(LayoutFoodSupplyPlanner.Plan(input,100,new[]{dark})==null,"basins without a sun lamp cannot receive food credit");
        LayoutPowerModel.Loads(new[]{room},out int critical,out int nonCritical);
        True(critical >= 3460 && nonCritical == 0,"the sun lamp, basins and climate load require backed critical power");
        var materials=input.Equipment.Count(new[]{room},Array.Empty<GridPoint>(),1,null,foodSupply:plan);
        True(materials.FoodImportValue==plan.ImportValue && materials.ImportValue>plan.ImportValue+1000,
            "grow equipment construction and imported food both consume the existing budget");
    }

    private static TechCatalog FoodSupplyTestCatalog()
    {
        var basin = new CatalogDef("HydroponicsBasin", CatalogCategory.Building, TechTier.Industrial,
            new[] { "Hydroponics" }, 1, 4, costList: new[] { new KeyValuePair<string,int>("Steel",100),
                new KeyValuePair<string,int>("ComponentIndustrial",1) }, basePowerConsumption: 70,
            passability: CatalogPassability.PassThroughOnly, growFertility: 2.8);
        var lamp = new CatalogDef("SunLamp", CatalogCategory.Building, TechTier.Industrial,
            costList: new[] { new KeyValuePair<string,int>("Steel",40) }, basePowerConsumption: 2900,
            growLightRadius: 5.8);
        var meal = new CatalogDef("MealSimple", CatalogCategory.Item, nutrition: 0.9, marketValue: 10, stackLimit: 10);
        var replacements = new[] { basin, lamp, meal };
        return new TechCatalog(CoreCatalog.Source, CoreCatalog.Defs.Where(d => !replacements.Any(r => r.DefName==d.DefName)).Concat(replacements),
            CoreCatalog.Research.Where(r=>r.DefName!="Hydroponics").Concat(new[] { new CatalogResearch("Hydroponics",TechTier.Industrial,new[]{"Electricity"}) }));
    }

    private static void ScarceCroplandFoodLayout()
    {
        var terrain=Build(130,130,(x,z)=> x>=75 && x<=82 && z>=72 && z<=79 ? Ground : Ground & ~TerrainTraits.Fertile);
        var requirements=LayoutRequirements.FromPopulation(4,Industrial,Hot,stage:MidStage,importValueLimit:9000);
        var input=new ProceduralLayoutInput(requirements,terrain,9,FoodSupplyTestCatalog());
        string before=terrain.Digest;
        var result=ProceduralLayoutPlanner.Plan(input,requireJointSite:true);
        Accepted(result);
        var layout=result.Layout;
        True(layout.FoodSupply!=null && layout.FoodSupply.Covered,"a partial field has an explicit complete food contract");
        True(layout.FieldWall.Fields.Count>=P4BJointSite.MinimumMainFieldCells && layout.FieldWall.Fields.Count<requirements.FieldCells,
            "use the available natural field without pretending it supplies the full crop demand");
        True(layout.Rooms.Any(r=>r.Kind==LayoutRoomKind.Hydroponics),"a funded feasible grow room is preferred to imports alone");
        Equal(layout.FoodSupply.ImportValue,layout.Materials.FoodImportValue,"food purchases are included once in materials");
        True(layout.Materials.OrdinaryImportValue<=requirements.BaseImportValueLimit,"imports and cultivation retain the existing allowance");
        True(layout.Power.FuelDays>=layout.FoodSupply.Days,"cultivation power is funded through the first harvest");
        True(RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input,layout).Accepted,"the whole layout and loadout pass native-spawner preflight");
        Equal(before,terrain.Digest,"farming adaptation does not change the captured terrain");
    }

    private static void OmittedMainFieldFoodLayout()
    {
        // The entire map has only 24 fertile cells; lanes can only reduce this.
        var terrain=Build(130,130,(x,z)=> x>=75 && x<=80 && z>=72 && z<=75 ? Ground : Ground & ~TerrainTraits.Fertile);
        var requirements=LayoutRequirements.FromPopulation(4,Industrial,Hot,stage:MidStage,importValueLimit:9000);
        var input=new ProceduralLayoutInput(requirements,terrain,9,FoodSupplyTestCatalog());
        string before=terrain.Digest;
        var result=ProceduralLayoutPlanner.Plan(input,requireJointSite:true);
        Accepted(result);
        var layout=result.Layout;
        True(layout.HasOmittedMainField, "a sub-25-cell field is explicitly omitted");
        True(layout.FieldWall==null, "no field wall, gates, access ports or outside field posts are built");
        True(!layout.JointSite.FieldReservation.Any() && !layout.JointSite.AllPostClearance.Any() &&
            layout.JointSite.Interfaces.Count==0, "the omitted field leaves no physical or defense reservation");
        Equal(0,layout.FoodSupply.NaturalCells,"unbuilt crops receive no production credit");
        var defenderArea = new HashSet<GridPoint>(P4BDefenderArea.Build(input,layout));
        True(!layout.JointSite.Field.Points().All(defenderArea.Contains),
            "the old field rectangle is not silently retained as a defended area");
        True(P4BFieldWallPlanner.PlanJoint(input,layout,Array.Empty<GridPoint>(),out string fieldReason)==null &&
            fieldReason=="main_field_omitted","field-wall planning cannot resurrect an omitted field");
        True(layout.FoodSupply.Covered,"hydroponics and finite imports cover the full original nutrition requirement");
        True(layout.Rooms.Any(r=>r.Kind==LayoutRoomKind.Hydroponics),"funded feasible cultivation is preferred to imports alone");
        True(layout.ApproachDefense!=null && layout.Gates.Count>0,"ordinary compound defense and arrivals remain planned");
        True(layout.Materials.OrdinaryImportValue<=requirements.BaseImportValueLimit,"the original materials allowance is retained");
        True(RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input,layout).Accepted,
            "the fieldless ordinary settlement passes full layout, loadout and spawn preflight");
        Equal(before,terrain.Digest,"field omission does not mutate captured terrain");
    }

    private static void FoodSupplyContract()
    {
        var food = new CatalogDef("MealSimple", CatalogCategory.Item, nutrition: 0.9, marketValue: 10, stackLimit: 10);
        var pemmican = new CatalogDef("Pemmican", CatalogCategory.Item, nutrition: 0.05, marketValue: 2.1, stackLimit: 75);
        var catalog = new TechCatalog(CoreCatalog.Source, CoreCatalog.Defs.Where(d => d.DefName != food.DefName && d.DefName != pemmican.DefName)
            .Concat(new[] { food, pemmican }), CoreCatalog.Research);
        var requirements = new LayoutRequirements(8, Industrial, HousingStandard.Barracks, Hot, 0, 620, 247, stage: MidStage);
        var input = new ProceduralLayoutInput(requirements, Plains(100), 7, catalog);
        var plan = LayoutFoodSupplyPlanner.Plan(input, 100, Array.Empty<LayoutRoom>());
        True(plan != null && plan.Covered, "natural field and finite imports retain the full nutrition demand");
        Equal(247, input.Requirements.FieldCells, "the original full-production requirement is not rewritten");
        Equal(100, plan.NaturalCells, "only actual natural crop cells receive production credit");
        Equal(8, plan.Days, "imports use the first-harvest horizon");
        Equal(143, plan.TotalFoodUnits, "one physical stock covers eight days with the safety factor");
        Equal(85, plan.ImportedFoodUnits, "the field shortfall has explicit finite import coverage");
        Equal(850, plan.ImportValue, "the purchase ledger funds the deficit at the captured food value");
        Equal(15, plan.StorageCells, "storage covers the whole stock, not just the supplement");
        True(plan.TotalFoodUnits < plan.BaselineFoodUnits + plan.ImportedFoodUnits,
            "startup food and deficit coverage share one stock rather than duplicate grants");
        var noField = LayoutFoodSupplyPlanner.Plan(input, 0, Array.Empty<LayoutRoom>());
        Equal(noField.TotalFoodUnits, noField.ImportedFoodUnits, "a wholly imported food supply is fully funded");
        True(!LayoutFoodSupplyPlanner.CanHydroponics(input, out _), "missing basin or sun lamp cannot receive production credit");
    }
}
