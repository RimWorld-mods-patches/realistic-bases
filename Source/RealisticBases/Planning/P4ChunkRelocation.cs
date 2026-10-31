using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    public static class P4ChunkRelocation
    {
        // The caller supplies empty cells outside every protected footprint.
        // Each source object gets its own destination: no merging or deletion.
        public static IReadOnlyList<GridPoint> SelectDestinations(IReadOnlyList<GridPoint> sources,
            IEnumerable<GridPoint> available)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (available == null) throw new ArgumentNullException(nameof(available));
            var free = available.Except(sources).Distinct().OrderBy(c => c).ToList();
            if (free.Count < sources.Count) return null;
            var result = new List<GridPoint>(sources.Count);
            foreach (GridPoint source in sources)
            {
                int best = 0, distance = int.MaxValue;
                for (int i = 0; i < free.Count; i++)
                {
                    int next = Math.Abs(source.X - free[i].X) + Math.Abs(source.Z - free[i].Z);
                    if (next < distance) { distance = next; best = i; }
                }
                result.Add(free[best]);
                free.RemoveAt(best);
            }
            return result;
        }
    }
}
