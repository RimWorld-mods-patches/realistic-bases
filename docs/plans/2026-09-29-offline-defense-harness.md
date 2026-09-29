# Offline adversarial defense harness: wiring plan

2026-09-29 · Design only; no harness implemented by this document.

## 1. Scope and defaults

Build an offline, engine-independent C# simulation around the existing pure
terrain/layout planners. Generate seeded maps, plan a settlement, then pit a
learned five-pawn attack against a budgeted defense. Optimize approaches and
defense layouts alternately, and extract transferable defense principles.

This is not a RimWorld raid/debug action, a game launch workflow, or a new
runtime dependency. It does not revive the postponed in-game assault harness
or mark P4B combat verification complete. The runtime exclusion of a global
adversarial optimizer in `docs/p4b-defense-plan.md` remains intact: this optimizer
is a development tool, with deliberate simulation-only freedoms.

Starting assumptions, configurable and recorded in every experiment:

- Attackers: exactly two `Gun_SniperRifle` pawns and three `Gun_AssaultRifle`
  pawns. Normal-quality gear, fixed equal skills and a fixed armor profile.
  No additional attackers when testing multiple fronts.
- Settlement stages: `IndustrialMid` and `IndustrialLate` (the code's name for
  high industrial). Record requested and resolved stage; exclude downgrades from
  these cohorts rather than silently comparing them as the requested stage.
- Residents: start with seven; test four and twelve separately. Use the actual
  `LayoutLoadoutModel` roster/gear, not seven invented riflemen or one sniper per
  turret. Skills are explicit simulation inputs, not supplied by that model.
- Defense budget: enough for two mini-turrets on each of four sides **plus one
  large turret**, with a declared reference allowance for cover, power, wiring
  and finite ammunition. Price the large slot as `Turret_Autocannon` initially.
  This means eight small turrets plus one large turret's worth of funded
  infrastructure, **not** a quota.
  The optimizer may buy fewer/larger guns, more screens, walls or power instead.
- Optimize defense placement without production access-route restrictions.
  Actual combat movement still respects physical obstacles; no teleporting.
- Initial combat model: clear daytime, no weather, fire, suppression, healing,
  mortar attacks or repair. Existing mortar reservations remain as assets, but
  any mortar operator is handled by an explicit, identical baseline rule in
  every comparison. Add these mechanics later as named fidelity variants.

## 2. What already exists

The existing simulated map is a planning snapshot, not a combat engine.

| Existing code | Use in the harness |
|---|---|
| `Planning/P1Model.cs`: `TerrainSnapshot`, `TerrainCell`, `GridPoint`, `TerrainTraits` | Immutable, seeded input grid |
| `RealisticBases.LayoutTests/Program.cs`: `Build`, `Plains`, `Obstacles`, `Mountain`, `NarrowPass` | Starting terrain recipes; currently private test helpers, not a reusable generator API |
| `RealisticBases.LayoutTests/CoreCatalogFixture.cs` | Existing pure catalog, generated from installed Core XML |
| `Planning/ProceduralLayoutModel.cs`: `ProceduralLayoutInput`, `LayoutRequirements` | Settlement requirements, resources, bounded layout search |
| `Planning/ProceduralLayoutPlanner.cs`: `ProceduralLayoutPlanner.Plan(input, requireJointSite: true)` | Rooms, field, perimeter and today's production defense baseline |
| `Planning/P4BApproachDefense.cs` | Exposed-front discovery, approach samples, existing post/cover candidates |
| `Planning/P4BTurretPlanner.cs` | Baseline guns, ranges, finite fuel, costs and placement diagnostics |
| `Planning/LayoutLoadout.cs`: `LayoutLoadoutModel.Build(input, layout)` | Actual funded resident roster and gear |
| `Planning/P4BFireLane.cs` | Conservative range/opaque-grid prefilter, not a full hit/cover calculation |
| `Planning/P3Doctrine.cs`: `GarrisonDoctrine.Assign` | Optional pure production-style assignment baseline; not the runtime Lord/jobs |

`CatalogDef` includes turret ranges, fuel, footprint, costs, watts and explosion
radius. It does **not** contain a complete combat model: pawn weapon accuracy,
shot timings/damage, material-adjusted hit points and armor are missing.
`P4BRealizedCoverAudit` uses game types and cannot be linked into the offline
runner. Do not mistake either current geometry tests or `combatPower` for DPS.

## 3. Project boundary

Add a sibling project, not files under the distributable mod project:

```text
Source/RealisticBases.DefenseSim/              net8.0 library
  Adapters/                                 planner output -> simulation inputs
  Scenarios/                                seeded maps and cohort manifests
  Combat/                                   mutable episode state and stepping
  Policies/                                 fixed and parameterized controllers
  Optimization/                             alternating best-response search
  Reports/                                  results, replay and explanations
Source/RealisticBases.DefenseSim.Runner/       net8.0 command-line executable
Source/RealisticBases.DefenseSim.Tests/        deterministic fixtures/regressions
```

Mirror the source-link pattern in
`RealisticBases.LayoutTests/RealisticBases.LayoutTests.csproj`: link the same
pure planning files and the generated `CoreCatalogFixture.cs` into DefenseSim.
Add pure P3 files only if using that baseline. Do not reference
`RealisticBases.csproj` (net472, game references), `AI/`, `Generation/`,
`Integration/`, Verse, Harmony or RIMAPI. Do not duplicate/fork planner logic.
A shared compile-items manifest can follow once both test projects need it;
it is not a prerequisite or an excuse for a broad planner refactor.

Generated runs/replays go under ignored `.runtime/defense-sim/`. Keep small
deliberate regression scenarios and their expected results in the test project.
No game executable or Steam is required to run the harness.

## 4. The exact planner seam to add

Today `ProceduralLayoutPlanner.Plan` performs the following inside the accepted
joint-site attempt: `run.Run()` -> validate -> `PlanJoint`/`WithFieldWall` ->
`FitDefenseAccess` -> `WithApproachDefense` -> `P4BTurretPlanner.Plan` ->
`WithTurret` -> validate. There is no public base-only switch today.

Add a narrowly scoped, explicit output mode (names below are proposed):

```csharp
enum LayoutDefensePass { Production, DeferredForOfflineSimulation }

ProceduralLayoutResult Plan(ProceduralLayoutInput input,
    bool requireJointSite = false,
    LayoutDefensePass defensePass = LayoutDefensePass.Production);
```

In deferred mode, return the valid rooms/perimeter/attached field immediately
after `WithFieldWall` and its validation, before `FitDefenseAccess`. Skip only
the approach-defense and turret fitting passes. Preserve normal room/field
geometry, facilities and base-access validation. Preserve existing diagnostics
and explicitly record `defense_deferred`; include the mode in result/cache
identity. Existing callers default to Production, with unchanged results.

For each scenario, retain two outputs:

1. Full Production plan, as an as-shipped baseline with its actual budget.
2. Deferred plan, as the immutable host for equal-budget candidate defenses.

These can differ because Production can grow frontage. Store both digests and
label that comparison honestly. Also construct an equal-budget heuristic
baseline on the **same deferred host** to isolate defense optimization from
base enlargement and budget changes.

Do not strip posts/guns from a completed plan by copying its wall lists: approach
fitting changes field-wall cells, doors, wall returns and material ledgers.
Do not repeatedly call the entire settlement planner for every defense mutation.

## 5. Adapter and simulation state

Introduce an offline `SimDefenseOverlay` rather than forcing candidates into
`P4BTurretPlan`/`P4BPerimeterPost`. Those production types/validators assume short
gate routes, fixed projection templates, service routes and support-group rules;
keeping them mandatory would prevent the experiment requested here.

The overlay contains turret types/origins/footprints, cover objects, limited
perimeter edits/gates, firing positions, initial pawn assignments, purchased
power/ammo and a complete cost ledger. Rooms, field, crops and essential facility
footprints are immutable. Candidate changes rebuild from the original host,
not from a previously mutated candidate. Limited gate/wall replacements must
retain containment at episode start and are charged without scrap-sale rewards.

`SimWorldBuilder` must materialize the host correctly:

1. Copy terrain; open only `layout.Excavation`, preserving other rock/water.
2. Apply roof edits/field clearing relevant to the declared fidelity model.
3. Place actual `BuiltWalls`, field walls, room/compound/field doors and blocking
   fixtures, with stable object IDs. Deduplicate shared wall/door cells; a door
   replaces its wall cell. Use actual cells, not `Enclosure` as a rectangle.
4. Preserve natural walls and classify remaining terrain occupancy explicitly.
   Generic `Occupied` alone does not say whether an object blocks bullets,
   provides low cover or can be destroyed. The map generator must supply a
   companion object layer for trees, boulders and other destructible obstacles.
5. Apply overlay edits, cover, turrets, power and storage objects; initialize
   HP, cooldowns, ammo and connected power state.
6. Instantiate each resident once from the funded loadout and each of the five
   attackers once. Initial defender placement at valid staffed posts is allowed
   in the placement-isolation experiment; no instantaneous moves afterward.

Separate immutable `SimScenario` from mutable `SimEpisodeState`. Track move
progress, doors, downed/dead actors, building HP, shot timers, projectiles,
ammo and destroyed power/cover. Invalidate navigation and LOS caches whenever
an obstacle changes. A single physical pawn cannot staff multiple posts or
answer simultaneous sniper threats from several locations.

The following is wiring pseudocode, **not existing APIs** except the named
current planner/loadout calls:

```csharp
terrain = mapFactory.Generate(mapSeed, terrainRecipe);
input = new ProceduralLayoutInput(requirements, terrain.Snapshot,
    layoutSeed, CoreCatalogFixture.Build(), resources: terrain.Resources);
result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true,
    defensePass: LayoutDefensePass.DeferredForOfflineSimulation);
if (!result.Success) return RecordPlanningFailure(result); // not a defense win
host = SimHostAdapter.Freeze(input, result.Layout, terrain.Objects);
roster = LayoutLoadoutModel.Build(input, result.Layout);
if (!roster.Success) return RecordRosterFailure(roster);
budget = DefenseBudget.FromReferencePackage(stage, combatCatalog);
overlay = defensePolicy.Propose(host, roster, budget);
ValidateSimulationCandidate(host, overlay, budget);
scenario = SimWorldBuilder.Build(host, overlay, roster, attackSquad, combatCatalog);
episode = SimEngine.Run(scenario, attackerPolicy, defenderPolicy, combatSeed);
```

## 6. Random maps and reproducibility

Start with correlated geometric recipes rather than independent cell noise:
open plains, clustered trees/boulders, irregular rock masses, pond/lakeshore,
and narrow approaches. Include long/asymmetric settlements and a south-heavy
open-ground case. Rotate/reflect maps and edge spawns to reveal orientation bugs.
Start with existing 130–150-cell test sizes; include larger sizes separately.

Keep immutable train/validation/test manifests with disjoint map seeds. Split
before optimization; never tune on the final test set. Generate and plan once,
cache the host/roster, then run many attacks against candidate overlays.
Use separate deterministic streams for map, layout, policy mutation, spawn and
combat RNG. Store seed, generator version, catalog/simulator versions, planner
digest, resolved stage and all budget/fidelity settings. Use repeated combat
seeds and paired randomness for comparisons; never expose future RNG to policies.
Use shot-addressed random draws where practical so insertion/reordering of one
actor does not shift every later shot in the comparison.

Planning rejection, invalid spawn geometry, missing combat data and simulator
error are distinct experiment failures, not defended outcomes. Report all
denominators. Prescreen only with documented terrain validity rules and retain
rejected seeds/diagnostics; do not cherry-pick favorable accepted maps.

For each reachable map edge, run the same five-pawn squad from that front,
including corner approaches. Unreachable fronts are recorded, not counted as
successful defenses. Multi-front scenarios split these same five pawns; they
do not spawn five per edge. Both policies see current terrain/visible state and
may replan on destruction. A full-map-information variant is useful as a
declared attacker stress bound; do not mix it with limited-information results.

## 7. Physical rules and breach definition

Use an event/fixed-tick model with consistent movement speed, warmup, burst
spacing, cooldown, weapon range/minimum range and projectile/hit resolution.
Resolve simultaneous actions without persistent attacker-first/defender-first
advantage. Both factions use the same shooting, cover and damage functions.

Create a separate, versioned `SimCombatCatalog`. Reuse the inheritance resolver
from `tools/extract-core-catalog.py` for read-only XML extraction, but add the
missing combat fields in a separate generated fixture. Include pawn/turret
weapons, projectile damage/penetration, range accuracy, shot timings, material
HP/armor, movement and directional cover. Missing data fails explicitly.
Keep this catalog out of the runtime planning catalog unless production needs it.
Source-reviewed approximations must be labeled; do not claim game equivalence.

Use `P4BFireLane` for an initial conservative LOS prefilter. Implement the
simulation's numeric ranges and directional cover separately: integer planning
ranges are insufficient for fractional turret blind zones. Cover must depend
on firing direction and the object's adjacency to the actual target/shooter
geometry; turrets use their defined firing origin, not the best footprint tile.
Track destruction of cover and turret explosion consequences. Attackers can
fire at barriers and adjacent attackers can bash them, with explicit damage
rules. A wall is not removed merely because a route planner prefers that cell.

Define the protected boundary from the **initial** host and overlay. Freeze it
before the episode; destruction must not redefine what counts as inside.
Default attacker victory: the first conscious mobile attacker physically enters
the protected interior through an opened/destroyed barrier. A destroyed outer
wall alone is `barrier_opened`, not a completed penetration through deeper layers.
The connected enclosed field counts as protected interior by default; record
core-only penetration as a secondary result, and offer it as a separate objective
variant. Do not let the optimizer choose or shrink its own victory region.

Stop immediately on first breach, all attackers incapacitated, or time limit.
Record timeout separately from attackers defeated. An interior already reachable
at tick zero is an invalid defensive enclosure, not an impressive fast breach.
No terrain-only sealed/unreachable scenario is credited as defended.

## 8. Budget and path relaxation

Keep a dedicated defense-capital ledger, separate from resident equipment and
the production wood-import limit. Price **all** defensive material at the same
catalog rates, including local stone/wood; locally available walls are not free.
Charge turrets, extra walls/doors, screens, loaded/reserve ammo, storage, purchased
generation, conduits and construction material for those facilities. Disallow
traps/IEDs as in production. Resident gear and the base enclosure are fixed
host costs, not a budget the optimizer can sell to buy guns.

Define a fixed reference package per stage: eight mini-turrets and one autocannon,
specified forward/flank cover per pair and for the large gun, declared short
wall/gate allowance, initial ammo plus one reserve load for all nine guns, and
power/wiring sufficient to run the package alongside base critical loads. The
current catalog's gun-only demand is 790 W; generation sizing must also cover
the host's critical loads. Calculate its cost from the catalog and publish the
exact manifest. Do not calculate a cheaper budget for a map with blocked sides.
The large slot is a capital allowance, not a research grant: current Mid does
not grant `HeavyTurrets` or `SniperTurret`, so it can spend that allowance on more
legal small guns/infrastructure. Late may purchase autocannon or uranium turrets
within the same cost limit. A Mid-with-heavy-research variant must be explicitly
labeled and is not the default Mid cohort.
Report material counts, cost and watts as well as the scalar budget. Also compare
both stages at a common fixed capital limit to separate tech advantage from
stage-dependent reference infrastructure costs.

Do not merely increase `LayoutRequirements.ImportValueLimit`: today's turret
planner can still stop at existing power headroom, short gate frames and sniper
guard requirements. The offline overlay needs its own budgeted power additions
and finite resource model. Power cannot be an infinite always-on boolean.

Relax production **placement** constraints: no required gate-to-post/service
route, no reserved P3 controlled mask, no prescribed short projection shape,
and no hard 35% sample-arc/90% sniper-support heuristic. Keep these as diagnostics
and optional ablations so the experiment can discover whether they help.
Retain legal tech/content, terrain support, no footprint collisions, immutable
host assets, initial containment and complete budget/resource checks.

Movement, LOS, closed doors and terrain never disappear. Begin with initial
staffed positions to isolate geometry. Redeployment uses ordinary physical
navigation. First runs use finite loaded turret ammo without rearming; separately
test an explicit finite-stock resupply policy, whose workers must really reach
the guns. Publish unused reserves rather than granting automatic remote reloads.

Every winning overlay gets a strict production-reachability/service audit later.
Report relaxed upper-bound performance and realizable performance separately;
path-relaxed winners are evidence for principles, not automatically spawnable
plans. Any future production route repair must be re-simulated after it changes
geometry or cost.

## 9. Attacker learning, without damage farming

Start with parameterized squad tactics and derivative-free population search
(evolutionary mutations or cross-entropy search), not a neural RL prerequisite.
Genes/parameters include entry front, breach target, cover/risk path weights,
sniper standoff positions, rifleman push timing, target priorities, split/flank
choice and retreat/reposition thresholds. Controllers choose new legal routes
and targets as the battle changes. They cannot modify HP, ammo or the map.

Maximize a lexicographic outcome:

1. Breach probability across fixed evaluation episodes.
2. For successes, earlier breach time (report failures at the horizon too).
3. Bounded damage to defenders and settlement assets.
4. Attacker survival as a final tie-breaker.

For individual episodes, every breach outranks every failure; among breaches,
earlier completion outranks any additional damage. For failed attacks, capped
damage/progress supplies the search signal. Compute damage from net loss of
initial health/value, counting each asset only up to its original maximum.
No repeated reward for shooting ruins, healing cycles, friendly fire or damage
after breach. An asset's material/HP changes cannot inflate its reward value.

Optional training shaping uses bounded potential change in distance/remaining
barrier work and small per-tick delay cost. Keep it out of reported outcome
ranking. Do not add an unbounded per-shot damage reward to a breach bonus.
If all defensive capability is irreversibly gone in this model, switch to the
fastest remaining feasible breach action/route, preserving physical travel and
barrier damage time; do not instantly award a breach. Add a test where killing
the last defender and then farming an irrelevant building loses to pushing in.

## 10. Defense optimization and robustness

Freeze the host, roster and defender controller while comparing geometry. Mutate
turret number/type/location, screens, projections, limited gate placement,
staffed posts, and power/ammo allocation. Candidate generation should permit
non-gate-adjacent cells under the relaxed placement mode. Existing P4B positions
are seeds, not the entire search space. Repairs preserve physical legality and
the budget; invalid candidates do not run combat.

Minimize worst-front/best-attacker breach rate first; then maximize time to
breach, bounded damage/incapacitation of attackers, and preservation of residents
and assets. Timeouts are not reported as attacker defeats. Publish mean results
and the worst-tail results so sacrificing a side cannot hide behind averages.
Use the same anti-farming/finite-horizon rules for defense damage.

Alternate best responses with an archive:

1. Train attacker candidates against the current defense plus archived defenses.
2. Train defense candidates against the strongest new attacks, archived attacks
   and scripted baselines from every reachable front.
3. Validate shortlisted candidates on unseen validation maps/combat seeds.
4. Retain diverse successful tactics/layouts, not just the latest champion.
5. Freeze both policies and evaluate once on the held-out test corpus.

Start with per-map overlay optimization as an upper bound. Then learn shared
placement weights/motifs across training maps, generating overlays for unseen
maps without test-time outcome feedback. Only the latter supports claims about
general planner principles. Do not run unbounded best-response training per map
inside the runtime mod.

Keep a fixed competent defender policy initially: use real ranges, priorities,
staffing, turret targeting and physical redeployment. Add P3 assignment as a
separately labeled baseline. Jointly optimizing defender tactics comes later;
otherwise a better controller can be mistaken for better geometry.

## 11. Implementation sequence and acceptance gates

1. **Adapter/seam.** Add deferred mode and source-linked projects. Test unchanged
   Production digests, valid frozen hosts, excavation/door/field materialization,
   roster accounting and deterministic replay. No combat/training yet.
2. **Combat kernel.** Add versioned combat fixtures and hand-designed 1v1,
   wall/door, adjacent-cover, turret blind-zone, destroyed-cover/power, burst/ammo
   and explosion tests. Breach/timeout/no-duplicate-staffing and same-tick ordering
   tests must pass before optimizing anything.
3. **Scripted benchmark.** Run two snipers/three riflemen with direct rush,
   sniper screening, weakest-barrier and flanking tactics from every edge.
   Compare as-shipped, equal-budget heuristic and simple symmetric defenses on
   both industrial stages. Save failure replays and benchmark runtime.
4. **Attacker search.** Show faster/more reliable breaches than scripted attacks
   on held-out validation scenarios. Add explicit reward-hacking regressions.
5. **Defense search.** Alternate archived opponents and equal-budget overlays.
   Show held-out improvements and worst-front results, not training champions
   alone. Retest under numeric combat/armor/cover sensitivity variants.
6. **Principles and translation.** Run matched-cost ablations and strict path
   audits. Recommend small production heuristics only if effects survive new
   maps, stronger attackers and modeling uncertainty. No automatic production
   planner replacement or P4B completion claim.

Use progressive evaluation: a small deterministic smoke corpus, then larger
balanced batches and multiple shot seeds for finalists. Benchmark episodes/sec
before choosing generation/population counts. Cache frozen hosts and static
geometry, parallelize independent episodes, and limit total episodes/wall time
explicitly. Evolution need not retrain the whole settlement layout.

## 12. Output needed to discover principles

Each run saves its config, versions, population/archive state and results, plus
event-based replays of champions and failures. A lightweight offline viewer can
render terrain, walls, cover, LOS/ranges, routes, staffed posts, shots, damage,
power loss and breach events without RimWorld. It is an inspection tool, not the
source of scoring or hidden tactical information for policies.

Report breach rate/time by stage/front/map recipe; attacker incapacitations;
defender/turret losses; settlement damage; cover contribution; actual mutual-fire
support; spent/unspent material, ammo and watts; planning/overlay invalidity;
and strict-route realizability. Include paired uncertainty intervals.

Use matched-budget ablations to test turret-to-pawn distance, sniper support,
overlapping fire, heavy-gun dead-ground coverage, bastion projections/flank cover,
gate distance, concentration versus all-front distribution, and blast spacing.
For example, relocating a turret farther from pawns while retaining the same
cost is more informative than simply removing a turret. Keep the attacker
archive fixed for one comparison and also retest with a fresh best response.

Final deliverable: a set of measured, understandable placement rules and saved
counterexamples, with simulation fidelity/path-relaxation caveats. A frequently
seen winning motif is a hypothesis; an ablation that reliably changes breach
outcomes on unseen maps is evidence for a defense principle.
