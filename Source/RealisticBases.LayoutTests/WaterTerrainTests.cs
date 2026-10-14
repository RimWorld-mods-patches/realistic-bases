using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealisticBases.Planning;

// Live Geological Landforms water tiles captured from the disposable profile.
internal static partial class Program
{
    private static TerrainSnapshot CapturedTerrain(string file, int size, string id)
    {
        var cells = new List<TerrainCell>();
        string[] rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, file));
        Equal(size, rows.Length, "captured height");
        foreach (string row in rows)
        {
            int width = 0;
            foreach (string token in row.Split(','))
            {
                string[] parts = token.Split('*');
                int count = int.Parse(parts[1]); width += count;
                for (int i = 0; i < count; i++) cells.Add(new TerrainCell((TerrainTraits)Convert.ToUInt16(parts[0], 16)));
            }
            Equal(size, width, "captured width");
        }
        return new TerrainSnapshot(size, size, cells, id);
    }

    private static void GlPeninsulaTile()
    {
        TerrainSnapshot terrain = CapturedTerrain("gl-peninsula-rejected-traits.rle", 160, "gl-peninsula-472DF8BFF1B96E6D");
        var climate = new LayoutClimate(-10, 31);
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialMid);
        var budget = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate, stage, climate, 100);
        var req = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, climate, stage: stage,
            populationBudget: budget, saleStorageStacks: 97, salePerishableStacks: 20);
        Equal(7, req.Residents, "keep the live funded roster");
        var input = new ProceduralLayoutInput(req, terrain, 0xAE80312B97715DE2UL, RuntimeFlatCatalogFixture.Build(),
            new LayoutFortificationReservation(1), resources: new LayoutLocalResources(7437, new[] { "BlocksGranite", "BlocksMarble" }));
        var result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        if (!result.Success)
            foreach (var d in result.Diagnostics.Where(d => d.Stage == "attempt" || d.Stage == "joint_site" || d.Stage == "result").Take(40)) Console.WriteLine("  peninsula " + d);
        Accepted(result);
        Console.WriteLine("  peninsula layout=" + result.Layout.Digest + " field=" + result.Layout.JointSite.Field +
            " compound=" + result.Layout.JointSite.Compound + " " + result.Stats);
    }
}
