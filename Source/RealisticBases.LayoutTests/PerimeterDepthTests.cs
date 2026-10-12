using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void FullPerimeterDepth()
    {
        foreach (int layers in new[] { 1, 2, 3 })
        {
            ProceduralLayoutInput input = Input(Plains(120), LayoutRequirements.FromPopulation(6, Industrial, Temperate), 31,
                new LayoutFortificationReservation(wallLayers: layers));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
            Accepted(result);
            Equal(layers, P4BPerimeterDepth.Minimum(input.Terrain, result.Layout),
                "all exterior approaches require the declared depth, not just the gate frontage");
            IReadOnlyList<GridPoint> envelope = P4BPerimeterDepth.Envelope(input.Terrain, result.Layout);
            True(result.Layout.Gates.SelectMany(g => g.BandDoors.Concat(new[] { g.Cell })).All(envelope.Contains),
                "serial gate crossings belong to the declared defended envelope");
            True(!envelope.Any(c => input.Terrain[c].Has(TerrainTraits.MapEdge)), "bounded envelope never grants map-edge pursuit");
            if (layers == 3)
            {
                ProceduralLayout l = result.Layout;
                // Recreate the former section-only geometry without changing
                // its gatehouse counters or connected section topology.
                var broken = new ProceduralLayout(l.InputDigest, l.Seed, l.Doctrine, l.MortarCapable, l.Anchor,
                    l.Rooms, l.Corridors, l.Court, l.Enclosure, l.BuiltWalls, l.NaturalWalls, l.Gates,
                    l.Excavation, l.RetainedThickRoof, l.ConstructedRoof, l.WallLayers, l.FieldCellsRequired,
                    l.Relationships, l.ExtraObstacles, l.Notes, l.TechStage, l.RolesDigest, l.EquipmentDigest,
                    l.WallStuff, l.Materials, l.Power, l.ExhaustPockets, l.OuterSections);
                Equal(1, P4BPerimeterDepth.Minimum(input.Terrain, broken), "old three-layer claim had a one-wall shortcut");
                True(ProceduralLayoutValidator.Validate(input, broken).Contains("perimeter_breach_depth_below_declared_layers"),
                    "production validator rejects the former section-only claim");
                True(l.PerimeterReinforcement.Any(c => c.Z > l.Enclosure.Max(p => p.Z)), "north reinforced too");
                Equal(layers, P4BPerimeterDepth.Minimum(input.Terrain, l.WithAddedObstacles(new GridPoint[0])),
                    "layout cloning preserves reinforcement");
            }
        }
        // A deliberately breached north side cannot hide behind a strong west
        // gatehouse or intact south/east corners. All four directions matter.
        var ground = Plains(50);
        var interior = new GridRect(20, 20, 10, 10).Points().ToArray();
        var walls = new HashSet<GridPoint>();
        foreach (int radius in new[] { 1, 3, 5 })
        {
            int low = 20 - radius, high = 29 + radius;
            for (int z = low; z <= high; z++)
                for (int x = low; x <= high; x++)
                    if (x == low || x == high || z == low || z == high) walls.Add(new GridPoint(x, z));
        }
        Equal(3, P4BPerimeterDepth.Minimum(ground, interior, walls, new GridPoint[0], 3), "closed rectangular shells");
        foreach (GridPoint outward in ProceduralLayoutPlanner.Cardinal)
        {
            var opened = new HashSet<GridPoint>(walls);
            foreach (int radius in new[] { 3, 5 })
                opened.Remove(new GridPoint(outward.X < 0 ? 20 - radius : outward.X > 0 ? 29 + radius : 25,
                    outward.Z < 0 ? 20 - radius : outward.Z > 0 ? 29 + radius : 25));
            Equal(1, P4BPerimeterDepth.Minimum(ground, interior, opened, new GridPoint[0], 3), "shortcut " + outward);
        }
    }
}
