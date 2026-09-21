---
name: run-rimworld-dev
description: Start this RimWorld mod against an isolated disposable profile, autoload a test save, operate RimWorld's developer UI reliably, and verify runtime behavior from logs. Use for in-game testing, debug actions, save/reload checks, or reproducing runtime faults; do not use it instead of the Docker build workflow.
---

# Run RimWorld for mod development

Use this skill for this repository's macOS runtime tests. Build changed C# first with the repository's `build-mod` skill. RimWorld does not hot-reload mod assemblies, so stop and restart the game after every DLL rebuild.

## Start from the disposable save

The working test profile is `/tmp/realistic-bases-p0-core`. It is intentionally separate from the user's normal RimWorld data. Do not test against or overwrite the normal profile.

RimWorld accepts the profile directory through `-savedatafolder`, but this setup autoloads a save by naming a disposable copy `autostart.rws`:

```bash
cp /tmp/realistic-bases-p0-core/Saves/RealisticBases-P0-Disposable.rws \
  /tmp/realistic-bases-p0-core/Saves/autostart.rws
```

Launch the installed game executable directly. Steam does not need to be running:

```bash
"/Users/roman_gr/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Contents/MacOS/RimWorld by Ludeon Studios" \
  -savedatafolder=/tmp/realistic-bases-p0-core \
  -logFile /tmp/realistic-bases-runtime.log
```

Run it in a long-lived terminal session and retain the session id. Stop that process with Ctrl-C before relaunching after a build. Recreate `autostart.rws` from the disposable source save when a test must return to the original baseline.

Verify startup from the log rather than assuming the visible map is ready:

```bash
rg -n "Command line arguments|Loading game from file autostart|GAME_LOADED|Exception|Error" \
  /tmp/realistic-bases-runtime.log
```

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
  /tmp/realistic-bases-runtime.log | tail -n 200
```

Use the filtered action `Audit current P3 defensive encounter` for an on-demand audit. Treat counts such as defenders outside bounds, controlled player pawns, mortar ammunition, barrel stock, power delta, and state transitions as test assertions, not merely diagnostics.

## Runtime-test loop

1. Build and run pure tests in Docker through `build-mod`.
2. Stop any existing RimWorld process.
3. Restore `autostart.rws` when a clean baseline is required.
4. Launch directly with `-savedatafolder` and `-logFile`.
5. Confirm `GAME_LOADED` in the log.
6. Invoke the desired debug action with `slash`, exact label, `Tab`, `Return`.
7. Confirm generation and runtime events in the log.
8. Pause before editing code; after rebuilding, restart from step 2.

Never infer success solely from a screenshot. The mod log is the source of truth for generation acceptance, controller initialization, save/reload restoration, audits, and exceptions.
