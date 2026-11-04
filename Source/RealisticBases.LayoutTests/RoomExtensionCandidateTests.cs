using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void RoomExtensionCandidateBudget()
    {
        var requirements=LayoutRequirements.FromPopulation(4,Industrial,Temperate,stage:MidStage);
        var input=new ProceduralLayoutInput(requirements,Plains(60),1234,CoreCatalog,
            budget:new LayoutSearchBudget(maxCandidatesPerRoom:8));
        string originalInput=input.Digest, originalTerrain=input.Terrain.Digest;
        foreach(string edge in new[]{"free","shared","pocket","sealed"})
        {
            var result=LayoutAttempt.ProbeExtensionCandidateBudget(input,edge,false);
            True(result.Placed,"a reachable seven-cell footprint survives narrow gaps with "+edge+" edges");
            True(result.Evaluations<=8,"impossible footprints do not spend furnishing slots");
            True(result.Bounds.HasValue && result.Bounds.Value.MinX==29 && result.Bounds.Value.MaxX==35,
                "the room uses the proven chamber without crossing the approach");
        }
        var blocked=LayoutAttempt.ProbeExtensionCandidateBudget(input,"pocket",true);
        True(!blocked.Placed,"an absorbable wall cannot become room interior");
        Equal(originalInput,input.Digest,"room-extension search preserves its input");
        Equal(originalTerrain,input.Terrain.Digest,"room-extension search preserves terrain");
    }
}

namespace RealisticBases.Planning
{
    internal sealed partial class LayoutAttempt
    {
        internal static (bool Placed,int Evaluations,GridRect? Bounds) ProbeExtensionCandidateBudget(
            ProceduralLayoutInput input,string edge,bool blockedInterior)
        {
            var grid=LayoutTerrainGrid.Build(input.Terrain,0);
            var stats=new LayoutSearchStats();
            var run=new LayoutAttempt(input,input.Equipment.Demand,grid,new GridPoint(10,20),
                LayoutDoctrine.OpenGround,false,input.Seed,stats,new List<LayoutDiagnostic>(),new ProceduralLayoutStageTimings());
            for(int i=0;i<run.occ.Length;i++)run.occ[i]=CourtCell;
            // One-cell public approach, then exactly one legal room chamber.
            for(int x=10;x<29;x++)run.occ[grid.Index(new GridPoint(x,20))]=Free;
            foreach(GridPoint c in new GridRect(29,17,7,7).Points())run.occ[grid.Index(c)]=Free;
            var root=new GridPoint(10,20);
            run.occ[grid.Index(root)]=Street;
            var main=new Segment{Id="main",Main=true,Axis=new GridPoint(1,0),Root=root};
            main.Sections.Add(new List<GridPoint>{root});run.segments.Add(main);
            for(int z=17;z<=23;z++)
            {
                var c=new GridPoint(35,z);
                if(edge=="shared") {run.occ[grid.Index(c)]=RoomCell;run.roomWalls.Add(c);run.wallOwner[c]="neighbour";}
                else if(edge=="pocket")run.occ[grid.Index(c)]=PocketWall;
                else if(edge=="sealed")run.occ[grid.Index(c)]=PocketSeal;
            }
            if(blockedInterior)run.occ[grid.Index(new GridPoint(32,20))]=PocketWall;
            var demand=new LayoutRoomDemand("extension-probe",LayoutRoomKind.Storage,1,"stockpile-cells",
                new[]{new GridRect(0,0,5,5)});
            bool placed=run.PlaceByConnectedExtension(demand,false);
            return (placed,stats.EvaluationCount,run.rooms.FirstOrDefault()?.Bounds);
        }
    }
}
