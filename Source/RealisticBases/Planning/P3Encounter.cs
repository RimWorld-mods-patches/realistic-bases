using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public enum StockPurpose
    {
        OperatingReserve,
        TradeGoods
    }

    public sealed class CombatSupply
    {
        public CombatSupply(string defName, int count, StockPurpose purpose, string reason)
        {
            if (string.IsNullOrWhiteSpace(defName) || count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            DefName = defName;
            Count = count;
            Purpose = purpose;
            Reason = reason ?? string.Empty;
        }

        public string DefName { get; }
        public int Count { get; }
        public StockPurpose Purpose { get; }
        public string Reason { get; }
    }

    public sealed class EncounterProfile
    {
        public EncounterProfile(string id, bool mortarCapable, int combatHours, int mortarShotsPerHour,
            int heShells, int incendiaryShells, int reinforcedBarrels, int requiredRangedPosts,
            int requiredGatePosts, int mortarOperators, int supportWorkers, int addedContinuousPowerWatts)
        {
            if (string.IsNullOrWhiteSpace(id) || combatHours <= 0 || mortarShotsPerHour < 0 || heShells < 0 ||
                incendiaryShells < 0 || reinforcedBarrels < 0 || requiredRangedPosts <= 0 || requiredGatePosts <= 0 ||
                mortarOperators < 0 || supportWorkers <= 0 || addedContinuousPowerWatts < 0)
                throw new ArgumentOutOfRangeException();
            Id = id;
            MortarCapable = mortarCapable;
            CombatHours = combatHours;
            MortarShotsPerHour = mortarShotsPerHour;
            HeShells = heShells;
            IncendiaryShells = incendiaryShells;
            ReinforcedBarrels = reinforcedBarrels;
            RequiredRangedPosts = requiredRangedPosts;
            RequiredGatePosts = requiredGatePosts;
            MortarOperators = mortarOperators;
            SupportWorkers = supportWorkers;
            AddedContinuousPowerWatts = addedContinuousPowerWatts;
            if (mortarCapable)
            {
                if (mortarOperators < 1 || HeShells < DeclaredMortarShots || reinforcedBarrels < 1)
                    throw new ArgumentException("Mortar profile does not cover declared firing duty.");
            }
            else if (mortarOperators != 0 || heShells != 0 || incendiaryShells != 0 || reinforcedBarrels != 0)
                throw new ArgumentException("Mortar-incapable profile contains mortar supplies.");
        }

        public string Id { get; }
        public bool MortarCapable { get; }
        public int CombatHours { get; }
        public int MortarShotsPerHour { get; }
        public int HeShells { get; }
        public int IncendiaryShells { get; }
        public int ReinforcedBarrels { get; }
        public int RequiredRangedPosts { get; }
        public int RequiredGatePosts { get; }
        public int MortarOperators { get; }
        public int SupportWorkers { get; }
        public int AddedContinuousPowerWatts { get; }
        public int DeclaredMortarShots => CombatHours * MortarShotsPerHour;
    }

    public static class P3EncounterProfiles
    {
        public static readonly EncounterProfile Industrial = new EncounterProfile(
            "industrial-encounter-v1", true, 6, 2, 12, 2, 2, 4, 2, 1, 1, 0);

        public static readonly EncounterProfile Tribal = new EncounterProfile(
            "tribal-encounter-v1", false, 6, 0, 0, 0, 0, 4, 2, 0, 1, 0);

        public static readonly EncounterProfile IndustrialNoMortar = new EncounterProfile(
            "industrial-no-mortar-v1", false, 6, 0, 0, 0, 0, 4, 2, 0, 1, 0);

        public static EncounterProfile For(PhysicalSettlementProfile profile) =>
            profile.Has(SettlementCapability.Electricity) ? Industrial : Tribal;
    }

    public sealed class EncounterPlan
    {
        public EncounterPlan(string basePlanDigest, EncounterProfile profile, IEnumerable<DefensivePost> posts,
            GridRect defendedBounds, GridPoint supportCenter, GridPoint innerFallback, GridPoint mortarCell,
            GridPoint mortarInteractionCell, GridPoint magazineCell, IEnumerable<CombatSupply> supplies,
            IEnumerable<GridPoint> defendedCells)
        {
            BasePlanDigest = basePlanDigest ?? string.Empty;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Posts = new ReadOnlyCollection<DefensivePost>((posts ?? Enumerable.Empty<DefensivePost>()).ToList());
            DefendedBounds = defendedBounds;
            DefendedCells = new ReadOnlyCollection<GridPoint>((defendedCells ?? defendedBounds.Points()).Distinct().OrderBy(c => c).ToList());
            SupportCenter = supportCenter;
            InnerFallback = innerFallback;
            MortarCell = mortarCell;
            MortarInteractionCell = mortarInteractionCell;
            MagazineCell = magazineCell;
            Supplies = new ReadOnlyCollection<CombatSupply>((supplies ?? Enumerable.Empty<CombatSupply>()).ToList());
            Digest = ComputeDigest();
        }

        public string BasePlanDigest { get; }
        public EncounterProfile Profile { get; }
        public IReadOnlyList<DefensivePost> Posts { get; }
        public GridRect DefendedBounds { get; }
        // Do not use DefendedBounds as movement authority. It remains a compact
        // diagnostic envelope for older saved controllers; this exact set is the
        // authority for P4 terrain-following layouts.
        public IReadOnlyList<GridPoint> DefendedCells { get; }
        public GridPoint SupportCenter { get; }
        public GridPoint InnerFallback { get; }
        public GridPoint MortarCell { get; }
        public GridPoint MortarInteractionCell { get; }
        public GridPoint MagazineCell { get; }
        public IReadOnlyList<CombatSupply> Supplies { get; }
        public string Digest { get; }

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p3-encounter-plan-v1");
            hash = StableHash.Add(hash, BasePlanDigest);
            hash = StableHash.Add(hash, Profile.Id);
            hash = StableHash.Add(hash, Profile.CombatHours);
            hash = StableHash.Add(hash, Profile.MortarShotsPerHour);
            foreach (DefensivePost post in Posts.OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                hash = StableHash.Add(hash, post.Id);
                hash = StableHash.Add(hash, post.SectorId);
                if (post.SupportedSectors.Count > 1)
                {
                    hash = StableHash.Add(hash, "supported-fronts");
                    foreach (string sector in post.SupportedSectors) hash = StableHash.Add(hash, sector);
                }
                hash = StableHash.Add(hash, post.Cell.X);
                hash = StableHash.Add(hash, post.Cell.Z);
                hash = StableHash.Add(hash, post.Mortar ? 1 : 0);
                hash = StableHash.Add(hash, post.Inner ? 1 : 0);
            }
            foreach (CombatSupply supply in Supplies.OrderBy(s => s.DefName, StringComparer.Ordinal))
            {
                hash = StableHash.Add(hash, supply.DefName);
                hash = StableHash.Add(hash, supply.Count);
                hash = StableHash.Add(hash, (int)supply.Purpose);
            }
            foreach (GridPoint cell in DefendedCells) { hash = StableHash.Add(hash, cell.X); hash = StableHash.Add(hash, cell.Z); }
            return StableHash.Hex(hash);
        }
    }

    public static class EncounterPlanner
    {
        public static EncounterPlan Plan(PhysicalSettlementPlan settlement, PhysicalSettlementProfile physicalProfile,
            TerrainSnapshot terrain)
        {
            if (settlement == null || physicalProfile == null || terrain == null) throw new ArgumentNullException();
            EncounterProfile profile = P3EncounterProfiles.For(physicalProfile);
            GridRect c = settlement.Compound;
            bool terrainFollowing = settlement.Archetype == TerrainArchetype.ExcavatedMountain || settlement.Archetype == TerrainArchetype.Foothill;
            List<DefensivePost> posts = terrainFollowing
                ? TerrainFollowingPosts(settlement, terrain)
                : new List<DefensivePost>
            {
                Post("north-west", "north", c.MinX + 11, c.MinZ - 1, 10, 38, false),
                Post("north-east", "north", c.MinX + 19, c.MinZ - 1, 10, 38, false),
                Post("south-west", "south", c.MinX + 11, c.MaxZ + 1, 10, 38, false),
                Post("south-east", "south", c.MinX + 19, c.MaxZ + 1, 10, 38, false),
                Post("west-center", "west", c.MinX - 1, c.MinZ + 12, 10, 38, false),
                Post("east-center", "east", c.MaxX + 1, c.MinZ + 12, 10, 38, false),
                Post("north-gate", "north", c.MinX + 15, c.MinZ + 1, 0, 4, false),
                Post("south-gate", "south", c.MinX + 15, c.MaxZ - 1, 0, 4, false),
                Post("inner-west", "inner", c.MinX + 12, c.MinZ + 9, 4, 24, true),
                Post("inner-east", "inner", c.MinX + 18, c.MinZ + 9, 4, 24, true)
            };
            GridPoint mortar = profile.MortarCapable ? (terrainFollowing
                ? TerrainFollowingMortarCourt(settlement, terrain) : new GridPoint(c.MinX + 26, c.MinZ + 20)) : new GridPoint(-1, -1);
            GridPoint mortarInteraction = profile.MortarCapable ? new GridPoint(mortar.X, mortar.Z - 1) : new GridPoint(-1, -1);
            if (profile.MortarCapable)
                posts.Add(new DefensivePost("mortar", "center", mortarInteraction, 0, 100, false, true, true, true, false, true));
            PhysicalFacility storage = settlement.Facilities.Single(f => f.Kind == FacilityKind.BulkStorage);
            GridPoint magazine = new GridPoint(storage.Bounds.MinX + 1, storage.Bounds.MinZ + 1);
            List<CombatSupply> supplies = new List<CombatSupply>();
            if (profile.MortarCapable)
            {
                supplies.Add(new CombatSupply("Shell_HighExplosive", profile.HeShells, StockPurpose.OperatingReserve,
                    profile.DeclaredMortarShots + " declared HE shots over " + profile.CombatHours + " game hours"));
                supplies.Add(new CombatSupply("Shell_Incendiary", profile.IncendiaryShells, StockPurpose.OperatingReserve,
                    "bounded contingency; fire-risk gate required"));
                supplies.Add(new CombatSupply("ReinforcedBarrel", profile.ReinforcedBarrels, StockPurpose.OperatingReserve,
                    "spawned only when active difficulty uses mortar barrels"));
            }
            GridRect envelope = new GridRect(c.MinX - 2, c.MinZ - 2, c.Width + 4, c.Height + 4);
            IEnumerable<GridPoint> controlled = terrainFollowing
                ? c.Points().Concat(posts.Select(p => p.Cell))
                : envelope.Points();
            EncounterPlan result = new EncounterPlan(settlement.Digest, profile, posts,
                envelope,
                new GridPoint(c.MinX + 15, c.MinZ + 12), new GridPoint(c.MinX + 15, c.MinZ + 9),
                mortar, mortarInteraction, magazine, supplies, controlled);
            IReadOnlyList<string> errors = EncounterValidator.Validate(settlement, physicalProfile, terrain, result);
            if (errors.Count > 0) throw new InvalidOperationException("Invalid P3 encounter plan: " + string.Join(",", errors));
            return result;
        }

        private static DefensivePost Post(string id, string sector, int x, int z, int minRange, int maxRange, bool inner) =>
            new DefensivePost(id, sector, new GridPoint(x, z), minRange, maxRange, true, true, true, false, inner, true);

        private static List<DefensivePost> TerrainFollowingPosts(PhysicalSettlementPlan settlement, TerrainSnapshot terrain)
        {
            GridRect c = settlement.Compound;
            GridPoint[] desired =
            {
                new GridPoint(c.MinX + 11, c.MinZ + 1), new GridPoint(c.MinX + 19, c.MinZ + 1),
                new GridPoint(c.MinX + 11, c.MaxZ - 1), new GridPoint(c.MinX + 19, c.MaxZ - 1),
                new GridPoint(c.MinX + 2, c.MinZ + 12), new GridPoint(c.MaxX - 2, c.MinZ + 12),
                new GridPoint(c.MinX + 15, c.MinZ + 1), new GridPoint(c.MinX + 15, c.MaxZ - 1),
                new GridPoint(c.MinX + 12, c.MinZ + 9), new GridPoint(c.MinX + 18, c.MinZ + 9)
            };
            string[] ids = { "north-west", "north-east", "south-west", "south-east", "west-center", "east-center", "north-gate", "south-gate", "inner-west", "inner-east" };
            string[] sectors = { "north", "north", "south", "south", "west", "east", "north", "south", "inner", "inner" };
            HashSet<GridPoint> used = new HashSet<GridPoint>();
            List<DefensivePost> result = new List<DefensivePost>();
            for (int i = 0; i < ids.Length; i++)
            {
                GridPoint cell = settlement.Logistics.Where(p => !used.Contains(p) && IsOpenPostCell(terrain, p))
                    .OrderBy(p => Distance(p, desired[i])).ThenBy(p => p).FirstOrDefault();
                if (!settlement.Logistics.Contains(cell) || used.Contains(cell))
                    throw new InvalidOperationException("Terrain-following P3 post has no open logistics cell: " + ids[i]);
                used.Add(cell);
                bool gate = i == 6 || i == 7;
                bool inner = i >= 8;
                result.Add(new DefensivePost(ids[i], sectors[i], cell, gate ? 0 : inner ? 4 : 10,
                    gate ? 4 : inner ? 24 : 38, true, true, true, false, inner, true));
            }
            return result;
        }

        private static GridPoint TerrainFollowingMortarCourt(PhysicalSettlementPlan settlement, TerrainSnapshot terrain)
        {
            HashSet<GridPoint> logistics = new HashSet<GridPoint>(settlement.Logistics);
            foreach (GridPoint origin in settlement.Logistics.OrderByDescending(p => p.X).ThenByDescending(p => p.Z))
            {
                GridPoint[] footprint = { origin, new GridPoint(origin.X + 1, origin.Z),
                    new GridPoint(origin.X, origin.Z + 1), new GridPoint(origin.X + 1, origin.Z + 1) };
                GridPoint interaction = new GridPoint(origin.X, origin.Z - 1);
                if (footprint.All(p => logistics.Contains(p) && IsOpenHeavyCell(terrain, p)) &&
                    logistics.Contains(interaction) && IsOpenPostCell(terrain, interaction)) return origin;
            }
            throw new InvalidOperationException("Terrain-following P3 mortar court has no open heavy logistics footprint");
        }

        private static bool IsOpenPostCell(TerrainSnapshot terrain, GridPoint cell) => terrain.InBounds(cell) &&
            terrain[cell].Has(TerrainTraits.Standable) && !terrain[cell].HasAny(TerrainTraits.Roofed | TerrainTraits.Water | TerrainTraits.Occupied);

        private static bool IsOpenHeavyCell(TerrainSnapshot terrain, GridPoint cell) => IsOpenPostCell(terrain, cell) &&
            terrain[cell].Has(TerrainTraits.SupportsHeavy);

        private static int Distance(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);
    }

    public static class EncounterValidator
    {
        public static IReadOnlyList<string> Validate(PhysicalSettlementPlan settlement,
            PhysicalSettlementProfile physicalProfile, TerrainSnapshot terrain, EncounterPlan encounter)
        {
            List<string> errors = new List<string>();
            if (encounter.BasePlanDigest != settlement.Digest) errors.Add("base_plan_digest");
            if (encounter.Posts.GroupBy(p => p.Id).Any(g => g.Count() > 1)) errors.Add("duplicate_post");
            if (encounter.Posts.Count(p => !p.Inner && !p.Mortar && p.MinimumRange > 0) < encounter.Profile.RequiredRangedPosts)
                errors.Add("ranged_post_capacity");
            if (encounter.Posts.Count(p => !p.Inner && !p.Mortar && p.MaximumRange <= 4) < encounter.Profile.RequiredGatePosts)
                errors.Add("gate_post_capacity");
            foreach (DefensivePost post in encounter.Posts.Where(p => !p.Mortar))
            {
                if (!terrain.InBounds(post.Cell) || !terrain[post.Cell].Has(TerrainTraits.Standable) || !post.IsValid)
                    errors.Add("post_invalid_" + post.Id);
                if (settlement.Perimeter.Contains(post.Cell)) errors.Add("post_on_opaque_wall_" + post.Id);
                if (!encounter.DefendedCells.Contains(post.Cell)) errors.Add("post_outside_controlled_geometry_" + post.Id);
            }
            if (encounter.Profile.MortarCapable)
            {
                GridPoint[] footprint = { encounter.MortarCell,
                    new GridPoint(encounter.MortarCell.X + 1, encounter.MortarCell.Z),
                    new GridPoint(encounter.MortarCell.X, encounter.MortarCell.Z + 1),
                    new GridPoint(encounter.MortarCell.X + 1, encounter.MortarCell.Z + 1) };
                if (footprint.Any(c => !terrain.InBounds(c) || !terrain[c].Has(TerrainTraits.Standable | TerrainTraits.SupportsHeavy) ||
                    terrain[c].Has(TerrainTraits.Roofed))) errors.Add("mortar_terrain");
                if (!terrain.InBounds(encounter.MortarInteractionCell) || !terrain[encounter.MortarInteractionCell].Has(TerrainTraits.Standable))
                    errors.Add("mortar_interaction");
                if (settlement.Facilities.Where(f => f.Roofed).Any(f => footprint.Any(f.Bounds.Contains))) errors.Add("mortar_roof_overlap");
                if (Math.Abs(encounter.MagazineCell.X - encounter.MortarCell.X) + Math.Abs(encounter.MagazineCell.Z - encounter.MortarCell.Z) > 40)
                    errors.Add("magazine_range");
                if (encounter.Supplies.Where(s => s.DefName == "Shell_HighExplosive").Sum(s => s.Count) < encounter.Profile.DeclaredMortarShots)
                    errors.Add("he_duty_shortfall");
                if (encounter.Supplies.Any(s => s.Purpose != StockPurpose.OperatingReserve)) errors.Add("trade_stock_in_p3");
                if (encounter.Profile.AddedContinuousPowerWatts != 0) errors.Add("mortar_power_model");
            }
            else if (encounter.Posts.Any(p => p.Mortar) || encounter.Supplies.Any() || encounter.MortarCell.X >= 0)
                errors.Add("tribal_mortar_content");
            return new ReadOnlyCollection<string>(errors.Distinct().ToList());
        }
    }
}
