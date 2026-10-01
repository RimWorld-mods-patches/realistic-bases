# P4E facility and defense findings

2026-10-01 · Implementation on `codex/p4e-facilities`; not a closeout.

## P4E.0 — Baseline and definition audit

Starting point: main `f436173`, preserving P4B/P4C/P4D. All inspections use installed Core 1.6.4871 XML and matching external decompiled implementation, not stripped reference method bodies. Shared build/runtime/API skills are read; no game process was running at the initial process check, and Docker Server 29.2.1 is available. No tools installed or normal profiles touched.

### Facility policy and stage matrix

`LayoutDemandModel.IsLiving` covers housing, jail, medical and dining only. Workshop and kitchen are occupied work rooms but miss comfort control. Freezer has separate cooling. New Laboratory must use the same explicit room-use policy; storage, power and magazines remain outside comfort policy. Existing targets remain −10°C freezer and +21°C occupied rooms. Thick-roof cooling relief, actual outdoor exhaust, finite primitive fuel and utility priority must remain intact.

| Final resolved stage | Laboratory bench | Analyzer | Seats / medical shelves |
|---|---|---|---|
| Neolithic | SimpleResearchBench | None | Stool / no legal Core shelf (ComplexFurniture absent); explicit ground fallback |
| Medieval | SimpleResearchBench | None | DiningChair or Stool / ShelfSmall or Shelf |
| IndustrialEarly | SimpleResearchBench | None | Legal resolved seats / shelves |
| IndustrialMid | HiTechResearchBench (MicroelectronicsBasics), simple fallback | None | Legal resolved seats / shelves |
| IndustrialLate / Spacer | HiTechResearchBench, simple fallback | MultiAnalyzer when legal, powered and linked | Legal resolved seats / shelves |

Ordinary eligible settlements get one laboratory bench, not a population-scaled research economy. Compact quest camps retain their existing no-workshop/no-lab contract. Simple bench is Core 3×2, no prerequisite, interaction offset `(0,-1)`, 75 stuff +25 steel. Hi-tech is 5×2, metallic stuff only, 250 W, 150 stuff +100 steel +10 components. Analyzer is 2×2, 200 W, requires MultiAnalyzer research, costs 40 steel/50 plasteel/20 gold/8 components. Bench facility linkage is explicit `CompAffectedByFacilities`; verify actual powered active link, not proximity alone. These choices do not grant autonomous NPC research.

### Seating and medicine

Current workshop fixtures reserve working cells but never furnish seats. Dining creates four chairs all facing north; three are wrong. Core research reserves `ReserveSittableOrSpot` at `InteractionCell`, and research/recipe toils gain chair comfort there. A seat is a permitted passable occupant of the working cell, not a side decoration; it must face the bench and retain circulation. Mortar operator cells are not seated work.

Medical currently has beds/optional monitor/light/climate but no shelves; `LayoutStockCells.ForRooms` prefers shelves only if furnished. Core ShelfSmall is 1×1, three stacks per cell, PassThroughOnly, 10 stuff; Shelf is 2×1, 20 stuff. Both require ComplexFurniture. Fixed filters allow manufactured medicine and shells, excluding chunks/plants/buildings. Actual item filters and stack limits are checked by existing `SpawnStacks`; do not change finite stock totals.

### Mortar contract and hazard

Existing layout/encounter/controller/spawner hold one mortar, operating cell and magazine marker. Saved controller uses `p3Mortar` reference plus stable saved post IDs/flags/assignments and one shared operating-stock ledger. Migration design: retain legacy `p3Mortar` read, introduce saved collection of guns and corresponding mortar post IDs; after loading absent collection map the old reference to the existing mortar post, without spawning guns or replenishing anything. Every job must resolve its gun from its assigned post, and every gun has at most one operator. Initial barrel count sums loaded barrels once over guns, not once per pawn. Quest contracts keep zero extra artillery.

One usable cell plus walls/door is a 3×3 enclosure using ShelfSmall. Source-level feasibility: pass-through shelf supports Touch item access from the doorway; live fetching is still required before accepting the design. Roof only the store. HE/incendiary shells explode when damaged (Core base radius 2.9, expansion 0.4 per stack count); a roofed enclosure is not blastproof. Prefer small divided stacks away from operators, beds, fuel and sole retreats. Shared six-hour shell/barrel ledger must not be copied per gun.

### Defense budget

Current ordinary population distribution remains 4–12 with expected mean seven. Import allowance is the physical profile allowance multiplied by ordinary wealth, separate from resident equipment. Turret construction, initial fuel/reserve and added cover are charged against that same import limit; connected dispatchable headroom and exclusive guard/support staffing are independent constraints. Maximum groups currently min(4, exposed faces). Heavy selection orders uranium then autocannon then mini unconditionally, so autocannons can be shadowed. Core Turret_Autocannon requires HeavyTurrets (IndustrialLate in the current stage model); uranium requires SniperTurret. P4E.4 must compare a named ordinary-only increment and useful gun mix, preserving native quest budgets and roster.

## Evidence ledger

- P4E.0 source audit complete. One-cell store fetching, thermal settling, seated use and multiple-gun behavior are not yet live-verified.
- P4E.2 complete below; P4E.3–P4E.5 pending. Do not infer integrated completion from a facility pass.

## P4E.1 — Laboratories and occupied-work-room climate

Appended Laboratory/Research/ResearchAnalyzer enum values (no shifted persisted values), with ordinary one-bench demand and compact-quest exclusion. Catalog capture now records resolved facility links/range and seating facts. Simple bench remains legal at primitive stages; hi-tech starts at IndustrialMid; eligible analyzers must have an explicit bench link and fit the eight-cell Core center-distance/clear-line rule. Laboratory finishes use the clean-room P4C category. Power/fuel and material summaries include the fixtures. Planner version is `p4e-facilities.1`; existing loaded layouts are not regenerated.

Shared occupied-room policy now includes Kitchen, Workshop and Laboratory. Existing freezer −10°C and occupied +21°C targets, thick-roof relief, outdoor-exhaust logic and legal primitive limits remain. No thermal/production simulator or autonomous research added.

The additional lab exposed packing and utility-frontage omissions. Initial full regression had five failures: a stale one-row bench test, the 12-map clutter corpus (7/12), two optional renewable placement assertions and a single-seed wind-zone assertion. Corrected the bench test to check the complete back row of deep benches; wind-zone validation now finds an actual turbine in a bounded six-seed corpus rather than requiring optional power on one obsolete seed. Added a bounded same-compound room-envelope/connecting-corridor extension and at most two utility-access branches per renewable source. All ordinary field approaches now reserve their connection before packing, including four residents. No field relocation, roster reduction or relaxed path/terrain rules. The exact clutter regression passes **12/12**, with three local-growth cases.

Disposable Core live run: map1, seven IndustrialLate residents, ten rooms, 240 crops, 468 finished floor cells, ten actual outdoor cooler exhausts, 242 conduits, 60 startup generator fuel and 204 finite wood reserve. `PROCEDURAL_SPAWN_COMPLETE` layout `67C5853BED407DC8`; playable arrival and clean startup/logs. Hi-tech bench's working cell is standable; one analyzer is genuinely linked and active, bench powered. Seats/medical shelves are intentionally still absent in this checkpoint (P4E.2).

| Observation | Game tick | Outdoor | Lab room | Workshop room | Operation |
|---|---:|---:|---:|---:|---|
| Initial settled | 3636 | 21.71°C | 21.00°C | 21.03°C | Powered heat/cool, active analyzer |
| HeatWave, started3647 | 17157 | 48.00°C | 21.00°C | 21.00°C | Powered coolers; ~13,500 settling ticks |
| ColdSnap, after ending HeatWave | 44741 | 1.76°C | 20.73°C | 20.78°C | Powered heaters; >20,000 settling ticks |

Debug climate probes use native temporary conditions and log actual map/tick; audits report actual room temperature, outdoor use, work cell, target/power/fuel and active links. The exact disposable process was stopped before rebuilding. Live samples establish this generated thermal arrangement, not seasonal or extreme primitive guarantees. Full integrated regression remains P4E.5.

## P4E.2 — Correct seats and finite medicine on shelves

2026-10-02. Resolved catalog metadata identifies supported seated work tables,
including inherited runtime types, separately from arbitrary interaction cells.
Kitchen, workshop and laboratory seats occupy the correct working cell with the
bench's native rotation. Dining seats face their associated table cell on every
side. Working cells and chairs are unique; the production validator rejects
missing/misoriented/illegal seats. Seats remain passable and material-budgeted;
no chair is placed at a mortar operating cell.

Appended MedicalStorage role (no shifted persisted enum values), preferring legal
ShelfSmall then Shelf. Medical room packing must fit the shelf or grow the room,
not discard medicine. Existing pooled stock placement puts medicine on that
shelf first, retaining actual filters, stuff, per-cell capacity and finite stock
identity. Neolithic Core has no legal shelf and explicitly retains ground
storage; Medieval and later get the resolved shelf. A catalog without ShelfSmall
resolves the legal Shelf fallback. Planner version is `p4e-facilities.2`.

Twelve seeded geometry cases exercise all four workstation rotations, every
dining side, uniqueness, legal medical storage, pooled shelf preference and
door/working-cell clearance. Primitive capability and missing-small-shelf
fallback fixtures pass. Full layout suite passes (440,982 ms), including 12/12
dense flat cases with four local-growth plans, P4D protected-objective cases,
materials, power, crops and full-perimeter regressions. Full planner/garrison
suite and the latest focused P4E suite pass; Release build has zero warnings or
errors. Three obsolete fixed-seed assertions were corrected to request two
gates explicitly, exercise the production joint field-site path, and find a
supported heavy emplacement in a bounded eight-seed corpus. Heavy support,
crop capacity and connectivity assertions remain intact.

The successful same-tile retry described in [P4B findings](p4b-findings.md)
provides live IndustrialMid evidence: four permanent residents, ten rooms,
correctly seated hi-tech research bench, stove, smithy, tailoring, machining and
stonecutting workstations. At tick18426 the powered laboratory was 21.00°C and
workshop 21.38°C with outdoors 27.05°C; all working cells were standable with
seats. No analyzer is legal at this stage; active IndustrialLate linkage remains
the separate P4E.1 observation.

The user clarified that **laboratories are decoration**. No NPC research job,
research progression, comfort-need injection or research validation probe is in
the resulting code. Only facility/seat geometry, legal equipment, power and
linkage are checked. The final isolated probe creates two genuinely new,
relation-free, gear-free pawns (never reuses saved residents/world pawns), issues
native eating and inventory-fetch jobs, then cleans up those temporary pawns.
It observes the actual `JobDriver_Ingest.GainingNutritionNow` chewing toil on a
dining chair next to an Eat surface, not merely an Ingest pawn walking over a
workstation chair.

Live final probe at tick19028: `researchPerformed=False`, `seatedEating=True`,
`medicineFetched=True`, eater at `(82,59)`, medicine Thing79299 with all thirteen
units and `medicineIdentityPreserved=True`. Cleanup returned that **same** Thing
to its original medical shelf. Audits before and after native active save/reload
at tick18426/18427 report one shelf stack, thirteen units, ID79299:13; later
audits retain the same count/identity. The permanent controller reloads four
defenders and four assignments, preserving finite partly consumed mortar stocks
without replenishment. The temporary probe does not become permanent staffing.

All game runs used the isolated five-mod profile, checked exact API actions and
one verified process; it was stopped before every build. Evidence is in ignored
`.runtime/logs/p4e-2-perimeter-medicine-pass-20261002.log` and
`.runtime/logs/p4e-2-east-transfer-gap-20261002.log` (the latter contains the final
chewing/fetch probe and the subsequently corrected cross-front route gap).
`p4e-2-east-transfer-fixed-20261002.log` verifies the corrected live transfer.
Shared skills and normal user saves/profiles were not changed.

P4E.3 magazines/multiple crews, P4E.4 defense allowance/autocannons and P4E.5
integrated terrain/quest/hazard/migration acceptance remain pending.
