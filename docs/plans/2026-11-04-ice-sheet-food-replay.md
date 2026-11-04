# Zero-season outdoor fields and candidate 33970

Ordinary settlements now omit their outdoor field when the native crop-specific
growing period is zero. They retain the full roster and original food demand,
prefer funded physically feasible hydroponics, and import the remaining shortfall.
Courtyard crops are also omitted. The live candidate at tile 33970 was accepted
with seven residents, 16 basins under one sun lamp, and no outdoor crops.

This is **not confirmed IceOasis coverage**: both the original and fresh native
Geological Landforms map-generator contexts say `Landforms: None`.

## Cause and rule

The old captured catalog lacked the food nutrition and hydroponic growth facts
needed to enable the already implemented scarce-cropland fallback. Enriching those
facts made the historical terrain pass, but it credited 139 outdoor crop cells.
The old climate input was only annual tile temperature plus/minus 20 degrees,
which cannot establish a native growing season.

`P4AMapPlanner` now captures the profile crop's native growing period using
`GenTemperature.TwelfthsInAverageTemperatureRange` and the crop's minimum/maximum
optimal growth temperatures, matching the native growing-zone UI. Tile 33970
reports zero days for rice. This policy treats a zero-day native growing period
as unsuitable for a reliable settlement field; it does not claim that every
instantaneous temperature is outside all possible plant-growth limits.

`LayoutClimate.OutdoorGrowingDays` is optional, validated in 0..60 and hashed only
when present. Unknown historical inputs keep their old hashes and behavior; the
estimated temperature envelope is never substituted for the missing native fact.
The fixture exporter includes the annotation on new captures. Positive seasons
retain existing field behavior; this change does not introduce seasonal yield
scaling or a complete climate simulation.

For a known zero-day season, joint-site search permits a partial food plan, then
removes all physical field cells, walls, gates, lanes, posts and reservations.
The old field rectangle remains search geometry only, using the existing omitted
main-field mechanism. Hydroponics then plans against the entire original crop
shortfall. The existing material, space, fuel and food ledgers still apply. A
missing legal food supply rejects explicitly. Food accounting and final layout
validation reject outdoor crop credit for a zero-day season.

## Reproduction and regression

Original capture, unchanged:

`.runtime/investigations/geological-landforms/next-cases/run-2026-10-31b/fixtures/13-iceoasis-tile-33970.cs`

- Original input `79885E6F17CCF6B2`, terrain `8EEF9DB44A04E07F`.
- Exact historical replay with `LegacyCapturedStageTables=true` retains
  `no_feasible_field_compound_pair` before layout attempts.
- Seven residents, 216-cell original crop target, 6,875 ordinary import allowance,
  1,225,000 evaluation cap. None was increased.

The initial enrichment experiment adds the complete MealSimple/Pemmican catalog
entries and copies basin fertility/sun-lamp growth radius from the resolved native
`1.7.4995 rev309` capture at `.../fixer/room-lighting/attempt-1/captured-input.cs`.
It uses the current research table but has no seasonal annotation: input
`145FE40904226821`, layout `95296B12AA0648A4`, 139 natural cells and seven basins.
This is historical diagnostic evidence, not the final zero-season policy result.

`IceSheetFoodReplayTests.cs` preserves all 40,000 original terrain-cell records,
including terrain/roof IDs, and asserts the original terrain digest. The compact
`ice-sheet-33970-terrain.rle` uses rows of `hexTraits:terrainId:roofId*count`.
The test catalog combines the existing RuntimeFlat fixture with four fully
captured food/growth definitions. Its unknown-season compatibility case accepts
`50A8F4885EFB1D0F`; its zero-season case first failed because the field remained,
then passed with omitted field, 16 basins, complete food funding and ordinary
offline spawn acceptance (`9BBBA2544D419AD8`). It checks that no courtyard crops or
field/defender reservations remain. Separate fast checks cover unknown versus
known season hashes, positive-season precedence, and forbidden natural-food credit.
These modeled-catalog layouts are distinct from the exact live capture below.

## Native acceptance

Fresh resolved capture: `.../fixer/iceoasis-33970/current-native-fixture.cs`.

- Input `0DA2E16588C2B5C7`, terrain `57148C2E2A8F8967`.
- Layout `3A544557E19B2E71`; native `PROCEDURAL_SPAWN_COMPLETE`, no plan/spawn rejection.
- All 40,000 terrain-cell tuples match the original capture. Only the source
  version label changes its terrain digest.
- Twelve rooms, seven residents, no outdoor field, 16 basins and one sun lamp.
- Actual paused save contains exactly 64 rice plants, all on the 64 basin cells.
- Actual initial food stock is 125 meals. Food ledger funds the full eight-day
  initial horizon once; the remaining production shortfall is imported.
- Hydroponic field equivalent 179.1999969482422; imported nutrition/day
  1.9081483063874423; ordinary imports 6,296/6,875; fuel horizon eight days.
- 129,371 evaluations, five attempts, one site, no budget exhaustion.
- Fresh fixture replay matches the native layout digest and passes spawn preflight,
  housing-floor checks (12 positions for seven residents) and room-lighting checks.

The base was shown paused, saved as `IceSheet-NoOutdoorField-2026-11-04.rws` in its
isolated disposable profile, then that exact process was stopped for final tests.
No elapsed cultivation, defender movement or save/reload acceptance is claimed.
No transaction/rollback code was changed. Original fixtures and recorded rollback
invariants, including Caldera's 899/899 plants and item/terrain/layer restoration,
remain unchanged; this work does not claim a new forced-rollback test.

All replay logs, source SHA-256 provenance, native stock/save checks, the paused
screenshot and final suite results are under `.../fixer/iceoasis-33970/`.

Validation: final full layout suite **207 passed, zero skipped, zero failed**;
exact historical and fresh native replays passed; native build succeeded with
zero errors and four existing warnings. The ordinary test assembly was rebuilt
without a captured fixture after the replays.
