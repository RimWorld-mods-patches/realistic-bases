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
