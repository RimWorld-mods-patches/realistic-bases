# Realistic Bases

Design work for a RimWorld mod that generates terrain-aware, functioning opponent settlements with deliberate defenses and no traps or IEDs.

- [High-level design and feasibility](docs/design-feasibility.md)
- [Implementation plan and acceptance criteria](docs/implementation-plan.md)
- [Research notes and source references](docs/research-notes.md)
- [Progress tracker](docs/progress.md)

Status: **P0 technical-risk milestone complete**, 2026-09-20. The guarded generator, terrain-order contract, hostile work, save/load, unload/re-entry, defeat handling, and both failure-recovery paths were observed in isolated Core and Geological Landforms + Map Preview runs. This remains a diagnostic prototype; P1 planner work has not started. Baseline: RimWorld 1.6, Core without DLC.

- [P0 findings and reproducible test protocol](docs/p0-findings.md)

The existing `CLAUDE.md` describes Colony Politics and references files absent from this directory. It is carryover context, not the specification for Realistic Bases. These documents concern the settlement mod requested here.
