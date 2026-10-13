# MVP supplies, settlement trade stock, and deferred sustainability

2026-09-20 · User-authorized scope revision · Design only; not yet implemented

This updates the current design and P5 acceptance criteria. It does not retrospectively change P0–P2 test results. P3 remains the next implementation milestone; this document does not authorize starting P5 early.

## 1. Encounter readiness replaces long-term self-sufficiency as the MVP gate

Players are expected to visit enemy settlement maps for encounters rather than run them for seasons. The MVP should therefore have plausible facilities, finite adequate stocks, and best-effort support behavior for a reasonable visit. Full sustainability remains a goal after the MVP.

Provisional defaults, chosen for implementation planning rather than measured from player behavior:

- **Routine horizon: three in-game days.** Include residents' food, spoilage, temperature control, generator/cooking fuel, essential tending supplies and relevant needs. This spans multiple day/night cycles without requiring a season-long test.
- **Combat horizon: six in-game hours within that visit**, at explicitly declared firing duty for the installed equipment. Do not equate time on map with continuous maximum-rate firing. Include shells, reload materials, barrels and critical electrical loads where applicable.
- A power-outage scenario is separate from combat duty. State its real load, duration, initial charge, reserve floor and permitted load shedding. A test at a lower load is evidence for that load, not the higher modeled case.

Calibrate these targets from real encounters and expose/profile them coherently. They are not new user-specified hard numbers. Larger existing reserves are allowed. Initial reserves may fund the whole encounter even if no crop matures or shell is manufactured. Any production credited toward coverage needs demonstrated inputs, throughput and available labor; otherwise its output is a bonus, not a promise.

Run representative bounded encounter tests and negative tests. Full two-harvest and winter self-sufficiency tests are no longer required for MVP acceptance. Essential hauling, refueling, reload support and needs relief must still function. If stocked raw ingredients require cooking, that cooking path must work. Reserve exhaustion, destroyed infrastructure and losses remain real outcomes, without free refill or immunity.

## 2. Two stock purposes, one physical quantity

**Operating reserves** fund residents and defense. **Trade stock** is the sale inventory this settlement actually offers. Maintain separate allocations/value budgets and appropriate warehouse/shelf storage for each; they may share item types but cannot share the same counted units.

Example: 100 medicine units reserved for operation plus 40 offered for sale means 140 units physically accounted for, not two labels on 100 units. If the player buys 10 sale units, 30 sale units and 100 reserve units remain. If defenders then consume 5 reserve units, 125 total remain. If emergency use instead draws from sale stock, first transfer the allocation and remove the consumed units from the offer. This is an illustrative accounting example, not a balance recommendation.

Sale goods should reflect the settlement's current trade inventory, including imports and player sales, rather than arbitrary extra loot. A tribal trader can carry an imported industrial item without making that item part of its normal production or defensive equipment. Local resources/capabilities shape operating reserves and any initial trade generation; they must not rewrite already existing offered stock into an unrelated palette.

## 3. Proposed integration contract

1. **Establish stock explicitly.** Reuse current authoritative trader stock if it exists. For an unvisited settlement, initialize a supported offer once through the game/adapter's stock-generation path and persist its identity/epoch. Pure planning receives an immutable snapshot; it must not call a getter that creates/rerolls stock.
2. **Match supported ordinary goods.** Preserve item type, count, stuff, quality and condition where relevant. Materialize goods into appropriate storage or transfer existing Things through an adapter. One authoritative ownership record must back both the trade view and map goods; never retain two independent physical copies. Exact engine hooks are an implementation spike, not an assumed existing API.
3. **Reconcile events.** Purchases remove sale goods; player sales add eligible goods; consumption/looting/destruction remove them. Splits, merges, partial trades and capture update identities/counts. Save/load and unload/re-entry preserve those results. No last-item/empty-container access may regenerate a depleted offer.
4. **Handle restocking deliberately.** Preserve the ordinary settlement trade abstraction outside an active encounter, with explicit stock epochs and ownership transitions. Loading a map, opening a report, becoming hostile or emptying a warehouse is not a restock event. Suppress restock/refill during a loaded assault or siege, including a scheduled restock boundary, and prevent re-entry from granting another copy. Off-map production, supply convoys and an economic simulation are not necessary for stock consistency.
5. **Bound storage and value coherently.** Include sale goods in loot/value assessment separately from reserves, and adapt storage/capacity to an existing offer. If a supported stock limit changes the offer, change its authoritative trade representation coherently before publication. Do not multiply stocks by difficulty or silently materialize only an arbitrary fraction of already offered goods while claiming exact fidelity. Report unsupported cases.
6. **Keep ordinary trade authorization.** The adapter does not make a hostile settlement tradeable. It uses any existing stock and the same faction/trader identity; hostility must not reroll previously observed merchandise. A trader-less faction may receive a documented specialization-based stock approximation, clearly marked as such and with no invented actual offer.

MVP exact matching initially covers supported ordinary item Things, including standard equipment and resources. Live trade pawns/animals, exotic modded containers and unsupported trader replacements need separate ownership adapters; preserve their original handling and explicitly exclude them from physical stock claims rather than cloning or deleting them. Full-category fidelity is a documented post-MVP extension. A rejected/unknown integration must be visible in diagnostics.

Validation cases: existing versus never-generated stock; empty stock; buy then attack; sell then attack; emergency allocation transfer; loot/destroy then revisit; partial trades; stack splitting; save/load; hostility change; capture; and scheduled restock during an encounter. Check quantity conservation and operating-reserve separation, not just visual resemblance.

## 4. Source evidence and open implementation questions

The local Settlement_TraderTracker (decompiled `RimWorld.Planet.Settlement_TraderTracker.cs`) owns a `ThingOwner`, serializes stock, and moves quantities through buy/sell methods. Its `StockListForReading` getter regenerates stock when absent **or empty**, and `TraderTrackerTick` expires stock on a timed restock boundary. Its initial generation passes trader kind, tile and faction to `ThingSetMakerDefOf.TraderStock`.

This directly supports using actual settlement merchandise as an input. It also means naïvely copying the list, emptying its holder, or calling the getter every generation can reroll or duplicate goods. The adapter must establish ownership, empty-stock suppression and restock reconciliation together. How to safely expose map-owned goods to the vanilla trade methods, and how loaded goods deteriorate versus off-map stock, remain explicit implementation questions. No runtime trade integration has been tested in this documentation revision.

## 5. Post-MVP backlog

| Deferred work | Future completion evidence |
|---|---|
| Full local food self-sufficiency | Repeated crop cycles, seasonal survival, preservation losses and actual labor/throughput balance |
| Sustained power/fuel economy | Seasonal demand/output, renewable/fuel replenishment and extended failure/recharge tests |
| Continuous ammunition/manufacturing supply | Real recipes, ingredients, labor and production matching declared long-term demand |
| Full workforce optimization | Sustainable schedules across needs, defense, production, repair and prisoner support |
| Off-map economic growth and deliveries | Time/capacity/cost accounting, interrupted trade routes and no siege refill |
| Broader trade-stock materialization | Tested pawn/animal identity, special containers and modded-trader adapters |

Not deferred by this revision: no traps/IEDs, finite real supplies, essential encounter support, valid facilities/paths, measured electrical/thermal behavior, stock conservation, capture ownership and no loot/resident duplication. Those remain MVP correctness requirements.
