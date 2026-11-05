# Connected field and compound siting on swamp terrain

Tile 15155 requested SwampHill but generated `Landforms: None` and the
TemperateSwamp biome. This is captured swamp-terrain coverage, not confirmed
coverage of the named landform.

At base commit b6029e9, the planner selects two 23×23 fields whose gate is
`(98,40)` and street terminal is `(98,47)`. The connection search reaches only
six legal cells. Preflight counted room capacity in a different, disconnected
patch of the compound reservation; repeating or growing the reservation never
connects the field to that patch. Both added regressions failed before the fix:
the field terminal did not share the anchor's region, and the complete
settlement rejected before room placement.

The user selected connected dry siting, with smaller fields when needed.
`P4BJointSitePlanner` now requires capacity in the field terminal's connected
placeable region and chooses its anchor and gate alternatives from that region.
Existing terrain setbacks, water exclusions, excavation accounting and final
route validation remain in force. Existing mountain routing can still charge
rock excavation to its allowance.

A funded partial-food search first retains the established field footprint.
Only when it finds no connected site does it try a fixed family of smaller
squares, sharing the original three-phase pair-evaluation allowance. This case
uses a 20×20 field after the 23×23 footprint fails. The smaller field changes
physical production, while the original nutrition demand remains authoritative.
Existing funded hydroponics preference and finite first-harvest food imports
cover shortfalls.

The captured regression keeps ten residents, a target of 308 crop cells, one
wall layer, 760 excavation cells, 10,313 ordinary import value and 2.5 million
room evaluations. The native crop season is 30 days. The exact native input
now accepts as layout `4E67D3A906EE994F`, both offline and in the game:

- 235 main-field crops plus 18 courtyard crops; 253 actual rice plants.
- Ten actual residents at distinct housing positions; twelve modeled housing
  positions and zero unlit cells in all twelve ordinary rooms.
- 178 initial meals, five fueled generators, five batteries, and eight days of
  fuel: 150 embedded and 180 stored base chemfuel.
- 4,667 ordinary import value, plus 1,520 for finishes, within the unchanged
  allowance. Allocated sale goods remain separate from those base reserves.

The planner retains a fully validated import-funded result while seeking a
hydroponics alternative. No such alternative was accepted within this case's
unchanged evaluation cap, so it returns the validated fallback at the cap.
Reaching the cap does not invalidate a finished plan or permit exceeding it.

Live verification also exposed an independent optional-flooring exception:
`All` returned true for an empty list, then floor selection indexed `cells[0]`.
One room with no eligible new floor cells consequently omitted every room's
finish. The expanded finish regression reproduced that exception and passes
with a nonempty-list guard. Final native generation applied 505 floor cells and
four smoothed wall cells without the omitted-finish exception; existing finish
eligibility and budgets are unchanged.

Regression anchors are preserved:

| Capture | Input | Terrain |
| --- | --- | --- |
| Original | `A476F0309828D451` | `498B952A3DB250C6` |
| Current native | `2F22723862984DAB` | `9B61FE510FD0271E` |

All 40,000 original and current terrain/roof/trait tuples match; the snapshot
version label changes the terrain hash. The original recorded rejection was
`no_feasible_field_compound_pair`; at b6029e9 its exact legacy input instead
advanced to `no_valid_layout`. With this fix, the legacy input still lacks food
facts and rejects the disconnected sites before packing. The complete current
native input uses sourced food facts and accepts without altering either capture.

All 215 layout tests pass, with no skips. Final focused checks cover the expanded
finish regression and deterministic connected-site selection under parallel
search. The native build has zero errors and four existing warnings. The final
base was shown paused, saved separately, and its disposable process stopped.
No elapsed cultivation, defender movement or save/reload acceptance is claimed.
Original item, plant, terrain and layer rollback captures remain untouched; no
fresh forced rollback was performed.

Runtime fixtures, replay commands, logs and verification are under
`.runtime/investigations/geological-landforms/next-cases/run-2026-10-31b/fixer/swamphill-15155`.
