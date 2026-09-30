using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    public enum P3PerimeterRegion { Compound, Field }

    public sealed class P3PerimeterSegment
    {
        public P3PerimeterSegment(GridPoint cell, string sector, P3PerimeterRegion region, bool gate)
        {
            if (string.IsNullOrEmpty(sector)) throw new ArgumentException(nameof(sector));
            Cell = cell; Sector = sector; Region = region; Gate = gate;
        }
        public GridPoint Cell { get; }
        public string Sector { get; }
        public P3PerimeterRegion Region { get; }
        public bool Gate { get; }
    }

    public sealed class P3PerimeterObservation
    {
        public P3PerimeterObservation(GridPoint cell, int tick, bool breach, int damage)
        {
            Cell = cell; Tick = tick; Breach = breach; Damage = Math.Max(0, damage);
        }
        public GridPoint Cell { get; }
        public int Tick { get; }
        public bool Breach { get; }
        public int Damage { get; }
    }

    public sealed class P3SectorThreat
    {
        public P3SectorThreat(string sector, int score, int attacks, int breaches, int hostiles)
        {
            Sector = sector; Score = score; Attacks = attacks; Breaches = breaches; Hostiles = hostiles;
        }
        public string Sector { get; }
        public int Score { get; }
        public int Attacks { get; }
        public int Breaches { get; }
        public int Hostiles { get; }
    }

    // All inputs are observations at the current tick. A hostile merely standing
    // near a wall is weaker evidence than damage to that wall. Old impacts decay.
    public static class P3PerimeterThreat
    {
        public const int MemoryTicks = 900;
        public static IReadOnlyList<P3SectorThreat> Rank(int tick, IEnumerable<P3PerimeterSegment> segments,
            IEnumerable<P3PerimeterObservation> observations, IEnumerable<GridPoint> hostiles,
            string previousSector = null)
        {
            List<P3PerimeterSegment> perimeter = (segments ?? Enumerable.Empty<P3PerimeterSegment>()).ToList();
            List<P3PerimeterObservation> recent = (observations ?? Enumerable.Empty<P3PerimeterObservation>())
                .Where(o => o.Tick <= tick && tick - o.Tick <= MemoryTicks).ToList();
            List<GridPoint> enemies = (hostiles ?? Enumerable.Empty<GridPoint>()).ToList();
            return perimeter.GroupBy(s => s.Sector, StringComparer.Ordinal).Select(group =>
            {
                HashSet<GridPoint> cells = new HashSet<GridPoint>(group.Select(s => s.Cell));
                List<P3PerimeterObservation> hits = recent.Where(o => cells.Contains(o.Cell)).ToList();
                int breaches = hits.Count(o => o.Breach);
                int attacks = hits.Count(o => !o.Breach);
                int damage = hits.Where(o => !o.Breach).Sum(o => Math.Min(40, o.Damage));
                int nearby = enemies.Count(e => group.Any(s => DistanceSquared(e, s.Cell) <= 16 * 16));
                int fresh = hits.Sum(o => Math.Max(1, (o.Breach ? 40 : 18 + Math.Min(12, o.Damage / 5)) *
                    (MemoryTicks - (tick - o.Tick)) / MemoryTicks));
                int score = fresh + Math.Min(24, damage / 5) + Math.Min(12, nearby * 3) +
                    (group.Key == previousSector && (nearby > 0 || hits.Count > 0) ? 4 : 0);
                return new P3SectorThreat(group.Key, score, attacks, breaches, nearby);
            }).OrderByDescending(s => s.Score).ThenByDescending(s => s.Breaches)
                .ThenBy(s => s.Sector, StringComparer.Ordinal).ToList();
        }

        private static int DistanceSquared(GridPoint a, GridPoint b) =>
            (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
    }
}
