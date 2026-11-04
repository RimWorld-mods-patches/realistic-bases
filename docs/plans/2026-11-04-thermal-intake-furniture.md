# Furniture beside indoor vents and coolers

Indoor air cells beside wall coolers and generator-room vents no longer reserve
an empty pawn interaction tile. Compatible furniture can occupy those cells.
Outdoor exhaust and room separation requirements remain enforced.

## Native rules

The installed RimWorld 1.7.4995 implementations were inspected with the bundled
decompiler, alongside the project's pinned 1.6.4871 source:

- `Building_Cooler.TickRare` requires both adjacent cells to be non-impassable.
  It does not require either cell to be empty or reachable as a job target.
- `Building_Vent.TickRare` exchanges heat using adjacent room regions. Native
  room lookup includes impassable furniture with free air exchange. Solid,
  full-fill impassable furniture removes that cell's region and cannot replace
  the served room; doors and fences have their native special region handling.

Beds, shelves and hydroponics basins that are natively passable can therefore
share a cooler's indoor air cell. A vent may also face room-permeable impassable
furniture, such as a generator. This is distinct from the conservative blocking
flag used to pack furniture and preserve job access elsewhere in a room.

## Planning and validation

`LayoutFurnishing.Room` now tracks air requirements separately from occupied
footprints and required empty interaction cells. Existing furniture is checked
when a cooler or vent is placed. Later furniture placement checks those air
requirements too, covering freezer racks placed after coolers. The temperature
fixture's `Access` collection is empty; its native cold/hot geometry is unchanged.
Door approaches, actual workstation interaction tiles, furniture job access and
distinct resident spawn floor remain governed by their existing rules.

The catalog captures optional `BlocksRoom` facts from resolved native defs.
Explicit true and false facts participate in the catalog hash. Historical
captures without this annotation retain their original hashes and conservatively
exclude unknown impassable furniture from vent air cells. Their captured native
passability is sufficient for cooler checks. New fixture exports preserve the
additional fact.

Final layout validation checks the actual catalog properties of indoor occupants.
Native spawn audit independently verifies cooler-side native passability and the
actual served/exhaust rooms, and records furnished cooler/vent air cells. Existing
outdoor-temperature, pocket, roof-hole and exhaust clearance rules remain intact.

## Regression evidence

Before the repair, three focused regressions failed:

- A cooler could not face a passable bed that was already placed.
- A freezer shelf could not use the indoor cell reserved earlier by its cooler.
- A vent could not face room-permeable generator furniture.

All three pass after the repair. A fourth test covers conservative unknown room
facts, distinct captured hashes, full-fill vent blockers and the continued
rejection of impassable cooler intakes. The cooler geometry suite and the
committed Oasis food/perimeter regression also pass.

Native decompilation, replay, profile and final validation evidence is retained
under `.runtime/investigations/vent-indoor-cell/`. No transaction/rollback code was
changed; the historical investigation fixtures and rollback records remain intact.

## Native verification

After the user restored the previously supported RIMAPI version, the isolated
profile was restarted and debug-action discovery worked again. The fresh native
input `F7E229449562E85C` (terrain `6343F62425C755EE`, catalog
`197E1E942D99277A`) produces layout `46E76C8483CBFAA2`, reproduced exactly offline.
Native generation completes successfully. Its final cooler audit reports eight
furnished cooler cold sides and seven furnished vent cold sides, while retaining
native cooler passability and served/exhaust room checks.

The paused save contains 11 residents, 150 natural crop cells, 16 basins with 64
crop cells, one sun lamp and 196 meals. The three-layer settlement was shown
paused, saved as `Thermal-Intake-Furniture-2026-11-04.rws`, then its exact disposable
process was stopped. GL still reports no applied landform for this candidate.
This checks native placement/airflow prerequisites; it is not an elapsed thermal
performance or defender-movement test.

## Compatibility checks exposed by the new packing

The first full suite exposed three assumptions, all retained as checked behavior:

- The legacy P4 projection treats furniture anchors as impassable and automated
  devices as job targets. It cannot represent certain furnished air cells. The
  adapter now declares those layouts incompatible, instead of claiming a valid
  legacy navigation projection; native layout/spawn validation remains unchanged.
- The old oversized-power test assumed 200 residents exceeded a legal power room.
  Improved packing invalidated that boundary. It now supplies an explicit rated
  load beyond the room's physical generator-area bound and checks rejection
  before any layout evaluations.
- The T6 acceptance fixture's synthetic rock ridge lacked underlying buildable
  terrain. The changed layout used its excavated crossing to reach a solar yard,
  and the wiring checker correctly rejected that unsupported route. The acceptance
  fixture now marks buildable ground beneath the rock, still requiring excavation.
  The same test also checks that the network rejects unsupported wiring terrain.
  No production wiring, excavation or defense acceptance rule was loosened.

Final validation: **213 layout tests passed, zero skipped, zero failed**. The
captured native layout replays exactly, historical Oasis input/terrain anchors
are preserved, and the IceSheet food/housing compatibility check passes. The
final mod build has zero errors and four existing warnings. The native test
profile is stopped and its paused checkpoint is preserved.
