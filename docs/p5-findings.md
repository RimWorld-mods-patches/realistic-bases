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
