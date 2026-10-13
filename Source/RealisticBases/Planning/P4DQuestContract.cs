using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class QuestPartFacts
    {
        public QuestPartFacts(string name, bool hidden = false) { Name = name; Hidden = hidden; }
        public string Name { get; }
        public bool Hidden { get; }
    }

    // A copy of the actual native roster's facts. Runtime Pawn references never
    // enter the planner, and these items are descriptive, not creation requests.
    public sealed class QuestResidentFacts
    {
        public QuestResidentFacts(string id, string kind, LoadoutRole role, LoadoutItem weapon,
            IEnumerable<LoadoutItem> apparel = null)
        {
            Id = id; Kind = kind; Role = role; Weapon = weapon;
            Apparel = Array.AsReadOnly((apparel ?? Enumerable.Empty<LoadoutItem>()).ToArray());
        }
        public string Id { get; }
        public string Kind { get; }
        public LoadoutRole Role { get; }
        public LoadoutItem Weapon { get; }
        public IReadOnlyList<LoadoutItem> Apparel { get; }
    }

    public sealed class QuestSiteFacts
    {
        public QuestSiteFacts(int siteId, string faction, string generator, IEnumerable<QuestPartFacts> parts,
            double threatPoints, int pawnGroupSeed, double lootValue, int expectedCount,
            IEnumerable<QuestResidentFacts> roster, int lootStacks, string prisonerId = null,
            bool prisonerHeld = false, bool nativeDefinitions = true, bool caravanArrival = true,
            int turretsCount = 0, int mortarsCount = 0)
        {
            SiteId = siteId; Faction = faction; Generator = generator;
            Parts = Array.AsReadOnly(parts.ToArray()); ThreatPoints = threatPoints;
            PawnGroupSeed = pawnGroupSeed; LootValue = lootValue; ExpectedCount = expectedCount;
            Roster = Array.AsReadOnly(roster.ToArray()); LootStacks = lootStacks;
            PrisonerId = prisonerId; PrisonerHeld = prisonerHeld;
            NativeDefinitions = nativeDefinitions; CaravanArrival = caravanArrival;
            TurretsCount = turretsCount; MortarsCount = mortarsCount;
        }
        public int SiteId { get; }
        public string Faction { get; }
        public string Generator { get; }
        public IReadOnlyList<QuestPartFacts> Parts { get; }
        public double ThreatPoints { get; }
        public int PawnGroupSeed { get; }
        public double LootValue { get; }
        // Native hint, not the authoritative roster count.
        public int ExpectedCount { get; }
        public IReadOnlyList<QuestResidentFacts> Roster { get; }
        public int LootStacks { get; }
        public string PrisonerId { get; }
        public bool PrisonerHeld { get; }
        public bool NativeDefinitions { get; }
        public bool CaravanArrival { get; }
        // The native Turrets part's advertised mini turrets and mortars.
        public int TurretsCount { get; }
        public int MortarsCount { get; }
    }

    public interface IQuestLayoutStrategy
    {
        string Id { get; }
        bool Matches(QuestSiteFacts site);
        int PrisonerCapacity { get; }
        string ValidateObjective(QuestSiteFacts site);
        // Advertised static defense this strategy builds exactly.
        int Turrets(QuestSiteFacts site);
        int Mortars(QuestSiteFacts site);
    }

    public sealed class BanditEliminationLayoutStrategy : IQuestLayoutStrategy
    {
        public string Id => "core-bandit-v1";
        public int PrisonerCapacity => 0;
        public bool Matches(QuestSiteFacts site) => site.Parts.Select(p => p.Name).SequenceEqual(new[] { "BanditCamp" });
        public string ValidateObjective(QuestSiteFacts site) => site.PrisonerId == null ? null : "unexpected_protected_target";
        public int Turrets(QuestSiteFacts site) => 0;
        public int Mortars(QuestSiteFacts site) => 0;
    }

    public sealed class GuardedRescueLayoutStrategy : IQuestLayoutStrategy
    {
        public string Id => "core-guarded-rescue-v1";
        public int PrisonerCapacity => 1;
        public bool Matches(QuestSiteFacts site) => site.Parts.Select(p => p.Name).SequenceEqual(new[]
            { "PrisonerWillingToJoin", "Outpost", "PossibleUnknownThreatMarker" });
        public string ValidateObjective(QuestSiteFacts site) =>
            string.IsNullOrEmpty(site.PrisonerId) || !site.PrisonerHeld ? "missing_original_prisoner_holder" :
            site.Roster.Any(p => p.Id == site.PrisonerId) ? "protected_target_in_roster" : null;
        public int Turrets(QuestSiteFacts site) => 0;
        public int Mortars(QuestSiteFacts site) => 0;
    }

    // Core rescue guarded by the native Turrets part: exactly the advertised
    // mini turrets and mortars, one guard and one crew per mortar.
    public sealed class TurretRescueLayoutStrategy : IQuestLayoutStrategy
    {
        public const int MinimumTurrets = 2, MaximumTurrets = 11, MaximumMortars = 1;
        public string Id => "core-turret-rescue-v1";
        public int PrisonerCapacity => 1;
        public bool Matches(QuestSiteFacts site) => site.Parts.Select(p => p.Name).SequenceEqual(new[]
            { "PrisonerWillingToJoin", "Turrets", "PossibleUnknownThreatMarker" });
        public string ValidateObjective(QuestSiteFacts site) =>
            site.Faction != "Pirate" && site.Faction != "OutlanderRough" ? "unsupported_turret_faction" :
            site.TurretsCount < MinimumTurrets || site.TurretsCount > MaximumTurrets ||
                site.MortarsCount < 0 || site.MortarsCount > MaximumMortars ? "unsupported_advertised_guns" :
            site.Roster.Count != site.MortarsCount + 1 ? "native_guard_and_crew_count" :
            string.IsNullOrEmpty(site.PrisonerId) || !site.PrisonerHeld ? "missing_original_prisoner_holder" :
            site.Roster.Any(p => p.Id == site.PrisonerId) ? "protected_target_in_roster" : null;
        public int Turrets(QuestSiteFacts site) => site.TurretsCount;
        public int Mortars(QuestSiteFacts site) => site.MortarsCount;
    }

    public sealed class P4DQuestContract
    {
        public const string Version = "p4d-quest-contract.1";
        // Explicit bounded support, independent of the ordinary 4–12 policy.
        public const int MaximumRoster = 32;
        private static readonly IQuestLayoutStrategy[] Strategies =
            { new BanditEliminationLayoutStrategy(), new GuardedRescueLayoutStrategy(), new TurretRescueLayoutStrategy() };
        private static readonly string[] Factions = { "Pirate", "OutlanderRough", "TribeRough", "TribeSavage" };

        private P4DQuestContract(QuestSiteFacts site, IQuestLayoutStrategy strategy)
        {
            Site = site; Strategy = strategy;
            ulong hash = StableHash.Add(StableHash.Start, Version);
            hash = StableHash.Add(hash, site.SiteId);
            hash = StableHash.Add(hash, site.PawnGroupSeed);
            Seed = LayoutSeed.Sub(hash, strategy.Id);
            EquipmentValue = site.Roster.Sum(r => r.Apparel.Concat(r.Weapon == null
                ? Enumerable.Empty<LoadoutItem>() : new[] { r.Weapon }).Sum(i => i.Value));
            // Declared provisional quest-derived allowances. Static defense is
            // only what the quest advertises; no traps or unfunded operators.
            ConstructionImportAllowance = checked((int)Math.Ceiling(site.ThreatPoints * 12));
            OperatingValueAllowance = checked((int)Math.Ceiling(site.ThreatPoints * 2));
            ulong digest = StableHash.Add(Seed, site.Faction);
            digest = StableHash.Add(digest, site.ThreatPoints.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            digest = StableHash.Add(digest, site.LootValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            digest = StableHash.Add(digest, site.PrisonerId ?? string.Empty);
            digest = StableHash.Add(digest, site.LootStacks);
            if (QuestTurrets + QuestMortars > 0)
            {
                digest = StableHash.Add(digest, QuestTurrets);
                digest = StableHash.Add(digest, QuestMortars);
            }
            foreach (QuestResidentFacts r in site.Roster)
            {
                digest = StableHash.Add(digest, r.Id); digest = StableHash.Add(digest, r.Kind);
                digest = StableHash.Add(digest, (int)r.Role);
                foreach (LoadoutItem item in r.Apparel.Concat(r.Weapon == null ? Enumerable.Empty<LoadoutItem>() : new[] { r.Weapon }))
                    digest = item.AddTo(digest);
            }
            Digest = StableHash.Hex(digest);
        }

        public QuestSiteFacts Site { get; }
        public IQuestLayoutStrategy Strategy { get; }
        public ulong Seed { get; }
        public string Digest { get; }
        public int EquipmentValue { get; }
        public int ConstructionImportAllowance { get; }
        public int OperatingValueAllowance { get; }
        public int QuestTurrets => Strategy.Turrets(Site);
        public int QuestMortars => Strategy.Mortars(Site);
        // Native puts one stack of 5–8 shells beside each manned mortar; the
        // count is fixed per contract so generation replays.
        public int QuestMortarShells => QuestMortars == 0 ? 0 :
            QuestMortars * (5 + (int)(LayoutSeed.Sub(Seed, "native-mortar-shells") % 4UL));
        // Static threat beyond the native roster: only what the quest advertises.
        public int AddedStaticThreatPoints => QuestTurrets + QuestMortars == 0 ? 0 : (int)Math.Ceiling(Site.ThreatPoints);
        // A finite three-simple-meal equivalent per original occupant, not
        // three item units: pemmican and meals have different nutrition.
        public int StartupFoodUnits(double nutritionPerUnit)
        {
            if (!Finite(nutritionPerUnit) || nutritionPerUnit <= 0)
                throw new ArgumentOutOfRangeException(nameof(nutritionPerUnit));
            return checked((int)Math.Ceiling((Site.Roster.Count + Strategy.PrisonerCapacity) * 2.7 / nutritionPerUnit - 0.000001));
        }

        public static bool TryCreate(QuestSiteFacts site, out P4DQuestContract contract, out string reason)
        {
            contract = null; reason = null;
            if (site == null) reason = "missing_site";
            else if (!site.NativeDefinitions || site.Generator != "Encounter" || !site.CaravanArrival)
                reason = "unsupported_generator_or_arrival";
            else if (!Factions.Contains(site.Faction)) reason = "unsupported_faction";
            else if (site.Parts.Any(p => p.Hidden)) reason = "unsupported_hidden_part";
            else if (!Finite(site.ThreatPoints) || site.ThreatPoints <= 0 || site.ThreatPoints > 100000 ||
                !Finite(site.LootValue) || site.LootValue < 0 || site.LootStacks < 0)
                reason = "invalid_native_parameters";
            else if (site.Roster.Count == 0 || site.Roster.Count > MaximumRoster)
                reason = "native_roster_capacity";
            else if (site.Roster.Any(r => string.IsNullOrEmpty(r.Id) || string.IsNullOrEmpty(r.Kind)) ||
                site.Roster.Select(r => r.Id).Distinct().Count() != site.Roster.Count)
                reason = "invalid_native_roster_identity";
            else
            {
                IQuestLayoutStrategy strategy = Strategies.FirstOrDefault(s => s.Matches(site));
                if (strategy == null) reason = "unsupported_composition";
                else if ((reason = strategy.ValidateObjective(site)) == null)
                    contract = new P4DQuestContract(site, strategy);
            }
            return contract != null;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // The advertised guns are the quest's own content: their power, and
        // their construction, loaded fuel and cover value on top of the camp
        // allowance, come from the catalog.
        public LayoutRequirements Requirements(SettlementTechStage stage, LayoutClimate climate, TechCatalog catalog = null)
        {
            int gunWatts = 0, gunValue = 0;
            if (QuestTurrets + QuestMortars > 0)
            {
                if (catalog == null || !catalog.TryGet("Turret_MiniTurret", out CatalogDef mini) || mini.Fuel.Count != 1 ||
                    mini.FuelMultiplier <= 0 || !catalog.TryGet(mini.Fuel[0], out CatalogDef fuel))
                    throw new ArgumentException("The advertised guns need the catalog's mini turret.", nameof(catalog));
                int[] values = { P4EArtillery.ConstructionValue(catalog, mini.DefName), P4EArtillery.ImportValue(catalog, QuestMortars),
                    new[] { "Barricade", "Sandbags" }.Select(n => P4EArtillery.ConstructionValue(catalog, n))
                        .Where(v => v != int.MaxValue).DefaultIfEmpty(int.MaxValue).Max() };
                if (values.Any(v => v == int.MaxValue)) throw new ArgumentException("Unresolved gun costs.", nameof(catalog));
                gunWatts = QuestTurrets * mini.BasePowerConsumption;
                // Front and both flanks of a one-cell gun take cover.
                gunValue = checked(QuestTurrets * (values[0] + 3 * values[2] +
                    (int)Math.Ceiling(mini.FuelCapacity / mini.FuelMultiplier * fuel.MarketValue)) + values[1]);
            }
            SettlementCapability capabilities = SettlementCapability.None;
            if (stage.Level >= SettlementTechStageLevel.IndustrialEarly)
                capabilities = SettlementCapability.Electricity | SettlementCapability.Refrigeration;
            var floors = new List<KeyValuePair<LayoutRoomKind, int>>
            {
                new KeyValuePair<LayoutRoomKind, int>(LayoutRoomKind.Storage, Math.Max(12, Site.LootStacks + Site.Roster.Count * 2))
            };
            if (Strategy.PrisonerCapacity > 0) floors.Add(new KeyValuePair<LayoutRoomKind, int>(LayoutRoomKind.Jail, Strategy.PrisonerCapacity));
            return new LayoutRequirements(Site.Roster.Count, capabilities, HousingStandard.Barracks, climate,
                0, 60 + Site.Roster.Count * 35, 0, floors, stage, checked(ConstructionImportAllowance + gunValue),
                EquipmentValue, stockpileRoomCapacityTarget: 48, compactQuestCamp: true,
                defenseReserveWatts: gunWatts, questArtillery: QuestMortars);
        }

        public SettlementLoadout DescribeLoadout(SettlementTechStage stage) => new SettlementLoadout(stage,
            Site.Roster.Select((r, i) => new ResidentLoadout(i, r.Role, false, r.Weapon, r.Apparel, null)),
            new LoadoutItem[0], new LoadoutItem[0], new LoadoutItem[0], EquipmentValue,
            new string[0], new[] { Version + ":authoritative_native_gear" }, null);

        public IReadOnlyList<string> ValidateReservations(ProceduralLayout layout)
        {
            var errors = new List<string>();
            if (layout == null) return new[] { "missing_layout" };
            if (layout.FieldCellsRequired != 0 || layout.FieldWall != null || layout.JointSite != null)
                errors.Add("quest_camp_has_agricultural_contract");
            // Courts are packed with the rooms; turrets come with the camp's
            // final defense, so a layout without it has none yet.
            int turrets = layout.Turret?.All.Count() ?? 0;
            int expectedTurrets = layout.ApproachDefense == null ? 0 : QuestTurrets;
            if (QuestTurrets + QuestMortars == 0 && (layout.Court != null || layout.Turret != null))
                errors.Add("unfunded_quest_static_defense");
            else if (turrets != expectedTurrets || layout.Courts.Count != QuestMortars ||
                (layout.Turret?.All.Any(t => t.DefName != "Turret_MiniTurret" || t.ReserveFuelUnits != 0) ?? false))
                errors.Add("advertised_static_defense_mismatch");
            if (layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Bed) < Site.Roster.Count)
                errors.Add("native_roster_bed_capacity");
            if (layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.PrisonBed) < Strategy.PrisonerCapacity)
                errors.Add("protected_prison_capacity");
            return new ReadOnlyCollection<string>(errors);
        }
    }
}
