using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Map-independent grow-room geometry. The 24-bar core pattern is an exact
    // packing of the vanilla lamp's full-growth disc; rotation and clipping
    // adapt it to room bounds and the entrance. Basins reserve floor space but
    // remain traversable according to their native passability.
    internal static class LayoutHydroponicsPacking
    {
        internal const int CoreMaximumBasins = 24;
        internal sealed class Bar
        {
            internal GridPoint[] Cells;
            internal GridPoint Facing;
        }
        private static readonly int[,] Core = {
            { -2,-5,1,0 },
            { -4,-4,1,0 },
            { 0,-4,0,1 },
            { 1,-4,1,0 },
            { -4,-3,1,0 },
            { 1,-3,1,0 },
            { -4,-2,1,0 },
            { 1,-2,1,0 },
            { -5,-1,0,1 },
            { -4,-1,1,0 },
            { 1,-1,1,0 },
            { 5,-1,0,1 },
            { -4,0,1,0 },
            { 1,0,1,0 },
            { -4,1,0,1 },
            { -3,1,0,1 },
            { -1,1,1,0 },
            { 3,1,0,1 },
            { 4,1,0,1 },
            { -2,2,0,1 },
            { -1,2,1,0 },
            { -1,3,1,0 },
            { -1,4,1,0 },
            { -1,5,1,0 },
        };

        internal static IReadOnlyList<Bar> Pack(int width, int height, GridPoint lamp, double radius,
            HashSet<GridPoint> reserved, int required)
        {
            bool Allowed(GridPoint c) => c.X>=1 && c.X<=width && c.Z>=1 && c.Z<=height && !reserved.Contains(c) &&
                (c.X-lamp.X)*(c.X-lamp.X)+(c.Z-lamp.Z)*(c.Z-lamp.Z)<=radius*radius;
            GridPoint Transform(GridPoint p,int turn,bool mirror)
            {
                int x=mirror?-p.X:p.X,z=p.Z;
                for(int i=0;i<turn;i++){int old=x;x=-z;z=old;}
                return new GridPoint(x,z);
            }
            var options=new List<List<Bar>>();
            foreach(bool mirror in new[]{false,true})for(int turn=0;turn<4;turn++)
            {
                var bars=new List<Bar>();
                for(int b=0;b<Core.GetLength(0);b++)
                {
                    var cells=Enumerable.Range(0,4).Select(i=>Transform(new GridPoint(Core[b,0]+i*Core[b,2],Core[b,1]+i*Core[b,3]),turn,mirror))
                        .Select(p=>new GridPoint(p.X+lamp.X,p.Z+lamp.Z)).ToArray();
                    if(cells.All(Allowed))bars.Add(new Bar{Cells=cells,Facing=Transform(new GridPoint(-Core[b,2],-Core[b,3]),turn,mirror)});
                }
                options.Add(bars);
            }
            // Small rooms can use free lit strips outside the clipped core pattern.
            foreach(bool horizontalFirst in new[]{false,true})
            {
                var occupied=new HashSet<GridPoint>();var bars=new List<Bar>();
                foreach(bool horizontal in new[]{horizontalFirst,!horizontalFirst})
                    for(int z=1;z<=height;z++)for(int x=1;x<=width;x++)
                    {
                        var cells=Enumerable.Range(0,4).Select(i=>new GridPoint(x+(horizontal?i:0),z+(horizontal?0:i))).ToArray();
                        if(!cells.All(c=>Allowed(c)&&!occupied.Contains(c)))continue;
                        occupied.UnionWith(cells);bars.Add(new Bar{Cells=cells,Facing=horizontal?new GridPoint(-1,0):new GridPoint(0,-1)});
                    }
                options.Add(bars);
            }
            var best=options.OrderByDescending(o=>o.Count).First();
            return best.Count<required?Array.Empty<Bar>():best.Take(required).ToArray();
        }
    }
}
