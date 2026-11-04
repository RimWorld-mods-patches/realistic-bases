using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Shared by the live spawn gate and its offline route regression.
    public static class P4BDefenderArea
    {
        public static IReadOnlyList<GridPoint> Build(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            var fieldWall = layout.FieldWall;
            var approachDefense = layout.ApproachDefense;
            var postOpaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            return P4BPerimeterDepth.Envelope(input.Terrain, layout)
                .Concat(layout.Gates.SelectMany(g => g.BandDoors.Concat(g.OuterVestibule).Concat(new[] { g.Cell, g.Staging })))
                .Concat(layout.OuterSections.SelectMany(section => section.ServiceLane(layout.Gates[section.GateIndex])))
                .Concat(layout.JointSite?.Compound.Points() ?? Enumerable.Empty<GridPoint>())
                .Concat(layout.JointSite?.FieldReservation ?? Enumerable.Empty<GridPoint>())
                .Concat(fieldWall?.Access ?? new GridPoint[0])
                // Low cover is passable; vanilla paths can step over a
                // screen. This permits transit, not a protected post.
                .Concat(approachDefense?.CoverCells ?? new GridPoint[0])
                .Concat(approachDefense?.TransitCells ?? new GridPoint[0])
                .Concat(approachDefense?.Posts.SelectMany(p => p.Route) ?? new GridPoint[0])
                .Concat(approachDefense?.AllPosts ?? new GridPoint[0])
                .Concat(fieldWall?.ArrivalRoute.TakeWhile(c => !c.Equals(fieldWall.OuterGate)) ?? Enumerable.Empty<GridPoint>())
                .Concat(fieldWall?.ExteriorPosts ?? new GridPoint[0])
                .Concat(fieldWall == null ? Enumerable.Empty<GridPoint>() : new[] { fieldWall.OuterGate })
                .Concat((layout.Turret?.All ?? Enumerable.Empty<P4BTurretPlan>()).SelectMany(t => t.ServiceRoute))
                .Concat(P3DefenderRoute.TransitEnvelope(input.Terrain,
                    (fieldWall?.Access ?? new GridPoint[0])
                        .Concat(approachDefense?.TransitCells ?? new GridPoint[0])
                        .Concat(approachDefense?.Posts.SelectMany(p => p.Route) ?? new GridPoint[0])
                        .Concat((layout.Turret?.All ?? Enumerable.Empty<P4BTurretPlan>()).SelectMany(t => t.ServiceRoute)),
                    postOpaque.Except(P4BApproachDefensePlanner.AccessGates(layout))))
                .Distinct().ToList();
        }
    }
}
