# Plan: P5B garrison threat states, sleep rotation and redeploy

2026-10-13 · Status: **implemented and live-checked** (decisions below). It covers P5B items 1–3 of the
[implementation plan](../implementation-plan.md#p5b--better-management).
Items 4–6 (cover, wall lights, Neolithic shelves) are done or in review.
Item 7 (turret-guarded rescue) is a separate quest checkpoint after this one.

## Problems today

The garrison code lives in `AI/P3Garrison.cs` (runtime) and
`Planning/P3Doctrine.cs` (the pure `GarrisonDoctrine.Assign`).

- **Item 2, threat states.** The only states are `Routine` (no hostile on
  the map) and the assault family (`HoldSectors`, `LocalResponse`,
  `InnerFallback`). `threat` is "any hostile pawn spawned". A player caravan
  that has just arrived and has not attacked yet therefore puts every
  defender on a post at once.
- **Item 1, sleep rotation.**
  - `ReliefCandidates` lets `n − 1` defenders rest at the same time in
    `Routine`, so most of them sleep together at night.
  - In the assault states, up to `n − minimumOnDuty` may still go to bed.
    The only condition is that no hostile is within 12 cells of the support
    centre.
  - Nothing wakes a sleeper when an assault starts.
- **Redeploy.** `SelectPost` ranks `Engagements` above `CoversSector`, and
  `stable_assignment` holds a post for 300 ticks. The live one-side probe
  staffed slots that hold the west rather than the nearest west and field
  posts. When the attacked side changes, the pack follows only after a delay,
  and only partly.
- **Item 3, odd walk.** The `RB_P3_Support` and `RB_P3_Relief` duties fall
  back to `JobGiver_P3HoldAssignedPost`. For those roles the assigned
  destination is `SupportCenter`. After eating or sleeping, a defender walks
  to the support centre and stands there. That is the strange "internal
  post".

## Proposed design

### Threat states (pure, in `P3Doctrine`)

| State | Trigger | Staffing |
|---|---|---|
| `Routine` | No hostile on the map. | A **watch** of 1 on the most exposed side's posts. Everyone else is off duty. |
| `Watch` (new) | Hostiles present, no hostile action yet. | Mortar crews, plus a watch of **⌈pack/3⌉, at least 2**, on posts that cover the likely side (the existing `ObserveThreatSector`, toward the hostiles). Everyone else is off duty. |
| `HoldSectors` / `LocalResponse` | **Assault**: a hostile harmed a resident, a resident's building or wall, or a turret; a hostile has fired within the last 600 ticks; a hostile entered the field or the compound; or a hostile came within **35 cells** of the perimeter. | **Everyone is alarmed**: sleepers are woken and rest relief is suspended. The **whole ranged pack** goes to the attacked side. |
| `InnerFallback` | As today. | As today. |

- The assault latches. It falls back to `Watch` only after **2,500 ticks**
  (one in-game hour) with no new assault signal.
- Mortars are crewed in `Watch` and fire natively at enemies. In vanilla a
  caravan can enter a hostile settlement's map only through the "Attack"
  arrival, so any hostile there is an attacker, and the first shell starts
  the assault. `Watch` covers the time between the attackers arriving at the
  map edge and their first action or approach.
- New `GarrisonSituation` input: `AssaultSignal` (bool) and the tick it was
  last seen. The runtime computes it from `mindState.lastAttackTargetTick` on
  hostiles, from harm to residents (`lastHarmTick`), from the perimeter
  hit-point watch that already exists, and from the intrusion flags.

### Off-duty role and the odd walk (item 3)

- Add an `OffDuty` role and an `RB_P3_OffDuty` duty: bounded native needs,
  then an idle wander inside the compound near the pawn's own bed, the dining
  room or the support room. The duty has **no** `HoldAssignedPost` fallback.
- `Support` keeps its jobs but ends in the same wander, not the walk to the
  support centre.
- `Relief` ends in the wander too. Only `GateReserve` and the mobile reserve
  keep a staging point.
- When a defender changes role, the next job starts from where it stands. No
  centre waypoint.

### Sleep rotation (item 1)

- In `Routine` and `Watch`, at most **⌊off-duty/2⌋** defenders rest at once.
  The most tired go first (`NeedsRelief` ordered by rest level). The others
  wait until a sleeper is back above 95% rest.
- Watch duty rotates. The doctrine picks watchers among the most rested
  defenders, with hysteresis, so a tired watcher is swapped out before an
  off-duty sleeper is woken.
- In the assault states, no new rest relief is given. Sleepers are woken:
  their `LayDown` job ends and `RestUtility.WakeUp` runs. Eating stays
  allowed at the post (`ThinkNode_P5AwakePostNeeds`). Tending relief for the
  bleeding and the downed is kept.
- Vanilla still forces exhaustion collapse. Rotation only spreads sleep out
  and never stops it entirely.

### Redeploy (pure)

- In `HoldSectors` and `LocalResponse`, **posts that cover the attacked side
  rank first**. Within those, the order is engagements, then nearest by
  route. Posts that don't cover the side are used only once the side's
  covered slots are full (C4 guarantees enough slots for the pack).
- **Cooldown override:** a `stable_assignment` is dropped at once when the
  old post does not cover the current attacked side.
- **Sector hysteresis:** the attacked side must persist for 300 ticks before
  the pack moves (the existing `P3PerimeterThreat.Rank` scoring, plus a
  minimum dwell). This avoids ping-pong between two sides.
- Moves to the new side must use `ResponseRouteAllowed` (inside routes). A
  post on the new side that can't be reached safely is skipped.

## Steps

### G1: Pure doctrine
- [x] `Watch` state and `OffDuty` role. Add `AssaultSignal` and the latch to
      `GarrisonSituation`.
- [x] Watch sizing, mortar crews in `Watch`, and the rested-first watch
      rotation.
- [x] `ReliefCandidates`: the half-cap in `Routine`/`Watch`, no rest relief
      under assault, tending kept.
- [x] `SelectPost`: attacked-side posts first, cooldown override, sector
      dwell.
- [x] PlannerTests:
  - [x] a caravan with no action stays in `Watch` with ⌈pack/3⌉ watchers;
  - [x] the first shot gives `HoldSectors` with the whole pack on that side;
  - [x] the side changing from west to north moves every ranged defender
        after the dwell;
  - [x] at most half the off-duty defenders sleep at night;
  - [x] nobody is relieved to rest under assault;
  - [x] the latch falls back after 2,500 ticks.

### G2: Runtime
- [x] Compute `AssaultSignal` in `RefreshAndApply`. Log `THREAT_STATE`
      transitions with their trigger.
- [x] `RB_P3_OffDuty` duty def with no post fallback. Remove the
      `SupportCenter` walk from Support and Relief.
- [x] Wake sleepers on the transition to assault (logged as `ALARM woke=n`).
- [x] Save/load the new latch fields. Old saves default to the current
      state.

### G3: Live checks
- [x] Arrival without action: `Watch`, the watchers on the likely side, the
      others eating, sleeping and wandering, and no walk to the centre.
- [x] Night: the number of simultaneous sleepers stays at or below the cap.
- [x] The first shot: `ALARM`, sleepers awake, and the pack on the attacked
      side within one transfer.
- [x] The attack moves to another side: the pack follows after the dwell.
- [x] Save/reload in each state.
- [x] Update `p3`/`p5` findings, `progress.md` and the implementation plan.

Results are in the [P5 findings](../p5-findings.md#p5b-garrison-states-live-2026-10-13).

## Decisions (2026-10-13)

1. **Watch size:** ⌈pack/3⌉, with a minimum of 2.
2. **Approach alarm range:** 35 cells from the perimeter.
3. **Routine watch:** one sentry.
4. **Mortars in `Watch`:** crewed, and they fire on enemy factions. The
   first shell or hit makes it an assault. Telling a visit from an attack
   needs no arrival-type hook today, because hostile maps are entered only
   by attack.

## Implementation notes

- **Sleep cap.** Half the off-duty defenders, but never fewer than
  ⌈pack/3⌉ (and at least one). A defender sleeps about a third of the day;
  in the live check, ⌊off-duty/2⌋ alone left the watchers at 2–6% rest.
- **Who watches and who sleeps.** One score: rest level, −0.3 for a sleeper,
  and +1 for a watcher who does not need rest. A sleeper leaves its bed
  only when clearly more rested than the others, and gives it up to a
  defender 0.3 more tired. Mortar crews may be tired; rested crews first.
  The PlannerTests simulate three days of `Watch` with vanilla-like rates.
- **Under assault,** food and tending relief stay (with the existing minimum
  on duty); rest relief stops, and a relieved defender does not sleep.
  Patients' bed rest is separate.
- **Sides.** The first hostile side after `Routine` is taken at once; later
  changes wait for the dwell. The dwell keeps its pending side through
  refreshes in which no side ranks. A defender already on a post covering
  the attacked side keeps it, so the pack does not reshuffle within the side.
- **`AssaultSignal` triggers:**
  - `garrison_fired`: an owned turret or mortar fired (`Verb.LastShotTick`),
    or a resident attacked. The first shell counts even when it misses.
  - `hostile_fired`: `lastAttackTargetTick` of a hostile.
  - `resident_harmed`, `hostile_hit`, `perimeter_hit`.
  - `approach`: within 35 cells.
  - Breach and intrusion count through the existing flags.
- **Saves.** A save without the latch fields that was taken mid-assault
  starts a full latch on load.

## Future (not this step)

- **Friendly settlements.** A visitor map of an allied or neutral faction
  would need `Routine` and `Watch` without hostility (traders, guests), plus
  a trigger for when the visitors turn hostile. Design this when a friendly
  map exists, for example through trading-post or visitor support.
