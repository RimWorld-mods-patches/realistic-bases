using System;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static QuestSiteFacts QuestFacts(int count = 5, bool rescue = false,
        string[] parts = null, bool hidden = false, string prisoner = "target-1", bool held = true,
        string faction = "Pirate", string generator = "Encounter", double points = 350,
        bool native = true, bool caravan = true, int expected = -1)
    {
        string[] names = parts ?? (rescue
            ? new[] { "PrisonerWillingToJoin", "Outpost", "PossibleUnknownThreatMarker" }
            : new[] { "BanditCamp" });
        return new QuestSiteFacts(91, faction, generator, names.Select(n => new QuestPartFacts(n, hidden)),
            points, 12345, 550, expected, Enumerable.Range(0, count).Select(i =>
                new QuestResidentFacts("native-" + i, "Pirate", LoadoutRole.RangedLine,
                    new LoadoutItem("Gun_Autopistol", null, LoadoutQuality.Normal, 1, 150))),
            5, rescue ? prisoner : null, rescue && held, native, caravan);
    }

    private static P4DQuestContract QuestContract(QuestSiteFacts facts)
    {
        True(P4DQuestContract.TryCreate(facts, out var c, out var reason), "quest contract: " + reason);
        return c;
    }

    private static void QuestStrategies()
    {
        True(QuestContract(QuestFacts()).Strategy is BanditEliminationLayoutStrategy, "elimination strategy");
        True(QuestContract(QuestFacts(rescue: true)).Strategy is GuardedRescueLayoutStrategy, "rescue strategy");
        foreach (var facts in new[]
        {
            QuestFacts(parts: new[] { "BanditCamp", "Turrets" }),
            QuestFacts(parts: new[] { "BanditCamp", "PossibleUnknownThreatMarker" }),
            QuestFacts(rescue: true, parts: new[] { "PrisonerWillingToJoin", "Outpost" }),
            QuestFacts(rescue: true, parts: new[] { "Outpost", "PrisonerWillingToJoin", "PossibleUnknownThreatMarker" }),
            QuestFacts(hidden: true), QuestFacts(faction: "Mechanoid"), QuestFacts(generator: "Other"),
            QuestFacts(native: false), QuestFacts(caravan: false)
        })
            True(!P4DQuestContract.TryCreate(facts, out _, out _), "unsupported composition/owner remains native");
    }

    private static void QuestRosterContracts()
    {
        var facts = QuestFacts(17, points: 5000, expected: 3);
        var contract = QuestContract(facts);
        var needs = contract.Requirements(TechStageModel.Create(SettlementTechStageLevel.IndustrialMid), Temperate);
        Equal(17, needs.Residents, "no ordinary twelve cap or expected-count truncation");
        Equal(17, contract.DescribeLoadout(needs.Stage).Residents.Count, "entire actual roster");
        Equal(contract.EquipmentValue, needs.EquipmentValueLimit, "native actual gear ledger");
        Equal(60000, needs.ImportValueLimit, "declared threat-derived construction allowance");
        Equal(0, contract.AddedStaticThreatPoints, "no added guns or operators");
        Equal(0, needs.FieldCells, "explicit finite/no-field profile");
        Equal(51, contract.StartupFoodUnits(0.9), "meal reserve follows the native roster");
        Equal(918, contract.StartupFoodUnits(0.05), "pemmican reserve uses nutrition, not meal item count");
        Equal(contract.Digest, QuestContract(facts).Digest, "replay deterministic");
        Equal(contract.Seed, QuestContract(facts).Seed, "purpose-separated seed stable");
        NotEqual(contract.Digest, QuestContract(QuestFacts(18, points: 5000)).Digest, "roster affects contract");
        foreach (var f in new[] { QuestFacts(0), QuestFacts(33), QuestFacts(points: double.NaN), QuestFacts(points: -1) })
            True(!P4DQuestContract.TryCreate(f, out _, out _), "unsupported native inputs fallback without trimming");
        True(QuestContract(QuestFacts(1, points: 100)).Requirements(needs.Stage, Temperate).Residents == 1,
            "native low count never raised to ordinary minimum four");
    }

    private static void QuestObjectiveContracts()
    {
        foreach (var f in new[] { QuestFacts(rescue: true, held: false), QuestFacts(rescue: true, prisoner: null),
            QuestFacts(rescue: true, prisoner: "native-1") })
            True(!P4DQuestContract.TryCreate(f, out _, out _), "missing/shared objective fallback");
        var contract = QuestContract(QuestFacts(rescue: true));
        var needs = contract.Requirements(TechStageModel.Create(SettlementTechStageLevel.IndustrialMid), Temperate);
        Equal(1, needs.Floor(LayoutRoomKind.Jail), "existing prison reserved upfront");
        Equal(5, needs.Residents, "prisoner excluded from defender roster");
        True(contract.Site.Roster.All(r => r.Id != contract.Site.PrisonerId), "target never controlled");
        Equal(18, contract.StartupFoodUnits(0.9), "protected prisoner included in finite meals");
        Equal(324, contract.StartupFoodUnits(0.05), "protected prisoner included in tribal nutrition");
    }

    private static ProceduralLayoutInput QuestInput(P4DQuestContract contract, TerrainSnapshot terrain) =>
        new ProceduralLayoutInput(contract.Requirements(TechStageModel.Create(SettlementTechStageLevel.IndustrialMid), Temperate),
            terrain, contract.Seed, CoreCatalog, questContract: contract);

    private static void QuestCampLayouts()
    {
        foreach (bool rescue in new[] { false, true })
        {
            var contract = QuestContract(QuestFacts(rescue: rescue));
            var input = QuestInput(contract, Plains(160));
            var first = ProceduralLayoutPlanner.Plan(input);
            True(first.Success, "shared camp packs: " + string.Join(";", first.Diagnostics));
            Equal(first.Layout.Digest, ProceduralLayoutPlanner.Plan(input).Layout.Digest, "camp deterministic");
            Equal(0, contract.ValidateReservations(first.Layout).Count, "objective/facility reservations");
            True(first.Layout.Court == null && first.Layout.FieldWall == null && first.Layout.Turret == null,
                "no fake field, extra operator or static threat");
            var loadout = LayoutLoadoutModel.Build(input, first.Layout);
            Equal(0, loadout.Armoury.Count + loadout.Medicine.Count + loadout.Components.Count, "no ordinary gear/wealth leakage");
            True(loadout.Residents.All(r => r.Weapon.DefName == "Gun_Autopistol"), "native weapons preserved descriptively");
            Equal(0, ProceduralLayoutValidator.ValidateLoadout(input, first.Layout, loadout).Count, "native roster accepted");
        }
    }

    private static void QuestCampRejection()
    {
        var contract = QuestContract(QuestFacts());
        var input = QuestInput(contract, Build(70, 70, (x, z) => TerrainTraits.Water));
        string before = input.Digest;
        True(!ProceduralLayoutPlanner.Plan(input).Success, "unbuildable site falls back");
        Equal(before, input.Digest, "preflight leaves input immutable");
        Equal(5, contract.Site.Roster.Count, "rejection never discards defenders");
    }

    private static void QuestTerrainTechMatrix()
    {
        foreach (bool rescue in new[] { false, true })
        foreach (var level in new[] { SettlementTechStageLevel.Neolithic, SettlementTechStageLevel.IndustrialMid })
        foreach (var terrain in new[] { Plains(160), Mountain(160, 160), Obstacles(160) })
        {
            var contract = QuestContract(QuestFacts(rescue: rescue, points: 700));
            var input = new ProceduralLayoutInput(contract.Requirements(TechStageModel.Create(level), Temperate),
                terrain, contract.Seed, CoreCatalog, questContract: contract);
            var result = ProceduralLayoutPlanner.Plan(input);
            True(result.Success, "quest matrix " + rescue + "/" + level + ": " + string.Join(";", result.Diagnostics));
            Equal(0, contract.ValidateReservations(result.Layout).Count, "all native occupants and objective fit");
            Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, "shared geometry validates");
            Equal(0, ProceduralLayoutValidator.ValidateLoadout(input, result.Layout,
                LayoutLoadoutModel.Build(input, result.Layout)).Count, "native gear remains descriptive");
            Equal(rescue ? 1 : 0, result.Layout.Rooms.Count(r => r.Kind == LayoutRoomKind.Jail), "no duplicate prison");
            Equal(5, input.Requirements.Residents, "terrain never changes the native roster");
        }
    }
}
