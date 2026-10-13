# Plan: layout engine v2 (hallway tree, clusters, shared walls)

2026-10-12 · Status: **V2.0–V2.5 implemented** on branch `claude/layout-engine-v2`
(from `main` `a8a0b48`, which includes P4E and the P5 encounter-stock work);
clusters is the default engine. **V2.6 live checks pending.** Design agreed in
the 2026-10-12 brainstorm.

**2026-10-13:** the legacy engine and its comparison tests were removed, along
with the `v2-legacy-*` examples. Clusters is the only engine. Engines are still
injected through `ProceduralLayoutInput` (`ILayoutEngine`), and the default
input digest is unchanged. Legacy mentions below are historical.

## Goal

Settlements should read as player-built and stay functional, and planning
should stop discarding usable layouts. Today's layouts are free-standing room
boxes on streets, ringed by a public yard (`docs/p4a-examples/`). Late stages
veto finished layouts, and each new feature (labs, magazines, ventilation)
becomes another veto pass.

## Agreed decisions

| Topic | Decision |
|---|---|
| Scope | A new core generator (option B) behind the unchanged `ProceduralLayout` contract. The spawner, P3, P4B, P4C, P4D, P4E and P5 consume it as today. |
| Styles | **Superstructure**, **town** and **mountain**, all supported from the start through one `StyleProfile`, selected from tech stage and terrain. The default switches only when all three pass. |
| Growth order | Organic inside-out growth with a light upfront reservation: approach front, gate side, and a defense buffer that widens into a forecourt on the primary front. |
| Generator | Corridor-tree accretion. A hallway tree grows from the gate side. Room **clusters** (food, production, living, medical, jail, power, magazine) are packed along hallway frontage by a slicing packer. Rooms inside a cluster share walls in every style. |
| Furnishing and cooling | Reuse `LayoutFurnishing.Furnish`, the cooler hot/cold model, pockets and `LayoutRoomPartition` unchanged. Exhaust chain in every style: exterior wall → shared vent shaft (pocket) → new pocket. Cooled rooms go at cluster ends or outer edges first. |
| Rejections | A degradation ladder plus 3–4 scored candidates per site. Reject only on hard invariants. Every downgrade is logged. |
| Defense | Reuse the P4B planners on reserved space. One concentrated battery on the primary front plus central staging (defense-sim findings 2, 4, 5). No traps or IEDs. |
| Reuse | P5 code connects to generation only through `P4ProceduralSpawner` and `P4BTurretPlanner`, so it is unchanged. The P4E lab, mortar magazine and work-room climate rules carry over into clusters, and the P4E tests and fixtures stay as gates. |

## Architecture as implemented

Planning stays pure and deterministic. The back half of `LayoutAttempt` is
already a working pipeline over one occupancy grid, which is the `PlanState`
of the design: yards → power → mortar courts → trim → enclose (gates, layered
sections, full perimeter, roof support, materials, cooler partition check).
The planner loop then adds field wall → defense access → approach defense →
turrets. v2 replaces only the front half (spine, branches, room placement)
and adds candidate selection. Extracting the back half into separate stage
classes is deferred until a second consumer needs it.

```
Planning/
  LayoutStyle.cs             StyleProfile presets + deterministic selection, LayoutEngine switch
  LayoutClusters.cs          cluster catalog: groups LayoutRoomDemand into ordered clusters
  LayoutHallways.cs          (partial LayoutAttempt) reserve front, hallway tree, lazy branch growth
  LayoutClusterPacking.cs    (partial LayoutAttempt) slicing packer with shared walls; reuses Evaluate/Commit
  LayoutCandidateScore.cs    final-plan score: compactness, routes, shared walls, downgrades, defense
  ProceduralLayoutPlanner.cs Plan(): engine dispatch, candidate pool, downgrade log
  ProceduralLayoutValidator  shared room walls legal; overlap anywhere else still rejects
  LayoutEquipment.Count      shared wall cells charged once
```

`ProceduralLayoutInput` gains `Engine` (`Legacy` | `Clusters`), included in
its digest. Legacy stays reachable for comparison and for previously
generated plans. Saved settlements keep their plan; the planner version is
bumped when the default switches.

### Shared-wall contract

Two rooms may share a cell only when it is an edge cell of both. It must not
be either room's door or a wall cooler, and it must not be the hot cell of
any cooler. Interiors never overlap. Effects on consumers:

- The spawner and partition already use sets, so they are unchanged.
- The materials ledger deduplicates wall cells.
- The validator's `room_overlap_*` check becomes "overlap outside mutual walls".

Internal doors between rooms are out of scope for v2: every room keeps one
door onto public circulation, which the validator's reachability rules
require.

### Style profiles

| Parameter | Superstructure | Town | Mountain |
|---|---|---|---|
| Selection | Electric stage on open ground | Non-electric stage, or ladder fallback | Thick-rock cluster ≥ demand/2 (today's `RockProtected` test) |
| Main hallway | 2 wide | 1–2 wide outdoor path | 1–2 wide tunnel, excavation cost |
| Cluster joining | Clusters share walls with neighbours and back onto each other across blocks | Clusters are detached buildings with 1–3-cell gaps; rooms within a cluster share walls | Carved chambers; rock or built shared walls |
| Branch spacing | Two cluster depths, so blocks back onto each other | Cluster depth + gap | Cluster depth, rock between chambers |
| Exhaust | Exterior wall → vent shaft → pocket | Exterior wall (usually) | Vent shaft → pocket with roof hole (existing budget) |

### Clusters

| Cluster | Rooms (packing order) | Rule |
|---|---|---|
| Food | freezer/pantry → kitchen → dining | Freezer at the cluster end (exterior walls for exhaust); kitchen–food hard relation unchanged |
| Production | storage → workshop(s) → laboratory | Near the hallway centre |
| Living | barracks/bedrooms | Away from the front; bedroom rows share side walls |
| Care | medical, jail | Medical central; jail away from housing |
| Power | power room | Placed after yards, unchanged, beside production when possible |
| Magazine / court | existing P4E `PlaceCourt`/`FindMagazine` | Unchanged; separation from sensitive rooms kept |

### Degradation ladder (order)

Already present and kept:
1. Optional turret network.
2. Defense-access frontages.
3. Mortar courts (dropped one at a time).
4. Wall layers 3→2→1 (integration loop in `P4AMapPlanning`).

Added by v2, each logged as a `downgrade` diagnostic:
5. Cluster split: pack a cluster's rooms separately along any frontage.
6. Style fallback: superstructure → town spacing.
7. Legacy engine as a last resort for the same input, reported as `engine_fallback`. Its frequency is a tuning signal, never a success.

**Hard floor** (never downgraded): every demanded room and capacity, beds for
all residents, connected gates, reachability, valid coolers, roof support,
material budget, no traps.

### Candidate scoring

After the first valid layout at a site, keep planning up to `K = 3` valid
candidates (bounded attempts) and return the lowest score:

- **Compactness:** enclosure cells ÷ room cells.
- **Shared walls:** a bonus for wall cells shared by two rooms.
- **Routes:** real public walking distances for food store→kitchen, food store→dining, storage→workshop, and housing→dining.
- **Downgrades:** a weighted count of ladder steps taken.

Evaluation work is bounded, and ties are broken by deterministic seed order.

## Checkpoints

Each checkpoint must pass three gates before its commit: the Docker Release
build (0 warnings, 0 errors), PlannerTests and the relevant LayoutTests
(the full suite before a closeout), and `git diff --check`.

| ID | Scope | Evidence |
|---|---|---|
| V2.0 | This plan | Documentation commit |
| V2.1 | Shared-wall contract: validator, ledger dedupe, ASCII export; `LayoutEngine` input switch | Unit tests: legal shared wall accepted; interior, door or cooler overlap rejected; ledger counts a shared wall once; legacy digests unchanged |
| V2.2 | Style profiles, cluster catalog, hallway tree and slicing packer in the `Clusters` engine | Plains industrial (superstructure), tribal (town) and mountain (rock) fixtures accepted with shared walls; all room/cooler/furnishing validator rules pass |
| V2.3 | Candidate pool, scoring, downgrade log, engine fallback | Determinism; candidates bounded; downgrade reasons reported |
| V2.4 | Comparison matrix, legacy vs clusters, same seeds and terrains (plains, clutter, obstacles, captured/P4E flat tiles, mountain, tribal, hot/cold) | Acceptance rate, fallback rate, compactness, shared-wall ratio, route metrics, time; regenerated `docs/p4a-examples` |
| V2.5 | Switch the default when all three styles beat legacy on acceptance with no validator regressions; bump the planner version | Full LayoutTests/PlannerTests; P4D quest camps (town style) pass |
| V2.6 | Live checks: superstructure, town and mountain spawn; coolers; P3 defense; save/reload; forced rollback | `run-rimworld-dev` evidence in findings |

## Results (pure planner evidence, 2026-10-12)

Synthetic terrain only; no live map was generated. Matrix: `--only "V2 matrix"`,
4 seeds × 18 cases, both engines on identical inputs. The cases cover funded
industrial and tribal rosters at wealth 60/100/180 with joint fields, hot and cold
climates, three wall layers, clutter, live density, obstacles, the captured flat
tile, the two P4E captured tiles, and tribal and industrial mountains.

| Measure | Legacy | Clusters |
|---|---|---|
| Valid accepted plans | 71/72 (`p4e-flat-cooler` seed 4: search budget exhausted) | **72/72** |
| Shared wall cells per base | 0 | 10–56 |
| Circulation overhead (non-room, non-yard enclosure cells per room cell) | 0.32–0.79 | 0.27–0.78 |
| Time per plan | baseline | about equal after pre-defense pooling (pooling after defense had tripled it) |

With clusters as the default, the whole existing LayoutTests suite passes
unchanged (126/126), including the P4D quest camps, P4B defense, P4E
facilities, ordinary population and determinism. PlannerTests and the Release
build are clean. `docs/p4a-examples/` were regenerated with the clusters engine.
`v2-legacy-*` and `v2-clusters-*` show the same inputs side by side.

Decisions made during implementation:

- **The planner version string is not bumped.** The engine is part of the input digest, settlement seeds are persisted, and explicit legacy inputs keep their historical digests (pinned by test). The runtime `PROCEDURAL_PLAN` log reports `engine=`.
- **The pool ranks validated room layouts before the P4B finishing passes,** then finishes the best first and falls back in score order. Defense work stays near one pass per accepted plan.
- **Overhead barely moved** because it is dominated by the enclosure setback ring and hull fill, not by room packing. Making a building's outer wall the perimeter would change the P4B perimeter contracts and is left for a later step.

Open follow-ups:

- **V2.6 live checks.**
- **Bent gate entry (design section 2):** not yet implemented; the gate is still chosen by the shared back half.
- **Town cohesion:** a town kitchen can land across the street from its pantry. Tune the cluster score with matrix evidence.
- **Test speed:** the long P4E defense-allowance sweep dominates suite time. Run the suite as `--shard i/n` processes on one build.

## Follow-up: natural-barrier siting and inner-yard crops (2026-10-12)

The user observed a beach tile where the base sat inland with every side open, and a walled courtyard left empty.

**Siting.** The joint field/compound score gains `3 ×` the cells of a ring just outside the whole site that attackers cannot reach from the map edge (`LayoutExposure`; `P4BJointSitePlanner.ProtectionWeight`).
- Deep water, natural rock and edifices block. Shallow water and clearable plants are walkable, so they give no credit. Cells beyond the map are not counted.
- The term applies to both engines: siting is shared.
- On the synthetic beach the envelope now backs onto the shore; on the lakeshore the chosen site has 143 protected ring cells against a candidate median of 118.
- The open-ground anchor ordering for non-joint sites is unchanged.

**Inner-yard crops.** After the P4B finishing passes, the clusters engine plants extra crops (`ProceduralLayout.BonusFields`, `LayoutBonusFields`) on enclosed, open-sky, fertile yard.
- Excluded: rooms, streets, door fronts, fixtures and their access cells, courts, power yards and wind zones, pockets, roofs, the area around each gate approach, and defense posts, routes, cover and turrets.
- The cells are eroded by 3×3, so a walkway remains around each patch, and only patches of at least 16 cells are kept.
- They are additive (never counted toward `FieldCellsRequired`) and kept only if the layout still validates. The validator checks them, and the spawner plants them like the main field except on conduit cells.
- Quest camps and legacy plans get none.

Evidence: LayoutTests 129/129 and PlannerTests pass; matrix clusters 80/80 (legacy 79/80), including the new beach and lakeshore cases. Pure planner evidence only.

## Out of scope for v2

Internal doors and room chains; roofed hallways (streets stay unroofed so the
public room keeps vanilla outdoor status); extracting back-half stage classes;
new room types; specialization (P7). P5A and P5B items are implemented on top
of v2 afterwards.
