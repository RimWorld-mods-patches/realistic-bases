---
name: use-rimapi-debug-actions
description: Use RIMAPI's debug-actions extension to discover, filter, and invoke supported RimWorld debug actions from HTTP clients or automation.
---

# Use the RIMAPI debug-actions API

Use this skill when automating or troubleshooting the RIMAPI debug-actions extension. It applies to debug actions from any loaded RimWorld mod; do not assume an action belongs to a particular mod.

## Requirements

- RimWorld must be running with a game loaded and RIMAPI enabled.
- The debug-actions companion mod must be enabled after RIMAPI.
- Use RIMAPI's configured HTTP address and port. Its default is `http://localhost:8765`; it can be changed in RIMAPI settings. This macOS installation listens on IPv6 loopback, so use `localhost`, **not** `127.0.0.1`, and bypass proxy settings with `--noproxy '*'`.
- With the installed RIMAPI 1.10.0, the extension is namespaced under `/api/v1/debug-actions`. Confirm the mounted route with `GET /api/v1/dev/endpoints` if the installed version changes; do not infer it from the extension ID alone.

## Discover and filter actions

List all actions:

```sh
curl --noproxy '*' --fail-with-body --max-time 8 \
  'http://localhost:8765/api/v1/debug-actions/debug-actions'
```

Filter by a case-insensitive substring in the action name, category, assembly, declaring type, signature, or ID:

```sh
curl --noproxy '*' --fail-with-body --max-time 8 --get \
  --data-urlencode 'filter=Realistic Bases' \
  'http://localhost:8765/api/v1/debug-actions/debug-actions'
```

The list response has `count`, `filter`, and `actions`. Each action record includes a stable `id`, `name`, `category`, `assembly`, `declaring_type`, `signature`, and `is_runnable`. Use the returned ID as the execution selector; do not derive it from the display name. IDs are URL-safe Base64 identifiers and should still be passed as URL-encoded query values.

## Invoke a listed action

Only run an action when its record has `is_runnable: true`. Invoke it with POST and an empty JSON object body:

```sh
curl --noproxy '*' --fail-with-body --max-time 120 -X POST \
  -H 'Content-Type: application/json' \
  --data '{}' \
  'http://localhost:8765/api/v1/debug-actions/debug-actions/run?id=ACTION_ID_FROM_LIST'
```

RIMAPI requires a request body on POST requests; `{}` provides the required content length. A successful response contains `success: true` and the action record. Invocation errors contain `success: false` and an error string.

For repeated tests, select the exact runnable action by its returned name, then use its ID rather than copying a long ID from an earlier session:

```sh
RB_ACTION_ID="$(curl --noproxy '*' --fail-with-body --silent --show-error \
  --get --data-urlencode 'filter=Generate playable flat P4 settlement' \
  'http://localhost:8765/api/v1/debug-actions/debug-actions' |
  jq -er '.actions[] | select(.name == "Generate playable flat P4 settlement" and .is_runnable) | .id')"
curl --noproxy '*' --fail-with-body --max-time 120 -X POST \
  -H 'Content-Type: application/json' --data '{}' \
  "http://localhost:8765/api/v1/debug-actions/debug-actions/run?id=$RB_ACTION_ID"
```

In the disposable profile, the verified sequence was: load the save, check `GET /api/v1/game/state`, list actions (43 matched `Realistic Bases`), invoke `Use high-industrial P4 stage once`, then invoke `Generate playable flat P4 settlement`. Both invocation responses had `success: true`, which means the method ran; inspect `PROCEDURAL_PLAN`, `SPAWN_COMPLETE` or `PLAN_REJECT` in the game log to determine whether generation itself succeeded. A rejected map can still have a successful API invocation.

## Supported actions and effects

The extension discovers `[DebugAction]` methods in loaded assemblies. It directly invokes parameterless, static methods returning `void`. It reports other discovered actions with `is_runnable: false`; these can require a cell, a target, an instance, a menu selection, or other input that this endpoint does not provide.

Running an action executes that mod's code in the live game and can change the world or game state. Check its category and signature, use a development save, and invoke only the intended action. The endpoint is an automation interface, not a safety filter.

If the extension route is missing, confirm both mods are enabled, restart/load a game so RIMAPI initializes, and check RimWorld's log for the registered endpoint path. RIMAPI's detailed registration messages require its optional logging setting. If no actions appear, check that the desired mods are loaded and that the companion recognizes RimWorld 1.6's `LudeonTK.DebugActionAttribute` on their methods. A backgrounded/locked game can leave queued HTTP requests unanswered. Bring the **verified disposable** game window to the foreground and retry a read-only `game/state` request before diagnosing a route problem. A timed-out mutating POST may execute later when the game resumes: inspect the game log for that action and its effects **before** sending it again. Distinguish timeout from 404 and an empty action list.
