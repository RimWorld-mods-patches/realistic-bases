using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// Natural-barrier siting and inner-yard crops (layout engine v2 follow-up).
internal static partial class Program
{
    private const TerrainTraits Sand = TerrainTraits.Standable | TerrainTraits.Buildable | TerrainTraits.SupportsHeavy;
    private const TerrainTraits Shallow = TerrainTraits.Water | TerrainTraits.Standable;

    // Sea along the south edge: deep water, a shallow strip, then a sand beach.
    internal static TerrainSnapshot Beach(int size) => Build(size, size, (x, z) =>
    {
        int shore = 34 + (int)(4 * Math.Sin(x / 11.0));
        if (z < shore - 8) return DeepWater;
        if (z < shore - 3) return Shallow;
        if (z < shore + 6) return Sand;
        return Ground;
    });

    // A deep lake in the east half with a shallow rim.
    internal static TerrainSnapshot Lakeshore(int size) => Build(size, size, (x, z) =>
    {
        double d = Math.Sqrt((x - size * 0.78) * (x - size * 0.78) + (z - size * 0.5) * (z - size * 0.5));
        if (d < size * 0.26) return DeepWater;
        if (d < size * 0.26 + 3) return Shallow;
        return Ground;
    });

    private static void ExposureRules()
    {
        TerrainSnapshot terrain = Build(20, 20, (x, z) =>
        {
            if (x == 5) return z == 10 ? Shallow : DeepWater;          // a deep channel with one shallow ford
            if (x >= 12 && x <= 16 && z >= 6 && z <= 10)               // a rock ring around a pocket
                return x == 14 && z == 8 ? Ground : RockRoofed;
            return Ground;
        });
        bool[] reach = LayoutExposure.EdgeReach(terrain);
        Func<int, int, bool> at = (x, z) => reach[z * terrain.Width + x];
        True(at(2, 2) && at(8, 2), "both banks reachable through the shallow ford");
        True(at(5, 10), "shallow water is walkable");
        True(!at(5, 3), "deep water blocks");
        True(!at(12, 6), "rock blocks");
        True(!at(14, 8), "a pocket enclosed by rock is unreachable");
        int protectedCells = LayoutExposure.ProtectedRing(terrain, reach, new GridRect(13, 7, 3, 3), 0, out int ring);
        Equal(8, ring, "ring of a 3x3 at offset 0");
        Equal(8, protectedCells, "rock ring cells are all protected");
    }

    private static int SiteProtection(TerrainSnapshot terrain, ProceduralLayout layout, int margin) =>
        LayoutExposure.ProtectedRing(terrain, LayoutExposure.EdgeReach(terrain),
            LayoutExposure.Union(layout.JointSite.Field, layout.JointSite.Compound), margin + 2, out _);

    private static void BarrierSiting()
    {
        foreach (var c in new[] { new { Name = "beach", Terrain = Beach(180) }, new { Name = "lakeshore", Terrain = Lakeshore(180) } })
        {
            LayoutRequirements requirements = V2Funded(P2ProfileCatalog.IndustrialTemperate, SettlementTechStageLevel.IndustrialLate, Temperate, 100);
            ProceduralLayoutInput input = new ProceduralLayoutInput(requirements, c.Terrain, 3, CoreCatalog, new LayoutFortificationReservation(1));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            if (!result.Success)
                foreach (LayoutDiagnostic rejection in result.Diagnostics.Where(d => d.Stage == "finish"))
                    Console.WriteLine("  finish-rejection " + c.Name + " " + rejection.Detail);
            Accepted(result);
            Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, c.Name + " validator");
            int margin = input.Fortification.PlacementMargin;
            int chosen = SiteProtection(c.Terrain, result.Layout, margin);
            // The best-scoring alternatives considered by the same preflight.
            P4BJointSiteResult sites = P4BJointSitePlanner.Find(c.Terrain, requirements.FieldCells,
                input.Equipment.Demand.MinimumRoomArea * 3 / 2, margin, LayoutSeed.Sub(input.Seed, "joint-site"),
                input.Budget.MaxSites, input.Budget.MaxSites * 1024, requirements.Residents);
            bool[] reach = LayoutExposure.EdgeReach(c.Terrain);
            int median = sites.Candidates.Select(s => LayoutExposure.ProtectedRing(c.Terrain, reach,
                LayoutExposure.Union(s.Field, s.Compound), margin + 2, out _)).OrderBy(v => v).ElementAt(sites.Candidates.Count / 2);
            True(chosen > 0, c.Name + " site backed by a natural barrier, protected ring cells=" + chosen);
            Console.WriteLine("  " + c.Name + " chosenProtectedRing=" + chosen + " candidateMedian=" + median +
                " candidates=" + sites.Candidates.Count + " compound=" + result.Layout.JointSite.Compound);
            ExportLayout("siting-" + c.Name, result, c.Terrain);
        }
    }

    private static void InnerYardCrops()
    {
        int planted = 0;
        foreach (V2Case c in V2StyleCases().Where(x => x.JointSite))
        {
            ProceduralLayoutResult result = V2Plan(c);
            Accepted(result);
            ProceduralLayout layout = result.Layout;
            ProceduralLayoutInput input = new ProceduralLayoutInput(c.Requirements, c.Terrain, c.Seed, CoreCatalog,
                new LayoutFortificationReservation(1));
            Equal(0, ProceduralLayoutValidator.Validate(input, layout).Count, c.Name + " validator with bonus fields");
            HashSet<GridPoint> bonus = new HashSet<GridPoint>(layout.BonusFields);
            True(!bonus.Overlaps(layout.Corridors.SelectMany(k => k.Cells)) && !bonus.Overlaps(layout.Rooms.SelectMany(r => r.Bounds.Points())) &&
                !bonus.Overlaps(layout.FieldWall.Fields), c.Name + " bonus crops stay off streets, rooms and the required field");
            True(layout.BonusFields.All(b => c.Terrain[b].Has(TerrainTraits.Fertile) && layout.Enclosure.Contains(b)), c.Name + " fertile enclosed crops");
            planted += layout.BonusFields.Count;
            Console.WriteLine("  " + c.Name + " innerYardCrops=" + layout.BonusFields.Count + " requiredField=" + layout.FieldWall.Fields.Count);
            ExportLayout("yard-" + c.Name, result, c.Terrain);
            if (layout.BonusFields.Count > 0)
            {
                GridPoint street = layout.Corridors.SelectMany(k => k.Cells).First();
                Contains(ProceduralLayoutValidator.Validate(input, layout.WithBonusFields(layout.BonusFields.Concat(new[] { street }))),
                    "bonus_field_illegal_cell");
            }
        }
        Console.WriteLine("  inner-yard crop cells over joint cases=" + planted);
    }
}
