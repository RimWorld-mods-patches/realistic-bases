# Offline defense harness, milestone 1: threat-field adversary

2026-09-29 · Implements the first measurable slice of
`2026-09-29-offline-defense-harness.md`. That document remains the long-range
design (tick combat, learned attackers, cross-map principle learning); this one
replaces its sequencing for the first milestone.

## 1. Why a different first milestone

The original sequence produces no measurement until a combat catalog, a tick
engine and scripted squads all exist, and then optimizes by evolutionary search
inside a noisy simulator. Most of the attacker's best response against a *fixed*
defense is a shortest-path problem over a threat field, which is exact,
deterministic and cheap. Milestone 1 therefore delivers:

- the planner seam (`LayoutDefensePass.DeferredForOfflineSimulation`);
- a frozen host built from the deferred plan;
- a deterministic, expected-value adversarial evaluator;
- a budgeted optimizer that places the whole defense from scratch;
- reports that show *why* a layout wins (threat map, worst attack route).

An `IDefenseEvaluator` boundary lets a later stochastic tick simulation
re-score finalists without changing the optimizer.

Not in scope: RimWorld runtime changes beyond the planner parameter, learned
neural policies, weather/fire/mortars/healing/repair/resupply, cross-map motif
learning, production-route realizability repair.

## 2. Decisions taken in the brainstorm

| Decision | Choice |
|---|---|
| First milestone | Principle signal fast; faithful tick combat later as a verifier |
| Attacker model | Dijkstra route proposals over a threat field + deterministic mean-field rollout |
| Defense placement | Decided entirely by the optimizer from an empty overlay; no hand heuristic baseline |
| Attack variety | Many seeded random spawn points on the same map; disjoint training and held-out spawn sets |
| Defense vocabulary | Turrets (stage-legal), resident posts, sandbags, barricades, extra walls |
| Hard rule | Every host gate keeps a walkable route from its outside to the map edge |
| References | Empty overlay (residents only) and as-shipped Production plan (own host, labeled) |

## 3. Projects

```text
Source/RealisticBases/Planning/ProceduralLayoutPlanner.cs   LayoutDefensePass + optional Plan() argument
Source/RealisticBases.DefenseSim/          net8.0 exe: library code + CLI runner (Program.cs)
  Maps/MapFactory.cs                       seeded terrain recipes
  Host/SimHost.cs                          deferred layout -> grids, doors, interior, gate access
  Combat/CombatTable.cs                    versioned hand-entered combat numbers (approximations)
  Defense/DefenseOverlay.cs                immutable overlay + legality
  Defense/DefenseBudget.cs                 silver ledger, reference package manifest
  Eval/Visibility.cs                       LOS (reuses P4BFireLane) + directional cover
  Eval/ThreatField.cs                      per-cell expected incoming DPS
  Eval/AttackPlanner.cs                    reverse Dijkstra, route + standoff proposals
  Eval/Rollout.cs                          deterministic mean-field engagement
  Eval/Evaluator.cs                        IDefenseEvaluator, ThreatFieldEvaluator, scores
  Search/DefenseSearch.cs                  seeded simulated annealing with an attack archive
  Reports/Report.cs                        result.json, map.txt, threat.svg
Source/RealisticBases.DefenseSim.Tests/    net8.0 exe; links DefenseSim sources except Program.cs
```

Both link the same pure planning files and `CoreCatalogFixture.cs` as
`RealisticBases.LayoutTests.csproj`. No game references, no NuGet packages
beyond the SDK (JSON through `System.Text.Json`). Run outputs go to ignored
`.runtime/defense-sim/`.

## 4. Planner seam

```csharp
public enum LayoutDefensePass { Production, DeferredForOfflineSimulation }
public static ProceduralLayoutResult Plan(ProceduralLayoutInput input, bool requireJointSite = false,
    LayoutDefensePass defensePass = LayoutDefensePass.Production)
```

In deferred mode the joint-site branch stops after `WithFieldWall` and its
validation, skipping `FitDefenseAccess`, `WithApproachDefense` and the turret
pass, and records diagnostic `defense/deferred`. Production is unchanged;
a test compares Production layout digests with and without the explicit argument.

## 5. Host

`SimHost.Build(terrain, layout)`:

- **Walkable:** terrain Standable or ClearablePlant, not water/natural rock/
  occupied, or any excavated cell; minus host solids.
- **Solid barriers (bashable):** `BuiltWalls`, outer walls, gate band walls,
  exhaust pocket walls, room edge cells, field walls. HP from `WallStuff`.
- **Doors (bashable, closed and locked to attackers, opaque):** gate cells, band
  doors, room doors, field inner/outer gates, sally ports (as-shipped host).
- **Natural rock/occupied terrain:** impassable, opaque, not bashable.
- **Interior:** `Enclosure` and field cells, plus every walled-in open cell
  connected to them (field margins, lanes). An edge-reachable settlement cell
  rejects the host (`host_interior_open`); so does an empty interior.
- **Fronts:** E/N/W/S by angle from the interior centroid. Every spawn and
  every resident post belongs to one front.
- **Gate exits:** for every door that has an exterior-reachable neighbor, that
  neighbor. The overlay must keep each one connected to the map edge.
- **Placeable:** walkable, buildable, unroofed, outside the interior, not on a
  host structure, not on the map edge, not a gate exit.

## 6. Combat table (approximation v1)

Hand-entered, labeled `defense-sim-combat.v1 (source-reviewed approximation)`.
Unknown defNames throw. Weapons: range, minimum range, damage per shot, burst
shots, warmup, burst gap, cooldown, accuracy at touch/short/medium/long
(3/12/25/40 cells, linearly interpolated). Included: attacker weapons, every
Industrial resident weapon from `LayoutLoadoutModel`, and the three turret guns.

Other constants: pawn effective damage-to-down 70; armor multiplier 0.75 for
flak-vest profiles, 1.0 otherwise; move 4.0 cells/s, diagonal ×1.414,
pass-through obstacle +0.7 s; pawn per-cell accuracy factor 0.985, turret 0.99;
cover 0.75 wall/rock, 0.55 sandbags/barricade, 0.25 tree/plant; object HP:
wall 300×stuff, door 160×stuff, barricade 300×stuff, turrets 180/280/400
(mini/autocannon/sniper) with stuff factors for granite 1.7, marble 1.2,
steel 1.0, wood 0.65. Turret ammo = `FuelCapacity × InitialFuelPercent /
FuelPerShot` shots, no rearming. Barrier bash DPS = 5 per pawn (melee; breacher gear out of scope).

## 7. Evaluation

**Visibility.** For each defender, a boolean map over cells within
`max(defender range, 45)`: `P4BFireLane.CanEngage` on the opaque set, with
pawn shooters allowed to lean from a cardinal neighbour. Used symmetrically for
return fire.

**Cover.** A target adjacent (8-neighbourhood) to a cover object gets that
object's cover value when the angle between target→cover and target→shooter
is ≤15°, falling linearly to 0 at 65°. Best cover wins.

**Threat field.** Per walkable cell, the sum over defenders of sustained DPS ×
hit chance × (1 − cover) × attacker armor, within range bands.

**Attack plans.** A plan is (spawn cell, route weight λ ∈ {0, 1, 4}, sniper
mode ∈ {with squad, standoff}, rifle mode ∈ {rush, fight}). For each λ one
reverse Dijkstra runs from the interior boundary over cost = move time +
barrier time (HP / squad bash DPS) + λ × threat × dwell; the route from any
spawn follows the predecessor chain. Standoff cells are exterior cells with
zero threat and the most defenders within sniper range and LOS; snipers take
the nearest by walking distance. Spawns: seeded random cells on the reachable map
edge; 32 training, 32 held-out.

**Rollout.** Deterministic, 0.5 s steps, horizon 300 s, expected damage (no
dice). Squad = 2 snipers + 3 riflemen, normal quality, flak armor. Defenders
focus the visible attacker with the highest expected damage; turrets spend
ammo. The group follows the route; blocked by a barrier it bashes it in melee;
in fight mode it stops while a live defender is visible in range. Overwatch
snipers walk to their standoff cell and shoot defenders, preferring ones that
cannot answer; the riflemen go at once (Overwatch) or 45 s after the snipers
arrive (OverwatchDelayed). A breach is any conscious attacker entering an
interior cell. Outcome: breach, time, attackers down, defenders down, bounded
defender damage, route progress, turret ammo spent. Destroyed barriers do not
reopen sight lines (static visibility).

**Defender controller (frozen while comparing geometry).** Turrets serve every
front. Each ranged resident has one post per front; when a front is attacked
they react after 5 s and walk out from the nearest gate exit (exterior BFS
steps / 4 cells/s), neither shooting nor targetable before arrival.
`Respond` (default): once the squad is within 25 route steps of the barrier it
will bash, residents that cannot engage that bash cell walk (exterior BFS from
their post) to the nearest cell with sight and range to it; out of action while
moving. `Static`: residents hold their posts. Added during implementation: with
static posts an omniscient attacker simply bashes the wall segment farthest
from them, so no geometry mattered.

**Scores.** Attack plans rank: breach > earlier breach > (for failures) route
progress > more defender damage > more surviving attackers. A defense is
evaluated per spawn by its best plan; the annealing scalar (lower is better) is
`1000·breachRate + 300·mean breach margin/horizon + 20·mean survivors/5 +
10·mean progress + 5·mean defender losses + 0.5·cost/limit`. Breach rate
dominates; breach time is the main gradient when every spawn is breached.

## 8. Defense overlay, budget, search

**Overlay:** turrets (def, origin), sandbags, barricades, walls, resident posts.
Legality: stage-legal defs (`TechCatalog.IsLegal`), footprints on placeable
cells, no collisions, gate exits connected, interior not opened, posts on
reachable exterior cells, one post per ranged resident per front (distinct
within a front), caps of 40 walls, 40 barricades, 60 sandbags.

**Budget:** silver = `costList` × market value + stuff × market value, loaded
ammo (`ceil(capacity × initial / multiplier)` fuel items), turret watts at the
per-watt cost of `ChemfuelPoweredGenerator`. The per-stage reference package is
8 mini-turrets + 1 autocannon + 2 sandbags per turret + loaded ammo + watts; its
manifest is printed. Mid cannot build the autocannon but keeps the allowance.

**Search:** seeded simulated annealing (T 2 → 0.02) from residents at random
posts and no purchases. Mutations: add, move (±1–6), remove or retype a turret;
add a 1–4 cell sandbag/barricade/wall line or cover beside a shooter, remove a
structure cell; move a post. 60% of placements are sampled around the exterior
route cells of the latest best responses (per front for posts); this biases
proposals only, evaluation decides. Each iteration evaluates against the archive
of the strongest training plans (up to 3 per spawn); a full sweep over all
training plans runs every 20 iterations and alone crowns the champion.
Held-out spawns are evaluated only for the final report.

## 9. Runner

```text
defense-sim [--recipe plains|obstacles|mountain|narrow|clutter] [--map-seed N]
            [--stage mid|late] [--residents N] [--iterations N] [--search-seed N]
            [--defender respond|static] [--out DIR] [--sweep K] [--trace]
```

Writes `result.json` (config, versions, digests, budget manifest, ledgers,
per-spawn outcomes train/held-out for champion, empty and as-shipped),
`map.txt` (terrain, host, overlay, worst route) and `threat.svg`.
`--sweep K` runs K map seeds × both stages and prints a summary table.

## 10. Tests (DefenseSim.Tests)

1. Seam: Production digest unchanged; deferred has no turret/approach defense,
   has `defense/deferred`, validates.
2. Host: interior not edge-reachable; every door is solid for attackers; a
   gate exit reaches the edge.
3. Visibility/threat: wall blocks; adjacent sandbag facing the shooter reduces
   threat; autocannon zero inside minimum range; lean works around a corner.
4. Rollout: no defenders ⇒ breach at travel + bash time; adding a turret never
   makes the same plan breach earlier; deterministic.
5. Budget: reference package priced exactly from catalog numbers; over-budget
   and Mid autocannon overlays rejected; a wall sealing a gate exit rejected.
6. Search: same seed ⇒ same champion digest; champion never scores worse than
   empty on training spawns.

## 11. Acceptance for milestone 1

- All tests pass in Docker (`mcr.microsoft.com/dotnet/sdk:8.0`), existing
  LayoutTests still pass.
- A runner invocation on plains/Mid/7 residents finishes in minutes and shows
  the champion beating the empty overlay on held-out spawns.
- Output clearly labels approximations and path relaxation.

## 12. Additions after the first results (2026-09-30)

- **Objective.** The attackers are the player's pawns. The attacker's best
  response prefers any breach, then fewest downed, then least damage, then
  earliest. A breach costing ≥3 downed attackers (`--costly`) is a defensive
  success; the headline metric is *cheap breaches* on held-out spawns, and the
  annealing scalar is `1000·cheapBreachRate + 150·(1 − attacker damage/350) +
  50·breachRate + 20·margin + 5·defender losses + 0.5·cost/limit`.
- **Settlement mortar.** From the planner's mortar court; the first Support
  resident crews it instead of holding a post. 12 HE shells, forced-miss
  radius 9, 15 s cycle, indirect fire at attacker cells stationary ≥5 s beyond
  29.9 cells; expected damage (blast/miss)² × 50 × armor per pawn per shell.
  Flags `--mortar`, `--mortar-shells`, `--mortar-miss`.
- **Defender tactics knobs** (`DefenderTactics`): commit distance, maximum
  intercept walk, retreat HP fraction, counter-sniper responders, focus rule.
  `--tune-tactics` grid-searches 288 combinations with the layout frozen, then
  re-optimizes the layout once under the tuned tactics.
- **Budget scale** (`--budget-scale`), ablations and motif extraction in every
  run (`result.json` → `ablations`, `motifs`; full champion overlay saved).
- **Stochastic tick verifier v0** (`--verify-ticks K`): same host, overlay,
  routes, tactics and plans; 0.1 s steps, warmup/burst/cooldown timers,
  per-shot hit rolls, full/half armor rolls, sampled mortar landings; the
  attacker re-chooses its plan per spawn from K paired seeds.

Results and planner recommendations: [`../defense-sim-findings.md`](../defense-sim-findings.md).
