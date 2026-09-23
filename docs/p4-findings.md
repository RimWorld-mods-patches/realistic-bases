# P4 terrain variety and stronger fortifications: findings and validation

2026-09-23 · Status: P4 and P4A integration in progress; P5/P6 unstarted

The 2026-09-23 direction makes the P4A procedural generator the vehicle for all
remaining P4 work. Earlier fixed-compound findings below remain historical
evidence. The prior instruction to finish a separate fixed-compound P4 matrix and
defer P4A until P4 closes is superseded. P4 exit requires the procedural path to
pass its own terrain, fortification, pathing, recovery, and live matrix.

## 2026-09-24 scope split

P4B now owns coordinated defensive layout: additional exterior-wall layers, material/reinforcement allocation for them, firing/cover/turret placement and operational validation. See [P4B plan](p4b-defense-plan.md). P4/P4A retain terrain, environmental adaptation, baseline enclosure/gates, facility circulation, recovery and reservations/interfaces for P4B. Older scope/checklist text below is historical where it assigns these defenses to P4. In particular, gatehouse bands are not proof of complete exterior-wall layering; no prior observation is relabelled as P4B evidence.

## Scope and boundaries

P4 extends the completed P0–P3 supplied encounter with terrain-adapted settlement
archetypes and one-to-three physical wall layers. It must use generated terrain as
the authority: foothills, excavated mountain footholds, lakeshores, peninsulas, and
viable islands are candidates only when their actual terrain, roof, support,
approach, and capability constraints pass. It must not fabricate water, flatten a
landform, or assume a terrain label is enough to make an archetype viable.

P4 may explicitly mutate planned construction, excavation, and roof cells. Each
such plan must list its mutation footprint; validation and recovery distinguish
those expected changes from collateral terrain damage and later mod mutations.
Terrain outside that footprint remains preserved. Every path must still contain no
traps or IEDs.

P4 keeps P3's bounded encounter horizon, finite physical supplies, player-scope
guards, and local combat containment. It changes P3 posts and defended routes only
through valid archetype geometry; a rectangle must not become a shortcut around an
irregular mountain or causeway compound. Trade-stock ownership, revisit/casualty
persistence, long-term sustainability, specialized tactics, P5, and P6 are out of
scope.

## Fixed invariants

- Planning is snapshot-only, versioned, seeded, deterministic, and bounded. A
  rejected candidate leaves the map untouched.
- A candidate must have legal occupied cells, interaction cells, resident/support
  routes, gates, a player-edge arrival/retreat route, and usable firing positions.
  Deep water, natural rock, and ordinary walls are never assumed passable or
  shoot-through.
- Mountain plans keep natural boundaries except inside the declared excavation and
  roof footprint. Every claimed roof has support and every outdoor utility/mortar
  court is unroofed, standable, and reachable.
- Lakeshore, peninsula, and island plans require an observed viable approach;
  incompatible tiny, inaccessible, or narrow-pass candidates reject or use a
  terrain-preserving valid fallback. Bridges are constructed only where the actual
  terrain and RimWorld placement rules permit them.
- Fortification layers are one through three physical wall rings or gatehouse bands, with coherent
  faction/local-material palettes, bounded imports/reinforcement budgets, and
  multiple functioning gates where the archetype supports them. No traps or IEDs.
- Climate alternatives must be profile-capability legal and backed by real P2/P3
  finite reserves/utilities; tribal profiles receive no industrial substitute.
- P3 posts, supply routes, relief routes, mortar courts, and pursuit containment
  are recalculated for actual archetype geometry. No unrelated map or player AI is
  affected.

## Checklist and evidence policy

| Step | Implementation | Required evidence before commit | Status / commit |
|---|---|---|---|
| P4.0 | Scope, archetype contract, terrain-mutation/recovery ledger, matrix, and boundaries | Docs agree with P0–P3 evidence and leave P5/P6 unstarted | Complete — this checkpoint |
| P4.1 | Immutable terrain analysis, versioned archetype inputs, deterministic bounded candidate/rejection model | 47/47 combined pure tests; Docker Release build 0 warnings/errors; same-input/rejection/landform/snapshot cases passed | Complete — this checkpoint |
| P4.2 | Foothill and excavated-mountain physical plans/spawning with explicit mutation footprints, roof support, natural-boundary preservation, and P3 geometry adapters | Pure/build suite passed. Core 160×160 excavated-mountain spawn and deep recovery passed; Core foothill now also passed with 12/12 reachable facilities, two-layer gates, P3, and a playable arrival. | Complete |
| P4.3 | Lakeshore, peninsula, and supported-island plans with legal approach/bridge/gate/circulation validation | Versioned water-preservation footprint and dry-route pure case added (50/50). Core water and rejected inaccessible/tiny/narrow cases remain required. | In progress |
| P4.4 | Layered fortifications, palettes/import budgets, climate alternatives, P3/save-reload regression, Core + Geological Landforms matrix, visual review, and exit decision | Actual final-map/path/roof/utility/combat/recovery observations, separate from estimates/screenshots | Pending |

Source analysis, pure tests, build/static checks, and in-game observations remain
separate evidence. A candidate label, screenshot, checksum, or optional-mod load
line is never proof of traversal, roof support, a functioning gate, or safe P3
combat behavior.

The resident-circulation correction is specified in [P4 connectivity](p4-connectivity.md).

## P4A procedural settlement layouts — active integration

The completed P2 layout is a fixed 31×25 prototype: its room offsets, dimensions,
and gate positions are hardcoded. Its prior runtime evidence remains valid evidence
for spawning, supplies, rollback, power, and defensive behavior, but it is not
evidence of a procedural settlement generator. P4 cannot close by translating,
rotating, recoloring, or choosing among fixed compound templates.

P4A will derive facility demand and capacity from the population and capability
profile, generate a seeded adjacency/circulation graph, and use a bounded packing
search over usable terrain. The room envelopes may remain rectangular and reuse
small furnishing patterns, but their positions and dimensions, district/corridor
arrangement, enclosing wall, gate positions, and each room portal must be planner
outputs. Packing, circulation, enclosure, and one-to-three-layer gatehouse
crossings are solved and repaired or rejected together. A fixed compact prototype
may remain only as an explicit exceptional fallback; its use is logged so it cannot
mask the normal generator.

P4 and P4A now share one runtime generator. P4A proof will
require deterministic replay; several fixed-terrain seeds
whose internal layouts differ beyond translation/rotation; changed population or
facility demand that coherently changes capacities and geometry; terrain that
changes the selected layout rather than only relocating/rejecting the same
rectangle; all room/job access connected through each supported wall thickness;
and representative in-game visual, pathing, defense, and recovery regressions.

## Required live matrix

| Terrain/capability case | Required assertion |
|---|---|
| Core foothill/mountain, industrial and tribal where supported | accepted plan or explicit safe rejection; natural boundary/roof/mortar/utilities/gates/paths audited |
| Core lakeshore/peninsula | approach, retreat, gates, multilayer circulation, outdoor court, and water preservation verified |
| Core viable island | actual legal approach/causeway or bridge, supplies/posts and edge route verified; no water assumptions |
| Core tiny/inaccessible island and narrow pass | deterministic rejection/fallback before mutation; playable arrival remains valid |
| Core cold/arid or scarce-resource profile | capability-legal food/power alternative and bounded reserve assumptions audited |
| Geological Landforms 1.7.13.1 + Map Preview | generated output is used first; geometry-affecting terrain/passability/late mutations recorded; accepted and rejected cases remain safe |
| Changed archetype P3 regression | usable posts and real firing opportunities, bounded routes, no player control, active save/reload, breach/relief/fallback continuity |
| Recovery | forced pre-spawn and deep partial failure restore P4-owned terrain/rock/roof mutations and preserve unowned landform output |

## Initial source observations

P1's `TerrainSnapshot` already carries per-cell traits and stable digesting; P2's
`PhysicalSettlementPlanner` is intentionally a fixed rectangle and P3 derives its
defended rectangle/posts from that compound. P4 therefore cannot safely be an
ad-hoc spawning override: it needs an immutable archetype geometry contract and
validators before changing the P2 layout/spawner. P0’s final audit established that
later vanilla/optional-mod steps can change final terrain after the settlement step,
so P4 audits must compare expected mutation footprints rather than claim a whole-map
checksum is unchanged.

The supported runtime baseline is RimWorld 1.6.4871 rev597, with Core and a prior
compatibility observation using Geological Landforms 1.7.13.1 plus Map Preview.
Those P0/P3 observations establish ordering/coexistence only; each P4 geometry must
be tested afresh.

## P4.1 immutable terrain analysis

`TerrainArchetypePlanner` is a pure, versioned pre-layout classifier. It examines
only the immutable `TerrainSnapshot` and a capability profile, uses deterministic
edge-connected regions, and returns a candidate with its observed land, buildable,
fertile, natural-rock, roofed-rock, and water-boundary counts. It recognizes flat,
foothill, excavated-mountain, and lakeshore evidence but does not yet mutate a map
or claim that an archetype can spawn; P4.2/P4.3 must supply the corresponding
physical geometry and runtime validation.

An isolated land region is explicitly rejected as
`island_requires_verified_approach`. The model does not infer a bridge or water
passability. Inadequate buildable/fertile capacity likewise rejects before a layout
or map mutation. Region consideration is capped by the input maximum and the
snapshot digest remains unchanged.

Four added pure cases passed with the prior suite (47/47 total): deterministic
selection/snapshot preservation; foothill, roofed-rock mountain, and lakeshore
classification; inaccessible-island rejection without an assumed bridge; and
bounded insufficient-land rejection. The Docker Release build passed with zero
warnings/errors.

## Gatehouse connectivity checkpoint

`WallLayers` now reserves only five-cell external gatehouse bands rather than a
sealed inset ring. Each additional north/south band has four walls, one faction
door, and a one-cell vestibule; the finished public graph must cross every door in
serial order to reach its one interior root. The plan validator requires the exact
gatehouse geometry, and the spawner/audit treat all primary and reinforced doors as
real structures. Pure tests cover one, two, and three layers plus a blocked
vestibule and offset-door rejection. This does not yet prove an in-game multilayer
run.

## Core foothill and layered-gate runtime checkpoint

On 2026-09-23, the isolated Core disposable profile generated a playable foothill
settlement after arming two P4 wall layers. The authoritative log recorded
`archetype=Foothill`, `gateFactionDoors=4/4`, `facilitiesReachable=12/12`, valid
attack/retreat topology, 298/298 roofed facility cells, no prohibited defenses,
and a pass-doors player arrival from the edge. P3 initialized eleven posts and a
mortar without controlling player pawns. After saving and reloading the active
encounter, the controller restored `HoldSectors`, four defenders, four assignments,
and twelve HE shells. This is Core evidence only; it does not close the remaining
lakeshore/island, climate, or GL matrix.

## Next action

P4.2 now has a versioned `TerrainMutationFootprint`: each excavation is captured
roofed natural rock, each roof clearance is owned in the same ledger, and adjacent
non-owned rock is preserved. The spawner snapshots and restores natural-rock
edifices and roofs on rollback, verifies `RoofCollapseUtility.WithinRangeOfRoofHolder`
before constructing a roof, and logs the outside-footprint natural-rock checksum.
P3 now persists exact controlled cells for terrain-following layouts; its rectangle
is only a compatibility diagnostic envelope. These are implementation/pure-test
results, not yet evidence that an actual mountain map passed.

The field is now chosen deterministically from the four adjacent cardinal plots,
instead of requiring the former east-only rectangle. This lets a foothill/mountain
candidate use legal fertile ground without flattening terrain. The debug-only
`Generate playable mountainous P4 settlement` action is a controlled test harness:
it requests a Core mountainous world tile but still lets the immutable snapshot
reject an unfit map before mutation. Two live mountain harness attempts did exactly
that (`compound_cell_unusable`, `mutationCount=0`), so they are rejection evidence.

The 160×160 Core mountain harness then supplied an accepted physical plan. The final
spawn recorded 187 excavated rocks, `unownedRockPreserved=true`, 12/12 reachable
facilities, two faction gates, valid attack/retreat topology, 298 roofed facility
cells, a P3 controller with eleven posts and a mortar, and a reachable playable
arrival. Visual inspection showed the expected dark mountain mass and preserved
outer rock; log/path/roof assertions remain authoritative.

The terrain-following P3 adapter selects every post and its unroofed heavy mortar
court from immutable open logistics cells instead of fixed exterior coordinates. On
the forced partial mountain spawn, recovery removed 477 owned things, restored 298
roofs and the one owned rock, preserved unowned rocks, and restored the original
terrain/roof checksum. The full-contract retry then spawned normally.

## P4.3 water contract

Lakeshore plans now record the exact adjacent water cells in their immutable
footprint. At least eight captured water boundary cells are required, and attack and
retreat routes remain entirely dry. The spawner never creates a bridge or changes
those cells. Interior islands remain rejected unless a real, pre-existing dry route
to the edge is captured; P4 does not fabricate one.

## Next action

Validate the remaining real Core foothill, lakeshore, and P4 matrix cases. P4A's
bounded procedural-layout solver is explicitly deferred until separately authorized;
P5/P6 remain unstarted.

## 2026-09-23 procedural runtime supersession

The preceding fixed-compound checkpoints remain historical evidence, not acceptance
evidence for the new P4/P4A generator. The user authorized P4 and P4A together;
`ProceduralLayout` now drives ordinary settlement generation. The live Core mountain
run produced nine rooms, two gates, one fortification layer, four residents, 124
crops, 208 excavated rocks, 117 retained thick-roof cells, and 128 conduits. Its
terrain/roof checksum at settlement spawn matched final map generation; player
arrival crossed the doors, and P3 kept four defenders assigned without controlling
player pawns. Generator startup fuel was capped at 45, leaving 153 in storage.

Conduit routing explicitly excludes intact natural rock and checks vanilla conduit
terrain buildability. The successful mountain route used the excavated path; it
did not require polishing rock. This does not establish that a conduit can be
built through an intact stone wall, nor does it claim to support such a route.

Settlement and power steps moved to orders 1400/1401, after vanilla map contents
that previously changed the final terrain/roof checksum. The power step refreshes
networks but no longer calls vanilla automatic power generation, which added
unowned structures after the transactional spawn.

The new transaction also passed its own forced-partial Core mountain test. It
removed 329 owned things, reported recovery, restored the pre-spawn terrain/roof
checksum, and then succeeded on the same map: nine rooms, one gate, four residents,
276 excavations, 235 retained thick-roof cells, 137 conduits, final checksum
unchanged, and playable arrival. The old mountain forced-failure recovery above
tested the fixed-compound path and is separate evidence.

At this checkpoint, remaining P4/P4A exit evidence includes procedural
lakeshore maps, hot/cold and tribal cases, and GL plus Map Preview. P5/P6 are
unstarted.

## Cooler integration and procedural save/reload

The cooler-exhaust commits `fc79176`–`9bc9d1d` orient wall coolers by their
vanilla hot/cold sides and add sealed roof-open exhaust pockets when a mountain
hallway is not an outdoor-temperature room. The current build, P1–P4 tests, and
P4A layout suite pass. A new spawn-time audit checks RimWorld's actual `Room`
objects: every cooler's cold side must be in a sealed served room and its hot
side in a distinct room using outdoor temperature.

Disposable Core live runs passed this audit for a mountain with two coolers, a
SmallHills foothill with four, and a three-layer SmallHills foothill with three.
Each run also passed final terrain/roof preservation and playable arrival. The
three-layer encounter survived save/reload with four defenders and four
assignments restored on each of its three test maps; a manual post-reload audit
of the active map showed four alive/assigned, two occupied posts, zero defenders
outside bounds, and zero player pawns controlled.

These runs close the current cooler regression and provide Core procedural
foothill/three-layer and reload evidence. Lakeshore, hot/cold, tribal,
and GL+Map Preview procedural cases still require focused live checks.

## Reachable exterior fields

Procedural crop selection now floods dry, standable exterior terrain from the
gate staging cells before selecting fertile tiles. Water, intact rock, existing
edifices, the enclosure, gatehouse, and exhaust-pocket structures cannot be
crossed; the staging cells themselves are not planted. A pure regression rejects
fertile land across a water barrier and reserved crop cells. A disposable Core
mountain still spawned all 124 required crops with one live outdoor cooler,
preserved the final terrain/roof checksum, and admitted the player arrival.

## Flat-map search and owned boulder clearance

Two visibly open flat maps initially rejected before mutation. The corrected
diagnostics exposed planner failures instead of hiding them behind six equipment
notes: one site had no contiguous placement component after the three-cell
obstacle margin; another exhausted candidate room placement. Scattered thick-roof
counts had incorrectly selected a mountain strategy, and the global site budget
could be consumed before open-ground fallback. The planner now requires a
contiguous thick-roof mass for that strategy, reserves bounded search slots for
fallback, ranks open anchors by clearance, and permits explicitly owned
excavation of unroofed or thin-roofed natural rock in an open-ground layout.
Thick-roof mountain excavation remains in the protected strategy. The
mutation/rollback ledger snapshots removed rock and roof cells.

A clean, single-process disposable Core flat run then accepted an open-ground
layout: nine rooms, two gates, 101 owned rock removals, zero retained thick
roofs, 124 crops, five coolers with actual outdoor exhaust rooms, and four
defenders with a mortar. The final terrain/roof checksum matched settlement
spawn and player arrival crossed the doors. The pure suite includes a
scattered-rock regression. Earlier overlapping-process flat results were
discarded; this run started after verifying exactly one game process used the
disposable profile.

A separate single-process forced-partial run on a `Hilliness.Flat` world tile
restored 334 owned things and the original terrain/roof checksum, then succeeded
on the same map with final checksum and arrival checks. That tile contained a
contiguous rock mass, and the accepted layout chose `RockProtected` with 196
retained thick-roof cells. It therefore does **not** yet prove rollback for the
new open-ground boulder-clearance subpath.

## Flat clutter regression (pure planner)

One further single-process flat world still rejected despite ample visible land:
the planner ended at `room_unplaceable` after 30 attempts, and another sampled
site failed while closing its perimeter. A new deterministic 160×160 synthetic
fixture models clustered natural-rock outcrops and sparse non-rock edifices at
roughly the observed live-map densities. It reproduced the failure without
launching RimWorld. The cause was twofold: the placement mask expanded every
occupied cell by the full fortification setback, and enclosure construction
rejected when its perimeter touched an existing edifice.

The setback now expands from terrain hazards and the map edge, while occupied
cells remain individually unplaceable. Enclosure construction absorbs nearby
existing edifices inside the compound so its built perimeter does not collide
with them. All four synthetic clutter variants now produce accepted layouts,
and the test asserts no built wall occupies an existing edifice. The full pure
layout suite and Release build pass. This is planner-level regression evidence;
the previously failing live tile has not yet been retested, so flat-map runtime
robustness is not closed by this test alone.
