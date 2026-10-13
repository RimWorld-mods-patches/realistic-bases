using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static class P5EncounterArchiveTests
{
    // The 2026-10-13 live failure: same-tick re-entry substituted residents and
    // native generation added personal meals (46 -> 57 -> 52).
    public static void Roster()
    {
        var world = new Dictionary<int, P5ResidentStatus>
        {
            [94775] = new P5ResidentStatus(true, false, true, false),
            [94786] = new P5ResidentStatus(true, true, true, false),     // killed
            [94798] = new P5ResidentStatus(true, false, true, true),     // captured by the player
            [94807] = new P5ResidentStatus(true, false, false, false),   // left the faction
            [94817] = new P5ResidentStatus(true, false, true, false),
        };
        var fates = P5RosterRestore.Resolve(new[] { 94775, 94786, 94798, 94807, 94817, 94817, 99999 },
            id => world.TryGetValue(id, out var s) ? s : new P5ResidentStatus(false, false, false, false));
        Check(fates.Select(f => f.Key).SequenceEqual(new[] { 94775, 94786, 94798, 94807, 94817, 99999 }));
        Check(fates.Where(f => f.Value == P5ResidentFate.Restored).Select(f => f.Key).SequenceEqual(new[] { 94775, 94817 }));
        Check(fates.Single(f => f.Key == 94786).Value == P5ResidentFate.Dead);
        Check(fates.Single(f => f.Key == 94798).Value == P5ResidentFate.Captured);
        Check(fates.Single(f => f.Key == 94807).Value == P5ResidentFate.Departed);
        Check(fates.Single(f => f.Key == 99999).Value == P5ResidentFate.Missing);
        Check(P5RosterRestore.Resolve(null, id => default).Count == 0);

        // Carried goods return with their residents, so the loose ledger only
        // records loose stock. Two same-tick revisits keep the total constant.
        int loose = 46, carried = 11, total = loose + carried;
        for (int visit = 0; visit < 2; visit++)
        {
            var ledger = P5ReserveLedger.Observe(new[] { new KeyValuePair<string, int>("MealSimple", 46) }, k => loose);
            loose = ledger.Grant("MealSimple", 46, new Dictionary<string, int>());
            Check(loose + carried == total);
        }
    }

    public static void Assets()
    {
        string key = P5AssetSnapshot.KeyFor("Battery", 12, 34);
        Check(key == "Battery@12,34");
        var battery = new P5AssetSnapshot(key, -1, -1, 300, null, 0);
        Check(battery.RestoredHitPoints(100) == 100);                   // undamaged stays full
        Check(battery.RestoredEnergy(600) == 300 && battery.RestoredEnergy(200) == 200);
        Check(battery.RestoredFuel(75) == -1);                          // not a fuelled device
        var generator = new P5AssetSnapshot("WoodFiredGenerator@1,1", 40, 75.4f, -1, null, 0);
        Check(Math.Abs(generator.RestoredFuel(75) - 75) < 1e-6);        // capped at capacity
        Check(Math.Abs(new P5AssetSnapshot("G@1,1", -1, 60.4f, -1, null, 0).RestoredFuel(75) - 60.4f) < 1e-4);
        Check(generator.RestoredHitPoints(300) == 40 && generator.RestoredHitPoints(20) == 20);
        Check(new P5AssetSnapshot("Wall@0,0", 0, -1, -1, null, 0).RestoredHitPoints(300) == 1);
        var mortar = new P5AssetSnapshot("Turret_Mortar@5,5", -1, 80, -1, "Shell_HighExplosive", 1);
        Check(mortar.ShellDef == "Shell_HighExplosive" && mortar.ShellCount == 1);
        var empty = new P5AssetSnapshot("Turret_Mortar@6,6", -1, 80, -1, "Shell_HighExplosive", 0);
        Check(empty.ShellDef == null && empty.ShellCount == 0);         // nothing loaded, nothing granted
        bool threw = false;
        try { new P5AssetSnapshot("", -1, -1, -1, null, 0); } catch (ArgumentException) { threw = true; }
        Check(threw);
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("P5.4 archive check failed");
    }
}
