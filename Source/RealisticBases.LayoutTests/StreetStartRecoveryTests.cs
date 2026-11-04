using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void StreetStartRecovery()
    {
        TerrainSnapshot terrain = Build(90, 90, (x, z) => x == 40 && z == 44 ? DeepWater : Ground);
        string original = terrain.Digest;
        var input = Input(terrain, LayoutRequirements.FromPopulation(4, Industrial, Temperate, stage: MidStage), 1234);
        var anchor = new GridPoint(40, 40);
        var field = new GridRect(63, 33, 14, 14);
        LayoutAttempt Attempt(GridRect compound, out List<LayoutDiagnostic> diagnostics)
        {
            diagnostics = new List<LayoutDiagnostic>();
            var site = new P4BJointSite(field, compound, anchor, new GridRect(66,35,5,5).Points(), new GridPoint[0],
                new GridPoint[0], new GridPoint(63, 40), new GridPoint(76, 40), new GridPoint[0], 0);
            var grid = LayoutTerrainGrid.Build(terrain, input.Fortification.PlacementMargin).RestrictTo(compound);
            return new LayoutAttempt(input, input.Equipment.Demand, grid, anchor, LayoutDoctrine.OpenGround,
                false, 1234, new LayoutSearchStats(), diagnostics, new ProceduralLayoutStageTimings(), jointSite: site);
        }
        var attempt = Attempt(new GridRect(20, 20, 40, 40), out var diagnostics);
        True(attempt.BuildSpine(), "a nearby complete street replaces the one-cell start beside a hazard");
        True(diagnostics.Any(d => d.Code == "street_start_recovered"), "recovery is diagnosed");
        True(!Attempt(new GridRect(40, 40, 1, 1), out _).BuildSpine(),
            "nearby open terrain outside the compound cannot authorize a street");
        anchor = new GridPoint(40, 39);
        True(Attempt(new GridRect(20, 20, 40, 40), out var unchanged).BuildSpine(), "an existing legal start still succeeds");
        True(!unchanged.Any(d => d.Code == "street_start_recovered"), "a legal original start is retained");
        Equal(original, terrain.Digest, "street recovery does not modify terrain");

        terrain = Build(90, 90, (x, z) => x >= 20 && x <= 70 && z >= 54 && z <= 68 ? Ground : DeepWater);
        input = Input(terrain, LayoutRequirements.FromPopulation(4, Industrial, Temperate, stage: MidStage), 1234);
        anchor = new GridPoint(40, 40);
        True(!Attempt(new GridRect(20, 20, 50, 50), out _).BuildSpine(),
            "recovery does not search a distant usable street beyond its eight-cell radius");
        anchor = new GridPoint(40, 60);
        True(Attempt(new GridRect(20, 20, 50, 50), out _).BuildSpine(), "the distant street itself is usable");
    }
}
