using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using RealisticBases.Planning;

namespace RealisticBases.Diagnostics
{
    // Disposable test data only. Export the resolved game catalog so a native
    // rejection can be replayed without guessing XML/runtime differences.
    internal static class P4PlannerFixtureExporter
    {
        internal static void Write(string path, TechCatalog catalog)
        {
            string source = "// Generated resolved runtime catalog: TEST DATA ONLY.\n" +
                "using System.Collections.Generic;\nusing RealisticBases.Planning;\n" +
                "internal static class RuntimeFlatCatalogFixture\n{\n" +
                "public static TechCatalog Build() => new TechCatalog(\"captured Core runtime\",\n" +
                "new CatalogDef[] {\n" + string.Join(",\n", catalog.Defs.Select(Constructor)) +
                "\n}, new CatalogResearch[] {\n" + string.Join(",\n", catalog.Research.Select(Constructor)) + "\n});\n}\n";
            System.IO.File.WriteAllText(path, source);
        }

        internal static void WriteProceduralInput(string path, IList<ProceduralLayoutInput> inputs,
            IList<ProceduralLayoutResult> results, PhysicalSettlementProfile profile, string stageMode, string worldSeed,
            int tile, int targetLayers, int wealthPercent)
        {
            if (inputs == null || inputs.Count == 0 || results == null || results.Count != inputs.Count || profile == null)
                throw new ArgumentException("Captured planner attempts and results must be nonempty and aligned.");
            ProceduralLayoutInput input = inputs[inputs.Count - 1];
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].QuestContract != null)
                    throw new InvalidOperationException("Captured timing-tile replay does not support quest contracts.");
                if (inputs[i].Requirements.Digest != input.Requirements.Digest ||
                    inputs[i].Terrain.Digest != input.Terrain.Digest || inputs[i].Catalog.Digest != input.Catalog.Digest ||
                    inputs[i].Resources.Digest != input.Resources.Digest || inputs[i].Seed != input.Seed ||
                    inputs[i].Engine.Id != input.Engine.Id || inputs[i].Budget.MaxSites != input.Budget.MaxSites ||
                    inputs[i].Budget.AttemptsPerSite != input.Budget.AttemptsPerSite ||
                    inputs[i].Budget.MaxEvaluations != input.Budget.MaxEvaluations ||
                    inputs[i].Budget.MaxCandidatesPerRoom != input.Budget.MaxCandidatesPerRoom ||
                    inputs[i].Budget.MaxRepairsPerAttempt != input.Budget.MaxRepairsPerAttempt ||
                    inputs[i].Budget.MaxRouteNodes != input.Budget.MaxRouteNodes)
                    throw new InvalidOperationException("Planner attempts changed a shared input outside fortification or degree.");
            }
            StringBuilder source = new StringBuilder(2 * 1024 * 1024);
            source.AppendLine("// Captured complete live planner input. Generated disposable test fixture.");
            source.AppendLine("using System.Collections.Generic;");
            source.AppendLine("using RealisticBases.Planning;");
            source.AppendLine("internal static class CapturedTimingTileFixture");
            source.AppendLine("{");
            source.AppendLine("    public const string ExpectedInputDigest = " + Literal(input.Digest) + ";");
            source.AppendLine("    public const string ExpectedLayoutDigest = " + Literal(results[results.Count - 1].Layout?.Digest ?? "none") + ";");
            source.AppendLine("    public const string ExpectedTerrainDigest = " + Literal(input.Terrain.Digest) + ";");
            source.AppendLine("    public const string ExpectedRequirementsDigest = " + Literal(input.Requirements.Digest) + ";");
            source.AppendLine("    public const string ExpectedCatalogDigest = " + Literal(input.Catalog.Digest) + ";");
            source.AppendLine("    public const string ExpectedResourcesDigest = " + Literal(input.Resources.Digest) + ";");
            source.AppendLine("    public const string ExpectedProfileDigest = " + Literal(profile.Digest) + ";");
            source.AppendLine("    public const string ExpectedProfileId = " + Literal(profile.Id) + ";");
            source.AppendLine("    public const int ExpectedProfileVersion = " + profile.Version.ToString(CultureInfo.InvariantCulture) + ";");
            source.AppendLine("    public const string ExpectedStage = " + Literal(input.Requirements.Stage.ToString()) + ";");
            source.AppendLine("    public const string ExpectedStageMode = " + Literal(stageMode ?? string.Empty) + ";");
            source.AppendLine("    public const string ExpectedWorldSeed = " + Literal(worldSeed ?? string.Empty) + ";");
            source.AppendLine("    public const int ExpectedTile = " + tile.ToString(CultureInfo.InvariantCulture) + ";");
            source.AppendLine("    public const int ExpectedTargetLayers = " + targetLayers.ToString(CultureInfo.InvariantCulture) + ";");
            source.AppendLine("    public const int ExpectedWealthPercent = " + wealthPercent.ToString(CultureInfo.InvariantCulture) + ";");
            source.AppendLine("    public const string ExpectedEngineId = " + Literal(input.Engine.Id) + ";");
            source.AppendLine("    public const int ExpectedPlannerDegree = " + input.PlannerDegree.ToString(CultureInfo.InvariantCulture) + ";");
            source.AppendLine("    public static string[] ExpectedAttemptInputDigests = new string[] {" +
                string.Join(",", inputs.Select(i => Literal(i.Digest))) + "};");
            source.AppendLine("    public static string[] ExpectedAttemptLayoutDigests = new string[] {" +
                string.Join(",", results.Select(r => Literal(r.Layout?.Digest ?? "none"))) + "};");
            source.AppendLine("    public static PhysicalSettlementProfile BuildProfile() => " + Constructor(profile) + ";");
            source.AppendLine("    public static ProceduralLayoutInput[] Build(int plannerDegree)");
            source.AppendLine("    {");
            source.AppendLine("        SettlementTechStage stage = " + StageExpression(input.Requirements.Stage) + ";");
            LayoutRequirements r = input.Requirements;
            source.AppendLine("        var requirements = new LayoutRequirements(residents: " + r.Residents +
                ", capabilities: (SettlementCapability)" + ((int)r.Capabilities).ToString(CultureInfo.InvariantCulture) +
                ", housing: (HousingStandard)" + ((int)r.Housing).ToString(CultureInfo.InvariantCulture) +
                ", climate: new LayoutClimate(" + r.Climate.MinOutdoorC + ", " + r.Climate.MaxOutdoorC + ")" +
                ", mortarBiasPercent: " + r.MortarBiasPercent + ", excavationBudgetCells: " + r.ExcavationBudgetCells +
                ", fieldCells: " + r.FieldCells + ", capacityFloors: " + Literal(r.CapacityFloors.OrderBy(p => p.Key).ToArray()) +
                ", stage: stage, importValueLimit: " + r.ImportValueLimit + ", equipmentValueLimit: " + r.EquipmentValueLimit +
                ", stockpileRoomCapacityTarget: " + r.StockpileRoomCapacityTarget + ", compactQuestCamp: " + Literal(r.CompactQuestCamp) +
                ", defenseReserveWatts: " + r.DefenseReserveWatts + ", logisticsUtilityValueLimit: " + r.LogisticsUtilityValueLimit +
                ", saleStorageStacks: " + r.SaleStorageStacks + ", salePerishableStacks: " + r.SalePerishableStacks +
                ", retainedStorageStacks: " + r.RetainedStorageStacks + ", retainedPerishableStacks: " + r.RetainedPerishableStacks +
                ", questArtillery: " + r.QuestArtillery + ", defenseImportReserveValue: " + r.DefenseImportReserveValue +
                ", baseImportValueLimit: " + r.BaseImportValueLimit + ");");
            source.AppendLine("        var terrain = BuildTerrain();");
            source.AppendLine("        var seed = 0x" + input.Seed.ToString("X16", CultureInfo.InvariantCulture) + "UL;");
            source.AppendLine("        var catalog = BuildCatalog();");
            source.AppendLine("        var budget = " + Literal(input.Budget) + ";");
            source.AppendLine("        var resources = " + Literal(input.Resources) + ";");
            source.AppendLine("        return new ProceduralLayoutInput[]");
            source.AppendLine("        {");
            foreach (ProceduralLayoutInput attempt in inputs)
                source.AppendLine("            new ProceduralLayoutInput(requirements, terrain, seed, catalog, " +
                    Literal(attempt.Fortification) + ", budget, resources, plannerDegree: plannerDegree),");
            source.AppendLine("        };");
            source.AppendLine("    }");
            source.AppendLine("    private static TechCatalog BuildCatalog() => " + Constructor(input.Catalog) + ";");
            source.AppendLine("    private static TerrainSnapshot BuildTerrain()");
            source.AppendLine("    {");
            source.AppendLine("        var cells = new List<TerrainCell>(" + (input.Terrain.Width * input.Terrain.Height) + ");");
            for (int z = 0; z < input.Terrain.Height; z++)
            {
                for (int x = 0; x < input.Terrain.Width;)
                {
                    TerrainCell cell = input.Terrain[x, z];
                    int count = 1;
                    while (x + count < input.Terrain.Width)
                    {
                        TerrainCell next = input.Terrain[x + count, z];
                        if (next.Traits != cell.Traits || next.TerrainId != cell.TerrainId || next.RoofId != cell.RoofId) break;
                        count++;
                    }
                    source.AppendLine("        for (int i = 0; i < " + count + "; i++) cells.Add(new TerrainCell((TerrainTraits)" +
                        ((ushort)cell.Traits).ToString(CultureInfo.InvariantCulture) + ", " + cell.TerrainId + ", " + cell.RoofId + ")); // row " + z);
                    x += count;
                }
            }
            source.AppendLine("        return new TerrainSnapshot(" + input.Terrain.Width + ", " + input.Terrain.Height +
                ", cells, " + Literal(input.Terrain.SourceVersion) + ");");
            source.AppendLine("    }");
            source.AppendLine("}");
            System.IO.File.WriteAllText(path, source.ToString());
        }

        private static string StageExpression(SettlementTechStage stage)
        {
            string expression = "TechStageModel.Create((SettlementTechStageLevel)" +
                ((int)stage.Level).ToString(CultureInfo.InvariantCulture) + ")";
            foreach (TechAdaptation adaptation in stage.Adaptations)
                expression += ".WithAdaptation(" + Literal(adaptation.ResearchDefName) + ", " + Literal(adaptation.Reason) + ")";
            return expression;
        }

        private static string Constructor(object value)
        {
            Type type = value.GetType();
            ConstructorInfo ctor = type.GetConstructors().Single();
            return "new " + type.Name + "(" + string.Join(", ", ctor.GetParameters().Select(p =>
            {
                string property = type == typeof(CatalogDef) && p.Name == "research" ? "ResearchPrerequisites" :
                    char.ToUpperInvariant(p.Name[0]) + p.Name.Substring(1);
                PropertyInfo info = type.GetProperty(property) ?? throw new InvalidOperationException("No fixture property " + property);
                return p.Name + ": " + Literal(info.GetValue(value, null));
            })) + ")";
        }

        private static string Literal(object value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
            if (value is bool b) return b ? "true" : "false";
            if (value is Enum) return "(" + value.GetType().Name + ")" + Convert.ToInt64(value, CultureInfo.InvariantCulture);
            if (value is GridPoint c) return "new GridPoint(" + c.X + ", " + c.Z + ")";
            if (value is IEnumerable<GridPoint> points)
                return "new GridPoint[] {" + string.Join(",", points.Select(x => Literal(x))) + "}";
            if (value is IEnumerable<string> strings) return "new string[] {" + string.Join(",", strings.Select(x => Literal(x))) + "}";
            if (value is IEnumerable<KeyValuePair<string, int>> costs)
                return "new KeyValuePair<string,int>[] {" + string.Join(",", costs.Select(x =>
                    "new KeyValuePair<string,int>(" + Literal(x.Key) + "," + x.Value + ")")) + "}";
            if (value is IEnumerable<KeyValuePair<LayoutRoomKind, int>> floors)
                return "new KeyValuePair<LayoutRoomKind,int>[] {" + string.Join(",", floors.Select(x =>
                    "new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)" + ((int)x.Key).ToString(CultureInfo.InvariantCulture) + "," + x.Value + ")")) + "}";
            if (value is IEnumerable<FacilityDemand> facilities)
                return "new FacilityDemand[] {" + string.Join(",", facilities.Select(Constructor)) + "}";
            if (value is IEnumerable<StartupScenario> scenarios)
                return "new StartupScenario[] {" + string.Join(",", scenarios.Select(Constructor)) + "}";
            if (value is IEnumerable<CatalogDef> defs)
                return "new CatalogDef[] {" + string.Join(",", defs.Select(Constructor)) + "}";
            if (value is IEnumerable<CatalogResearch> research)
                return "new CatalogResearch[] {" + string.Join(",", research.Select(Constructor)) + "}";
            if (value is PhysicalSettlementProfile || value is FacilityDemand || value is StartupScenario ||
                value is SettlementResourceBudget || value is CatalogDef || value is CatalogResearch || value is TechCatalog ||
                value is LayoutClimate || value is LayoutFortificationReservation || value is LayoutSearchBudget ||
                value is LayoutLocalResources)
                return Constructor(value);
            if (value is double number) return number.ToString("R", CultureInfo.InvariantCulture) + "d";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
