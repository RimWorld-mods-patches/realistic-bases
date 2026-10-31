using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RealisticBases.Planning;

internal static partial class Program
{
    // Exact terrain and proposed-wall blockers from rejected candidate 3777140E7656D31C.
    // This tests the field-arrival rejection itself, independently of room-search budgets.
    private static void CoastalIslandFieldArrival()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "coastalisland-field-route.json")));
        JsonElement fixture = document.RootElement;
        var cells = new List<TerrainCell>();
        foreach (var row in fixture.GetProperty("terrainRuns").EnumerateArray())
            for (int i = 0; i < row[0].GetInt32(); i++)
                cells.Add(new TerrainCell((TerrainTraits)row[1].GetInt32(),
                    (ushort)row[2].GetInt32(), (ushort)row[3].GetInt32()));
        var terrain = new TerrainSnapshot(200, 200, cells, fixture.GetProperty("sourceVersion").GetString());
        Equal("964955B192B71C51", terrain.Digest, "captured terrain including terrain/roof IDs is unchanged");
        Equal("0CAF3237DF5C8CBB", fixture.GetProperty("inputDigest").GetString(), "source input anchor");
        Equal("3777140E7656D31C", fixture.GetProperty("candidateDigest").GetString(), "rejected candidate anchor");
        GridPoint Point(JsonElement p) => new GridPoint(p[0].GetInt32(), p[1].GetInt32());
        HashSet<GridPoint> Blockers(string key) => new HashSet<GridPoint>(fixture.GetProperty("blockers")
            .GetProperty(key).EnumerateArray().SelectMany(span => Enumerable.Range(span[1].GetInt32(),
                span[2].GetInt32()).Select(x => new GridPoint(x, span[0].GetInt32()))));
        var occupied = Blockers("occupied");
        var baseline = Blockers("baseline");
        var walls = Blockers("walls");
        var doors = Blockers("doors");
        var blocked = new HashSet<GridPoint>(occupied.Concat(baseline).Concat(walls).Concat(doors));
        bool Legal(GridPoint c) => terrain.InBounds(c) && LayoutExposure.Walkable(terrain[c]) && !blocked.Contains(c);
        var start = new GridPoint(74, 94);
        var join = new GridPoint(98, 78);
        var userSegment = Enumerable.Range(78, 17).Reverse().Select(z => new GridPoint(74, z))
            .Concat(Enumerable.Range(75, 24).Select(x => new GridPoint(x, 78))).ToList();
        True(userSegment.All(c => Legal(c) && !terrain[c].Has(TerrainTraits.Water)),
            "the user's south-then-east segment is entirely dry and clear of the new walls");
        var oldRoute = fixture.GetProperty("oldArrival").EnumerateArray().Select(Point).ToList();
        True(oldRoute.Any(walls.Contains), "the old whole route cannot be blindly reused after field construction");
        var suffix = oldRoute.SkipWhile(c => !c.Equals(join)).ToList();
        Equal(79, suffix.Count, "surviving old arrival suffix length");
        Equal(36, suffix.Count(c => terrain[c].Has(TerrainTraits.Water)), "suffix crosses standable shallow water");
        var witness = userSegment.Concat(suffix.Skip(1)).ToList();
        Equal(119, witness.Count, "the user's complete exterior witness reaches the edge");
        CheckRoute(witness);

        var route = P4BFieldWallPlanner.RouteOutside(Point(fixture.GetProperty("outerGate")),
            Point(fixture.GetProperty("outward")), terrain, occupied, baseline, walls, doors);
        True(route != null, "field arrival must find a legal exterior path when the captured witness exists");
        CheckRoute(route);
        var secondary = P4BFieldWallPlanner.RouteFromStagingToEdge(terrain, start, blocked, 40000);
        True(secondary != null, "secondary gate reconnection uses the same exterior movement rule");
        CheckRoute(secondary);
        Equal(terrain.Digest, fixture.GetProperty("terrainDigest").GetString(), "routing preserves captured terrain");

        void CheckRoute(IReadOnlyList<GridPoint> path)
        {
            Equal(start, path[0], "route begins outside the field gate");
            True(terrain[path[path.Count - 1]].Has(TerrainTraits.MapEdge), "route reaches a map edge");
            True(path.All(Legal), "every route cell remains exterior, pawn-walkable and free of proposed walls/doors");
            True(path.Zip(path.Skip(1), (a, b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z))
                .All(distance => distance == 1), "the route is cardinally continuous");
        }
    }

    private static void FieldMovementAndDryGround()
    {
        var shallow = TerrainTraits.Standable | TerrainTraits.Water;
        var origin = new GridPoint(4, 4);
        var exit = new GridPoint(4, 8);
        TerrainSnapshot channel = Build(9, 9, (x, z) => x == 4 && z >= 4 ? shallow : DeepWater);
        var empty = new HashSet<GridPoint>();
        List<GridPoint> Route(HashSet<GridPoint> walls, HashSet<GridPoint> doors) =>
            P4BFieldWallPlanner.RouteOutside(new GridPoint(4, 3), new GridPoint(0, 1),
                channel, empty, empty, walls, doors);
        var route = Route(empty, empty);
        True(route != null && route.Last().Equals(exit), "standable shallows provide the only exterior arrival");
        True(Route(new HashSet<GridPoint> { new GridPoint(4, 6) }, empty) == null,
            "a proposed wall still closes a shallow-water approach");
        True(Route(empty, new HashSet<GridPoint> { origin }) == null,
            "a declared door at the outside start cannot authorize re-entry");
        channel = Build(9, 9, (x, z) => x == 4 && z >= 4 && z != 6 ? shallow : DeepWater);
        True(Route(empty, empty) == null, "deep water still blocks exterior arrivals");

        // A complete dry field enclosure sits on an island separated from every edge by a moat.
        var inner = new GridPoint(9, 4);
        var outer = new GridPoint(9, 15);
        var baseline = Enumerable.Range(4, 12).Select(x => new GridPoint(x, 4)).Where(c => !c.Equals(inner)).ToList();
        var wall = Enumerable.Range(5, 11).SelectMany(z => new[] { new GridPoint(4, z), new GridPoint(15, z) })
            .Concat(Enumerable.Range(5, 10).Select(x => new GridPoint(x, 15))).Where(c => !c.Equals(outer)).ToList();
        var crop = new GridPoint(9, 10);
        var post = new GridPoint(9, 17);
        var breach = new GridPoint(6, 15);
        TerrainSnapshot island = Island(shallow);
        TerrainSnapshot Island(TerrainTraits moat, GridPoint? wet = null) => Build(20, 20, (x, z) =>
            wet.HasValue && wet.Value.Equals(new GridPoint(x, z)) ? shallow | Ground :
            x == 1 || x == 18 || z == 1 || z == 18 ? moat : Ground);
        IReadOnlyList<string> Validate(IEnumerable<GridPoint> walls) => P4BFieldWall.Validate(island,
            new[] { crop }, baseline, walls, new[] { inner, outer }, new[] { post }, new GridPoint(9, 2));
        Equal(0, Validate(wall).Count, "the outside post connects to the edge across standable shallows");
        island = Island(DeepWater);
        Contains(Validate(wall), "post_not_outside_field_wall");
        island = Island(shallow, breach);
        Contains(Validate(wall), "field_wall_terrain_invalid");
        Contains(Validate(wall.Where(c => !c.Equals(breach))), "field_exposed_behind_closed_wall");
        island = Island(shallow, crop);
        Contains(Validate(wall), "field_not_fertile_unroofed_ground");
    }
}
