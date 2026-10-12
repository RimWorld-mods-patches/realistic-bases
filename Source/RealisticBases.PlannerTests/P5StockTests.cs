using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static class P5StockTests
{
    public static void Epochs()
    {
        var absent = new P5StockEpoch();
        Check(absent.Decide(P5StockRequest.Inspect, true, false, false, -1, 10).Action == P5StockAction.None);
        var acquired = absent.Decide(P5StockRequest.Acquire, true, false, false, -1, 10);
        Check(acquired.Action == P5StockAction.GenerateOnce && acquired.State.Epoch == 1);
        // No count input is intentional: a present empty holder is authoritative.
        var empty = absent.Decide(P5StockRequest.Acquire, true, true, true, 2, 10);
        Check(empty.Action == P5StockAction.AdoptExisting && empty.State.GeneratedTick == 2);
        Check(empty.State.Decide(P5StockRequest.Acquire, true, true, true, 2, 10).Action == P5StockAction.None);
        Check(empty.State.Decide(P5StockRequest.Acquire, true, false, true, 2, 10).Action == P5StockAction.Inconsistent);
        Check(absent.Decide(P5StockRequest.Acquire, true, false, true, 2, 10).Action == P5StockAction.Inconsistent);
        Check(absent.Decide(P5StockRequest.Acquire, false, false, false, -1, 10).Action == P5StockAction.Unsupported);
        int boundary = 2 + P5StockEpoch.RestockTicks;
        Check(empty.State.Decide(P5StockRequest.ScheduledExpiry, true, true, true, 2, boundary).Action == P5StockAction.None);
        var loaded = empty.State.Decide(P5StockRequest.EnterEncounter, true, true, true, 2, 10).State;
        Check(loaded.Decide(P5StockRequest.ScheduledExpiry, true, true, true, 2, 11).Action == P5StockAction.None);
        Check(loaded.Decide(P5StockRequest.ScheduledExpiry, true, true, true, 2, boundary + 1).Action == P5StockAction.SuppressExpiry);
        var expired = empty.State.Decide(P5StockRequest.ScheduledExpiry, true, true, true, 2, boundary + 1);
        Check(expired.Action == P5StockAction.ExpireOffMap && expired.State.Epoch == 1);
        var next = expired.State.Decide(P5StockRequest.Acquire, true, false, true, 2, boundary + 2);
        Check(next.Action == P5StockAction.GenerateOnce && next.State.Epoch == 2);
        var legacy = absent.Decide(P5StockRequest.Acquire, true, false, true, 2, boundary + 2);
        Check(legacy.Action == P5StockAction.GenerateOnce && legacy.Reason == "adopt_legacy_expiry");
        var claimed = loaded.Decide(P5StockRequest.Claim, true, true, true, 2, 10).State;
        Check(!claimed.InEncounter && claimed.Status == P5StockStatus.Claimed);
        Check(claimed.Decide(P5StockRequest.Acquire, true, false, true, 2, boundary + 2).Action == P5StockAction.None);
        Check(loaded.Decide(P5StockRequest.LeaveEncounter, true, true, true, 2, 11).State.GeneratedTick == 2);
        Check(absent.Epoch == 0 && absent.Status == P5StockStatus.Uninitialized);
    }

    public static void Conservation()
    {
        var source = new List<P5ItemFacts> { new P5ItemFacts(1, "Medicine", 100, allocation: P5Allocation.Operating),
            new P5ItemFacts(2, "Medicine", 40), new P5ItemFacts(3, "Gun", 1, "Steel", 4, 70, 100) };
        var stock = new P5StockSnapshot(source);
        source.Clear();
        Check(stock.Items.Count == 3 && stock.Items[2].Quality == 4 && stock.Items[2].HitPoints == 70);
        var bought = stock.Remove(2, 10, P5Allocation.Sale);
        var used = bought.Remove(1, 5, P5Allocation.Operating);
        Check(used.Count("Medicine", P5Allocation.Sale) == 30 && used.Count("Medicine", P5Allocation.Operating) == 95);
        var emergency = used.Split(2, 4, 5, P5Allocation.Operating).Remove(4, 5, P5Allocation.Operating);
        Check(emergency.Count("Medicine", P5Allocation.Sale) == 25 && emergency.Count("Medicine", P5Allocation.Operating) == 95);
        var split = emergency.Split(1, 5, 30, P5Allocation.Operating);
        var merged = split.Merge(5, 1, 30, 100);
        Check(merged.Count("Medicine", P5Allocation.Operating) == 95 && merged.Items.All(i => i.Id != 5));
        Check(stock.Count("Medicine", P5Allocation.Sale) == 40 && stock.Count("Medicine", P5Allocation.Operating) == 100);
        Reject(() => stock.Remove(1, 5, P5Allocation.Sale));
        Reject(() => stock.Remove(2, 41, P5Allocation.Sale));
        Reject(() => stock.Split(1, 2, 1, P5Allocation.Sale));
        Reject(() => stock.Merge(1, 2, 1, 150));
        Reject(() => stock.Transfer(99, P5Allocation.Operating));
        Reject(() => new P5StockSnapshot(new[] { stock.Items[0], stock.Items[0] }));
        Check(stock.Transfer(2, P5Allocation.Operating).Count("Medicine", P5Allocation.Sale) == 0);
        Check(stock.Remove(2, 40, P5Allocation.Sale).Count("Medicine", P5Allocation.Sale) == 0);
        // Seeded split/merge/partial-buy depletion sequence; no manufacture or refill.
        var rng = new Random(81231);
        var sequence = new P5StockSnapshot(new[] { new P5ItemFacts(10, "Steel", 75) });
        long removed = 0;
        for (int id = 11; id < 150; id++)
        {
            var item = sequence.Items.FirstOrDefault();
            if (item == null) break;
            if (item.Count > 1 && rng.Next(2) == 0)
                sequence = sequence.Split(item.Id, id, rng.Next(1, item.Count), P5Allocation.Sale);
            else
            {
                int quantity = rng.Next(1, item.Count + 1);
                sequence = sequence.Remove(item.Id, quantity, P5Allocation.Sale); removed += quantity;
            }
            Check(sequence.Count("Steel", P5Allocation.Sale) + removed == 75);
        }
    }
    public static void Capacity()
    {
        var snapshot = new P5StockSnapshot(new[] {
            new P5ItemFacts(1, "Steel", 639, stackLimit: 75, marketValue: 1.9),
            new P5ItemFacts(2, "Gun", 1, quality: 3, hitPoints: 70, maxHitPoints: 100, stackLimit: 1, marketValue: 250),
            new P5ItemFacts(3, "Meat", 1532, stackLimit: 75, marketValue: 2, storage: P5StorageKind.Perishable),
            new P5ItemFacts(4, "Shell", 31, stackLimit: 25, marketValue: 80, storage: P5StorageKind.Hazardous) });
        Check(snapshot.StorageCells(P5StorageKind.Ordinary) == 10);
        Check(snapshot.StorageCells(P5StorageKind.Perishable) == 21);
        Check(snapshot.StorageCells(P5StorageKind.Hazardous) == 2);
        Check(Math.Abs(snapshot.Value(P5Allocation.Sale) - (639 * 1.9 + 250 + 1532 * 2 + 31 * 80)) < 0.001);
        var divided = snapshot.Split(1, 5, 75, P5Allocation.Operating);
        Check(divided.Items.Single(i => i.Id == 5).StackLimit == 75);
        Check(divided.StorageCells(P5StorageKind.Ordinary) == 10);
        Check(Math.Abs(divided.Value(P5Allocation.Sale) + divided.Value(P5Allocation.Operating) - snapshot.Value(P5Allocation.Sale)) < 0.001);
    }
    // Unload/re-entry never refills: re-entry spawns min(planned, remainder).
    public static void ReserveLedger()
    {
        Check(P5ReserveLedger.Key("Steel") == "Steel" && P5ReserveLedger.Key("Gun_Revolver", "Steel") == "Gun_Revolver|Steel");
        var planned = new[] { new KeyValuePair<string, int>("MealSimple", 84), new KeyValuePair<string, int>("Shell_HighExplosive", 6),
            new KeyValuePair<string, int>("Shell_HighExplosive", 6), new KeyValuePair<string, int>("WoodLog", 120) };
        var observed = new Dictionary<string, int> { ["MealSimple"] = 40, ["Shell_HighExplosive"] = 30, ["WoodLog"] = 0 };
        P5ReserveLedger ledger = P5ReserveLedger.Observe(planned, k => observed.TryGetValue(k, out int v) ? v : 0);
        Check(ledger.Remaining["MealSimple"] == 40);
        Check(ledger.Remaining["Shell_HighExplosive"] == 12);   // stray extra shells never become reserves
        Check(ledger.Remaining["WoodLog"] == 0);
        Check(ledger.Cap("MealSimple", 84) == 40 && ledger.Cap("MealSimple", 30) == 30);
        Check(ledger.Cap("WoodLog", 120) == 0);                 // exhausted stays exhausted
        Check(ledger.Cap("Steel", 75) == 75);                   // not generated last time: planned amount
        P5ReserveLedger again = P5ReserveLedger.Observe(new[] { new KeyValuePair<string, int>("MealSimple", ledger.Cap("MealSimple", 84)) },
            k => 40);
        Check(again.Remaining["MealSimple"] == 40);             // a second unload without use keeps the same balance
        // Visit sequence: exhausted on visit 1, so visit 2 grants and records 0;
        // stray same-def items dropped later never refill it on visit 3.
        var visit1 = P5ReserveLedger.Observe(new[] { new KeyValuePair<string, int>("WoodLog", 100) }, k => 0);
        int visit2Grant = visit1.Grant("WoodLog", 100, new Dictionary<string, int>());
        var visit2 = P5ReserveLedger.Observe(new[] { new KeyValuePair<string, int>("WoodLog", visit2Grant) }, k => 300);
        Check(visit2Grant == 0 && visit2.Remaining["WoodLog"] == 0 && visit2.Grant("WoodLog", 100, new Dictionary<string, int>()) == 0);
                // Two planned stocks of one key share a single remaining allowance.
        var shared = new P5ReserveLedger(new[] { new KeyValuePair<string, int>("WoodLog", 100) });
        var granted = new Dictionary<string, int>();
        Check(shared.Grant("WoodLog", 80, granted) == 80 && shared.Grant("WoodLog", 50, granted) == 20 &&
            shared.Grant("WoodLog", 10, granted) == 0 && granted["WoodLog"] == 100);
        Check(shared.Grant("Steel", 40, granted) == 40 && !granted.ContainsKey("Steel"));
        // Allocated merchandise is a separate authoritative physical object,
        // not a refill of a depleted generated allowance or a new planned key.
        var allocated = new P5StockSnapshot(new[] { new P5ItemFacts(91, "ReinforcedBarrel", 3),
            new P5ItemFacts(92, "MedicineHerbal", 25, hitPoints: 40, maxHitPoints: 60) })
            .Transfer(91, P5Allocation.Operating).Transfer(92, P5Allocation.Operating);
        var emptyBarrels = new P5ReserveLedger(new[] { new KeyValuePair<string, int>("ReinforcedBarrel", 0) });
        Check(emptyBarrels.Grant("ReinforcedBarrel", 2, new Dictionary<string, int>()) == 0);
        Check(allocated.Count("ReinforcedBarrel", P5Allocation.Operating) == 3 &&
            allocated.Count("MedicineHerbal", P5Allocation.Operating) == 25 &&
            allocated.Items.Single(i => i.Id == 92).HitPoints == 40);
        Check(allocated.Remove(91, 1, P5Allocation.Operating).Count("ReinforcedBarrel", P5Allocation.Operating) == 2);
                Reject(() => new P5ReserveLedger(new[] { new KeyValuePair<string, int>("Steel", -1) }));
        Reject(() => new P5ReserveLedger(new[] { new KeyValuePair<string, int>("Steel", 1), new KeyValuePair<string, int>("Steel", 2) }));
        try { ledger.Cap("Steel", -1); throw new Exception("negative plan accepted"); } catch (ArgumentOutOfRangeException) { }
    }

    private static void Check(bool condition) { if (!condition) throw new Exception("P5 stock contract failed"); }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Invalid stock operation accepted");
    }
}
