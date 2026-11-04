# Coast room-extension candidate budget

The captured Coast tile26727 (selected as a Cove candidate) rejected with
`no_valid_layout` despite having usable room space in its reserved compound.
Input `4E88F5FFF1CCA684` and terrain `8A0814BA5DA0C78A` remain unchanged.
The user reviewed a completed offline probe before implementation.

The room-extension fallback spent its furnishing-candidate slots on rectangles
that already intersected blocked terrain or occupied cells. It reached the
per-room cap before considering a reachable valid footprint. Larger-settlement
search already used an integral occupancy filter; the seven-resident case did
not use that filter. Its evaluation cap was not exhausted, so increasing the
cap or reducing the population would not address this search failure.

The repair applies a constant-time footprint prefilter before spending a
candidate slot. Narrow search still permits ordinary shared walls and absorbable
exhaust-pocket walls/seals on the boundary, with an empty interior and a free
door cell. The existing exact placement/furnishing/thermal/access checks decide
whether a surviving candidate is legal. The broader search's existing strict
filter and route-around-room behavior remain unchanged. No new routing policy,
room-size relaxation, site selection or budget increase is introduced.

A regression gives the search eight candidate slots and a one-cell approach to
one usable room chamber. It failed before the repair. It now places the room
with free, shared-wall and pocket/seal boundaries, rejects an occupied interior,
and preserves input/terrain digests. This specifically prevents a strict
all-free filter from accidentally removing existing shared-wall eligibility.

The normal captured-input replay now passes layout and spawn preflight with
seven residents, eleven rooms,240crops(minimum216) and sufficient housing floor.
It uses166022of1225000evaluations across27attempts/3sites, without exhaustion.
The accepted production-planner layout is `0F75E674DFC8B0D1`. All eleven audited
rooms have normal modeled light coverage independently of sun lamps.

The reviewed diagnostic witness was `D0C6463C659D1836` at71524cumulative
evaluations. It finished the first viable candidate directly. The normal
planner evaluates its existing candidate pool and selects the final layout;
these are separate recorded results. Neither is native spawn/path evidence.
All physical transaction and recorded rollback invariants remain unchanged.

The previously skipped `GlPeninsulaTile` regression is also enabled again and
passes through the normal runner. It is no longer a permanent suite skip.

Exact baseline snapshots, isolation probes, replay commands, the comparison,
and final verification logs are retained under the ignored investigation:
`next-cases/run-2026-10-31b/fixer/coast-26727/`.

Verification completed: all204layout tests pass with zero skips or failures;
the planner/garrison suite passes. The mod build has zero errors and four
existing warnings. No native generation or movement claim is made for this
repair. The original spawn transaction and historical rollback records are
unchanged.
