# Realistic Bases: progress tracker

Updated: 2026-09-24

## Current state

Design, source review, and P0–P3 are complete. P4 and P4A are active together: the procedural planner is the target for remaining terrain, fortification, equipment, and runtime work. P5 remains unstarted. Earlier fixed-compound P4 observations are historical evidence only.

Specification: [design and feasibility](design-feasibility.md). Delivery gates: [implementation plan](implementation-plan.md). Detailed evidence: [P0 findings](p0-findings.md).

## Milestones

| Milestone | Status | Completion evidence / next action |
|---|---|---|
| Design and feasibility | Complete | Design, implementation plan, and source review written |
| P0 — Resolve the technical risks | Complete | Build, Core/GL generation, hostile jobs, save/load, lifecycle, defeat, and both recovery modes observed in game |
| P1 — Planner and validation harness | Complete | Fixed-input determinism, invalid-terrain rejection, bounded termination, required property cases, read-only planning, diagnostics, and Core/GL integration passed; see [P1 findings](p1-findings.md) |
| P2 — First complete physical settlement | Complete | Industrial/tribal profiles, full plan-driven spawning, real power/freezer/outage behavior, Core recovery, and GL + Map Preview normal/recovery matrices passed; see [P2 findings](p2-findings.md) |
| P3 — Playable defensive encounter | Complete | Bounded local garrison, physical mortar supply, save/reload, full failure matrix, tribal exclusions, and Core/GL regression passed; see [P3 findings](p3-findings.md) |
| P4 — Terrain variety and stronger fortifications | In progress | Finish terrain and fortifications on the P4A procedural generator; see [P4 findings](p4-findings.md) |
| P4A — Procedural settlement layouts | In progress | Core flat/mountain/foothill, three wall layers, forced rollback, cooler-room audit, and P3 save/reload pass; lakeshore, climate, tribal, and optional-mod matrix remain |
| P5 — Encounter supplies, trade stock, and persistent consequences | Not started | Bounded reserves, trade consistency, no-duplication persistence, and accessible/separate hazardous stock placement where justified; full sustainability deferred |
| P6 — Balance, specialized tactics, and release | Not started | Requires P5 and supported terrain coverage; add route diagnostics, evidence-driven soft-preference tuning, reproducibility and encounter-quality review |
| P7 — Settlement specialization | Not started (post-MVP) | Final planned step: agriculture/industry first; evaluate mining, ranching, forestry, trading, military, medical and research profiles; verify trade-product influence feasibility |

## P0 checklist

| ID | Status | Completion evidence |
|---|---|---|
| P0.1 Minimal mod/build setup | Complete | Repository-prescribed Docker Release build: 0 warnings/errors; isolated Core launch and disposable `.rws` save/reload passed |
| P0.2 Generation hook and guards | Complete | Guarded `Settlement.MapGeneratorDef` postfix selected `RB_P0_Base_Faction` only for eligible ordinary hostile human settlements; default remains off |
| P0.3 Terrain and optional-mod order | Complete | Historical 400/401 Core and GL evidence; P4A runtime moved settlement/power to 1400/1401 for final terrain/roof preservation |
| P0.4 Hostile garrison behavior | Complete | Two-pawn custom Lord ran; holder remained on its post under real damage; worker picked up and consumed finite fuel |
| P0.5 Hostile NPC work | Complete | Vanilla refuel, bill, and sow work givers completed generator refuel, stove refuel, a meal, and rice sowing without ownership changes or replenishment |
| P0.6 Lifecycle | Complete | Active work survived save/load; removal/re-entry preserved wood depletion and marker-wall damage; vanilla defeat ended controller ownership |
| P0.7 Failure recovery | Complete | Core and GL pre-spawn rejection made zero mutations; partial failure removed one owned thing, restored one terrain cell/checksum, and both fallbacks remained playable with no traps/IEDs |
| P0 report | Complete | [P0 findings](p0-findings.md) records hooks, observed events, accepted revisions, and the reproducible protocol |

## Accepted P0 decisions

| Item | Decision |
|---|---|
| Generation integration | Keep the narrowly guarded getter postfix and dedicated generator; do not replace explicitly selected third-party generators |
| Step order | Preserve `MapCommonBase`; P4A settlement/power steps run at 1400/1401 after vanilla contents that changed the final terrain/roof checksum |
| Worker adapter | Keep the map-scoped adapter over vanilla work givers; finite physical inputs and observed completion are the correctness boundary |
| Unload/re-entry | Persist coarse settlement deltas by world-object ID; regenerate defenders on re-entry. Individual defender identity is intentionally outside the current contract |
| Retreat test | Deterministic map removal/reopen replaces a literal caravan retreat for P0 because it exercises the same map-unload/re-entry persistence boundary without adding caravan behavior to the spike |
| Arrival test | Require a pass-doors route from map edge to the settlement target. Hostile doors may still require normal breaching in actual play |
| Map Preview | Its preview pipeline stops before order 230, so the later settlement step is excluded from previews; the prior fixed-compound compatibility run does not establish P4A compatibility |
| Supported build | Tested executable: RimWorld 1.6.4871 rev597. The installed `Version.txt` rev595 text is treated as stale packaging metadata; compile API remains pinned to 1.6.4871 |

No active P0 blocker remains. Carryover `CLAUDE.md` describes another mod; the Realistic Bases documents remain authoritative.

## P1 committed checklist

| Step | Scope | Validation and commit policy | Status |
|---|---|---|---|
| P1.0 | Milestone scope, checklist, and evidence document | Documentation review; standalone checkpoint commit | Complete |
| P1.1 | Versioned profile inputs, stable seeds, immutable terrain snapshots, connected-region analysis | Docker build: 0 warnings/errors; four focused pure tests passed; committed as the P1.1 feature checkpoint | Complete |
| P1.2 | Bounded candidate search, rectangular room envelopes, adjacency/circulation, reservations, validator, property matrix | Docker build: 0 warnings/errors; 12/12 pure tests passed including every required property case; committed as the P1.2 feature checkpoint | Complete |
| P1.3 | Replace fixed diagnostic layout through P0 integration; add overlays and compact generation report | Docker build: 0 warnings/errors; isolated Core primary plan/spawn, playable arrival, overlay, report, terrain preservation, and final audit observed; committed as P1.3 checkpoint | Complete |
| P1.4 | Integrated rejection/fallback and optional-mod runtime matrix; final findings | Core and GL + Map Preview normal/rejection/partial matrix passed, including `GL_Cliff` + `Caves`; final P1 checkpoint committed after full verification | Complete |

Commit mapping is recorded in [P1 findings](p1-findings.md). The same stepwise commit policy applies to later authorized milestones.

## P2 committed checklist

| Step | Scope | Validation and commit policy | Status |
|---|---|---|---|
| P2.0 | Milestone scope, bounded checklist, assumptions, and evidence document | Documentation review; standalone checkpoint commit | Complete |
| P2.1 | Versioned industrial/tribal capabilities, resident-driven facility demand, resource/import budgets, crops, startup food/fuel, equipment, and power scenarios | 20/20 combined pure tests passed; Docker Release build 0 warnings/errors; standalone feature commit | Complete |
| P2.2 | Deterministic complete-settlement layout: all facilities, fields, gatehouses, logistics routes, interaction cells, utilities, cover/turrets, material allocation, and hard validation | 29/29 combined pure tests passed; deterministic full layout, overlap/capability/route validation; Docker Release build 0 warnings/errors | Complete |
| P2.3 | Plan-driven RimWorld spawning, one-time initialization, equipment/stocks/crop growth, operational power/freezer behavior, and realized-map audit/debug reporting | 29/29 pure tests and Docker Release build passed; Core industrial/tribal, rejection, deep rollback, and real outage audits passed; standalone integration commit | Complete |
| P2.4 | Recovery and optional-mod regression matrix, declared startup scenario observations, final findings and exit decision | Core rejection/partial rollback and real outage passed; Geological Landforms 1.7.13.1 + Map Preview normal/rejection/deep-partial matrix passed; static checks and closeout commit | Complete |

Step-to-commit mapping and observed evidence are recorded in [P2 findings](p2-findings.md). P3 now proceeds under the checklist below.

## P3 committed checklist

| Step | Scope | Validation and commit policy | Status |
|---|---|---|---|
| P3.0 | Milestone scope, bounded checklist, evidence separation, and MVP supply boundary | Documentation review; standalone checkpoint commit `4fca42a` | Complete |
| P3.1 | Pure defensive doctrine: sectors/posts, weapon-aware one-role assignment, pursuit and route bounds, reserves, breach response, needs relief, fallback, invalid-post reassignment, and anti-oscillation state | 36/36 combined pure tests; Docker Release build 0 warnings/errors; commit `d8c0d63` | Complete |
| P3.2 | Physical encounter contract: usable firing posts outside opaque walls, staffed reachable unroofed mortars, finite HE/bounded incendiary stocks, active-rule reload/barrel supplies, roster/facility/power budgets, and tribal exclusions | 42/42 combined pure tests; Docker Release build 0 warnings/errors; commit `7feb21a` | Complete |
| P3.3 | Runtime garrison Lord/controller, narrow support jobs, physical consumption/exhaustion, local breach response, save/load state reconstruction, diagnostics, and player-scope guards | 43/43 pure tests; Docker Release build 0 warnings/errors; focused Core role/containment/consumption/save-load checks; standalone integration commit | Complete |
| P3.4 | Encounter matrix and closeout: multiple approaches, night, harassment, breach, incendiary threat, operator/power loss, relief, invalid post, exhaustion, active reload, tribal, Core/GL regression, and player unaffected | 43/43 pure tests; Docker Release build 0 warnings/errors; full Core/tribal/GL live matrix passed; standalone closeout commit | Complete |

P3 exit is a newly generated supplied encounter whose defenders occupy usable posts, respond locally within pursuit/route limits, consume finite physical supplies, preserve active behavior through save/reload, and do not affect player pawns or settings. This is not a claim of long-term sustainability, trade-stock integration, or full unload/revisit casualty identity.

## Current MVP scope revision — 2026-09-20

The user reduced the sustainability requirement and added settlement trade-stock fidelity. P5 now targets best-effort encounter operation backed by finite reserves, with a provisional three-game-day routine horizon and separate six-game-hour declared combat duty budget. Full long-term self-sufficiency, repeated harvest/seasonal testing and continuous production balance are deferred, not deleted. See [supply and MVP scope](supply-and-mvp-scope.md).

Trade goods must reflect the actual settlement offer separately from operating reserves. Snapshot/ownership/reconciliation support is planned in P5; P2's current generated stocks are not evidence of this new feature. Purchases, sales, looting, depletion and restock cannot duplicate goods. Persistence/ownership correctness remains in the MVP.

P0–P3 findings remain historical evidence; no completed checks were rewritten. P4 is in progress and P5 remains unstarted. The earlier P2 outage observation used 360 W while a planning scenario named 650 W; P3 measured 80 W net gain during combat, 0 W after generator removal, and 0 W added continuous mortar load rather than restating the planning value as observed fact.

## P4 committed checklist

| Step | Scope | Validation and commit policy | Status |
|---|---|---|---|
| P4.0 | Terrain-archetype scope, mutation/recovery contract, live matrix, evidence policy, and explicit P5/P6 boundary | Documentation review; standalone checkpoint commit | Complete |
| P4.1 | Immutable terrain analysis and deterministic bounded archetype selection/rejection | 47/47 combined pure tests and Docker Release build 0 warnings/errors; standalone feature commit | Complete |
| P4.2 | Foothill/excavated-mountain layouts, roof/support/mutation recovery, and P3 geometry adaptation | Pure/build suite passed; accepted Core 160×160 mountain and deep rollback passed. A Core foothill generated with 12/12 facilities, two-layer gates, P3/mortar, and player arrival. | Complete |
| P4.3 | Lakeshore/peninsula/supported-island layouts with legal approach, bridge, gate, and circulation constraints | Versioned preserved-water footprint plus dry-route pure case passed; Core water/rejection cases remain. | In progress |
| P4.4 | Layered fortifications, palettes/budgets/climate alternatives, P3/save-reload regression, Core/GL matrix, and exit | Actual runtime/recovery evidence and closeout commit | Pending |
| P4/P4A integration | In progress | ProceduralLayout is the production plan and validator. Build runtime catalog/resources, stable seed/stage, transactional spawn, geometry-based P3, fixtures/utilities/fields, then terrain/fortification and live matrix. Commit each validated checkpoint. |

The 2026-09-23 scope change supersedes the earlier ordering that deferred P4A until P4 closeout. Do not count fixed-compound passes as procedural runtime evidence. P4 and P4A exit only after the new path passes their pure, build, Core, GL, and recovery gates.

## Future planning update — 2026-09-24

The user accepted the bounded ideas from the [colleague-design review](colleague-design-review.md) for future milestones. P5 handles justified hazardous-stock separation while preserving reserve/trade ownership. P6 measures important actual routes before changing soft preferences or bounded candidate scoring, and reviews encounter quality with reproducible inputs. Existing connectivity and functional constraints remain mandatory; no global optimizer, new broad room catalog or Multiplayer work is added.

P7 is the final planned milestone, after the P6 MVP release: agriculture/industry first, followed by feasibility-ranked mining/quarrying, ranching, forestry, trading, military, medical and research candidates. Specialization changes facility demand and layout emphasis. Explicitly investigate whether supported hooks can influence actual offered products at initial stock creation/restock; preserve existing offers and document unsupported cases. It retains basic settlement functions, finite encounter supplies, stable saved identity and no-duplication behavior. Full sustainability remains deferred. All added work is planned, not completed; current P4/P4A closeout is unchanged. Commit each validated implementation step separately.

## Activity log

| Date | Update |
|---|---|
| 2026-09-14 | Completed design/feasibility work and started P0 on baseline `0f12114`. |
| 2026-09-14 | Added the P0 scaffold and probes; Docker build and isolated startup smoke tests passed. |
| 2026-09-19 | Corrected the Harmony reference to the installed 2.4.1 runtime; observed hostile work, save/load, Core generation, recovery, re-entry, and defeat behavior. |
| 2026-09-20 | Verified reachable playable arrivals and repeated normal/rejection/partial cases with Geological Landforms 1.7.13.1 and Map Preview active; closed P0. |
| 2026-09-20 | Checkpointed P0 as `646db81`; started P1 with a stepwise validation-and-commit checklist. |
| 2026-09-20 | P1.1 added versioned inputs, stable seed/digests, copied terrain snapshots, and deterministic connected-region analysis; Docker build and four pure tests passed. |
| 2026-09-20 | P1.2 added bounded candidates/routes, room and circulation data, occupancy/utility/defense reservations, validation, and the full 12-case pure property suite. |
| 2026-09-20 | P1.3 replaced fixed-site selection with immutable snapshot planning and plan-driven diagnostic spawning; Core runtime produced a valid plan/spawn, overlay, compact report, playable arrival, and clean final audit. |
| 2026-09-20 | P1.4 passed Core recovery and GL + Map Preview normal/recovery matrices, including `GL_Cliff` + `Caves`; all P1 exit criteria passed and P1 closed. |
| 2026-09-20 | Authorized P2; established the bounded physical-settlement checklist and evidence policy. |
| 2026-09-20 | P2.1 added versioned industrial/tribal facility demand, nutrition/crop/fuel/equipment assumptions, bounded budgets, and startup power scenarios; 20/20 combined pure tests and Docker Release build passed. |
| 2026-09-20 | P2.2 added a deterministic 31x25 compound plus 10x13 field, complete facility placements, local-material input, two edge routes, utilities/defenses, and hard validation; 29/29 combined pure tests and Docker Release build passed. |
| 2026-09-20 | P2.3 replaced the diagnostic shell with full plan-driven settlement spawning and realized-map audits; Core industrial/tribal profiles, zero-mutation rejection, 476-thing/298-roof rollback, clean power topology, freezer operation, and a real six-hour generator-service buffer passed. |
| 2026-09-20 | P2.4 passed Geological Landforms 1.7.13.1 + Map Preview normal/rejection/deep-partial cases on live maps (including GL cliff/river mutators), final pure/build/XML/diff checks passed, and P2 closed. |
| 2026-09-20 | Revised P5 to bounded encounter readiness and trade-stock fidelity; documented full sustainability as post-MVP. Documentation only. |
| 2026-09-20 | Authorized P3 and established a stepwise defensive-encounter implementation, validation, and commit checklist; P4 remains unstarted. |
| 2026-09-20 | P3.1 added deterministic exclusive role/post assignment, weapon compatibility, local breach state, route-wide pursuit limits, safe relief, inner fallback, and reassignment cooldown; 36/36 pure tests and Docker Release build passed. |
| 2026-09-20 | P3.2 added deterministic usable outer/inner posts and an industrial unroofed mortar court with a six-hour finite duty budget, conditional barrels, stock-purpose metadata, zero added mortar power load, and hard tribal exclusions; 42/42 pure tests and Docker Release build passed. |
| 2026-09-21 | P3.3 replaced the P0 probe Lord with the settlement-scoped runtime garrison, added narrow support and mortar jobs, strict assigned-post containment, physical reserve consumption, active save/reload reconstruction, and diagnostics. 43/43 pure tests and a clean Docker build passed; focused Core live checks preserved assignments and a loaded shell across reload with no player pawn control. |
| 2026-09-21 | P3.4 passed four approach sectors, night/harassment and incendiary gating, breach/fallback/relief, operator and power loss, finite exhaustion, tribal exclusions, active reload evidence, and Geological Landforms + Map Preview regression. Invalid encounter plans now reject before mutation. P3 closed; P4/P5 remain unstarted. |
| 2026-09-21 | Authorized P4; documented terrain-adapted archetype, explicit mutation/recovery, fortification, climate, P3-regression, and Core/GL validation gates. P5/P6 remain unstarted. |
| 2026-09-21 | P4.1 added immutable versioned terrain-archetype analysis with deterministic flat/foothill/excavated-mountain/lakeshore evidence, explicit inaccessible-island rejection, and bounded search. 47/47 pure tests and Docker build passed. |
| 2026-09-21 | P4.2 implementation checkpoint added an explicit natural-rock/roof mutation ledger, transactional rock restoration, verified constructed-roof support, outside-footprint rock checksums, and exact persisted P3 controlled geometry. 49/49 pure tests and Docker build passed; runtime mountain/recovery evidence remains outstanding. |
| 2026-09-21 | Extended P4.2/P4.3 with deterministic four-side farm selection, all-natural-rock excavation ownership, a debug-only mountainous Core harness, and preserved lakeshore-water/dry-route validation. 50/50 pure tests and Docker build passed. Two mountainous maps safely rejected before mutation because neither could host the complete footprint. |
| 2026-09-21 | Passed the accepted Core mountain and deep recovery P4.2 paths: a 160×160 mountain excavated 187 owned rocks while preserving unowned rock and had valid P3/mortar/gates/routes/arrival; forced failure restored 477 things, 298 roofs, one rock, and the checksum before retry succeeded. |
| 2026-09-21 | Added generated-foothill and terrain-following P3 harness support. The first Large Hills map was correctly classified as an excavated mountain and passed the accepted runtime path; the harness now targets Small Hills for an honest foothill case. |
| 2026-09-22 | Added the P4 final planned-occupancy/cell-graph contract and connectivity architecture note. Public circulation now has one resident root, room-local target access is separate, and the former sealed inset-ring layout has a pure regression that fails with `public_root_blocked`. |
| 2026-09-22 | Recorded P4A after P4 and before P5: P2's fixed 31×25 arrangement remains historical prototype evidence, not a procedural-layout result. P4A will add seeded demand/terrain-aware packing, circulation, enclosure, portals, variation, and fallback observability under separate authorization. |
| 2026-09-23 | Merged the additive P4A pure foundation/tech-stage history into the P4 continuation line; mod build, P1–P4 tests, and 62 P4A layout tests passed. Core disposable runtime then generated a two-layer foothill settlement: 4/4 faction doors, 12/12 reachable facilities, player arrival, P3 active encounter and save/reload restoration passed. |
| 2026-09-23 | Switched ordinary generation to the P4A procedural layout, added preflight and a rock/roof/plant transaction, and moved steps to 1400/1401. A Core mountain live run passed final terrain/roof checksum, two gates, 208 excavations, 117 retained thick roofs, 128 conduits, four active defenders, finite fuel stock, and playable arrival. A second Core mountain forced-partial run restored 329 owned things and the original checksum, then succeeded on the same map. Current Docker build, 55 planner tests, and 62 layout tests pass. Remaining procedural matrix and save/reload evidence keep P4/P4A open. |
| 2026-09-23 | Integrated the cooler orientation and sealed-exhaust-pocket work. Added a live `Room` audit; Core mountain (2 coolers), foothill (4), and three-layer foothill (3) passed actual outdoor exhaust, final checksum, and arrival checks. The three-layer encounter reloaded with four defenders/assignments and passed a post-reload P3 audit. Mod build and both pure suites pass; remaining flat/lakeshore, climate, tribal, and optional-mod matrix keeps P4/P4A open. |
| 2026-09-23 | Replaced distance-only crop selection with dry exterior flood from gate staging cells, excluding water, intact rock, edifices, planned structures, and the staging cells themselves. Pure barrier/reservation test and mod build pass; a Core mountain spawned 124 reachable crops and passed cooler, final-checksum, and arrival audits. |
| 2026-09-23 | Fixed flat-map failures: actionable rejection diagnostics, contiguous-thick-roof doctrine choice, reserved fallback site budget, clearer open-ground anchors, and owned excavation of non-thick natural rock. A scattered-rock pure regression and mod build pass. A clean single-process Core flat run generated nine rooms, two gates, 101 owned rock removals, 124 crops, five live outdoor cooler exhausts, four defenders and a mortar; final checksum and arrival passed. Overlapping-process results were discarded and the run-game skill now requires exact-profile process verification. |
| 2026-09-23 | A single-process forced-partial run on a Flat world tile restored 334 owned things and the original checksum, then regenerated and passed final checksum/arrival. Its layout was rock-protected; open-ground boulder-clearance rollback remains a distinct untested subpath. |
| 2026-09-24 | Reproduced a further flat-map planner rejection with a deterministic synthetic 160×160 clustered-rock/sparse-edifice fixture. Fixed over-expansion of fortification setbacks around existing edifices and perimeter collisions with them. Four fixture variants and the full pure layout suite pass, with no built wall on an existing edifice; Release build passes. The exact rejected live tile still needs retest, so P4/P4A remains open. |
| 2026-09-24 | Added accepted review ideas to future P5/P6 scope and P7 settlement specialization as the final post-MVP milestone, including additional candidate profiles and an explicit trade-product influence feasibility check. Planning only; P4/P4A implementation scope/status unchanged. |
