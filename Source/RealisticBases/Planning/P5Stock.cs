using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public enum P5StockStatus { Uninitialized, Canonical, Expired, Claimed }
    public enum P5StockRequest { Inspect, Acquire, EnterEncounter, LeaveEncounter, ScheduledExpiry, Claim }
    public enum P5StockAction { None, AdoptExisting, GenerateOnce, ExpireOffMap, SuppressExpiry, Unsupported, Inconsistent }

    // This is policy over copied facts, never a lazy engine stock getter.
    public sealed class P5StockEpoch
    {
        public const int RestockTicks = 30 * 60000;
        public P5StockEpoch(P5StockStatus status = P5StockStatus.Uninitialized, int epoch = 0,
            int generatedTick = -1, bool inEncounter = false)
        {
            if (epoch < 0 || generatedTick < -1) throw new ArgumentOutOfRangeException();
            Status = status; Epoch = epoch; GeneratedTick = generatedTick; InEncounter = inEncounter;
        }
        public P5StockStatus Status { get; }
        public int Epoch { get; }
        public int GeneratedTick { get; }
        public bool InEncounter { get; }

        public P5StockDecision Decide(P5StockRequest request, bool supported, bool nativeHolder,
            bool nativeEverGenerated, int nativeGeneratedTick, int now)
        {
            if (now < 0 || nativeGeneratedTick < -1) throw new ArgumentOutOfRangeException();
            if (!supported) return Result(P5StockAction.Unsupported, this, "unsupported_tracker");
            if (request == P5StockRequest.Claim)
                return Result(P5StockAction.None, new P5StockEpoch(P5StockStatus.Claimed, Epoch, GeneratedTick, false), "claimed");
            if (Status == P5StockStatus.Claimed) return Result(P5StockAction.None, this, "ownership_ended");
            if (request == P5StockRequest.EnterEncounter || request == P5StockRequest.LeaveEncounter)
                return Result(P5StockAction.None, new P5StockEpoch(Status, Epoch, GeneratedTick,
                    request == P5StockRequest.EnterEncounter), "encounter_transition");
            if (request == P5StockRequest.Inspect) return Result(P5StockAction.None, this, "read_only");
            if (request == P5StockRequest.ScheduledExpiry)
            {
                if (InEncounter) return Result(P5StockAction.SuppressExpiry, this, "loaded_encounter");
                if (Status != P5StockStatus.Canonical || GeneratedTick < 0 ||
                    (long)now - GeneratedTick <= RestockTicks) return Result(P5StockAction.None, this, "not_due");
                return Result(P5StockAction.ExpireOffMap,
                    new P5StockEpoch(P5StockStatus.Expired, Epoch, GeneratedTick), "scheduled_off_map_expiry");
            }
            if (Status == P5StockStatus.Canonical)
                return Result(nativeHolder ? P5StockAction.None : P5StockAction.Inconsistent,
                    this, nativeHolder ? "reuse_including_empty" : "canonical_holder_missing");
            if (Status == P5StockStatus.Expired && nativeHolder)
                return Result(P5StockAction.Inconsistent, this, "expired_holder_still_present");
            if (Status == P5StockStatus.Uninitialized && nativeHolder)
                return Result(P5StockAction.AdoptExisting, new P5StockEpoch(P5StockStatus.Canonical,
                    checked(Epoch + 1), nativeGeneratedTick, InEncounter), "adopt_including_empty");
            // Legacy native stock may already have expired before the adapter existed.
            // An unexplained absent holder is not permission to reroll a known offer.
            bool legacyExpiry = nativeEverGenerated && nativeGeneratedTick >= 0 &&
                (long)now - nativeGeneratedTick > RestockTicks;
            if (Status == P5StockStatus.Uninitialized && nativeEverGenerated && !legacyExpiry)
                return Result(P5StockAction.Inconsistent, this, "unexplained_native_absence");
            if (InEncounter && (Status == P5StockStatus.Expired || legacyExpiry))
                return Result(P5StockAction.SuppressExpiry, this, "no_encounter_restock");
            return Result(P5StockAction.GenerateOnce, new P5StockEpoch(P5StockStatus.Canonical,
                checked(Epoch + 1), now, InEncounter), legacyExpiry ? "adopt_legacy_expiry" : "explicit_acquisition");
        }
        private static P5StockDecision Result(P5StockAction action, P5StockEpoch state, string reason)
            => new P5StockDecision(action, state, reason);
    }

    public sealed class P5StockDecision
    {
        internal P5StockDecision(P5StockAction action, P5StockEpoch state, string reason)
        { Action = action; State = state; Reason = reason; }
        public P5StockAction Action { get; }
        public P5StockEpoch State { get; }
        public string Reason { get; }
    }

    public enum P5Allocation { Operating, Sale }

    public sealed class P5ItemFacts
    {
        public P5ItemFacts(int id, string def, int count, string stuff = null, int quality = -1,
            int hitPoints = -1, int maxHitPoints = -1, P5Allocation allocation = P5Allocation.Sale)
        {
            if (id < 0 || string.IsNullOrWhiteSpace(def) || count <= 0 || quality < -1 || quality > 6 ||
                hitPoints < -1 || maxHitPoints < -1 || hitPoints > maxHitPoints)
                throw new ArgumentException("Invalid physical item facts");
            Id = id; Def = def; Count = count; Stuff = stuff ?? ""; Quality = quality;
            HitPoints = hitPoints; MaxHitPoints = maxHitPoints; Allocation = allocation;
        }
        public int Id { get; }
        public string Def { get; }
        public int Count { get; }
        public string Stuff { get; }
        public int Quality { get; }
        public int HitPoints { get; }
        public int MaxHitPoints { get; }
        public P5Allocation Allocation { get; }
        public bool SameAttributes(P5ItemFacts other) => other != null && Def == other.Def &&
            Stuff == other.Stuff && Quality == other.Quality && HitPoints == other.HitPoints &&
            MaxHitPoints == other.MaxHitPoints;
        public P5ItemFacts With(int id, int count, P5Allocation allocation)
            => new P5ItemFacts(id, Def, count, Stuff, Quality, HitPoints, MaxHitPoints, allocation);
    }

    // Exclusive stack allocation avoids publishing reserve units via vanilla's
    // full-stack trade view. Runtime merges across purposes must be refused.
    public sealed class P5StockSnapshot
    {
        public P5StockSnapshot(IEnumerable<P5ItemFacts> items)
        {
            List<P5ItemFacts> copy = (items ?? throw new ArgumentNullException(nameof(items)))
                .OrderBy(i => i.Id).ToList();
            if (copy.Select(i => i.Id).Distinct().Count() != copy.Count)
                throw new ArgumentException("Duplicate physical identity");
            Items = new ReadOnlyCollection<P5ItemFacts>(copy);
        }
        public IReadOnlyList<P5ItemFacts> Items { get; }
        public long Count(string def, P5Allocation allocation) => Items.Where(i => i.Def == def &&
            i.Allocation == allocation).Sum(i => (long)i.Count);
        public P5StockSnapshot Split(int sourceId, int newId, int count, P5Allocation allocation)
        {
            P5ItemFacts source = Items.Single(i => i.Id == sourceId);
            if (count <= 0 || count >= source.Count || Items.Any(i => i.Id == newId))
                throw new ArgumentException("Split must preserve two distinct positive stacks");
            return new P5StockSnapshot(Items.Where(i => i.Id != sourceId).Concat(new[] {
                source.With(sourceId, source.Count - count, source.Allocation), source.With(newId, count, allocation) }));
        }
        public P5StockSnapshot Remove(int id, int count, P5Allocation requiredAllocation)
        {
            P5ItemFacts item = Items.Single(i => i.Id == id);
            if (count <= 0 || count > item.Count || item.Allocation != requiredAllocation)
                throw new ArgumentException("Removal exceeds the authorized allocation");
            return new P5StockSnapshot(Items.Where(i => i.Id != id).Concat(count == item.Count
                ? Enumerable.Empty<P5ItemFacts>() : new[] { item.With(id, item.Count - count, item.Allocation) }));
        }
        public P5StockSnapshot Merge(int sourceId, int targetId, int count, int stackLimit)
        {
            P5ItemFacts source = Items.Single(i => i.Id == sourceId), target = Items.Single(i => i.Id == targetId);
            if (sourceId == targetId || !source.SameAttributes(target) || source.Allocation != target.Allocation ||
                count <= 0 || count > source.Count || (long)target.Count + count > stackLimit)
                throw new ArgumentException("Incompatible merge or quantity");
            var rest = Items.Where(i => i.Id != sourceId && i.Id != targetId).ToList();
            rest.Add(target.With(targetId, checked(target.Count + count), target.Allocation));
            if (count < source.Count) rest.Add(source.With(sourceId, source.Count - count, source.Allocation));
            return new P5StockSnapshot(rest);
        }
        public P5StockSnapshot Transfer(int id, P5Allocation allocation)
        {
            if (!Items.Any(i => i.Id == id)) throw new ArgumentException("Missing physical identity");
            return new P5StockSnapshot(Items.Select(i => i.Id == id ? i.With(i.Id, i.Count, allocation) : i));
        }
    }
}
