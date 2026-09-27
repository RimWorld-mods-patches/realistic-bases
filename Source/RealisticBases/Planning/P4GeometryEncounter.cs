using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // A P3 boundary expressed only in final geometry, independent of either
    // settlement plan type. All cells must come from the validated layout.
    public sealed class GeometryEncounterInput
    {
        public GeometryEncounterInput(string layoutDigest, EncounterProfile profile,
            IEnumerable<GridPoint> enclosure, IEnumerable<GridPoint> perimeterWalls,
            IEnumerable<KeyValuePair<GridPoint, GridPoint>> gates,
            IEnumerable<GridPoint> publicCirculation, IEnumerable<GridPoint> magazineCandidates,
            GridPoint? mortarCell = null, GridPoint? mortarInteraction = null)
        {
            LayoutDigest = layoutDigest ?? string.Empty;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Enclosure = Copy(enclosure);
            PerimeterWalls = Copy(perimeterWalls);
            Gates = new ReadOnlyCollection<KeyValuePair<GridPoint, GridPoint>>((gates ??
                Enumerable.Empty<KeyValuePair<GridPoint, GridPoint>>()).ToList());
            PublicCirculation = Copy(publicCirculation);
            MagazineCandidates = Copy(magazineCandidates);
            MortarCell = mortarCell;
            MortarInteraction = mortarInteraction;
        }

        public string LayoutDigest { get; }
        public EncounterProfile Profile { get; }
        public IReadOnlyList<GridPoint> Enclosure { get; }
        public IReadOnlyList<GridPoint> PerimeterWalls { get; }
        // Key is gate door; value is its inside endpoint.
        public IReadOnlyList<KeyValuePair<GridPoint, GridPoint>> Gates { get; }
        public IReadOnlyList<GridPoint> PublicCirculation { get; }
        public IReadOnlyList<GridPoint> MagazineCandidates { get; }
        public GridPoint? MortarCell { get; }
        public GridPoint? MortarInteraction { get; }

        private static IReadOnlyList<GridPoint> Copy(IEnumerable<GridPoint> points) =>
            new ReadOnlyCollection<GridPoint>((points ?? Enumerable.Empty<GridPoint>()).Distinct().OrderBy(c => c).ToList());
    }

    public static class GeometryEncounterPlanner
    {
        private static readonly GridPoint[] Deltas =
        {
            new GridPoint(0, -1), new GridPoint(1, 0), new GridPoint(0, 1), new GridPoint(-1, 0)
        };

        public static EncounterPlan Plan(GeometryEncounterInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            HashSet<GridPoint> publicCells = new HashSet<GridPoint>(input.PublicCirculation);
            HashSet<GridPoint> enclosed = new HashSet<GridPoint>(input.Enclosure);
            HashSet<GridPoint> walls = new HashSet<GridPoint>(input.PerimeterWalls);
            if (enclosed.Count == 0 || publicCells.Count == 0 || input.Gates.Count == 0 ||
                publicCells.Any(c => !enclosed.Contains(c)))
                throw new InvalidOperationException("P3 geometry has no valid enclosed public circulation");
            if (input.Gates.Any(g => !publicCells.Contains(g.Value) || !Adjacent(g.Key, g.Value)))
                throw new InvalidOperationException("P3 gate endpoint is not public and adjacent");
            if (input.Profile.MortarCapable != input.MortarCell.HasValue ||
                input.Profile.MortarCapable != input.MortarInteraction.HasValue)
                throw new InvalidOperationException("P3 mortar capability and court disagree");
            if (input.MortarInteraction.HasValue && !publicCells.Contains(input.MortarInteraction.Value))
                throw new InvalidOperationException("P3 mortar interaction is not on public circulation");

            List<DefensivePost> posts = new List<DefensivePost>();
            HashSet<GridPoint> used = new HashSet<GridPoint>();
            for (int i = 0; i < input.Gates.Count; i++)
            {
                GridPoint gate = input.Gates[i].Key;
                GridPoint inside = input.Gates[i].Value;
                foreach (GridPoint candidate in publicCells.OrderBy(c => Distance(c, inside)).ThenBy(c => c))
                {
                    if (Distance(candidate, inside) > 2 || !used.Add(candidate)) continue;
                    posts.Add(Post("gate-" + i + "-" + posts.Count, Sector(gate, enclosed), candidate, true));
                    if (posts.Count(p => p.Id.StartsWith("gate-" + i + "-", StringComparison.Ordinal)) == 2) break;
                }
            }
            if (posts.Count < input.Profile.RequiredGatePosts)
                throw new InvalidOperationException("P3 geometry lacks gate defense posts");

            List<GridPoint> ranged = publicCells.Where(c => !used.Contains(c) && Deltas.Any(d =>
                walls.Contains(new GridPoint(c.X + d.X, c.Z + d.Z))))
                .OrderBy(c => Distance(c, input.Gates[0].Value)).ThenBy(c => c).ToList();
            int next = 0;
            while (next < input.Profile.RequiredRangedPosts && ranged.Count > 0)
            {
                // Spread posts over the perimeter instead of clustering at one gate.
                GridPoint cell = next == 0 ? ranged[0] : ranged.OrderByDescending(c =>
                    posts.Where(p => !p.Mortar).Min(p => Distance(c, p.Cell))).ThenBy(c => c).First();
                ranged.Remove(cell);
                used.Add(cell);
                posts.Add(Post("ranged-" + next, Sector(cell, enclosed), cell, false));
                next++;
            }
            if (next < input.Profile.RequiredRangedPosts)
                throw new InvalidOperationException("P3 geometry lacks ranged perimeter posts");

            GridPoint center = Center(input.Enclosure);
            GridPoint support = publicCells.OrderBy(c => Distance(c, center)).ThenBy(c => c).First();
            GridPoint fallback = publicCells.Where(c => !used.Contains(c))
                .OrderBy(c => Distance(c, support)).ThenBy(c => c).FirstOrDefault();
            if (!publicCells.Contains(fallback)) fallback = support;
            GridPoint magazine = input.MagazineCandidates.FirstOrDefault(c => enclosed.Contains(c));
            if (!enclosed.Contains(magazine)) throw new InvalidOperationException("P3 geometry lacks an enclosed magazine");
            if (input.MortarCell.HasValue)
                posts.Add(new DefensivePost("mortar", "center", input.MortarInteraction.Value,
                    0, 100, false, true, true, true, false, true));
            List<CombatSupply> supplies = new List<CombatSupply>();
            if (input.Profile.MortarCapable)
            {
                supplies.Add(new CombatSupply("Shell_HighExplosive", input.Profile.HeShells, StockPurpose.OperatingReserve,
                    "declared firing duty"));
                if (input.Profile.IncendiaryShells > 0)
                    supplies.Add(new CombatSupply("Shell_Incendiary", input.Profile.IncendiaryShells, StockPurpose.OperatingReserve,
                        "bounded contingency"));
                supplies.Add(new CombatSupply("ReinforcedBarrel", input.Profile.ReinforcedBarrels, StockPurpose.OperatingReserve,
                    "non-classic mortar reserve"));
            }
            int minX = enclosed.Min(c => c.X), maxX = enclosed.Max(c => c.X);
            int minZ = enclosed.Min(c => c.Z), maxZ = enclosed.Max(c => c.Z);
            return new EncounterPlan(input.LayoutDigest, input.Profile, posts,
                new GridRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1),
                support, fallback, input.MortarCell ?? new GridPoint(-1, -1),
                input.MortarInteraction ?? new GridPoint(-1, -1), magazine, supplies, input.Enclosure);
        }

        private static DefensivePost Post(string id, string sector, GridPoint cell, bool gate) =>
            new DefensivePost(id, sector, cell, gate ? 0 : 10, gate ? 4 : 38,
                !gate, true, true, false, true, true);

        private static bool Adjacent(GridPoint a, GridPoint b) => Distance(a, b) == 1;
        private static int Distance(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);
        private static GridPoint Center(IEnumerable<GridPoint> cells) =>
            new GridPoint((int)Math.Round(cells.Average(c => c.X)), (int)Math.Round(cells.Average(c => c.Z)));

        private static string Sector(GridPoint cell, HashSet<GridPoint> enclosure)
        {
            GridPoint center = Center(enclosure);
            int dx = cell.X - center.X, dz = cell.Z - center.Z;
            // RimWorld's positive Z points north, matching the live P3 threat-sector calculation.
            return Math.Abs(dx) > Math.Abs(dz) ? (dx < 0 ? "west" : "east") : (dz < 0 ? "south" : "north");
        }
    }
}
