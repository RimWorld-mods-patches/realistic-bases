# P4D quest-location findings

2026-10-01 · P4D.0–P4D.3 complete; P4D.4 open

## Native Core baseline

Installed runtime and pinned source: 1.6.4871 rev597. No DLC. Real Core scripts
were generated in `.runtime/rimworld-dev-profile`, accepted through `Quest.Accept`,
and entered through `CaravanArrivalAction_VisitSite`. The checked process was the
sole game process and used that exact savedata folder. The API companion invoked
the new disposable-only P4D actions. Evidence is in ignored
`.runtime/logs/p4d-native-baseline.log`; native rescue serialization was captured
with the reusable `Saves/RealisticBases-P4D-Active.rws` test slot (later scenarios
overwrite that disposable slot). These actions do not install or replace
quests and do not emit artificial objective-completion signals.

| Contract | Observed native inputs/content | Lifecycle observed |
|---|---|---|
| `OpportunitySite_BanditCamp`, `[BanditCamp]` | Quest 0/site 91, Pirate, Encounter; points 350, loot 550, seed 803333044, expectedEnemyCount -1; four Pirates and one Grenadier_Destructive, native DefendBase Lord; no turret in this random baseline | NotYetAccepted → Ongoing; timeout 720000 disabled by MapGenerated; after debug killing hostile force, native NoActiveThreats at tick 3267 and AllEnemiesDefeated at 3275; EndedSuccess observed 4533; native MapRemoved/Despawned/Destroyed at 4550 after caravan departure |
| `OpportunitySite_PrisonerWillingToJoin`, `[PrisonerWillingToJoin, Outpost, PossibleUnknownThreatMarker]` | Quest 1/site 94, OutlanderRough, Encounter; outpost points 350, loot 550, seed 2079025107; four Town_Guards, Villager, Town_Councilman and one native turret; prisoner Human64188 held before arrival, then same ID spawned in one prison room with one prison bed, original faction/host, full health, no Lord, WillJoinColonyIfRescued true | Autoaccepted Ongoing; timeout 1380000 disabled by MapGenerated; holder empty after original-target transfer; objective reachable with PassDoors, ordinary fog retained; native active save captured. Rescue/release, failure and expiry are later adapter validation, not certified by this baseline |

## Exact source contracts and boundary

`Script_BanditCamp.xml` ends successfully on `site.AllEnemiesDefeated`; Core
`Site.CheckAllEnemiesDefeated` uses `GenHostility.AnyHostileActiveThreatToPlayer`
including dormant/fogged threats and turrets. Site destruction before completion
fails the quest. WorldObjectTimeout fails and is disabled on MapGenerated.
The reward graph owns the choice, original reward objects and delivery. Our
adapter must never send these signals or pay rewards itself.

`Script_PrisonerWillingToJoin.xml` autoaccepts, disables its timeout on arrival,
and marks the pawn reward choice used on MapGenerated. Its NoWorldObject ending
has no Success/Fail outcome. Prisoner death or failure to rescue is not a license
to invent a failure signal. Native custody/freeing/join behavior remains owner.

`Site.ExtraGenStepDefs` yields base extras followed by each ordered part's linked
steps. MapGenerator sorts by order/index and gives each step an independent seed
derived from map seed and SeedPart. Both BanditCamp and Outpost use
GenStep_Outpost (400, SeedPart 398638181) and GenStep_Power (401).
PrisonerWillingToJoin uses its scatterer at1600 (SeedPart69356099). Encounter
inherits MapCommonBase and RocksFromGrid_NoMinerals. Preserve its terrain,
ruins, start/fog work, Site callbacks and supported native caravan entry.

GenStep_Outpost selects the settlement pawn group, threatPoints, inhabitants=true
and OutpostSitePartUtility.GetPawnGroupMakerSeed (parms.randomValue). Its bedCount
uses expectedEnemyCount only as a hint; SymbolResolver_Settlement recomputes kinds
through GeneratePawnKindsExample. Authoritative kinds and actual roster must be
obtained with native generation, once, without ordinary population caps, replacement
gear or added crew. The native roster can include nonfighter inhabitants, as this
six-pawn rescue baseline demonstrates. Static defenses must have a declared
allowance; no hidden ordinary wealth allocation.

GenStep_Outpost reads/adds UsedRects and reads RectOfInterest. The prisoner step
takes the actual pawn from SitePart.things, generates an8×8 prison separately,
spawns the pawn, sets its rescue flag, adds rootsToUnfog, sets RectOfInterest and
refogs the contracted prison. Adapt those obligations using the shared existing
Jail/PrisonBed room, rather than running a second prison step. Preserve holders,+fog roots and generation metadata on rollback. No full-generator recursive
fallback; the original narrow steps must run once in their original seed scopes.

`Site.PostMapGenerate` invokes part workers and starts native detection raids;
`Site.ShouldRemoveMapNow` removes these initial sites when no blocking player
pawn/building remains, including incomplete sites. Re-entry after native removal
is impossible and must not be replaced by ordinary settlement regeneration.

## Strategy selection and support matrix

The user requested strategy pattern during implementation. Use a small explicit
strategy per objective, with shared contract/planning/transaction services:
bandit elimination, then guarded rescue. Each strategy recognizes the full ordered
composition and supplies objective reservations/placement validation. Quest
success, rewards and callbacks are outside the strategies.

Core QuestGen_Sites.GenerateSite **always** appends PossibleUnknownThreatMarker
when the rescue script requests hiddenSitePartsPossible. Source inspection shows
SitePartWorker_UnknownThreatMarker only changes an inspect label; this marker has
no linked generation step or threat/held content. On 2026-10-01 the user explicitly
authorized the exact verified marker in this rescue composition. Hidden actual
threats and all other unknown compositions still stay native.

| Ordered parts | Planned routing |
|---|---|
| BanditCamp | Initial elimination strategy, only verified Core hostile human factions/Encounter/step flags |
| PrisonerWillingToJoin, Outpost, PossibleUnknownThreatMarker | Initial rescue strategy, visible Outpost and exact harmless Core marker only |
| PrisonerWillingToJoin, Outpost | Explicitly recognizable marker-free guarded composition; enable only after contract evidence |
| Any extra part, hidden threat, ambush, turret part, other objective, peaceful/nonhuman faction, DLC map or competing generator | Native unchanged |
| Guarded ItemStash | Follow-on only; outside initial implementation |

P4D.0 adds only disposable native-validation tools and this evidence; production
quest routing remains native until the subsequent checkpoints.

## P4D.1 pure contracts and camp requirements

Added `IQuestLayoutStrategy` with exact bandit and guarded-rescue strategies.
The immutable contract contains complete native roster/gear facts, site and
group seed, actual loot stack reservation and protected-target identity facts.
It rejects unsupported owners/compositions/arrivals, missing holders, duplicate
roster IDs and target inclusion. `expectedEnemyCount` remains descriptive;
the actual roster sets all housing and supply capacities. Counts below four and
above twelve are retained. The explicit implementation bound is32, beyond which
native routing is required without resampling/truncation.

The deliberate compact finite camp profile removes fields, workshops and
unneeded elimination prison cells. Guarded rescue reserves the existing Jail
and PrisonBed. It disallows mortar courts and added static defenses in v1.
Construction/import allowance is explicitly `ceil(threatPoints ×12)`; finite
operating-stock allowance is `ceil(threatPoints ×2)`. Those provisional numbers
are separate from the original loot allowance and native gear value, with no
ordinary wealth roll. Runtime must enforce them; P6 balance remains open.
Native equipment is described by the actual roster and is not recreated,
rerolled or graded against ordinary gear-role/climate rules. Ordinary input
digests and requirements keep their existing behavior when no quest contract
is supplied.

Five focused P4D fixtures passed, including complete deterministic bandit/rescue
geometry through the shared planner, native seventeen-pawn/no-minimum-four
contracts, unsupported composition/arrival/faction, original-prisoner reservation,
and zero-mutation unusable terrain fallback. The ordinary population-budget test
also passed. Production Docker Release build: zero warnings/errors. Full suites
and live adapters are later checkpoints; P4D.1 did not enable production quest
generation.

## P4D.2 native bandit integration

The production adapter now admits only the verified Core BanditCamp composition,
hostile human faction, exact Encounter step inventory and ordinary caravan entry.
Its entry scope surrounds native `CaravanArrivalAction_VisitSite.DoEnter`, including
the queued generation event. Pods, gravships, debug map creation, competing step
inventories and other site compositions remain native. It replaces only Outpost400
and its linked Power401 after a successful shared P4A/P4B/P4C transaction; original
terrain generation, fog/arrival work, Site callbacks and quest graph are retained.

Native Settlement-group generation uses the quest's points, inhabitants flag and
group seed once. The actual original pawns and equipment are borrowed, not replaced
or truncated. The original native stockpile generator produces the exact staged
loot objects from the quest allowance. Its own market-value overshoot is recorded,
not silently clamped or supplemented. There are zero added static threat points,
no turrets/mortars/traps, and no ordinary settlement population/wealth state.
Finite food and startup fuel are checked separately against the explicit operating
allowance. P3 receives only the admitted roster; foreign/captured/slave/player pawns
are excluded from assignments, bounds recovery and job givers.

The shared transaction returns borrowed objects to their exact staging owner before
destroying created structures. Late rollback detaches the provisional Lord and
controller and restores UsedRects, RectOfInterest and unfog roots. Signals are held
inside synchronous provisional mutation, published only on commit and discarded on
rollback. Native fallback reuses the same roster/loot once in the original narrow
Outpost call; no full-generator recursion. Unverified recovery fails closed.

Live evidence (all logs ignored under `.runtime/logs/`):

| Run | Observed result |
|---|---|
| `p4d-bandit-integration.log`, Quest1/site94 OutlanderRough | Native points306.25/group seed-1849110477; six original defenders, seven shared rooms, zero turrets; operating572.2/613. Layout8DCA99C452677D4E and finish2B46A506BB1CBC79 survived active save/reload with identical roster/loot IDs. Native NoActiveThreats3155/AllEnemiesDefeated3169; EndedSuccess5013. Original MedicineUltratech63960×12 and Jade63961 reward objects delivered to home map0 (jade split by native stack limit). Native MapRemoved/Despawned/Destroyed5029 after departure. |
| Same run, Quest2/site97 | Forced failure after base and roster placement; rollback recovered=True; native fallback spawned4/4 of the prepared roster, staging empty. |
| `p4d-bandit-recovery.log`, Quest1/site94 | Forced preflight rejection mutationCount=0; original outpost generation and arrival ran. An earlier tribal test exposed a null fuel-definition lookup; rollback recovered all6 pawns. The no-power lookup was fixed rather than treating fallback as adapter success. |
| `p4d-bandit-late-recovery.log`, Quest0/site91 Pirate | Forced failure after loot, controller and generation-metadata transfer; recovered=True; native fallback4/4, staging empty, native DefendBase ownership. No premature quest outcome. |
| Same run, Quest1/site94 OutlanderRough | Fresh seven-defender camp committed normally. Departure before defeat emitted native destruction/failure, EndedFailed2707; no reward paid. |
| `p4d-bandit-caravan-expiry.log` | Native timeout shortened only for disposable Quest0:30ticks; Core removed the site and ended failed. A fresh Quest1/site92 passed the final caravan guard and committed a four-defender camp. |

Docker Release builds remain zero warnings/errors. Foundation/garrison suite passed
with zero failures. The complete layout regression suite is recorded at closeout;
rescue, terrain/optional-mod matrix and additional lifecycle cases remain P4D.3/.4.

## P4D.3 original-prisoner rescue integration

The exact authorized rescue composition now uses the same shared camp transaction.
It borrows the existing pawn from its original `SitePart.things`, spawns that pawn
in the reserved Jail/PrisonBed room and verifies identity, health, name, tags,
faction/host, no Lord, roofed enclosure and access. It sets the native rescue flag
and unfog root; the original scatterer at1600 applies the native prison refog and
interest rectangle without creating a second prison or target. Its inherited
`Generate` method is patched on `GenStep_Scatterer` with an exact prisoner-step
type guard. The earlier incorrect subclass target caused a startup error; that
run is discarded, and subsequent clean boots exercised the corrected patch.

P3 receives only the native combat roster, never the target. Save/reload stores
the original target reference. Death naturally prunes a destroyed reference; it
does not generate a replacement. Partial native roster preparation fails closed,
rather than falling back with a second sampled roster. Food is a finite 2.7
nutrition per original defender/target (three simple meals), converted using the
chosen food's nutrition. This corrects the earlier item-count reserve without
changing ordinary settlement food or increasing the operating allowance.

| Live evidence | Result |
|---|---|
| `p4d-rescue-fixed-startup.log`, Quest2/site93 TribeRough | Original Human41238, four native guards; one Jail/Bedroll, health1, original host, no Lord. Active reload retained target, roster, loot, layout92129FD789BDBDBF and finish48E593A255945588. After guards were defeated and hostile doors breached through damage, vanilla OfferHelp recruited the same pawn; released-pawn reload retained identity and no P3 control. Departure with both free colonists removed the site natively and ended `EndedUnknownOutcome`. Production does not open hostile doors for this test. |
| `p4d-rescue-recovery.log`, Quest1/site92 TribeRough | Forced failure after original Human41245 transfer, loot, controller and metadata. Rollback restored original holder, health1 and rescue flagfalse, UsedRects0/roots0/no interest key. Native fallback used the same target and all4 staged defenders once, staging empty, one native prison. |
| Same log, Quest2/site95 OutlanderRough | Original Human70239, four native guards, one planned prison, layout500A74DFD210D465; fifteen simple meals /13.5 nutrition, operating257.2/475. Normal industrial rescue committed and suppressed only the native duplicate prison work. |
| `p4d-rescue-final-lifecycle.log`, Quest0/site91 TribeRough | Original Human41233 was killed. Quest remained Ongoing after normal ticks and active reload; no replacement pawn appeared. Departure removed the site and ended `EndedUnknownOutcome`, exactly as Core specifies. |
| Same log, Quest1/site94 TribeSavage | Normal five-guard rescue committed, original living Human55468 in one prison. Departure without freeing/taking the target removed the site and ended `EndedUnknownOutcome`. |
| Same log, Quest5/site100 Pirate | Exact supported rescue composition, native pre-arrival timeout shortened to30ticks; Core ended `EndedFailed` and removed the site. No artificial signals. |
| Same log, Quest6/site101 Pirate | Unsupported `[PrisonerWillingToJoin, Turrets, PossibleUnknownThreatMarker]` entered through native caravan arrival: five native turrets and the original Human78787 in one native prison; no procedural commit or duplicate prison suppression. |

All valid rescue runs started without Harmony/mod-construction errors. Six focused
P4D tests pass, including12 shared terrain/tech/objective cases, nutrition conversion,
unsupported routing and immutable rejection. Release build: zero warnings/errors.
Compatibility and full regression closeout remain P4D.4.
