# Geological Landforms generation rejections — 2026-10-29

Both recorded rejection cases now complete native generation on RimWorld
1.7.4990 with the current checkout. Defender movement/combat validation is
explicitly postponed. Original 1.6 captures and rollback evidence remain intact
under `.runtime/investigations/geological-landforms/`.

## Tour confirmation

`Tile.Mutators` selects candidates only. The tour reads GL's active
`Landform.GeneratingLandforms` after GL prepares map generation and before cleanup.
Only an exact selected-form ID match emits `GL_TOUR_CONFIRMED`; otherwise it emits
`GL_TOUR_UNCONFIRMED`. Disposable recorded-tile actions revisit Atoll 68248,
Badlands 109873, and Caldera 89176 in the original `narwhal` world.

The live checks confirm Badlands as applied GL coverage. Atoll and Caldera
still have native GL `Landforms: None` and remain unconfirmed even when spawning
succeeds. Canyon/Cirque's original None contexts are also not coverage. Cliff's
original timeout preceded map/planner events; it is not either rejection.

## Badlands: compound setback incorrectly excluded legal field fences

The original fixture reproduced `no_feasible_field_compound_pair` with input
`FCEA3634AAD7FDDE` and terrain `79AF29C72969CA3E`. Its joint-site search performed
7,652 pair evaluations and scanned 30,749 origins before returning zero pairs;
zero layout attempts did not mean no preflight work occurred. The fresh 1.7
capture reproduced the same rejection with input `9AE7456F29D359EE`.

Field-fence preflight required `LayoutTerrainGrid.CanPlace`, which includes the
compound's hazard setback. That excluded dry, buildable fences beside water
although the final field validator permits those physical cells. On the original
Badlands map, eligible fenced squares under that setback did not supply the
216 required crop cells. Disabling attachment checks alone still found zero sites.

The fix adds one final dense field-origin pass only after both existing searches
fail. This pass checks the fence by its physical open-ground rules. It retains
map-edge margins, compound capacity and hazard setbacks, attachment clearance,
full crop demand, post access/fire lanes, and final layout/spawn validation. Each
phase has the existing pair-evaluation cap; there are at most three phases.
Successful established sites, including Caldera, keep their existing search.

The dry-fence regression failed before the fix and now passes, including input
immutability, finite work, and deterministic selection at planner degrees 1/4.
The complete captured-input replay additionally requires a successful layout and
spawn preflight. The fixed original layout is `EBE0C927FBF2AAD1`, with seven
residents and 222 crop cells, without changing either input digest anchor.

Current regression command (the old `--expect-rejection` command is historical
baseline evidence and is expected to fail after this fix):

```sh
docker run --rm -v "$PWD":/mod -v rimworld-nuget:/root/.nuget/packages -w /mod mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet run --project Source/RealisticBases.LayoutTests/RealisticBases.LayoutTests.csproj -c Release \
  -p:LivePlannerFixture=/mod/.runtime/investigations/geological-landforms/02-badlands-tile-109873-0.cs \
  -- --live-fixture --expect-fixed-layout EBE0C927FBF2AAD1
```

`--expect-fixed-layout` preserves captured inputs and replaces only the old rejected
outcome assertion. `--require-success` performs the same validation without pinning
a new layout digest. Baseline rejection logs and fixture constants are preserved.

## Caldera: nearest-eight waypoint pruning omitted legal gateways

The original planner still produces `7C899E3CEF005B1D` from input
`1D9ACE282BC88CD1`. The subsequent native spawn rejection was post `(145,43)` with
zero eligible defenders. Four reported native paths were found, with 57/58/59/58
nodes and first outside cell `(131,34)` in `NodesReversed` order, via `(143,47)`.

`P3DefenderRoute.NextWaypoint` considered only the eight nearest entry gateways.
Nearby sally ports displaced legal compound/field crossings such as `(117,47)`
and `(124,47)`. The fix searches all distinct declared entry gateways, still in
distance order, and retains area, aggregate length, exterior-transfer, exposure,
and minimum-two eligibility checks. No movement area or live safety rule expanded.

`P4BDefenderArea` and `P4BFieldPostPath` share the live area construction and
all three spawn decisions with the offline regression. Before the fix, its four
modeled starts had zero eligible paths. Afterwards all four are eligible; the
nearest-eight negative control still rejects all four. A small normal-suite test
also failed before the fix and verifies that a broken corridor still rejects.

This is a modeled spawn-gate regression, not an exact PawnPath replay. The capture
contains terrain traits but lacks native per-pawn costs and complete native node
sequences. The eight-way model's direct paths also have 57–59 nodes; its first
outside cell is `(137,33)`, while native logging recorded `(131,34)`. The native
outside cell is explicitly asserted to remain forbidden. Planner digest equality
alone is not considered reproduction of the spawn rejection.

```sh
docker run --rm -v "$PWD":/mod -v rimworld-nuget:/root/.nuget/packages -w /mod mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet run --project Source/RealisticBases.LayoutTests/RealisticBases.LayoutTests.csproj -c Release \
  -p:LivePlannerFixture=/mod/.runtime/investigations/geological-landforms/03-caldera-tile-89176-0.cs \
  -- --live-fixture --field-route
```

## Native RimWorld 1.7 check

The installed game changed to 1.7.4990. At the user's request, the existing net472
mod assemblies were tested through that game's compatibility runtime in separate
copied disposable profiles. All six test mods loaded; GL loaded 44 landforms;
GAME_LOADED, native autoload, profile, exact process, and RIMAPI Playing were verified.
The original profile/save was preserved. The initial 1.7 run confirmed Caldera's
spawn fix but still rejected Badlands. After the field-siting fix:

| Case | Input / layout on 1.7 | Native result |
|---|---|---|
| Atoll | `04B8F4AD5408B6FC` / `F11B39F902B473A4` | Spawn complete; GL unconfirmed |
| Badlands | `9AE7456F29D359EE` / `C7F52ED8AD2CEF74` | Spawn complete; GL confirmed; seven residents, 222 crops |
| Caldera | `BB1491BFB76BEF2A` / `AA8932ED91F86D65` | Spawn complete; GL unconfirmed |

Badlands' native field-post audit passed all 19 posts with at least two eligible
defenders each (longest legal path 63 nodes). Caldera's passed all 22, including
`(145,43)` (longest 80 nodes; seven via-gate paths). These execute actual Map/PawnPath
spawn checks. Manual controller audits show residents alive and inside bounds;
no tick-based defender movement/combat probe was run. The game stayed paused at
583 throughout generation. Native 1.7 inputs differ from the original 1.6 captures,
so this does not claim an exact native 1.6 replay or general 1.7 compatibility.

Evidence and fresh C# captures: `live-check/1.7/` (before the field fix) and
`live-check/1.7-field-fix/` (after it), beneath the original investigation directory.

## Rollback and validation

The preserved original Caldera abort has `itemLedgerRestored=True`,
`plantsRestored=899/899`, `provisionalRoofCollapsesDiscarded=0`, and `recovered=True`;
its rejection has `terrainPreserved=True layersPreserved=True`. Transactions and
rollback behavior were not changed. These remain historical live assertions;
no additional forced rollback was performed on 1.7.

Release compilation, all 89 planner/garrison tests for the routing change, and
all 19 relevant P4B layout tests passed (zero skips/failures). Original Badlands
fixed-success replay, fresh 1.7 Badlands fixed-success replay, the Caldera modeled
spawn gate, and native generation checks provide separate evidence. Defender
movement validation remains postponed.

## Next batch: Coast street-start recovery — 2026-10-30

For the next rejection batch the user selected bounded nearby street starts
within the same compound. A two-cell-wide street could fail even when its
single anchor cell was placeable. Recovery now considers at most 144 starts
within eight Manhattan cells, only when the original street is blocked or too
short; existing attempt seeds vary the local search. The compound, field,
population, excavation and search limits remain unchanged, as do final checks.

Coast input `D47AF42378F0F7C7` and terrain `5AF8227B5E031447` now produce layout
`BDCB78E3F0B8AD98` at degrees 1/4 and pass offline spawn preflight. A native
1.7.4990 check on tile 55094 reproduced those exact digests, confirmed Coast,
and spawned eight residents with 272 crops. All 15 outside posts passed native
path eligibility. Release build and all 20 relevant P4B layout tests passed.

Detailed evidence is in
`.runtime/investigations/geological-landforms/next-cases/run-2026-10-29a/fixer/coast-resolution.md`.
The CoastCorner housing/spawn policy is awaiting a user choice. CoastalIsland,
CoveWithIsland and the Crater-candidate route failure remain open. Movement
validation remains postponed; no new changes have been committed.

## Next batch: CoastCorner housing floor — 2026-10-30

The user selected enough reserved housing floor during planning, retaining
housing-only spawning. Housing furnishing now requires one reachable empty
floor cell per resident after all fixtures. Its conservative count includes
wall-attachment footprints; final validation independently checks planned
housing floor after owned excavation and plant clearance. The native
Standable/GetEdifice check and all resident/bed requirements are unchanged.

The original CoastCorner input `E722DB5C914F8E79` and terrain
`46DEC693D5B6AEC4` now produce `175571737C37739F` at planner degrees 1/4.
Its housing interior grows from 24 to 27 cells and reserves nine conservatively
modeled floor cells. Native 1.7.4990 generation confirms 10 actual housing-only
spawn cells, eight planned beds, eight spawned residents, 272 crops, and all
26 outside posts passing native path eligibility. GL confirms CoastCorner.

Coast was rechecked under the combined policies: the same original input and
terrain now generate `7D8DB8AFECD93D0E`, with eight residents, 272 crops, 13 native
housing-only cells and all 14 outside posts passing. This supersedes the
street-only `BDCB78E3F0B8AD98` intermediate layout above. Both native checks stayed
paused; defender movement/combat validation remains postponed.

The full layout run passed 186 checks and found two seeded renewable fixture
failures. Both fixture inputs were refreshed to a funded twelve-resident,
seed-five wind/solar layout; all original renewable, storage and overlap
assertions remain intact and both focused reruns pass. One pre-existing GL
peninsula case remains skipped. Release build and the new furnishing regression
pass. The original Caldera layout/route regression also remains unchanged.

Details: `next-cases/run-2026-10-29a/fixer/coastcorner-resolution.md` beneath the
runtime investigation directory. Current CoastalIsland still exhausts its
existing budget and is awaiting a user policy choice. No new changes have
been committed, and no search or defender-route cap has been increased.

### CoastalIsland exterior movement repair (2026-10-30)

The user identified a valid route from outside field gate `(74,94)`, south and
east to `(98,78)`, then along the surviving ordinary arrival to `(98,0)`. The
119-cell witness contains 36 standable shallow-water cells in its final suffix.
The old whole arrival crosses the proposed field wall, so reusing it unchecked
would be invalid. The dry-only field search exhausted its reachable region;
this was a movement-rule mismatch, rather than evidence for a larger budget.

The user selected matching ordinary exterior movement while keeping arrivals
outside the compound. Field arrival and secondary-gate reconnection now use
`LayoutExposure.Walkable`. Field-wall exposure and open-gate access validation
use the same pawn movement rule, including detecting exposure through shallows.
Crop soil, walls, field gates, cover and field-interior reservations retain
their dry-ground requirements. Deep water, physical blockers and compound
re-entry remain forbidden, and all budgets/final checks are retained.

The focused regression first failed at the actual `RouteOutside` rejection.
Its tracked `coastalisland-field-route.json` preserves all 40,000 terrain cells
(traits, terrain IDs and roof IDs), source version, proposed-wall blockers and
the old arrival from failed candidate `3777140E7656D31C`. Terrain digest is
independently checked as `964955B192B71C51`; source input is `0CAF3237DF5C8CBB`.
Additional checks cover shallow versus deep water, new-wall and door blockers,
dry foundations/crops, and a shallow-water breach behind closed field gates.

Full captured replay now accepts layout `D221B810DFCA5DB8`, with 10 residents,
327 crops and 20 modeled housing-only spawn cells, in 253,888 evaluations rather
than exhausting 2,500,000. Degree-one and repeated degree-four runs agree.
Native RimWorld 1.7.4990 confirms GL CoastalIsland in the generated map context,
the exact input/layout digests, 10 residents/beds, and `PROCEDURAL_SPAWN_COMPLETE`.
The chosen layout has zero outside field posts; this is generation evidence,
not defender-path or movement coverage. The game stayed paused at tick 583,
was stopped, and its copied profile restored to the five-mod baseline.

Original Badlands anchors and fixed layout `EBE0C927FBF2AAD1` still pass. Original
Caldera remains `7C899E3CEF005B1D`, with its separate spawn-gate regression still
demonstrating restricted eligibility zero versus fixed eligibility four. The
original rollback records and generation transaction code are untouched.
Runtime evidence is under `next-cases/run-2026-10-29a/fixer/`, including
`coastalisland-movement-replay.log`, `coastalisland-movement-degree4.log`, and
`live-coastalisland-movement/`. No newer changes have been committed.

The complete layout suite subsequently passed all 190 runnable checks, with
zero failures and the existing GL peninsula skip. Coast and CoastCorner retain
their fixed digests (`7D8DB8AFECD93D0E` and `175571737C37739F`) and pass the
required-housing-floor replays. The separate CoveWithIsland candidate replay
(actual generated GL Coast) still exhausts 900,000 evaluations: no complete
room layout, predominantly dining placement failures. It does not share the
demonstrated field-arrival cause. Its next repair direction is awaiting the
user's choice; no budget increase or room-packing change has been made.

### Loose stone chunk clearance (2026-10-30)

The user's complete manual arrangement exposed a capture/classification problem.
Native 1.7 inspection of the same tile and original input confirmed that 29 of
the 42 blocked cells under its rooms held loose granite/sandstone chunks. Native
`Standable` is false on those pass-through items even though their terrain is
walkable. Classifying them as bad ground also spread a three-cell terrain
setback around them. The remaining cells were seven existing walls, one door,
and five shallow-water cells; those remain protected by their existing rules.

The user explicitly authorized cleaning loose stone chunks for rooms and fields.
`ClearableStoneChunk` now records that distinction in the terrain snapshot.
Prepared-ground predicates admit those cells consistently for room envelopes,
fields, field walls, access, housing-floor counts and planned room partitions.
Native recognition is restricted to single-cell stone-chunk items with no other
non-clearable obstruction. It does not grant terrain affordances or remove
water, edifices, natural rock or excavation limits.

Before construction, the transaction relocates only chunks obstructing the
planned footprint, including the entire reserved field and necessary access.
Each keeps its actual object identity, stack count and rotation. Destinations
are distinct empty dry cells outside that footprint. No chunk is destroyed,
merged, converted to stock or granted as construction resources. Rollback
restores its original position and checks its identity/count alongside the
existing item, plant, terrain and layer invariants.

The original raw capture remains `7B4C440C730E9E76` / `B761C6656E0B2AEC`.
The new explicit clearance metadata changes the live input to
`1ECAE19CE6FCBBAE`, terrain `63244712B46AF52C`, layout `DF0DDA6C2ACDFFC3`.
It accepts within the unchanged 900,000-evaluation cap at 324,461 evaluations,
with six residents and 191 crop cells (185 required). Its native 1.7 spawn
completed. All 730 original chunks retained their IDs and stack counts; only
81 moved. Two forced failures, after floors and after smoothing, each restored
81/81 chunks, 400/400 plants, the item ledger and terrain/layers, then the same
map successfully retried. The isolated game stayed paused at tick 583 and was
stopped; its copied baseline config was restored. Actual GL context was Coast,
so this remains a repaired rejection rather than applied CoveWithIsland coverage.

The room/field eligibility regression failed before the implementation and
passes afterward; it also retains water, edifice and natural-rock exclusions.
Relocation tests verify distinct destinations, no source-cell reuse, stable
ordering and rejection before mutation when insufficient space exists. All
192 layout checks pass, with zero failures and the existing GL peninsula skip.
The actual native captured input replays twice at planner degree four with the
same input/terrain/layout digests and sufficient housing-only spawn floor.

Evidence is under `next-cases/run-2026-10-29a/fixer/`: `live-cove-manual-audit/`,
`live-cove-chunk-clearance/`, `cove-clearable-chunks-live-replay.log`,
`stone-chunks-before.log`, `stone-chunks-after.log`, and
`layout-full-stone-chunks-final.log`. The original Caldera rollback record
(`itemLedgerRestored=True`, 899/899 plants, terrain/layers preserved) is untouched.
The manual editor now uses the verified chunk-aware grid and preserves the
submitted layout. Its remaining flags identify actual water, ruins or access
conflicts; no other placement policy has been changed. Defender movement remains
postponed, and no new changes have been committed.

### Fitted manual Cove layout: public exhaust clearance

Completing the user's fixed eleven-room proposal, including its approved six
street cells and geothermal yard, exposed a thermal prediction rejection at
workshop exhaust `(82,67)`. Captured loose chunks on this public street were
still separators in `LayoutRoomPartition`, although the live transaction already
clears chunks and vegetation throughout the enclosure before auditing rooms.
The thermal model now includes that existing owned enclosure clearance. A red
then green regression also retains untouched outside chunks and permanent
water/rock/edifice exclusions; no live acceptance rule or transaction was changed.

Disposable completion instrumentation reconstructs canonical fixture metadata,
asserts the fixed room bounds/doors/facings, and calls the existing site-interface,
enclosure, finalization, validation and spawn-preflight stages. Offline and native
layout digests match `A523C7229C49D8EF`: eleven rooms, two gates, six residents and
189 unroofed crops. Native housing, cooler/vent and field-post route audits pass.
All 730 original chunk identities/definitions/counts remain, with 69 relocated.
The normal power-finishing stage retains six fuel generators and four batteries
plus geothermal within the same power-room shell.

The full layout suite passes 193 tests with one existing skip. Badlands retains
input `FCEA3634AAD7FDDE`, terrain `79AF29C72969CA3E`, layout `EBE0C927FBF2AAD1`;
Caldera retains layout `7C899E3CEF005B1D` and the separate modeled spawn-gate
negative control (zero eligible under the old restriction, four after the fix).
Recorded rollback evidence is unchanged. Defender movement experiments remain
postponed. Live GL context is Coast, so this is not CoveWithIsland form coverage.
Runtime evidence and the accepted manual checkpoint are under the ignored
`fixer/manual-live-probe/REPORT.md`; diagnostic importer sources are explicitly
compiled for that disposable check and are absent from normal project inputs.
