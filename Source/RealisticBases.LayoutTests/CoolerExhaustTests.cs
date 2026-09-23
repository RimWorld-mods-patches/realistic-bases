using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// Cooler orientation and exhaust (docs/plans/2026-09-23-cooler-exhaust.md).
internal static partial class Program
{
    private static void RunCoolerExhaustTests()
    {
        Run("cooler rotation matches vanilla hot and cold sides", CoolerRotationMatchesVanilla);
    }

    // Vanilla Building_Cooler: cold = Position + South.RotatedBy(rot), hot = Position + North.RotatedBy(rot).
    private static GridPoint VanillaHot(GridPoint cell, string rotation)
    {
        switch (rotation)
        {
            case "East": return new GridPoint(cell.X + 1, cell.Z);
            case "South": return new GridPoint(cell.X, cell.Z - 1);
            case "West": return new GridPoint(cell.X - 1, cell.Z);
            default: return new GridPoint(cell.X, cell.Z + 1);
        }
    }

    private static GridPoint VanillaCold(GridPoint cell, string rotation)
    {
        GridPoint hot = VanillaHot(cell, rotation);
        return new GridPoint(2 * cell.X - hot.X, 2 * cell.Z - hot.Z);
    }

    private static List<ProceduralLayoutResult> CoolerLayouts()
    {
        List<ProceduralLayoutResult> results = new List<ProceduralLayoutResult>();
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            // Hot industrial settlements: a freezer plus wall coolers in every living room.
            ProceduralLayoutResult result = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Industrial, Hot), seed);
            Accepted(result);
            results.Add(result);
        }
        return results;
    }

    private static void CoolerRotationMatchesVanilla()
    {
        HashSet<string> rotations = new HashSet<string>();
        int coolers = 0;
        foreach (ProceduralLayoutResult result in CoolerLayouts())
            foreach (LayoutRoom room in result.Layout.Rooms)
                foreach (LayoutFixture cooler in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.WallCooler))
                {
                    coolers++;
                    rotations.Add(cooler.Rotation);
                    GridPoint cell = cooler.Cells[0];
                    GridPoint cold = VanillaCold(cell, cooler.Rotation);
                    GridPoint hot = VanillaHot(cell, cooler.Rotation);
                    True(room.Interior.Contains(cold), room.Id + " cold side " + cold + " must be inside the cooled room (rotation " + cooler.Rotation + ")");
                    True(!room.Bounds.Contains(hot), room.Id + " hot side " + hot + " must be outside the cooled room (rotation " + cooler.Rotation + ")");
                    Equal(cold, cooler.ColdCell.Value, room.Id + " ColdCell agrees with vanilla");
                    Equal(hot, cooler.HotCell.Value, room.Id + " HotCell agrees with vanilla");
                    Equal(hot, cooler.Exhaust.Value, room.Id + " exhaust is the vanilla hot cell");
                }
        True(coolers >= 8, "hot-climate layouts place coolers: " + coolers);
        True(rotations.Count == 4, "all four rotations exercised: " + string.Join(",", rotations.OrderBy(r => r)));
    }
}
