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
            HashSet<GridPoint> blockers = opaque as HashSet<GridPoint> ?? new HashSet<GridPoint>(opaque);
            return CanEngageCore(origin, target, minimumRange, maximumRange,
                new HashSetBlocker(blockers));
        }

        // Joint-site preflight uses a fixed terrain-only blocker mask across
        // many candidate posts. Avoid re-hashing every ray cell for that path.
        internal static bool CanEngage(GridPoint origin, GridPoint target, int minimumRange, int maximumRange,
            bool[] opaque, int width, int height)
        {
            if (minimumRange < 0 || maximumRange < minimumRange) throw new ArgumentOutOfRangeException(nameof(maximumRange));
            if (opaque == null) throw new ArgumentNullException(nameof(opaque));
            if (width <= 0 || height <= 0 || opaque.Length != width * height)
                throw new ArgumentException("Opaque mask dimensions do not match the grid.", nameof(opaque));
            return CanEngageCore(origin, target, minimumRange, maximumRange,
                new MaskBlocker(opaque, width, height));
        }

        internal static bool CanEngage(GridPoint origin, GridPoint target, int minimumRange, int maximumRange,
            CellSet opaque)
        {
            if (minimumRange < 0 || maximumRange < minimumRange) throw new ArgumentOutOfRangeException(nameof(maximumRange));
            if (opaque == null) throw new ArgumentNullException(nameof(opaque));
            return CanEngageCore(origin, target, minimumRange, maximumRange,
                new CellSetBlocker(opaque));
        }

        private static bool CanEngageCore<TBlocker>(GridPoint origin, GridPoint target, int minimumRange,
            int maximumRange, TBlocker blocker) where TBlocker : struct, IBlocker
        {
            long dx = (long)target.X - origin.X, dz = (long)target.Z - origin.Z;
            long squared = dx * dx + dz * dz;
            if (squared == 0 || squared < (long)minimumRange * minimumRange ||
                squared > (long)maximumRange * maximumRange) return false;
            if (blocker.IsBlocked(origin) || blocker.IsBlocked(target)) return false;
            int nx = Math.Abs(target.X - origin.X), nz = Math.Abs(target.Z - origin.Z);
            int sx = Math.Sign(target.X - origin.X), sz = Math.Sign(target.Z - origin.Z);
            int x = origin.X, z = origin.Z, ix = 0, iz = 0;
            while (ix < nx || iz < nz)
            {
                long xCross = ix < nx ? (2L * ix + 1) * nz : long.MaxValue;
                long zCross = iz < nz ? (2L * iz + 1) * nx : long.MaxValue;
                if (xCross == zCross)
                {
                    if (blocker.IsBlocked(new GridPoint(x + sx, z)) ||
                        blocker.IsBlocked(new GridPoint(x, z + sz))) return false;
                    x += sx; z += sz; ix++; iz++;
                }
                else if (xCross < zCross) { x += sx; ix++; }
                else { z += sz; iz++; }
                if (blocker.IsBlocked(new GridPoint(x, z))) return false;
            }
            return true;
        }

        private interface IBlocker
        {
            bool IsBlocked(GridPoint cell);
        }

        private struct HashSetBlocker : IBlocker
        {
            private readonly HashSet<GridPoint> cells;
            public HashSetBlocker(HashSet<GridPoint> cells) { this.cells = cells; }
            public bool IsBlocked(GridPoint cell) { return cells.Contains(cell); }
        }

        private struct MaskBlocker : IBlocker
        {
            private readonly bool[] cells;
            private readonly int width, height;
            public MaskBlocker(bool[] cells, int width, int height)
            { this.cells = cells; this.width = width; this.height = height; }
            public bool IsBlocked(GridPoint cell)
            {
                return cell.X >= 0 && cell.Z >= 0 && cell.X < width && cell.Z < height &&
                    cells[cell.Z * width + cell.X];
            }
        }

        private struct CellSetBlocker : IBlocker
        {
            private readonly CellSet cells;
            public CellSetBlocker(CellSet cells) { this.cells = cells; }
            public bool IsBlocked(GridPoint cell) { return cells.Contains(cell); }
        }
    }
}
