# P3 playable defensive encounter: findings and validation

2026-09-20 · Status: in progress

## Scope and boundaries

P3 replaces the two-role P0 diagnostic Lord with a settlement-scoped defensive encounter controller. It covers sectors and usable firing posts, weapon-aware exclusive role assignment, bounded local response, gate defense, reserves, breach response, safe needs relief, inner fallback, staffed mortars, finite combat supplies, and narrow essential support. Defenders must not become an unrestricted assault force.

The current MVP supply revision governs this work. Routine readiness targets a provisional three game days and combat readiness a separate six game hours at declared firing duty. These are planning defaults to calibrate, not unbounded sustain claims. P3 will physically consume finite stocks and will keep stock-purpose metadata capable of later separating operating reserves from trade goods, but actual settlement trade-stock ownership/reconciliation belongs to P5. Full seasonal operation, repeated harvests, and continuous manufacture are post-MVP.

P4 terrain/fortification variants, P5 trade/persistence integration, specialized EMP/smoke tactics, and broad manufacturing are excluded. P0's coarse unload/re-entry state and regenerated defender limitation remain accepted; P3 must validate active-encounter save/reload, not claim full revisit/casualty persistence.

## Fixed implementation invariants

- Planning remains deterministic, snapshot-based, bounded, and separate from map mutation.
- One defender has one active assignment. Invalid posts cause stable reassignment or fallback, not reservation/job oscillation.
- Destination and traversed route must remain within role-specific defended bounds. Ordinary harm, hunger, elapsed time, or a remote target must not transition the garrison into unrestricted assault.
- Closed gates and solid walls must still leave reachable, occupiable cells with real lines of fire. No wall-top or through-wall assumption is accepted.
- Industrial mortars must be staffed, reachable, unroofed, supplied with finite legal projectiles and any reload/barrel material required by RimWorld 1.6. Incendiary use is bounded and accepted only after fire/friendly-risk validation. Tribal settlements receive no illegal mortar, firearm, turret, or electric substitute.
- Essential hauling, refueling, tending, and needs relief use narrow settlement-owned adapters and real items. No recurring free food, fuel, ammunition, medicine, or barrel generation is permitted.
- Player pawns, player work settings, ordinary NPCs, and unrelated maps remain outside the controller's scope.
- Primary, repair, recovery, and fallback paths contain no traps or IEDs.
- Runtime evidence must state measured electrical draw, firing duty, consumption, and reserve duration. P2's observed 360 W outage load is not evidence for added P3 equipment and must not be restated as the earlier 650 W model.

## Checklist and evidence policy

| Step | Implementation | Required evidence before commit | Status / commit |
|---|---|---|---|
| P3.0 | Scope, checklist, boundaries, and evidence ledger | Documents agree; P4/P5 remain unstarted | In progress |
| P3.1 | Pure doctrine and assignment/state model | Determinism; weapon/post compatibility; exclusive roles; pursuit plus route bounds; breach/local response; safe relief; inner fallback; invalid-post stability | Pending |
| P3.2 | Encounter layout, mortar court, finite supply and capacity budgets | Usable post geometry; mortar reachability/unroofedness/staffing; declared duty quantities; active-rule materials; power delta; tribal hard exclusions; inherited planner properties | Pending |
| P3.3 | Runtime Lord/controller, support work, consumption, persistence and diagnostics | Build; focused live job/state/consumption/save-load checks; no broad behavior patch; recovery/fallback unchanged | Pending |
| P3.4 | Full encounter/regression matrix and exit decision | Multiple approach battles; night; long range; gate breach; incendiary; operator/power removal; exhaustion; invalid post; needs relief; active save/reload; tribal; Core/GL; player unaffected | Pending |

Source inspection, pure tests, compilation, map-generation audits, and live battle observations are recorded as distinct evidence categories. A duty assignment, successful plan, or clean build is never reported as proof that a pawn occupied a post, a mortar fired, a stock was consumed, or a battle stayed local.

## Commit mapping

No P3 implementation commit yet. P3.0 will be filled with the standalone documentation commit before code changes begin.

## Evidence ledger

### Source and API evidence

Pending.

### Pure/state tests

Pending.

### Build and static checks

Pending.

### Core runtime observations

Pending.

### Geological Landforms / Map Preview regression

Pending.

## Known limitations and next action

The existing implementation still assigns one defender to a fixed P0 hold point and all remaining defenders to the P0 sequential work probe. It has no P3 sectors, exclusive role model, local breach state, mortar, finite magazine, or encounter persistence. The next step is P3.1: implement and test the deterministic defensive doctrine and assignment model before adding map objects or runtime behavior.
