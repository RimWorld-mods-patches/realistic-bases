# Plan: P5B turret-guarded rescue quest

2026-10-13 · Status: **implemented and live-checked** (decisions below). It covers P5B item 7 of
the [implementation plan](../implementation-plan.md#p5b--better-management),
on top of the P4D quest adapter ([P4D plan](../p4d-quest-plan.md)).

## Native contract

Core rescue quests roll `[PrisonerWillingToJoin, Turrets,
PossibleUnknownThreatMarker]`. The reset save always rolls it, and P4D sends
it to native generation today.

- **`Turrets` part.**
  - Industrial factions only (`minFactionTechLevel`), so Pirate and
    OutlanderRough.
  - `turretsCount` = threat points / 45, clamped to 2–11 mini turrets.
    `mortarsCount` is 0–1.
  - The quest text advertises both counts, plus `mortarsCount + 1` enemies.
- **Generation.** `GenStep_Turrets` (order 420) rings the prison's
  `RectOfInterest` with BaseGen `edgeDefense`:
  - sandbags, the turrets, and outdoor lighting;
  - one guard under `LordJob_DefendBase`;
  - per mortar, a manned mortar, its own crew pawn (`LordJob_ManTurrets`)
    and 5–8 harmful shells.

  `TurretsPower` (421) wires them.
- **No `Outpost` part:** no outpost roster, loot or rooms. The prison comes
  from `GenStep_PrisonerWillingToJoin` (1600), which P4D already suppresses
  after placing the original prisoner in our jail.

## Proposed design

- **Strategy** `core-turret-rescue-v1`, matching the exact three-part list.
  - **Roster:** the native guard plus one crew per mortar (`faction.RandomPawnKind()`,
    inhabitant, capable of violence), generated once in a seeded scope. All are
    admitted to P3: the guard holds posts and the crew mans the mortar.
  - **Facilities:** a compact camp with a jail (one prison bed), beds for
    the roster, a small store, and power sized for `turretsCount × 80 W`
    plus the camp load. No loot, as in native.
- **Exact-count guns.**
  - The turret planner needs a field-wall frame, so quest camps get their
    own placer.
  - It puts exactly `turretsCount` mini turrets on the camp's exposed sides,
    spread round-robin. Each has a cover nest toward the outside, a service
    cell reachable from inside and a powered conduit.
  - It puts exactly `mortarsCount` mortars in a mortar court.
  - If either count does not fit, the camp is rejected before any mutation
    and native runs.
- **Supplies:** native-equivalent. Turrets get full barrels and no reserve;
  each mortar gets one stack of 5–8 harmful shells (`TurretGunUtility`).
  The added static threat equals the advertised one.
- **Hook.** A `GenStep_Turrets` prefix replaces steps 420/421 for this exact
  composition; the existing prison suppression covers 1600. Fallback: a
  rejection before mutation leaves native untouched. A late failure rolls
  back created content and destroys the prepared (never spawned) guard and
  crew, because native generates its own, as it would have. The prisoner
  is returned to its holder exactly as P4D does.

## Steps

### R1: Pure
- [x] Strategy, contract facts (`TurretsCount`, `MortarsCount`, roster
      size), requirements (power reserve for the guns, beds, jail).
- [x] Exact-count quest gun placer and validation (counts, cover, service,
      power access); reservations reject unadvertised guns.
- [x] Tests: exact counts for 2/6/11 turrets × 0/1 mortar, deterministic
      replay, a cramped site that rejects, ordinary settlements unchanged.

### R2: Runtime
- [x] `GenStep_Turrets`/`TurretsPower` hook, eligibility, roster and supply
      preparation, spawn, P3 admission (guard posts, crew on mortar).
- [x] Forced failures before mutation, after base and after transfer, with
      native fallback once.

### R3: Live
- [x] A native quest from the reset save: advertised counts equal spawned
      guns, one prison, the original prisoner, guard and crew under P3,
      guns powered and loaded.
- [x] Rescue/join, death of the target, departure, active save/reload.
- [x] Forced late failure, then native fallback. Update P4D/P5 findings.

Results are in the [P4D findings](../p4d-findings.md#p5b-turret-guarded-rescue-2026-10-13).

## Implementation notes

- **Gun budget.** The advertised guns are funded on top of the camp's
  import allowance: construction, loaded fuel and three cover cells per
  mini turret, plus the mortar. Their shells are left out of the operating
  allowance.
- **Turret rules.** Quest guns follow the geometric rules: cover nest,
  service route from a camp gate, blast clearance, fire lane, power and
  defender frontage. They skip the settlement rules for staffing,
  sniper guards and mutual support: native turrets are unstaffed apart
  from the one guard.
- **Mortar.** Courts are exact for quest camps; the kit is mortar-capable
  only when the quest advertises one. A quest encounter profile carries
  the native stack (5–8 HE, fixed by the contract seed) and no spare
  barrel.
- **Small rosters.** The garrison keeps its watch before assigning a
  support worker, so a lone guard stands sentry.
- **Power ramp.** Vanilla switches new consumers on one at a time, so the
  turrets come up over about 600 ticks after arrival, as on a native site.

## Decisions (2026-10-13)

1. **Mortar crew:** native, one crew pawn per mortar plus the guard.
2. **Turret placement:** the camp's exposed sides, with cover and inside
   service.
3. **Supplies:** native-equivalent; full barrels, 5–8 shells per mortar, no
   reserve.
