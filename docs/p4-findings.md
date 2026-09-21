# P4 terrain variety and stronger fortifications: findings and validation

2026-09-21 · Status: P4 in progress; P5/P6 unstarted

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
- Fortification layers are one through three physical wall rings, with coherent
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
| P4.2 | Foothill and excavated-mountain physical plans/spawning with explicit mutation footprints, roof support, natural-boundary preservation, and P3 geometry adapters | 50/50 combined pure tests and Docker Release build 0 warnings/errors. The mountainous harness has twice safely rejected unfit full footprints with `mutationCount=0`; an accepted mountain/foothill and partial-recovery live case is still required before closure. | In progress |
| P4.3 | Lakeshore, peninsula, and supported-island plans with legal approach/bridge/gate/circulation validation | Versioned water-preservation footprint and dry-route pure case added (50/50). Core water and rejected inaccessible/tiny/narrow cases remain required. | In progress |
| P4.4 | Layered fortifications, palettes/import budgets, climate alternatives, P3/save-reload regression, Core + Geological Landforms matrix, visual review, and exit decision | Actual final-map/path/roof/utility/combat/recovery observations, separate from estimates/screenshots | Pending |

Source analysis, pure tests, build/static checks, and in-game observations remain
separate evidence. A candidate label, screenshot, checksum, or optional-mod load
line is never proof of traversal, roof support, a functioning gate, or safe P3
combat behavior.

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
that (`compound_cell_unusable`, `mutationCount=0`), so they are rejection evidence,
not a claim of mountain support.

## P4.3 water contract

Lakeshore plans now record the exact adjacent water cells in their immutable
footprint. At least eight captured water boundary cells are required, and attack and
retreat routes remain entirely dry. The spawner never creates a bridge or changes
those cells. Interior islands remain rejected unless a real, pre-existing dry route
to the edge is captured; P4 does not fabricate one.

## Next action

Find and validate a real Core mountain/foothill map, including pre-spawn and deep
partial recovery. No P5/P6 work enters this step.
