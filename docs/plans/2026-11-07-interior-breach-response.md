# Interior breach response

2026-11-07 · Follow-up to P3: hostile pawns inside the compound

## Goal

When attackers breach the perimeter and enter a warehouse, hospital, or other
interior room, the garrison should respond locally. The response must not send
the whole garrison charging. It must keep logistics and other noncombatants out
of occupied areas, and it may concede the occupied area when fighting is too
costly.

## Agreed behavior

1. Use current attacker positions directly. The danger area includes the room
   occupied by each hostile and a short radius around the pawn for nearby
   approaches.
2. Support, relief, and other noncombat jobs must not enter the danger area or
   route through it. In particular, operating supplies in an occupied warehouse
   are treated as unavailable. Jobs in progress are interrupted if their target
   or route becomes unsafe. A pawn already inside may leave by a route that
   exits the danger area without entering it again.
3. Send a limited combat team to engage compound intruders. Remaining defenders
   keep fallback assignments. Combat stays within the defended bounds and uses
   the existing post-based attack/job logic.
4. Compare ready defender combat weight with intruder combat weight. The initial
   policy requires a 10% defender advantage to fight; otherwise the garrison
   concedes. A conceded incident stays conceded until intruders leave or the
   defender advantage rises above 35%, preventing rapid policy oscillation.
   Defenders stop pursuing and withdraw to separate reachable positions. They
   can return fire from their position when a nearby enemy is visible. Native
   raid behavior governs what attackers do next. The player receives one
   **Garrison concedes** letter per concession, with a link to the occupied area.
5. Clear the danger area when no active intruders remain in the compound and
   allow safe logistics to resume.

## Implementation notes

- Compound penetration already selects `InnerFallback`. This follow-up adds an
  `InteriorResponder` duty for at most half of the living garrison, capped by
  the number of active intruders and excluding hungry, injured, or exhausted
  candidates.
- Danger cells are reconstructed from hostile positions on each controller
  refresh; they are transient and need no save migration.
- The combat weight is a conservative runtime estimate based on attack-verb
  class/range, health, rest, and food. It is a tactical gate, not a claim of
  exact battle prediction.
- Logistics use path checks against danger cells, not only endpoint checks.

## Validation still required

Build evidence is required before commit. Live validation should later cover:

- one attacker in a warehouse with a stronger garrison: a limited response
  attacks while the rest hold safe posts;
- several armed attackers against a weak garrison: one concession letter,
  distinct fallback positions, and no logistics jobs entering the occupied room;
- a support worker already fetching stock when the warehouse becomes occupied:
  the job ends and the pawn withdraws;
- intruders leave: danger clears, the garrison returns to its regular state, and
  safe stock becomes usable again;
- save/reload during an incident: danger and response are reconstructed from
  current pawns without duplicate notifications or stale danger cells.

No live in-game validation is claimed by this change.
