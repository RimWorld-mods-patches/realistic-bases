# Multiplayer audit

Audited 2026-10-13 against the installed Multiplayer mod (`rwmt.multiplayer`,
1.6 build) and the runtime code in `AI`, `Core`, `Generation`, `Integration`
and `Persistence`. Pure planning code was checked for nondeterministic inputs.
Two-client tests are pending; they happen in the user's playtest.

## How Multiplayer runs this mod

- Every client runs the same simulation from the same save, executing synced
  commands at the same tick. Map generation (caravan arrival, debug
  generation) therefore runs on every client and must produce identical maps.
- On join, Multiplayer can copy the host's mod config files to the client
  (`syncConfigs`). It does not sync config changes made during a session.
- Debug actions are synced: Multiplayer runs a debug-action node on every
  client (`DebugActionNodeEnter`, `DebugSync`). One-shot static flags that
  debug actions set are therefore identical on all clients.
- `Multiplayer.API.MP.IsInMultiplayer` (shipped in the mod's
  `0MultiplayerAPI.dll`) reports a running session. Realistic Bases reads it
  by reflection (`MultiplayerSession`), with no assembly reference.

## Findings and fixes

| Finding | Risk | Fix |
|---|---|---|
| The generator switch was a per-client mod setting read during generation (`Settlement.MapGeneratorDef` patch, P4D eligibility). | A client with a different setting generates vanilla's base while the host generates ours. | The game saves its own copy (`P0WorldState.generatorEnabled`, always written). Single player adopts the mod setting on load and whenever it changes; a Multiplayer session keeps the saved host value. The settings window says so during a session. |
| The settings window had "force next rejection" and "force next partial failure" checkboxes, and all force flags were saved in the mod config. | A window click sets the flag on one client only. | The checkboxes are removed (debug actions arm the same flags and are synced). The force flags are no longer saved. |
| `P3ServiceRetry` cooldowns (300 ticks) were not saved. | A reload changed service decisions for up to 300 ticks. Multiplayer reloads every client from the same save, so this was a reload difference rather than a desync, but it is now exact. | Pending entries are saved with the garrison controller. |
| The `Thing`-keyed sale-stock and reserve caches rebuild lazily without destroyed things, so a destroyed thing could be tracked or not depending on when the rebuild ran (a UI read can trigger it). | A lookup for a destroyed thing could differ between clients. | Lookups return nothing for destroyed things. |

## Checked and found safe

- **Timing:** `Stopwatch` in P1/P2 planning feeds logs only. The planners
  stop on fixed candidate budgets, never on time.
- **Randomness:** the planners use derived stable seeds (world seed, tile,
  faction, profile). The quest adapter uses `Rand.PushState` with a quest
  seed. There is no `System.Random`, `Guid`, clock or `GetHashCode`
  input to a decision. The one `GetHashCode` call is in a log line.
- **Floating point:** no transcendental maths (`Pow`, `Sin`, `Exp`, `Log`,
  and so on) in planning or runtime decisions. Basic IEEE operations are
  identical across platforms.
- **Collections keyed by reference:** `Dictionary`/`HashSet` enumerate in
  insertion order, not hash order. Every client performs the same
  operations, so iteration order matches.
- **Static state:** the generation contexts (`P4DQuestGeneration`
  `CaravanEntrySite`, `NativeActive`, `Runs`; `P4DProvisionalSignals`) live
  only inside one synchronous generation or are keyed by map. The P4A
  catalog cache is a pure function of the loaded defs and difficulty. The P1/P2
  records and `P2RuntimeAudit` serve diagnostics only.
- **Client-local reads:** runtime code reads no `Find.CurrentMap`,
  `Find.Selector`, camera, `Prefs` or UI state. Only debug actions do,
  under Multiplayer's debug-action sync.
- **UI that changes state:** the mod adds no gizmos, float menus, tabs or
  dialogs. The trade-stock getter patch changes state only when vanilla's
  own getter regenerates stock. It follows vanilla's Multiplayer behaviour.
- **Garrison controller:** all decision state is saved. Threat severity
  and intrusion flags are recomputed before use. The field-assault probe
  fields feed logs only.
- **Threads:** none.

## Not supported

Developer probes assume one disposable profile (`P4DQuestDebugActions.Disposable`
is per client) and are not Multiplayer tools.

## Two-client playtest checklist

1. Host and client have identical mod lists. Join with config sync on.
2. Caravan to an ordinary hostile settlement: no desync on arrival. The
   layout is identical on both clients.
3. Assault it: the garrison alarm, Watch, sector moves and mortar fire
   cause no desync.
4. A turret-guarded rescue quest site: arrive, rescue, leave.
5. Trade with a settlement, then attack it: the physical sale stock
   matches on both clients.
6. Leave and revisit a settlement (archive/restore).
7. Change the generator setting on the client mid-session: generation
   still follows the host's saved value.
