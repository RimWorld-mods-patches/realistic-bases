using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Ordinary night lighting, independent of sunlight and timed sun lamps.
    // Matches Core's glow flood: 100/141 path costs, an initial distance of 1,
    // native falloff and integer color truncation. One lamp must supply normal
    // working glow; overlapping weaker lights are not credited speculatively.
    public static class LayoutRoomLighting
    {
        public const double NormalWorkingGlow = 0.5;

        public static double GroundGlow(CatalogDef lamp, int distanceHundredths)
        {
            Facts(lamp, out double radius, out int strength);
            double distance = distanceHundredths / 100.0;
            if (radius <= 0 || strength <= 0 || distance <= 0 || distance > radius) return 0;
            double falloff = 0.6 * (1 - distance / radius) + 0.4 / (distance * distance);
            int channel = Math.Min(255, Math.Max(0, (int)(strength * falloff)));
            return Math.Min(NormalWorkingGlow, channel / 255.0 * 3.6);
        }

        public static IReadOnlyList<GridPoint> CoveredCells(GridRect interior, GridPoint source,
            CatalogDef lamp, IEnumerable<GridPoint> lightBlockers = null)
        {
            var blocked = new HashSet<GridPoint>(lightBlockers ?? Enumerable.Empty<GridPoint>());
            if (!interior.Contains(source) || blocked.Contains(source)) return new GridPoint[0];
            if (blocked.Count == 0)
                return interior.Points().Where(c => GroundGlow(lamp, Distance(source, c)) >= NormalWorkingGlow).ToArray();

            // Light floods around obstacles, rather than assuming a straight ray.
            var distance = interior.Points().ToDictionary(c => c, c => int.MaxValue);
            var pending = new HashSet<GridPoint> { source };
            distance[source] = 100;
            while (pending.Count > 0)
            {
                GridPoint here = pending.OrderBy(c => distance[c]).ThenBy(c => c).First();
                pending.Remove(here);
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        GridPoint next = new GridPoint(here.X + dx, here.Z + dz);
                        if (!interior.Contains(next) || blocked.Contains(next)) continue;
                        if (dx != 0 && dz != 0 && blocked.Contains(new GridPoint(here.X + dx, here.Z)) &&
                            blocked.Contains(new GridPoint(here.X, here.Z + dz))) continue;
                        int cost = distance[here] + (dx != 0 && dz != 0 ? 141 : 100);
                        if (cost >= distance[next]) continue;
                        distance[next] = cost;
                        pending.Add(next);
                    }
            }
            return interior.Points().Where(c => !blocked.Contains(c) && distance[c] != int.MaxValue &&
                GroundGlow(lamp, distance[c]) >= NormalWorkingGlow).ToArray();
        }

        public static IReadOnlyList<GridPoint> UnlitCells(TechCatalog catalog, LayoutRoom room)
        {
            var blockers = room.Fixtures.Where(f => catalog.TryGet(f.DefName, out CatalogDef def) && def.BlocksLight)
                .SelectMany(f => f.Cells).Where(room.Interior.Contains).ToArray();
            var covered = new HashSet<GridPoint>();
            foreach (LayoutFixture fixture in room.Fixtures.Where(f => f.Role == SettlementRole.Light))
                if (catalog.TryGet(fixture.DefName, out CatalogDef def))
                    foreach (GridPoint source in fixture.Cells)
                        covered.UnionWith(CoveredCells(room.Interior, source, def, blockers));
            return room.Interior.Points().Except(blockers).Except(covered).ToArray();
        }

        private static int Distance(GridPoint a, GridPoint b)
        {
            int x = Math.Abs(a.X - b.X), z = Math.Abs(a.Z - b.Z);
            return 100 + Math.Max(x, z) * 100 + Math.Min(x, z) * 41;
        }

        private static void Facts(CatalogDef def, out double radius, out int strength)
        {
            radius = Math.Min(40, def?.GlowRadius ?? 0);
            strength = def?.GlowStrength ?? 0;
            if (radius > 0 || strength > 0 || def == null) return;
            // Historical Core captures predate resolved glower facts. New live
            // catalogs always capture them; unknown custom lights get no credit.
            switch (def.DefName)
            {
                case "WallLamp": radius = 11; strength = 214; break;
                case "StandingLamp": radius = 12; strength = 214; break;
                case "TorchWallLamp": radius = 9; strength = 184; break;
                case "TorchLamp": radius = 10; strength = 184; break;
            }
        }
    }
}
