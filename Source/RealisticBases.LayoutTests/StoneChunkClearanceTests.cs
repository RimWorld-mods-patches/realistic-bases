using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void StoneChunkThermalClearance()
    {
        // Cove workshop exhaust cells (82,67)/(83,67) are public street with
        // loose chunks. Native spawn clears the enclosure before auditing rooms.
        var cleared = new GridPoint(5, 5);
        var untouched = new GridPoint(9, 5);
        TerrainTraits chunks = (Ground & ~TerrainTraits.Standable) | TerrainTraits.ClearableStoneChunk;
        var terrain = Build(14, 12, (x, z) => z == 5 && (x == 5 || x == 9) ? chunks : Ground);
        var layout = PartitionLayout(Array.Empty<GridPoint>(), Array.Empty<GridPoint>(), enclosure: new[] { cleared });
        string before = terrain.Digest;
        var partition = LayoutRoomPartition.Build(terrain, layout);
        True(partition.IsOutdoor(cleared), "a public exhaust cell cleared by native spawn belongs to outdoor air");
        True(partition.RoomAt(untouched) < 0, "chunks beyond the owned clearance retain native thermal behavior");
        Equal(before, terrain.Digest, "thermal prediction never alters the captured terrain");
        foreach (var permanent in new[] { TerrainTraits.Occupied, TerrainTraits.NaturalRock, TerrainTraits.Water })
        {
            var blocked = Build(14, 12, (x, z) => x == 5 && z == 5 ? chunks | permanent : Ground);
            True(LayoutRoomPartition.Build(blocked, layout).RoomAt(cleared) < 0,
                "clearance does not remove permanent obstruction " + permanent);
        }
    }

    private static void StoneChunkTerrain()
    {
        // Captured chunks are walkable but not Standable. They carry neither
        // Occupied nor NaturalRock: a room/field can clear the same loose item.
        const TerrainTraits chunkFlag = TerrainTraits.ClearableStoneChunk;
        TerrainTraits chunkGround = (Ground & ~TerrainTraits.Standable) | chunkFlag;
        var terrain = Build(80, 80, (x, z) => chunkGround);
        string before = terrain.Digest;
        var grid = LayoutTerrainGrid.Build(terrain, 3);
        True(grid.CanPlace(new GridPoint(40, 40)), "loose chunks do not remove room envelopes or create a terrain setback");
        Equal(LayoutCellClass.Open, grid.At(new GridPoint(40, 40)), "chunk-covered dry ground is available after clearance");
        True(LayoutExposure.Walkable(new TerrainCell(chunkGround)), "loose chunks do not seal an arrival route");
        var pairs = P4BJointSitePlanner.Find(terrain, 124, 550, 3, 1234, 4, 4096);
        True(pairs.Candidates.Count > 0, "fertile land covered by loose chunks still supplies a field and compound");
        True(pairs.Candidates[0].Crops.All(c => terrain[c].Has(chunkFlag)), "the field regression actually uses chunk-covered crop cells");
        Equal(before, terrain.Digest, "planning does not delete chunks or mutate its terrain input");
        foreach (var permanent in new[] { TerrainTraits.Water, TerrainTraits.Occupied })
            Equal(LayoutCellClass.Bad, LayoutTerrainGrid.Classify(new TerrainCell(chunkGround | permanent)),
                "chunk clearance does not legalize water or an existing building");
        Equal(LayoutCellClass.Rock, LayoutTerrainGrid.Classify(new TerrainCell(chunkGround | TerrainTraits.NaturalRock)),
            "natural rock remains excavation terrain");
        Equal(LayoutCellClass.Bad, LayoutTerrainGrid.Classify(new TerrainCell(chunkGround & ~TerrainTraits.Buildable)),
            "a loose chunk does not grant building affordances to its ground");
    }

    private static void StoneChunkDestinations()
    {
        var source = new[] { new GridPoint(2, 2), new GridPoint(2, 2) };
        var empty = new[] { new GridPoint(2, 2), new GridPoint(2, 3), new GridPoint(3, 2), new GridPoint(3, 2), new GridPoint(0, 0) };
        var result = P4ChunkRelocation.SelectDestinations(source, empty);
        Equal(2, result.Count, "each chunk object gets a destination even when sources share a cell");
        Equal(new GridPoint(3, 2), result[0], "nearest destinations use stable coordinate ordering");
        Equal(new GridPoint(2, 3), result[1], "destinations are distinct, so stacks cannot merge");
        True(!result.Any(source.Contains), "source cells are not reused as destinations");
        True(P4ChunkRelocation.SelectDestinations(source, new[] { new GridPoint(3, 2), new GridPoint(3, 2) }) == null,
            "insufficient distinct empty cells rejects before moving any object");
        Equal(string.Join(";", result), string.Join(";", P4ChunkRelocation.SelectDestinations(source, empty.Reverse())),
            "allocation does not depend on map enumeration order");
    }
}
