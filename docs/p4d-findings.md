# P4D quest-location findings

2026-10-01 · P4D.0–P4D.1 complete; P4D.2–P4D.4 open

## Native Core baseline

Installed runtime and pinned source: 1.6.4871 rev597. No DLC. Real Core scripts
were generated in `.runtime/rimworld-dev-profile`, accepted through `Quest.Accept`,
and entered through `CaravanArrivalAction_VisitSite`. The checked process was the
sole game process and used that exact savedata folder. The API companion invoked
the new disposable-only P4D actions. Evidence is in ignored
`.runtime/logs/p4d-native-baseline.log`; native rescue serialization is preserved
as `Saves/RealisticBases-P4D-Active.rws`. These actions do not install or replace
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
and live adapters are later checkpoints; P4D.1 does not enable production quest
generation.
