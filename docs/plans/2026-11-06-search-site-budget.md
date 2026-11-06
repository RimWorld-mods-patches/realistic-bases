# Share search work across small-roster settlement sites

Confirmed CliffAndCoast tile 92330 exhausted its 400,000-evaluation search
allowance while trying the first four rock-protected sites. Several nearly
complete layouts had already spent 335–340 of their 340 excavation cells before
placing power, whose tested footprints needed another 33–35 cells. Later ranked
sites could fit the complete settlement but received no search opportunity.

The planner already calculated a site's soft allowance from the remaining
evaluations divided by the remaining sites. Its yield checks, however, only ran
with the broader room-packing strategy used for larger populations. This change
applies the same checks to every packing strategy. It keeps the existing layout
style preference, site quotas, growth variants and candidate ordering.

Yielding happens between attempts and compound variants. An attempt may exceed
its soft site share; the global evaluation limit remains hard. Unused work is
shared among the remaining sites. A complete candidate already in the validated
pool can still be finished and accepted when further search reaches the cap.
The change does not raise population, site, evaluation, excavation or import
limits, and does not weaken wall depth, setback or final validation.

The original fixture remains unchanged: input `9B8E8E43DB16F5CA`, terrain
`78D0DEF635C4DF6E`, SHA-256
`7e7af7e053f5a84c9b835bccdb99c9c75fb188848e631ce5ebd8bf6f6b811592`.
The captured GL context confirms CliffAndCoast. This is separate from the known
Cliff attempt that timed out before map and planner events.

The permanent captured-map regression reproduces the original rejection before
the fix. With fair sharing, it accepts the fifth site with four residents, 155
crop cells, one requested wall layer and 333/340 excavation cells. It finishes at
400,000 evaluations using an already valid candidate pool. The modeled catalog
produces `B4D3B90C545D061C`; the exact native fixture produces
`D31B5798C449AFFF`. Both keep ordinary imports at 400/4,125. Exact replay passes
full layout validation, ordinary spawn preflight and housing-floor checks, and
repeats the same result with four-way site search.

Evidence is under
`.runtime/investigations/geological-landforms/next-cases/run-2026-11-06b/fixer/cliffandcoast-92330`.
The full layout run passes 219 tests. Its remaining test accepted and validated
all twelve cluttered maps but failed an assertion requiring the old search to
exercise compound growth. That incidental search-order assertion was replaced
with explicit search-limit checks; the separate joint-site growth geometry
checks remain. The entire twelve-map test then passes its focused rerun, covering
all 220 tests without a remaining failure.

A fresh native check confirms CliffAndCoast and accepts the same input, terrain
and layout digests as exact offline replay. The saved map has four residents at
distinct positions and 155 unroofed rice plants. All nine field posts have at
least two legal native defender paths; the longest is 49 cells under the
unchanged 160-cell limit. Native realization uses 333 excavations, retains 317
existing roof cells, and builds all 73 field walls and both field gates. Finishing
applies 414 floor cells and 160 smoothed walls. The base was shown paused and
saved to an isolated checkpoint before stopping the test process.

Historical rollback records and captured fixtures are preserved. No new forced
rollback, movement, elapsed cultivation or save/reload validation is claimed.
