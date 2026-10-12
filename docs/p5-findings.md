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
