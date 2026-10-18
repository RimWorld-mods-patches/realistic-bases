# Perimeter coverage and field join experiments

Branch: `codex/wall-coverage-experiments`, baseline `ea6cb53` (includes `d53347f`).
Scope: the investigation in `.runtime/investigations/wall-coverage/PROMPT.md`.
No commits, multiplayer protocol changes, tooling installation or normal-profile use.

## Superseding scope — 2026-10-17

The user selected a hard maximum of twelve logical compound crossings total,
allocated by reachable terrain and marginal main-wall coverage. Existing arrival
gates count toward twelve. Three crossings on one compass side is a soft
concentration preference only; an exposed useful front may receive more when
other fronts are protected or irrelevant. Do not force the full budget. The
field enclosure remains capped at one logical crossing per compass side,
including its required inner connection, outer entrance, and any shared
field-to-compound interface. A serial band of doors through two or three
adjacent wall layers counts once per enclosure; physical cells and construction
cost are counted once.

Rank legal gate-plus-post candidates across all exposed fronts by newly
coverable compound wall-adjacent targets under final occupancy, then longest
uncovered main-wall gap reduction. A soft preference for sides below three
crossings breaks ties before capacity and approach samples. A new compound
gate must add compound coverage; field positions count only if they can fire on
those same compound targets. Field-only coverage never substitutes for the
main-wall objective. No all-covered requirement or fixed percentage rejection
rule is imposed.

Candidate gates must produce useful compound-facing firing positions and
satisfy solid-flank separation, serial-layer continuity, public interior access,
short legal exterior routes, cover, retreat, terrain, roof, room, objective,
material and power constraints. Existing corridor ends remain candidates;
bounded public connectors through clear ground are allowed when they preserve
private rooms and fixtures. Preserve deterministic ordering, count arrivals and
shared interfaces correctly, and keep ordinary room doors outside the logical
gate budget.

The first prototype is `industrial-3layers-joint`, seed one, with a frozen
underlying room/field/site geometry where possible. Compare compound and field
wall edges separately, show gate orientation and useful positions, and track
logical entrances, physical doors, acceptance, work, material costs and useful
slots. Expand to seeds and obstacle tiles only after the compound map visibly
improves. No percentage target overrides real terrain or capacity constraints.

## Design

Separate the compound/field join from defense access. Use exposed boundary segments
rather than one compass-side percentage to describe gaps. Retain raw geometric
coverage as diagnostics, not a universal rejection rule. A possible post is not a
staffed gun: report capacity and unavailable fronts separately. Adjacent walkable
ground measures melee exposure; across-water ranged exposure remains in the
existing approach model.

Reserve a few public defense exits at useful corridor ends before packing, then
fit posts after geometry. Permit serial door crossings through adjacent wall
layers with solid flanks, public inside access, useful exterior positions and
bounded logical entrance count. Preserve room/field capacity, actual layer count,
material/power limits, deterministic ordering, paths and retreat.

Prefer a local shared field boundary or a small enclosed connector. Do not fill
the whole compound depth across the field span, pave over landforms, or close
arbitrary narrow strips merely to improve a metric. Score actual feasible exit
and post space rather than imposing a blanket map-edge distance rule. Any change
to full-pack-per-side acceptance must be explicit and independently justified;
do not relax it just to pass tests.

## Experiments

1. **E0 baseline/diagnostics:** add test-side wall-to-exterior-edge measurements,
   compound versus field potential coverage, longest straight uncovered segment,
   post/slot/entrance counts and existing area/food-walk/work metrics. Export the
   unchanged baseline. No new production rejection rule.
2. **E1 field join:** evaluate a bounded local join independently, compare the
   baseline corpus and saved failing fixtures, and inspect same-case maps. Keep
   broad field-span inflation and disconnected joins out of the retained result.
3. **E2 defense access:** evaluate a small number of layered crossings from
   existing public corridor connections. Every crossing must pass serial layer,
   flank, path, material and final occupancy checks. Count entrances separately
   from their physical doors. Compare compound coverage versus added holes.
4. **E3 bounded selection:** if E1/E2 expose candidate-choice defects, test local
   post-space/compound-segment priorities without unbounded repairs. Preserve
   usable existing positions and actual roster requirements. Record losing
   variants rather than combining regressions into a claimed fix.
5. **Closeout:** render before/after maps, run focused regressions while iterating,
   then one full relevant suite pass and Release build for the retained variant.
   Record acceptance, evaluation counts, area, food walk, geometric coverage,
   longest gap, staffing capacity, logical gates, failures and limitations.

Use identical input/seed cases and metric definitions in comparisons; record when
site choice changes, so a different packing is not attributed solely to a local
join. Record the known pre-existing GL peninsula failure separately. All exports,
logs and experiment snapshots stay under ignored `.runtime/`. Pure planner
evidence is not live firing, pathing, save compatibility or multiplayer execution.

## Results

### Analysis checkpoint — 2026-10-16

Implementation and test execution were delegated to Luna 6 High at the user's
request; root reviewed the design, code and measurements. The E3b matrix, E4
probe, targeted regressions, full suites and Release build have now run. E3a and
E3b remain as focused planner changes; E4 was rejected. No commit or merge has
been made. Unrelated user changes to `.rimworld-dev.json` and `.rimworld-dev.md`
were preserved.

| Variant | Evidence | Decision |
|---|---|---|
| E0: unchanged production planner plus diagnostics | `e0.log`: 120/120 accepted, 316 seconds | Baseline acceptance. Initial coverage used stale pre-defense reachability; do not use its coverage numbers for final comparisons. |
| E1: shared field wall with at most six cells of local fill per column | `e1.log` / `e1-corrected.log`: 20/20 seed-one cases; focused cooler regression passed | Rejected as a complete join solution. Three-layer map produces a T-shaped corridor with exposed flanks, relocating the neck. Site/packing changes confound comparisons. The inherited patch also charges the shared inner gate twice; it is not retained in current source. |
| E2: existing desired gate count raised from two to four | `e2-gates4.log`: 20/20; metric-v2 three-layer control in `e2-metric-v2.log` | Rejected as a blanket policy. More openings and search work did not reliably improve compound coverage. |
| E3a: let posts walk through their own ordinary arrival gate's two-cell crossing | `e3a-only-default-v2.log`: 120/120 accepted; focused useful-nest and both blocked-crossing checks passed | Retained. Standing slots, cover and wall returns remain off arrival routes. Does not solve the compound/front geometry alone. |
| E3b: opposite-flank posts reuse one sally door and the same two entry cells | `e3b-default-v2.log`: 120/120 accepted, 290 seconds; fixed compound retains 36 slots with eight rather than nine sally ports; final defense validation and both blocked-prefix checks passed | Retained as a bounded entrance-efficiency improvement. It slightly trades coverage and three slots on three-layer seed one for three fewer entrances there. Shared cover is unsupported and remains rejected. |
| E4: prefer a distinct outward direction for the secondary existing gate | `e4-direction-seed1.log`: accepted, but seed one loses seven slots and the longest gap grows from 13 to 25 while selected site and packing change | Rejected. The result is worse and confounded by changed site choice. |

### Measurement interpretation

Metric v2 floods attacker ground against final occupied geometry, including
added walls and turret footprints, and prevents diagonal corner cutting. Earlier
`e0.log` uses stale reachability; the first `*-corrected` runs still allow diagonal
corner cutting. Keep these historical logs, but do not mix their coverage values
with metric-v2 comparisons. `e3a-only-default-v2.log` and
`e3b-default-v2.log` use the same v2 metric.

Coverage means potential line of fire from any candidate post slot within the
diagnostic 25-cell range. It is not simultaneous staffing, turret firing credit,
response-time proof or live combat. The longest gap is a straight uncovered run,
not a general connected-region measure. Existing across-water approach checks
remain separate. `foodWalk` sums distances for rooms and post targets, so changing
post count changes the total; do not interpret it as a per-person walking cost.

Matched E3a versus E3b comparison across the 84 cases with reported exposure:

| Metric | E3a | E3b |
|---|---:|---:|
| Logical entrances | 932 | 916 |
| Physical gate doors | 968 | 952 |
| Candidate standing slots | 2,165 | 2,191 |
| Compound uncovered / exposed edges | 6,387 / 13,112 | 6,381 / 13,107 |
| Field uncovered / exposed edges | 1,939 / 5,896 | 1,946 / 5,909 |

Twelve cases have fewer entrances, 72 are unchanged, and none have more. Slots
increase in 13 cases, remain unchanged in 70 and decrease in one. Specifically,
three-layer seed one keeps the same room bounds, field, selected gates, attempts,
evaluations and two site candidates; E3b loses three slots (41 to 38), saves three
entrances (17 to 14), changes compound uncovered edges from 170/230 to 171/230,
and changes field uncovered edges from 25/78 to 28/81. Three-layer seed six
keeps its base geometry and work counts, gains one slot, saves one entrance, and
increases its longest uncovered straight run from 15 to 16. Across measured
cases, E3b improves entrance efficiency with a small, nonuniform coverage trade.
The other 36 matrix cases have no measured exposure in this diagnostic output;
their zeros are not proof of complete defense.

On the frozen 12-resident rectangle, E3a-only produced nine sally ports, nine
posts and 36 slots, with no opposite-flank pair sharing an entrance. E3b produced
eight sally ports, nine posts and the same 36 slots. A pair of opposite-flank
nests shares the same two route-prefix cells and one physical door; their cover
cells do not overlap. Final validation accepts the plan, while adding an obstacle
at either shared route cell rejects it. This fixed-geometry probe isolates the
entrance saving from room packing.

The separate four-gate experiment illustrates why access and gate count must be
considered together: metric-v2 three-layer seed one changes from 232/292 compound
edges uncovered without E3a to 176/282 with E3a, with two arrival-gate posts using
four crossing cells. It also grows from 18 to 22 logical entrances. Trace evidence
proves the arrival-route predicate blocked otherwise open transit; a regenerated
layout comparison alone does not prove the cause of every coverage difference.

### Maps and closeout

Saved synthetic seed-one maps live under the experiment directory:

- `viz-e0-e1/`: baseline versus local shared join, including three-layer, beach
  and clutter cases; reviewed by root.
- `viz-e0-gates4/`: baseline versus four-gate request; reviewed by root.
- `viz-e2-e3a-gates4/`: four gates with/without arrival-route sharing; both maps
  change accepted packing/site and must not be presented as a frozen-layout test.
- `viz-e3a-e3b-default-v2/industrial-3layers-joint.png`: E3a versus E3b default
  two-gate seed one. The room bounds, field and primary geometry match; visible
  changes are defensive slots, cover and post returns.

E4 kept the default two-gate request and required field gate, then preferred a
different outward direction for the secondary existing gate. On three-layer
seed one it changed selected site and packing (two candidates and 19 attempts to
one candidate and four attempts), reduced slots from 41 to 34, raised the longest
uncovered gap from 13 to 25, and modestly worsened compound edges from 170/230 to
171/237. It was not extended beyond this seed.

Focused E3a/E3b access and blocked-route checks, all 11 defense-capacity checks,
exposed-side terrain checks and deterministic replay passed. The full LayoutTests
run completed 175 tests with one failure: the known pre-existing `GL peninsula
captured tile places a joint settlement` packing failure; all other 174 passed.
The full PlannerTests suite passed with zero failures. The Release build succeeded
with zero warnings and zero errors. Detailed output is under `experiments/final/`.
The current source contains no temporary trace statements.

The corrected metric is labeled `metric=v2-no-corner-cut` in
`experiments/final/metric-v2-seed1.log`. E0 and first corrected logs remain for
history but are superseded by metric-v2 for coverage claims. These pure planner
runs do not demonstrate live pathfinding, combat, save compatibility or
multiplayer execution. No commit or push has been made.

## E5 — first capped compound-wall prototype (historical, 2026-10-16)

This prototype used the then-selected hard maximum of three compound crossings
per side and one per field side. The user later superseded that compound policy
with a hard total of twelve and a soft three-per-side preference; the E5 metrics
below are historical and must not be presented as the current cap behavior. E5
first separated compound wall-adjacent targets from field targets and counted a
serial door band through adjacent reinforcement layers once.

### Frozen-layout construction probe

`e5-frozen-test.log` is a construction harness over frozen three-layer
industrial joint geometry with the same room/field digest on both outputs. Its
left panel is deliberately **defense-free**, not the old E3 defense, so it is
not an old-vs-new improvement comparison. The harness reports compound 45/203
exposed edges uncovered, field 51/99, longest uncovered runs 11/12, 15 posts
and 55 alternative slots, all with potential LOS to compound targets. Main
crossings are `[3,2,3,2]`; field crossings are `[1,1,1,1]`. This result explains
the candidate construction but does not establish an improvement over the
previous defense.

The corrected two-panel image is
`experiments/viz-e5-frozen/industrial-3layers-joint.png`; its left and right
inputs come from distinct files under `e5-frozen-before-map/` and
`e5-frozen-after-map/`. It shows the frozen room and field geometry with added
compound-wall crossings and nests. The earlier duplicated-input image is
superseded.

### Normal-generation prototype evidence

Latest-source seed-one outputs pass final pure-layout validation for these
cases. Metric v3 reports potential LOS to compound wall-adjacent ground and
field wall-adjacent ground separately. Slots are alternative staffed positions,
not simultaneous defenders.

| Case | Compound uncovered / targets | Field uncovered / targets | Longest compound / field gap | Posts / alternative slots | Main crossings by side | Field crossings by side |
|---|---:|---:|---:|---:|---|---|
| Industrial, 1 layer | 7 / 146 | 21 / 74 | 3 / 11 | 16 / 46 | `[3,3,3,3]` | `[1,1,1,1]` |
| Industrial, 2 layers | 4 / 142 | 20 / 72 | 1 / 10 | 15 / 46 | `[3,3,3,3]` | `[1,1,1,1]` |
| Industrial, 3 layers | 36 / 189 | 39 / 87 | 11 / 12 | 15 / 55 | `[3,3,2,2]` | `[1,1,1,1]` |
| Beach, 1 layer | 18 / 108 | 21 / 60 | 9 / 15 | 13 / 35 | `[3,2,3,3]` | `[1,1,1,0]` |
| Clutter, 1 layer | 16 / 113 | 31 / 71 | 3 / 11 | 11 / 30 | `[3,3,2,2]` | `[1,1,1,1]` |

Logs and exports are `e5-final-{1layer,2layer,normal-3layer-v3,beach,clutter}.log`
and their matching `-export` directories. The three-layer seed-one input digest
is `6060999073C66CF5`; it completed one site, three attempts and 63,508
evaluations, with no repair. Compared with earlier E3 logs, its selected site
and room packing changed, so those older coverage counts are not a causal
baseline. At that checkpoint, a metric-v3 run of the old defense on the exact same frozen
geometry was still outstanding. That comparison is completed in the total-twelve
results below.

The final-source three-layer seed sweep passed 3/3. Seed 2 leaves 40/210
compound and 32/98 field edges uncovered (longest gaps 8/12), with 14 posts and
51 alternative slots. Seed 3 leaves 34/192 compound and 42/86 field edges
uncovered (gaps 11/12), with 16 posts and 56 slots. Both use main caps
`[3,3,3,3]` and field caps `[1,1,1,1]`; see
`experiments/e5-final-3layer-seeds123-v3.log`.

### Integration and validation state

The normal generator now creates gatehouses with one door in each **adjacent**
reinforcement shell, selects a short public interior connector, subtracts every
door from wall construction and material ledgers, and retains routes through
field staging and final validation. The spawner and P3 perimeter registration
consume all serial doors. Field-wall cells count as solid flanks for a compound
opening that meets the field perimeter. The retained final validator enforces the total twelve-compound-crossing budget
and the one-field-crossing-per-side cap, counting existing arrivals and shared
interfaces. The global greedy selector compares marginal compound coverage
across all exposed fronts before spending another crossing. A side below three
has only a soft tie-break advantage, and a useful front can exceed three when
terrain closes or protects other sides.

This was the pre-total-budget checkpoint: single-case V2 runs for one, two and
three reinforcement layers, beach and clutter passed, but the prototype still
had the old per-side gate policy, eight large-roster capacity failures, a
frontage assertion issue, and the known GL packing failure. Those failures and
its cap table are historical; final verification for the total-twelve policy is
recorded below.

## Total-twelve global allocation and final comparison — 2026-10-17

The user selected twelve compound crossings total and one field crossing per
compass side. The three-per-side preference is soft; the planner now builds
bounded legal post candidates for all exposed fronts and greedily selects the
next option by marginal compound coverage, then gap reduction, then the soft
side-spread preference, with staffing and approach coverage secondary. It will
not spend a new compound crossing with zero marginal compound gain or add a
field-only opening. The finite search is bounded by two flank posts per possible
compound or field crossing. It may leave budget unused when coverage is already
served or no legal candidate improves the wall. All route, cover, material,
public-access, reinforcement, crop, and final-occupancy checks remain active.

The earlier metric-v3 boundary measure was audited and superseded. Metric v4
measures the enclosure boundary, not only the final placed-wall inventory:
ordinary arrival doors and compound serial door bands remain compound boundary
targets; field inner/outer entrances and field-port doors remain field boundary
targets; post wall returns remain opaque obstacles but do not become boundary
segments. The attacker flood still uses final occupancy and prevents corner
cutting. All results below use `metric=v4-final-boundaries-no-corner-cut`.
Coverage counts are alternative potential LOS positions, not simultaneous
staffing or proof of live firing.

The seed-one through seed-eight large-roster regressions passed after the new
policy. The final metric-v4 results are:

| Seed | Covered compound boundary targets | Compound crossings S/E/N/W | Field crossings S/E/N/W |
|---:|---:|---|---|
| 1 | 178 / 179 | `[3,3,4,2]` | `[1,1,0,1]` |
| 2 | 210 / 214 | `[4,3,2,3]` | `[1,0,1,0]` |
| 3 | 153 / 161 | `[4,2,3,3]` | `[1,1,0,1]` |
| 4 | 197 / 219 | `[4,3,2,3]` | `[1,0,1,1]` |
| 5 | 177 / 195 | `[4,3,3,2]` | `[1,1,1,1]` |
| 6 | 200 / 211 | `[4,1,3,4]` | `[1,1,1,1]` |
| 7 | 198 / 202 | `[3,2,2,5]` | `[1,1,1,1]` |
| 8 | 192 / 206 | `[3,3,3,3]` | `[1,1,0,1]` |

The follow-up after ranged-threat scoring is pooled at 1,505 / 1,587 (94.8%).
The weakest case is seed four at 197 / 219 (90.0%); the weakest directional
face is seed five east at 42 / 50 (84.0%). No per-case percentage floor was
tuned into acceptance. These are the latest eight-case results; the original
`e5-total12-metricv4-8seeds.log` is the pre-ranged-scoring checkpoint.
`final-total12/defense-focused-after-shoreline-fix.log` contains the latest
independent per-direction fractions, capacity diagnostics and final crossings.
A one-front rocky fixture gives the only reachable south face six crossings
while east, north and west receive none; 80 / 84 compound targets are covered.
The broad fixed rectangle still requires a full ranged pack on every exposed
side. A pure garrison fixture assigns two real post slots to two of ten ranged
defenders and keeps the other eight in safe gate reserve.

`e5-total12-frontages-capcheck.log` records the field-port, crop and turret
checks. Generated field ports only count when weapon-compatible positions can
fire on compound targets. A fixture with the required east field entrance
rejects a second east-side field crossing; it does not add a field-only opening
when the existing connection remains valid. Turrets preserve useful arcs and
final diagnostics report weak geometric shortfalls. The negative turret fixture
rejects a footprint occupying a protected post slot.

### Same-base E3 versus total-twelve E5 comparison — pre-shoreline repair

The comparison uses the E3 planner implementation from commit
`ea6cb53153f0f5e5faa5e380591b988c9aeac303` and the retained total-twelve E5
planner on the exact same raw frozen layout object: 12 residents, seed one,
Industrial/Temperate, three wall layers, raw geometry digest
`9F92078BA30A2698`. The raw geometry restores E5 defense door cells, wall
returns and field-wall cells before either planner runs, then applies each
planner result to that same room, field, primary-gate and wall geometry. This is
not a defense-free comparison. Metric v4 uses each final plan's boundary
inventory and attacker flood, so target denominators differ when an opening
changes which wall-adjacent cells attackers can reach.

- E3 old: 162 / 197 compound targets uncovered (35 / 197 covered), 26-cell
  longest gap, 11 posts and 38 slots. This historical planner exceeds today's
  field-crossing cap.
- Total-twelve E5: 27 / 195 uncovered (168 / 195 covered), 8-cell longest gap,
  11 posts and 39 slots; compound crossings `[4,2,1,1]` (eight total), field
  crossings `[1,1,1,1]`.

The metric log is `e5-same-base-old-new-v4.log`. Matching ASCII inputs are in
`e5-same-base-old-new-v4/`; the rendered and inspected side-by-side image is
`e5-same-base-old-new-v4-viz/industrial-3layers-joint.png`. The prior metric-v3
matched comparison and seed logs remain historical only. These synthetic
planner maps do not demonstrate live RimWorld pathfinding, firing, save
compatibility, or multiplayer execution.
Ranged-threat scoring later changed selected posts on the eight-seed cases. This
matched comparison and image are retained as pre-repair evidence and must be
rerun before describing them as the current E5 result.

Focused current-policy checks pass for total-cap/serial-door counting, the
one-front allocation, compound-first candidate priority, shortened-nest cache
invalidation, eight large-roster seeds, public field access/crops, the field
cap, turret occupancy and ranged reserve assignment. Full-suite and build
results follow. No commit, push or merge has been made.

### Final validation closeout — 2026-10-18

- Post-repair full `LayoutTests`: 180 tests, 179 passed and 1 failed. The shoreline
  regression `P4B approach defense covers exposed sides and respects terrain` now
  passes. `GL peninsula captured tile places a joint settlement` still fails with
  `no_valid_layout`; the terminal diagnostic is `result:no_valid_layout sites=3
  attempts=28 evaluations=474469 repairs=158 routeNodes=250605 budgetExhausted=False`,
  including `attempt:room_unplaceable 55,90/OpenGround medical growths=3`.
  The run stops here per the verification brief, so post-repair `PlannerTests`,
  Release build, and same-base comparison were not run.
- Continuation post-repair full `PlannerTests` passed all 89 tests with zero failures.
  The post-repair Release build succeeded with zero warnings and zero errors.
- Their logs are `planner-tests-post-repair.log` and `release-build-post-repair.log`
  under `.runtime/investigations/wall-coverage/experiments/final-total12/`.
- Pre-repair full `PlannerTests` passed with zero failures, and the pre-repair Release
  build succeeded with zero warnings and zero errors. The configured shipped-DLL
  allowlist is only `RealisticBases.dll`; these are historical results, not post-repair
  validation.
- The post-repair full layout log is
  `.runtime/investigations/wall-coverage/experiments/final-total12/layout-tests-post-repair.log`.
  Pre-repair logs remain as `layout-tests.log`, `planner-tests.log` and
  `release-build.log`. The post-repair same-base log and image remain pending; the
  old matched side-by-side map remains historical evidence described above.
- These remain pure planner, test and compilation results. They do not establish
  live RimWorld pathfinding or firing, save compatibility, or multiplayer
  behavior.

### Paused handoff after shoreline ranged-threat repair — 2026-10-18

The user explicitly paused further progress. The current uncommitted repair is
in `P4BApproachDefense.cs`: candidate scoring derives ranged threat samples from
reachable approach probes with line of sight to the exterior cell of an exposed
**compound** wall face. Its per-post hits use a separate immutable-post cache.
A new compound crossing can now be selected for useful ranged compound coverage
when melee wall-adjacent gain is zero; compound gain and gap reduction still
rank first. Field openings still require compound ground coverage, and their
one-per-side cap remains. The shoreline fixture now checks a real west-wall
ranged threat and a useful defending shot, forbids water in routes and cover,
and asserts a rock-backed west face receives no crossing. The ranking fixture
gives a field-only candidate a high ranged score and verifies main-wall
compound gains still win.

After this repair, the focused shoreline test passed, and the full
defense-named subset passed 31/31 tests in 242,978 ms. That subset includes the
updated eight-seed v4 regression and compound-priority, crossing-cap, cache,
field-limit, frontage, broad-pack and one-front checks. Its latest pooled v4
result is 1,505 / 1,587 (94.8%); updated per-seed figures are in the table
above. Logs are
`.runtime/investigations/wall-coverage/experiments/final-total12/shoreline-fix-focused.log`
and
`.runtime/investigations/wall-coverage/experiments/final-total12/defense-focused-after-shoreline-fix.log`.

The post-repair full LayoutTests rerun passed the shoreline case, while the GL
peninsula case still fails with `no_valid_layout`. The continuation PlannerTests
run passed 89/89 and the Release build succeeded with zero warnings and errors.
The user confirmed GL peninsula is pre-existing and directed that the baseline
worktree check be skipped. The same-base metric-v4 rerun remains pending because
its former one-test runner is no longer registered in the current test source;
only its old maps remain retained. The retained comparison and rendered image
predate ranged-threat scoring and remain historical evidence only.
There is no active mod-test process or test container. Docker's existing
`buildx_buildkit_multiplatform0` container remains running. The interrupted
focused command's retained session id was `44741`; it completed normally with
31/31 passes and exit code 0 before the pause took effect. No changes were made
to the user's `.rimworld-dev.json` / `.rimworld-dev.md` package-ID edits. No
commit, push, merge, or normal-profile game run occurred.

Current verification status: full LayoutTests completed with the known GL failure;
the shoreline case passed. PlannerTests passed 89/89 and the Release build had zero
warnings and errors. The exact disposable RimWorld process check found no running
game. The full layout log is
`.runtime/investigations/wall-coverage/experiments/final-total12/layout-tests-post-repair.log`;
the named report is `.runtime/investigations/wall-coverage/REPORT-post-repair-verification.md`.
The continuation logs are `planner-tests-post-repair.log` and
`release-build-post-repair.log` in the same folder. The historical comparison is
`e5-same-base-old-new-v4.log` with its old rendered image. Completing the current
comparison requires a temporary runner; the brief prohibits source or test code
changes, so this step is awaiting direction on that constraint. No source or test
code was changed, and no commit, push, merge, version bump, or normal-profile game
run occurred during this verification.
