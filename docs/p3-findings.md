# P3 playable defensive encounter: findings and validation

2026-09-21 · Status: P3 complete; P4/P5 unstarted

## Scope and boundaries

P3 replaces the two-role P0 diagnostic Lord with a settlement-scoped defensive encounter controller. It covers sectors and usable firing posts, weapon-aware exclusive role assignment, bounded local response, gate defense, reserves, breach response, safe needs relief, inner fallback, staffed mortars, finite combat supplies, and narrow essential support. Defenders must not become an unrestricted assault force.

The current MVP supply revision governs this work. Routine readiness targets a provisional three game days and combat readiness a separate six game hours at declared firing duty. These are planning defaults to calibrate, not unbounded sustain claims. P3 will physically consume finite stocks and will keep stock-purpose metadata capable of later separating operating reserves from trade goods, but actual settlement trade-stock ownership/reconciliation belongs to P5. Full seasonal operation, repeated harvests, and continuous manufacture are post-MVP.

P4 terrain/fortification variants, P5 trade/persistence integration, specialized EMP/smoke tactics, and broad manufacturing are excluded. P0's coarse unload/re-entry state and regenerated defender limitation remain accepted; P3 must validate active-encounter save/reload, not claim full revisit/casualty persistence.

## Fixed implementation invariants

- Planning remains deterministic, snapshot-based, bounded, and separate from map mutation.
- One defender has one active assignment. Invalid posts cause stable reassignment or fallback, not reservation/job oscillation.
- Destination and traversed route must remain within role-specific defended bounds. Ordinary harm, hunger, elapsed time, or a remote target must not transition the garrison into unrestricted assault.
- Closed gates and solid walls must still leave reachable, occupiable cells with real lines of fire. No wall-top or through-wall assumption is accepted.
- Industrial mortars must be staffed, reachable, unroofed, supplied with finite legal projectiles and any reload/barrel material required by RimWorld 1.6. Incendiary use is bounded and accepted only after fire/friendly-risk validation. Tribal settlements receive no illegal mortar, firearm, turret, or electric substitute.
- Essential hauling, refueling, tending, and needs relief use narrow settlement-owned adapters and real items. No recurring free food, fuel, ammunition, medicine, or barrel generation is permitted.
- Player pawns, player work settings, ordinary NPCs, and unrelated maps remain outside the controller's scope.
- Primary, repair, recovery, and fallback paths contain no traps or IEDs.
- Runtime evidence must state measured electrical draw, firing duty, consumption, and reserve duration. P2's observed 360 W outage load is not evidence for added P3 equipment and must not be restated as the earlier 650 W model.

## Checklist and evidence policy

| Step | Implementation | Required evidence before commit | Status / commit |
|---|---|---|---|
| P3.0 | Scope, checklist, boundaries, and evidence ledger | Documents agree; P4/P5 remain unstarted | Complete — `4fca42a` |
| P3.1 | Pure doctrine and assignment/state model | Determinism; weapon/post compatibility; exclusive roles; pursuit plus route bounds; breach/local response; safe relief; inner fallback; invalid-post stability | Complete — `d8c0d63` |
| P3.2 | Encounter layout, mortar court, finite supply and capacity budgets | Usable post geometry; mortar reachability/unroofedness/staffing; declared duty quantities; active-rule materials; power delta; tribal hard exclusions; inherited planner properties | Complete — `7feb21a` |
| P3.3 | Runtime Lord/controller, support work, consumption, persistence and diagnostics | Build; focused live job/state/consumption/save-load checks; no broad behavior patch; recovery/fallback unchanged | Complete — `820ec44` |
| P3.4 | Full encounter/regression matrix and exit decision | Multiple approach battles; night; long range; gate breach; incendiary; operator/power removal; exhaustion; invalid post; needs relief; active save/reload; tribal; Core/GL; player unaffected | Complete — this closeout checkpoint |

Source inspection, pure tests, compilation, map-generation audits, and live battle observations are recorded as distinct evidence categories. A duty assignment, successful plan, or clean build is never reported as proof that a pawn occupied a post, a mortar fired, a stock was consumed, or a battle stayed local.

## Commit mapping

- P3.0 documentation and checklist: `4fca42a`.
- P3.1 doctrine/state model: `d8c0d63`.
- P3.2 physical encounter contract: `7feb21a`.
- P3.3 runtime garrison integration: `820ec44`.
- P3.4 full runtime matrix and closeout: this closeout checkpoint.

## Evidence ledger

### Source and API evidence

P3.1 is engine-independent. `GarrisonDoctrine` selects routine, sector hold, local breach response, or inner fallback from an immutable situation. It validates a full pursuit route rather than only the target radius, requires every traversed cell to remain inside the defended bounds, and applies an explicit route-length cap. Posts declare cover, real line of fire, route validity, range band, mortar/inner role, and current usability.

Assignment is stable and exclusive: sorted inputs and deterministic tie-breaking produce one role per defender and one defender per post. Weapon range/class and mortar capability constrain post selection. A still-valid previous post is retained during a 300-tick reassignment cooldown; an invalid post bypasses the cooldown immediately. Needs relief is assigned only when its route is safe and remaining defenders preserve minimum outer staffing. A lost outer defense selects only inner posts/fallback destinations.

Vanilla 1.6 source inspection established the runtime contract for P3.2. `JobGiver_ManTurrets` only selects a reservable mannable turret when ammunition is reachable and, for non-classic mortars, barrel fuel is present or reachable. `JobDriver_ManTurret` searches within 40 cells, carries exactly one shell into `CompChangeableProjectile`, consumes that carried Thing, loads a reinforced barrel through `CompRefuelable` when active rules require one, and then mans the interaction cell. Non-player shell loading rejects projectiles whose damage definition does not harm health. `CompChangeableProjectile` serializes shell type and loaded count. These behaviors support real physical consumption and save/load but must still be observed live in P3.3/P3.4.

The P3 encounter plan derives from the immutable P2 plan and adds four exterior ranged posts covering north/south approaches, two inner fallback posts, and two gate-reserve cells without placing a pawn on a solid perimeter wall. The industrial plan adds one 2x2 mortar footprint in an unroofed, heavy-support open court, a reachable interaction post, and a magazine less than 40 cells away. The current four-resident roster is intentionally role-switched: one mortar operator and one support worker are assignments, not extra simultaneous identities; combat reassignment cannot double-assign them.

The provisional combat budget declares two HE shots per game hour for six game hours: 12 HE shells. Two incendiary shells are a separate bounded contingency and do not count toward HE duty; live fire/friendly-risk gating remains required before use. Two reinforced barrels are planned but will be physically spawned only when the active storyteller difficulty does not use classic mortars. All P3 combat stock is tagged `OperatingReserve`; P3 creates no `TradeGoods` allocation and does not implement the P5 trade adapter.

The Core manned mortar is not powered. P3.2 therefore records 0 W of added continuous mortar load. This neither changes nor revalidates P2's observed 360 W outage case, and it does not claim that the earlier 650 W planning model was measured. Live P3 audit will separately report the actual connected network draw with the encounter equipment present.

### Pure/state tests

The combined P1/P2/P3 console suite passes 43/43 tests. Eight doctrine cases cover deterministic exclusive assignment, weapon/post compatibility, destination-and-route pursuit bounds, threatened-sector breach prioritization, safe relief, inner fallback after invalid outer posts, stable reassignment without oscillation, and deterministic routine support reassignment into combat. Six encounter cases cover deterministic validation, opaque-wall avoidance and approach coverage, unroofed/reachable mortar geometry and finite supplies, operating-reserve-only stock metadata, zero mortar electrical load, and tribal exclusion.

### Build and static checks

Repository-prescribed Docker Release build passed with zero warnings and zero errors after the P3.4 changes. The combined suite passed 43/43 again. XML validation and `git diff --check` passed immediately before the closeout commit.

### Core runtime observations

The focused Core run used a fresh disposable developer quick-start save with Harmony, Core, RIMAPI 1.10.0, and Realistic Bases. RIMAPI was test instrumentation only and is not a mod dependency. A generated industrial encounter initialized four defenders, eleven posts, one unpowered mortar, 12 HE shells, two incendiary shells, and three total reinforced barrels including the installed barrel. Initial `Routine` duties assigned exactly one support worker, one mortar operator, and two ranged posts; playable arrival changed the same four exclusive assignments to one mortar operator and three ranged posts in `HoldSectors`.

The first live containment pass exposed `JobGiver_WanderNearDutyLocation` crossing rectangular defended bounds near edge posts. Replacing only the P3 fallback wander with an assigned-post hold job removed the excursions. Three pre-save and five save/reload-period audits then reported `outsideBounds=0`, no recovery events, no invalid-post recoveries, and `playerPawnsControlled=0`. This is a settlement-duty correction, not a broad pawn-AI patch.

The mortar loaded and fired physical stock under vanilla jobs. Before save, the audit observed one HE and one incendiary round consumed and an incendiary round physically loaded. The active encounter saved at tick 5039 and reloaded with `HoldSectors`, four defenders, four assignments, 11 HE remaining, and `Shell_Incendiaryx1` still loaded. After reload, the mortar resumed manning/firing and later reported 10 HE and one incendiary remaining. The measured connected-network net gain during combat was 80 W; the added mortar continuous draw remained 0 W. No ammunition, barrel, food, fuel, or medicine was regenerated by P3.

RimWorld's native save loader succeeded through dedicated disposable P3 debug actions. RIMAPI's `/api/v1/game/save` endpoint returned an internal null-reference error in this environment and was not accepted as persistence evidence.

### P3.4 Core encounter matrix

The final Core matrix used the same isolated profile and a drafted test pawn so target movement could not blur sector or fire-risk observations. Moving that pawn to four distinct cells outside the generated bounds produced explicit `north`, `east`, `south`, and `west` duty transitions. The long-range/harassment positions retained local post assignments; every sampled audit reported `outsideBounds=0` and `playerPawnsControlled=0`. Setting local time to 23h preserved the same containment. The audit's fire-risk gate changed between `incendiarySafe=True` at separation and `False` near the settlement; physical incendiary and HE consumption had already been observed in the focused P3.3 battle.

Deterministic scenario actions then exercised failures without introducing free resources:

- destroying one generated gate changed `HoldSectors` to `LocalResponse` with `breached=True`;
- removing cover around all outer posts changed the controller to `InnerFallback`, assigned all viable defenders inside, and recorded invalid-post recoveries;
- draining one non-mortar defender assigned exactly that pawn `NeedsRelief` while the remaining defenders stayed assigned;
- exhausting loose and loaded shells produced `he=0`, `incendiary=0`, `mortarLoaded=none`, and no replacement stock;
- killing the current mortar operator reduced the live roster from four to three, immediately reassigned another defender as mortar operator, and subsequent audits observed further real shell consumption;
- removing the wood generator changed measured connected-network gain from 80 W to 0 W while the mortar continued to report 0 W added continuous draw.

One randomized map exposed an invalid southwest post before any settlement mutation. Planning had been called outside the spawner's recovery boundary, so the rejection surfaced as a generation exception. `TrySpawn` now converts an invalid P3 encounter plan into a clean `encounter_plan_rejected` failure before mutation. A rebuilt subsequent fresh generation completed normally. This is rejection containment, not a relaxation of post validation.

The clean tribal pass initialized four defenders and ten posts with `profile=tribal-encounter-v1`, `mortarPresent=False`, zero HE/incendiary/barrel stocks, `netGainWatts=0.0`, `tribalClean=True`, and zero prohibited defenses.

### Geological Landforms / Map Preview regression

The final disposable compatibility profile loaded Harmony, Core, HugsLib, Geological Landforms 1.7.13.1, Map Preview, RIMAPI 1.10.0, and Realistic Bases. Full generation accepted an industrial plan and initialized four defenders, eleven posts, a finite supplied mortar, and the P3 controller. Periodic combat audit reported `outsideBounds=0`, `playerPawnsControlled=0`, and no exception. The final terrain checksum changed after the order-400 settlement step, consistent with the already accepted later-mutator behavior; edge reachability and the no-trap/no-IED invariant remained true. The disposable profile was restored to Core + RIMAPI after the check.

## Exit decision and remaining boundaries

P3 passes its exit criteria. A newly generated supplied encounter uses usable exclusive posts, responds to all tested approach sectors and local breaches without unrestricted pursuit, consumes finite physical ammunition, survives active save/reload, handles relief/post/operator/power/ammunition failures, preserves tribal exclusions, coexists with the supported GL/Map Preview stack, and never assumes control of the player pawn.

This does not claim full unload/revisit defender identity, trade-stock ownership/reconciliation, long-term sustainability, specialized tactics, or P4 terrain/fortification variants. Those boundaries remain assigned to P5, post-MVP work, P6, and P4 respectively. No P4 or P5 implementation has started.
