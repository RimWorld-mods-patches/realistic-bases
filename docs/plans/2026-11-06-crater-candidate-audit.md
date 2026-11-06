# Recorded Crater candidate now accepts under existing rules

The recorded candidate is tile 47593 in the disposable `narwhal` world. Both
old and current native GL generator context report `Landforms: None`, so this
is not confirmed coverage of the Crater landform.

The native checkpoint and exact digests below were recorded before the later
small-roster search-budget change. The final layout suite retains this map's
population, wall, funding and preflight regression; no further native Crater
check after that budget change is claimed.

The historical planner succeeded as `4196AD28370EDDE7` after falling back from
two wall layers to one. Native spawning then rejected post `(137,107)` with no
eligible defenders. The original case JSON and terminal log record found paths
of 170, 169, 168 and 171 nodes, above the unchanged 160-cell cap. The report's
first length of 168 disagreed with those native records; the native records are
retained as authoritative. The logged invalid first-outside sentinel is not a
map location. Historical rollback restored 831/831 plants, the item ledger,
terrain and layers.

The original fixture is unchanged: input `68138DCEEFF2BB65`, terrain
`8269E96E7159BC66`, SHA-256
`6bd224d4ef6b325d969c91823121d2503e165b4448b068989dd4bf0353cf63f9`.
Current replay verifies both original attempt inputs, then succeeds on the first
two-layer attempt `6EF5F47C09F47116` as `D293BEE6A5679FEE`. This is a changed
planner result, not a replay of the old native path failure.

A fresh native check with the existing fixes also accepts:

| Fact | Current native evidence |
| --- | --- |
| Input | `C054C475C947DEEC` |
| Terrain | `4A46270FDB6B2886` |
| Catalog | `197E1E942D99277A` |
| Layout | `239C4911916D3ED2` |
| Residents / rooms | 4 / 11 |
| Wall layers | 2, without fallback |
| Crops | 156, above the original 124-cell demand |
| Excavation | 310 of 340 cells; 303 roofs retained |
| Native post audit | 10 posts, each reachable by at least two defenders |
| Longest accepted native path | 64 cells; cap remains 160 |
| Evaluations | 203,440 of 400,000; no exhaustion |
| Ordinary import allowance / finish spend | 4,125 / 680 |

The physical terrain and roof definition IDs match at every cell. Exactly 611
cells gained the existing `ClearableStoneChunk` trait: 605 internal cells and six
edge cells. The snapshot version label also changed. The current input therefore
must not be presented as byte-identical to the historical capture. No new
planner, route, cap or spawning rule was needed for this disposition.

Exact replay of the new native fixture matches `239C4911916D3ED2` and passes
ordinary spawn preflight. Housing has ten modeled positions for four residents;
the saved map has four distinct resident positions. The ordinary offline lamp
audit finds no dark cells in any of the eleven rooms. Native generation applied
437 floor cells and 160 smoothed walls. The base was shown paused, saved in a
separate disposable checkpoint, and the process stopped.

A permanent regression protects the current captured map, complete roster,
requested two-layer depth, crops, excavation, funding, housing and preflight.
Its shared modeled catalog produces `5CD6D46FC60D772B` at 400/4,125 ordinary
import value; that digest is distinct
from the exact native replay. The focused test passes. The 215-test layout suite
passed for the preceding committed fix; no production planning behavior changed
for this case, so only the new regression and exact replay were added here.

The historical native path node lists were never captured. Its original spawn
rejection has not been faithfully reproduced offline, and a changed planner
layout is not claimed as such a reproduction. The queue disposition is
**accepted under existing fixes**, supported by a fresh native route audit.
Defender movement, elapsed cultivation and save/reload remain untested; no fresh
forced rollback was performed and historical rollback captures are untouched.

Runtime evidence is under
`.runtime/investigations/geological-landforms/next-cases/run-2026-10-31b/fixer/crater-47593`.
