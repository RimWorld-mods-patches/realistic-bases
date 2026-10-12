using System;
using System.Collections.Generic;
using RealisticBases.Planning;

internal static class P5ReadinessTests
{
    public static void Food()
    {
        Check(P5Readiness.FoodUnits(7, 1.6, 0.05) == 840);
        Check(P5Readiness.FoodUnits(7, 1.6, 0.9, 84) == 84);
        Check(P5Readiness.FoodUnits(4, 1.6, 0.05, 1280) == 1280);
        foreach (int residents in new[] { 4, 7, 9, 10, 12 })
            foreach (double nutrition in new[] { 0.05, 0.9, 1.8 })
                Check(P5Readiness.FoodUnits(residents, 1.6, nutrition) * nutrition + 0.00001 >= residents * 6.0);
    }
    public static void Combat()
    {
        var guns = new[] { new P5GunDuty("m1", "HE", 2, 1, 1), new P5GunDuty("m2", "HE", 2, 1, 1),
            new P5GunDuty("auto", "Steel", 20, 2, 180), new P5GunDuty("mini", "Steel", 20, 0.5, 60),
            new P5GunDuty("uranium", "Uranium", 20, 2, 60) };
        var insufficient = new P5CombatCoverage(guns, new Dictionary<string, int> { ["HE"] = 10, ["Steel"] = 75, ["Uranium"] = 60 });
        Check(!insufficient.Covered && insufficient.Inputs[0].Shortfall == 12);
        Check(insufficient.Inputs[1].Available == 315 && insufficient.Inputs[1].Demand == 300);
        var sufficient = new P5CombatCoverage(guns, new Dictionary<string, int> { ["HE"] = 22, ["Steel"] = 75, ["Uranium"] = 180 });
        Check(sufficient.Covered);
        // Shared loose stock is counted once, not copied for each gun.
        var shared = new P5CombatCoverage(new[] { new P5GunDuty("a", "Steel", 20, 1, 0),
            new P5GunDuty("b", "Steel", 20, 1, 0) }, new Dictionary<string, int> { ["Steel"] = 120 });
        Check(!shared.Covered && shared.Inputs[0].Shortfall == 120);
    }
    public static void Outage()
    {
        var actual = new P5OutageCoverage(650, 0, 300, 1);
        Check(actual.Covered && actual.RequiredWattDays == 163.75);
        Check(!new P5OutageCoverage(2000, 0, 300, 1).Covered);
        Check(!new P5OutageCoverage(2000, 0, 0, 0).Covered);
        Check(!new P5OutageCoverage(650, 0, 300, 1, reserveFloorWattDays: 200).Covered);
        Check(new P5OutageCoverage(2000, 2005, 0, 1).Covered);
    }
    private static void Check(bool value) { if (!value) throw new Exception("P5 readiness contract failed"); }
}
