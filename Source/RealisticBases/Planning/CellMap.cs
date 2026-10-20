using System;
using System.Collections.Generic;

namespace RealisticBases.Planning
{
    // Dense map-cell values with explicit presence, so default(T) is a valid
    // stored value. Out-of-bounds keys use a small dictionary fallback to retain
    // Dictionary<GridPoint,T> semantics for candidate origins beyond the map.
    public sealed class CellMap<T>
    {
        private readonly int width;
        private readonly int height;
        private readonly T[] values;
        private readonly CellSet present;
        private readonly Dictionary<GridPoint, T> overflow;

        public CellMap(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            this.width = width;
            this.height = height;
            values = new T[checked(width * height)];
            present = new CellSet(width, height);
            overflow = new Dictionary<GridPoint, T>();
        }

        public CellMap(TerrainSnapshot terrain)
            : this(terrain?.Width ?? throw new ArgumentNullException(nameof(terrain)), terrain.Height)
        {
        }

        public int Count => present.Count;

        public T this[GridPoint cell]
        {
            get
            {
                if (!TryGetValue(cell, out T value)) throw new KeyNotFoundException("CellMap has no value for " + cell);
                return value;
            }
            set => Set(cell, value);
        }

        public bool ContainsKey(GridPoint cell) => present.Contains(cell);

        public bool TryGetValue(GridPoint cell, out T value)
        {
            if (!present.Contains(cell))
            {
                value = default(T);
                return false;
            }
            if (InBounds(cell))
            {
                value = values[Index(cell)];
                return true;
            }
            return overflow.TryGetValue(cell, out value);
        }

        public void Set(GridPoint cell, T value)
        {
            if (!InBounds(cell))
            {
                overflow[cell] = value;
                present.Add(cell);
                return;
            }
            values[Index(cell)] = value;
            present.Add(cell);
        }

        public bool Remove(GridPoint cell)
        {
            if (!present.Remove(cell)) return false;
            if (InBounds(cell)) values[Index(cell)] = default(T);
            else overflow.Remove(cell);
            return true;
        }

        public void Clear()
        {
            Array.Clear(values, 0, values.Length);
            present.Clear();
            overflow.Clear();
        }

        private bool InBounds(GridPoint cell) => cell.X >= 0 && cell.Z >= 0 && cell.X < width && cell.Z < height;
        private int Index(GridPoint cell) => cell.Z * width + cell.X;
    }
}
