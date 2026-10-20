# Plan: turret batteries per defense crossing, and power for them

2026-10-18 · Status: **T4 cap locked at 1,100%; T4A exact-plan parity open; corrected-validator T4B run complete, review pending.** Builds on `codex/wall-coverage-experiments`
at `62ab734` (cleanup over `876719f`). Implementation is delegated to Luna; Claude reviews.

## Goal

Bases with powered defense get many more turrets. The turrets sit next to the posts that
guard them and are not exposed to enemy fire that nothing in the base can answer.

- **Anchor.** One battery per defended crossing: every compound or field crossing with at
  least one firing nest, including arrival gates with nests. Reserve for up to 16 per base:
  12 compound crossings plus four field crossings.
- **Composition**, using legal turrets only:
  - mini-turrets: 3–6;
  - autocannons: 0–2;
  - slug turrets (`Turret_Sniper`): 0–1, IndustrialLate only.
- **Snipers are not required.** A base with no sniper-rifle resident still gets batteries.

## Decisions (user, 2026-10-18)

1. "Post" means a defense crossing, not a single firing nest.
2. **Budget grows to fit.** Plan the batteries first, then size defense power and turret
   import value to that plan. Keep a hard upper cap by wealth.
3. **No long-range answer (IndustrialMid, no sniper): minimize exposure.** Still place
   3+ mini-turrets per crossing. Among legal positions, choose the ones with the least
   unanswerable exposure, and report the residual exposure.
4. Prefer chemfuel generators to wood-fired ones whenever chemfuel is legal.
5. Allow solar panels inside wind-turbine clear zones.
6. **Reserve for the full counts, then trim power** (T4A). Import reserve covers the full
   per-crossing composition. Power is reserved for it during packing, and unused generators
   are removed after turrets are placed.
7. **Wealth cap approved at 1,100%** of the wealth-scaled base import allowance, capped at
   the stage reserve. The T4 outcome showed no measured base reaching it.
8. **Geothermal is firm power** (T4A). It counts toward critical load (turrets, freezer
   coolers, heaters, vitals monitors) before fuel generators are sized.
9. **Solar and wind count through battery storage** (T4A), superseding the solar/wind
   clause of decision 8. Battery `efficiency` is 0.5 (`Buildings_Power.xml`), so a source
   that directly covers the load for share *p* of the day has firm credit
   `average output / (2 − p)`. Use *p* = 0.5 for solar (night) and 0.5 for wind
   (conservative): about 570 W per panel and 610 W per turbine. Add battery storage for the
   off share: credited W × (1 − p) days, about ½ battery per source, on top of the existing
   6-hour outage bridge. Keep at least one fuel generator whenever critical load is credited
   to solar or wind, for eclipses and long calm spells.
10. **Size the power reserve to a realistic estimate** (T4B), superseding the power part of
    decision 6. The full-count T4A reserve is 14,880 W (about 17 generators). The first T4A
    outcome used at most 1,890 W; after the performance fix, the accepted corpus used at most
    960 W and sometimes selected no battery after final network validation. Carry the approved
    roughly 3 kW proposal into T4B, recheck it against the final measurements, and keep the
    full-count import reserve. Trimming still removes any surplus.
11. **Power headroom stops placement, and the reserve rises** (T5, 2026-10-18). Turret
    placement stops adding turrets when power headroom runs out, keeps what fits and reports
    the shortfall. It never rejects the whole turret plan for power (T5 combined run, seed 4:
    `power_generation_below_load`, zero turrets). The reserve rises from 2,500/3,000 W to
    3,300 W (IndustrialMid) and 4,000 W (IndustrialLate), because the T5 combined run used
    up to 3,260 W.
12. **T5 diagnoses turret-cover failures before choosing placement rules.** 5,689 of 13,007
    rejected attempts were logged as `turret_cover` blocked by "terrain at 0,0", a diagnostic
    gap. Find the real blocking cell and rule first. V1 (minimum ignores the gain gate) is
    dropped: only 2 of 22 never-attempted crossings had any legal candidate. V6 and V7 are
    rejected by the user. V2, V3, V5, the V4 ladder and exempting nest cover (not slots) from
    blast clearance are decided after the diagnosis.
13. **Cover-aware service routing first, no rule change** (T5c, user 2026-10-18). T5b showed
    8,069 of 8,153 `turret_cover` rejections came from the candidate's own service route
    running through its required front or flank cover cell. The planner tried neighbouring
    cells as service cells and routed before reserving cover. Fix: fix the cover cells first,
    then route only to the non-cover side cells (for a mini, the back cell), with cover cells
    blocked. A position whose back side cannot be reached remains a `service_route` failure.
    Re-measure, then decide relaxations (V2, V3, V5, the V4 ladder, nest-cover blast
    exemption) only for what still fails.
14. **T5d combined measurement** (user 2026-10-18). T5c kept 37/157 batteries: cover
    failures moved on to blast clearance (1,954), hard exposure (1,154), service route (938)
    and close staffing (752). Autocannons fell 12 → 5, mostly through unreachable support-mini
    service endpoints. Measure one combined run against T5c with:
    - **Nest-cover blast exemption.** Turret blast clearance ignores nest *cover* cells
      (barricades). Full clearance stays from nest slots, access gates and other defended
      assets.
    - **V5 alternate route doors.** A service route may start from any door of the crossing's
      serial band, or from the adjacent crossing's door on the same side.
    - **Between-nests candidates.** Add candidate positions on the wall stretch between two
      neighbouring nests on the same side, 2–3 cells off the wall so the back cell stays
      free, with a service lane along the wall, spaced by blast clearance, within 14 cells of
      both nests. A turret there counts toward the crossing whose door serves it. Existing
      defender-frontage and nest fire-lane checks still apply.
    The exposure rule (decision 3) and close staffing stay unchanged. T5b and T5c stay
    uncommitted until the user approves.
15. **Conditional T5e: lower the hard exposure requirement** (user 2026-10-18). If T5d doesn't
    raise battery coverage meaningfully, the next step lowers the 100%-answered rule at
    crossings with a sniper post or slug turret. Start at 90%, the pre-T3 sniper-guard
    threshold, and measure against T5d. Claude brings the T5d result and the threshold proposal
    to the user before T5e starts.
    **Confirmed 2026-10-19:** T5d gave 37 → 41/157 batteries, which isn't meaningful. T5e sets the
    requirement to **90%** answered. The same run also counts the between-nests candidates
    generated per base and their rejection codes, because T5d selected none and reported no
    generation count.
    **Kept at 90% (user, 2026-10-19).** T5e stayed at 41/157 batteries; answered share rose
    46.6% → 48.6%.
16. **Turret support is geometric, not garrison-concurrent** (T5f, user 2026-10-19). Through
    T5c–T5e, batteries stayed at 3–5 per base, matching the 3–4 nests that
    `P4BStaffing.Audit` staffs at once. `network_concurrent_close_staffing` requires every
    turret on every side to be defended simultaneously by distinct residents. The user's rule:
    a settlement is like a player base. Population varies, and the defense isn't rebuilt each
    time, so defenders move to the relevant nests and **more nests are better than fewer**.
    Therefore:
    - Remove garrison concurrency as a turret placement or validation requirement:
      `network_concurrent_close_staffing`, and `network_concurrent_sniper_staffing` as a gate.
      Keep the staffing audit as a reported diagnostic.
    - A turret still needs a nest **on its attacked side** close enough to defend it
      (`NearbyPawnSupport` / `CanClose` geometry: within 14 cells, with shots on attacker
      ground near the turret). Credit the **stage's standard rifle range** (for example the
      bolt-action rifle, 36.9, at IndustrialLate), not the best weapon in the roster and not a
      specific assigned resident (user, 2026-10-19).
    - **Runtime slot range follows the weapon.** Ordinary perimeter firing slots currently cap
      engagement at 25 (`P4ProceduralSpawner.cs` around line 269; `GarrisonDoctrine.Engagements`
      uses min(weapon, slot maximum)). Raise every perimeter slot's maximum to 44.9 so the
      defender's weapon decides. Nest cover is already built against attackers out to 45
      (`ThreatRange`). Sniper-guard slots are unchanged.
    - **Corner (angle) nests count for both adjacent sides** when their geometry covers the
      turret's side.
    - A turret with no such nest on its side still fails. Report those sides: they show where
      more nests are needed.
    **Result:** batteries 41 → 54/157, turrets 185 → 250, summed planner time 369 → 306 s.
17. **Rebuild the between-nests candidates** (T5g, user 2026-10-19). The T5d version tried only
    two positions per nest pair, both at the midpoint. It measured "2–3 out" and the 1–2-cell
    service lane from the first crossing's door cell. With layered walls that door sits in the
    inner layer, so the lane falls inside the wall band (20 service endpoints, 0 in the lane).
    The rebuild:
    - tries every cell along the wall stretch between the two neighbouring nests' crossings on
      the same side, 2–4 cells out from the **outer wall face**;
    - keeps only positions within 14 cells of both nests;
    - uses the normal cover-aware back-side routing, with no lane rule;
    - leaves all other checks unchanged.
    Measure once against T5f. If it still adds no batteries, remove the between-nests code.
17a. **Between-nests, third attempt** (T5g2, user 2026-10-19, before T5h). The T5g maps showed
    the "stretch" was a straight line between the two doors that crossed walls, yards and
    rooms: broad-flat 18 of 44 cells were wall. Pairs were also formed by nest side rather than
    by wall face (seed 5 east (132,99)–(102,105)). Restore from `between-nests.patch`, then:
    - walk the **outer wall face**, the attacker-side cells along the outermost wall, around
      the perimeter;
    - pair only crossings that are **consecutive on that walk and share the same face**;
    - place positions 2–4 cells out along the face's outward normal at each walk cell;
    - require **within 14 cells of one nest and within the stage-standard rifle range (36.9) of
      the other nest, with line of fire to ground near the turret**.
    Report the funnel and maps to Claude. Claude checks the maps before anything is concluded.
17a2. **Between-nests, loop spec** (T5g3, user 2026-10-19). T5g2 found 0 pairs. The outer
    walls are stepped, so splitting the perimeter into straight same-direction faces gave 67–76
    runs of median 3–4 cells (longest 25), none holding two crossing doors. Crossings were also
    matched by their attack-side label rather than door position (3–5 per base unmatched, for
    example door 109,28 in a south run labelled east,west). New spec:
    - one continuous loop of attacker-side cells along the outermost wall, following every step
      and corner;
    - pairs are crossings consecutive along the loop, located by **door cell**, regardless of
      face direction;
    - positions 2–4 cells out from the **nearest wall cell** at each loop cell (local outward
      direction);
    - within 14 cells of one nest and within 36.9 of the other with line of fire.
    Run after V1 plus the face report. Report the funnel and maps (maps-t5g3) to Claude, who
    checks them before any conclusion.
17b. **V1 and a face-coverage report, before T5h** (user 2026-10-19, from the seed 1 map):
    - **V1 now.** The 3-mini minimum ignores the "neighbours already cover it" gain gate. On
      seed 1 the field's south crossing `field-sally@146,37` has a nest but failed with
      `no_useful_unsaturated_approach` after one check, because battery `118,36` already
      covers its approach. Extras (4th–6th) keep the gate. Measure against the current code
      right after T5g2. T5i keeps the V4 ladder and the 100%-exposure comparison.
    - **Face-coverage report (diagnosis only).** Turret support is checked per compass side of
      the whole settlement, so a long face can have no nest while its side counts as covered.
      Example: seed 1's field north wall, where the other gate's exterior arrival route runs one
      cell off the wall and blocks a crossing there. Per base, list every exposed outer wall
      face (straight run of outer wall, compound or field) with no nest within about 14 cells,
      with its length and the reason no crossing exists there. Don't change placement.
17c. **Crossings on faces with an arrival route along them** (user 2026-10-19). Where an
    exterior arrival route runs along an outer wall face, as on seed 1's field north wall,
    allow a defense crossing door in that face anyway. Its nest goes off the route, on the far
    side of it or beside it, with the route cells kept clear: no nest slot, cover or turret on
    the route. The door counts toward the crossing caps as usual. The user accepts that such a
    door sits next to the arrival entrance and adds little as an entrance. It exists so the
    face gets a nest and turrets. Implement after the face-coverage report, then measure
    separately: faces without a nest, batteries per defended crossing, turrets.
18. **Then diagnose service-route failures** (T5h). Unreachable back sides are the top
    remaining blocker on seeds 1, 3, 4 and 6 (62–194 rejections). Record, per rejected
    candidate, what cuts the back cell off from every allowed door: nest cover or route, other
    turrets, wall bands, field, terrain or crops. Report and stop for the user.
19. **Re-measure options judged under the staffing ceiling** (T5i, user 2026-10-19). T5d (37 → 41)
    and T5e (41, 90% exposure) were measured while concurrent staffing capped each base at
    about one battery per staffed nest, so their effects could only show up to that cap. Some
    options were also judged under the old geometry or tested in broken form:
    - **V1:** the 3-mini minimum ignores the "neighbours already cover it" gain gate. It was
      dropped after a probe found 2 of 22 never-attempted crossings had a legal position, but
      that probe ran under the old geometry and the staffing cap. Seeds 7 and 8 now fail mainly
      on this reason.
    - **V4, the real spacing ladder:** radius + 2 → radius → mini-to-mini below the radius,
      only for crossings below 3. The T5 combined run used zero spacing instead, so the ladder
      has never been measured.
    - **The 90% exposure rule:** report what 100% would give on the current code, so the
      setting is judged without the staffing cap.
    After T5g (and alongside T5h, since it changes only measurement), run one combined
    measurement of V1 + the V4 ladder against the current code, with the 100% exposure
    comparison as a separate column. Report per base and stop for the user's choice.
20. **Overnight review fixes** (T5j, user 2026-10-19). Keep V1, the V4 ladder and 17c. Then:
    - **Exposure rule decoupled from the garrison.** The 90% answered rule applies only at a
      crossing **with a slug turret**, which is fixed base equipment. It no longer applies where
      the staffing audit happens to assign a resident sniper guard, because that assignment
      shifts as turrets are added. Every other crossing uses least-exposure ranking. Check the
      rule only for the crossing being changed: one crossing's shortfall never rejects a
      proposal elsewhere. Remove the sniper-guard coupling from `crossing_hard_exposure_coverage`.
    - **Service routes as in the game.** Routes may pass over nest barricade and sandbag cells,
      which RimWorld pawns cross (`PassThroughOnly`), and may share cells with other turrets'
      service routes. Walls, turret footprints, turret cover and other impassable cells still
      block.
    - **Remove the between-nests code.** It's kept in `between-nests.patch`.
    Measure the 14 bases against T5i (90%), and render maps for seeds 1, 2 and 5 and broad-flat
    w180. Commit everything since `f762c2f` only after Claude's review (user: commit after the
    fixes).

## Evidence (verified 2026-10-18)

Turret stats are from the runtime catalog fixture:

| Turret | Range | Power | Footprint | Blast radius |
|---|---|---|---|---|
| Mini-turret | 0–28.9 | 80 W | 1×1 | 3.9 |
| Autocannon | 8.9–32.9 | 150 W | 2×2 | 5.9 |
| Slug turret | 11.9–45.9 | 150 W | 2×2 | 5.9 |

An enemy sniper rifle reaches 44.9. A mini-turret costs about 600 import value, including
reserve fuel and cover.

### Why bases get few turrets (`P4BTurretPlanner.cs`)
- `Plan` runs at most `min(4, exposed fronts)` groups. Each group places one gun, and a
  heavy gun adds one supporting mini-turret.
- A candidate needs `Gain ≥ 4`, and gain counts only approach samples that fewer than two
  turrets already cover, so placement stops once samples are covered twice.
- `Plan` returns null without an equipped sniper (`no_equipped_sniper_guard`). `Validate`
  also requires every gun to be guarded by a staffed sniper post.
- The defense power reserve is a flat 960 W (`P4EDefenseReserveWatts`).
- Accepted test bases have 3–4 turrets, with 231–316 approach samples left uncovered.

### Why mini-turrets end up far from posts and exposed
- Heavy-gun candidates are searched 8–20 cells sideways and 1–15 cells forward of a nest
  slot. The supporting mini-turret is searched 5–19 cells sideways.
- The only nearness rule is "within 14 cells of any position on that side".
- `SniperGuardCoverage` tests only the gun's own side's sparse probe samples and accepts
  90% of them. The band 28.9–44.9 cells from a mini-turret, where an enemy sniper can hit
  it and it can't answer, is not modelled.
- Blast clearance keeps turrets at least 5.9 cells (mini) or 7.9 (heavy) from nest slots,
  nest cover, access gates and each other.

### Power
- The game's `Buildings_Power.xml` lists only Electricity as research for both
  `WoodFiredGenerator` and `ChemfuelPoweredGenerator`. Both are 2×2 and 1000 W.
  - Wood: 22 logs/day (≈26 silver), tank 75 (3.4 days).
  - Chemfuel: 4.5/day (≈10 silver), tank 30 (6.7 days), 1 more component, construction
    skill 6.
  - `Chemfuel` has no research requirement.
  - `LayoutEquipment.cs:377–389` picks wood unless local wood can't cover the stock.
- In decompiled 1.6, `CompPowerPlantWind.RecalculateBlockages` treats a wind cell as
  blocked only by a roof or a thing with `blockWind`. `SolarGenerator` doesn't set it.
  - The wind path is 7 wide: 10 rows on the long side, 6 on the short side
    (`ProceduralLayoutPlanner.WindZone`).
  - A 4×4 panel fits twice on the long side and once on the short side.
  - Renewables are placed in the order geothermal, watermill, solar, wind. Panels need
    `YardCellFree`, which excludes wind-zone cells.

## Steps

Each step ends with focused `--only` runs. There is one full suite at the end (step 6).
The GL peninsula LayoutTests failure is known and pre-existing: never stop for it.

### T0 Diagnostics (no behaviour change)
- Turret metrics on the eight total-twelve seeds and the shoreline and broad-rectangle
  fixtures. Per base: turrets by type, per-crossing counts, watts and import value.
- **Dense exposure metric.** For each turret, take the attacker-reachable cells (2-cell
  grid, from `P4BApproachDefensePlan`'s reachable set) within 44.9 cells that have line of
  sight to it. Count how many are answered:
  - by a staffed sniper post (range 44.9) or a slug turret (45.9);
  - for mini-turrets, cells within 28.9 also count as answered by the turret itself.
- Record the baseline in this plan.

T0 fixture mapping: **broad** is the generated broad flat settlement from
`P4BTurretNetwork`; **shoreline** is the generated `lakeshore-joint` V2 matrix case.
The synthetic `DefenseRectangle` is not used because it has no field wall, power plan or
turrets.

#### T0 baseline results

The eight `total12-seed1..8` layouts all produced zero turrets: each decision was
`unplaced: no_equipped_sniper_guard`. They therefore had 0 W, 0 import, zero guns at every
defended crossing and no turret exposure cells to answer. Whole planner times were
22.080, 25.414, 26.415, 26.540, 26.971, 29.072, 29.881 and 30.996 seconds respectively.

The `shoreline-lakeshore-seed1` matrix case also produced zero turrets for
`no_equipped_sniper_guard`: 0 W, 0 import, zero guns at each of its eight defended
crossings, no turret exposure cells, and 5.699 seconds total planner time.

The broad flat `P4BTurretNetwork` fixture selected seed 3 and placed three mini-turrets at
two compound crossings. Crossing `compound-sally@74,57` has two minis, 160 W, import value
1,216, with 2,482 exposed samples: 1,757 answered and 725 unanswered. Crossing
`compound-sally@79,55` has one mini, 80 W, import value 608, with 1,234 exposed samples:
799 answered and 435 unanswered. Other defended crossings had no turrets. The base total
is three minis, 240 W, import value 1,824, and 8.531 seconds planner time.

The dense exposure diagnostic samples reachable cells on a deterministic 2-cell grid,
within 44.9 cells of each turret origin and with line of sight. Answered cells are covered
by a staffed sniper post or slug turret, plus the mini-turret's own shots within 28.9.
Focused diagnostic runs passed: total-twelve seeds 8/8, broad fixture 1/1, and shoreline
matrix 1/1. Logs are in `.runtime/investigations/turret-batteries/` as `t0-total12.log`,
`t0-broad.log` and `t0-shoreline.log`.

### T1 Chemfuel preference — complete

`LayoutEquipment` now selects `ChemfuelPoweredGenerator` whenever it is in the legal
dispatchable set; wood remains the fallback when chemfuel is unavailable. Fuel selection,
warehouse reservation, material ledger and import accounting still use the selected
generator's catalog facts.

On the same broad-flat seed 3, fuel changed from `WoodLog:141` to `Chemfuel:44`; imported
generator fuel changed from 0 to 44; base import value rose from 3,190 to 3,292 (+102).
The dispatchable warehouse reservation changed from 198 wood units in 3 cells to 41 chemfuel
units in 1 cell. Early-tech cases still use wood because chemfuel is unavailable. The eight
total-twelve default-stage cases also retain wood and stay within their existing budgets.

Focused `power: chemfuel` and early-wood-fallback tests passed, and the total-twelve subset
passed 8/8. The broad `P4BTurretNetwork` fixture now fails its expectation of at least three
turrets: the third group is rejected by the current import limit (`import_budget`). This is
pending T4's budget-sizing step; no budget cap or test expectation was changed here. Logs are
`.runtime/investigations/turret-batteries/t1-generator-before.log`,
`t1-chemfuel-focused.log`, `t1-early-fallback-focused.log`,
`t1-power-before-total12.log`, `t1-power-before-broad.log`,
`t1-total12-focused.log` and `t1-broad-seeds-after.log`.

### T2 Solar under wind
- Place wind turbines before solar.
- Let solar panels occupy unroofed cells in a wind clear zone. A panel's footprint must
  stay out of the turbine's own footprint and out of every other blocking structure.
- Score a turbine candidate by the solar spots its zone opens up.
- Keep `MaxUnitsPerSource`.
- Update `PlaceRectYard` occupancy so the validator accepts panels inside zones.

#### T2 results

Renewable order is geothermal, watermill, wind, then solar. `PlaceRectYard` allows solar
footprint cells in reserved wind zones while keeping other occupied cells blocked. Wind
candidate ranking first counts legal, street-accessible solar footprints using at least one
newly cleared zone cell, excluding the turbine footprint; existing deterministic distance and
seeded tie-breaks order candidates with equal counts. The validator permits solar cells in a
wind zone and still rejects all other yard footprints or blocking structures there. The
four-unit cap per source is unchanged.

Focused seed 17 places one 7x2 wind turbine and one 4x4 solar panel. Twelve of the panel's
16 cells lie inside the wind zone; none overlaps the turbine footprint, and final validation
passes. The layout has 1,770 W renewable output, one chemfuel generator, 44 fuel units and
3,292 import value; planner time was 217 ms. Focused solar-in-wind, open-sky power and wind
zone tests passed 1/1 each. Log: `.runtime/investigations/turret-batteries/t2-solar-wind-focused.log`.

### T2 Solar under wind
- Place wind turbines before solar.
- Let solar panels occupy unroofed cells in a wind clear zone. A panel's footprint must
  stay out of the turbine's own footprint and out of every other blocking structure.
- Score a turbine candidate by the solar spots its zone opens up.
- Keep `MaxUnitsPerSource`.
- Update `PlaceRectYard` occupancy so the validator accepts panels inside zones.

### T3 Battery placement and the exposure rule
- Replace the `min(4, fronts)` group loop and the saturation stop with batteries per
  defended crossing, in deterministic order (most exposed front first).
- Search each turret type from `ExplosionRadius + 2` (5.9 for minis, 7.9 for heavy guns)
  out to the 14-cell `NearbyPawnSupport` limit from that crossing's nest slots, within the
  arc its posts watch. Keep blast clearance from every defended asset. Service routes start
  at that crossing's door. The battery's own mini-turrets cover autocannon blind zones.
- Keep every existing legality rule: footprint, cover, arrival routes, nest routes and
  cover, fire-lane obstruction, blast clearance, service route, power access.
- Composition per crossing:
  - mini-turrets: 3, rising to 6 with open ground and uncovered approach on that front;
  - autocannons: 0–2 where ground lies in their 8.9–32.9 band;
  - slug turret: 1 when legal and no sniper post guards that crossing, otherwise 0–1.
- **Exposure rule**, using the T0 dense metric:
  - with a sniper post or slug turret at the crossing, 100% of exposure cells must be
    answered (hard rule);
  - otherwise, rank legal positions by fewest unanswered cells and report the residual
    per crossing.
- Remove the `no_equipped_sniper_guard` abort. Apply the sniper-guard staffing check only
  where a sniper exists. Keep the close-support check (`CanClose`, `NearbyPawnSupport`).
- Cache sight-line fields per cell. Keep the evaluation bounded and report planner time.

#### T3 clarified rules (Claude, user-confirmed)
- The 3–6 mini-turret count is the crossing's total and includes minis assigned to heavy-gun
  blind zones. Each heavy must have at least one battery mini that passes `CloseSupport`.
- Start with three minis. Add a fourth through sixth only while the best legal next mini
  gains at least four approach samples on that crossing's front, using the existing gain
  definition. Open ground is represented by those samples; it does not add a separate
  threshold.
- A selected slug is that crossing's long-range answerer and triggers the hard 100%
  exposure rule without sniper staffing. It still needs close pawn support. Cells closer
  than its 11.9 minimum range must be answered by minis assigned to that battery. Answerers
  may be on another crossing when range and line of sight allow; each crossing keeps its
  own exposure check.

#### T3 results (complete)

`Plan` now orders defended gates by exposed sample count and secures an all-or-nothing
three-mini baseline per selected gate before considering a heavy gun. Candidate offsets are
bounded by each turret type's explosion-clearance radius through the 14-cell
`NearbyPawnSupport` limit, measured from the crossing's nest slots; blast clearance against
all defended assets remains unchanged. It adds minis only when the best legal candidate
retains the existing `Gain >= 4` threshold, with a six-mini cap. The dense attacker-grid
unanswered-exposure score is cached per candidate origin and ranks legal positions before
approach gain. A selected heavy retains its `CloseSupport` mini and both count inside the
crossing battery. Final validation checks dense exposure network-wide; an actually staffed
sniper answerer or selected slug invokes 100% coverage. For cells in a slug's minimum range,
only minis from that same gate's battery can answer.

Focused `P4B turret network` passed 1/1. On broad-flat seed 1, the current import allowance
fits three minis at one compound crossing: 240 W, import 1,824, and 1,334 of 2,986 dense
exposure cells answered (1,652 unanswered); planner time was 14.801 s. A richer 20,000
import-limit replan fits six minis at one crossing (480 W, import 3,648). The fixture has no
staffed sniper post, so the no-answerer exposure ranking applies. Other crossings were left
without partial batteries when three minis could not fit.

The high-industrial focused test passed 1/1 after finding a network-root-independent
heavy group on seed 7: one autocannon with its CloseSupport mini, four minis total, 470 W,
import 4,369, and close staffing 5/5. The test now inspects all crossing groups, because the
network root is normally a mini. The selected slug branch remains subject to the hard dense
exposure rule; when it cannot fit, the mini battery remains and is ranked by unanswered
exposure.

The per-seed heavy-only audit before the placement fix had no `import_budget` or
`critical_power_headroom` rejections. Counts were: `blast_clearance`, `blind_zone_support`,
`footprint`, `network_network_concurrent_close_staffing`, `network_turret_obstructs_defender_frontage`,
`service_route`, `turret_cover`.

| Seed | Blast | Blind support | Footprint | Close staffing | Frontage | Service route | Turret cover |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 315 | 60 | 12 | 4 | 4 | 34 | 204 |
| 2 | 370 | 60 | 14 | 8 | 0 | 40 | 228 |
| 3 | 282 | 40 | 30 | 4 | 0 | 28 | 132 |
| 4 | 254 | 20 | 7 | 0 | 0 | 14 | 60 |
| 5 | 364 | 68 | 16 | 8 | 0 | 40 | 240 |
| 6 | 208 | 8 | 24 | 0 | 0 | 4 | 24 |
| 7 | 212 | 32 | 8 | 0 | 0 | 16 | 96 |
| 8 | 306 | 28 | 30 | 0 | 0 | 16 | 96 |

That audit isolated placement as the cause. The failed fixed ring was replaced with the per-type
band from explosion clearance through 14 cells from the crossing's nest slots; blast clearance
from every defended asset is unchanged. The battery's three minis are placed first, then an
optional heavy is fitted with its support mini counted in the same 3–6 total. The unchanged
high-industrial heavy-support check and the network test both pass 1/1. T3/T4 order was not
reversed: T3 was completed before beginning T4. Logs: `t3-network-per-type-band.log`,
`t3-heavy-per-type-band-network-check.log`, `t3-heavy-first-recheck.log`,
`t3-heavy-rejection-audit.log` and `t3-heavy-ring-recheck.log`.

### T4 — provisional defense reserve proposal (awaiting approval)

The reserve estimate uses all 12 possible crossings, three minis per crossing, and one
stage-legal heavy per crossing, matching the current T3 planner's cap. A heavy's support mini
is already inside the three-mini baseline. That gives **4,680 W** at IndustrialMid and
IndustrialLate: 12 × (3 × 80 W + 150 W). The import reserve is **44,700** at IndustrialMid
(12 × (3 × 608 + 1,901)) and **55,872** at IndustrialLate (12 × (3 × 608 + 2,832)).

The wealth ceiling is marked provisional in code: turret reserve up to **1,100% of the
wealth-scaled base import allowance**, additionally limited by the stage reserve above. The
existing 125% base defense allowance remains for ordinary materials; material planning uses
`BaseImportValueLimit` and cannot consume the turret reserve. For the Industrial Temperate
profile (base import 5,500 at wealth 100), the proposed late-stage totals are:

| Wealth | Scaled base | Material cap (125%) | Turret reserve | Total import cap |
|---:|---:|---:|---:|---:|
| 60 | 3,300 | 4,125 | 36,300 (wealth-limited) | 40,425 |
| 100 | 5,500 | 6,875 | 55,872 | 62,747 |
| 180 | 9,900 | 12,375 | 55,872 | 68,247 |

The focused population-budget test passed 1/1. The end-to-end population-layout test passed
1/1 across Industrial Temperate and Tribal Temperate wealth 60/100/180. Industrial cases
retained the full 4,680 W reserve: wealth 60 and 100 planned five generators (four beyond the
non-reserve need) and two batteries; wealth 180 planned six generators (five beyond the
non-reserve need) and two batteries. Tribal cases reserved 0 W. The reserve is available to
later turret placement; unused import allowance remains unspent. Logs:
`t4-population-reserve-focused.log` and `t4-population-layouts-focused.log`.

The **1,100% reserve ceiling is provisional and is not locked**. Review the per-wealth totals
above before setting a final cap. Work stops here for approval; T5/T6 have not started.

### T4 Budget sized up front
- Before generators are placed, estimate the batteries from population, wealth and stage.
  Assume the base gets the full crossing count and 3 mini-turrets per crossing, plus
  composition by stage. Reserve watts and import value for them, replacing the flat 960 W.
- Hard upper cap by wealth. Propose the numbers from the T0 and T3 measurements and get
  the user's approval before locking them.
- Unused reserve stays unspent. Generators may be idle.

### T4A Full-count reserve and generator trimming
Starts after the T4 outcome run is reviewed and the wealth cap is decided (both done).
- **Firm renewables (decision 8).** `LayoutPowerModel.Generators` and the turret planner's
  `AvailableWatts` count geothermal output (capacity factor 1.0) toward critical load and
  the defense reserve before sizing fuel generators. Watermill uses the same rule once it is
  placed. Measure on a steam-geyser fixture: fuel generators and fuel stock before and after.
- **Solar and wind through storage (decision 9).** Credit each panel and turbine at
  `average output / (2 − p)` toward critical load and the defense reserve. Size the extra
  batteries for the off share, keep the 6-hour bridge, and keep at least one fuel generator
  when solar or wind carry critical load. Batteries must still fit the power room. Measure on
  a wind-plus-solar fixture: fuel generators, batteries and fuel stock before and after.
- **Fill in rounds, first.** The T4 outcome had 2–4 crossings with 4–6 mini-turrets and 8–10
  crossings without a battery. Give every crossing its 3-mini minimum before any heavy
  gun, then heavies, then the 4th–6th minis, still in deterministic exposure order.
- Count every possible defended crossing: up to 12 compound crossings plus up to 4 field
  crossings (one per side), so 16, not 12. Four T4-outcome seeds had 13.
- Reserve import value for the full composition at every possible crossing: 6 mini-turrets,
  plus the stage-legal heavy guns (0–2 autocannons, 0–1 slug turret). This replaces the
  "3 minis + 1 heavy" sizing. The import reserve is a ceiling, so unused value stays unspent.
- Lift T3's one-heavy-per-crossing limit to that composition.
- Reserve power for the same full composition during packing, so space for the generators
  exists.
- After turret placement, remove the reserve generators the placed network doesn't need.
  Trim fuel stock, warehouse cells, batteries and import to match. Freed slots inside the
  power room stay as empty floor: the room shell, walls and roof are unchanged, and its
  vent/heat handling stays valid for the reduced count.
  The final power plan must pass the validator, including power-terminal connectivity.
- Report per base: generators reserved vs kept, watts and import reserved vs spent, turrets
  by type, and minis and heavies per crossing.

#### T4A results (complete)

The full IndustrialLate reserve covers 16 crossings at 14,880 W and 166,240 import before the
wealth ceiling. Actual turret-import ceilings are 36,300/60,500/108,900 at wealth 60/100/180.
The final 14-case corpus passed validation. The total-12 cases kept 2–3 of 17 reserved
generators and 4 of 10 batteries; broad-flat kept 1 of 16 generators and 4 of 10 batteries at
wealth 60/100, and 3 of 17 generators and 4 of 10 batteries at wealth 180. Across the corpus,
turrets used at most 960 W. Exact per-base counts, exposure and crossing placements are in
`.runtime/investigations/turret-batteries/REPORT.md` under T4A.

The global minimum-first phase remains in `P4BTurretPlanner.Plan`: every crossing gets its
three-mini attempt before heavies and extras. A full network audit runs after filling and rolls
back the last placement until staffing, exposure, power and approach checks pass; if a battery
falls below three minis, the planner removes that whole battery. In this corpus the final
audit retained 0–4 three-mini batteries per base and no heavy guns or extra minis. Unplaced
batteries were limited mainly by blast clearance and service-route access. The focused
high-industrial support test still selects an autocannon with its CloseSupport mini and
passes 5/5 close staffing.

The initial wealth-60 broad-flat case hit `search_budget_exhausted` at 150.3 s because trimming
removed wall vents but left their sealed exhaust pockets. Keeping the packed vents and exhaust
paths fixed `pocket_*_unused`; the final wealth-60 case passes without raising the search
budget. Generator and battery fixtures, fuel, storage and import still trim; unused power-room
floor remains walkable inside the unchanged shell.

Decision 8's steam fixture supplied 3,600 W firm with zero fuel generators and zero fuel.
Decision 9's wind-plus-solar fixture averaged 1,770 W and supplied 1,179 W firm, with one
additional off-share battery, one retained fuel generator and 44 fuel units. Focused budget
and renewable tests passed. Logs are listed in the T4A investigation report.

### T4A-perf Planner-time regression and result parity (parity open)

The 262/146/144 s reports came from the four-way outcome sweep. Serial measurements separated
contention from the remaining regression. Candidate placement spent only 16 ms routing in
seed 1, but repeated per-service-cell BFS, crossfire scans and full network checks multiplied
work over hundreds of proposals. The saved perf path checked cover before route search and
used candidate-local validation, then ran one final network audit and removed invalid placements.
The final-removal diagnostics cite concurrent close staffing and hard exposure coverage; removing
the candidate-local shortcut restores these network checks during placement. Cover generation is
route-aware because the service route affects legal cover cells. A candidate-only local geometry
check with full network checks remains in place to avoid rechecking old groups.

| Base | T4 baseline | Serial before fix | Final serial | Ratio |
|---|---:|---:|---:|---:|
| Total-12 seed 1, wealth 180 | 13.309 s | 26.9 s | 17.475 s | 1.31× |
| Total-12 seed 7, wealth 180 | 25.229 s | 18.2 s | 16.035 s | 0.64× |
| Broad-flat, wealth 100 | 19.028 s | 11.0 s | 9.524 s | 0.50× |

The 14-case T4A fixed run passed, but exact plans still differ from T4A-pre: total-twelve seeds
1 and 7 have different layout/turret outcomes, and seed 8 has a different layout digest despite
matching logged turret positions. Total-twelve totals are 143 turrets/6 autocannons/32 batteries
pre-fix and 149/8/32 with the current validator. Broad-flat wealth 60 now accepts, where the
saved pre-run exhausted its search budget. The latest planner time is 23–34 s on the perf probes,
so the prior 1.5× target is not established. T4A exact-plan parity remains open; per-base digests
are in `REPORT.md`.

T0–T4A and the original perf path remain in `d7c78d0`; no new commit or push was made. The
original T4B report used that same perf path and is superseded by the corrected-validator rerun.

### T4B Realistic power reserve (decision 10)

Power reserve is now 2,500 W at IndustrialMid and 3,000 W at IndustrialLate, based on the
measured three-mini batteries, several heavy-gun slots and a safety margin. The full-count
turret import reserve and post-placement trimming remain unchanged. The initial 14-case T4B
output was generated on the broken deferred-check path; the corrected-validator rerun passed all
14 cases and is the current T4B measurement. See the side-by-side per-base comparison in
`REPORT.md`.

The total-12 Late fixtures now pack five generators and five batteries, keeping two or three
generators and four batteries. Broad-flat wealth 60/100 packs four generators and five
batteries, keeping one generator and three batteries; wealth 180 packs five and five, keeping
three generators and four batteries. Turret and crossing outcomes sometimes change because
the smaller reserved power room changes the packed room geometry. The T4B investigation report
has the per-base T4A-to-T4B turrets/crossings comparison, reserved and kept generator/battery
counts, and power-room interior dimensions before and after trim. Room shells remain unchanged
during trimming.

T4B results are uncommitted for review. T5 has not started.

### T5 Composition limits and spacing

**T5 status (2026-10-18):**
1. **T5a committed as `f762c2f`.** Placement stops at headroom (decision 11); reserves are
   3,300/4,000 W. The current-rule 14-base sweep and focused 320 W cutoff fixture passed.
2. **T5b diagnostics complete; placement-rule choice pending.** Every `turret_cover` rejection
   now records the candidate origin, actual failing cell and condition. The 14-base audit and
   seed 1/5 blocker maps are in `.runtime/investigations/turret-batteries/REPORT.md` and
   `maps-t5b/`. No placement rule was changed; wait for the user's choice.
3. **T5c complete; placement-rule choice pending.** Cover-first service routing was measured
   against T5a on all 14 bases and seeds 1/5. Results and remaining blockers are in
   `.runtime/investigations/turret-batteries/REPORT.md`. No cover, spacing or route-origin rule
   was relaxed; T5b/T5c changes remain uncommitted until the user chooses any further fix.
4. **T5d complete; review pending.** The combined nest-cover blast exemption, V5 alternate
    route doors and between-nests candidate band were measured against T5c on all 14 bases.
    Results and seed 1/5 maps are in `.runtime/investigations/turret-batteries/REPORT.md`.
    No commit was made; await review before further work.
5. **T5e complete; review pending.** The 90% hard-exposure threshold for crossings with a
    sniper post or slug turret and the between-nests generation funnel were measured against
    T5d on all 14 bases. Results are in `.runtime/investigations/turret-batteries/REPORT.md`.
    Battery coverage stayed 41/157; the lane filter removed all 20 between-nests service
    endpoints recorded. No commit was made; await review.
6. **T5f complete; review pending.** Concurrency no longer gates turret placement. The
    standard-rifle side-support rule and 44.9 runtime slot cap were measured against T5e on all
    14 bases, with seed 1/5 maps. Batteries rose 41 → 54/157; staffed close coverage remains
    diagnostic only. Results are in `.runtime/investigations/turret-batteries/REPORT.md`.
    No commit was made; await review.
- **Failure maps first.** Render two bases (total-twelve seed 1 and broad-flat wealth 180)
  showing each defended crossing, its candidate band, placed turrets, and the cells behind
  the top rejection codes (turret cover, service route, blast clearance). Then decide the fix
  from the maps. Crossings that reach only 1–2 minis are currently dropped.
- **Spacing fallback (user, 2026-10-18).** Use it only for a crossing that cannot reach its
  3-mini minimum. In vanilla, a turret explosion does full Bomb damage (×2 to turrets, 100)
  inside its radius and none beyond. Turrets have a 50% chance not to explode when destroyed.
  Try in order:
  1. normal turret-to-turret spacing, radius + 2;
  2. radius only (mini 3.9, heavy 5.9);
  3. mini-to-mini closer than the radius, accepting the chain risk.
  Spacing between heavy guns and minis stays at least the heavy radius. Clearance from nest
  slots, nest cover and access gates never relaxes. Report per base how many turrets used
  each level.
- Measure how often crossings reach 3 and 6 mini-turrets, given the 5.9 blast clearance
  and neighbouring batteries.
- Report crowded sides (for example seed 7 west, which has 5 crossings) rather than
  relaxing clearance silently.

### T6 Closeout
- Run the full LayoutTests, full PlannerTests and the Release build.
- Render before/after maps on the same frozen base and two seeds.
- Record per-base turrets, exposure answered/unanswered, watts, import value and planner
  time.
- Pure planner evidence does not prove live firing, refuelling or repair load,
  save compatibility, or multiplayer. The user owns the live and two-client checks.

## Open questions

- Whether mini-turret-to-mini-turret blast clearance may be relaxed if T5 shows crowding.
- The live refuel and repair load of 40+ turrets on a 4–12-resident garrison.
- The layout behavior changes do not alter saved data or existing defs. Under the
  pre-1.0 version policy, this is a PATCH bump from 0.1.1 to 0.1.2.
