# P4B defensive layout: findings and validation

2026-09-28 · Status: P4B.0–P4B.1 complete; P4B.2 in progress; P4B.3–P4B.4 open

The [P4B plan](p4b-defense-plan.md) is the scope authority. P4 and P4A remain open independently: their lakeshore, climate, tribal, optional-mod and exact rejected-flat-tile checks are not credited to P4B or silently closed. P5–P7 are not part of this implementation.

## 2026-09-28 multiple coordinated turret groups checkpoint

The user identified the remaining one-group cap and reiterated the [NPS bastion principles](https://www.nps.gov/casa/learn/historyculture/bastions.htm): projecting positions should reduce blind ground and create interlocking fire. The turret output now holds a root group plus immutable peer groups; each heavy retains its own close-support mini-turret. Group count is bounded by exposed fronts (at most four), not a prescribed gun count. Selection favors new fronts, newly covered reachable approach samples and second-source overlap. Each group must share at least four sampled targets with another gun or a verified covered pawn post; this is conservative planned geometry, not measured live crossfire.

All groups share construction/cover, loaded ammunition, reserve ammunition and power budgets. Each addition revalidates earlier firing lanes, post lanes, blast spacing and maintenance routes against final occupancy. The bounded gate-side candidate area was widened after a richer synthetic fixture showed that the former small area exhausted legal spacing after two groups. No safety limit was relaxed. Diagnostics retain count, total watts/spending, uncovered/single/overlapping samples and reasons for stopping; they do not describe partially covered sides as fully defended.

The synthetic flat fixture selects two heavy/mini groups (four turrets, 460 W, import value 5464) under the existing 5500 allowance. With the same settlement geometry and a 20000 allowance it selects three groups (six turrets, 690 W, value 8196). Tests prove additional coverage, sampled overlap, replay determinism, aggregate budgeting, and rejection of blocked peer service cells/footprints and duplicate emplacements. Both complete pure suites pass; the final LayoutTests run took 63.9 seconds and retained 12/12 high-industrial flat-clutter acceptance (11 same-site growth cases). Docker Release built with zero warnings/errors. Work remains uncommitted. Actual combat, measured cover, protected maintenance, depletion/rearm, active reload and the broader terrain matrix remain open.

The first network live run (`seed=D2A524CE1A78C50C`, map 1, layout `2B12407AE50668C1`) planned two groups/four turrets (460 W, value 5450). All four passed initial fuel, common power-net, cover and service-access audits, but every defender path to the first outside post returned `Found=false`. The transaction removed 827 owned things, restored 403/403 plants and preserved the terrain checksum. This is safe recovery evidence, not an accepted placement. Its archived log is ignored `.runtime/logs/p4b-network-path-audit-recovery.log`.

Inspection of the installed game's IL established a generation-time audit mismatch: `PathGrid.RecalculatePerceivedPathCostAt` defers updates while incremental dirtying is disabled; `ReEnableIncrementalDirtying` flushes them. `FindPathNow` uses the cached grid, and newer 1.6 path data also skips repeated gathering in the same tick. The synchronous final audit now temporarily flushes pending updates, invalidates the newer same-tick data cache through its optional runtime hook, clears reachability caches, and restores the caller's batching mode even if an audit fails. A pure deferred-cost model covers success, audit failure, flush failure, normal unbatched operation and idempotent restoration; it is not a replay of the exact rejected map. The runtime log records batching mode and changed walkability. A fresh live check is required to confirm the corrected audit.

The next live map (`seed=EFCD75EBB0A99617`, layout `3B634E224935E3FD`) rejected before mutation with `power_route_rejected:Power terminal disconnected at 50,96`. The runtime conduit reservation included a civilian arrival-route prefix but omitted the explicit field access lane/inner gate. A shared pure wiring-reservation helper now includes these, every gate, and all group service routes/footprints; whole-network validation requires each gun to connect to a powered compound terminal through legal conduit ground. Tests cover inclusion and network connectivity. This run did not reach the corrected pawn audit and does not validate it. Its ignored log is `.runtime/logs/p4b-network-field-power-preflight.log`.

The corrected disposable Core+RIMAPI run (`seed=33A23235EAE450E9`, map 1, tile 32897, layout `E83A1361F9DADE0D`) accepted two groups/four turrets: West uranium `(106,35)` with mini `(115,54)`, and East uranium `(153,92)` with mini `(148,67)`. The network requires 460 W and import value 5450. All four shared power net `375922256`, had their adjacent cover and reachable service cells, and audited initial fuel 30/30 or 60/60 with finite reserves 60 uranium or 80 steel per gun. Planned final approach metrics were 45 uncovered, 105 single-source and 86 overlapping samples; these are not measured live shots or universal blind-zone coverage. Selection stopped with a critical-power-headroom reason, not the former two-gun cap.

All four exposed sides had posts: South `(126,15)`, East/North shared `(144,75)`, and West `(119,46)`; the second field post supplied a fourth distinct usable position. Every position had at least two real P3-legal defender paths, longest 70 nodes. Nine rooms, 154 unroofed rice cells with zero wild plants, 90/90 field walls, 2/2 field gates, four residents and 204 conduits spawned successfully. Coolers audited freezer −10°C and four rooms +21°C; fog was cleared. No spawn rejection/rollback or exception occurred in this accepted run. Later P3 audits retained four living defenders with none outside bounds, but did not establish occupied exterior posts or turret firing; the disposable player pawn was downed before pause, so this was not a controlled durable combat test.

The accepted run logged `batchingWasDisabled=False` and `changedWalkability=0`. It therefore does **not** prove deferred path-grid updates caused the earlier failure. The same-tick path-data cache was invalidated, and the real path audit passed on this fresh map; exact replay of the earlier rejection remains unproved. Do not relabel the synthetic cache model as an exact-map reproduction. The API pause succeeded and the single disposable process was left running for inspection. Its ignored checkpoint log is `.runtime/logs/p4b-four-turret-network-accepted.log`. P4B is still open.

## 2026-09-28 short gate access and all-side live checkpoint

The unlocked Mac allowed the disposable Core+RIMAPI test to resume. The first all-side prototype had long exterior routes; the user's image/feedback led to replacing new posts with connected five-screen projections and six-cell routes, using existing gates or validated public-connected defense-only sally ports. A perpendicular gate can serve a corner's second exposed face only when the post is near that face, has incoming-side adjacent cover and useful shots. A synthetic north-side public-access obstruction verifies this corner recovery. Every six-cell route still has two exposed gate-axis cells; it is not advertised as fully covered.

The final pre-network live run (`seed=DD7CCE36569E2692`, map 1, tile 5899, layout `B0A1F98D5AFC0F8C`) assigned all four exposed sides without omissions: South used field post `(107,47)`, East/North shared `(110,111)` via gate `(107,109)`, and West used `(98,73)` via `(100,76)`. Both new posts had connected returns, five screens and reachable six-cell routes. Four distinct usable positions each had at least two real P3-legal pawn paths; the longest had 45 nodes. Inherited interior posts remain fallback-only.

That run spawned nine rooms, 156 unroofed rice cells with no wild plants, 100/100 field-wall cells, two field gates, two new sally ports and four residents. A uranium turret `(122,41)` and mini-turret `(100,43)` shared a power net with reachable service cells, initial fuel 30/30 and 60/60, and finite uranium/steel reserves. The final plan retained its equipment decision beyond the search-detail diagnostic cap. No exception/rollback occurred. The game was left paused for inspection, then the exact disposable process was stopped before the network changes above. The archived log is ignored `.runtime/logs/p4b-short-gate-four-front-checkpoint.log`. This establishes all-side placement/access, not actual alternate-front firing or multi-group operation.

## 2026-09-28 attacker-aware perimeter checkpoint (superseded live status)

The previous turret planner scored only the field-gate approach; inherited posts on other sides could sit behind opaque walls. The joint-site post-layout defense pass now computes edge-connected attacker ground and ranged approach samples for all four cardinal perimeter faces. It accounts for final enclosure walls/closed gates and unexcavated terrain blockers. Shoreline firing ground remains a threat even when water prevents directly walking to the wall. This is a bounded conventional land-attack model, not a guarantee against drop pods, sappers or demolition. Supported no-outdoor-field profiles and legacy non-joint fixtures do not yet invoke this production pass.

Every exposed side is assigned a usable existing field post, a legal covered exterior post with a cardinal route from an existing gate, or an explicit omission reason. Each new position has immediately adjacent forward and two flank screens. Dry corners can serve two exposed faces with demonstrated sightlines and incoming-side cover, without creating duplicate physical P3 assignments. Terrain and material constraints may still omit a position; a missing position does not turn an otherwise valid settlement into a planning rejection.

Turrets are ranked against these same approach samples, favoring multi-front coverage before total firing opportunities. Final checks require useful shots on the declared front, preserve all post and arrival paths, and retain cover, blast, power and finite-stock constraints. Large guns require a legal mini-turret plus at least one useful pawn position at their actual front; geometric support from the two field posts is no longer assumed on the far side of opaque walls. Actual pawn weapon eligibility, firing and cover effects remain unproved. New post routes are explicitly logged as reachable, not fully covered.

P3 now prioritizes the threatened front with the perimeter intact, not only after a breach. Shared corners retain multiple supported sectors in the existing saved sector-string field, while legacy single-sector records decode unchanged. Pure tests cover prioritization and exclusive physical assignment; a live reload of this new multi-sector encoding remains open.

Both complete pure suites pass; LayoutTests took about 50 seconds and retained 12/12 high-industrial flat-clutter acceptance (11 same-site growth cases). New tests cover all four exposed open-ground sides, a solid-rock-blocked western flank, reachable attackers firing across water, dry shared-corner access, deterministic replay, multi-front turret coverage and a blocked post route. Docker Release built with zero warnings/errors and `git diff --check` passed. Work remains uncommitted.

The repo-local disposable profile was reset and exactly one game process launched for the live placement check, using the game-launch and RIMAPI skills. The Mac was locked; Computer Use could not unlock it, startup did not reach `GAME_LOADED`, and localhost RIMAPI was unavailable. The exact profile process was stopped and absence verified. No debug action ran and no normal save was touched. Therefore the earlier live results below do **not** validate this new all-side implementation. Next: unlock the Mac, run the API-driven high-industrial placement/access audit, then use a durable combat probe from alternate fronts.

## 2026-09-28 connected field-post access checkpoint

The post-layout pass now gives each usable field post a small rear wall return cardinally attached to the field perimeter, four forward barricades and one exposed-flank barricade. This is a small connected projection, not a whole star fort. The explicit six-cell route crosses the open gate axis and then runs immediately behind the forward cover. Its two gate-axis cells are recorded as exposed; cover does not make that crossing invulnerable. Civilian arrival is routed around these screens after room packing, with an unscreened-route fallback if necessary.

Each post carries its route, screens, return and omission reason in the immutable plan/digest. Final validation checks the advertised geometry, wall attachment, screen presence and unblocked route. A blocked or wet optional return disables that post without discarding the valid field enclosure. Only verified usable posts enter P3 or satisfy a heavy turret's staffed-support check. Low-cover cells remain permitted P3 transit because vanilla paths can step over them; that permission does not claim mechanical cover during transit.

Both complete pure suites passed; LayoutTests took about 46 seconds and retained 12/12 high-industrial flat-clutter acceptance (11 locally grown compounds). New negative tests remove a screen, block an advertised route, obstruct an optional return, and put water under it. Docker Release built with zero warnings/errors. All work remains uncommitted.

The isolated Core+RIMAPI API-driven run (`seed=C84FE465257B78D9`, map 1, layout `12A53D7EFF0A16C4`) accepted a one-layer settlement with nine rooms, 156 unroofed rice cells, 90/90 attached field-wall cells including the two new returns, 2/2 field gates and 10/10 barricades. Both six-cell routes were advertised usable, both posts had at least two P3-legal pawn paths, and the longest legal path had 65 nodes. P3 later reported both posts occupied and no defenders outside bounds. The uranium turret at `(109,41)` and mini-turret at `(131,43)` shared a power net, had reachable service cells and initial fuel 30/30 and 60/60, with finite uranium/steel reserves. Coolers retained −10°C/+21°C and debug placement cleared all fog.

The existing field assault action put the disposable colonist at `(121,33)`. Both posts were occupied beside cover; five sniper projectiles were observed from `field-outer-1`, but none was recorded from `field-outer-0`. The game warned about calculating a disabled shooting-accuracy stat for `Smelly`; this is not yet established as the reason for the silent flank. The target died and the map unloaded at tick 6680, so this run does **not** prove both-pawn crossfire, turret firing/ammunition consumption, cover effectiveness or blind-zone defense. No exception was logged. The exact disposable process was stopped afterward; no normal save was touched.

Next: use a durable/nonlethal, instrumented combat probe that records each defender's weapon range and combat eligibility, each turret's shots/fuel, and actual front/flank cover. Validate long-range fire and close blind-zone support separately. Covered turret-maintenance/retreat corridors, broader projections and the remaining P4B live matrix are still open.

## 2026-09-28 coherent-defense checkpoint

The user requested bastion-like mutual support instead of scattered turrets, with close-range protection for large guns, adjacent cover, protected gate-to-post movement and flank protection. The defense pass now runs against the completed room/perimeter layout. Only the essential field connection and two post/front-cover cells are reserved early; additional approach/flank barricades are fitted afterward, avoiding rooms, walls and the civilian arrival path. Reserving all ten cover cells before room packing had regressed the high-industrial flat corpus to 6/12, so that approach was removed; post-layout fitting retains 12/12 acceptance.

The optional turret output now represents a bounded defense group. A large gun requires a separate mini-turret that can engage at least four sampled cells in its minimum-range dead ground, and each field pawn post must see at least two such samples at a conservative 25-cell range. Planned opaque walls and the turret footprints block those support checks. The group respects combined power/import/finite-stock budgets, disjoint footprints and explosion spacing. Otherwise selection steps down to a mini-turret or omits the optional gun; it does not reject the base. Every turret has an adjacent forward cover row and, where available, an exposed-side cover cell. Barricades are preferred because the selected stone/wood construction stuff is legal for them; sandbags require textile support not supplied by the current construction-stuff resolver.

Pure tests cover deterministic grouped selection, missing-small-support rejection, missing-adjacent-cover rejection, post-approach/front/flank cells, final occupancy and the existing 12-seed clutter corpus. A large turret's range is evaluated from its lower-left firing origin rather than allowing any tile of its 2×2 footprint to supply a shot. These are conservative geometry checks, not evidence of actual pawn weapon suitability, friendly-fire safety, cover effectiveness, or universal coverage of the whole blind zone.

The complete LayoutTests suite passed with zero failures (about 50 seconds), including 12/12 high-industrial clutter acceptance. PlannerTests also passed with zero failures. Docker Release built with zero warnings and errors; `git diff --check` was clean. This checkpoint remains uncommitted alongside the earlier P4B work.

A disposable Core+RIMAPI live run (`seed=D435ADBACA556D3F`, `map=1`, layout `CBFC0525F25285AB`) spawned nine rooms, 153 unroofed crops, 112/112 attached field walls, two gates, ten field-post barricades, and four residents. It placed a uranium turret at `(152,95)` with three cover cells, 30/30 fuel and 60 reserve uranium, plus a mini-turret at `(151,117)` with two cover cells, 60/60 fuel and 80 reserve steel. Both shared the same power net and had reachable service cells. The spawn completed without an exception, and the exact disposable process was stopped. This live check preceded the last conservative firing-origin validation tightening; the tightened planner has pure/build evidence but has not been re-spawned yet.

Still open: full connected bastion-like projections, covered turret-maintenance/retreat corridors, constrained-terrain screen completeness, actual fire into the heavy blind zone, measured front/flank cover, friendly-fire behavior, ammunition depletion/rearm, breach/fallback and save/reload. The live image showed the intended supported pair and field-post screens, not a complete whole-settlement defensive network. P4B is not complete.

## Baseline inspected at `daba7f3`

| Existing contract | Observed implementation | P4B consequence |
|---|---|---|
| Wall layers | `ProceduralLayoutPlanner.ChooseGates` reserves a five-cell-wide gatehouse band for each layer beyond the first. `ProceduralLayout.BuiltWalls` is the single general enclosure boundary. | Prior two-/three-layer reports prove serial gate doors/bands, **not** additional physical exterior layers on eligible sections. Record section coverage and omissions explicitly. |
| Planning / transaction | `ProceduralLayoutValidator.Validate` checks the pure plan. `P4ProceduralSpawner.TrySpawn` then preflights fields, conduits and occupied cells before `P4SpawnTransaction`, with rollback on partial failure. | Defense geometry, equipment and supply must enter the plan/preflight before any mutation and use the same transaction. |
| Materials | `LayoutEquipment.Count` accounts for room/enclosure walls, doors and power, but the validator currently counts only `BuiltWalls` plus exhaust-pocket walls, excluding gate-band walls from its wall input. | Count every new wall and gate-band wall, plus selected defense stuff; enforce bounded import value before spawning. |
| P3 posts | `GeometryEncounterPlanner` chooses gate posts within two public cells of doors and ranged posts by adjacency to perimeter walls. It does not trace line of fire through final opaque occupancy. | A wall-adjacent post is not evidence of a firing opportunity. Supply verified positions and fallback/retreat routes to P3. |
| Defensive equipment | The procedural spawner creates planned room fixtures, yards, and a mortar. It does not place selected turrets or cover around the enclosure. `SettlementRole.Defense` lists autocannon/mini-turret/sandbags/barricade, but `Turret_Sniper` and `SniperTurret` are absent. | Add stage legality and actual pre-spawn defense selection; catalog entries alone are not placement evidence. |
| Power / stocks | Conduits are routed from fixture terminals; finite mortar shells and generator fuel are spawned. There is no planned turret load or uranium stock for the procedural path. | Include turret watts, service cells, loaded fuel and reserve uranium in effective finite budgets and final net validation. |

These are code observations, not runtime results. The previous P3 and P4/P4A observations remain valid for their stated paths, but none closes a P4B gate.

## Acceptance gates and evidence log

| Step | Pure/build evidence required | Live evidence required | Result |
|---|---|---|---|
| P4B.0 | Inspect source and record baseline, immutable defense inputs/outputs, reservation/occupancy ownership, test corpus, recovery boundary | None | Documentation baseline only |
| P4B.1 | Deterministic 1/2/3-layer section topology on flat, irregular, corner, narrow, rock and water fixtures; exact physical wall/door counts; legal materials/imports; one public root with every crossing and service/retreat path | Final-map structures/terrain/roof/path audit for representative accepted plans | Complete: pure wall-connectivity/corner recovery and flat/mountain live return, path, checksum and forced-rollback audits; GL compatibility remains in P4B.4 |
| P4B.2 | Joint field/compound reservation, attached field wall and gates, exterior posts and cover, blocked-lane and blind-zone negatives | Actual defender shots, cover use, and unobstructed lanes on accepted maps | In progress: Core controlled assault observed projectiles from both occupied exterior posts with adjacent cover; cover effectiveness and broader raid/breach matrix remain open. |
| P4B.3 | IndustrialLate research prerequisite closure and lower-tier exclusions; resolved footprint/power/range/refuel/explosion values; effective difficulty-sensitive uranium accounting including loaded ammunition; power/material/threat/stock budget and transaction preflight | Turret firing, power loss, uranium consumption/rearm/exhaustion, breached gate/wall fallback and active reload | Pending |
| P4B.4 | Full pure suites, Docker Release, static no-trap/IED and final occupancy/recovery regressions | Representative player raid, Core and GL terrain, forced partial rollback and save/reload | Pending |

Every validation entry must distinguish pure test, build, runtime log, and observed game behavior. Screenshots may illustrate geometry but do not establish firing, cover, pathing or consumable depletion. A plan rejection with zero mutations is safe recovery evidence, not evidence that the fortification works. Do not mark P4B complete without the live matrix.

P4B.3 eligibility checkpoint: `IndustrialLate` now includes Core's `SniperTurret` research (its prerequisites were already inherited), and the defense role can resolve `Turret_Sniper` beginning at that stage. The Core fixture pure suite confirms prerequisite closure, `IndustrialMid` rejection and `IndustrialLate` acceptance. This establishes catalog legality; operation remains a separate gate.

P4B.3 resolved-facts checkpoint: the catalog now carries each autonomous turret's gun def, minimum/maximum range, power, finite refuel capacity/multiplier/initial load/per-shot use, difficulty-scaling flag, explosion radius and declared combat power. The generated Core 1.6 fixture and pure suite assert mini/autocannon/uranium facts, including the slug turret's 11.9–45.9 range, 150 W draw, 30 fuel capacity, 0.5 uranium multiplier and 5.9 explosion radius. The runtime adapter reads the same properties from resolved defs.

2026-09-27 P4B.3 prototype checkpoint: an optional single-emplacement planner now reserves a footprint, long-range firing sample, blast clearance, service route, power headroom, loaded finite fuel, reserve fuel, and construction/import value before spawning. The high-industrial pure fixture accepts a uranium slug turret; lower-stage and blocked cases reject it. In a disposable Core flat-map run, the generated plan selected `Turret_Sniper` with 150 W draw, 60 uranium item units represented by the initial 30 fuel, and 60 reserve uranium units. The live spawn audit found the turret on a power net, initial fuel 30/30, reserve stock 60, and a reachable service cell. The same run reported 130 unroofed rice cells, 90/90 attached field walls, 2/2 gates, 2/2 cover fixtures, no wild plants within the field, correct cooler setpoints, and zero remaining fog after the debug placement action. This proves placement and initial state, **not** live firing, power-loss behavior, consumption/rearm/exhaustion, breach fallback, or save/reload. A later closed-gate fire-lane diagnostic was separated into compound versus field approach samples; that diagnostic change has built and passed the pure suite but awaits a fresh play test. The disposable process was stopped without using the normal profile.

The RIMAPI/debug-actions companion is enabled only in this test profile. The installed RIMAPI registered the companion extension; its initial empty action list was traced to the companion scanning `Verse.DebugActionAttribute` instead of RimWorld 1.6's `LudeonTK.DebugActionAttribute`. The companion source was corrected and rebuilt. The skill's endpoint examples were updated from the nonexistent `/api/extensions/...` path to the observed `/api/v1/debug-actions/...` registration. This is test-harness work, not P4B feature evidence.

Follow-up: `GET /api/v1/debug-actions/debug-actions?filter=Realistic%20Bases` returned 43 actions, and POST invoked both the high-industrial override and playable flat generation. The API invocation response reports only method execution, not plan acceptance. Three further randomly selected high-industrial flat maps rejected before mutation (two `no_valid_layout`, one `search_budget_exhausted`), primarily in room packing. A synthetic 160×160 flat-clutter fixture based on their obstacle densities reproduced 1/3 acceptance with the former 38-cell compound reservation. Increasing the high-demand envelope to 46 cells produced 2/3 acceptance; the captured older flat fixture and the full deterministic suite still pass, as does Docker Release. This is an improvement, **not** proof that those exact live tiles now accept. A fresh game run is pending because the disposable process exited while a different normal-profile RimWorld process was running; no input was sent to that process.

The remaining flat rejection was traced to the planner's hard compound square: room packing could fail inside the reserved square while the same tile had free land immediately outside it. The joint planner now retries the *same* field, crossings, and compound site with outward 12- and 24-cell expansion before considering another site. The grown anchor must be in the original site's connected placeable component; the field and crop reservation never move or shrink. The bounded default evaluation cap is 400,000 so these local retries are not prematurely cut off by the former 200,000 cap. A 12-seed 160×160 high-industrial clutter matrix now accepts 12/12; 11 plans exercised local growth, and final layout validation passed. This is synthetic evidence, not a proof that every terrain or live seed must accept. Rejection remains correct for genuinely incompatible field/gate/terrain constraints or exhausted bounded search, and exact live-tile replay is still required.

The latest disposable flat generation did pass planning and select the uranium turret, but then rolled back because the field-post pawn-path audit imposed an unrelated 64-node length cap on otherwise defended-cell-only paths of 65–67 nodes. The cap has been removed; this edit passed the pure suite and Release build, but the same corrected path has not yet been observed in game.

Live follow-up on a clean disposable Core+RIMAPI profile: a new high-industrial flat tile (`seed=1833007F0BF18888`, map 1) accepted on candidate site 2 after automatic 2→1 layer fallback. The procedural spawn completed with nine rooms, 156 unroofed rice cells, 72/72 attached field walls, 2/2 field gates, 2/2 covered and reachable exterior posts, four residents, and a powered 30/30-fuel uranium turret with 60 reserve uranium. The post-path audit found two legal paths per post (longest 47 nodes), and no exception or startup error appeared in that run. This proves the updated build still works in game, but that particular tile did **not** need compound growth and did not exercise the previously failing >64-node route; both still rely on synthetic/code evidence pending a matching live case. The verified disposable process was stopped afterward.

## P4B.1 physical wall layers and access

The current prototype selects a seed-weighted 50/30/20 preference for one,
two or three layers, capped by tech stage and declared construction/import
spend per resident. A bounded fallback reduces layers when terrain cannot
support an outer section. Eligible straight gate-adjacent wall runs gain
physical outer sections connected to serial gate bands. The exact wall/door
material ledger, final wall/door realization and gate reachability are checked.
The pure layout suite and PlannerTests pass; Docker Release builds without
warnings or errors.

On the disposable Core flat-map test, an open-ground three-layer plan realized
6/6 outer wall cells, 8/8 band walls, 3/3 gate doors and 1/1 reachable gate;
a second flat-labeled, rock-bearing map realized 3/3, 8/8, 4/4 and 2/2.
Neither proves full concentric enclosure coverage: these are declared short
reinforcement sections. The captured flat-tile plant regression and 460/460
plant rollback are detailed in [P4 findings](p4-findings.md).

The strengthened P4B.1 prototype requires at least three physical outer-arm cells
per declared section, an inner run supporting every farther-out cell, and a
continuous walkable lane from each section end to its own gate crossing.
Candidates lacking an outer section reject before mutation; production's
bounded layer fallback can then choose a feasible thickness. The pure suite
accepts 1/2/3 layers on pond/ridge, mountain-face, shoreline and water-corner
fixtures, with a deliberately blocked lane rejected by the validator. The
spawner now clears lane plants within the transaction and audits both actual
lane cells and endpoint reachability. A disposable Core flat three-layer
generation realized 6/6 outer walls, 8/8 band walls, 3/3 doors, 1/1 gate
routes, 12/12 clear service cells and 2/2 reachable service termini. Both
pure suites and Docker Release passed; the game process was stopped afterward.

A separate clean three-layer forced partial-spawn test aborted after 613 owned
things and restored 379/379 cleared plants plus the terrain/roof checksum.
The same plan then spawned successfully: 6/6 outer walls, 16/16 band walls,
6/6 doors, 2/2 gate routes, 12/12 clear service cells and 2/2 reachable
service termini. Its cooler audit also found one freezer unit at −10°C and
four occupied-room units at +21°C. This tests rollback of the new lane-clearing
footprint on flat Core terrain, not constrained-terrain recovery or combat.

The remaining corner-recovery and live constrained-terrain/roof checks are
recorded below. P4B.2–P4B.4 remain open.

### Detached-wall correction

A user screenshot exposed a defect in the earlier prototype: straight arms
joined the gate band but ended as freestanding piers. The previous live
wall-count and service-lane audits did **not** prove physical connectivity.
Each flank now has an explicit return to the baseline/previous layer; a flank
without a straight arm uses a short gate-band corner return. A candidate
without every required return rejects before mutation, and the return cells
participate in the digest, material ledger, occupancy and spawn transaction.
The validator checks return geometry and anchor ownership. A new pure graph
regression checks that every outer wall cell has a cardinal wall path to the
enclosure. The 1/2/3-layer flat, pond/ridge, mountain-face, shoreline,
water-corner and narrow-approach fixtures pass, as do PlannerTests and the
Docker Release build. In a fresh disposable Core flat three-layer run, the
spawn audit reported 11/11 outer walls, 5/5 return cells, 4/4 anchored
returns, 8/8 band walls, 3/3 gate doors and 2/2 reachable service endpoints.
The in-game view showed the gate flanks returning into the existing wall.
This is flat-map connectivity evidence only, not a constrained-terrain,
combat or P4B closeout. The test process was stopped afterward.

### Corner recovery and constrained live terrain

A new pure water-corner fixture places a permanent obstruction at a previously
valid return cell. The old plan is rejected for `outer_section_unreturned`;
bounded replanning produces a different valid three-layer layout whose every
outer wall cell remains cardinally connected to the enclosure. The input
terrain digest is unchanged. The full layout suite and Release build pass.

A fresh disposable Core Mountain/Caves map accepted three requested layers
without downshift: 11/11 outer walls, 5/5 return cells, 4/4 anchored returns,
8/8 band walls, 3/3 doors, 1/1 gate route and 2/2 reachable service termini.
It excavated 106 rock cells. Final-map validation reported unchanged terrain
after the settlement/power steps, preserved terrain/roof state, no traps or
IEDs and a reachable player arrival. In a separate clean Mountain/Coast run,
a forced partial spawn rolled back 623 owned things, restored 514/514 plants
and the terrain/roof checksum, then the same three-layer plan spawned with the
same wall/return/path counts. This completes P4B.1's Core wall/access and
recovery gate. It does not prove fire lanes, cover use, turret operation, GL
compatibility, or the P4B.4 raid/save matrix.

## P4B.2 sightline baseline (not a completed gate)

### Joint-site pure preflight checkpoint (not yet integrated)

`P4BJointSitePlanner` now supplies a bounded, deterministic field-first pairing prototype. Its compact square candidates count connected, unroofed, fertile crop cells after the reserved wall and one-cell hauling lane are removed, then require a neighboring connected compound envelope with the requested usable-cell capacity and outside-post clearance. Pure regressions replay the reservation digest, reject an attractive isolated soil patch in favor of one with adjacent compound room, and reject roofed fields. This is a search contract only: the main procedural layout does not yet consume these reservations, and the field wall is not spawned. The next checkpoint must join it to room packing and validate the actual shared boundary/crossings.

The next pure checkpoint threaded the selected pair through an opt-in detailed-layout path: the existing room/street/power planner sees only the compound envelope as placeable, the resulting layout carries the immutable site digest, and final validation rejects any crop, field-wall or outside-post footprint consumed by compound geometry or constructed roof. A four-resident 160×160 flat synthetic case produced a complete valid layout and exact replay. This remains opt-in for pure tests; live `P4AMapPlanner` has not switched over and no field wall is yet authorized to spawn.

The pure final-geometry checkpoint now precomputes a bounded family of field gate/hauling-lane alternatives inside each paired site. The selected compound gate must align with one of those previously reserved interfaces. The field wall's two arcs each attach physically to the compound perimeter (directly or through bounded exterior connectors); the replacement arrival path crosses the inner and outer field gates and reaches the map edge, rather than relying on the obsolete compound-only route. Final layout acceptance includes this wall plan, exact crop cells, outside posts and its additional wall/door construction ledger. The flat four-resident fixture passes deterministic replay and final validation. This is still pure/synthetic evidence; the live planner and spawner have not switched over.

2026-09-27 live checkpoint: the production P4A path now requires the joint site and spawns its attached enclosure transactionally. A clean disposable Core run accepted a one-layer 160×160 map with 156 unroofed rice cells (124 minimum), 88/88 attached field-wall cells, 2/2 gates, 2/2 cover fixtures, and 2/2 reachable exterior posts. The spawn audit found zero wild plants within the established field and at least two P3-legal pawn paths per post. Later P3 audits reached three occupied posts with zero defenders outside defended bounds; the game view showed the rice-filled outdoor enclosure and main-perimeter connection. Rice is placed at generation; no growing zone or farm-work/replanting loop is claimed. Earlier post-path audit rejections rolled back all spawned things and restored all cleared plants; ordinary site-search rejections still occur on some live maps with zero mutations, usually `room_unplaceable`. This is not yet P4B.2 closeout: no raid has demonstrated exterior-post firing or cover use, and P4B.3 turret operation is still open.

2026-09-27 controlled Core assault checkpoint, disposable profile restored from `RealisticBases-P0-Disposable.rws`: flat 160×160 `map=1`, layout `A036DE5F6A81DDB7`, one-layer joint field with 156 crops and two outside posts. An opt-in debug probe moved the disposable player pawn to `(102,0,81)` facing posts `(99,0,95)` and `(105,0,95)`. At ticks 4800–6600 both defenders occupied their field posts, each had adjacent spawned cover, and the projectile trace recorded sniper-rifle and autopistol bullets launched by those assigned defenders at their post cells. The probe counted 32 field-post projectiles; the target's health fell from 1.000 to 0.347. P3 reported zero defenders outside its bounds and zero player pawns controlled. This establishes actual exterior firing on a controlled approach, not measured cover protection or a representative moving player raid. Pure geometry and live P3 had also disagreed about the north/south sign: positive Z is now north in both, and field posts inherit a directional sector instead of the nonmatching `field` label. A pure geometry assertion covers that mapping. Active save/reload did **not** complete in this run: the disposable process ended before the attempted save, and the UI selector subsequently exposed a separately running normal game. No normal save was written; future UI tests must verify the exact disposable process still exists before every UI session or after a turn boundary.

The user clarified the target topology: keep the main field unroofed and outdoors, surround it with wall, and put fighting positions outside that field-facing wall. Planting within the existing inner courtyard is not the target. A gate-adjacent exterior-post prototype was tested in a disposable Core flat map (two reachable posts, two barricades, two P3 assignments), but it did not enclose the field or prove live shots. That prototype was removed from production rather than treating its counts as P4B.2 evidence. The remaining implementation must co-plan the field, its perimeter and crossings, and posts beyond it.

The inherited `GeometryEncounterPlanner` chooses gate posts near the inside
endpoint and ranged posts beside a wall, setting `HasLineOfFire=true` without
tracing a shot. `P4BFireLane` now supplies a conservative pure prefilter for
Euclidean range and grid occlusion, including closed opaque doors and diagonal
wall slits. Synthetic clear, blocked-gate, exterior-flank, minimum/maximum
range and diagonal-slit cases pass. It is not a substitute for RimWorld's
runtime shot check. A diagnostic-only generated-plan audit samples the first
12 cells of each exterior gate route against planned opaque walls and closed
doors. In a clean Core flat three-layer plan, it found 0/6 inherited direct
posts with a shot and 0/12 sampled approach cells covered. The map still
spawned and passed its separate wall/access audit; this result identifies a
combat gap, not a generation failure. P4B.2 must next plan exterior fighting
positions, cover and fallback routes in final occupancy, then confirm actual
shots and defender use in-game before treating any post as valid.

## Implementation interface to preserve

Add a defense plan derived from the immutable procedural layout and resolved runtime catalog *before* `TrySpawn` mutates the map. Its selected walls/doors/fixtures/stock and route reservations must be part of one final occupancy model, material and power ledger, digest/report, and preflight. The existing transaction owns every defense spawn so a forced failure rolls it back. P3 consumes verified posts from that final model; it must not independently infer ranged utility from wall proximity. Repair is bounded and deterministic; a rejected candidate remains zero-mutation. Natural rock may contribute to protection, but it is not a fabricated built-wall layer.
