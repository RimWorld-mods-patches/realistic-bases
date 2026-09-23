using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RealisticBases.Planning
{
    public static class P1Versions
    {
        public const int ProfileSchema = 1;
        public const int SnapshotSchema = 1;
        public const int PlanSchema = 1;
        public const string Planner = "p1.1";
    }

    public struct GridPoint : IEquatable<GridPoint>, IComparable<GridPoint>
    {
        public readonly int X;
        public readonly int Z;

        public GridPoint(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int CompareTo(GridPoint other)
        {
            int z = Z.CompareTo(other.Z);
            return z != 0 ? z : X.CompareTo(other.X);
        }

        public bool Equals(GridPoint other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is GridPoint && Equals((GridPoint)obj);
        public override int GetHashCode() => unchecked((X * 397) ^ Z);
        public override string ToString() => X + "," + Z;
    }

    [Flags]
    public enum TerrainTraits : ushort
    {
        None = 0,
        Standable = 1 << 0,
        Buildable = 1 << 1,
        Roofed = 1 << 2,
        Water = 1 << 3,
        NaturalRock = 1 << 4,
        Fertile = 1 << 5,
        SupportsHeavy = 1 << 6,
        MapEdge = 1 << 7,
        Occupied = 1 << 8,
        ThickRoof = 1 << 9
    }

    public struct TerrainCell
    {
        public readonly TerrainTraits Traits;
        public readonly ushort TerrainId;
        public readonly ushort RoofId;

        public TerrainCell(TerrainTraits traits, ushort terrainId = 0, ushort roofId = 0)
        {
            Traits = traits;
            TerrainId = terrainId;
            RoofId = roofId;
        }

        public bool Has(TerrainTraits traits) => (Traits & traits) == traits;
        public bool HasAny(TerrainTraits traits) => (Traits & traits) != 0;
    }

    public sealed class TerrainSnapshot
    {
        private readonly TerrainCell[] cells;

        public TerrainSnapshot(int width, int height, IEnumerable<TerrainCell> source, string sourceVersion)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException("Snapshot dimensions must be positive.");
            }
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Width = width;
            Height = height;
            SourceVersion = sourceVersion ?? string.Empty;
            List<TerrainCell> copy = new List<TerrainCell>(source);
            if (copy.Count != width * height)
            {
                throw new ArgumentException("Snapshot cell count does not match its dimensions.", nameof(source));
            }
            cells = copy.ToArray();
            Digest = StableHash.Hex(ComputeDigest());
        }

        public int SchemaVersion => P1Versions.SnapshotSchema;
        public int Width { get; }
        public int Height { get; }
        public string SourceVersion { get; }
        public string Digest { get; }

        public TerrainCell this[int x, int z] => cells[IndexOf(x, z)];
        public TerrainCell this[GridPoint point] => this[point.X, point.Z];

        public bool InBounds(GridPoint point) => point.X >= 0 && point.Z >= 0 && point.X < Width && point.Z < Height;

        public IEnumerable<GridPoint> Points()
        {
            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    yield return new GridPoint(x, z);
                }
            }
        }

        private int IndexOf(int x, int z)
        {
            if (x < 0 || z < 0 || x >= Width || z >= Height)
            {
                throw new ArgumentOutOfRangeException("Cell is outside the snapshot.");
            }
            return z * Width + x;
        }

        private ulong ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, SchemaVersion);
            hash = StableHash.Add(hash, Width);
            hash = StableHash.Add(hash, Height);
            hash = StableHash.Add(hash, SourceVersion);
            for (int i = 0; i < cells.Length; i++)
            {
                hash = StableHash.Add(hash, (int)cells[i].Traits);
                hash = StableHash.Add(hash, cells[i].TerrainId);
                hash = StableHash.Add(hash, cells[i].RoofId);
            }
            return hash;
        }
    }

    public sealed class SettlementProfile
    {
        public SettlementProfile(string id, int version, int compoundWidth, int compoundHeight,
            int requiredCapacity, int maxCandidates, int maxRouteNodes = 4096)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Profile id is required.", nameof(id));
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (compoundWidth < 5 || compoundHeight < 5) throw new ArgumentOutOfRangeException("Compound dimensions must be at least 5.");
            if (requiredCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(requiredCapacity));
            if (maxCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
            if (maxRouteNodes <= 0) throw new ArgumentOutOfRangeException(nameof(maxRouteNodes));

            Id = id;
            Version = version;
            CompoundWidth = compoundWidth;
            CompoundHeight = compoundHeight;
            RequiredCapacity = requiredCapacity;
            MaxCandidates = maxCandidates;
            MaxRouteNodes = maxRouteNodes;
        }

        public int SchemaVersion => P1Versions.ProfileSchema;
        public string Id { get; }
        public int Version { get; }
        public int CompoundWidth { get; }
        public int CompoundHeight { get; }
        public int RequiredCapacity { get; }
        public int MaxCandidates { get; }
        public int MaxRouteNodes { get; }

        public string Digest
        {
            get
            {
                ulong hash = StableHash.Start;
                hash = StableHash.Add(hash, SchemaVersion);
                hash = StableHash.Add(hash, Id);
                hash = StableHash.Add(hash, Version);
                hash = StableHash.Add(hash, CompoundWidth);
                hash = StableHash.Add(hash, CompoundHeight);
                hash = StableHash.Add(hash, RequiredCapacity);
                hash = StableHash.Add(hash, MaxCandidates);
                hash = StableHash.Add(hash, MaxRouteNodes);
                return StableHash.Hex(hash);
            }
        }
    }

    public sealed class PlannerInput
    {
        public PlannerInput(SettlementProfile profile, TerrainSnapshot terrain, ulong seed)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            Seed = seed;
        }

        public SettlementProfile Profile { get; }
        public TerrainSnapshot Terrain { get; }
        public ulong Seed { get; }

        public string Digest
        {
            get
            {
                ulong hash = StableHash.Start;
                hash = StableHash.Add(hash, P1Versions.Planner);
                hash = StableHash.Add(hash, Profile.Digest);
                hash = StableHash.Add(hash, Terrain.Digest);
                hash = StableHash.Add(hash, Seed);
                return StableHash.Hex(hash);
            }
        }
    }

    public static class SeedDeriver
    {
        public static ulong Derive(string worldSeed, int tile, SettlementProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, worldSeed ?? string.Empty);
            hash = StableHash.Add(hash, tile);
            hash = StableHash.Add(hash, profile.Id);
            hash = StableHash.Add(hash, profile.Version);
            hash = StableHash.Add(hash, P1Versions.ProfileSchema);
            hash = StableHash.Add(hash, P1Versions.SnapshotSchema);
            hash = StableHash.Add(hash, P1Versions.PlanSchema);
            return hash;
        }
    }

    public sealed class ConnectedRegion
    {
        internal ConnectedRegion(int id, List<GridPoint> cells, int minX, int minZ, int maxX, int maxZ, bool touchesEdge)
        {
            Id = id;
            Cells = new ReadOnlyCollection<GridPoint>(cells);
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            TouchesEdge = touchesEdge;
        }

        public int Id { get; }
        public IReadOnlyList<GridPoint> Cells { get; }
        public int Area => Cells.Count;
        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }
        public bool TouchesEdge { get; }
    }

    public static class ConnectedRegionAnalyzer
    {
        private static readonly GridPoint[] Neighbors =
        {
            new GridPoint(0, -1),
            new GridPoint(-1, 0),
            new GridPoint(1, 0),
            new GridPoint(0, 1)
        };

        public static IReadOnlyList<ConnectedRegion> Analyze(TerrainSnapshot snapshot, Func<TerrainCell, bool> included)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (included == null) throw new ArgumentNullException(nameof(included));

            bool[] visited = new bool[snapshot.Width * snapshot.Height];
            List<ConnectedRegion> regions = new List<ConnectedRegion>();
            Queue<GridPoint> pending = new Queue<GridPoint>();
            foreach (GridPoint start in snapshot.Points())
            {
                int startIndex = start.Z * snapshot.Width + start.X;
                if (visited[startIndex] || !included(snapshot[start]))
                {
                    visited[startIndex] = true;
                    continue;
                }

                int minX = start.X;
                int minZ = start.Z;
                int maxX = start.X;
                int maxZ = start.Z;
                bool touchesEdge = false;
                List<GridPoint> cells = new List<GridPoint>();
                visited[startIndex] = true;
                pending.Enqueue(start);
                while (pending.Count > 0)
                {
                    GridPoint current = pending.Dequeue();
                    cells.Add(current);
                    minX = Math.Min(minX, current.X);
                    minZ = Math.Min(minZ, current.Z);
                    maxX = Math.Max(maxX, current.X);
                    maxZ = Math.Max(maxZ, current.Z);
                    touchesEdge |= current.X == 0 || current.Z == 0 || current.X == snapshot.Width - 1 || current.Z == snapshot.Height - 1;

                    for (int i = 0; i < Neighbors.Length; i++)
                    {
                        GridPoint next = new GridPoint(current.X + Neighbors[i].X, current.Z + Neighbors[i].Z);
                        if (!snapshot.InBounds(next)) continue;
                        int nextIndex = next.Z * snapshot.Width + next.X;
                        if (visited[nextIndex]) continue;
                        visited[nextIndex] = true;
                        if (included(snapshot[next])) pending.Enqueue(next);
                    }
                }
                cells.Sort();
                regions.Add(new ConnectedRegion(regions.Count, cells, minX, minZ, maxX, maxZ, touchesEdge));
            }
            return new ReadOnlyCollection<ConnectedRegion>(regions);
        }
    }

    public static class StableHash
    {
        public const ulong Start = 1469598103934665603UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Add(ulong hash, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= Prime;
                }
                return hash;
            }
        }

        public static ulong Add(ulong hash, ushort value) => Add(hash, (int)value);

        public static ulong Add(ulong hash, ulong value)
        {
            unchecked
            {
                for (int shift = 0; shift < 64; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= Prime;
                }
                return hash;
            }
        }

        public static ulong Add(ulong hash, string value)
        {
            string text = value ?? string.Empty;
            hash = Add(hash, text.Length);
            unchecked
            {
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= (byte)text[i];
                    hash *= Prime;
                    hash ^= (byte)(text[i] >> 8);
                    hash *= Prime;
                }
                return hash;
            }
        }

        public static string Hex(ulong hash) => hash.ToString("X16");
    }
}
