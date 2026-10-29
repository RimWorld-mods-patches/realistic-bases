using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void FieldFenceNearWater()
    {
        // The field fence is dry and buildable. Nearby water does not require
        // the compound's three-cell fortification setback around that fence.
        var field = new GridRect(74, 50, 20, 20);
        var compound = new GridRect(26, 37, 46, 46);
        TerrainSnapshot terrain = Build(140, 140, (x, z) =>
        {
            var c = new GridPoint(x, z);
            if (field.Contains(c)) return Ground;
            if (compound.Contains(c) || x >= 70 && x <= 75 && z >= 57 && z <= 63 ||
                x >= 94 && x <= 101 && z >= 50 && z <= 69)
                return Ground & ~TerrainTraits.Fertile;
            return DeepWater;
        });
        string digest = terrain.Digest;
        var sites = P4BJointSitePlanner.Find(terrain, 216, 550, 3, 1234,
            attachmentDepth: 7);
        True(sites.Candidates.Count > 0, "a dry field fence beside water can attach to the reserved compound");
        True(sites.Evaluated <= 3 * 4096 && sites.FieldOriginsScanned <= 3 * terrain.Width * terrain.Height,
            "all fallback work remains bounded independently of room repair");
        var parallel = P4BJointSitePlanner.Find(terrain, 216, 550, 3, 1234,
            attachmentDepth: 7, plannerDegree: 4);
        True(sites.Candidates.Select(s => s.Digest).SequenceEqual(parallel.Candidates.Select(s => s.Digest)),
            "parallel field rescue retains deterministic site selection");
        foreach (var site in sites.Candidates)
        {
            True(site.Crops.Count >= 216 && site.Crops.All(c => terrain[c].Has(TerrainTraits.Fertile) &&
                !terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.Roofed)), "crop capacity and outdoor soil remain mandatory");
            True(site.Wall.All(c => terrain[c].Has(TerrainTraits.Buildable) &&
                !terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.NaturalRock | TerrainTraits.Occupied)),
                "the fence itself remains legal dry ground");
        }
        Equal(digest, terrain.Digest, "siting preserves the captured terrain");
    }
}
