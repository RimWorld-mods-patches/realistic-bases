using System;
using System.Collections.Generic;

namespace RealisticBases.Planning
{
    // Conservative grid prefilter for proposed P4B firing positions. It treats
    // closed doors and walls alike as opaque; RimWorld's runtime shot check
    // remains authoritative after a position is placed on the actual map.
    public static class P4BFireLane
    {
        public static bool CanEngage(GridPoint origin, GridPoint target, int minimumRange, int maximumRange,
            IReadOnlyCollection<GridPoint> opaque)
        {
            if (minimumRange < 0 || maximumRange < minimumRange) throw new ArgumentOutOfRangeException(nameof(maximumRange));
            if (opaque == null) throw new ArgumentNullException(nameof(opaque));
            long dx = (long)target.X - origin.X, dz = (long)target.Z - origin.Z;
            long squared = dx * dx + dz * dz;
            if (squared == 0 || squared < (long)minimumRange * minimumRange ||
                squared > (long)maximumRange * maximumRange) return false;
            HashSet<GridPoint> blockers = opaque as HashSet<GridPoint> ?? new HashSet<GridPoint>(opaque);
            if (blockers.Contains(origin) || blockers.Contains(target)) return false;
            int nx = Math.Abs(target.X - origin.X), nz = Math.Abs(target.Z - origin.Z);
            int sx = Math.Sign(target.X - origin.X), sz = Math.Sign(target.Z - origin.Z);
            int x = origin.X, z = origin.Z, ix = 0, iz = 0;
            while (ix < nx || iz < nz)
            {
                // Compare crossings without floating-point ties. At an exact
                // grid corner both adjacent cells must be clear; a diagonal
                // wall slit is not accepted as a reliable firing lane.
                long xCross = ix < nx ? (2L * ix + 1) * nz : long.MaxValue;
                long zCross = iz < nz ? (2L * iz + 1) * nx : long.MaxValue;
                if (xCross == zCross)
                {
                    if (blockers.Contains(new GridPoint(x + sx, z)) ||
                        blockers.Contains(new GridPoint(x, z + sz))) return false;
                    x += sx; z += sz; ix++; iz++;
                }
                else if (xCross < zCross) { x += sx; ix++; }
                else { z += sz; iz++; }
                if (blockers.Contains(new GridPoint(x, z))) return false;
            }
            return true;
        }
    }
}
