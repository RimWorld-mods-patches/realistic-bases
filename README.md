# Realistic Bases

A RimWorld 1.6 mod that generates terrain-aware, functioning hostile settlements: procedural layouts,
defended garrisons with finite supplies, physical trade stock and trap-free defenses. The generator is on
by default and can be turned off in the mod settings; it changes newly generated ordinary hostile
settlements and supported quest sites only.

Status (2026-10-13): P0–P3, P4D, P4E and P5 are complete for their scoped contracts; P4/P4A/P4B are in
progress and P6–P8 are planned. Baseline: RimWorld 1.6, Core without DLC. Multiplayer is a requirement; see the
[Multiplayer audit](docs/multiplayer-audit.md).

## Install

Download `RealisticBases-<version>.zip` from the
[releases](https://github.com/RimWorld-mods-patches/realistic-bases/releases) (the rolling `main-latest`
pre-release follows `main`) and extract it into RimWorld's `Mods` folder, so that the path is
`Mods/RealisticBases/About/About.xml`. Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077),
loaded before Realistic Bases.

## Development

### Prerequisites

- macOS with RimWorld 1.6 from Steam (the live-test workflow assumes the macOS Steam layout; build and unit
  tests run anywhere).
- [Docker Desktop](https://www.docker.com/products/docker-desktop/): builds and tests run in
  `mcr.microsoft.com/dotnet/sdk:8.0`; no local .NET SDK is required.
- Python 3.9+ and `git`.
- Steam Workshop subscriptions to the test mods
  [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) and
  [RIMAPI](https://steamcommunity.com/sharedfiles/filedetails/?id=3593423732). The third test mod,
  [RIMAPI Debug Actions](https://github.com/RimWorld-mods-patches/rimapi-debug-actions), is downloaded by
  the setup script.

The commands below use bash or zsh syntax.

### Build and unit tests

```sh
eval "$(python3 .shared/rimworld-dev-skills/scripts/dev-env.py --shell)"
docker run --rm -v "$PWD":/mod -v "$RW_NUGET_VOLUME":/root/.nuget/packages -w /mod "$RW_SDK_IMAGE" \
  dotnet build RealisticBases.sln -c Release
docker run --rm -v "$PWD":/mod -v "$RW_NUGET_VOLUME":/root/.nuget/packages -w /mod "$RW_SDK_IMAGE" \
  dotnet run --project Source/RealisticBases.PlannerTests/RealisticBases.PlannerTests.csproj -c Release
docker run --rm -v "$PWD":/mod -v "$RW_NUGET_VOLUME":/root/.nuget/packages -w /mod "$RW_SDK_IMAGE" \
  dotnet run --project Source/RealisticBases.LayoutTests/RealisticBases.LayoutTests.csproj -c Release -- --only P4D
```

The build writes `1.6/Assemblies/RealisticBases.dll`. The layout suite takes several minutes in full; while
iterating, select tests with `-- --only <name>`. CI runs everything on every pull request.

### Live-test setup (once per checkout)

```sh
python3 .shared/rimworld-dev-skills/scripts/setup-dev.py          # report what is missing
python3 .shared/rimworld-dev-skills/scripts/setup-dev.py --apply  # fix it
```

`--apply` creates an isolated game profile under `.runtime/` (never your normal profile), enables exactly
the baseline mods from `.rimworld-dev.json` there, links this checkout and the test mods into the game's
`Mods` folder as `dev-<packageId>`, and downloads RIMAPI Debug Actions. Finally it creates the baseline
save, the untouched quick-test colony every live test starts from: it launches the disposable profile with
`-quicktest`, saves the new colony as `RealisticBases-P0-Disposable` and closes the game again (about a
minute). Close any other RimWorld first; it refuses to run while another game answers on the RIMAPI port.
Run it again whenever it reports something; it changes nothing that is already in place.

Keep Steam closed while running the disposable profile, so Workshop mods are not loaded twice. To create
the baseline by hand instead, launch the profile as in the next section without an `autostart.rws`, choose
**Dev quicktest** in the main menu, save as `RealisticBases-P0-Disposable` and quit.

### First live test

```sh
eval "$(python3 .shared/rimworld-dev-skills/scripts/dev-env.py --shell)"
cp "$RW_BASELINE_SAVE" "$RW_PROFILE_DIR/Saves/autostart.rws"
"$RW_RIMWORLD_APP/Contents/MacOS/RimWorld by Ludeon Studios" \
  -savedatafolder="$RW_PROFILE_DIR" -logFile "$RW_LOG_FILE" &
# Wait until the colony has loaded (the log shows event=GAME_LOADED), then:
python3 "$RW_SHARED_ROOT/skills/use-rimapi-debug-actions/scripts/run_debug_action.py" \
  "Generate playable flat P4 settlement" --base-url "$RW_RIMAPI_BASE" --log-file "$RW_LOG_FILE" \
  --wait-event 'event=PROCEDURAL_SPAWN_COMPLETE\b' --fail-event 'event=PROCEDURAL_(PLAN|SPAWN)_REJECT\b' \
  --request-timeout 120 --wait-timeout 300
```

A pass prints `completion_evidence_observed` with the `PROCEDURAL_SPAWN_COMPLETE` line. Every other live
check, its debug action and its acceptance evidence are in [.rimworld-dev.md](.rimworld-dev.md).

### Agent skills

`.claude/skills` and `.agents/skills` link the shared workflows vendored at
`.shared/rimworld-dev-skills` ([source](https://github.com/RimWorld-mods-patches/rimworld-dev-skills)):
`build-mod`, `run-rimworld-dev`, `use-rimapi-debug-actions` and `version-bump`. Shipped changes on `main`
need a version bump in `About/About.xml` and the project together; the version guard hook in
`.claude/settings.json` enforces it for Claude Code.

### Branches and releases

Work happens on feature branches merged into `main` through pull requests. Every push to `main` publishes
the `main-latest` pre-release; a `v<version>` tag matching `<modVersion>` publishes a release.

## Documentation

- [High-level design and feasibility](docs/design-feasibility.md)
- [Implementation plan and acceptance criteria](docs/implementation-plan.md)
- [Progress tracker](docs/progress.md)
- [Research notes and source references](docs/research-notes.md)
- [MVP supply horizon, trade stock and deferred sustainability](docs/supply-and-mvp-scope.md)
- [Multiplayer audit](docs/multiplayer-audit.md)
- Findings: [P0](docs/p0-findings.md), [P1](docs/p1-findings.md), [P2](docs/p2-findings.md),
  [P3](docs/p3-findings.md), [P4](docs/p4-findings.md), [P4D](docs/p4d-findings.md),
  [P5](docs/p5-findings.md)
