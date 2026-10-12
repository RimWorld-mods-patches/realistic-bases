# P5 — Finite encounter supplies and canonical stock

2026-10-12 · Authorized continuation from P4E `be39af9` on
`codex/p5-encounter-stock`. No merge/push is implied.

## Scope and invariants

The [grand plan](implementation-plan.md) and [supply scope](supply-and-mvp-scope.md)
remain authoritative. Three routine days and six combat hours are provisional
coverage targets, not continuous maximum fire. Declare actual per-equipment
duty, roster, connected loads, charge, stock, spoilage and labor. Count no future
harvest/manufacture without demonstrated inputs and throughput. Retain larger
existing reserves. Essential finite support must work without player orders.

There is one physical quantity, allocated to operation or sale. Preserve native
offer Things/attributes and supported identity; never create a loot copy. An
explicit persisted stock epoch distinguishes absent, depleted and expired stock.
Empty reads, hostility, arrival/reload and re-entry never restock. Preserve native
trade authorization and supported off-map restock; pause expiry while an encounter
owns the goods. Unsupported pawns/animals/containers/trader replacements retain
original ownership with diagnostics. Imports do not grant technology.

Beyond active reload, preserve supported damage, depletion, casualties, capture,
fuel/charge/loaded ammo and asset ownership. Observe native destroyed-settlement
removal rather than inventing a revisit. Resolve P0's coarse regeneration only
for ordinary supported settlements; do not change P4D quest contracts. Preserve
P4E exact-item rollback and pre-existing deferred roof-collapse queue recovery.

P5A is separate: generator ventilation/full initial fill, shell minimums,
cabinet orientation, missing floors and richest-spacer wall materials are retained
in the grand plan, not silently absorbed here. No traps/IEDs, broad economy,
seasonal sustainability, P6/P7 implementation or extra agents.

## Validated checkpoints

| ID | Implementation | Required evidence before completion |
|---|---|---|
| P5.0 | Source/runtime baseline and bounded plan | Native stock/buy/sell/expiry and map-lifecycle source; baseline limitations; standalone plan commit |
| P5.1 | Immutable stock/epoch/conservation contract and native ownership spike | Pure transitions and conservation tests; installed-method startup resolution; explicit absent/existing/empty acquisition without planner mutation |
| P5.2 | Quantified finite encounter readiness and support | Actual roster/equipment/load/duty audit; legal stocked capacity/value; three-day routine/six-hour combat and separate outage observations; worker/generator/fuel/ammo failures |
| P5.3 | Ordinary sale-stock ownership adapter | Native buy/sell before attack, partial trade/split/merge/consume/loot/destroy/emergency transfer; exact quantity/attribute conservation; absent/empty/expiry/hostility/capture/rollback |
| P5.4 | Supported unload/re-entry persistence | Same survivors/assets and removed casualties/goods; damage/fuel/charge/loaded ammo; capture ends NPC control; native defeat/removal distinguished; active/unloaded save/load |
| P5.5 | Integrated support matrix and closeout | Relevant complete suites/Release; native horizons and failures; P4D original-target regression and installed compatibility; stopped process/baseline restored; explicit boundaries |

Commit each validated checkpoint (or clearly labeled independently validated
subcheckpoint). Planning, a getter invocation, an API success or a screenshot is
not acceptance. Record failed attempts and exact limitations in
[findings](p5-findings.md). If a contract is blocked, finish independent work and
record the gap; do not mark the milestone complete until all required gates pass.

## Initial architecture decisions to verify

- Only the exact native `Settlement`/`Settlement_TraderTracker` path is initially
  eligible. A dedicated runtime acquisition service may establish a native offer;
  pure planning receives copied immutable item facts and cannot call game getters.
- Probe actual ownership and capacity before transferring existing Things. Native
  buy/sell methods split and move Things, so identity tracking must cover split,
  merge, destruction and partial trades—not merely the original stack IDs.
- A physical map stock authority must coexist with native serialization without
  putting a spawned Thing into a second ThingOwner. Storage growth must retain
  already published quantities; unsupported integration fails visibly, never
  destroys original merchandise or silently truncates it.
- Native map teardown passes pawns to the world before clearing map references.
  Persistence must intercept at a verified pre-teardown boundary, not attempt to
  recover cleared Things from a late MapRemoved callback. Choose and test a
  bounded ownership-preserving archive/restore design before enabling it.
- Diagnostics distinguish planned coverage, measured operation and negative
  cases, and report real shortfalls at actual tested loads.
