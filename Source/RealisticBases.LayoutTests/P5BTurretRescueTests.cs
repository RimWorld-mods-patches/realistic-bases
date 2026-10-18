using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static readonly string[] TurretRescueParts =
        { "PrisonerWillingToJoin", "Turrets", "PossibleUnknownThreatMarker" };

    private static QuestSiteFacts TurretRescueFacts(int turrets, int mortars, int roster = -1,
        string faction = "Pirate", double points = -1) =>
        new QuestSiteFacts(91, faction, "Encounter", TurretRescueParts.Select(n => new QuestPartFacts(n)),
            points > 0 ? points : System.Math.Max(300, turrets * 45), 12345, 0, -1,
            Enumerable.Range(0, roster >= 0 ? roster : mortars + 1).Select(i =>
                new QuestResidentFacts("native-" + i, "Pirate", LoadoutRole.RangedLine,
                    new LoadoutItem("Gun_Autopistol", null, LoadoutQuality.Normal, 1, 150))),
            0, "target-1", true, turretsCount: turrets, mortarsCount: mortars);

    private static int MiniTurretWatts()
    {
        True(CoreCatalog.TryGet("Turret_MiniTurret", out CatalogDef mini), "mini turret in the catalog");
        return mini.BasePowerConsumption;
    }

    private static ProceduralLayoutInput TurretRescueInput(P4DQuestContract contract, TerrainSnapshot terrain) =>
        new ProceduralLayoutInput(contract.Requirements(TechStageModel.Create(SettlementTechStageLevel.IndustrialMid),
            Temperate, CoreCatalog), terrain, contract.Seed, CoreCatalog, questContract: contract);

    private static void TurretRescueContracts()
    {
        P4DQuestContract contract = QuestContract(TurretRescueFacts(6, 1));
        True(contract.Strategy is TurretRescueLayoutStrategy, "turret rescue strategy");
        LayoutRequirements needs = contract.Requirements(TechStageModel.Create(SettlementTechStageLevel.IndustrialMid),
            Temperate, CoreCatalog);
        True(needs.ImportValueLimit > contract.ConstructionImportAllowance, "the advertised guns are funded on top of the camp");
        Equal(6, contract.QuestTurrets, "advertised turrets");
        Equal(1, contract.QuestMortars, "advertised mortar");
        Equal(6 * MiniTurretWatts(), needs.DefenseReserveWatts, "power reserved for the advertised guns");
        Equal(1, needs.QuestArtillery, "exactly the advertised courts");
        Equal(1, needs.Floor(LayoutRoomKind.Jail), "the original prisoner's cell");
        Equal(2, needs.Residents, "the guard and the mortar crew");
        NotEqual(contract.Digest, QuestContract(TurretRescueFacts(7, 1)).Digest, "the advertised guns are in the digest");
        foreach (QuestSiteFacts facts in new[]
        {
            TurretRescueFacts(6, 1, roster: 1), TurretRescueFacts(6, 0, roster: 2),
            TurretRescueFacts(1, 0), TurretRescueFacts(12, 0), TurretRescueFacts(6, 2),
            TurretRescueFacts(6, 0, faction: "TribeRough")
        })
            True(!P4DQuestContract.TryCreate(facts, out _, out _), "unadvertised guns or crews stay native");
        True(QuestContract(QuestFacts(rescue: true)).QuestTurrets == 0, "the outpost rescue builds no guns");
        True(contract.QuestMortarShells >= 5 && contract.QuestMortarShells <= 8, "native 5-8 shells per mortar");
        EncounterProfile mortar = P3EncounterProfiles.ForQuestArtillery(1, contract.QuestMortarShells);
        True(mortar.HeShells == contract.QuestMortarShells && mortar.ReinforcedBarrels == 0 && mortar.IncendiaryShells == 0,
            "the quest mortar profile carries native supply and no reserve");
    }

    private static void TurretRescueLayout(int turrets, int mortars)
    {
        P4DQuestContract contract = QuestContract(TurretRescueFacts(turrets, mortars));
        ProceduralLayoutInput input = TurretRescueInput(contract, Plains(160));
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
        True(result.Success, "turret rescue " + turrets + "/" + mortars + ": " +
            string.Join(";", result.Diagnostics.Where(d => d.Stage == "quest_guns" || d.Stage == "result" ||
                d.Stage == "quest_defense").Select(d => d.Stage + ":" + d.Code + ":" + d.Detail).Take(8)));
        ProceduralLayout layout = result.Layout;
        Equal(turrets, layout.Turret?.All.Count() ?? 0, "exactly the advertised turrets");
        True(layout.Turret.All.All(t => t.DefName == "Turret_MiniTurret" && t.ReserveFuelUnits == 0 &&
            t.CoverCells.Count > 0), "covered mini turrets, loaded as native");
        Equal(mortars, layout.Courts.Count, "exactly the advertised mortars " +
            string.Join("|", layout.Notes.Where(n => n.Contains("mortar") || n.Contains("court"))));
        Equal(1, layout.Rooms.Count(r => r.Kind == LayoutRoomKind.Jail), "one prison");
        Equal(0, contract.ValidateReservations(layout).Count, "reservations");
        Equal(0, ProceduralLayoutValidator.Validate(input, layout).Count, "shared geometry validates");
        Equal(layout.Digest, ProceduralLayoutPlanner.Plan(input).Layout.Digest, "deterministic");
        string sides = string.Join(",", layout.Turret.All.GroupBy(t => t.Side).OrderBy(g => g.Key)
            .Select(g => g.Key + "=" + g.Count()));
        System.Console.WriteLine("turret rescue " + turrets + "/" + mortars + " layout=" + layout.Digest + " sides=" + sides);
    }

    private static void TurretRescueCramped()
    {
        P4DQuestContract contract = QuestContract(TurretRescueFacts(11, 1));
        // A 32-cell buildable square in walkable marsh: the camp fits, eleven
        // covered turrets do not.
        ProceduralLayoutInput input = TurretRescueInput(contract, Build(120, 120, (x, z) =>
            x >= 44 && x < 76 && z >= 44 && z < 76 ? Ground : TerrainTraits.Standable));
        string before = input.Digest;
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
        True(!result.Success, "advertised guns that cannot fit fall back to native");
        True(result.Diagnostics.Any(d => d.Stage == "quest_guns" && d.Code == "rejected"),
            "the camp fits, the advertised guns do not");
        Equal(before, input.Digest, "preflight leaves input immutable");
    }

    private static void TurretRescueTerrain(string name, TerrainSnapshot terrain)
    {
        P4DQuestContract contract = QuestContract(TurretRescueFacts(6, 1));
        ProceduralLayoutInput input = TurretRescueInput(contract, terrain);
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
        True(result.Success, "turret rescue on " + name + ": " + string.Join(";", result.Diagnostics
            .Where(d => d.Stage == "quest_guns" || d.Stage == "result").Select(d => d.Code + ":" + d.Detail).Take(4)));
        Equal(6, result.Layout.Turret.All.Count(), "exactly the advertised turrets on " + name);
        Equal(1, result.Layout.Courts.Count, "exactly the advertised mortar on " + name);
        Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, "geometry validates on " + name);
    }

    private static void RunTurretRescueTests()
    {
        Run("P5B turret rescue contract requires the advertised guns and crews", TurretRescueContracts);
        foreach (var counts in new[] { (2, 0), (6, 1), (11, 1) })
            Run("P5B turret rescue camp places " + counts.Item1 + " turrets and " + counts.Item2 + " mortars",
                () => TurretRescueLayout(counts.Item1, counts.Item2));
        Run("P5B turret rescue rejects a site too small for the advertised guns", TurretRescueCramped, 55);
        Run("P5B turret rescue camp on a mountain", () => TurretRescueTerrain("mountain", Mountain(160, 160)), 35);
        Run("P5B turret rescue camp among obstacles", () => TurretRescueTerrain("obstacles", Obstacles(160)));
    }
}
