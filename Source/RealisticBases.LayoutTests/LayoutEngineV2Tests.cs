using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// Layout engine v2 (docs/plans/2026-10-12-layout-engine-v2.md).
internal static partial class Program
{
    private static void RunLayoutEngineV2Tests()
    {
        Run("V2 neighbouring rooms may share plain walls only", V2SharedWallContract);
        Run("V2 shared walls are built and charged once", V2SharedWallLedger);
        Run("V2 engine choice is part of the input digest, legacy unchanged", V2EngineDigest);
    }

    private static LayoutRoom V2Room(string id, GridRect bounds, GridPoint door, GridPoint facing, params LayoutFixture[] fixtures) =>
        new LayoutRoom(new LayoutRoomDemand(id, LayoutRoomKind.Storage, 1, "stockpile-cells", new[] { new GridRect(0, 0, 3, 3) }),
            bounds, door, facing, 1, fixtures, false, new GridPoint[0]);

    private static void V2SharedWallContract()
    {
        // Two 3x3-interior rooms side by side on x=4, doors on the south wall.
        LayoutRoom west = V2Room("west", new GridRect(0, 0, 5, 5), new GridPoint(2, 0), new GridPoint(0, 1));
        LayoutRoom east = V2Room("east", new GridRect(4, 0, 5, 5), new GridPoint(6, 0), new GridPoint(0, 1));
        Equal(0, ProceduralLayoutValidator.SharedWallViolations(new[] { west, east }).Count, "common wall is legal");

        // A shared corner (rooms meeting diagonally) is a wall of both as well.
        LayoutRoom north = V2Room("north", new GridRect(4, 4, 5, 5), new GridPoint(6, 8), new GridPoint(0, -1));
        Equal(0, ProceduralLayoutValidator.SharedWallViolations(new[] { west, north }).Count, "shared corner is legal");

        LayoutRoom overlapping = V2Room("overlap", new GridRect(3, 0, 5, 5), new GridPoint(5, 0), new GridPoint(0, 1));
        Contains(ProceduralLayoutValidator.SharedWallViolations(new[] { west, overlapping }), "room_overlap_overlap");

        LayoutRoom doorOnShared = V2Room("door", new GridRect(0, 0, 5, 5), new GridPoint(4, 2), new GridPoint(-1, 0));
        Contains(ProceduralLayoutValidator.SharedWallViolations(new[] { doorOnShared, east }), "room_overlap_door");

        LayoutFixture cooler = new LayoutFixture(LayoutFixtureKind.WallCooler, "Cooler", new[] { new GridPoint(4, 2) }, null,
            new GridPoint(-1, 0), true, 200, false, "cooled", new GridPoint(5, 2));
        LayoutRoom cooled = V2Room("cooled", new GridRect(0, 0, 5, 5), new GridPoint(2, 0), new GridPoint(0, 1), cooler);
        Contains(ProceduralLayoutValidator.SharedWallViolations(new[] { cooled, east }), "room_overlap_cooled");
    }

    private static void V2SharedWallLedger()
    {
        ProceduralLayoutInput input = Input(Plains(60), LayoutRequirements.FromPopulation(4, Industrial, Temperate), 7);
        LayoutRoom west = V2Room("west", new GridRect(0, 0, 5, 5), new GridPoint(2, 0), new GridPoint(0, 1));
        LayoutRoom east = V2Room("east", new GridRect(4, 0, 5, 5), new GridPoint(6, 0), new GridPoint(0, 1));
        LayoutRoom apart = V2Room("east", new GridRect(10, 0, 5, 5), new GridPoint(12, 0), new GridPoint(0, 1));
        int shared = input.Equipment.Count(new[] { west, east }, new GridPoint[0], 0, null).WallCells;
        int separate = input.Equipment.Count(new[] { west, apart }, new GridPoint[0], 0, null).WallCells;
        // Each room has 16 edge cells less its door; the common wall (5 cells) is counted once.
        Equal(30, separate, "two separate rooms");
        Equal(25, shared, "shared wall charged once");
    }

    private static void V2EngineDigest()
    {
        TerrainSnapshot terrain = Plains(60);
        LayoutRequirements requirements = LayoutRequirements.FromPopulation(4, Industrial, Temperate);
        ProceduralLayoutInput implicitLegacy = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog);
        ProceduralLayoutInput legacy = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog, engine: LayoutEngine.Legacy);
        ProceduralLayoutInput clusters = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog, engine: LayoutEngine.Clusters);
        Equal(implicitLegacy.Digest, legacy.Digest, "legacy digest unchanged");
        True(clusters.Digest != legacy.Digest, "clusters engine is a distinct input");
        Equal(LayoutEngine.Legacy, implicitLegacy.Engine, "legacy is the default engine");
    }
}
