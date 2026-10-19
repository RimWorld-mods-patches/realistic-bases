# Planner performance

Status: PP0b complete; PP1–PP5 await user selection. It follows the turret-batteries work
([plan](2026-10-18-turret-batteries-and-power.md)), after the startup-power and
hidden-conduit tasks.

## Problem

At `3837389`, planning one settlement takes 8–54 s. The T6 sweep timings
(`t6-outcome.log`) were:

- 8.0 s: lakeshore w100, a Neolithic base with no turrets;
- 19.6–25.7 s: most of seeds 1–8;
- 54.7 s: seed 5.

Planning runs while the map is generated, so the player waits this long on entering a
settlement. It also makes each turret sweep and the full LayoutTests slow.

PP0 measured the stages; see the baseline and "PP0 findings" below. PP1–PP5 were written
before any measurement, so their order follows the findings, not the numbering.

## Rules

- **Results don't change.** Each step must leave every layout's digest the same on the
  sweep bases and the LayoutTests fixtures. The performance fix `d7c78d0` changed results
  by accident (143 to 57 turrets, corrected in `8f61a5c`).
- **Deterministic for multiplayer.** Any parallel work must merge its results in a fixed
  order and must not depend on timing.
- **One step at a time.** Measure each step against the same seeds before taking the
  next one.

## Steps

### PP0: Stage timers
Add a timer to each stage of planning and report it next to `plannerMs`. The stages are:

- site and candidate search;
- field wall (`P4BFieldWallPlanner.PlanJoint`);
- `FitDefenseAccess`;
- approach defense (`P4BApproachDefensePlanner.Plan`);
- turret planning (`P4BTurretPlanner.Plan`);
- the power-trim and crossing-removal loop;
- `ProceduralLayoutValidator.Validate` (summed over all calls, with a call count);
- scoring.

Record a baseline on the 14 T6 bases. The baseline decides the order of PP1–PP5.



PP0 baseline: complete on `d508a12`

Three full T6 sweeps ran sequentially, 14 bases per repeat. Stage values below are
medians of the three runs; each stage cell is `median milliseconds / share of median
planner wall time`. CPU is process CPU time measured around each plan. Stage shares
use total `plannerMs` as the denominator. The baselines are in
`.runtime/investigations/turret-batteries/pp0-repeat-{1,2,3}.log`; the computed table
is `.runtime/investigations/turret-batteries/pp0-stage-medians.csv`.

Load averages at the start/end of each repeat (1, 5 and 15 minutes):

- Repeat 1: `3.85 3.57 5.21` → `2.90 3.25 4.68`
- Repeat 2: `2.90 3.25 4.68` → `2.11 2.71 4.10`
- Repeat 3: `2.11 2.71 4.10` → `2.80 2.94 3.88`

All 14 layout digests match the T6 baseline. The full LayoutTests run passed 182/183;
the sole failure is the known GL peninsula captured-tile case. PlannerTests passed.
The Release build passed with no warnings or errors.

| Base | Total wall / CPU ms | site + candidate ms / share | field wall ms / share | FitDefenseAccess ms / share | approach defense ms / share | turret planning ms / share | power trim + crossing removal ms / share | validator ms / share | scoring ms / share | Validator calls |
|---|---|---|---|---|---|---|---|---|---|---|
| total12-seed1-w180 | 15837 / 16280 | 8765 / 55.3% | 21 / 0.1% | 3 / 0.0% | 3325 / 21.0% | 3529 / 22.3% | 3 / 0.0% | 254 / 1.6% | 8 / 0.1% | 7 |
| total12-seed2-w180 | 20482 / 20650 | 7748 / 37.8% | 18 / 0.1% | 36 / 0.2% | 5713 / 27.9% | 6743 / 32.9% | 7 / 0.0% | 243 / 1.2% | 6 / 0.0% | 8 |
| total12-seed3-w180 | 14622 / 14690 | 7864 / 53.8% | 11 / 0.1% | 1 / 0.0% | 3130 / 21.4% | 3403 / 23.3% | 1 / 0.0% | 205 / 1.4% | 6 / 0.0% | 7 |
| total12-seed4-w180 | 25059 / 25130 | 7757 / 31.0% | 25 / 0.1% | 63 / 0.2% | 13325 / 53.2% | 3657 / 14.6% | 1 / 0.0% | 243 / 1.0% | 5 / 0.0% | 9 |
| total12-seed5-w180 | 42035 / 42120 | 7697 / 18.3% | 64 / 0.1% | 203 / 0.5% | 29307 / 69.7% | 4306 / 10.2% | 1 / 0.0% | 386 / 0.9% | 5 / 0.0% | 15 |
| total12-seed6-w180 | 14196 / 14210 | 7851 / 55.3% | 14 / 0.1% | 33 / 0.2% | 3584 / 25.2% | 2504 / 17.6% | 1 / 0.0% | 192 / 1.4% | 5 / 0.0% | 8 |
| total12-seed7-w180 | 18202 / 18290 | 7845 / 43.1% | 8 / 0.0% | 1 / 0.0% | 5746 / 31.6% | 4229 / 23.2% | 5 / 0.0% | 262 / 1.4% | 5 / 0.0% | 7 |
| total12-seed8-w180 | 13985 / 14000 | 7808 / 55.8% | 7 / 0.1% | 4 / 0.0% | 3113 / 22.3% | 2812 / 20.1% | 1 / 0.0% | 278 / 2.0% | 5 / 0.0% | 8 |
| lakeshore-joint-w60 | 3805 / 3810 | 1868 / 49.1% | 5 / 0.1% | 3 / 0.1% | 1830 / 48.1% | 0 / 0.0% | 0 / 0.0% | 96 / 2.5% | 5 / 0.1% | 6 |
| lakeshore-joint-w100 | 4830 / 4860 | 2645 / 54.8% | 6 / 0.1% | 1 / 0.0% | 2076 / 43.0% | 0 / 0.0% | 0 / 0.0% | 98 / 2.0% | 6 / 0.1% | 6 |
| lakeshore-joint-w180 | 10759 / 10770 | 3013 / 28.0% | 12 / 0.1% | 2 / 0.0% | 7584 / 70.5% | 0 / 0.0% | 0 / 0.0% | 144 / 1.3% | 5 / 0.1% | 8 |
| broad-flat-w60 | 9727 / 9730 | 2794 / 28.7% | 5 / 0.1% | 1 / 0.0% | 4151 / 42.7% | 2614 / 26.9% | 1 / 0.0% | 176 / 1.8% | 4 / 0.0% | 7 |
| broad-flat-w100 | 13038 / 13080 | 3453 / 26.5% | 6 / 0.0% | 1 / 0.0% | 6059 / 46.5% | 3388 / 26.0% | 1 / 0.0% | 161 / 1.2% | 4 / 0.0% | 7 |
| broad-flat-w180 | 18984 / 18990 | 4991 / 26.3% | 12 / 0.1% | 29 / 0.1% | 8711 / 45.9% | 4946 / 26.1% | 1 / 0.0% | 201 / 1.1% | 5 / 0.0% | 8 |

### PP0 findings and revised order
Totals over the 14 bases, from the medians above:

| Stage | Time | Share |
|---|---|---|
| approach defense | ~97 s | ~43% |
| site and candidate search | ~82 s | ~36% |
| turret planning | ~42 s | ~19% |
| validator | ~3 s | ~1.4% |
| power-trim loop | ~25 ms | under 0.1% |

- **Approach defense is the main cost,** and it explains the outliers: seed 5 (29.3 s,
  70%), seed 4 (13.3 s, 53%) and lakeshore w180 (7.6 s, 70%). It runs line-of-sight work
  (`CanEngage`, `FiringSlots`, `RequiredCover`: 17 uses in `P4BApproachDefense.cs`) and
  rebuilds `Opaque` with a full terrain scan at lines 178, 801 and 948.
- **Site search costs a nearly constant ~7.8 s on seeds 1–8,** whatever the terrain. That
  points to a fixed enumeration (sites × attempts) that may be prunable, not to geometry.
- **Turret planning** is 10–33%, highest on seed 2.
- **Dropped:** PP3, because the power-trim loop takes milliseconds, and PP4, because turret
  planning takes 0.3 ms on the Neolithic bases. The validator (about 1–2%, 6–15 calls) is
  not worth optimizing either.

Revised order:
1. **PP0b, profile inside the hot stages.** Use a sampling profiler or sub-timers on seed 5
   and seed 4 (approach defense), seed 1 (site search) and seed 2 (turret planning), and
   name the top functions. CPU time equals wall time in PP0, so the planner is
   single-threaded and CPU-bound, and the profile is reliable even under load.
2. **PP1 and PP2 for approach defense first,** then for turrets, if PP0b confirms that
   line of sight and `Opaque` dominate.
3. **Site search,** pruning or caching, depending on what PP0b shows.
4. **PP5** where PP0b shows it matters. **PP6** stays optional.

### PP0b: Sampling profile results

PP0b is complete. I collected CPU-sampling traces for `total12-seed5-w180`,
`total12-seed4-w180`, `total12-seed1-w180` and `total12-seed2-w180` with `dotnet-trace`.
No planner source or decisions changed. The focused outcome test passed for all four, and
all four input/layout digests match `t6-outcome.log`.

The trace top-N report samples both the test worker and its waiting caller; `Thread.Join`
accounts for about half of the samples. The approximate milliseconds below normalize each
method's inclusive/exclusive sample share by the `PlanClusters` inclusive share in that
trace, then multiply by that run's `plannerMs`. Inclusive values overlap and must not be
added. The `self` column is the profiler's exclusive time. Treat these as function-ranking
estimates, not exact timers.

| Base / hot stage | Function | Inclusive ms | Self ms |
|---|---|---:|---:|
| seed 5 / approach | `P4BApproachDefensePlanner.Plan` | ~28,560 | ~40 |
| seed 5 / approach | `LayoutAttempt.FitDefenseAccess` | ~23,610 | ~10 |
| seed 5 / approach | `Enumerable.Any` | ~23,850 | ~1,760 |
| seed 5 / approach | `Enumerable.Count` | ~22,130 | ~5,840 |
| seed 5 / approach | `P4BApproachDefensePlanner.Plan` closure `b__21` | ~21,800 | ~150 |
| seed 5 / approach | `GridPoint[].FindItemIndex` | ~12,250 | ~12,250 |
| seed 5 / approach | `P4BCoverNest.Shielded` | ~9,730 | ~3,560 |
| seed 4 / approach | `P4BApproachDefensePlanner.Plan` | ~13,500 | ~20 |
| seed 4 / approach | `LayoutAttempt.FitDefenseAccess` | ~10,360 | ~10 |
| seed 4 / approach | `Enumerable.Any` | ~12,270 | ~1,170 |
| seed 4 / approach | `Enumerable.Count` | ~11,040 | ~2,760 |
| seed 4 / approach | `P4BApproachDefensePlanner.Plan` closure `b__21` | ~10,340 | ~70 |
| seed 4 / approach | `GridPoint[].FindItemIndex` | ~7,490 | ~7,490 |
| seed 4 / approach | `P4BCoverNest.Shielded` | ~4,420 | ~1,760 |
| seed 1 / site search | `P4BJointSitePlanner.Find` | ~7,250 | ~50 |
| seed 1 / site search | `P4BJointSitePlanner.ReachableFromLane` | ~2,350 | ~400 |
| seed 1 / site search | `GridPoint[].FindItemIndex` | ~3,660 | ~3,660 |
| seed 2 / turret planning | `P4BTurretPlanner.Plan` | ~7,280 | <10 |
| seed 2 / turret planning | `P4BTurretPlanner.PlanGroup` | ~7,220 | ~10 |
| seed 2 / turret planning | `P4BTurretPlanner.PlanGroup` closure `b__65` | ~2,310 | ~440 |
| seed 2 / turret planning | `P4BTurretPlanner.ExposureCoverage` | ~2,080 | ~350 |
| seed 2 / turret planning | `P4BTurretPlanner.UnansweredExposure` | ~630 | ~190 |
| seed 2 / turret planning | `P4BCoverNest.Shielded` | ~1,700 | ~610 |

The ~7.8 s site-search plateau is mostly `P4BJointSitePlanner.Find`: it took about
7.25 s (roughly 81% of seed 1's 8.92 s site/candidate stage). `ReachableFromLane` is a
substantial child at about 2.35 s inclusive. In approach defense, `FitDefenseAccess`
repeats `P4BApproachDefensePlanner.Plan`; its inclusive time overlaps the `Plan` row.
The profile's hot path is repeated LINQ `Any`/`Count` and `GridPoint` array membership in
those candidate/coverage passes. Turret planning on seed 2 is dominated by `PlanGroup`;
`ExposureCoverage` and `UnansweredExposure` are identifiable children.

Raw traces and top-N output are under
`.runtime/investigations/planner-performance/pp0b/` and
`.runtime/investigations/planner-performance/pp0b-profile-summary.log`.
No sub-timers were kept, and no optimization was made.

### PP1: Line of sight
`P4BFireLane.CanEngage` walks a ray cell by cell and looks up each cell in a
`HashSet<GridPoint>`. The turret and exposure code uses it in about 37 places. When a
caller passes a collection that isn't a `HashSet`, the method copies the whole blocking set
on every call (`P4BFireLane.cs:20`). The exposure metric alone casts rays from every
attacker cell within 44.9 of every turret, and that is repeated for every candidate.

- Use a flat `bool[]` grid of blocking cells, built once per layout.
- Cache visibility per origin for nest and turret cells that are evaluated repeatedly.
- Make every caller pass the prepared grid, so no call copies the set.
- Compute the per-origin visibility fields, and the per-cell exposure counts, in
  parallel (`Parallel.For` over the origins or attacker cells). It is safe because:
  - the inputs (the blocking grid and the origin list) are read-only;
  - each origin writes only its own result slot, indexed by origin;
  - totals are integer counts summed in index order.

  The result is therefore identical on any core count and in any thread order, which
  keeps multiplayer deterministic. The planner works on a terrain snapshot and doesn't
  touch Verse or Unity, so worker threads are allowed. Nothing in this step may use a
  shared RNG. The terrain part of PP2 is one pass over the map, and isn't worth
  parallelizing.
- **Thread count:** `clamp(Environment.ProcessorCount - 1, 1, 8)`, set explicitly through
  `ParallelOptions.MaxDegreeOfParallelism`. Mono's thread pool otherwise grows slowly
  and without a bound.
  - One core stays free for Unity's main thread and the long-event loading screen.
  - Above about 8 threads, a few hundred origins give little more speed: memory
    bandwidth and per-task overhead dominate.
  - Hand each thread contiguous ranges of origins (`Partitioner.Create(0, n)`), not one
    task per origin.
  - With one core, or a degree of 1, run the same code sequentially as the fallback.
- **Settings and tests:** make the degree a planner input (default automatic), so the
  test harness can set it. LayoutTests already run 10 workers in parallel, so nested
  parallelism there would oversubscribe the CPU. Tests use degree 1, plus one
  determinism test that plans the same base at degrees 1 and 8 and compares digests.
- **Measure** PP1 at degrees 1, 2, 4 and 8 on the T6 bases, and lower the cap if the gain
  flattens earlier.

### PP2: Blocking-cell set
`P4BApproachDefensePlanner.Opaque` is rebuilt 16 times across the planner, and each
rebuild scans the whole terrain grid. Compute the terrain part once per input and add
only the layout's own walls, gates, doors and room edges on each build.

### PP3: Power-trim loop (dropped after PP0: about 1–7 ms)
Each time a crossing is dropped for power, the planner attaches the network, trims power
and runs a full `ProceduralLayoutValidator.Validate` again (`ProceduralLayoutPlanner.cs`,
in the `while (candidate != null)` loop). Instead:

- estimate the generator and battery need arithmetically from the turret watts;
- drop crossings until the need fits;
- then attach, trim and validate once.

Keep the full validation as the final gate. The accepted network must stay identical.

### PP4: Skip impossible turret work (dropped after PP0: 0.3 ms)
When the tech stage or the power rules out turrets, skip the turret stage. The
Neolithic lakeshore base still runs all three spacing levels for every crossing, and each
one is rejected with `stage_or_power_unavailable`.

### PP5: Repeated work and allocations
- `Finish` builds the same `occupied` set twice.
- Approach defense and the field wall are planned again in several branches.
- LINQ chains in the hot loops allocate heavily.

Cache or hoist this work where PP0 shows it matters.

### PP6: Parallel candidates (optional, last)
Evaluate candidate sites or pool entries on several cores, merging in a fixed order. Try
this only if PP1–PP5 aren't enough. It carries the most risk for determinism and for
RimWorld's main-thread assumptions.

## Done when
- Every sweep base and fixture has the same digest as before.
- The full LayoutTests, PlannerTests and the Release build pass (GL peninsula is a known
  failure).
- Planner time is reported per stage, before and after, on the 14 T6 bases.
