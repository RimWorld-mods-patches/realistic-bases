---
name: run-rimworld-dev
description: Start this RimWorld mod against an isolated disposable profile, autoload a test save, operate RimWorld's developer UI reliably, and verify runtime behavior from logs. Use for in-game testing, debug actions, save/reload checks, or reproducing runtime faults; do not use it instead of the Docker build workflow.
---

# Run RimWorld for mod development

Use this skill for this repository's macOS runtime tests. Stop the exact disposable game process and verify it exited **before** building changed C# with `build-mod`. RimWorld does not hot-reload mod assemblies; launch a fresh process after every DLL rebuild.

## Start from the disposable save

Keep all mutable RimWorld test state under the repository's ignored `.runtime/` directory. Resolve the current worktree rather than hard-coding a checkout path. Use repository-root variables, `$HOME`, or configured/discovered external paths in skills; never embed a concrete username:

```bash
RB_REPO_ROOT="$(git rev-parse --show-toplevel)"
RB_PROFILE_DIR="$RB_REPO_ROOT/.runtime/rimworld-dev-profile"
RB_LOG_DIR="$RB_REPO_ROOT/.runtime/logs"
RB_LOG_FILE="$RB_LOG_DIR/rimworld.log"
RB_RIMWORLD_APP="${RB_RIMWORLD_APP:-$HOME/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app}"
mkdir -p "$RB_LOG_DIR"
```

The working profile is `.runtime/rimworld-dev-profile`. It contains disposable `Config/`, `Saves/`, and mod runtime data and is excluded by the repository `.gitignore`. It is intentionally separate from the user's normal RimWorld data. Never launch a development test against the normal profile, copy the normal saves into this profile, or commit anything below `.runtime/`.

The profile must have these active mods in `Config/ModsConfig.xml`, in dependency order: Harmony, Core, RIMAPI, RimWorld Debug Actions (`rimapi-debug-actions`), and Realistic Bases. The companion is a separate local `rimapi-debug-actions` repository; locate its actual checkout through the installed mod link or workspace layout rather than assuming a user-specific path. Its presence in `Mods/` does not activate it, so verify `ModsConfig.xml` and the game's loaded-mod log after every profile reset. Optional-mod tests may temporarily add HugsLib, Geological Landforms, and Map Preview to this file; restore the five-mod baseline afterward. RIMAPI and its companion are test instrumentation, not shipped dependencies.

The immutable reset point is `Saves/RealisticBases-P0-Disposable.rws`. If the profile already exists, preserve that file. If the entire ignored profile is absent, create it explicitly with a Core developer quick-test run, enable only the baseline mods above, save the untouched colony as `RealisticBases-P0-Disposable`, stop the game, and only then begin scenario testing. Do not silently substitute a normal user save.

RimWorld accepts the profile directory through `-savedatafolder`, but this setup autoloads a save by naming a disposable copy `autostart.rws`:

```bash
cp "$RB_PROFILE_DIR/Saves/RealisticBases-P0-Disposable.rws" \
  "$RB_PROFILE_DIR/Saves/autostart.rws"
```

Restore that copy before every clean scenario. Do not overwrite the immutable reset point with `autostart.rws` or an active-encounter save.

`RB_RIMWORLD_APP` defaults to the current user's standard macOS Steam library. If the game is installed elsewhere, discover and set its actual app path first; verify the executable exists. Launch it directly. Steam does not need to be running:

```bash
"$RB_RIMWORLD_APP/Contents/MacOS/RimWorld by Ludeon Studios" \
  -savedatafolder="$RB_PROFILE_DIR" \
  -logFile "$RB_LOG_FILE"
```

Keep `-logFile`: it sends the authoritative runtime evidence to the repository-local test log. Use the tested separate-argument form above, not `-logFile=...`; the equals-sign form did not reliably produce the intended log in this setup. This is an argument-format warning, not a prohibition on file logging.

Run it in a long-lived terminal session and retain the session id. Never run two RimWorld processes against the same profile. On macOS the GUI process can survive after the launching terminal session reports exit, so Ctrl-C is **not** proof that the game stopped. Before restoring a save or relaunching, inspect the process table for the exact disposable `-savedatafolder` path:

```bash
ps -axo pid=,command= | rg -F 'RimWorld by Ludeon Studios' \
  | rg -F -- "-savedatafolder=$RB_PROFILE_DIR" | rg -v '/bin/zsh -c|rg -F'
```

If a process remains, verify its full command line matches this profile, then send `kill -TERM` to that exact PID and repeat the process-table check until no match remains. Do not use `killall`, a name-only match, or an unverified PID. Stop and verify the process before rebuilding, editing the profile, changing the active-mod list, restoring the save, or relaunching. After launching, verify exactly **one** process matches this profile. A second match invalidates that run's test evidence; stop both before trying again.

If another RimWorld process is running, the macOS app-name/bundle-ID UI selector may switch to that unrelated window after the disposable process exits (including across a Codex turn boundary). Before every new UI session and after any unexpected screen change, recheck the exact disposable process query above and confirm the screenshot is the disposable game state. If its process is gone, **do not send any more UI input**: the visible window may belong to the user's normal game. Relaunch the disposable process first, then verify the exact profile and screen. An app binding by name alone is not proof of ownership.

Verify startup from the log rather than assuming the visible map is ready:

```bash
rg -n "Command line arguments|Loading game from file autostart|GAME_LOADED|Exception|Error" \
  "$RB_LOG_FILE"
```

## Connect through RIMAPI

The installed [RIMAPI](https://github.com/IlyaChichkov/RIMAPI) mod embeds a local
HTTP API server in RimWorld. It supports RimWorld 1.5-1.6 and listens on
`http://localhost:8765` by default once a colony is loaded; the port is configurable
in `Options > Mod Settings > RIMAPI`. The server is not available at the main menu.
RIMAPI also supports Server-Sent Events (SSE) for live updates and extensions from
other mods; discover the exact routes supported by the installed version with
`GET /api/v1/dev/endpoints` or the linked documentation.

Use `curl` from the host to inspect or control the running disposable game. Use
`localhost`, not `127.0.0.1`: this installation's listener is IPv6-backed. Bypass
host proxy settings and set the base URL explicitly if the port was changed:

```bash
RB_RIMAPI_BASE=http://localhost:8765
curl --noproxy '*' --fail --silent --show-error --max-time 4 \
  "$RB_RIMAPI_BASE/api/v1/game/state"
```

The working debug-action route on installed RIMAPI 1.10.0 is
`/api/v1/debug-actions/debug-actions`, **not** `/api/extensions/debug-actions/...`.
Confirm the companion by listing/filtering actions and checking that the desired
record has `is_runnable: true`. Run its returned ID with a JSON-body POST as
specified in `use-rimapi-debug-actions`. A successful HTTP action invocation
means the debug method ran, not that settlement generation accepted its plan;
check the authoritative log separately. If HTTP times out while the game is
backgrounded or the Mac is locked, bring the verified disposable game window
forward and retry a read-only request. Before retrying a timed-out POST, check
the log: the action may have executed later. One flat generation request in
this profile did exactly that after the HTTP client timed out.

The most useful endpoints for runtime tests are:

- `GET /api/v1/game/state` — current tick, pause state, storyteller, wealth, and colonist count.
- `GET /api/v1/version` — game, mod, and API version information.
- `GET /api/v1/maps` — loaded maps, including map ids and whether each is the player home map.
- `GET /api/v1/map/pawns?map_id=0` — pawns on a map with health, mood, and positions.
- `GET /api/v1/colonists/positions` — lightweight cross-map pawn positions for polling.
- `GET /api/v1/map/terrain?map_id=0` — terrain/floor grid data.
- `GET /api/v1/dev/endpoints` — the endpoint list exposed by the installed RIMAPI version.
- `POST /api/v1/camera/screenshot` — a base64 JPEG of the current game view; useful when a visual assertion is needed.
- `POST /api/v1/game/speed?speed=0|1|2|3` — pause or change game speed.

RIMAPI responses use an envelope such as `{"success":true,"data":...}`. Check both
the HTTP status and the JSON `success` field. JSON bodies and response fields use
`snake_case`; use `map_id`, not `mapId`. Map, pawn, building, and zone ids are integers. Every `POST` must send
a body so that `Content-Length` is present; for an otherwise bodyless request use
`-H 'Content-Type: application/json' -d '{}'`.

For this mod, use RIMAPI to confirm the game is alive, find the active map and pawns,
poll positions, inspect screenshots, pause/restart the simulation, and perform
vanilla setup actions when a test explicitly needs them. The enabled companion
exposes runnable parameterless `[DebugAction]` methods, including this mod's
custom actions, at `/api/v1/debug-actions/debug-actions` and
`/api/v1/debug-actions/debug-actions/run`; read the sibling
`use-rimapi-debug-actions` skill before invoking one. Continue to use the
authoritative runtime log for `GAME_LOADED`, `SPAWN_COMPLETE`,
`PLAN_REJECT`, P3 audits, save/reload restoration, and exceptions. Prefer read-only
GET requests during diagnosis; mutating endpoints such as game load/save, speed,
building/map edits, pawn edits, and `/api/v1/dev/*` can change or corrupt the
disposable save and must be intentional. The dev-tools API is effectively God Mode.
The installed `/api/v1/game/save` route has produced a null-reference failure in this
profile; use RimWorld's native save/load UI or the mod's dedicated debug actions for
save/reload evidence instead of treating that endpoint as reliable.

Useful examples:

```bash
# Confirm the loaded disposable colony and discover its map ids.
curl --noproxy '*' --fail --silent --show-error --max-time 4 "$RB_RIMAPI_BASE/api/v1/game/state"
curl --noproxy '*' --fail --silent --show-error --max-time 4 "$RB_RIMAPI_BASE/api/v1/maps"

# Inspect the playable encounter map after SPAWN_COMPLETE.
curl --noproxy '*' --fail --silent --show-error --max-time 4 "$RB_RIMAPI_BASE/api/v1/map/pawns?map_id=$RB_TARGET_MAP_ID"
curl --noproxy '*' --fail --silent --show-error --max-time 4 "$RB_RIMAPI_BASE/api/v1/colonists/positions"

# Pause before inspecting or changing state through another request.
curl --noproxy '*' --fail --silent --show-error --max-time 4 \
  -X POST "$RB_RIMAPI_BASE/api/v1/game/speed?speed=0" \
  -H 'Content-Type: application/json' -d '{}'
```

After every arrival or save reload, re-query `game/state` and `maps`, resolve `RB_TARGET_MAP_ID` from the encounter you intend to inspect, and confirm the current view/audit targets that map. Do not assume map 0 or `Find.CurrentMap` is the encounter; the home map can still be selected. Recheck pause state as well: explicitly resume for tick-based lifecycle or pawn-movement assertions, then pause before inspecting. A completed generation action does not establish the map selection or simulation speed.

See the [RIMAPI quick start](https://ilyachichkov.github.io/RIMAPI/quick_start.html),
[API reference](https://ilyachichkov.github.io/RIMAPI/api.html), and
[API conventions](https://ilyachichkov.github.io/RIMAPI/developer_guide/api_conventions.html)
when an endpoint's exact query names or request schema matters.

## Control the Unity UI

Use Computer Use and bind the app by bundle id `ludeon.rimworld`. RimWorld exposes almost none of its Unity controls through macOS accessibility, so the reliable developer-action workflow is keyboard driven:

1. Press `slash` to open the developer actions menu.
2. Type the complete action label; the filter field receives text automatically.
3. Press `Tab`, then `Return` to invoke the sole filtered result.
4. Re-query the app state or take a screenshot before the next action.

Do not rely on mouse coordinates for the debug action list. Retina scaling made apparent screenshot coordinates unreliable, while `Tab` then `Return` consistently invoked the filtered action.

Useful keys in the disposable profile:

- `slash`: developer actions menu
- `grave`: developer log window
- `F8`: toggle world/local-map view
- `space`: pause or resume
- `1`, `2`, `3`: normal, fast, and fastest time

If Computer Use reports that the user changed the app, fetch a fresh app state before sending more input. If the developer log opens automatically after an error, inspect the file log first and use `grave` to close the overlay.

## Keep an encounter map loaded

`Generate isolated hostile P0 settlement` is useful for generation-only checks, but a hostile map with no player pawn can unload immediately. Use `Generate playable hostile P0 settlement` for tick-based P2/P3 runtime tests; it moves one colonist from the disposable colony onto the generated hostile map so the map remains active.

Generation can legitimately reject a randomly chosen site. `DEBUG_SETTLEMENT_READY` alone does not prove that the custom settlement spawned. Require `SPAWN_COMPLETE` and the expected P2/P3 initialization events. On `PLAN_REJECT`, retry only within the disposable profile, preferably after restoring the baseline when test isolation matters.

For a P3 runtime pass, monitor the authoritative log:

```bash
rg -n "SPAWN_COMPLETE|PLAN_REJECT|RealisticBases:P3|Exception|Error" \
  "$RB_LOG_FILE" | tail -n 200
```

Use the filtered action `Audit current P3 defensive encounter` for an on-demand audit. Treat counts such as defenders outside bounds, controlled player pawns, mortar ammunition, barrel stock, power delta, and state transitions as test assertions, not merely diagnostics.

## Runtime-test loop

1. Resolve `RB_REPO_ROOT`, `RB_PROFILE_DIR`, `RB_LOG_DIR`, and `RB_LOG_FILE` as shown above.
2. Stop any existing RimWorld process using this profile and verify the exact-path process-table query returns no matches; Ctrl-C alone is insufficient on macOS.
3. Build and run pure tests in Docker through `build-mod`.
4. Restore `autostart.rws` from `RealisticBases-P0-Disposable.rws` for a clean scenario.
5. Launch directly with the repository-local `-savedatafolder` and `-logFile` paths; verify exactly one matching process before interacting with the game.
6. Confirm the expected active-mod list and `GAME_LOADED` in the log.
7. Prefer the companion debug-actions API for a supported action. Use `slash`, exact label, `Tab`, `Return` only if the API cannot serve it; the UI filter depends on the macOS keyboard input source being English.
8. Confirm generation and runtime events in the log; after arrival/reload, recheck target map and pause state before runtime assertions.
9. Pause and stop RimWorld before editing code; repeat from step 2 for the next rebuild/run.

Never infer success solely from a screenshot. The mod log is the source of truth for generation acceptance, controller initialization, save/reload restoration, audits, and exceptions.
