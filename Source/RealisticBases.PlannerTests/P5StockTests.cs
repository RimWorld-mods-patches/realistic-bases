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
    private static void Check(bool condition) { if (!condition) throw new Exception("P5 stock contract failed"); }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Invalid stock operation accepted");
    }
}
