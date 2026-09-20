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
| P1.0 | Scope, checklist, and evidence policy | Pending | Document review |
| P1.1 | Stable inputs, seed derivation, snapshots, connected regions | Pending | Docker build and focused pure tests |
| P1.2 | Bounded planner, graph/reservations, validation, property matrix | Pending | Full pure property suite |
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
