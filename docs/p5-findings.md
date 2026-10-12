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
