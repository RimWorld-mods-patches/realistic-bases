---
name: run-rimworld-dev
description: Start this RimWorld mod against an isolated disposable profile, autoload a test save, operate RimWorld's developer UI reliably, and verify runtime behavior from logs. Use for in-game testing, debug actions, save/reload checks, or reproducing runtime faults; do not use it instead of the Docker build workflow.
---

# Run RimWorld for mod development

Use this skill for this repository's macOS runtime tests. Build changed C# first with the repository's `build-mod` skill. RimWorld does not hot-reload mod assemblies, so stop and restart the game after every DLL rebuild.

## Start from the disposable save

Keep all mutable RimWorld test state under the repository's ignored `.runtime/` directory. Resolve the current worktree rather than hard-coding a checkout path:

```bash
RB_REPO_ROOT="$(git rev-parse --show-toplevel)"
RB_PROFILE_DIR="$RB_REPO_ROOT/.runtime/rimworld-dev-profile"
RB_LOG_DIR="$RB_REPO_ROOT/.runtime/logs"
RB_LOG_FILE="$RB_LOG_DIR/rimworld.log"
mkdir -p "$RB_LOG_DIR"
```

The working profile is `.runtime/rimworld-dev-profile`. It contains disposable `Config/`, `Saves/`, and mod runtime data and is excluded by the repository `.gitignore`. It is intentionally separate from the user's normal RimWorld data. Never launch a development test against the normal profile, copy the normal saves into this profile, or commit anything below `.runtime/`.

The profile must have these active mods in `Config/ModsConfig.xml`, in dependency order: Harmony, Core, RIMAPI, and Realistic Bases. Optional-mod tests may temporarily add HugsLib, Geological Landforms, and Map Preview to this file; restore the four-mod baseline afterward. RIMAPI is test instrumentation, not a shipped dependency.

The immutable reset point is `Saves/RealisticBases-P0-Disposable.rws`. If the profile already exists, preserve that file. If the entire ignored profile is absent, create it explicitly with a Core developer quick-test run, enable only the baseline mods above, save the untouched colony as `RealisticBases-P0-Disposable`, stop the game, and only then begin scenario testing. Do not silently substitute a normal user save.

RimWorld accepts the profile directory through `-savedatafolder`, but this setup autoloads a save by naming a disposable copy `autostart.rws`:

```bash
cp "$RB_PROFILE_DIR/Saves/RealisticBases-P0-Disposable.rws" \
  "$RB_PROFILE_DIR/Saves/autostart.rws"
```

Restore that copy before every clean scenario. Do not overwrite the immutable reset point with `autostart.rws` or an active-encounter save.

Launch the installed game executable directly. Steam does not need to be running:

```bash
"/Users/roman_gr/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Contents/MacOS/RimWorld by Ludeon Studios" \
  -savedatafolder="$RB_PROFILE_DIR" \
  -logFile "$RB_LOG_FILE"
```

Run it in a long-lived terminal session and retain the session id. Never run two RimWorld processes against the same profile. Stop the process with Ctrl-C before rebuilding, editing the profile, changing the active-mod list, restoring the save, or relaunching.

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
vanilla setup actions when a test explicitly needs them. RIMAPI does not expose this
mod's custom developer actions or `RealisticBases:*` log events automatically, so
continue to use the authoritative runtime log for `GAME_LOADED`, `SPAWN_COMPLETE`,
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
curl --noproxy '*' --fail --silent --show-error --max-time 4 "$RB_RIMAPI_BASE/api/v1/map/pawns?map_id=0"
curl --noproxy '*' --fail --silent --show-error --max-time 4 "$RB_RIMAPI_BASE/api/v1/colonists/positions"

# Pause before inspecting or changing state through another request.
curl --noproxy '*' --fail --silent --show-error --max-time 4 \
  -X POST "$RB_RIMAPI_BASE/api/v1/game/speed?speed=0" \
  -H 'Content-Type: application/json' -d '{}'
```

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
2. Build and run pure tests in Docker through `build-mod`.
3. Stop any existing RimWorld process using this profile.
4. Restore `autostart.rws` from `RealisticBases-P0-Disposable.rws` for a clean scenario.
5. Launch directly with the repository-local `-savedatafolder` and `-logFile` paths.
6. Confirm the expected active-mod list and `GAME_LOADED` in the log.
7. Invoke the desired debug action with `slash`, exact label, `Tab`, `Return`.
8. Confirm generation and runtime events in the log.
9. Pause and stop RimWorld before editing code; after rebuilding, restart from step 3.

Never infer success solely from a screenshot. The mod log is the source of truth for generation acceptance, controller initialization, save/reload restoration, audits, and exceptions.
