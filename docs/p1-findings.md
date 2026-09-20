# P1 planner and validation findings

Updated: 2026-09-20

## Scope and evidence rule

P1 is **in progress**. Its output is a deterministic, bounded, non-mutating plan plus diagnostics. It may spawn the existing minimal P0 diagnostic shell through the proven generation boundary, but it does not claim P2 facilities, economy, operational power, or complete defenses.

Evidence is recorded separately as:

- **pure tests:** planner behavior over synthetic immutable snapshots;
- **build checks:** compilation, XML, and whitespace validation;
- **runtime checks:** observed in-game integration, overlays, reports, recovery, and optional-mod behavior.

Planning must not mutate a RimWorld `Map` or any runtime object. Map reads end when an immutable terrain snapshot is constructed. Spawning consumes an already validated plan in a separate phase with the P0 rollback/fallback behavior intact.

## Implementation steps and commits

| Step | Intended result | Commit | Evidence |
|---|---|---|---|
| P0 checkpoint | Preserve completed P0 before P1 edits | `646db81` | P0 Docker build and runtime matrix complete |
| P1.0 | Scope, checklist, and evidence policy | `1bec546` | Document review |
| P1.1 | Stable inputs, seed derivation, snapshots, connected regions | `9f0aff1` | Docker build: 0 warnings/errors; 4/4 focused pure tests passed |
| P1.2 | Bounded planner, graph/reservations, validation, property matrix | This step's `feat: add bounded P1 planner and validator` commit | Docker build: 0 warnings/errors; 12/12 pure tests passed |
| P1.3 | Map adapter, P0 spawner integration, overlays, report | Pending | Build plus isolated Core runtime |
| P1.4 | Recovery/optional compatibility matrix and closeout | Pending | Isolated Core and GL + Map Preview runtime evidence |

## Fixed P1 contracts

- Profile and snapshot schema versions participate in the input digest and plan digest.
- Seeds are derived only from explicit stable inputs; runtime object identity and enumeration order are excluded.
- Candidate enumeration and every search limit are deterministic and reported.
- Rectangular room envelopes are the initial primitive, while compound outline and circulation graph are independent plan data.
- Occupancy, interaction, route, gate, roof-support, planned power-network, and planned defense-sector reservations are explicit and validated for conflicts.
- Power and defense reservations are planning information only; they are not evidence of working P2/P3 systems.
- The hard no-trap/no-IED invariant remains in both planned catalogs and final-map audit.
- P0's coarse depletion/damage state, regenerated defenders, and unload/reopen probe remain narrow P0 behavior. They do not satisfy P5 identity/casualty persistence or literal caravan semantics.

## Exit gate

P1 completes only when fixed inputs produce identical plan digests, invalid terrain is rejected, bounded searches terminate deterministically, the required property cases pass, planning/spawning separation is demonstrated, and the integrated planner/recovery/diagnostic path is observed in isolated game profiles. A successful build alone is insufficient.

## P1.1 observations

The planning model is a pure C# layer with no RimWorld or `Verse` reference. `TerrainSnapshot` copies its source cells, carries an explicit source/schema version, and exposes only read access. Stable FNV-based digests cover every profile field, snapshot cell, schema version, and explicit seed input. Region analysis uses row-major starts and a fixed four-neighbor order, so region IDs and cell order do not depend on hash-table or runtime enumeration behavior.

Focused Docker tests passed for stable seed/input digests, snapshot copying, deterministic connected regions, and disconnected-region edge metadata. These are pure-test observations, not yet evidence that live map adaptation or integrated generation works.

## P1.2 observations

The planner deterministically orders candidate origins by center distance, a stable seed-derived tie value, and coordinates. Candidate count and route-node count are hard profile inputs and are included in reporting/digests. Each accepted plan contains a compound outline, two rectangular room envelopes, a small circulation graph, an edge route, a gate, and explicit room/interaction/structure/route/roof-support/planned-power/planned-defense reservations. The latter two are reservations only, not functioning systems.

The validator checks input identity, capacity, candidate/route bounds, route continuity and edge termination, room interactions, reservation bounds/conflicts, required reservation classes, and room connectivity. The pure suite passed 12/12 cases: stable seeds, snapshot immutability, deterministic regions, region edge metadata, identical plan digests, disconnected land, narrow corridors, obstructed interactions, impossible capacity, all-water terrain, deterministic candidate limits, and unchanged snapshot digest.

This evidence demonstrates pure planning behavior. Live-map snapshot conversion, spawning separation, overlays, reports, and P0 recovery integration remain P1.3/P1.4 work.
