# Ordinary room lighting

The user selected wall lamps first, adding floor lamps only for uncovered areas.
Standing lamps must keep their light at the top regardless of room rotation.

The previous wall-light pass used a fixed squared reach of20. If any room cell
was outside that reach, it discarded the entire wall-light plan. The fallback
placed one floor light per36room cells through ordinary furniture slots, which
clustered lamps along one wall without checking the far side. Floor lights also
inherited room rotation despite the vanilla standing lamp being non-rotatable.

The replacement scores candidates by newly covered interior cells, preserves
useful wall coverage, and searches all legal floor positions only for its gaps.
Floor fixtures retain room access/connectivity reservations. Once coverage is
complete, wholly redundant lamps are removed. This is bounded greedy coverage,
not a claim of globally minimal lamp count or zero overlapping illumination.

Coverage uses resolved native glower radius and strongest color channel, captured
along with light-blocking fixture facts in the catalog and exported fixtures.
Historical Core captures without those fields use the known Core lamp facts;
unknown custom lamps receive no guessed coverage. Optional facts are hashed only
when present, preserving original captured input anchors. New captures explicitly
include the facts and consequently have new catalog/input digests.

`LayoutRoomLighting` models the vanilla flood's100/141cardinal/diagonal costs,
initial distance1, light blockers and corner rule, native falloff, integer color
truncation and normal ground glow0.5. It conservatively requires one ordinary
lamp to provide normal brightness at each target cell; it does not credit
additive weak overlaps. Sky and timed sun lamps receive no ordinary-light credit.
The implementation was checked against pinned1.6source and the installed
1.7.4995glow-flood decompilation. This is offline coverage modeling, not a native
rendering measurement.

Final layout validation rejects uncovered cells in ordinary furnished rooms.
The separately packed artillery magazine retains its existing template contract.
`StandingLamp`, `TorchLamp` and `SunLamp` use canonical North rotation; wall
attachments keep the orientation required by their supporting wall.

## Verification

- The orientation regression first reproduced StandingLamp South versus expected
  North. All four room orientations now pass; wall attachments remain oriented
  toward their own supports.
- The dense11×11grow-room regression first failed41cells under the conservative
  single-source coverage criterion. The corrected plan covers its whole interior
  with ordinary wall lights, independently of the sun lamp.
- Dining, research, housing and stock rooms pass; a17×17room beyond wall-light
  reach exercises floor gap filling. Removing any retained test lamp leaves an
  uncovered cell. An opaque partition blocks modeled light.
- Complete layout suite:202passed,1existing skipped peninsula case,0failures.
  Planner/garrison suite passed. Final mod build:0errors,4existing warnings.
- Offline replay of captured DesertPlateau input5BFCD3B27ED06638 accepts layout
  949AC824E2C128CE, terrain7131D30BFF05BE36. All12audited rooms have zero uncovered
  cells. Its7×11grow room uses two wall lights on opposite sides. That selected
  layout has16basins/1sunlamp and funds remaining food shortfall; it is a new
  candidate, not a claim that the saved23-basin layout was relit in place.

The user explicitly accepted offline auditing. Experimental live measurement
instrumentation was removed completely after its early grid update hit an
uninitialized render cache. Those diagnostic failures rolled back; they are not
new geological-landform rejection cases. No native brightness or accepted live
lighting-fix result is claimed. The saved threshold25checkpoint is preserved.

Offline replay reporting is available with `--live-fixture --lighting-report`
(the command replays a captured input; it does not start the game). Exact logs
and captures remain in the ignored geological investigation's`fixer/room-lighting`.
