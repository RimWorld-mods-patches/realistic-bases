using System;
using System.Collections.Generic;

namespace RealisticBases.Planning
{
    // Dense deterministic membership for map cells. In-bounds cells use one bit
    // each; a small ordered overflow keeps the old set semantics for candidates
    // just outside the map rectangle.
    public sealed class CellSet
    {
        private readonly int width;
        private readonly int height;
        private readonly int cellCount;
        private readonly ulong[] words;
        private readonly HashSet<GridPoint> overflow;
        private readonly List<GridPoint> overflowOrder;
        private int inBoundsCount;

        public CellSet(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            this.width = width;
            this.height = height;
            cellCount = checked(width * height);
            words = new ulong[(int)((cellCount + 63L) / 64L)];
            overflow = new HashSet<GridPoint>();
            overflowOrder = new List<GridPoint>();
        }

        public CellSet(TerrainSnapshot terrain)
            : this(terrain?.Width ?? throw new ArgumentNullException(nameof(terrain)), terrain.Height)
        {
        }

        public int Width => width;
        public int Height => height;
        public int Count => inBoundsCount + overflow.Count;

        public bool InBounds(GridPoint cell) => cell.X >= 0 && cell.Z >= 0 && cell.X < width && cell.Z < height;

        public bool Contains(GridPoint cell)
        {
            if (!InBounds(cell)) return overflow.Contains(cell);
            int index = Index(cell);
            return (words[index >> 6] & (1UL << (index & 63))) != 0;
        }

        public bool Add(GridPoint cell)
        {
            if (!InBounds(cell))
            {
                if (!overflow.Add(cell)) return false;
                overflowOrder.Add(cell);
                return true;
            }

            int index = Index(cell);
            int word = index >> 6;
            ulong mask = 1UL << (index & 63);
            if ((words[word] & mask) != 0) return false;
            words[word] |= mask;
            inBoundsCount++;
            return true;
        }

        public bool Remove(GridPoint cell)
        {
            if (!InBounds(cell))
            {
                if (!overflow.Remove(cell)) return false;
                overflowOrder.Remove(cell);
                return true;
            }
            int index = Index(cell);
            int word = index >> 6;
            ulong mask = 1UL << (index & 63);
            if ((words[word] & mask) == 0) return false;
            words[word] &= ~mask;
            inBoundsCount--;
            return true;
        }

        public void UnionWith(CellSet other)
        {
            EnsureCompatible(other);
            if (ReferenceEquals(this, other)) return;
            for (int i = 0; i < words.Length; i++)
            {
                ulong old = words[i];
                ulong merged = old | other.words[i];
                inBoundsCount += PopCount(merged ^ old);
                words[i] = merged;
            }
            for (int i = 0; i < other.overflowOrder.Count; i++) Add(other.overflowOrder[i]);
        }

        public void UnionWith(IReadOnlyList<GridPoint> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            for (int i = 0; i < cells.Count; i++) Add(cells[i]);
        }

        public void UnionWith(HashSet<GridPoint> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            foreach (GridPoint cell in cells) Add(cell);
        }

        public void ExceptWith(CellSet other)
        {
            EnsureCompatible(other);
            if (ReferenceEquals(this, other))
            {
                Clear();
                return;
            }
            for (int i = 0; i < words.Length; i++)
            {
                ulong old = words[i];
                ulong remaining = old & ~other.words[i];
                inBoundsCount -= PopCount(old ^ remaining);
                words[i] = remaining;
            }
            for (int i = 0; i < other.overflowOrder.Count; i++) Remove(other.overflowOrder[i]);
        }

        public void ExceptWith(IReadOnlyList<GridPoint> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            for (int i = 0; i < cells.Count; i++) Remove(cells[i]);
        }

        public void CopyFrom(CellSet other)
        {
            EnsureCompatible(other);
            if (ReferenceEquals(this, other)) return;
            Array.Copy(other.words, words, words.Length);
            inBoundsCount = other.inBoundsCount;
            overflow.Clear();
            overflowOrder.Clear();
            for (int i = 0; i < other.overflowOrder.Count; i++)
            {
                GridPoint cell = other.overflowOrder[i];
                if (other.overflow.Contains(cell))
                {
                    overflow.Add(cell);
                    overflowOrder.Add(cell);
                }
            }
        }

        public CellSet Clone()
        {
            CellSet copy = new CellSet(width, height);
            copy.CopyFrom(this);
            return copy;
        }

        public void Clear()
        {
            Array.Clear(words, 0, words.Length);
            inBoundsCount = 0;
            overflow.Clear();
            overflowOrder.Clear();
        }

        // Clears destination, then copies in-bounds cells in GridPoint order
        // (Z, then X), followed by out-of-bounds cells in insertion order.
        public void CopyTo(List<GridPoint> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                ulong word = words[wordIndex];
                while (word != 0)
                {
                    int bit = TrailingZeroCount(word);
                    int index = wordIndex * 64 + bit;
                    if (index < cellCount) destination.Add(new GridPoint(index % width, index / width));
                    word &= word - 1;
                }
            }
            for (int i = 0; i < overflowOrder.Count; i++)
            {
                GridPoint cell = overflowOrder[i];
                if (overflow.Contains(cell)) destination.Add(cell);
            }
        }

        public HashSet<GridPoint> ToHashSet()
        {
            List<GridPoint> cells = new List<GridPoint>();
            CopyTo(cells);
            return new HashSet<GridPoint>(cells);
        }

        private int Index(GridPoint cell) => cell.Z * width + cell.X;

        private void EnsureCompatible(CellSet other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (width != other.width || height != other.height)
                throw new ArgumentException("CellSet dimensions must match.", nameof(other));
        }

        private static int PopCount(ulong value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }

        private static int TrailingZeroCount(ulong value)
        {
            int count = 0;
            while ((value & 1UL) == 0)
            {
                value >>= 1;
                count++;
            }
            return count;
        }
    }
}
