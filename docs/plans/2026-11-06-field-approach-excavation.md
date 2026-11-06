# Keep field connectors consistent with ordinary excavation

SwampHill-selected tile 110256 rejected before room placement. The captured
native map context says `Landforms: None`, so this is an unconfirmed candidate
failure, not named SwampHill coverage. Its final attempt tried ten sites and
510 layouts with zero room evaluations: every field-to-street connection failed.
The room evaluation budget was not exhausted.

Joint siting counts connected room capacity through natural rock. Ordinary
streets and rooms can excavate rock without thick roof, within the settlement's
excavation allowance. The field connector used those same excavation checks,
but its caller only enabled excavation for the rock-protected doctrine. An
ordinary settlement could therefore start its street in a region that siting
considered connected, yet reject its field link without trying a legal tunnel.

A diagnostic audit held the map, street, reservations and setbacks fixed. For
one final-layer attempt, the field terminal `(141,76)` reached only 256 open
cells and could not reach the street at `(141,46)`. Ignoring reservations or
geothermal occupancy did not connect them. Allowing only rock already legal
under ordinary excavation did connect them. This isolates the excavation
dispatch mismatch; reducing wall clearance does not address it.

The user chose to enable the existing bounded excavation connector for ordinary
settlements too. It still tries an open route first, minimizes new excavation
before route length, shares the original per-interface node allowance, and
claims nothing until a complete funded route exists. `CanExcavate` still forbids
ordinary excavation under thick roof. Water, occupancy, fortification setbacks,
resident demand, food accounting and final validation retain their existing
rules. No excavation or search cap is raised.

The route regression fails before the fix on an ordinary five-cell rock
barrier, then checks exact charging, insufficient funds without partial claims,
thick-roof rejection, water rejection, preference for an existing dry route,
the shared route-node cap, and immutable terrain. A separate full-map regression
retains all 40,000 captured terrain/roof/trait tuples and requires complete
settlement acceptance with ten residents and unchanged physical limits.

The immutable native fixture anchors are input `28CF9F05EFDBF3E9`, terrain
`13C34CB83909B4F5`, requirements `FD96C80E9BAF05D3`, catalog
`197E1E942D99277A`, resources `BC14232905946407`, SHA-256
`71f817a2605e24df55aa19ef9bcde88a678c62e1e26b33204e938d975c9d86aa`.
Evidence is under
`.runtime/investigations/geological-landforms/next-cases/run-2026-11-06c/fixer/swamphill-110256`.

The exact fixture replays to `7FB13C7282F402CC`; the shared modeled catalog
regression produces `2CEAF1CD4E62CD2D`. Both retain ten residents, 240 outdoor
crop cells, 62/760 excavations and 8,426/10,313 ordinary import value. Fourteen
distinct housing-floor positions remain. The existing one-layer fallback
accepts a completed validated candidate at the unchanged 2.5-million-evaluation
cap; `BudgetExhausted=True` describes the ended search, not an invalid plan.
The full layout suite passes all 221 checks with no skips or failures. The
production mod builds with zero errors and four existing warnings.

A fresh native generation accepts the same input, terrain and layout digests.
It has ten actual residents at distinct saved positions and 240 unroofed rice
plants. All 24 field posts have at least two eligible native defender paths;
the longest is 130 nodes under the unchanged 160-node cap. It realizes all
84 field walls and both field gates, excavates 62 cells, and applies 572 floor
cells and 72 smoothed wall cells. All 60 planned power consumers are powered
after native reconnection. The one-layer outcome uses the unchanged three-to-
two-to-one fallback; three-layer acceptance is not claimed. The base was shown
paused, checkpointed separately, and the exact disposable process was stopped.

Original rollback records and fixtures are preserved. This planner rejection
had `mutationCount=0`; it did not reach spawn or rollback. No new forced
rollback or defender movement validation is implied.
