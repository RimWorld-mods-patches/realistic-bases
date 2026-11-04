# Oasis candidate: denser fields with funded hydroponics

Candidate 35518 now generates an accepted settlement with 150 natural crop cells,
17 hydroponics basins and one sun lamp, retaining all 11 residents and a true
three-layer perimeter. The previously accepted plan had 115 natural cells and
21 basins. The user chose the better field with funded hydroponics, retaining
the original budgets and final acceptance rules.

The candidate's original and current native GL contexts both report
`Landforms: None`; this is not confirmed Oasis landform coverage.

## Historical rejection and current input

The original fixture remains unchanged at
`.runtime/investigations/geological-landforms/next-cases/run-2026-10-31b/fixtures/19-oasis-tile-35518.cs`.
Its three recorded layer attempts retain input digests `14471273EB402395`,
`9B78D245AB9AFA7C` and `E54C95D00EBC1503`, terrain `EE173D1088B85086`, and
`no_feasible_field_compound_pair` with the original research table.

The old catalog lacks meal nutrition and cultivation facts. A separate enriched
probe sources those facts from the resolved native 1.7.4995 catalog and uses the
current research table. Existing food fallback then accepts a complete plan,
showing the historical rejection did not need a new food allowance or lower
population. The original full natural-production target remains 339 cells.

The fresh native capture includes the complete current catalog and 45 native
rice-growing days: input `C8C4A38D50BBE234`, terrain `6343F62425C755EE`.
All 40,000 terrain-cell tuples match the original capture; only the source version
label changes its terrain digest. The baseline native layout was
`3C8A0E0F080C8920`, with 115 natural cells and 21 basins.

## Field selection cause

Partial-food preflight accepted results from its four-cell sampling lattice.
Fine sampling happened only when that search found no feasible pair, so a valid
sparse field prevented the planner from seeing denser nearby offsets. The coarse
candidate list began with 133, 127 and 115 crop cells. The first two did not yield
the accepted settlement; the third did. Fine sampling finds 150-cell candidates
under the existing field, compound, access and terrain rules.

The old field was at `(109,157)`, size 24x24, with compound `(86,86)`, size 69x69.
Its 115 crops comprised 23 soil and 92 gravel cells. A fine-grid candidate at
`(100,107)` has 58 soil and 92 gravel cells. These are crop counts under the
existing fertility threshold, not a new weighted fertility or annual-yield model.
The improved complete settlement moves its compound as well as its field; this
is not a claim that the field can simply be moved through the existing rooms.

Partial-food fallback now starts its bounded search with fine field origins.
The candidate count, pair-evaluation caps, room-search evaluation cap, excavation,
food and population requirements remain unchanged.

## Hydroponics preference and fallback

Fine sampling alone exposed another stopping issue. An imports-only layout
could pass at the first site and end the search before a later site completed
an affordable grow room. Candidate ordering already preferred hydroponics within
a pool; it could not compare a later site once the first site returned.

The planner now reserves a fully finished, validated imports-only layout when
legal hydroponics and a production shortfall exist. It continues the same bounded
search. A subsequent funded hydroponics plan can win; if the search ends without
one, the best reserved imports-only result remains accepted. It never grants
unfunded basins or discards the valid fallback merely because cultivation is
unaffordable. No live acceptance rule was relaxed.

## Regression and acceptance evidence

The permanent captured-terrain regression first failed at the 150-cell assertion.
An isolated fine-sampling-only source probe then failed at the hydroponics
assertion. The normal repaired planner passes both, including all 11 residents,
three-layer breach depth, eight-day food/fuel funding and ordinary spawn preflight.
A separate test prices a legal basin beyond the budget and verifies that the
fully funded imports-only fallback still passes. Shared captured food definitions
and the terrain/roof-ID reader were extracted from the prior IceSheet tests;
those tests and the exact native IceSheet layout remain passing.

The repaired native layout is `4C103BCEA62FC70D`, on the same captured input as
the earlier baseline. Native `PROCEDURAL_SPAWN_COMPLETE` and exact offline replay
both pass. Native save evidence confirms:

- 150 outdoor rice plants and 68 basin rice plants, with no overlap.
- 17 basins, one sun lamp, 196 initial meals, 13 generators and ten batteries.
- 11 residents, 14 rooms and a three-layer perimeter.
- Ordinary imports 10,334 / 11,344; food and generation fuel cover eight days.
- 586,103 / 3,025,000 room-search evaluations, 15 attempts and two sites, with
  no budget exhaustion; the final housing model has 18 positions for 11 residents.

The better-field plan uses more of the ordinary import allowance than the earlier
accepted base, while remaining within the same limit. It is not presented
as globally cheapest or as proof of an optimal field over all possible layouts.

The improved base was shown paused and saved as
`Oasis-Refined-Field-35518-2026-11-04.rws`; the exact disposable process was then
stopped. The earlier accepted checkpoint is also retained. No elapsed crop-growth,
defender-movement or save/reload acceptance is claimed. No transaction/rollback
code changed; original rollback evidence, including Caldera's 899/899 plant and
item/terrain/layer restoration records, remains intact.

Artifacts and final validation logs are under
`.../fixer/oasis-35518/`. Opt-in planner source clones were used only for diagnosis;
the test output was cleaned and rebuilt with the normal planner afterward.

Validation: **209 layout tests passed, zero skipped, zero failed**. Original
rejection anchors, the unchanged native IceSheet layout and the refined native
Oasis layout all replay exactly. Native build succeeds with zero errors and four
existing warnings. All 13 ordinary rooms pass the offline lighting audit (the
fourteenth room is the separately handled magazine). The ordinary test assembly
was rebuilt without captured fixtures after the replays.
