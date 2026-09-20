# P1 planner and validation findings

Updated: 2026-09-20

## Scope and evidence rule

P1 is **complete**. Its output is a deterministic, bounded, non-mutating plan plus diagnostics. It may spawn the existing minimal P0 diagnostic shell through the proven generation boundary, but it does not claim P2 facilities, economy, operational power, or complete defenses.

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
| P1.2 | Bounded planner, graph/reservations, validation, property matrix | `783b702` | Docker build: 0 warnings/errors; 12/12 pure tests passed |
| P1.3 | Map adapter, P0 spawner integration, overlays, report | `e931719` | Docker build and pure regression passed; isolated Core primary runtime passed |
| P1.4 | Recovery/optional compatibility matrix and closeout | This step's `test: close P1 validation matrix` commit | Core recovery plus GL + Map Preview normal/recovery matrices passed; final static checks passed |

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

## P1.3 observations

The map adapter performs one read-only pass over terrain, roofs, affordances, natural rock, edifices, fertility, water, standability, and map-edge membership. It produces the immutable snapshot before planning. The generation step then either exits untouched or passes an accepted plan to the separate spawner; the old live-map site search is removed.

An initial live Core run exhausted the original 256/512 center-biased limits on obstructed terrain. It correctly logged rejection with zero mutation and exposed an unsafe assumption in the playable debug harness. The production diagnostic profiles now use a finite 6,400-candidate bound, enough to enumerate every origin on the 80×80 spike map, while synthetic limit tests retain smaller explicit bounds. The untouched-map arrival path now safely falls back to any valid map edge rather than assuming a settlement state exists.

The repeated Core run accepted candidate 438/6400 in 33 ms after a 2 ms snapshot, produced plan digest `EE015D28981DC495`, spawned the plan-driven 9×9 outline with two room envelopes, preserved the settlement-time terrain checksum, passed `noTrapsOrIEDs=True` and `edgeReachablePassDoors=True`, and placed an existing colonist with `targetReachablePassDoors=True`. The overlay reported 4,778 suitable cells, two rooms, 36 route cells, one gate, one roof-support reservation, seven planned-power cells, two planned-defense cells, and 16 retained rejection diagnostics.

The compact report records runtime/planner/profile/snapshot/plan versions, seed and input digest, archetype, planned capacity, explicit `not-modeled-p1` supply status, candidate and route bounds, repairs/fallbacks, timing, and `operationalPower=false operationalDefense=false`. Planning is replayed against the retained input when the record is created; digest equality and unchanged snapshot status are emitted in subsequent reports. This avoids presenting P1 reservations as operational P2/P3 evidence.

## P1.4 runtime matrix

Core forced rejection accepted the compact profile with plan digest `9BC09B856DE41FD4`; partial failure restored one terrain cell and removed one owned thing before accepting compact digest `114E01E9806F769F`. Both reports emitted `deterministicReplay=True snapshotUnchanged=True`, both fallbacks preserved settlement-time terrain, both arrivals were playable, and both final audits reported edge reachability with no traps/IEDs.

With Geological Landforms 1.7.13.1 and Map Preview active:

- normal generation accepted digest `5FBBD57DA75663AD`, reported 42 planned capacity and no operational power/defense claim;
- forced rejection accepted compact digest `9270C517AF15C6C5` after 432 candidates;
- partial rollback accepted compact digest `71CBEB70C24E57C4` after checksum restoration;
- every case reported deterministic replay, unchanged snapshot, playable pass-doors arrival, edge reachability, and no traps/IEDs;
- additional isolated samples exercised `GL_Cliff` + `Caves`. One cliff plan accepted candidate 277 with digest `3391EA56ADF54297`; another accepted candidate 11 with digest `B842944AC1C41152`. Final audits retained edge reachability and the prohibited-defense invariant.

Later terrain/mutator steps changed some final checksums, as already established in P0. The settlement-time before/after checksums matched in every P1 spawn, so the planner/spawner did not overwrite generated terrain.

## Reproducible checks

Pure tests run in the repository-prescribed SDK container:

```text
docker run --rm -v "$PWD":/mod -v rimworld-nuget:/root/.nuget/packages \
  -w /mod/Source/RealisticBases.PlannerTests mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet run -c Release
```

The 12 tests cover stable seeds, copied snapshots, deterministic regions, edge metadata, identical plan digests, disconnected land, narrow corridors, obstructed interaction cells, impossible capacity, water rejection, bounded termination, and no snapshot mutation.

For runtime checks, launch the isolated Core or GL profile with `-quicktest`, use **Generate playable hostile P0 settlement**, and then invoke **Show current P1 planning overlay** and **Log current P1 generation report**. Arm the existing pre-spawn and partial-failure actions for recovery cases. The overlay labels planned power and defense explicitly; it does not turn those reservations into functioning systems.

## Remaining limitations and handoff

P1 uses one rectangular compound archetype with two rectangular room envelopes. The model permits other outlines and circulation graphs, but those belong to later authorized work. Capacity is geometric planned capacity; supply is explicitly `not-modeled-p1`. Roof support, power, and defense sectors are reservations only. No P2 facilities, economy, material budget, furnishings, or functional networks are implemented.

The P0 lifecycle simplifications remain unchanged: coarse depletion/damage persistence, regenerated defenders on re-entry, and deterministic unload/reopen do not certify P5 identity/casualty persistence or literal caravan behavior. P2 must preserve the immutable planning boundary, bounded searches, stable digests, final-map audit, recovery semantics, guarded generator selection, and hard no-trap/no-IED invariant.

## Exit decision

The P1 exit gate passed: fixed inputs repeat the same digest; invalid terrain and impossible capacity reject; candidate and route searches have explicit deterministic bounds; all required property cases pass; snapshot digests remain unchanged; spawning consumes only accepted plans; and Core plus optional-mod integration/recovery paths were observed live. P2 remains unstarted.
