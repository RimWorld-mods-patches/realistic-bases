# Plan: unified defense posts and threat-arc cover (before P5B)

2026-10-13 · Status: **C1–C4 implemented** on branch `claude/defense-posts-cover`;
**C5 mostly done**: flat, save/reload, a one-side probe, tribal and the P4D
bandit quest pass live. The GL smoke test, the supported rescue variant and
a live mountain site remain. Design agreed in the
2026-10-13 brainstorm.
Base: `main` `850beb8` (P5 and P5A merged). This checkpoint precedes P5B
([implementation plan](../implementation-plan.md#p5b--better-management));
P5B's threat-state staffing reads the post inventory built here.

## Goal

Defenders get too little cover, and it has the wrong shape. Today two fixed
templates exist: gate-side posts (`P4BPerimeterPost`, a 4-cell forward row,
one flank cell and a wall return) and field-wall posts (`P4BPostAccessPlan`
with its own screens). Posts should be one kind, wrapped in cover on every
side an attacker can shoot from, and every exposed side should be able to
hold the whole ranged defender pack. P5B then moves the pack to whichever
side is attacked.

## Agreed decisions

| Topic | Decision |
|---|---|
| Problem | Too little cover and the wrong shape. The material choice and the cover locations are not the issue. |
| Post model | One `P4BPost` type. Field-wall and gate/perimeter posts become anchors in one planner (approach A). |
| Shape | A **nest per threat arc**: each standing slot is covered on every threat-facing neighbour, front and diagonal flanks included. |
| Capacity | Every exposed side has covered slots for the **whole ranged pack**. The mobile reserve is excluded for now. Corner posts count for both sides. Turrets do not count. |
| Shortfall | Take what fits and log `side_capacity_short:<side>=n/pack`. Never reject the base. |
| Budget | Cover built from local construction stuff is free, with no import charge. Barricade first; sandbags as a fallback, charged only when their fabric is imported. |
| Timing | Fitted after room packing, as today. Early reservation regressed the flat corpus from 12/12 to 6/12 ([P4B findings](../p4b-findings.md)). |
| Turrets | They keep their own nests in `P4BTurretPlanner` for now. Posts and turret cover block each other. |
| Order | Cover first, then P5B. |

## Design

### Data model

`P4BPost`: anchor wall cell, kind (field wall / perimeter / gate band),
access gate or sally port, access route, ordered slots, cover cells, an
optional wall return (opaque) and the facing side ids. A post faces a side
when it engages at least 4 of that side's samples, the existing rule.

- `P4BApproachDefensePlan.Posts` is the single post inventory, read by
  the turret planner, P3 and P5B.
- `P4BFieldWallPlan` keeps the field, its walls, gates and arrival route.
  Its `PostAccess`, `Screens` and `CoverCells` move to the shared posts.
- The digest tag becomes `p4b-approach-defense-v2`. `RecoverLegacySlots`
  and the post-load migration stay for existing saves.

### CoverNest builder

Input: the anchor, the slot count requested (1–4), the facing sides with
their samples, occupancy (rooms, walls, fixtures, routes, arrival,
reinforcement lanes, reserved cells) and terrain.

1. A neighbour octant of a slot is **threat-facing** when it points toward
   at least one sample the slot can engage,
   `P4BFireLane.CanEngage(slot, sample, 8, 25, opaque)`.
2. The first slot is the anchor's forward cell. Further slots grow along
   the wall tangent, alternating left and right, in one row so that
   neighbouring slots share cover.
3. The required cover is every threat-facing neighbour of every slot,
   except walls, other slots and the access route.
4. If any required cell is blocked, drop the outermost slot and retry.
   Blocked means not buildable, occupied, on an arrival or route cell, or
   in a reinforcement lane. If the anchor slot alone cannot be covered,
   reject the anchor.
5. Validate the result:
   - every slot is reachable from the access gate along a route behind
     the slots, and that route never crosses cover;
   - every slot still engages at least 4 samples (cover is not opaque);
   - the wall return is kept only on a flank with no threat.
6. The builder is deterministic: fixed anchor order, fixed growth order and
   no randomness.

Expect about 3 cover cells per slot on a straight front.

### Selection per exposed side

- `pack` is the number of funded ranged defenders.
- Greedily add the candidate with the most new useful slots for this side,
  until the side's slots reach `pack` or it runs out of anchors.
- Posts that face two sides (corner posts) are tried first.
- Marginal coverage breaks ties, so posts spread along the front.
- At most 4 posts per side.
- Reserve each accepted post's cover and route immediately.

### Spawning and runtime

- `P4ProceduralSpawner` places cover objects and wall returns in the same
  transaction. The audit checks every planned cover cell, and rollback is
  unchanged.
- `P3Doctrine`/`P3Garrison` turn each slot into one `DefensivePost`:
  - `SupportedSectors` = the facing sides;
  - `AccessGate` = the post's access gate;
  - `HasCover` = every threat-facing neighbour of the slot is covered.
- Until P5B, assignment across sides follows today's doctrine.
- Diagnostics: `side_capacity:<side>=<slots>/<pack>`,
  `post:<id> slots=<n> cover=<n> facing=<sides>`, `nest_shrunk:<reason>`.
- Settlements already on the map keep their stored posts. A layout
  regenerated on a revisit gets new posts, handled by P5's matching by
  definition.

## Steps

### C1 — Post model and CoverNest (pure)
- [x] Add `P4BPost` and a pure `P4BCoverNest` builder in `Planning/`.
- [x] Unit fixtures:
  - [x] straight front gets a full wraparound;
  - [x] corner post facing two sides;
  - [x] one-sided threat keeps its wall return;
  - [x] a blocked flank cell shrinks the nest;
  - [x] an anchor that cannot be covered is rejected;
  - [x] the route behind the slots never crosses cover.

### C2 — Unified anchors and per-side selection
- [x] Generate anchors from gate and sally-port positions and from
      field-wall faces.
- [x] Remove the fixed templates in `P4BApproachDefense` (~L320–340) and
      the post and screen code in `P4BFieldWallPlanner`.
- [x] Per-side capacity selection loop and capacity diagnostics.
- [x] Local-stuff cover is free in `Materials`/`CoverImportValue`.
- [x] Update the validator (`P4BApproachDefensePlanner.Validate`) and
      `ProceduralLayoutValidator` checks.

### C3 — Consumers
- [x] `P4BTurretPlanner`: block on the new post cells, and remove its reads
      of `fieldWall.CoverCells` and `CoverDefName`.
- [x] `P4ProceduralSpawner`: spawn and audit the posts (~L385, ~L796).
- [x] `P3Doctrine`/`P3Garrison`: one post per slot, plus `SupportedSectors`
      and per-slot `HasCover`.
- [x] `P4DQuestGeneration` (L241): cover cells from the new inventory.
- [x] Keep the legacy save recovery working.

### C4 — Tests and regression
- [x] Capacity fixtures:
  - [x] flat open terrain with 4/7/12 residents reaches the full pack on
        every exposed side;
  - [x] a mountain-backed unexposed side gets no posts;
  - [x] a cramped side comes out short but is accepted.
- [x] Update the existing turret, support and field-wall tests to the
      shared posts.
- [x] Same seed gives the same digest.
- [x] Add slots (`@`) and cover (`b`) to the ASCII dump (`s`/`B` were taken);
      `docs/p4a-examples` refreshed with `posts-*` and `v2-clusters-*` exports.
- [x] Gates: full LayoutTests, PlannerTests, V2 matrix (now 120/120, one
      engine), high-industrial flat clutter 12/12, Release build clean.

### C5 — Live checks (short only)
- [x] Core IndustrialMid flat: generation succeeds, capacity log and
      save/reload keeps the posts. The nest screenshot was not taken
      (screen capture is unavailable to the agent); the measured
      per-slot cover audit stands in for it.
- [x] Raid from one side: defenders take covered slots and the per-slot
      `HasCover` holds.
- [x] Tribal settlement: wood or stone barricades.
- [ ] Mountain foothill: an unexposed side gets no posts (pure fixture only).
- [ ] P4D rescue/bandit regression and a Geological Landforms smoke test
      (bandit passed; supported rescue variant and GL not run).
- [x] Update `p4b-findings.md`, `progress.md` and the implementation plan.

Long three-day horizons stay deferred to post-release live testing.

## Then: P5B

To be designed in detail when reached:
- **Threat states.** For a caravan that does not attack, a reduced watch
  on the likely side and routine for everyone else. For an assault,
  everyone is alarmed, sleepers included, and the whole ranged pack goes
  to the attacked side.
- **Redeploy.** The pack moves when the attacked side changes.
- **Sleep rotation.** Defenders do not all sleep at the same time.
- **Odd walk.** Fix the walk to the "internal post" when a defender
  changes activity.

## Done when

- Every exposed side has covered slots for the whole ranged pack, or a
  logged reason why it falls short.
- Cover wraps every threat-facing neighbour of every slot.
- Field-wall and gate posts are one type.
- Acceptance does not regress.
