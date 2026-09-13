# Realistic Bases: progress tracker

Updated: 2026-09-14

## Current state

Design and source review are complete. Implementation has not yet been validated in-game. The next authorized milestone is **P0 — Resolve the technical risks**, assigned to a Codex task using **Sol (`gpt-5.6-sol`), medium reasoning effort**.

Specification: [design and feasibility](design-feasibility.md). Delivery gates: [implementation plan](implementation-plan.md). Existing evidence: [research notes](research-notes.md).

## Milestones

| Milestone | Status | Completion evidence / next action |
|---|---|---|
| Design and feasibility | Complete | Design, implementation plan and source review written; no runtime claims |
| P0 — Resolve the technical risks | Queued for execution | Complete the seven experiments below and document observed results |
| P1 — Planner and validation harness | Not started | Wait for P0 integration and recovery decisions |
| P2 — First complete physical settlement | Not started | Wait for validated planner |
| P3 — Playable defensive encounter | Not started | Requires P2 and P0 NPC-job findings |
| P4 — Terrain variety and stronger fortifications | Not started | Requires P3 and terrain-order evidence |
| P5 — Sustained operation and persistent consequences | Not started | Requires P3; validate P4 archetypes as available |
| P6 — Balance, specialized tactics, and release | Not started | Requires P5 and supported terrain coverage |

## P0 checklist

| ID | Experiment / deliverable | Status | Evidence |
|---|---|---|---|
| P0.1 | Minimal mod/build setup; Core-only launch, pinned references, debug log and reproducible test saves | Not started | Pending |
| P0.2 | Trace settlement generation; prototype guarded dedicated generator; record hooks, guards and terrain preservation | Not started | Pending |
| P0.3 | Generate debug layout after terrain; repeat with Geological Landforms; record order, checksums, exclusions and final validity | Not started | Pending |
| P0.4 | Custom Lord with hostile defenders holding under damage; actual shell/fuel pickup and use | Not started | Pending |
| P0.5 | Hostile NPC completes cooking, generator refueling and faction-owned growing work using real inputs | Not started | Pending |
| P0.6 | Save/load, retreat, removal, re-entry and defeat experiments; establish depletion/damage/roster persistence strategy | Not started | Pending |
| P0.7 | Forced rejection and partial-spawn failure; demonstrate playable recovery without traps or IEDs | Not started | Pending |
| P0 report | Technical findings, observed logs, chosen hooks, worker adapter, lifecycle strategy and recovery behavior | Not started | Add `docs/p0-findings.md` when evidence exists |

**P0 exit gate:** all required experiments have observed evidence, or the affected design contract has been explicitly revised with the reason. Source inspection, successful compilation, test scaffolding, or job assignment alone cannot establish the requested in-game behavior. Unrun runtime experiments remain pending.

## Decisions and blockers

| Item | State | Required resolution |
|---|---|---|
| Exact generation hooks and recovery path | Open | P0.2, P0.3 and P0.7 |
| Enemy work/ownership adapters | Open | P0.4 and P0.5 |
| Unload/revisit representation | Open | P0.6 |
| Build and game-test tooling | Unverified for this project | P0.1; verify the local environment |
| Active execution blocker | None recorded | Record a concrete blocker, attempted remedies and next action if one occurs |

Carryover `CLAUDE.md` describes a different mod. Use the Realistic Bases documents as this project's specification; verify any reusable environment facts independently.

## Update protocol

- The implementing task changes its milestone to **In progress** when work begins and keeps this tracker current at meaningful checkpoints.
- Use **Not started**, **Queued for execution**, **In progress**, **Blocked**, or **Complete**. Record partial evidence without marking an unfulfilled gate complete.
- Link findings, test commands/results, relevant logs or saved test artifacts, and commits where available. Distinguish source evidence, build checks and observed runtime behavior.
- Record blockers with the failed action and the next concrete step. Preserve finished milestone summaries; add dated updates below.
- Keep traps and IEDs prohibited throughout. Stay within P0 until its results are reviewed; do not quietly expand into P1 or declare the full mod operational.

## Activity log

| Date | Update |
|---|---|
| 2026-09-14 | Completed design, feasibility review and phased implementation plan. |
| 2026-09-14 | Created progress tracker and queued P0 for a Sol task at medium reasoning effort. |
