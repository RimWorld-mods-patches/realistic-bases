# Plan: cooler orientation and outdoor exhaust

2026-09-23 · Status: **steps 1–4 implemented (pure) on branch `claude/cooler-exhaust`, on top of Codex's `codex/p4-finish` (`7aeaa71`). Step 5 is partly done: the spawner now builds pocket walls. The live game is not yet tested.** This plan extends the P4A planner (`docs/p4a-foundations.md`, `docs/plans/2026-09-23-tech-stage-equipment.md`). It covers pure planner work plus the runtime changes it needs.

## Problem (observed in game on the procedural path)

1. **Coolers face the wrong way.** The hot side points into the freezer.
2. **The hot side must exhaust into an outdoor space.** That means either outside the base wall, or an unroofed space inside the base: a small sealed pocket (1×1 is enough) with no door and no roof. Under rock, a cooled room therefore needs a way to reach unroofed space.

## Root cause and vanilla rules

- Vanilla `Building_Cooler.TickRare`:
  - **cold cell** = `Position + IntVec3.South.RotatedBy(rot)`
  - **hot cell** = `Position + IntVec3.North.RotatedBy(rot)`
  - Neither cell may be impassable. The rotation names the **hot** direction.
- The P4A bug: `LayoutFixture.Rotation` (`ProceduralLayoutPlanner.cs`) derives a `WallCooler`'s rotation from `Facing`, which is the cold direction, into the room. So the rotation is inverted. The benches' rotation was pinned against vanilla by a test, but the cooler's wasn't. Codex's `P4ProceduralSpawner` passes `fixture.Rotation` through, so the bug reached the game. The fixed P2 compound (`"West"` on the freezer's west wall) is correct.
- Vanilla `Room.UsesOutdoorTemperature`: true if the room `TouchesMapEdge`, or if `OpenRoofCount >= ceil(0.25 × CellCount)`. Doors separate rooms. A sealed, unroofed 1×1 pocket qualifies.
- The current P4A exhaust rule ("the hot cell is an Open, undug street cell") works only by luck. The walled street network is one room whose open fraction is never checked, and under rock that fraction can fall below 25%.
- The generator may clear thick mountain roof (`RoofRockThick`) through P4's roof-clearance ledger, even though players can't.

## Agreed decisions

| Topic | Decision |
|---|---|
| Rock roof holes | **Allowed but minimal.** Exhaust pockets under rock clear the thick roof over their cells (usually one per pocket), recorded as roof clearance. The rock-protect doctrine has a hard roof-hole budget (provisional: ceil(cooled rooms / 2)). |
| Approach | **A: outdoor-verified exhaust with a pocket fallback.** A cooler may sit on any straight wall segment of its room. Its hot cell must be in a room that is outdoors by vanilla's rule, in the final planned layout: either a qualifying public room, or a sealed, doorless, unroofed exhaust pocket, shared by back-to-back rooms when possible. |
| Validation | A pure `LayoutRoomPartition` reproduces vanilla room splitting and roof coverage on the final plan. The validator checks each cooler's hot and cold cells against it. |

## Design

**The fixture model.** A `WallCooler` stores `Cell` (the wall), `ColdCell` and `HotCell`. Rotation is derived from `HotCell − Cell` by the vanilla convention. `Facing` remains the cold direction and is used only for furnishing. Both cells must be passable, and the cold cell stays free.

**`LayoutRoomPartition` (pure).**
- Separators: built walls, room walls (except the door), natural and intact rock, cooler cells, doors and gates. Everything else is split into rooms by a cardinal flood.
- Roof per cell: a constructed roof over planned rooms, retained thick roof on dug cells, none on open streets, yards and pockets, and none on cleared pocket cells.
- Outdoor rule: touches the map edge, or unroofed cells ≥ ceil(25% of cells).
- It serves temperature logic only. Connectivity stays with `ProceduralLayoutValidator`.

**Placement, integrated with room placement.** For each room needing N coolers (freezers; living rooms in hot climates that are not fully under rock), try wall cells, scoring the exhaust options:
1. **Public exhaust** (cost 0): the hot cell is an unroofed, undug public street, plaza or yard cell. It is provisional until the partition check after enclosure.
2. **Shared pocket** (cost 1): the hot cell is an existing pocket that touches this room's wall.
3. **New pocket** (cost 3, +5 if roof clearing is needed): a free cell outside a straight wall segment, sealed on its other sides by existing walls or rock, other room walls, or new built walls. It has no door, is never on a street, and doesn't disconnect circulation. On open ground it is unroofed; under rock it takes a roof clearance within the budget.

Extra rules:
- Freezer coolers keep their hot cell away from the freezer door.
- A hot cell is never another room's interior, a walkway through a door, or another cooler's cold cell.
- Pocket cells are occupied, like court cells.

**After enclosure (the guaranteed correction).**
1. Build the partition.
2. If the public room fails the outdoor rule, convert its public-exhaust coolers to shared or new pockets, re-placing a cooler on another wall segment of the same room if needed.
3. If that fails, fail the attempt with `cooler_exhaust_not_outdoor` and continue the search.

**Ledger and roofs.**
- Pocket walls go into the material ledger with the stage's construction stuff.
- Cleared pocket roofs are recorded in a new `ProceduralLayout.ExhaustRoofClearance`. At runtime they map to P4's `TerrainMutationFootprint` roof-clearance category (owned, recorded and rolled back) and are excluded from retained roof.
- The roof-support check stays valid, because holders are intact rock, and it is re-run.

**Validator.**
- Vanilla hot and cold cells from the rotation; both passable.
- The cold cell is in the served room; the hot cell is in an outdoor room.
- Pockets are doorless, unroofed and sealed, with no street access.
- The roof-hole budget holds under the protect doctrine.
- The freezer is fully roofed with exactly one door.

## Implementation steps

Each step: Docker Release build 0/0, LayoutTests and PlannerTests passing, `git diff --check` clean, and one commit.

1. **Rotation hotfix.** Store `ColdCell` and `HotCell` on wall coolers and derive the rotation from the hot side.
   - Tests: all four rotations match vanilla hot and cold cells; freezer and hot-climate coolers put their cold cell inside the served room.
   - Ship this first. It is also the minimal fix for the observed bug.
2. **`LayoutRoomPartition`.**
   - Tests: vanilla-rule unit cases (map edge, 25% boundary, doors split rooms, a 1×1 sealed pocket counts as outdoor, a roofed pocket doesn't); an open-ground layout's public room qualifies; a deep-rock street network doesn't.
3. **Exhaust candidates and pockets in placement.** Shared and new pockets, scoring, the protect-doctrine roof-hole budget, pocket walls in the ledger, and `ExhaustRoofClearance`.
   - Tests: open-ground freezer uses public exhaust with no pocket; back-to-back rooms share one pocket; a deep-rock freezer gets a 1×1 pocket with its roof cleared, within budget, with support intact; when the budget is exhausted, the room moves toward the rock face or the layout rejects predictably.
4. **Post-enclosure correction and validator.** Conversion to pockets when the public room fails.
   - Tests: a forced low-open-fraction public room; injected faults (a door into a pocket, a roofed pocket, a hot cell in a room) are caught.
   - Regenerate `docs/p4a-examples/`, where the ASCII export marks pockets as `x` and cooler hot/cold cells.
5. **Runtime (Codex / P4A integration).**
   - `P4ProceduralSpawner`: spawn coolers with the fixture rotation, build pocket walls, and clear pocket roofs through the mutation ledger.
   - Extend `P2RuntimeAudit`'s cooler check so that the hot room `UsesOutdoorTemperature` and the cold room is the served room.
   - Live cases: an open-ground industrial freezer, a hot-climate barracks, and a rock-protected freezer with a pocket.

## Merge and conflict handling

Steps 1–4 are pure and land on `claude/p4a-foundations`. Codex has merged that branch into `codex/p4-finish` and edits `Source/RealisticBases.LayoutTests/Program.cs` there. To keep merges easy:
- New tests go in a new file, `Source/RealisticBases.LayoutTests/CoolerExhaustTests.cs`, with a single registration line in `Program.cs`.
- Existing files are changed only where the design requires.
- Codex merges the branch again. It must not rebase or rewrite the P4A commits.

If step 1 is needed in game before steps 2–4, Codex can cherry-pick the step-1 commit alone.

## Open calibration values (provisional)

Roof-hole budget, pocket scoring costs, and the maximum pocket size (1×1 by default; 1×2 only when one cell can't take two coolers' hot sides).

## As built (2026-09-23)

- **Step 1 (`cb9893c`):** the rotation now points the hot side away from the room. The validator recomputes the vanilla hot and cold cells from the rotation string (`cooler_orientation_*`). With the old rule restored, this check rejects every freezer layout.
- **Steps 2–4 (this commit):**
  - `LayoutRoomPartition`: vanilla room splitting, roof coverage and `UsesOutdoorTemperature`.
  - Coolers may use any straight wall, and choose their exhaust by cost: public open sky 0, shared pocket 1, new pocket 3, +5 for roof clearing, +1 for digging.
  - Pockets are sealed by pocket walls, room walls or intact rock. Their reserved walls and seals can become later rooms' walls.
  - Pocket walls are counted in the material ledger.
  - Roof clearing over a pocket is allowed only on excavated rock, because P4's ledger clears roof only over excavation.
  - The protect doctrine has a roof-hole budget of ceil(cooled rooms / 2).
  - After enclosure, the partition check reruns the attempt in pockets-only mode if a public-exhaust cooler's room isn't outdoor.
  - The validator checks the cold side, the outdoor hot side, pocket sealing, roof and excavation ledgers, and the budget.
  - The ASCII export shows `x` for a pocket and `O` for a roof hole.
- **Step 5 (partial):** `P4ProceduralSpawner` now spawns the pocket walls. Roof clearing over dug pockets already followed from its "excavated but not retained, roofed" clearance rule.
- **Still open:**
  - Extend `P2RuntimeAudit`'s cooler check: the hot room `UsesOutdoorTemperature` and the cold room is the served room.
  - Live cases: an open-ground freezer, a hot-climate barracks, and a rock-protected freezer with a pocket.
- **Evidence (synthetic):**
  - Mod build 0/0; LayoutTests all pass; PlannerTests 55/55.
  - Hot open ground: 36 coolers over 6 seeds, 31 of them exhausting into public open-sky space, all outdoor by the partition.
  - Rock-protected, temperate and hot, 12 layouts: 19 pockets and 12 roof holes, all within budget, with rock support intact.
  - Fault injection: a removed pocket wall is caught as `_not_sealed`; a kept roof is caught as `_roof_ledger` plus `cooler_exhaust_not_outdoor`.
  - Example: `docs/p4a-examples/mountain-industrial-exhaust-pockets.txt`.
- **Untested:**
  - The pockets-only replan path. No test layout triggered it; open-ground public rooms always passed the 25% rule.
  - Shared pockets never occurred in the test seeds.
