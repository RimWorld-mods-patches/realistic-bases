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

## 2026-10-13 resumed P5 validation

The user reports the rejection fixed and authorizes continuing validation at
`7760723` (includes structural fix `ccbc373` and main/P5A merge `6b8847f`).
Independent rerun: captured trade-site regression passes (layout
`ECACFC4362EF7881`,3 sites/28 attempts, budget not exhausted), PlannerTests passes,
and Release builds with zero warnings/errors. Log:
`p5-resume-build-tests-20261013.txt`. The earlier134-case/matrix results above
are the fixer's recorded evidence, not newly rerun full suites.

### Native trade→attack and active reload

Cold-load the preserved pre-attack save, confirm the five-mod baseline and
generator disabled at BOOT, audit native trade, explicitly enable the guarded
generator, then invoke the native attack action through the checked API runner.
At585: `P5_NATIVE_TRADE_ATTACK_COMPLETE passed=True sameOffer=True epoch=1
offerUnits=6368 saleSteel=710 playerSteel=4`. Accepted map1 has five residents,
12 rooms, layout `3F05405EFACB3508`. `P5_STOCK_SPAWN saleStacks=124 saleUnits=6368
nativeOnly=False`; physical audit has124 spawned stacks,26 unsupported native
Things, `duplicateIdentity=False`. Native hostility comes from the real attack;
no research, relations, currency, pawns or supplies were granted.

`P5 test native empty offer and loaded expiry` passes: two empty reads, exact26
native Things restored, loaded restock suppressed, epoch1/tick restored and124
mapped stacks retained. This is a synchronous clock-boundary test, not elapsed
30-day evidence. The separate sale/reserve merge probe rejects before mutation:
this early-industrial base has no operating steel stack for its hardcoded fixture.
Do not count that invocation as a merge pass or a production ownership failure.

Save/reload585→586 preserves all124 mapped sale IDs and API facts (definition,
count, stuff in the audit, quality, HP, position and rotation); the physical-stock
audit is identical. Snapshot files:
`p5-fixed-trade-before-reload-things-20261013.json` and
`p5-fixed-trade-after-reload-things-20261013.json`. Sale goods occupy50 cells,
maximum3 sale stacks/cell. An independent saved-XML check against installed shelf
size/rotation rules finds112 sale stacks on27 actual shelves (52 shelf cells),
all accepted by their saved filters, and12 stacks on distinct ground cells.
All serialized item classes on shelves stay within3 stacks/cell including
operating items. Ground-sale definitions are nonperishable; this does not yet
prove freezer temperature, reachable resupply or hazardous-stock separation.
Log: `p5-fixed-native-trade-attack-20261013.log`. Named active save:
`RealisticBases-P5-fixed-native-trade-active-20261013.rws`.

### Full materialization rollback and forced overflow

Independent cold-loads of the same pre-attack save, never concurrent game
processes. After all124 stacks materialize, the armed late failure reports
`P5_FULL_STOCK_ROLLBACK recovered=True exactNativeThings=True
quantitiesAttributes=True itemLedgerRestored=True nativeThings=96`.
The spawn rejects intentionally with `terrainPreserved=True layersPreserved=True`;
833/833 plants restored, offer6368/epoch1/playerSteel4 unchanged. The attack's
success verifier correctly reportsFalse because generation was deliberately
rolled back; its subsequent audit still cannot resolve a realized encounter and
throws the already-known diagnostic exception. This does not invalidate the
earlier explicit rollback assertions or establish successful generation.
Log: `p5-full-transfer-rollback-20261013.log`.

Forced zero sale-storage destinations on the next independent replay produce
`P5_SALE_NATIVE_ONLY`, `saleStacks=0 saleUnits=0 nativeOnly=True`, then accepted
base generation and `P5_NATIVE_TRADE_ATTACK_COMPLETE passed=True sameOffer=True`.
The entire offer6368 remains native, epoch1, saleSteel710/playerSteel4; physical
stock inactive/empty, native96 Things/6396 total units including unsupported
categories, `duplicateIdentity=False`. No partial transfer/truncation occurs.
Native trading authorization is unchanged (hostility normally disallows trade);
no hostile trade-window bypass is claimed. Log: `p5-sale-overflow-20261013.log`.

Broader P5 stays open: off-map expiry/defeat, measured combat/outage/failure
coverage, supported asset/roster/casualty persistence, other profiles and P5.5.

### Tending regression: patient/doctor handoff fails

First trial on the forced-overflow map starts587, patient `Human103379`, finite
medicine17. The encounter unloads natively at2604 before the10000-tick deadline;
there is no completion marker or tending pass. Preserve that limitation in
`p5-sale-overflow-20261013.log`, not as evidence of successful support.

Independent cold-load of the successful physical-stock save, then start the
existing routine probe to park the original visitor at home and hold the map
without changing NPC needs, jobs, supplies or native defeat. Start tending586
on `Human94817`. At10590, **`P5_TENDING_PROBE_COMPLETE passed=False alive=True
nativeTendJobs=0 medicineBefore=17 medicineAfter=17 physicallyConsumed=0`**.
The routine trial includes this bounded test injury; it is not an untouched
healthy-roster trial. Log: `p5-fixed-early-routine-tending-20261013.log`.
Named save `RealisticBases-P5-native-tending-failure-20261013.rws` at10590
preserves the exact failed tending checkpoint independently of the routine result.

Saved duties show a real support worker `Human94807` (Medicine3) and the patient
assigned `RB_P3_Relief`, but both are running `Wait_Combat`. The patient is not in
a bed; `playerSettings IsNull=True` on both NPCs. Installed1.6.4871 source explains
two concrete integration gaps:

- `TryGiveSupportJob` calls `DoctorTendToHumanlikes`/`WorkGiver_Tend.HasJobOnThing`.
  Its `HealthAIUtility.ShouldBeTendedNowByPlayer` predicate returnsFalse for an NPC
  with null playerSettings; its humanlike laying predicate also requires a bed.
- Relief only runs `SatisfyBasicNeeds` (food/rest/chemical needs) then the hold job.
  There is no patient-go-to-bed branch for an otherwise awake injured resident.

This is not explained by absent medicine or an unassigned support worker.
Do not manufacture playerSettings, order patients/doctors manually, or grant
medicine to make the test pass. A scoped NPC medical-rest/tending adapter with
real reservation/reachability/medicine consumption is still required; this
validation request does not implement that production change.

### Early-industrial three-day routine: pass, separate from tending

Continue the same explicitly held five-resident map after the tending deadline's
automatic pause. An additional intermediate pause at61614 was inspected and
resumed; only elapsed game ticks are counted. At180587:
`P5_ROUTINE_PROBE_COMPLETE passed=True elapsed=180001 residents=5 ate=5 slept=5
sleptInBed=5 nativeRefuelJobs=5 physicalFuelIncreases=5 missingSamples=0
starvingSamples=0 downedSamples=0 minFood=0.108 minRest=0.128 parentChanged=False`.
No NPC orders, supplies or credited future production. This is the same trial
with the controlled tending injury above, not a successful medical-care test.
The map hold is test instrumentation, not native removal/revisit evidence.

Food nutrition66.60→41.40; loaded fuel375→300.40 and stored wood330→75 give
actual net fuel depletion329.60 units. The readiness model's worst-case demand
is330 over three days. At107179, current freezer/storeroom temperature−9.34°C;
all rooms' raw temperatures are preserved in `p5-early-routine-rooms-20261013.json`.
The probe records26 sampled cooler-power-off observations, so do not claim
uninterrupted cooling or outage resilience. The six-hour outage audit remains
insufficient at steady actual2985W/rated4245W with600Wd battery charge; the
startup0W observation is not meaningful full-load outage acceptance. HE30 and
barrel charges60 remain finite; six-hour firing-duty operation is still unmeasured.

Named final save `RealisticBases-P5-early-three-days-20261013.rws`, final pawns/
rooms JSON files `p5-early-routine-final-{pawns,rooms}-20261013.json`, and the
routine/tending log retain the evidence. Focused already-built P5/P5A layout tests
also pass, failures0/23114ms, `p5-resumed-focused-layout-20261013.txt`; no DLL
rebuild while the game was running. The disposable process is stopped and its
autoload copy restored to the unchanged immutable baseline. P5 is not complete;
the NPC medical adapter is the next concrete implementation defect.

Final physical audit still has124/6368 sale stacks/units,26 unsupported native
Things and no duplicate ownership; no emergency sale allocation occurred in
this trial. Final cold room60 is roofed,−9.42°C. The routine log has no runtime
exception or ten-jobs-per-tick error. This does not override the earlier native
tending failure or the26 sampled cooler-power-off observations.

### NPC tending adapter: fixed

The relief duty now has a scoped patient bed-rest branch after basic needs:
`JobGiver_P5PatientBedRest` lays an injured relief resident in its own native
bed (`CurrentBed` or `RestUtility.FindBedFor`) while it has hediffs needing tend,
inside the existing whole-route needs bound. The support worker no longer uses
`WorkGiver_Tend.HasJobOnThing`/`FindBestMedicine`, whose player-settings and
medical-care predicates reject NPCs. `TryGiveTendJob` keeps the vanilla
`GoodLayingStatusForTend`, reservation/reachability, Doctor capability and
aggro-mental checks, and issues the native `TendPatient` job with the residents'
own operating medicine (owned stock or resident inventory, never sale goods;
best potency, then nearest). Medicine pickup, reservation and consumption stay
native. No playerSettings, orders or medicine are manufactured.

Replay of the same failed trial: cold-load
`RealisticBases-P5-fixed-native-trade-active-20261013.rws`, start the routine hold
probe at586, then tending on the same patient `Human94817` with medicine17.
At3225: **`P5_TENDING_PROBE_COMPLETE passed=True alive=True nativeTendJobs=1
medicineBefore=17 medicineAfter=16 physicallyConsumed=1 npcOrdersIssued=0
extraMedicine=0`**. The next duty pass (3240) returns the patient from
`NeedsRelief` to a ranged post, and it is at that post shortly after. There are no
runtime exceptions. Log: `p5-npc-tending-adapter-20261013.log`. Clean Release
build and PlannerTests pass. This is one bounded routine injury; combat-time
tending and downed rescue-to-bed remain unmeasured.

## 2026-10-13 P5.4 validation after NPC tending fix

The user confirms `95ab4cd` is committed/validated and requests continued P5
validation. Its recorded native tending replay above is accepted as the fixer's
evidence, not claimed as a new replay here. Independently rerun PlannerTests and
Release build: pass, zero build warnings/errors. Log:
`p5-persistence-validation-build-20261013.txt`. No production code changed in
this validation checkpoint.

### Real unload/re-entry: sale preservation passes, encounter state fails

Cold-load the preserved genuinely depleted three-day save
`RealisticBases-P5-early-three-days-20261013.rws`. Confirm the five-mod profile,
fresh GAME_LOADED, paused game and map1. At180588, save the actual before state;
use the existing routine action **only to park the original visitor**, then
explicitly enable the guarded generator and invoke real unload/re-entry twice.
Both visits remain at180588: no elapsed-time production, restocking or consumption
can explain the differences. The abandoned routine measurement is not another
three-day pass. No actor, supplies, needs, relations or research are granted by
the validation actions.

| State | Before (map1) | First re-entry (map2) | Second re-entry (map3) |
|---|---|---|---|
| Sale stock | 124 stacks /6368 units | Same124 IDs, quantities, stuff, quality and HP | Same124 IDs and facts |
| Loose operating simple meals | 46 | 46 | 46 |
| Resident-carried simple meals | 0 | 11 | 6 |
| Loose + loaded operating HE | 29+1 | 29+0 | 29+0 |
| Generator fuel | 300.4000 | 375 | 375 |
| Loose operating wood | 75 | 0 | 0 |
| Battery stored power | 600 | 300 | 300 |

Sale offer remains epoch1, saleSteel710, fingerprint/identities unchanged,
`duplicateIdentity=False`;26 unsupported native Things remain native. Both
`P5_REENTRY_AFTER sameOffer=True physicalStacks=124` markers pass. This closes
the repeated re-entry sale-conservation check for this previously traded fixture,
not the whole P5.4 contract.

The original roster is94775/94786/94798/94807/94817. The first re-entry keeps
94798/94817 but substitutes168793/168812/168819 for the other three. Those three
original residents remain serialized in **worldPawns/pawnsAlive**, not dead.
The second roster is168819/213715/213727/213735/213747: none of the original five
returns. Asset identities also change (battery94433→168446, mortar94572→168585
on the first re-entry). These are same-tick revisits, not fresh settlements.

`P5_RESERVE_LEDGER` correctly records MealSimple46 and HE29. However, first-entry
spawning logic still runs on revisits: native `PawnGenerator.GeneratePawn`
creates/reuses arbitrary residents and supplies personal inventory outside the
46-meal spawn allowance. Direct saved-inventory inspection attributes the first
11 extra meals to94817:3,168793:2,168812:3,168819:3. The next visit gets3 each on
213727/213735. Total operating meals therefore change46→57→52 without ticks or
inputs. The existing pure remainder-cap test does not cover native pawn-generation
inventories. This is a conservation defect, not a larger reserve intentionally
retained from the previous visit.

The existing spawner also rebuilds assets and initializes batteries to50%, while
the unload ledger explicitly excludes turret/mortar loads. Thus the loaded HE
unit is lost and charge resets. Fuel is pooled/floored into the wood allowance,
then redistributed into full generators: total375.4000→375, not a demonstrated
net fuel grant, but per-device state and fractional fuel are not retained. This
trial does not test damaged walls, casualties, capture, or an almost-empty battery;
do not infer their exact failure modes or claim they pass.

### Evidence and next implementation boundary

Log: `p5-unload-state-validation-20261013.log`. Named saves under the disposable
profile, all suffixed `-20261013.rws`:

- `RealisticBases-P5-before-state-reentry`
- `RealisticBases-P5-after-state-reentry`
- `RealisticBases-P5-after-second-state-reentry`

Reproduce by cold-loading the before save, parking the existing visitor with
`Start P5 three-day routine probe`, enabling the generator if BOOT says disabled,
then `P5 unload and re-enter current settlement`. Save the result and compare
`p3Defenders`, each resident's actual inventory, battery `storedPower`, generator
`fuel`, and mortar gun `loadedShell`/`loadedCount`; the existing offer-only
completion marker cannot certify those state gates. The original older save
logs missing think-node keys after the duty-tree change; no runtime exception
or ten-jobs error occurs, and no ticks advance during the two re-entry comparisons.

Next implementation is **P5.4's supported encounter archive/restore**, not another
stock-capacity/tending tweak: retain actual surviving roster/gear/needs and supported
asset state before native teardown; restore them without rerunning initial pawn
inventory, battery or loaded-ammo grants. Keep the native stock holder and exact
allocated-item archive as sole authorities. Preserve native defeat/capture and
P4D exclusions; do not claim casualty persistence merely from saving pawn IDs.
Add conservation/restore regressions before a live replay of these named saves.

The disposable process is stopped and autostart restored to the immutable baseline
(matching SHA256 `81bae97b413a0520183fae4d14ae2f090b33ebe389c8df311b4e7d124f652541`).
P5.4 fails this gate; P5 remains incomplete. No production fix, merge or push is
included in this validation checkpoint. Off-map expiry/defeat, combat-time medical
support/rescue, measured combat/outage and the integrated P5.5 matrix remain open.

## 2026-10-13 P5.4 encounter archive/restore: implemented

**Design.** A pre-teardown archive in the existing `MapDeiniter.Deinit` prefix,
after sale return and allocated-item archiving and before the ledger:

- **Residents.** Record the surviving resident IDs (humanlike, own faction, not
  prisoners or hosted). Carried stacks move into the resident's own inventory,
  because native despawn would drop them on the closing map. Native teardown still
  passes the residents to the world. A postfix pins them in
  `WorldPawns.ForcefullyKeptPawns`, so world-pawn GC cannot discard them. On
  re-entry, `TakeSurvivors` removes the same pawns from the world and spawns them
  at home cells with their own gear, inventory, needs and injuries. Dead, captured,
  departed or missing residents are not replaced (`P5RosterRestore`). A
  re-entry with zero survivors rejects visibly (`p5_no_surviving_residents`)
  rather than inventing a roster. First visits and pre-P5.4 saves keep native
  generation.
- **Assets.** Snapshot every owner-faction building with damage, a refuelable
  comp, a battery, or a turret gun with a changeable projectile. Key it by
  `def@position` and store it with its layout digest. On re-entry with the same
  digest: generators skip the pooled tank fill and get their archived tank
  exactly. After the fresh-layout audits, `ApplyAssets` restores damage, every
  archived fuel/barrel value, battery charge and loaded shells. Each value is
  clamped to capacity and never raised above the archive. A different layout
  digest skips the restore (logged) and keeps the conservative pooled behaviour.
- **Ledger.** `Observe` now counts only loose, non-sale, non-allocated units.
  Resident inventories travel with the residents, and device loads travel with
  the asset archive, so neither is regenerated from the remainder.

Pure regressions (`P5EncounterArchiveTests`): survivor-only restore with
dead/captured/departed/missing fates, constant meal totals across two same-tick
revisits, and archived-not-refilled asset clamps. PlannerTests pass, and the
Release build has zero warnings and errors. Debug-only additions: `P5 save
off-map archive checkpoint`. `P5 reopen saved allocated-stock settlement` also
accepts a resident-archived site.

### Live replay of the failed re-entry gate

Cold-load `RealisticBases-P5-before-state-reentry-20261013.rws`. Park the visitor
with the routine action, enable the generator, then run `P5 unload and re-enter`
twice, all at 180589:

| State | Before (map1) | Re-entry map2 | Re-entry map3 |
|---|---|---|---|
| Residents | 94775/94786/94798/94807/94817 | same five, `generated=0 replaced=0` | same five |
| Food need | 0.148/0.860/0.632/0.980/0.976 | identical | identical |
| Loose operating meals (API) | 46 | 46 | 46 |
| Resident inventory units | 2 | 2 | 2 |
| Loaded mortar HE | 1 | 1 | 1 |
| Total refuelable fuel | 320.3982 | 320.3982 | 320.3982 |
| Battery stored energy | 600 | 600 | 600 |
| Ledger | MealSimple46, HE29, WoodLog75 | same | same |

Both `P5_REENTRY_AFTER sameOffer=True physicalStacks=124`, with the asset
restore `applied=7/7 unmatched=0 granted=0`. Log:
`p5-roster-asset-reentry-20261013.log`.

**Off-map save/load.** Unload only, then save
`RealisticBases-P5-offmap-archive-20261013.rws`. The saved XML lists all five
residents in `pawnsForcefullyKeptAsWorldPawns`. After a cold restart from that
save and reopening, the same five residents return with identical needs: 46 loose
meals, fuel 320.3982, energy 600, one loaded HE shell, and 124 sale stacks with an
unchanged offer. Log: `p5-offmap-archive-save-20261013.log`.

**Casualty.** On that reopened map, resident 94786 was killed through the RIMAPI
pawn status edit, a disposable test mutation. The next unload archives four
survivors and pins 4/4. Re-entry restores exactly 94775/94798/94807/94817, with
`generated=0 replaced=0`. The corpse's single carried unit stays with the closed
map. Log: `p5-offmap-reload-casualty-20261013.log`.

**Not covered.**
- Destroyed buildings are regenerated; only surviving buildings keep their
  damage. Skipping destroyed walls or fixtures would conflict with the realized
  layout audits and needs its own design.
- Asset Thing IDs are regenerated; their state is restored, not their identity.
- Capture was exercised only in the pure model.
- Residents archived off-map are frozen as world pawns; no elapsed off-map
  consumption is modelled.
- Off-map expiry/defeat, combat-time medical support, measured combat/outage,
  other profiles and P5.5 remain open.

The disposable process is stopped and autostart is restored to the immutable
baseline (SHA256 `81bae97b…652541`).

## 2026-10-13 P5 closeout work: outage, combat, medical care, destroyed buildings, expiry

Continuation toward P5 completion (`909b2e2` and follow-ups). Each live run below
cold-loads a named disposable save; a probe that is reloaded mid-run is never
credited. The process is stopped before every build.

### Outage: batteries now bridge the whole rated load

The 2026-10-12 audit found that one battery (sized for the *critical* load only)
cannot bridge six hours at the real connected load. NPC residents shed no load,
so `LayoutPowerModel.Batteries` now covers critical + non-critical + defense
reserve + the power room's own draw for six hours at zero generation, including
native 5W self-discharge per battery. Both the demand model and the planner
iterate generators and batteries until stable. The validator requires the bridge
for the whole rated load. Batteries spawn fully charged, like the generators'
full tanks. LayoutTests 134/134 (ten shards), V2 matrix legacy 80/80 and
clusters 80/80.

New disposable `Start P5 six-hour outage probe` (`P5DutyProbe`):
- **Arming:** it waits until every switched-on consumer has been powered for 300
  ticks.
- **Zero generation:** it switches off every flickable generator and holds
  unswitchable generation (solar/wind) at zero output through a test-only
  `UpdateDesiredPowerOutput` postfix. Renewables are not despawned, because a
  yard can carry the conduit tree.
- **Sampling:** every switched-on consumer is sampled for 15000 ticks.

Two earlier attempts were probe defects and are not counted: the first sampled
the generation tick before any consumer powered on; the second despawned a solar
yard that bridged the turret branch.

Fresh ten-resident IndustrialMid base (layout `D550586008426400`, three batteries):
**`P5_OUTAGE_PROBE_COMPLETE passed=True elapsed=15000 generatorsSwitchedOff=7
renewablesZeroed=1 startChargeWd=1800 minChargeWd=726.08 unpoweredSamples=0`**
at an actual 4458W/rated 6235W load. Log `p5-outage-combat-first-20261013.log`.

### Six-hour combat duty and combat-time medical care

New disposable `Start P5 six-hour combat probe`:
- **Waves:** three native assault waves (250 points each, hostile humanlike
  faction, `LordJob_AssaultColony`) arrive two hours apart.
- **Supplies:** it measures the physical use of HE, incendiary, barrels, steel
  and medicine, including resident inventories.
- **Ammunition:** it counts samples where hostiles are present and every mortar
  is unloaded with no shells left.
- **Medical:** it measures urgent neglect, meaning a resident who is bleeding or
  downed outside a bed, past one hour, while no enemy is within 25 cells, a
  support worker exists, and nobody is tending or rescuing.
- **Deaths:** each death is reported with its blood loss and whether the
  resident was downed first.

The runs exposed and drove three medical fixes:

1. Defenders held on post during a threat are never relieved to a bed, and
   vanilla tends humanlike patients only in bed. The support worker now tends a
   standing, non-moving defender in place once no enemy is within 25 cells, using
   vanilla `draftedTend`, which holds the patient during the tend.
   Downed, then heaviest bleeding, are treated first.
2. The doctrine chose a support worker with Caring disabled by backstory
   (`BushSniper94`), so nobody tended three wounded residents in bed (tendJobs=0).
   `DefenderCandidate.CanTend` now records `WorkTypeIsDisabled(Doctor)`, and
   while anyone needs tending, tend-capable support workers are preferred
   (pure regression `P5 a wounded roster gets a support worker who can tend`).
3. With one doctor and four wounded, bleeding patients waited in bed. A relief
   patient in bed whose oldest untended bleeding wound is 1250 ticks old, with no
   doctor reserving them, now self-tends without medicine (vanilla
   `JobGiver_SelfTend` semantics).

Passing runs (each from `RealisticBases-P5-before-combat-20261013.rws` unless noted):

- **Flat IndustrialMid:** `passed=True`. Used HE17, barrel charges 17, Steel75
  and Medicine 1, with ammoOutSamples=0 and urgent neglect 0. The one death was
  a kill in combat (blood loss 0.01, never downed). The readiness model's
  six-hour demand is HE36 and barrel charges 36, with no shortfall.
- **Native-terrain revisit of the same settlement:** `passed=True`. Used HE5,
  Steel75 and Medicine 4. Both deaths were kills in combat; urgent neglect 0.

In both runs the final stragglers kept the threat alive (`hostilesLeft=True`),
and two or three residents still had minor non-bleeding wounds at the end. These
are reported, not hidden. Logs `p5-combat-pass-and-flat-reentry-20261013.log` and
`p5-native-damage-expiry-release-20261013.log`.

### Destroyed buildings, layout stability and changed layouts

- **Destroyed buildings.** `P5ReserveState` records every realized building key.
  At unload the archive also stores the keys that are absent (destroyed,
  deconstructed or stolen). On re-entry of the same layout the full layout is
  realized and audited as before. After commit, the archived-destroyed buildings
  are removed again, exactly like a loss in combat; newly unsupported thin roofs
  are dropped silently because they already fell then. The controller already
  handles buildings destroyed mid-fight.
- **Layout stability.** A revisit now plans with at least the storage demand of
  its last accepted layout (`P5RevisitDemand`), so goods bought or consumed
  between visits do not reshape the base.
- **Changed layouts.** If the layout still changes (demand grew, or the
  debug-flat first visit below), consumable state transfers by definition
  (`P5AssetSnapshot.Match`). Damage and destroyed state are not moved to other
  buildings. Loaded shells and generator tank fuel of archived buildings that
  did not come back return as loose stock, and are recorded in the reserve
  ledger.

Live evidence:

- **Changed layout.** The debug "flat" generator builds a smaller map than the
  native world size, so the first native revisit changes the terrain input and
  the layout (`D5505860…` became `9731EB8C…`). `P5_ASSET_RESTORE_BY_DEF matched=12/17`
  transferred 726.08Wd of charge. `P5_ASSET_LEFTOVERS fuelUnits=223
  shells=Shell_HighExplosive:2 granted=0`. All ten residents were restored.
  - **Limitation:** the new layout has one mortar instead of three, so the
    capped HE plan drops the excess archived HE. This is a loss on that path,
    not a grant.
- **Same layout after real combat damage.** The native revisit re-entered the
  same layout `9731EB8CACC6F51B`. `P5_ASSET_RESTORE applied=77/77 damaged=69
  fuel=317.7393 energy=1388.46 loadedShells=Shell_HighExplosivex1 granted=0`.
  `P5_DESTROYED_RESTORE removed=20/20 defs=Turret_MiniTurret:3,PowerConduit:17
  rebuilt=0`. The 8 survivors were restored (`generated=0 replaced=0`), and the
  ledger reflects the HE and medicine used.

### Off-map expiry and off-map removal

- **Expiry.** `P5 advance disposable clock past stock expiry` jumps
  32145→1834645 without simulation (declared). The unloaded settlement's native
  stock expired, and the attack re-entry did not restock (`P5_SALE_NATIVE_ONLY
  reason=Stock acquisition rejected: no_encounter_restock`, `offerUnits=0`), as
  the existing epoch policy specifies. The resident and asset archive survived
  the jump: 8 residents restored, 77/77 assets, 20 destroyed removed.
- **Removal.** `P5 destroy archived settlement off-map` was followed at the next
  2500-tick check by `P5_ARCHIVE_RELEASED reason=settlement_removed
  unpinnedResidents=8`. Released residents return to native world-pawn handling,
  and archived goods of a removed settlement are discarded.

### Other profile, capture, native defeat, P4D and compatibility

- **Neolithic profile.** `Use neolithic P4 stage once` + `Generate playable flat
  P4 settlement` produced 8 residents, 960 pemmican (48 nutrition = the 3-day
  requirement with 1.25 safety), 600 fuel wood, no power. The readiness audit
  covers food (68.7/48) and wood (910 loaded + 600 loose / 600 required).
  - **Six-hour combat:** `passed=True`. HerbalMedicine 4 used, urgent neglect
    0. Four of eight residents were killed outright (never downed) by
    gun-armed raiders. This is a balance observation for P6, not a medical
    failure.
  - **Changed-layout revisit is not meaningful here:** the debug stage override
    is one-shot, so the revisit planned the faction's real IndustrialMid stage.
    The changed-layout diagnostic then showed `Campfire:8/0/0`, i.e. no target
    buildings. In normal play the stage is persisted per faction.
- **Capture.** A resident made prisoner of the player through the RIMAPI faction
  edit (declared test mutation) left P3 control at once. The native unload
  archived only the three free residents, and the archive restore brought back
  exactly those three (`generated=0 replaced=0`). Killed and captured residents
  are not replaced.
- **Native defeat.** All residents were killed (test API) with the visitor
  attached. Vanilla's defeat check converted the site
  (`SETTLEMENT_OWNERSHIP_ENDED parent=DestroyedSettlement`), and at the next
  check `P5_ARCHIVE_RELEASED reason=settlement_removed`. Sale and operating goods
  remain on the destroyed site as native loot (7991 units). No exceptions.
- **P4D regression.** The first run caught a real defect: the new asset-restore
  call cast a quest `Site` to `Settlement` (`PROCEDURAL_SPAWN_ABORT
  InvalidCastException`). The transaction rolled back and the native fallback ran
  correctly (4/4 roster). Fixed by calling the restore only with an archive.
  After the fix, both Core contracts commit procedurally:
  - **Rescue:** `QUEST_COMMIT strategy=core-guarded-rescue-v1 defenders=4
    nativeOutpostSuppressed=true`, with the original prisoner in its prison,
    native host and join flag, and no lord.
  - **Bandit:** `QUEST_COMMIT strategy=core-bandit-v1 defenders=6`.

  Log `p5-p4d-regression-20261013.log`.
- **Compatibility.** With Geological Landforms 1.7.13.1 and Map Preview enabled,
  the game loads (44 landforms, 0 errors), and both generation and the native
  revisit commit. The 6 residents are restored, and the changed-layout
  diagnostic matched generators 3/3 and batteries 3/3. The profile was restored
  to the five-mod baseline afterwards.
- **Changed layout, generalized.** Unmatched refuelables now return their
  unburnt fuel as whole loose units of their own fuel item. Replay:
  `P5_ASSET_LEFTOVERS fuel=WoodLog:224,ReinforcedBarrel:2 granted=0`.

**Final validation.** Release build 0 warnings/errors; PlannerTests pass (including
the new P5.4/P5 medical regressions); LayoutTests 134/134 in ten shards; V2 matrix
legacy 80/80 and clusters 80/80. The disposable process is stopped, autostart is
the immutable baseline (SHA256 `81bae97b…`), and ModsConfig is the five-mod
baseline.

**Deferred to post-release live testing (user decision 2026-10-13):** long
horizons only.
- **Neolithic three-day routine:** the partial run reached 60000 ticks; all eight
  residents ate and slept in beds, with no starvation or downing samples. Log
  `p5-neolithic-partial-routine-20261013.log`.
- **Re-proving the early-industrial three-day routine** under the new battery and
  medical rules.

**Known limits.**
- On a changed layout (storage demand grew, or a debug-only map/stage
  difference), damage and destroyed state are not transferred, and archived
  operating stock above the new layout's plan (e.g. HE for fewer mortars) is
  lost rather than kept.
- Asset Thing IDs regenerate.
- Off-map time is not simulated for archived residents.
- Expired offers do not restock on an attack re-entry, per the existing epoch
  policy.

## P5B garrison states: live (2026-10-13)

Branch `claude/p5b-garrison-states`. Core + RIMAPI, disposable profile, flat
industrial base (layout `B26CA3AD7070250A`, 7 residents, one mortar). The
test visitor was placed with `P5B park test visitor far from the walls` and
moved with the RIMAPI `pawn/edit/position` route. Read with
`P5B audit garrison states`. Logs: `.runtime/logs/p5b-garrison-live-*.log`.

| Check | Evidence |
|---|---|
| Arrival without action | `Routine → Watch` with the visitor 127 cells out. Three watchers (⌈7/3⌉) on the side facing the visitor, the mortar crewed, two defenders off duty wandering by their beds, one support worker, nobody idle at the support centre. |
| Night | Simultaneous sleepers 2–3 of 7, never above the cap. Off-duty defenders ate and wandered. |
| Alarm | Visitor moved within 35 cells: `Watch → HoldSectors trigger=approach`, `ALARM woke=2 stillAsleep=0`, every posted defender on the attacked side. |
| Side change | West to south: the pack moved 360 ticks after the south side first ranked (300-tick dwell plus refresh timing), five of five posted defenders. |
| Latch | Visitor moved away: `HoldSectors → Watch` about 2,500 ticks after the last signal. |
| Save/reload | Mid-assault save reloaded with state, sector, pending dwell and last assault intact. Saves made before the duty-tree change log vanilla `Could not find think node` warnings for running jobs; no errors. |
| Mortar | It fired at the distant visitor in `Watch`: HE 30 → 10 over about 30 game hours, all misses at 141 cells. A miss raised no signal, so the garrison stayed in `Watch` while spending its stock. Now `garrison_fired` (`Verb.LastShotTick`) starts the assault on the first shell: `Watch → HoldSectors trigger=garrison_fired`, `ALARM woke=3`. |

**Defects found live and fixed:**
- Relieved (hungry) sleepers kept sleeping through the alarm. Relief under
  assault now excludes sleep.
- The first `Watch` sector waited for the dwell from the default routine
  sector. It is now taken at once.
- Rest collapse. The runtime never passed rest levels to the doctrine. The
  watch kept the previous watchers, and ⌊off-duty/2⌋ sleepers was too few, so
  watchers fell to 2–6%. Fixes: real rest levels, one awake score with a
  sleeper hold band, and at least ⌈pack/3⌉ sleepers. The night replay
  recovered from an exhausted start (lowest about 11%, then rising). A
  three-day pure simulation now guards this.
- Posts were reshuffled within the attacked side. A defender on a covering
  post now keeps it.

**Open:** a well-rested melee defender is kept off ranged watch posts while
tired ranged defenders watch.

### Wall lights and Neolithic shelves (P5B items 5–6)

| Base | Fixtures |
|---|---|
| Industrial flat, layout `EEDCB50707D03FA0`, 8 residents | `WallLamp` 17, no floor lamps; `Shelf` 33 and `ShelfSmall` 2 (marble). Every wall lamp faces a wall-attachment support. |
| Neolithic flat, layout `979478B3FEF82EAA`, 7 residents | `TorchWallLamp` 14; `ShelfSmall` 53 (wood). Every torch faces a support. |

No exceptions in either spawn.
