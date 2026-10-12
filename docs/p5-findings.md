# P5 findings and evidence

2026-10-12 · Active implementation, **not complete**.

## P5.0 — Baseline

Start: clean P4E `be39af9`, preserving `847fcb5` refund-free floor undo, exact
physical-item recovery and restoration of the pre-existing roof-collapse queue.
Main was inspected read-only: its P5 text matches this branch; its newer separate
P5A list is retained. Main's older P4E status does not supersede tested closeout.
The supplied user instruction explicitly authorizes P5 now; historical "do not
start early" scope wording is not an active prohibition.

Build/live/API shared skills and consumer configuration read. Installed-source
baseline is external `Assembly-CSharp-1.6.4871`, matching the P4E final clean
startup on RimWorld1.6.4871rev597. Docker29.2.1 is available. Exact disposable
profile process check found no running game. No install or normal-profile edits.

### Native stock authority

`Settlement_TraderTracker` owns private `ThingOwner<Thing> stock`, generation
tick and `everGeneratedStock`; all serialize. `StockListForReading` regenerates
when null **or empty**. `GetDirectlyHeldThings` reads the actual holder without
generation. `RegenerateStock` destroys old stock, generates with trader kind,
tile/faction, marks pawns as world pawns and sets the generation tick. Trader kind
comes from the faction's declared base trader kinds, not a specialization guess.

`GiveSoldThingToTrader` initializes when null, SplitOffs the player's real Thing,
calls PreTraded and adds it without merging. Human pawns have a distinct path.
`GiveSoldThingToPlayer` SplitOffs the offered real Thing, calls PreTraded and moves
it to caravan inventory (or adds a pawn); failed native inventory placement can
destroy it. Native TradeDeal resolves sales before other transfers. No copying
adapter has been assumed safe.

`TraderTrackerTick` calls TryDestroyStock once elapsed time is strictly greater
than30days. `TryDestroyStock` removes/destroys non-pawns and nulls the holder.
`NextRestockTick` exposes the boundary; empty stock is not a legitimate new epoch.
`Settlement.Goods` uses the lazy getter, while buy/sell delegate to the tracker.
CanTradeNow/visit commands retain native faction/permit authorization; the adapter
must not grant hostile trading.

### Native lifecycle and existing limitations

`Settlement.ShouldRemoveMapNow` allows removal when player blocking pawns,
buildings and incoming transporters are absent. `MapDeiniter.Deinit` passes pawns
to the world first, later clears map references/listers. Current P0 MapRemoved
only records one wood cell, one wall flag and survivor IDs; the procedural
spawner generates a new roster and fixtures. This is not P5 unload persistence.
Native SettlementDefeatUtility replaces the object with DestroyedSettlement and
destroys the original when no active human threats remain; preserve that terminal
lifecycle rather than respawning defeated settlements.

Existing generation funds three-day generator/primitive fuel, profile food and
shared12HE/2incendiary/2spare barrels for up to3 mortars. P4E native rearm, medicine,
hazards and reload proofs remain valid but do not quantify the new whole-roster,
whole-equipment horizons. Larger P5A shell minimums remain a separate request.

## Evidence ledger

- P5.0: source inspection and clean baseline/process/Docker checks only.
- No P5 build, pure transition test, native trade, horizon or revisit pass yet.

## P5.1a — Native ownership and epoch spike

Immutable copied item facts and a pure epoch/conservation policy now cover
absent/existing/empty acquisition, strict30day expiry, loaded suppression,
off-map transition, capture termination, reserve/sale isolation, partial removal,
split/merge and emergency allocation transfer. Seeded depletion never refills.
These pure operations do not call native lazy getters or create merchandise.

The scoped runtime spike explicitly acquires only an exact supported native
Settlement/Settlement_TraderTracker. It saves epochs; canonical empty reads do
not invoke RegenerateStock, and scheduled expiry pauses while the map is loaded.
The tracker remains the **sole holder**, with no map materialization yet. No
ordinary planner or arrival automatically initializes offers at this checkpoint.
Unsupported animals/pawns, nested minified containers and unknown behavior are
reported and remain native. Trade authorization was not changed.

Disposable existing P4E map1/site91 at2611 had absent stock (-1 generation tick).
Explicit native generation acquired epoch1 once:64 ordinary item stacks,
8930 units;84 total Things including13 minified containers and7 animals.
Repeat acquisition returned action=None with unchanged IDs/attributes/counts.
Temporarily detaching and finally restoring those exact84 Things allowed two
real empty-getter reads: zero regeneration/physical copies. A synchronous clock
boundary at30days+1tick suppressed native expiry for the loaded map; clock and
all Things restored. This is a boundary test, **not**30days of measured operation.

First active reload preserved all ordinary quantities/IDs but the back-reference
audit failed: vanilla deep-loads ThingOwner with no constructor owner and does
not serialize that owner. The scoped P5 LoadedGame recovery now rebinds only an
ownerless native holder belonging to a managed exact tracker. It neither moves
nor clones stock; conflicting owners fail visibly. Corrected cold startup and
audit at2612 preserve all64/8930, epoch1/generated2611, nativeOwnership=True,
including quality/stuff/HP. Animals remain original world-pawn stock references.

Evidence: p5-native-stock-spike-20261012.log (includes failed reload audit),
p5-stock-owner-reload-20261012.log (corrected cold reload), saved
RealisticBases-P5-stock-spike-20261012.rws, p5-stock-planner-tests-20261012.txt.
Release build0 warnings/errors and complete planner/garrison suite pass.
Relevant layout full suite is deferred to integrated layout changes; none were
made here. The exact disposable processes were stopped before every build.

This is P5.1a only. Native buy/sell and off-map restock execution, physical
ownership integration/rollback, horizon/readiness, support and unloaded
persistence remain open. No P5 completion or full-category fidelity claim.

## P5.2a — Quantified readiness audit (not horizon acceptance)

Pure nutrition, shared whole-defense ammunition and actual-load outage models
pass in the complete planner/garrison suite. Food calculations use nutrition,
preserve larger existing minima and credit no production. Gun demand aggregates
all installed identities against each shared finite input exactly once. Outage
accounting includes native5W self-discharge per battery and does not let a
clamp-to-zero pass an exhausted system. Release build0 warnings/errors.

The runtime audit copies current P3-owned operating stocks plus residents' held
items, excluding the native sale holder. It records real roster hunger, ready
food/spoilage at current temperature, weapon fuel/difficulty multipliers,
per-shot plus per-burst consumption, connected loads/charge and fuel. The first
audit failed because the mortar uses per-shot rather than per-burst fuel; that
failed attempt is preserved, not counted as acceptance.

Corrected native ten-resident map1 at2612: nutrition/day16.80, ready/stable food
133.20 versus required63.00 for three days with1.25safety. Five installed guns
(one mortar and four direct guns) cover six hours at2mortar shots/gun/hour and
20direct projectiles/gun/hour: HE12/12, barrel charges40/12, Steel840/720.
These are current-stock calculations, **not** observed horizon operation.

One actual power net draws7175W; stored charge649.79Wd cannot bridge six hours
with zero generation, even at reserve floor0. No lower-load success substitutes
for that deficit. Seven wood-consuming devices require462 units for three more
days; current loaded99.80 + loose357 =456.80, a5.20 deficit consistent with prior
operation. Future horizon tests must name their start tick and elapsed time;
inspection does not reset or refill their reserves. Fuel readiness/support and
full-load outage remain open rather than declared universally sufficient.

Evidence: p5-readiness-planner-tests-20261012.txt;
p5-readiness-baseline-20261012.log (failed audit);
p5-readiness-audit-corrected-20261012.log (actual quantified baseline).
Usage-limit approval review briefly blocked one test invocation; it was retried
normally after the user's continuation. No bypass or unexecuted test is counted.

## P5.2b — Needs relief and bed use (validation in progress)

An attempted long routine test exposed real starvation/post sleeping after one
day despite stocked food. The initial setup was invalid for routine-only proof:
vanilla rejects Guest status between hostile factions, so the stand-in remained
an attacker and the controller stayed HoldSectors. That failed attempt consumed
real finite ammunition and is retained, not counted as routine acceptance.
At92594 Karen68415 was starving on a Wait_Combat post; other defenders used
LayDown on ground at their post coordinates. Failure snapshot:
RealisticBases-P5-routine-needs-failure-20261012.rws; first log:
p5-routine-three-days-first-20261012.log.

Pure reproduction:40 valid post options with ten hungry funded residents.
Old relief compared healthy bodies against half of all geometric posts, denying
everybody relief. New policy uses actual funded bodies, releases a rotating
bounded subset, retains two on duty under threat and one awake routine watch.
Unsafe relief remains blocked. An awake watch also preserves native defeat
semantics, which do not count sleeping pawns as active threats.

Relief now uses native SatisfyBasicNeeds with a whole-settlement160-cell route
check, not a14-cell radius that excludes the food store/barracks. Post/mortar/
support/fallback needs cannot select LayDown instead of relief. Earlier food/rest
thresholds release residents before exhaustion; role changes interrupt a post
wait or ground sleep, not a progressing eating/refueling job. Ongoing native bed
rest remains relief until substantially recovered. No player settings, needs,
roster, stocks or research are granted/changed by these production rules.

The opt-in disposable routine probe requires all original residents to eat and
sleep in actual Building_Bed targets, real fuel increases and180000 advanced
ticks with no sampled starvation/downing/loss. Paused wall time is not credited.
An active probe reload is explicitly interrupted, never credited as a complete
measurement. A saved pre-probe point allows repetition. Test actions resolve a
unique supported encounter after a reload switches the current view to the home
map; ambiguous targets fail closed and the resolved map is logged.

The corrected captive-stand-in trial reached167851ticks (2.8days): all10 ate and
slept in real beds,7 actual refuel jobs/increases, no starvation/downing/loss and
no sampled cooler outage; minimum food0.088/rest0.214. It failed its full horizon
because the disposable captive visitor died, not because a resident failed.
That fixture failure remains preserved in p5-routine-beds-three-days-20261012.log
and RealisticBases-P5-beds-visitor-fixture-failure-20261012.rws. No three-day pass
is claimed from it. Food133.20→84.60 and fuel456.80→63.94 demonstrate actual
depletion, not refill. Some native direct-fire ammunition was consumed; a new
six-hour combat budget must not be assumed still intact after routine operation.

Final routine instrumentation instead parks the same visitor in the disposable
home map and explicitly holds only this running probe's ordinary settlement map
against automatic unloading. This is logged as disposableMapHold=True. Native
defeat remains untouched; outside this opt-in probe the removal method runs
unchanged. No new pawn, stock, needs, research or generator output is added.
The visitor is restored if alive at completion. This tests NPC routine operation, **not**
native removal/revisit, which remains an independent P5.4 gate.

Complete planner/garrison suite passes including the new deadlock/watch and
full-offer storage/value fixtures; Release build0 warnings/errors. Native first
night at53868 shows nine distinct real Bed targets and one awake Wait_Combat
watch, rather than ground sleeping at posts. Full layout suite also passes:
failures0/819875ms, p5-relief-full-layout-20261012.txt.

The final explicit map-hold trial **passes** at182613, elapsed180001ticks from
2612: all10 original residents ate and slept in actual beds,7 native refuel
jobs/physical fuel increases, missing/starving/downed samples0, sampled cooler
power losses0, unchanged native settlement parent. Minimum food0.090/rest0.198.
No NPC orders, new stocks or credited future production. Food133.20→79.20,
generator fuel456.80→41.69 and loose wood0 show real depletion. At the end,
remaining six-hour modeled inputs are still HE12/12, barrel charges40/12 and
Steel840/720. This is **not yet** a measured six-hour combat-duty pass.

Active save/reload182613→182614 retains the same ten resident IDs, all20 named
operating item-stack IDs/counts/quality/condition/positions, food79.20, HE12,
warehouse spare barrel identity and epoch1's64/8930 native sale inventory. The
raw item endpoint also lists787 natural stone chunks: vanilla compression
recreates their IDs on load while preserving def/count/position. The initial
all825-ID comparison therefore failed and is not claimed as an exact-ID pass;
the operating-item comparison is separate. This also warns the future physical
trade adapter to preserve serialization identity for any supported compressible
sale item, rather than assuming all native map Things retain their IDs.

Evidence: p5-routine-map-hold-three-days-20261012.log;
p5-routine-three-days-before/after-reload-items-20261012.json and home/encounter
pawn snapshots; RealisticBases-P5-three-days-beds-20261012.rws. The parked
disposable visitor is absent from the final spawned roster; its survival is not
a gate for this isolated NPC operation test or evidence for player protection.
Restore the untouched baseline before the next independent trade probe. The
exact disposable game is stopped. P5 remains open for other profiles, measured
combat/outage/tending/failures, canonical physical trade and unload persistence.

## P5.1b — Native partial buy/sell prototype

The canonical ownership hook is now exercised through actual Core TradeDeal,
not a replacement pricing/transfer routine. The untouched disposable reset has
three existing colonists. The first setup selected a pawn that failed native
negotiation checks and was refused before stock acquisition; it is preserved in
p5-native-partial-trade-first-20261012.log, not counted as a pass. Selection now
requires native CanTradeWith, enabled Social/trade stats and normal caravan
trade authorization. No skill, faction relation, permit or money is granted.

Successful case at583: original Human185 negotiates from caravan91 at ordinary
friendly Industrial settlement0. Existing home silver funds the wallet through
a real physical transfer; testCurrencyAdded=0. Explicit native stock acquisition
generates epoch1 once. Native purchase7steel then sale3 yields offer526→522 and
caravan+4. Combined physical silver remains2608. Native holder is sole owner;
no operating reserve or map copy is created. Ordinary stack count65→67 reflects
native non-merging transaction fragments, not multiplied quantities. The18
unsupported native animals/containers retain their native ownership.

Cold load followed by active save/reload583→584→585 preserves67 ordinary
stacks, exact ID/def/count/stuff/quality/condition fingerprint61809FC698FE832B,
steel522, playerSteel4, silver2608, original negotiator185, epoch1/generated583,
and nativeOwnership=True. The audit reads the real holder without its lazy
getter. Current holder back-reference recovery also passes on these85 total
stock Things. Native trade authorization remains unchanged and no attack has
yet been performed. This establishes the hook/ownership prototype before the
physical adapter, not P5.3/full trade fidelity or buy-then-attack acceptance.

Evidence: p5-native-partial-trade-negotiator-20261012.log;
p5-native-partial-trade-reload-20261012.log;
RealisticBases-P5-native-partial-trade.rws. Release build0 warnings/errors.
The exact disposable game was stopped. Full layout/planner passes remain the
preceding checkpoint; this adds only isolated debug instrumentation, not layout
or garrison changes. Physical stock/capacity, unmanaged-existing-offer adoption,
allocation transfers, loot/capture/expiry and unload reconciliation remain open.

## P5.3a — Single-authority physical-stock foundation

Normal generation does **not** yet materialize trade stock. An isolated explicit
prototype transfers only inert native steel/medicine into existing legal shelves.
The full offer's perishable/hazard storage and adaptive planner capacity remain
open; prototypeSlice=True/fullOfferMaterialized=False are logged deliberately.

The map component holds references, not cloned Things, a second ThingOwner or
saved independent quantities. Getter views combine actual native and actual
map-owned objects, rejecting duplicate IDs. Native stock is borrowed through
P4's transaction; oversized native stacks split through the engine and rollback
recombines them into their original objects/counts before returning ownership.
Sale/reserve native merges are refused, split allocation follows real fragments,
NPC Lords forbid consuming sale goods without a later explicit transfer, and
native purchases prune objects that leave settlement ownership. Unsupported
holder/components remain native. Map-owned sale references serialize; a scoped
compression guard preserves managed sale IDs because vanilla's compression
reference scan does not inspect custom map-component references. An actual
compressible-sale fixture is still pending; ordinary shelf reload passes below.

Native map1/site91 at2612: forced failure after borrowing and splitting original
steel/medicine recovers=True, exactNativeThings=True, quantitiesAttributes=True,
itemLedgerRestored=True and restores the prior roof queue (discarded0). No source
Thing is destroyed/replaced. The subsequent commit moves original Steel75313:639
and MedicineIndustrial75321:35 to11 legal shelf stacks, retaining original IDs
for one fragment each, and removes both original native-holder copies. Physical
units674 plus untouched native goods still total8930 ordinary units, identical
def/stuff/quality/condition aggregateCD6EF4C790E86AEC. Native holder82 contains
the remaining62 ordinary and20 unsupported Things; the combined view has no
duplicate physical/native identity. Nine new fragment IDs represent splits, not
additional units or arbitrary offer truncation.

Active and cold reload retain all11 mapped IDs/counts/positions and the aggregate.
Native TryAbsorbStack with respectStackLimit=False cannot merge saleSteel75313:39
into operatingSteel68292:75; both counts remain unchanged. Temporarily emptying
the private native holder leaves the11 mapped stacks visible and does not reroll;
all82 original native Things return exactly. A synchronous30day+1 expiry boundary
remains suppressed while loaded, epoch1 intact. Before the boundary, the ordinary
native tick now still runs its pawn cleanup; only due loaded expiry is skipped.
Full pure suite and Release build0 warnings/errors pass. No new layout geometry
or ordinary-generation stock feature is enabled by this checkpoint.

Evidence: p5-physical-stock-transfer-first-20261012.log;
p5-physical-stock-merge-expiry-20261012.log;
p5-physical-stock-planner-tests-20261012.txt;
RealisticBases-P5-inert-physical-stock-20261012.rws. Exact game stopped. P5.3
remains open for full-offer admission/storage, emergency transfer, buy/sell/loot/
destruction/capture/re-entry and the complete restock/compatibility matrix.

## P5.3b/P5.4-stock — Full-offer materialization and stock persistence (implemented, live verification pending)

2026-10-12, branch `claude/p5-stock-completion` (from `main` `966d7a3`, layout
engine v2 default). The scope was agreed with the user: the trade offer and stock
persistence across unload/re-entry. Survivor and casualty identity keeps the
existing coarse P0 behaviour. **Only pure/build evidence exists.** Nothing below
has been observed in a running game. Codex verifies live.

What is implemented:

- **Normal generation materializes the canonical offer.** For an ordinary funded settlement, `P4AMapPlanner.Run` explicitly acquires the native offer (`P5PhysicalSaleStock.AcquireForGeneration` → `P5TradeStock.Acquire`) before planning.
  - The settlement's planning requirements (`LayoutRequirements`) gain `SaleStorageStacks` (ordinary + hazardous) and `SalePerishableStacks`, added to warehouse and freezer/pantry capacity. They enter the requirements digest only when non-zero.
  - After the operating stocks, the spawner moves every supported ordinary item from the native holder into those rooms through `Materialize`. This happens inside the same spawn transaction, using Codex's exact borrow/split/rollback `Transfer`. Perishables go to food storage.
  - The planned snapshot must equal the holder's supported items at spawn, or generation fails rather than silently diverging.
  - If legal storage cannot take the whole offer, the capacity preflight (before any ownership change) keeps the **entire** offer native and sellable (`P5_SALE_NATIVE_ONLY`) instead of truncating it.
  - Quest sites, legacy four-resident plans, unsupported trackers and unsupported Things (pawns, animals, nested containers, unknown behaviour) stay native, as before.
- **Emergency allocation transfer.** Every 2500 ticks, when the residents' own stock of a category is exhausted on the map, one sale stack of that category is released, and only then becomes usable. It leaves the offer, the lord's forbidden list and the sale cache (`P5_EMERGENCY_TRANSFER`).
  - Categories: medicine, human food, and the exact operating defs (fuel, shells, barrels, reload materials) recorded at generation.
- **Unload returns sale goods.** A `MapDeiniter.Deinit` prefix runs before pawns pass to the world.
  - Every still-owned physical sale object, including items in resident inventories, returns to the native holder as itself, unmerged (`P5_SALE_RETURNED ... copies=0`).
  - Defeated or claimed settlements are inactive and keep their goods as map loot.
  - Re-entry acquires the same canonical epoch (no regeneration) and materializes the same objects again.
- **Operating-reserve ledger.** The spawner records each operating reserve (generator fuel, primitive fuel wood, medicine, components and armoury by def|stuff, mortar shells and barrels, turret reload stock, startup food) at the amount it actually granted, **including zero**.
  - At unload (`P5_RESERVE_LEDGER`) the remainder per key is recorded: loose non-sale items, resident inventories and fuel loaded in generators. It is capped at what was granted, so player-dropped items never become reserves.
  - Re-entry grants at most that remainder, as one allowance per key shared by every stock of that key (`P5_STOCK_SPAWN capped=`). Off-map balances stay frozen.

Pure evidence:

- PlannerTests `P5 reserve ledger caps re-entry at the unload remainder` covers the remainder cap, the shared-key allowance, an exhausted reserve staying exhausted across three visits despite stray items, and invalid ledgers.
- LayoutTests `P5 sale offer is sized into storage without changing other inputs` covers digest invariance with no offer, warehouse and freezer growth, accepted joint-site plans whose stock cells cover operating plus sale stacks, and quest camps refusing an offer.
- Full PlannerTests and LayoutTests 127/127 pass, and the Release build has 0 warnings and 0 errors.

Known limitations, deliberately not claimed:

- Turret and mortar loaded charge and the residents' personal gear reset on re-entry (the roster is regenerated, per P0).
- Hazardous sale goods use ordinary warehouse cells, not a separate hazard store.
- Sale-goods spoilage while loaded is native behaviour.
- The six-hour measured combat duty and the full-load outage deficit recorded in P5.2a remain open.

### Live verification checklist for Codex (disposable profile)

New debug actions, category "Realistic Bases P5":
- "Audit P5 physical sale stock and reserves"
- "P5 run emergency allocation transfer now"
- "P5 unload and re-enter current settlement" (real `DeinitAndRemoveMap` plus regeneration; refuses while player pawns are on the map).

1. **Fresh ordinary funded settlement** (`Generate playable flat P4 settlement`). Expect:
   - `P5_SALE_SNAPSHOT` before `PROCEDURAL_PLAN saleStacks=a+b`.
   - `P5_STOCK_SPAWN saleStacks>0 nativeOnly=False`.
   - Audit: `duplicateIdentity=False`, with physical plus native units equal to the snapshot units and fingerprint.
   - Goods on legal shelves or cells, perishables in the freezer.
   - Then save/reload and compare IDs and the fingerprint.
2. **Native buy and sell, then attack.** Buy some physical stock and sell one item.
   - The purchased objects leave the map, the sold item stays native, and the offer equals the native trade window.
   - In the attack, residents do not consume sale goods while they have reserves.
3. **Emergency transfer.** Exhaust or remove the residents' medicine (or meals), then run the transfer action.
   - Exactly one sale stack of that category is released, the offer is reduced, and residents may use it.
4. **Unload and re-entry with depletion.** Consume or loot some reserves and buy some goods, move the player pawns away, then run the unload action. Expect:
   - `P5_SALE_RETURNED copies=0` and `P5_RESERVE_LEDGER` with reduced values.
   - `P5_REENTRY_UNLOADED sameOffer=True`.
   - On regeneration, `P5_STOCK_SPAWN revisitLedger=True capped=...` and `P5_REENTRY_AFTER sameOffer=True`, with no restored purchases or refilled reserves.
   - Repeat once to confirm a stable balance.
5. **Overflow and failure.** If storage overflows, expect `P5_SALE_NATIVE_ONLY` with the full offer still tradeable. Forced late rollback after `Materialize` must return the exact native Things (as in P5.3a).
6. **Expiry and defeat.** A 30-day off-map boundary followed by re-entry gives a new epoch, legitimately. Defeat leaves goods as loot with no return to the native holder.

## Live verification — 2026-10-12, stock-completion branch

Tested implementation tip `229273c` on `claude/p5-stock-completion`, with the
existing 75e8 mod link, isolated profile and five-mod baseline only. No normal
profile, research, relation, currency, NPC or stock grants were used. Steam was
not required. One disposable game process ran at a time. The branch was initially
tested detached because the main checkout held it; after that checkout moved to
unrelated coastal-layout work, this worktree attached to the requested branch.
That unrelated checkout was not modified. Nothing was merged or pushed.

### Build/pure gates

Release build: zero warnings/errors. PlannerTests: zero failures. LayoutTests
was built once, then all ten `--shard i/10` processes ran in parallel: **127/127
tests passed**, every shard reporting zero failures. The slowest shard took
559637ms. Evidence: `p5-completion-release-20261012.txt`,
`p5-completion-planner-20261012.txt`, `p5-completion-layout-build-20261012.txt`,
and `p5-completion-layout-shard-0` through `-9-20261012.txt`, under
`.runtime/logs/`. After the small carried-reserve fix below, Release and
PlannerTests passed again (`p5-carried-reserve-release-20261012.txt` and
`p5-carried-reserve-planner-20261012.txt`). No pure layout code changed in the fix;
the ten-shard result is the preceding implementation-tip result, not a second run.

### Case results

| Requested case | Result | Observed evidence / remaining gate |
|---|---|---|
| 1. Fresh generation and active reload | Partial pass | At583, map1/site91: `PROCEDURAL_PLAN engine=Clusters saleStacks=93+15`, followed by `P5_STOCK_SPAWN saleStacks=108 saleUnits=6924 nativeOnly=False` and `PROCEDURAL_SPAWN_COMPLETE`. Audit: `duplicateIdentity=False`, offer6924, physical6924;21 unsupported native Things remain native. Active reload583→584 preserves all108 mapped object IDs and API item facts, counts and positions; mapped audit text is identical. Exact legal floor-cell/room checks and the whole protection matrix are not closed. |
| 2. Native buy/sell, then attack | Not run | Stopped at the conservation failure below. The old independent native trade spike is not a mapped-stock buy/sell/attack pass. Core `CaravanArrivalAction_Trade.CanTradeWith` explicitly requires `!settlement.HasMap`; a proper off-map trade→attack sequence is needed, not bypassing native authorization on the already-loaded hostile debug site. |
| 3. Emergency transfer | Failed, with one small defect fixed | Removing loose operating medicine while retaining one resident-carried medicine incorrectly releases ten sale medicines. Fixed/replayed below. With the carried unit subsequently removed, exactly one ten-unit sale stack is released, offer medicine35→25 and operating medicine0→10. Actual native use is not established: the tending probe rejects its setup because it only counts original owned medicine, not released sale medicine. |
| 4. Depletion and repeated re-entry | Partial pass; conservation failure after emergency transfer | At26897, map1→2→3: both cycles report `P5_SALE_RETURNED copies=0`, `P5_REENTRY_UNLOADED sameOffer=True`, `P5_REENTRY_AFTER sameOffer=True`;107 remaining sale stacks/6918 units persist. Wood230 and HE11 are capped. After magazine depletion, HE/incendiary/spare-barrel keys remain0 on the next entry. However, newly allocated sale barrels are then omitted from the unload allowance (below). Purchases and a second unchanged exhausted-key cycle were not exercised. |
| 5. Full-materialization rollback / overflow | Not run | No storage overflow occurred in the accepted108-stack case. The existing late-rollback action selects steel/medicine from the native holder, which normal materialization has already emptied of those goods; the generation `forceFailure` hook precedes materialization. Prior P5.3a evidence is not a fresh full-offer late-rollback pass. |
| 6. Off-map expiry / defeat loot | Not run | Neither a new off-map expiry epoch nor defeated-settlement loot ownership was observed in this run. Loaded-expiry suppression from previous checkpoints does not substitute for either case. |
| 7. V2.6 three-style smoke | Partial, industrial only | One accepted flat IndustrialLate clusters settlement,15 rooms/eight residents; active reload, real temperatures and native post/mortar occupation observed. Town, mountain, shared-wall visual inspection and native bed/stove/bench route completion remain unobserved. |

First run: `p5-completion-live-first-20261012.log`. Named saves:
`RealisticBases-P5-completion-fresh-20261012`,
`RealisticBases-P5-completion-premature-emergency-20261012`,
`RealisticBases-P5-completion-first-reentry-20261012`,
`RealisticBases-P5-completion-zero-ammo-before-reentry-20261012`, and
`RealisticBases-P5-completion-zero-ammo-after-reentry-20261012` (all `.rws`).

The first layout is `BCE3BE9EFE54DF88`, seed `4D94FBA67FBEE725`, with a
debug-once IndustrialLate stage and effective one-layer fallback. Re-entry uses
the real faction's IndustrialMid stage and the native250-cell map size, rather
than the initial debug160-cell map. Consequently the re-entry result proves
stock/ledger behavior, **not** identical layouts, stage persistence or pawn
identity. Generated personal inventories still change under the disclosed P0
roster policy (for example observed medicine21→24 despite a21 ledger allowance).

Read-only saved/API analysis: all108 sale references are present and unchanged
across reload;90 map to accepting saved shelves, and18 occupy non-shelf cells
whose full room/walkability checks were not independently completed. All15
perishable sale stacks map to accepting food shelves. The first analysis used an
incorrect even-size shelf rotation offset; it was corrected and is not reported
as illegal storage. Evidence: `p5-completion-fresh-items-20261012.json`,
`p5-completion-reload-items-20261012.json`,
`p5-completion-saved-storage-comparison-20261012.json`.

Native elapsed observation584→26897 is **26313 ticks**, not a three-day pass.
At10757, actual room temperatures include storerooms175/188 at−10.0/−9.65°C,
laboratory/kitchen/dining/hospital/prison at21°C, barracks20.89°C and
workshop20.74°C. The generator room181 is50.69°C (generator ventilation is
separate P5A work). At26549, P3 reports eight assigned residents, seven occupied
posts, no out-of-bounds defenders and a manned mortar with one loaded HE shell.
The routine probe recorded four native refuels, but no complete food/bed horizon
was obtained. These results do not close V2.6. Room and pawn snapshots are retained.

### Fixed defect: carried operating reserves do not protect sale allocation

At26897 on map3, removing only the loose operating-medicine cell `(173,165)`
(including its shelf) leaves `MedicineIndustrial122660:1` in resident
`Human122655`'s inventory. The reserve audit observes medicine1, but emergency
allocation releases sale `MedicineIndustrial52913:10`, taking operating stock
to11. `OperatingAvailable` was scanning spawned map items only.

Commit **`f7f4708`** includes resident inventories/carry trackers in this check
and adds the disposable regression action
`P5 test carried medicine protects sale allocation`. It creates no supplies and
issues no NPC jobs. Replaying the same saved fixture after a cold restart gives:

```text
P5_CARRIED_RESERVE_GUARD_COMPLETE tick=26898 map=3 passed=True carriedMedicine=1 saleMedicineBefore=35 saleMedicineAfter=35 suppliesAdded=0 npcOrdersIssued=0
```

The action may release an unrelated exhausted category: in this fixture it also
releases the single three-barrel sale stack, which is recorded explicitly rather
than concealed. Pre-fix save: `RealisticBases-P5-completion-carried-medicine-failure-20261012.rws`.
Fixed replay: `p5-completion-carried-reserve-fixed-20261012.log` and
`RealisticBases-P5-completion-carried-medicine-fixed-20261012.rws`.

### Open conservation defect: emergency allocation is not handed to the reserve allowance

On map3 the generated spare-barrel allowance is correctly0 following depletion.
At26898, emergency allocation releases original sale `ReinforcedBarrel52914:3`;
the offer loses three units and `P5_RESERVE_AUDIT` observes operating barrels3.
On native map removal at26900:

```text
P5_SALE_RETURNED tick=26900 map=3 settlement=91 stacks=105 units=6905 nativeStacks=126 copies=0
P5_RESERVE_LEDGER tick=26900 ... ReinforcedBarrel:0 ...
MAP_REMOVED tick=26900 map=3 settlement=91 ...
```

The sale stack is no longer returned as merchandise, but the reserve ledger still
caps it at its original generation allowance0. The legitimate transfer therefore
has no persisted remaining allowance; re-entry's grant for that key is0. This is
not the desired rule that arbitrary player-dropped items must not create reserves:
these are the settlement's exact existing sale objects, explicitly reallocated.
The failure save `RealisticBases-P5-completion-emergency-unload-20261012.rws`
retains the reduced native offer and zero-barrel ledger; the fixed-replay save
above retains the pre-unload three operating barrels. The consumed-medicine
setup also demonstrates native removal, but is not a tending pass.

Source review identifies the missing handoff: `EmergencyTransfer.Release` removes
sale/cache/forbidden references without registering the transferred objects with
`P5ReserveState`'s allowance or P3's operating-stock references. This also leaves
released mortar supplies outside `GunAvailable`/`CountStock`'s original-stock
view. The live tending probe similarly rejects released medicine as absent
because its measurement reads original `P5OwnedThings`; that is a diagnostic
limitation, **not** proof that the native doctor cannot use it. These linked
allocation/consumer/persistence issues were left for the implementation owner;
no redesign was attempted and no native-use pass is claimed.

A separate early wood release at10000 occurred while substantial generator fuel
remained (225.26 loaded at26897). The scoped fix does not change whether loaded
fuel/charge counts as an available reserve; that policy/consumer check remains
open rather than being silently assumed fixed.

The player-free replay map unloaded natively when ticks were resumed after the
tending setup rejection. That fixture failure and the subsequent save are kept;
no death, attack, successful tending or full routine horizon is inferred from it.
The broader matrix stopped on the conservation defect. The disposable game was
stopped and its autostart copy restored from the immutable reset save. All named
evidence remains ignored under `.runtime/`. **P5 and V2.6 remain incomplete.**

## P5.3c — Exact emergency-allocation handoff and retained operating goods

2026-10-12/13 continuation: the user authorized the fix and further P5 work.
Astra confirmed that legitimate retained inventory must exceed a fresh plan's
cap when necessary, keep its actual type/attributes, and not close broader P5
while P0 asset/roster regeneration remains coarse. No additional agents,
merge or push. The preceding failed evidence remains intact above.

Emergency allocation atomically removes the actual sale reference, registers it
with P3's operating/owned consumers and tracks its operating allocation. Failure
restores the original sale/consumer references. Native splits inherit allocation;
merges with unrelated sale/base stock are blocked, consumed/looted references are
pruned, and managed allocated IDs opt out of vanilla save compression.

At unload the actual surviving allocated Things move into a sole-owner
ThingOwner on the saved settlement record. Map tracking is references only;
after restore the off-map holder is empty. These items are not recreated from the
base count ledger. Thus an exhausted base barrel key correctly remains0 while
`allocatedArchived=3` preserves the three separately owned original barrels.
Retained ordinary/perishable capacity is reserved before planning. Restoration
preflights legal slots and borrows through the existing spawn transaction, keeping
type/stuff/quality/HP and component state. Oversized retained stacks currently
reject before borrowing, not truncate; explicit oversized split recovery remains
an untested follow-up. Raw ingredients are inventory, not guaranteed ready meals.

### Observed regressions

- At26898, forced handoff rollback: `P5_EMERGENCY_ROLLBACK_COMPLETE passed=True
  exactSaleThings=107 copies=0`. Actual transfer of `ReinforcedBarrel52914:3`
  reduces sale6918→6915 and creates no new units.
- Real unload archives that exact stack: `P5_ALLOCATED_ARCHIVE stacks=1 units=3
  copies=0`; base ledger0, `allocatedArchived=3`. Off-map save/active reload then
  re-entry at26899 restores ID52914/count3/HP75 with `recreated=0`, despite a
  fresh two-barrel target. Holder empty on-map; offer unchanged.
- Explicit native SplitOff/destruction consumes one unit. Another real
  unload/re-entry restores the same original ID with2, sale6915 unchanged.
  A subsequent player-free active reload unloads naturally on its first tick;
  the two-barrel archive survives. This is native removal evidence, not a mapped
  active-reload pass.
- Cold-load that off-map save and reopen at26901. Attach the original visitor
  (no new pawn), exhaust119 operating-food units, then allocate existing sale
  `Meat_Turkey52920:75`, a type absent from the generated meal ledger. Controlled
  damage sets HP60→53; actual rot progress904.0806. Active reload26901→26902 keeps
  ID/count/HP/rot/position. Real unload/re-entry at26902 keeps the same meat75,
  HP53/rot904.0806 and original barrel2; sale6840 remains unchanged. The existing
  routine action only parks the visitor before explicit removal: no elapsed
  routine horizon or cooking/feeding pass is credited.
- Independent fresh encounter, start584: destroy only the original two spare
  barrels, allocate its actual three-unit sale stack, and run the native loading
  probe. It accepts this explicit P5 case while retaining the ordinary two-spare
  contract. At3540: `P4E_LOGISTICS_PROBE_COMPLETE passed=True operators=54706
  looseHE=11 loadedTotal=1 spareBarrels=2 initialBarrels=3 barrelFuel=20`.
  Elapsed2956ticks; initial embedded barrel20 deliberately consumed; real funded
  crew fetched/consumed an allocated barrel with no NPC orders or new inputs.

Logs under `.runtime/logs/`: `p5-operating-archive-live-20261012.log`,
`p5-allocated-type-state-live-20261012.log`, and
`p5-allocated-barrel-native-use-20261012.log`. Named `.rws` evidence saves use
`RealisticBases-P5-allocated-` plus `three-before-unload`, `three-offmap`,
`three-restored`, `two-second-visit`, `two-native-unload`,
`ingredient-before-reload`, `ingredient-after-reload`, `ingredient-after-reentry`,
or `native-barrel-use`, each suffixed `-20261012`. No normal profile was touched.

Release builds: zero warnings/errors. PlannerTests passes allocation conservation
and depleted-base allowance assertions. The focused layout fixture verifies
retained storage/freezer growth, empty-input digest compatibility and quest
exclusion. Full LayoutTests passes127/127 again in ten shards, slowest529330ms:
`p5-retained-layout-shard-0` through `-9-20261012.txt`. Later probe-only additions
and allocated-reference pruning also compile cleanly and leave pure geometry
unchanged. The original loss is fixed; trade/attack, full-transfer rollback/
overflow, expiry/defeat, broader asset/roster persistence and integrated P5/V2.6
remain open. No full P5 closeout is claimed.

## 2026-10-13 pause checkpoint — traded settlement still rejects after dense-storage merge

The user requested merging `main`'s storage-density update into this P5 branch.
Merge `1cd5d31` includes main `d6a2aef`, dense storage `6db1421`, and coastal/
barrier siting plus inner-yard crops. The four pending P5 probe/spawner edits
were saved in a named stash and restored without conflicts; no prior work was
discarded and nothing was merged into main or pushed.

### Native trade/attack before that merge

Using the untouched disposable baseline, existing negotiator `Human185` and
existing currency, native buy7/sell3 succeeds at583: offer steel714→710,
playerSteelDelta4, total silver2040 conserved, epoch1. The70 ordinary native
stacks plus26 unsupported native Things survive a cold reload. This is a new
actual offer, not the older526→522 prototype case.

At584, that original caravan's native attack acquires the already-published
offer without regeneration (`offerUnits=6368`, playerSteel4, epoch1). Generation
rejects before mutation, so the attack verifier correctly reports
`passed=False sameOffer=True`. Native hostility is the normal attack consequence;
no relations, research, pawns or supplies were granted. The subsequent diagnostic
map audit cannot resolve a realized encounter; its post-event exception is a
fixture/error-reporting limitation, not successful generation. A generic active
save explicitly preserves the failed world/map afterwards.

Native input:250×250, seed `8DD10F58FC412C58`, IndustrialEarly/faction stage,
wealth75, five residents, sale storage111 ordinary/hazardous plus13 perishable
stacks, wood63713, local granite/marble, climate−2..38°C. Original rejection:
`room_unplaceable ... RockProtected_storage-4`,90 attempts/221463 evaluations,
`budgetExhausted=False`, zero mutations. Terrain capture digest
`CDF248A8E1087ABF` is preserved as
`Source/RealisticBases.LayoutTests/p5-native-trade-rejected-traits.rle`.

Logs: `p5-native-trade-attack-20261013.log` and
`p5-native-trade-attack-arrival-20261013.log`. Saves:
`RealisticBases-P5-native-partial-trade-20261013.rws`,
`RealisticBases-P5-before-native-trade-attack.rws`, and
`RealisticBases-P5-native-trade-attack-rejection-20261013.rws`.
Raw capture/catalog and saves/logs remain under ignored `.runtime/`.

### Post-merge pure result: one pass, one failure

P5 storage-capacity assertions now count actual shelf slots, not floor cells;
this is required by the merged three-stacks-per-shelf-cell model. Existing sale/
retained storage sizing passes (81 ordinary slots,59 food slots in its fixture).
The new named regression, `P5 captured native trade tile fits its whole offer
with dense storage`, **still fails**:

```text
no_valid_layout sites=10 attempts=93 evaluations=263300 repairs=88 routeNodes=585170 budgetExhausted=False
validate:rejected 131,48 pocket_137,31_not_sealed
gate:route_budget_exhausted 131,48/RockProtected
repair:connected_room_extension 131,48/RockProtected room=power corridor=5 bounds=150,38,5x13
```

Shared-wall room packing now progresses through both storage rooms, but final
cooler-pocket sealing/gate routing still rejects the recorded candidate. This
does not prove those are the only failure modes across all attempts. Do not
replace the remaining diagnosis with a larger timeout or silently trim the offer.
The pure replay uses captured terrain traits, observed stage/wealth/stock demand
and the existing version-matched runtime catalog fixture; it is not a complete
serialized live planner input (for example captured geyser coordinates are not
included). Compare input fidelity when diagnosing. No post-merge live retry or
successful post-merge native attack is claimed.

Reproduce in Docker using the configured environment from `build-mod`:
`dotnet run --project Source/RealisticBases.LayoutTests/RealisticBases.LayoutTests.csproj -c Release -- --only "P5 captured native trade"`.
Focused combined run log: `p5-main-density-captured-tests-20261013.txt`,
failures1/16497ms. Its failing exit stopped the chained Release build, so no
post-merge Release/full-suite pass is claimed. Pre-merge trade/failure probes
compiled cleanly, but full-materialization forced rollback and forced overflow
actions have not yet been invoked or validated. They are committed as pending
instrumentation, including the corrected `nativeOnly` fallback diagnostic.

Per the user's explicit instruction, preserve the named failing test as red,
document/commit the current work, and **pause** rather than implement another fix.
The disposable game is stopped. P5 and V2.6 are not complete.

## 2026-10-13 captured trade-site rejection: structural fix (pure evidence only)

Fix commit `ccbc373` (on b75403b). Main `9b09530` (P5A) was then merged into
this branch. The named regression `P5 captured native trade tile fits its whole offer with dense storage`
now passes with unchanged inputs: 250×250 captured traits, seed `8DD10F58FC412C58`,
IndustrialEarly, wealth75, five residents, 111 ordinary/hazardous + 13 perishable sale stacks.
The result: layout `1F49048E56BD99C0`, two storage rooms, 3 sites/26 attempts, budget not exhausted.

Printing every attempt diagnostic showed three independent causes, not one:

1. **Field approach on rock (72 of 93 attempts).** Joint-site preflight offered
   field interfaces whose compound-side tip/lane is natural rock. Every OpenGround
   attempt then failed `field_approach_unavailable`. Preflight now applies the
   same static tip/lane terrain rule the planner reserves (`P4BJointSitePlanner.AttachmentClear`
   with shared `AttachmentDepth`/`AttachmentHalfTip`). Failures now record a per-interface reason.
2. **Exterior flood capped below the map.** `ExteriorDistances` stopped at the
   40k `MaxRouteNodes` search budget on a 62.5k-cell map (`gate:route_budget_exhausted`).
   Inland gates, including the one aligned with the field, were never reached. The
   bailey planner then rejected every valid room layout with `gate_faces_away_or_offset`.
   It is a single linear flood, now bounded by the map area; search routes keep the budget.
3. **Unsealed exhaust pocket (`pocket_137,31_not_sealed`).** A jail cooler beside
   its door had its pocket next to the door-outside cell, and the connected-extension
   repair claimed that cell as street before the room committed the pocket.
   `ExhaustCost` now rejects pockets beside a door-outside cell, and the extension skips
   corridors touching a new pocket. Applying this alone to b75403b removes that
   validator rejection, but the tile still fails on causes 1–2.

No stock, roster, shelf capacity, timeouts or validation were relaxed.
**Not yet done:** the live native trade→attack replay on the preserved
pre-attack save. The capture lacks some live inputs (for example geyser
coordinates), so the pure pass is not live evidence.

After the main merge: clean Release build, PlannerTests pass, LayoutTests 134/134,
V2 matrix legacy 80/80 and clusters 80/80.
