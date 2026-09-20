# Realistic Bases: progress tracker

Updated: 2026-09-20

## Current state

Design, source review, **P0 — Resolve the technical risks**, and **P1 — Planner and validation harness are complete**. **P2 — First complete physical settlement is in progress**. P2 is replacing P1's diagnostic shell with explicit industrial-temperate and tribal facility, budget, supply, utility, and realized-map contracts.

Specification: [design and feasibility](design-feasibility.md). Delivery gates: [implementation plan](implementation-plan.md). Detailed evidence: [P0 findings](p0-findings.md).

## Milestones

| Milestone | Status | Completion evidence / next action |
|---|---|---|
| Design and feasibility | Complete | Design, implementation plan, and source review written |
| P0 — Resolve the technical risks | Complete | Build, Core/GL generation, hostile jobs, save/load, lifecycle, defeat, and both recovery modes observed in game |
| P1 — Planner and validation harness | Complete | Fixed-input determinism, invalid-terrain rejection, bounded termination, required property cases, read-only planning, diagnostics, and Core/GL integration passed; see [P1 findings](p1-findings.md) |
| P2 — First complete physical settlement | In progress | P2.3 Core spawning/runtime/recovery matrix complete; optional-mod regression and closeout next |
| P3 — Playable defensive encounter | Not started | Requires P2; reuse the validated P0 Lord/work approach |
| P4 — Terrain variety and stronger fortifications | Not started | Requires P3; reuse the validated late generation order |
| P5 — Sustained operation and persistent consequences | Not started | Requires P3; expand the coarse P0 lifecycle model |
| P6 — Balance, specialized tactics, and release | Not started | Requires P5 and supported terrain coverage |

## P0 checklist

| ID | Status | Completion evidence |
|---|---|---|
| P0.1 Minimal mod/build setup | Complete | Repository-prescribed Docker Release build: 0 warnings/errors; isolated Core launch and disposable `.rws` save/reload passed |
| P0.2 Generation hook and guards | Complete | Guarded `Settlement.MapGeneratorDef` postfix selected `RB_P0_Base_Faction` only for eligible ordinary hostile human settlements; default remains off |
| P0.3 Terrain and optional-mod order | Complete | Settlement/power ran at 400/401 after terrain; Core and GL 1.7.13.1 full generation passed; GL cliff audit reported both GL and Map Preview active |
| P0.4 Hostile garrison behavior | Complete | Two-pawn custom Lord ran; holder remained on its post under real damage; worker picked up and consumed finite fuel |
| P0.5 Hostile NPC work | Complete | Vanilla refuel, bill, and sow work givers completed generator refuel, stove refuel, a meal, and rice sowing without ownership changes or replenishment |
| P0.6 Lifecycle | Complete | Active work survived save/load; removal/re-entry preserved wood depletion and marker-wall damage; vanilla defeat ended controller ownership |
| P0.7 Failure recovery | Complete | Core and GL pre-spawn rejection made zero mutations; partial failure removed one owned thing, restored one terrain cell/checksum, and both fallbacks remained playable with no traps/IEDs |
| P0 report | Complete | [P0 findings](p0-findings.md) records hooks, observed events, accepted revisions, and the reproducible protocol |

## Accepted P0 decisions

| Item | Decision |
|---|---|
| Generation integration | Keep the narrowly guarded getter postfix and dedicated generator; do not replace explicitly selected third-party generators |
| Step order | Preserve `MapCommonBase`; use custom settlement/power steps at 400/401 and audit the final map separately because later vanilla/mod steps may change terrain |
| Worker adapter | Keep the map-scoped adapter over vanilla work givers; finite physical inputs and observed completion are the correctness boundary |
| Unload/re-entry | Persist coarse settlement deltas by world-object ID; regenerate defenders on re-entry. Individual defender identity is intentionally outside the current contract |
| Retreat test | Deterministic map removal/reopen replaces a literal caravan retreat for P0 because it exercises the same map-unload/re-entry persistence boundary without adding caravan behavior to the spike |
| Arrival test | Require a pass-doors route from map edge to the settlement target. Hostile doors may still require normal breaching in actual play |
| Map Preview | Its preview pipeline stops before order 230, so the order-400 settlement is intentionally excluded from previews; the live compatibility profile validated coexistence during full generation |
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
| P2.4 | Recovery and optional-mod regression matrix, declared startup scenario observations, final findings and exit decision | Core rejection/partial rollback plus Geological Landforms + Map Preview regression; static checks; closeout commit only if every P2 gate passes | Pending |

Step-to-commit mapping and observed evidence are recorded in [P2 findings](p2-findings.md). P3 remains unstarted.

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
