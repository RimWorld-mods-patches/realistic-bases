using System.Linq;
using RimWorld;
using Verse;

namespace RealisticBases.Integration
{
    internal static class StoneChunkClearance
    {
        internal static bool IsLooseStoneChunk(Thing thing) => thing != null &&
            thing.def.category == ThingCategory.Item && thing.def.size.x == 1 && thing.def.size.z == 1 &&
            !thing.def.AffectsRegions && thing.def.IsWithinCategory(ThingCategoryDefOf.StoneChunks);

        internal static bool CanClearAt(IntVec3 cell, Map map)
        {
            if (cell.GetEdifice(map) != null || cell.GetTerrain(map).IsWater) return false;
            var things = cell.GetThingList(map);
            return things.Any(IsLooseStoneChunk) && things.All(t =>
                t.def.passability == Traversability.Standable || t is Plant || IsLooseStoneChunk(t));
        }
    }
}
