using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RealisticBases.AI;
using RealisticBases.Core;
using RealisticBases.Generation;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Integration
{
    // References to the actual physical sale objects, never a second quantity
    // or ThingOwner. The native holder retains unsupported/untransferred goods.
    public sealed class P5PhysicalSaleStock : MapComponent
    {
        private Settlement owner;
        private int factionId = -1;
        private List<Thing> sale = new List<Thing>();
        // Operating-reserve defs this settlement relies on (fuel, ammunition,
        // reload materials). Food and medicine are matched by category.
        private List<string> operatingDefs = new List<string>();
        private static Game cacheGame;
        private static readonly Dictionary<Thing, P5PhysicalSaleStock> cache = new Dictionary<Thing, P5PhysicalSaleStock>();
        public P5PhysicalSaleStock(Map map) : base(map) { }
        internal bool Active => owner != null && owner.Faction?.loadID == factionId && !owner.Faction.IsPlayer && map.Parent == owner;
        internal IEnumerable<Thing> Goods => Active ? sale.Where(StillOwned) : Enumerable.Empty<Thing>();

        internal static P5PhysicalSaleStock For(Settlement_TraderTracker tracker)
        {
            var result = tracker?.settlement?.Map?.GetComponent<P5PhysicalSaleStock>();
            return result?.Active == true && result.owner == tracker.settlement ? result : null;
        }
        internal static P5PhysicalSaleStock Tracked(Thing thing)
        {
            if (thing == null) return null;
            EnsureCache();
            return cache.TryGetValue(thing, out var result) && result.Active ? result : null;
        }
        private static void EnsureCache()
        {
            if (ReferenceEquals(cacheGame, Current.Game)) return;
            cacheGame = Current.Game; cache.Clear();
            if (Current.Game == null) return;
            foreach (Map current in Find.Maps)
            {
                var component = current.GetComponent<P5PhysicalSaleStock>();
                if (component?.sale != null)
                    foreach (Thing thing in component.sale.Where(t => t != null && !t.Destroyed)) cache[thing] = component;
            }
        }
        private bool StillOwned(Thing thing)
        {
            if (thing == null || thing.Destroyed) return false;
            if (thing.Spawned) return thing.Map == map;
            IThingHolder holder = thing.ParentHolder;
            while (holder != null)
            {
                if (holder is Pawn_InventoryTracker inventory)
                    return inventory.pawn.Map == map && inventory.pawn.Faction?.loadID == factionId && !inventory.pawn.IsPrisoner;
                if (holder is Pawn_CarryTracker carry)
                    return carry.pawn.Map == map && carry.pawn.Faction?.loadID == factionId && !carry.pawn.IsPrisoner;
                holder = holder.ParentHolder;
            }
            return false;
        }
        internal void Register(Thing thing)
        {
            EnsureCache();
            if (!sale.Contains(thing)) sale.Add(thing);
            cache[thing] = this;
        }
        internal void Prune()
        {
            foreach (Thing thing in sale.Where(t => !StillOwned(t)).ToArray())
            {
                sale.Remove(thing); cache.Remove(thing);
                foreach (var lord in map.lordManager.lords) lord.extraForbiddenThings.Remove(thing);
            }
        }
        private void Clear()
        {
            foreach (Thing thing in sale)
            {
                cache.Remove(thing);
                foreach (var lord in map.lordManager.lords) lord.extraForbiddenThings.Remove(thing);
            }
            sale.Clear(); owner = null; factionId = -1;
        }
        internal void Protect()
        {
            if (!Active) return;
            foreach (var lord in map.lordManager.lords.Where(l => l.faction?.loadID == factionId && l.LordJob is LordJob_P3Garrison))
                foreach (Thing thing in Goods)
                    if (!lord.extraForbiddenThings.Contains(thing)) lord.extraForbiddenThings.Add(thing);
        }
        public override void MapComponentTick()
        {
            if (owner == null || GenTicks.TicksGame % 250 != 0) return;
            if (!Active)
            {
                var record = P5TradeStock.Record(owner.trader);
                if (record != null) record.status = Planning.P5StockStatus.Claimed;
                Clear(); return;
            }
            Prune(); Protect();
            if (GenTicks.TicksGame % 2500 == 0) EmergencyTransfer();
        }

        // Emergency allocation transfer: when the residents' own reserve of a
        // category is exhausted on the map, one sale stack of that category
        // stops being merchandise first (removed from the offer, unforbidden),
        // and only then becomes available for operation. Never the reverse.
        internal int EmergencyTransfer()
        {
            if (!Active) return 0;
            int transferred = 0;
            foreach (IGrouping<string, Thing> group in Goods.Where(t => t.Spawned).GroupBy(Category).Where(g => g.Key != null).ToList())
            {
                if (OperatingAvailable(group.Key)) continue;
                Thing stack = group.OrderBy(t => t.thingIDNumber).First();
                Release(stack);
                transferred++;
                P0Log.Message("P5_EMERGENCY_TRANSFER", "map=" + map.uniqueID + " settlement=" + owner.ID +
                    " category=" + group.Key + " thing=" + stack.ThingID + " count=" + stack.stackCount +
                    " remainingSaleStacks=" + sale.Count + " offerReduced=True");
            }
            return transferred;
        }

        private string Category(Thing thing)
        {
            if (thing.def.IsMedicine) return "medicine";
            if (thing.def.IsNutritionGivingIngestible && thing.def.ingestible.HumanEdible &&
                !thing.def.IsDrug && thing.def.ingestible.preferability >= FoodPreferability.RawBad) return "food";
            return operatingDefs.Contains(thing.def.defName) ? "def:" + thing.def.defName : null;
        }

        private bool OperatingAvailable(string category)
        {
            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (thing.Spawned && thing.def.category == ThingCategory.Item && Tracked(thing) == null &&
                    Category(thing) == category) return true;
            }
            return false;
        }

        private void Release(Thing thing)
        {
            sale.Remove(thing); cache.Remove(thing);
            foreach (var lord in map.lordManager.lords) lord.extraForbiddenThings.Remove(thing);
        }

        internal void RecordOperatingDefs(IEnumerable<string> defs)
        {
            foreach (string def in defs.Distinct()) if (!operatingDefs.Contains(def)) operatingDefs.Add(def);
        }

        // Pre-teardown boundary (MapDeiniter.Deinit prefix, before pawns pass to
        // the world): every unsold physical sale object returns to the native
        // holder as itself. Nothing is cloned, merged or destroyed; re-entry
        // materializes the same objects again from the canonical holder.
        internal void ReturnToNative()
        {
            if (!Active) return;
            ThingOwner<Thing> holder = P5TradeStock.Holder(owner.trader);
            if (holder == null) throw new InvalidOperationException("Canonical native holder missing at unload");
            Prune();
            int returned = 0, units = 0;
            foreach (Thing thing in sale.ToArray())
            {
                if (thing.Spawned) thing.DeSpawn();
                else thing.holdingOwner?.Remove(thing);
                Release(thing);
                if (!holder.TryAdd(thing, canMergeWithExistingStacks: false))
                    throw new InvalidOperationException("Native holder refused returned sale stock " + thing.ThingID);
                returned++; units += thing.stackCount;
            }
            P0Log.Message("P5_SALE_RETURNED", "map=" + map.uniqueID + " settlement=" + owner.ID + " stacks=" + returned +
                " units=" + units + " nativeStacks=" + holder.Count + " copies=0");
            sale.Clear(); owner = null; factionId = -1;
        }

        // The ordinary generation path: explicit, guarded native acquisition.
        // Failures keep the native offer (and native-only trading) unchanged.
        internal static Planning.P5StockSnapshot AcquireForGeneration(Settlement settlement)
        {
            if (!P5TradeStock.Supported(settlement)) return null;
            try
            {
                Planning.P5StockSnapshot snapshot = P5TradeStock.Acquire(settlement);
                P0Log.Message("P5_SALE_SNAPSHOT", "settlement=" + settlement.ID + " items=" + snapshot.Items.Count +
                    " ordinaryStacks=" + snapshot.StorageCells(Planning.P5StorageKind.Ordinary) +
                    " perishableStacks=" + snapshot.StorageCells(Planning.P5StorageKind.Perishable) +
                    " hazardousStacks=" + snapshot.StorageCells(Planning.P5StorageKind.Hazardous) +
                    " saleValue=" + snapshot.Value(Planning.P5Allocation.Sale).ToString("F0"));
                return snapshot;
            }
            catch (Exception ex)
            {
                P0Log.Message("P5_SALE_NATIVE_ONLY", "settlement=" + settlement.ID + " reason=" + ex.Message);
                return null;
            }
        }

        // Materialize every supported ordinary item of the canonical offer
        // inside the generation transaction. Perishables use food storage.
        internal List<Thing> Materialize(P4SpawnTransaction tx, Settlement settlement, IEnumerable<IntVec3> storage,
            IEnumerable<IntVec3> food, Planning.P5StockSnapshot planned)
        {
            ThingOwner<Thing> holder = P5TradeStock.Holder(settlement.trader);
            List<Thing> supported = holder == null ? new List<Thing>() :
                holder.InnerListForReading.Where(t => P5TradeStock.Unsupported(t) == null).OrderBy(t => t.thingIDNumber).ToList();
            HashSet<int> plannedIds = new HashSet<int>(planned.Items.Select(i => i.Id));
            if (supported.Any(t => !plannedIds.Contains(t.thingIDNumber)) || supported.Count != planned.Items.Count)
                throw new InvalidOperationException("Native offer changed between planning and spawning");
            if (supported.Count == 0) return new List<Thing>();
            List<IntVec3> storageCells = storage.ToList(), foodCells = food.ToList();
            try
            {
                return Transfer(tx, settlement, supported, t => t.TryGetComp<CompRottable>() != null ? foodCells : storageCells);
            }
            catch (P5StorageCapacityException ex)
            {
                // Preflight fails before any ownership change: the whole offer
                // stays native and sellable rather than being truncated.
                P0Log.Message("P5_SALE_NATIVE_ONLY", "settlement=" + settlement.ID + " reason=" + ex.Message);
                return new List<Thing>();
            }
        }
        public override void ExposeData()
        {
            Scribe_References.Look(ref owner, "p5SaleOwner");
            Scribe_Values.Look(ref factionId, "p5SaleFaction", -1);
            Scribe_Collections.Look(ref sale, "p5PhysicalSaleGoods", LookMode.Reference);
            Scribe_Collections.Look(ref operatingDefs, "p5OperatingDefs", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                sale = sale?.Where(t => t != null && !t.Destroyed).ToList() ?? new List<Thing>();
                operatingDefs = operatingDefs ?? new List<string>();
            }
        }
        public override void FinalizeInit()
        {
            foreach (Thing thing in sale.ToArray()) Register(thing);
            Protect();
        }

        // Capacity is preflighted for every selected original Thing before any
        // ownership changes. Caller must supply legal planned storage cells.
        internal List<Thing> Transfer(P4SpawnTransaction tx, Settlement settlement, IEnumerable<Thing> selected,
            IEnumerable<IntVec3> storageCells) => Transfer(tx, settlement, selected, t => storageCells);

        internal List<Thing> Transfer(P4SpawnTransaction tx, Settlement settlement, IEnumerable<Thing> selected,
            Func<Thing, IEnumerable<IntVec3>> storageFor)
        {
            if (owner != null || sale.Count != 0) throw new InvalidOperationException("Physical stock already initialized");
            var holder = P5TradeStock.Holder(settlement.trader);
            var originals = selected.ToList();
            if (originals.Count == 0 || originals.Distinct().Count() != originals.Count ||
                originals.Any(t => !holder.Contains(t) || P5TradeStock.Unsupported(t) != null))
                throw new InvalidOperationException("Unsupported or unowned physical stock transfer");
            var used = new Dictionary<IntVec3, int>();
            var locations = new Dictionary<Thing, List<IntVec3>>();
            foreach (Thing thing in originals)
            {
                var cells = storageFor(thing).Distinct().ToList();
                foreach (IntVec3 c in cells)
                    if (!used.ContainsKey(c)) used[c] = c.InBounds(map) ? c.GetThingList(map).Count(t => t.def.category == ThingCategory.Item) : int.MaxValue;
                int stacks = checked((int)(((long)thing.stackCount + thing.def.stackLimit - 1) / thing.def.stackLimit));
                var slots = new List<IntVec3>();
                for (int i = 0; i < stacks; i++)
                {
                    IntVec3? selectedSlot = cells.Where(c =>
                    {
                        if (!c.InBounds(map)) return false;
                        var shelf = c.GetEdifice(map) as Building_Storage;
                        bool legal = shelf == null ? c.Standable(map) && c.GetEdifice(map) == null :
                            shelf.Faction == settlement.Faction && c.Walkable(map) && shelf.GetStoreSettings().AllowedToAccept(thing);
                        return legal && used[c] < (shelf == null ? 1 : c.GetMaxItemsAllowedInCell(map));
                    }).Select(c => (IntVec3?)c).FirstOrDefault();
                    if (!selectedSlot.HasValue)
                        throw new P5StorageCapacityException("Full published stock cannot fit legal storage: " + thing.ThingID);
                    IntVec3 slot = selectedSlot.Value;
                    used[slot]++; slots.Add(slot);
                }
                locations.Add(thing, slots);
            }
            owner = settlement; factionId = settlement.Faction.loadID;
            tx.OnRollback(Clear);
            var placed = new List<Thing>();
            foreach (Thing original in originals)
            {
                int count = original.stackCount;
                tx.Borrow(original, holder);
                var fragments = new List<Thing>();
                tx.OnRollback(() =>
                {
                    foreach (Thing fragment in fragments)
                    {
                        if (fragment.Destroyed) throw new InvalidOperationException("Borrowed stock fragment lost");
                        if (fragment.Spawned) fragment.DeSpawn();
                        fragment.holdingOwner?.Remove(fragment);
                        if (!original.TryAbsorbStack(fragment, respectStackLimit: false) || !fragment.Destroyed)
                            throw new InvalidOperationException("Borrowed stock fragment cannot be restored");
                    }
                    if (original.stackCount != count) throw new InvalidOperationException("Borrowed stock count recovery failed");
                });
                Register(original);
                int index = 0;
                while (original.stackCount > original.def.stackLimit)
                {
                    Thing fragment = original.SplitOff(original.def.stackLimit);
                    fragments.Add(fragment);
                    GenSpawn.Spawn(fragment, locations[original][index++], map);
                    Register(fragment); placed.Add(fragment);
                }
                GenSpawn.Spawn(original, locations[original][index], map);
                placed.Add(original);
            }
            Protect();
            return placed;
        }
    }

    internal sealed class P5StorageCapacityException : InvalidOperationException
    {
        internal P5StorageCapacityException(string message) : base(message) { }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.CanStackWith))]
    internal static class P5SaleAllocationMergePatch
    {
        private static void Postfix(Thing __instance, Thing other, ref bool __result)
        {
            if (__result && P5PhysicalSaleStock.Tracked(__instance) != P5PhysicalSaleStock.Tracked(other)) __result = false;
        }
    }
    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    internal static class P5SaleSplitPatch
    {
        private static void Prefix(Thing __instance, out P5PhysicalSaleStock __state) => __state = P5PhysicalSaleStock.Tracked(__instance);
        private static void Postfix(Thing __instance, Thing __result, P5PhysicalSaleStock __state)
        {
            if (__state != null && __result != null && __result != __instance) __state.Register(__result);
        }
    }
    [HarmonyPatch(typeof(CompressibilityDeciderUtility), nameof(CompressibilityDeciderUtility.IsSaveCompressible))]
    internal static class P5SaleCompressionPatch
    {
        private static bool Prefix(Thing t, ref bool __result)
        {
            if (P5PhysicalSaleStock.Tracked(t) == null) return true;
            __result = false; return false;
        }
    }
    [HarmonyPatch(typeof(Settlement_TraderTracker), nameof(Settlement_TraderTracker.GiveSoldThingToPlayer))]
    internal static class P5SaleNativePurchasePatch
    {
        private static void Prefix(Thing toGive, out P5PhysicalSaleStock __state) => __state = P5PhysicalSaleStock.Tracked(toGive);
        private static void Postfix(P5PhysicalSaleStock __state) => __state?.Prune();
    }
}
