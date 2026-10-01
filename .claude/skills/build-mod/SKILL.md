---
name: build-mod
description: Build and test the Realistic Bases RimWorld mod in Docker, and inspect installed vanilla or mod code with the bundled decompiler. Use for C# changes, compilation, pure regression tests, or verifying game signatures and behavior before patching.
---

# Build Realistic Bases

Use the existing Docker SDK and NuGet cache. This host does not require a .NET,
Mono, MSBuild or ILSpy installation. Do not install tooling or start Steam for
this workflow. If Docker is unavailable, ask the user to start Docker Desktop.

Resolve the current checkout, not the main checkout or a sibling project's paths.
Keep skill paths portable: use repository-root variables, `$HOME`, or explicitly
configured/discovered external paths; never embed a concrete username.

```bash
RB_REPO_ROOT="$(git rev-parse --show-toplevel)"
docker version --format '{{.Server.Version}}'
```

Before rebuilding an assembly used by the disposable game, follow
[`run-rimworld-dev`](../run-rimworld-dev/SKILL.md) to stop and verify the exact
profile process. A running game keeps the old assembly; rebuilds require restart.
Do not change normal profiles or mod activation as part of a build.

## Release build and pure tests

```bash
docker run --rm \
  -v "$RB_REPO_ROOT":/mod \
  -v rimworld-nuget:/root/.nuget/packages \
  -w /mod/Source/RealisticBases \
  mcr.microsoft.com/dotnet/sdk:8.0 dotnet build -c Release
```

Output: `1.6/Assemblies/RealisticBases.dll`. The project pins
`Krafs.Rimworld.Ref` to 1.6.4871 and builds net472. Check the actual project file
before changing a version; the installed game, reference package and decompiled
source must agree. Compilation uses reference assemblies, not the Steam install.

Run the complete foundation/garrison and layout suites when shared planning,
generation or AI changes warrant regression coverage:

```bash
docker run --rm \
  -v "$RB_REPO_ROOT":/mod \
  -v rimworld-nuget:/root/.nuget/packages \
  -w /mod/Source/RealisticBases.PlannerTests \
  mcr.microsoft.com/dotnet/sdk:8.0 dotnet run -c Release

docker run --rm \
  -v "$RB_REPO_ROOT":/mod \
  -v rimworld-nuget:/root/.nuget/packages \
  -w /mod/Source/RealisticBases.LayoutTests \
  mcr.microsoft.com/dotnet/sdk:8.0 dotnet run -c Release
```

For a focused layout pass, append `-- --only P4D` (or another matching test-name
fragment). The full layout suite can take several minutes; retain its running
session and wait for the final failure count. Focused/pure passes do not prove
live Harmony startup, generation, pawn movement or quest lifecycle correctness.

Keep `bin/`, `obj/`, generated assemblies and runtime evidence ignored. Preserve
the existing `PrivateAssets="all"` / `ExcludeAssets="runtime"` reference isolation:
only the mod's own assembly belongs in `1.6/Assemblies/`, not game, Unity or
reference DLLs. An existing Mods symlink can expose this checkout to the game;
verify its target before a live run. Do not create/replace installations or
symlinks automatically merely to build.

## Inspect real game code

Prefer the matching pre-generated source when present. Locate the external
`vanilla-decompiled/Assembly-CSharp-1.6.4871` tree through the workspace layout
and set `RB_DECOMPILED_DIR` to its actual path. It commonly sits beside the main
repository; do not assume it sits beside a managed worktree.
Its files have flat namespace-qualified names, such as
`Verse.GenStep_Scatterer.cs`, not namespace subdirectories. Do not copy that large
source tree into this skill.

When source is missing, stale, or from an optional mod, use the included helper.
It uses the pinned `ICSharpCode.Decompiler` library; no `ilspycmd` tool is needed.

```bash
RB_RIMWORLD_APP="${RB_RIMWORLD_APP:-$HOME/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app}"
RB_MANAGED="$RB_RIMWORLD_APP/Contents/Resources/Data/Managed"
docker run --rm \
  -v "$RB_MANAGED":/rw:ro \
  -v "$RB_REPO_ROOT/.claude/skills/build-mod/decompile":/decompile \
  -v rimworld-nuget:/root/.nuget/packages \
  -w /decompile \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet run -c Release -- /rw/Assembly-CSharp.dll RimWorld.GenStep_PrisonerWillingToJoin /rw
```

`RB_RIMWORLD_APP` defaults to the current user's standard macOS Steam library.
If the game is installed elsewhere, discover its actual app path and override
that variable before running; verify `RB_MANAGED` contains `Assembly-CSharp.dll`.

Arguments are `ASSEMBLY TYPE [REFERENCE_DIRECTORY ...]`. For an optional mod,
mount its assembly directory read-only (for example `/moddeps`), keep `/rw`
mounted, and pass the mod assembly path, fully qualified type, `/rw` and
`/moddeps`. Add other installed dependency directories if required; for example,
the local Harmony mod's `Current/Assemblies` mounted as `/harmony` and passed as
another argument. Assembly references use assembly names: GL's bundled
`HarmonyLib.dll` is not a replacement for the referenced `0Harmony.dll`.
Its dependencies need not live beside the target DLL. The helper's
framework/reference resolver prevents the old missing-mscorlib/version 4.0 error
when those installed references are supplied.

Inspect the installed implementation assembly, not `Krafs.Rimworld.Ref` (its
method bodies are stripped). Check namespaces and base classes, not only names
in a binary string dump. Inherited methods matter: the native prisoner scatterer
inherits `Generate` from `GenStep_Scatterer`. Harmony must target the declaring
method and guard the intended instance when that base method serves other types.
Verify patch resolution in a clean game startup; a successful build cannot catch
an undefined runtime Harmony target.

If reference resolution fails, verify mounted paths and installed dependencies;
do not install a host SDK or silently treat incomplete decompilation as proof.
Restore errors belong to Docker/NuGet; runtime errors belong to the disposable
log selected with the separate `-logFile PATH` arguments in `run-rimworld-dev`.
