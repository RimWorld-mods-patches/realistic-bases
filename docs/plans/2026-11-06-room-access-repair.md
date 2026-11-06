# Preserve room access through narrow dry ground

The captured tile 18364 failed with `no_valid_layout` after trying two field
sites and their larger compounds. In a ten-room attempt, the workshop at
`(139,74)` occupied the dry neck leading to the remaining power-room space.
Clearing only that footprint in an offline graph audit reconnected 368 unused
cells and 91 possible power-room bounding rectangles. The previous repair
search could grow streets and find rooms in currently reachable space, but did
not preserve future access before committing an earlier room.

The planner now checks a selected room footprint before committing it, including
rooms found by the connected-extension fallback. If the footprint would strand
at least one minimum legal room's worth of reachable ground, it looks for an
empty rectangle large enough for a minimum room and connects public circulation
to a legal midpoint outside one of its sides. The rectangle stays free during
routing, and the original room is reconsidered against the new corridor.
Rooms remain private footprints rather than public transit routes.

The minimum useful patch derives from the existing three-cell minimum interior:
a five-by-five outer footprint. Connectivity, rectangle scans and routing have
the existing route-node bound; work contributes to route statistics. Access
repairs share the attempt's existing corridor-growth or mountain-repair
allowance, and retries remain inside the original evaluation cap. Excavation
uses the existing street claim and budget. A failed check restores the previous
rib reservations without changing the partial layout.

Wall layers, inside setback, outside clearance, field reservations and final
validation are unchanged. The terrain margin reserves the inside setback, first
wall, outside clearance and two more cells for every extra wall layer. Removing
that margin is unnecessary for this repair and would require a separate
fortification design change.

The original native fixture remains unchanged: input `44EAF521CB7102F1`, terrain
`227B20B5C2AD3C95`, SHA-256
`5729b8bfa9428627f3aadf631a8f376a0195e16c717eda013dc43c012cc42819`.
The generated GL context was `None`, so this is a SwampHill-selected candidate,
not confirmed coverage of that landform.

Exact production replay accepts layout `8F7EE1A4713C053A`, with five residents,
163 natural crop cells and seven modeled housing positions. It uses 184,287 of
625,000 evaluations without exhaustion. Repeated four-way search produces the
same layout and passes ordinary offline spawn preflight. It accepts the existing
one-layer fallback after trying three and two layers; three-layer acceptance is
not established by this result.

A permanent geometry regression fails on the committed pre-fix planner and
passes with the repair. Boundary cases cover exhausted repair/growth allowances,
a short route limit, and water severing the connection. A second permanent test
preserves all 40,000 captured terrain/roof/trait tuples and checks the complete
settlement with the shared modeled catalog. That catalog's accepted digest is
`C35ED437B9CC5ACF`, distinct from the exact native fixture replay; its ordinary
imports are 3,489 of the captured 5,156 allowance.

Runtime replay, regression and live-check evidence is kept under
`.runtime/investigations/geological-landforms/next-cases/run-2026-11-06a/fixer/swamphill-18364`.
The full layout suite passes all 219 tests, with no skips or failures. A fresh
native check also accepts the exact same input, terrain and layout digests.
All 23 field posts have at least two P3-legal native defender paths; the longest
is 75 cells under the unchanged 160-cell cap. The saved target map has five
residents at distinct positions and 163 rice plants. Its field enclosure realizes
all 88 walls and both gates, and all crops are unroofed. Native finishing applies
494 floor cells and 39 smoothed walls. The settlement was shown paused, saved to
a separate disposable checkpoint, and the game process stopped.

Historical transaction rollback captures remain untouched; no new forced
rollback was performed. Defender movement, elapsed cultivation and save/reload
validation remain deferred.
