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
        }
        public override void ExposeData()
        {
            Scribe_References.Look(ref owner, "p5SaleOwner");
            Scribe_Values.Look(ref factionId, "p5SaleFaction", -1);
            Scribe_Collections.Look(ref sale, "p5PhysicalSaleGoods", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) sale = sale?.Where(t => t != null && !t.Destroyed).ToList() ?? new List<Thing>();
        }
        public override void FinalizeInit()
        {
            foreach (Thing thing in sale.ToArray()) Register(thing);
            Protect();
        }

        // Capacity is preflighted for every selected original Thing before any
        // ownership changes. Caller must supply legal planned storage cells.
        internal List<Thing> Transfer(P4SpawnTransaction tx, Settlement settlement, IEnumerable<Thing> selected,
            IEnumerable<IntVec3> storageCells)
        {
            if (owner != null || sale.Count != 0) throw new InvalidOperationException("Physical stock already initialized");
            var holder = P5TradeStock.Holder(settlement.trader);
            var originals = selected.ToList();
            if (originals.Count == 0 || originals.Distinct().Count() != originals.Count ||
                originals.Any(t => !holder.Contains(t) || P5TradeStock.Unsupported(t) != null))
                throw new InvalidOperationException("Unsupported or unowned physical stock transfer");
            var cells = storageCells.Distinct().ToList();
            var used = cells.ToDictionary(c => c, c => c.GetThingList(map).Count(t => t.def.category == ThingCategory.Item));
            var locations = new Dictionary<Thing, List<IntVec3>>();
            foreach (Thing thing in originals)
            {
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
                        throw new InvalidOperationException("Full published stock cannot fit legal storage: " + thing.ThingID);
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
