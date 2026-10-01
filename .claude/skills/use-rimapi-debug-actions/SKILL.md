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

For repeated tests, use the checked [runner](scripts/run_debug_action.py) with Python 3 (standard library only). It resolves the exact name, rejects missing/ambiguous/non-runnable actions before POST, URL-encodes the returned ID, bypasses proxies, sends `{}`, and checks JSON success. Add exact `--category` or `--assembly` selectors if names collide:

```sh
RB_REPO_ROOT="$(git rev-parse --show-toplevel)"
python3 "$RB_REPO_ROOT/.claude/skills/use-rimapi-debug-actions/scripts/run_debug_action.py" \
  'Generate playable flat P4 settlement' \
  --log-file "$RB_REPO_ROOT/.runtime/logs/rimworld.log" \
  --wait-event 'event=PROCEDURAL_SPAWN_COMPLETE\b' \
  --fail-event 'event=PROCEDURAL_(PLAN|SPAWN)_REJECT\b' \
  --request-timeout 120 --wait-timeout 180
```

Choose completion/failure regexes for the actual action; the example above is for procedural P4 generation, not every debug action. Include a map/site identifier when known, serialize mutating tests, and do not count another encounter's completion as evidence. Native generation or quest fallback may need different terminal events. The runner snapshots the log end immediately before POST and reads only newly appended complete lines. Log replacement/truncation fails verification. `--fail-event` is optional/repeatable. `--base-url` overrides the configured address; request and evidence-wait timeouts are separate.

For a synchronous setup action such as `Use high-industrial P4 stage once`, omit the log/wait options. The resulting `invoked_only` status proves invocation only. Generation tests must supply `--log-file` plus `--wait-event`; successful HTTP invocation alone does not prove generation. A timeout or uncertain POST outcome never triggers an automatic retry; inspect logs and game state before another mutation.

Run the runner's isolated mock-API tests without starting RimWorld:

```sh
python3 -m unittest discover \
  -s "$RB_REPO_ROOT/.claude/skills/use-rimapi-debug-actions/scripts" \
  -p 'test_*.py'
```

## Supported actions and effects

The extension discovers `[DebugAction]` methods in loaded assemblies. It directly invokes parameterless, static methods returning `void`. It reports other discovered actions with `is_runnable: false`; these can require a cell, a target, an instance, a menu selection, or other input that this endpoint does not provide.

Running an action executes that mod's code in the live game and can change the world or game state. Check its category and signature, use a development save, and invoke only the intended action. The endpoint is an automation interface, not a safety filter.

If the extension route is missing, confirm both mods are enabled, restart/load a game so RIMAPI initializes, and check RimWorld's log for the registered endpoint path. RIMAPI's detailed registration messages require its optional logging setting. If no actions appear, check that the desired mods are loaded and that the companion recognizes RimWorld 1.6's `LudeonTK.DebugActionAttribute` on their methods. A backgrounded/locked game can leave queued HTTP requests unanswered. Bring the **verified disposable** game window to the foreground and retry a read-only `game/state` request before diagnosing a route problem. A timed-out mutating POST may execute later when the game resumes: inspect the game log for that action and its effects **before** sending it again. Distinguish timeout from 404 and an empty action list.
