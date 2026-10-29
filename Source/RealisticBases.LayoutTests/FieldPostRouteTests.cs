using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void FieldPostGatewaySearch()
    {
        // Small route-provider counterexample for the Caldera failure: eight
        // nearby gateways offer unsafe shortcuts; a farther one stays inside
        // the existing corridor. All three live spawn fallbacks run here.
        var corner = new GridPoint(20, 4);
        var gate = new GridPoint(20, 2);
        var post = new GridPoint(20, 0);
        var center = new GridPoint(0, 0);
        var area = Enumerable.Range(0, 5).Select(z => new GridPoint(0, z))
            .Concat(Enumerable.Range(0, 21).Select(x => new GridPoint(x, 4)))
            .Concat(Enumerable.Range(1, 9).Select(x => new GridPoint(x, 0)))
            .Concat(Enumerable.Range(0, 5).Select(z => new GridPoint(20, z))).Distinct().ToArray();
        var entries = Enumerable.Range(1, 9).Select(x => new GridPoint(x, 0)).Concat(new[] { corner }).ToArray();
        IReadOnlyList<GridPoint> Route(GridPoint from, GridPoint to)
        {
            var path = new List<GridPoint> { from };
            void WalkX() { while (from.X != to.X) { from = new GridPoint(from.X + Math.Sign(to.X - from.X), from.Z); path.Add(from); } }
            void WalkZ() { while (from.Z != to.Z) { from = new GridPoint(from.X, from.Z + Math.Sign(to.Z - from.Z)); path.Add(from); } }
            if (to.Equals(corner)) { WalkZ(); WalkX(); }
            else { WalkX(); WalkZ(); }
            return path;
        }
        foreach (var home in Enumerable.Range(0, 4).Select(z => new GridPoint(0, z)))
        {
            var restricted = entries.OrderBy(g => Math.Abs(g.X - home.X) + Math.Abs(g.Z - home.Z)).Take(8).ToArray();
            True(!P4BFieldPostPath.Evaluate(home, post, center, gate, restricted, Route, area, area,
                new GridPoint[0]).Allowed, "nearby-only search rejects the post");
            var legal = P4BFieldPostPath.Evaluate(home, post, center, gate, entries, Route, area, area, new GridPoint[0]);
            True(legal.Allowed && legal.ViaGate && legal.Cells <= P3DefenderRoute.MaximumCells,
                "farther declared gateway makes the full route legal");
            var severed = area.Where(c => !c.Equals(new GridPoint(10, 4))).ToArray();
            True(!P4BFieldPostPath.Evaluate(home, post, center, gate, entries, Route, severed, area,
                new GridPoint[0]).Allowed, "a broken corridor still rejects instead of granting movement authority");
        }
    }
}
