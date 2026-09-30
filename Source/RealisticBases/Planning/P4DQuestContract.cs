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
            bool prisonerHeld = false, bool nativeDefinitions = true, bool caravanArrival = true)
        {
            SiteId = siteId; Faction = faction; Generator = generator;
            Parts = Array.AsReadOnly(parts.ToArray()); ThreatPoints = threatPoints;
            PawnGroupSeed = pawnGroupSeed; LootValue = lootValue; ExpectedCount = expectedCount;
            Roster = Array.AsReadOnly(roster.ToArray()); LootStacks = lootStacks;
            PrisonerId = prisonerId; PrisonerHeld = prisonerHeld;
            NativeDefinitions = nativeDefinitions; CaravanArrival = caravanArrival;
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
    }

    public interface IQuestLayoutStrategy
    {
        string Id { get; }
        bool Matches(QuestSiteFacts site);
        int PrisonerCapacity { get; }
        string ValidateObjective(QuestSiteFacts site);
    }

    public sealed class BanditEliminationLayoutStrategy : IQuestLayoutStrategy
    {
        public string Id => "core-bandit-v1";
        public int PrisonerCapacity => 0;
        public bool Matches(QuestSiteFacts site) => site.Parts.Select(p => p.Name).SequenceEqual(new[] { "BanditCamp" });
        public string ValidateObjective(QuestSiteFacts site) => site.PrisonerId == null ? null : "unexpected_protected_target";
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
    }

    public sealed class P4DQuestContract
    {
        public const string Version = "p4d-quest-contract.1";
        // Explicit bounded support, independent of the ordinary 4–12 policy.
        public const int MaximumRoster = 32;
        private static readonly IQuestLayoutStrategy[] Strategies =
            { new BanditEliminationLayoutStrategy(), new GuardedRescueLayoutStrategy() };
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
            // Declared provisional quest-derived allowances. Zero added static
            // threat in v1: no turrets, mortars, traps or unfunded operators.
            ConstructionImportAllowance = checked((int)Math.Ceiling(site.ThreatPoints * 12));
            OperatingValueAllowance = checked((int)Math.Ceiling(site.ThreatPoints * 2));
            ulong digest = StableHash.Add(Seed, site.Faction);
            digest = StableHash.Add(digest, site.ThreatPoints.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            digest = StableHash.Add(digest, site.LootValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            digest = StableHash.Add(digest, site.PrisonerId ?? string.Empty);
            digest = StableHash.Add(digest, site.LootStacks);
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
        public int AddedStaticThreatPoints => 0;

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

        public LayoutRequirements Requirements(SettlementTechStage stage, LayoutClimate climate)
        {
            SettlementCapability capabilities = SettlementCapability.None;
            if (stage.Level >= SettlementTechStageLevel.IndustrialEarly)
                capabilities = SettlementCapability.Electricity | SettlementCapability.Refrigeration;
            var floors = new List<KeyValuePair<LayoutRoomKind, int>>
            {
                new KeyValuePair<LayoutRoomKind, int>(LayoutRoomKind.Storage, Math.Max(12, Site.LootStacks + Site.Roster.Count * 2))
            };
            if (Strategy.PrisonerCapacity > 0) floors.Add(new KeyValuePair<LayoutRoomKind, int>(LayoutRoomKind.Jail, Strategy.PrisonerCapacity));
            return new LayoutRequirements(Site.Roster.Count, capabilities, HousingStandard.Barracks, climate,
                0, 60 + Site.Roster.Count * 35, 0, floors, stage, ConstructionImportAllowance,
                EquipmentValue, stockpileRoomCapacityTarget: 48, compactQuestCamp: true);
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
            if (layout.Court != null || layout.Turret != null) errors.Add("unfunded_quest_static_defense");
            if (layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Bed) < Site.Roster.Count)
                errors.Add("native_roster_bed_capacity");
            if (layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.PrisonBed) < Strategy.PrisonerCapacity)
                errors.Add("protected_prison_capacity");
            return new ReadOnlyCollection<string>(errors);
        }
    }
}
