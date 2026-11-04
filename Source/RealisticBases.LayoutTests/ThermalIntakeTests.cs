using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void ThermalAirflowFacts()
    {
        var unknown=new CatalogDef("Machine",CatalogCategory.Building,passability:CatalogPassability.Impassable);
        var permeable=WithRoomFact(unknown,false);var solid=WithRoomFact(unknown,true);
        string Digest(CatalogDef def)=>new TechCatalog("airflow",new[]{def},null).Digest;
        Equal(Digest(unknown),Digest(WithRoomFact(unknown,null)),"absent room facts preserve historical catalog hashes");
        True(Digest(unknown)!=Digest(permeable) && Digest(permeable)!=Digest(solid),"explicit native room facts participate in the hash");
        True(LayoutFixtureKinds.AllowsIndoorFurniture(LayoutFixtureKind.WallVent,permeable),"vents can face impassable furniture that retains a room region");
        True(!LayoutFixtureKinds.AllowsIndoorFurniture(LayoutFixtureKind.WallCooler,permeable),"coolers still reject native impassable air cells");
        True(!LayoutFixtureKinds.AllowsIndoorFurniture(LayoutFixtureKind.WallVent,solid) &&
            !LayoutFixtureKinds.AllowsIndoorFurniture(LayoutFixtureKind.WallVent,unknown),"full-fill and unknown impassable furniture are conservative");
    }

    private static LayoutKit ThermalTestKit(TechCatalog catalog) => new ProceduralLayoutInput(
        LayoutRequirements.FromPopulation(4,Industrial,Hot,stage:MidStage),Plains(100),1,catalog).Equipment.Kit;

    private static CatalogDef WithRoomFact(CatalogDef def, bool? blocksRoom)
    {
        var ctor=typeof(CatalogDef).GetConstructors().Single();
        return (CatalogDef)ctor.Invoke(ctor.GetParameters().Select(p=>p.Name=="blocksRoom" ? (object)blocksRoom :
            typeof(CatalogDef).GetProperty(p.Name=="research" ? "ResearchPrerequisites" :
                char.ToUpperInvariant(p.Name[0])+p.Name.Substring(1)).GetValue(def)).ToArray());
    }

    private static void CoolerIntakeCanContainBed()
    {
        var kit=ThermalTestKit(CoreCatalog);
        var plain=LayoutFurnishing.Furnish(LayoutRoomKind.Barracks,5,5,3,2,
            new LayoutFurnishContext(kit,false,false,_=>0));
        True(plain.Success,"baseline beds fit");
        var bed=plain.Fixtures.First(f=>f.Kind==LayoutFixtureKind.Bed);
        var cold=bed.Cells.First(c=>c.Z==5);
        var wall=new GridPoint(cold.X,6);
        True(kit.Catalog.TryGet(bed.DefName,out var def) && def.Passability!=CatalogPassability.Impassable,
            "the native bed can be walked through");
        var cooled=LayoutFurnishing.Furnish(LayoutRoomKind.Barracks,5,5,3,2,
            new LayoutFurnishContext(kit,false,true,c=>c.Equals(wall)?0:-1));
        True(cooled.Success,"a cooler intake over a passable bed needs no empty floor: "+cooled.Failure);
        var cooler=cooled.Fixtures.Single(f=>f.Kind==LayoutFixtureKind.WallCooler);
        Equal(0,cooler.Access.Count,"automatic temperature control has no pawn interaction tile");
        True(cooled.Fixtures.Any(f=>f.Kind==LayoutFixtureKind.Bed && f.Cells.Contains(cold)),"the bed is retained on the intake cell");
    }

    private static void FreezerIntakeCanContainShelf()
    {
        var kit=ThermalTestKit(CoreCatalog);
        var wall=new GridPoint(1,6);var cold=new GridPoint(1,5);
        var room=LayoutFurnishing.Furnish(LayoutRoomKind.Freezer,5,5,3,1,
            new LayoutFurnishContext(kit,false,false,c=>c.Equals(wall)?0:-1));
        True(room.Success,"freezer furnishing fits: "+room.Failure);
        True(room.Fixtures.Any(f=>f.Kind==LayoutFixtureKind.Shelf && f.Cells.Contains(cold)),
            "a shelf can use the cold-side cell after the wall cooler is placed");
        Equal(0,room.Fixtures.Single(f=>f.Kind==LayoutFixtureKind.WallCooler).Access.Count,"cold-side floor is not reserved for a job");
    }

    private static GridPoint WallBeside(GridPoint cold, int w, int h) => cold.Z==h ? new GridPoint(cold.X,h+1) :
        cold.X==1 ? new GridPoint(0,cold.Z) : cold.X==w ? new GridPoint(w+1,cold.Z) : new GridPoint(cold.X,0);

    private static void VentIntakeCanContainFurniture()
    {
        const int w=9,h=7,door=5;
        var original=ThermalTestKit(CoreCatalog);
        var generator=WithRoomFact(original.Generator,false);
        var catalog=new TechCatalog(CoreCatalog.Source,CoreCatalog.Defs.Where(d=>d.DefName!=generator.DefName).Concat(new[]{generator}),CoreCatalog.Research);
        var kit=ThermalTestKit(catalog);
        var plain=LayoutFurnishing.Furnish(LayoutRoomKind.Power,w,h,door,2,new LayoutFurnishContext(kit,false,false,_=>0,2));
        True(plain.Success,"baseline power furnishing fits");
        var cold=plain.Fixtures.First(f=>f.Kind==LayoutFixtureKind.Generator).Cells.First(c=>c.Z==h);
        var wall=WallBeside(cold,w,h);
        var room=LayoutFurnishing.Furnish(LayoutRoomKind.Power,w,h,door,2,
            new LayoutFurnishContext(kit,false,false,c=>c.Equals(wall)?0:-1,2));
        True(room.Success,"a vent can exchange room air through permeable generator furniture: "+room.Failure);
        Equal(0,room.Fixtures.Single(f=>f.Kind==LayoutFixtureKind.WallVent).Access.Count,"vent has no empty interaction tile");
        var solid=WithRoomFact(original.Generator,true);
        var blockedCatalog=new TechCatalog(CoreCatalog.Source,CoreCatalog.Defs.Where(d=>d.DefName!=solid.DefName).Concat(new[]{solid}),CoreCatalog.Research);
        var blocked=LayoutFurnishing.Furnish(LayoutRoomKind.Power,w,h,door,2,
            new LayoutFurnishContext(ThermalTestKit(blockedCatalog),false,false,c=>c.Equals(wall)?0:-1,2));
        True(!blocked.Success,"full-fill room-blocking furniture cannot replace a vent's served room");
    }
}
