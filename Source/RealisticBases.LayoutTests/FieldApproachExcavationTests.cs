using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void FieldApproachExcavation()
    {
        TerrainTraits rock = (Ground & ~TerrainTraits.Standable) | TerrainTraits.NaturalRock |
            TerrainTraits.Occupied | TerrainTraits.Roofed | TerrainTraits.ThickRoof;
        TerrainSnapshot Barrier(TerrainTraits obstacle, bool gap = false) => Build(90, 90, (x, z) =>
            x >= 44 && x <= 48 && (!gap || z >= 35 && z <= 45) ? obstacle : Ground);
        (bool Connected, GridPoint[] Excavation, GridPoint[] Route, int Nodes) Probe(TerrainSnapshot terrain,
            int allowance, LayoutDoctrine doctrine = LayoutDoctrine.RockProtected, int routeNodes = 40000)
        {
            var requirements = new LayoutRequirements(4, Industrial, HousingStandard.Barracks, Temperate,
                0, allowance, 100, stage: MidStage);
            var input = new ProceduralLayoutInput(requirements, terrain, 1234, CoreCatalog,
                budget: new LayoutSearchBudget(maxRouteNodes: routeNodes));
            return LayoutAttempt.ProbeFieldApproachExcavation(input, doctrine);
        }

        var terrain = Barrier(rock);
        string before = terrain.Digest;
        var tunnel = Probe(terrain, 5);
        True(tunnel.Connected, "a mountain street can connect to the field through five funded rock cells");
        Equal(5, tunnel.Excavation.Length, "each newly excavated rock cell is charged exactly once");
        True(tunnel.Excavation.All(c => terrain[c].Has(TerrainTraits.NaturalRock)), "only natural rock is excavated");
        True(tunnel.Excavation.All(c => tunnel.Route.Contains(c)), "excavation belongs to the actual connecting street");
        True(tunnel.Route.Zip(tunnel.Route.Skip(1), (a, b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z))
            .All(n => n == 1), "the connecting path is cardinally continuous");
        Equal(before, terrain.Digest, "planning leaves the terrain snapshot unchanged");

        var tooSmall = Probe(terrain, 4);
        True(!tooSmall.Connected && tooSmall.Excavation.Length == 0, "an unfunded tunnel rejects without claiming partial excavation");
        True(!Probe(terrain, 50, LayoutDoctrine.OpenGround).Connected, "open-ground doctrine still cannot excavate thick roof rock");
        var ordinaryRock = Barrier(rock & ~TerrainTraits.ThickRoof);
        string ordinaryBefore = ordinaryRock.Digest;
        var ordinaryTunnel = Probe(ordinaryRock, 5, LayoutDoctrine.OpenGround);
        True(ordinaryTunnel.Connected, "an ordinary street can connect through rock it is already allowed to excavate");
        Equal(5, ordinaryTunnel.Excavation.Length, "the ordinary field connector charges every new rock cell");
        True(ordinaryTunnel.Excavation.All(c => ordinaryRock[c].Has(TerrainTraits.NaturalRock) &&
            !ordinaryRock[c].Has(TerrainTraits.ThickRoof)), "ordinary excavation keeps its roof restrictions");
        var ordinaryTooSmall = Probe(ordinaryRock, 4, LayoutDoctrine.OpenGround);
        True(!ordinaryTooSmall.Connected && ordinaryTooSmall.Excavation.Length == 0,
            "ordinary field excavation rejects without partial claims when the shared allowance is insufficient");
        Equal(ordinaryBefore, ordinaryRock.Digest, "ordinary field routing leaves terrain unchanged");
        var dryRoute = Probe(Barrier(rock, gap: true), 5);
        True(dryRoute.Connected && dryRoute.Excavation.Length == 0, "an existing dry route is preferred to a tunnel");
        var ordinaryDryRoute = Probe(Barrier(rock, gap: true), 5, LayoutDoctrine.OpenGround);
        True(ordinaryDryRoute.Connected && ordinaryDryRoute.Excavation.Length == 0,
            "ordinary connectors also prefer an existing dry route");
        True(!Probe(Barrier(DeepWater), 50).Connected, "excavation does not authorize crossing water");
        True(!Probe(Barrier(DeepWater), 50, LayoutDoctrine.OpenGround).Connected,
            "ordinary field excavation does not authorize crossing water");
        var bounded = Probe(terrain, 50, routeNodes: 3);
        True(!bounded.Connected && bounded.Nodes <= 3, "fallback shares the original per-interface route-node cap");
        var ordinaryBounded = Probe(ordinaryRock, 50, LayoutDoctrine.OpenGround, routeNodes: 3);
        True(!ordinaryBounded.Connected && ordinaryBounded.Nodes <= 3,
            "ordinary fallback shares the original per-interface route-node cap");
    }
}

namespace RealisticBases.Planning
{
    internal sealed partial class LayoutAttempt
    {
        internal static (bool Connected, GridPoint[] Excavation, GridPoint[] Route, int Nodes)
            ProbeFieldApproachExcavation(ProceduralLayoutInput input, LayoutDoctrine doctrine)
        {
            var inner = new GridPoint(63, 40);
            var outer = new GridPoint(76, 40);
            var crops = new GridRect(66, 35, 5, 5).Points().ToArray();
            var fieldInterface = new P4BFieldInterface(inner, outer, crops, Array.Empty<GridPoint>(),
                Array.Empty<GridPoint>(), Array.Empty<GridPoint>(), Array.Empty<GridPoint>());
            var site = new P4BJointSite(new GridRect(63, 33, 14, 14), new GridRect(20, 20, 40, 40), new GridPoint(40, 40),
                crops, Array.Empty<GridPoint>(), Array.Empty<GridPoint>(), inner, outer,
                Array.Empty<GridPoint>(), 0, new[] { fieldInterface });
            var grid = LayoutTerrainGrid.Build(input.Terrain, input.Fortification.PlacementMargin).RestrictTo(site.Compound);
            var stats = new LayoutSearchStats();
            var run = new LayoutAttempt(input, input.Equipment.Demand, grid, site.Anchor, doctrine, false,
                input.Seed, stats, new List<LayoutDiagnostic>(), new ProceduralLayoutStageTimings(), jointSite: site);
            var street = new Segment { Id = "captured-mountain-street", Main = true, Axis = new GridPoint(0, 1), Root = site.Anchor };
            for (int z = 30; z <= 50; z++)
            {
                var c = new GridPoint(40, z);
                street.Sections.Add(new List<GridPoint> { c });
                run.occ[grid.Index(c)] = Street;
            }
            run.segments.Add(street);
            bool connected = run.ReserveFieldApproach();
            // The actual route consists of singleton sections; the final three tip cells are a cross-section.
            var approach = run.segments.FirstOrDefault(s => s.Id == "field-approach");
            var route = approach == null ? Array.Empty<GridPoint>() : approach.Cells.TakeWhile(c => !c.Equals(new GridPoint(56, 39))).ToArray();
            return (connected, run.dug.OrderBy(c => c).ToArray(), route, stats.RouteNodes);
        }
    }
}
