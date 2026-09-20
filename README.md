# Realistic Bases

Design work for a RimWorld mod that generates terrain-aware, functioning opponent settlements with deliberate defenses and no traps or IEDs.

- [High-level design and feasibility](docs/design-feasibility.md)
- [Implementation plan and acceptance criteria](docs/implementation-plan.md)
- [Research notes and source references](docs/research-notes.md)
- [Progress tracker](docs/progress.md)

Status: **P1 planner and validation harness complete**, 2026-09-20. The guarded P0 integration now consumes deterministic, bounded, validated plans built from immutable terrain snapshots. Pure property tests and isolated Core plus Geological Landforms + Map Preview runtime matrices passed. This remains a diagnostic prototype; P2 complete-settlement work has not started. Baseline: RimWorld 1.6, Core without DLC.

- [P0 findings and reproducible test protocol](docs/p0-findings.md)
- [P1 planner findings and validation evidence](docs/p1-findings.md)

The existing `CLAUDE.md` describes Colony Politics and references files absent from this directory. It is carryover context, not the specification for Realistic Bases. These documents concern the settlement mod requested here.
