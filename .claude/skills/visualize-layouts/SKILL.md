---
name: visualize-layouts
description: Render planner layouts as colour maps and compare two engine versions side by side (PNG sheets and a self-contained HTML page). Use when the user asks to see, visualize or compare generated base layouts, or after a layout-engine change to show its effect.
---

# Visualize planner layouts

The layout tests can export each plan as an ASCII map (`--export DIR`).
`scripts/layout_viz.py` turns those exports into colour maps: walls, corridors,
yards, doors and fields keep their map colours, and each room floor is filled by
cluster (food warm, freezer/pantry blue, work brown, housing purple, medical
green, power yellow) with its name abbreviated on it. It needs Python 3 with
Pillow. Pure planner output only: it says nothing about a live map.

## 1. Export the layouts

Resolve the environment as in [build-mod](../build-mod/SKILL.md), then run the
V2 matrix with an export directory. `V2_SEEDS=1` exports only seed 1, which is
what gets drawn (the matrix always exports seed 1); keep the full seed count
when you also want the averages. `V2_CASE` filters case names by substring.

```sh
RW_REPO_ROOT="$(git rev-parse --show-toplevel)"
eval "$(python3 "$RW_REPO_ROOT/.shared/rimworld-dev-skills/scripts/dev-env.py" --shell)"
docker run --rm -v "$RW_REPO_ROOT":/mod -v "$RW_NUGET_VOLUME":/root/.nuget/packages -w /mod "$RW_SDK_IMAGE" \
  dotnet run --project Source/RealisticBases.LayoutTests/RealisticBases.LayoutTests.csproj -c Release \
  -- --only "V2 matrix" --export /mod/.runtime/export-after > "$SCRATCH/matrix-after.txt" 2>&1
```

Keep exports under ignored `.runtime/` or the scratchpad, never in tracked paths.

## 2. Export the baseline

For a before/after comparison, check the base commit out into a detached
worktree in the scratchpad, copy in any test-side metric changes so both runs
report the same columns, and run the same command with that worktree mounted
as `/mod`:

```sh
git worktree add --detach "$SCRATCH/base" <base-commit>
cp Source/RealisticBases.LayoutTests/LayoutEngineV2Matrix.cs "$SCRATCH/base/Source/RealisticBases.LayoutTests/"
docker run --rm -v "$SCRATCH/base":/mod ... --export /mod/export-before > "$SCRATCH/matrix-before.txt" 2>&1
```

Only copy files that measure, never planner sources. Remove the worktree with
`git worktree remove` when done. If both runs share the container, run them one
after the other; contention doubles the time.

## 3. Render

```sh
VIZ="$RW_REPO_ROOT/.claude/skills/visualize-layouts/scripts/layout_viz.py"
python3 "$VIZ" png   "$SCRATCH/base/export-before" .runtime/export-after "$SCRATCH/viz/png" \
  --before-label "BEFORE (abc1234)" --after-label "NEW"
python3 "$VIZ" sheet "$SCRATCH/viz/png" "$SCRATCH/viz/sheet.png" industrial-joint-w100 tribal-joint-w100 beach-joint
python3 "$VIZ" page  "$SCRATCH/base/export-before" .runtime/export-after "$SCRATCH/viz/layouts.html" \
  --before-matrix "$SCRATCH/matrix-before.txt" --after-matrix "$SCRATCH/matrix-after.txt" \
  --before-label "Before · abc1234" --after-label "Corridors first" --title "Corridor-First Layouts"
```

- `png` writes one before|after image per case; a case missing from the
  before directory renders after-only.
- `sheet` stacks chosen cases for a quick look. Pick cases that show the
  change: a joint field case, a tribal (town) case, one with water and one with
  clutter or obstacles.
- `page` builds a single HTML file with every case, per-case and total
  food-walk and walled metrics parsed from the matrix logs, a colour key and
  case links. Images are embedded, so the file stands alone (about 300 KB for
  20 cases).

## 4. Show it

Read the sheet PNG yourself before describing it; name what changed and what
still looks wrong, with the metric deltas. For the user, `open` the sheet on
macOS, or publish the HTML page as an Artifact when they want all cases or a
link (republish the same file path to update it). State that the maps are
synthetic planner evidence, seed 1 only.

## Reading the maps

- Light blue cells are corridors; orange cells on room walls are doors.
- Pale green inside the walls is public yard space: large pale areas are wasted
  walled area.
- Yellow blocks are power yards; pale yellow is a wind turbine's clear zone.
- Green rectangles with a dark border are crop fields and their wall; pink and
  red marks outside are approach routes, posts and gates.
- If room colours are missing, the room table did not line up with the map's
  doors (`locate` failed); the map itself is still correct.
