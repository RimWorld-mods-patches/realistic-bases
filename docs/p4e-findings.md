# P4E facility and defense findings

2026-10-01 · Implementation on `codex/p4e-facilities`; not a closeout.

## P4E.0 — Baseline and definition audit

Starting point: main `f436173`, preserving P4B/P4C/P4D. All inspections use installed Core 1.6.4871 XML and matching external decompiled implementation, not stripped reference method bodies. Shared build/runtime/API skills are read; no game process was running at the initial process check, and Docker Server 29.2.1 is available. No tools installed or normal profiles touched.

### Facility policy and stage matrix

`LayoutDemandModel.IsLiving` covers housing, jail, medical and dining only. Workshop and kitchen are occupied work rooms but miss comfort control. Freezer has separate cooling. New Laboratory must use the same explicit room-use policy; storage, power and magazines remain outside comfort policy. Existing targets remain −10°C freezer and +21°C occupied rooms. Thick-roof cooling relief, actual outdoor exhaust, finite primitive fuel and utility priority must remain intact.

| Final resolved stage | Laboratory bench | Analyzer | Seats / medical shelves |
|---|---|---|---|
| Neolithic | SimpleResearchBench | None | Stool / no legal Core shelf (ComplexFurniture absent); explicit ground fallback |
| Medieval | SimpleResearchBench | None | DiningChair or Stool / ShelfSmall or Shelf |
| IndustrialEarly | SimpleResearchBench | None | Legal resolved seats / shelves |
| IndustrialMid | HiTechResearchBench (MicroelectronicsBasics), simple fallback | None | Legal resolved seats / shelves |
| IndustrialLate / Spacer | HiTechResearchBench, simple fallback | MultiAnalyzer when legal, powered and linked | Legal resolved seats / shelves |

Ordinary eligible settlements get one laboratory bench, not a population-scaled research economy. Compact quest camps retain their existing no-workshop/no-lab contract. Simple bench is Core 3×2, no prerequisite, interaction offset `(0,-1)`, 75 stuff +25 steel. Hi-tech is 5×2, metallic stuff only, 250 W, 150 stuff +100 steel +10 components. Analyzer is 2×2, 200 W, requires MultiAnalyzer research, costs 40 steel/50 plasteel/20 gold/8 components. Bench facility linkage is explicit `CompAffectedByFacilities`; verify actual powered active link, not proximity alone. These choices do not grant autonomous NPC research.

### Seating and medicine

Current workshop fixtures reserve working cells but never furnish seats. Dining creates four chairs all facing north; three are wrong. Core research reserves `ReserveSittableOrSpot` at `InteractionCell`, and research/recipe toils gain chair comfort there. A seat is a permitted passable occupant of the working cell, not a side decoration; it must face the bench and retain circulation. Mortar operator cells are not seated work.

Medical currently has beds/optional monitor/light/climate but no shelves; `LayoutStockCells.ForRooms` prefers shelves only if furnished. Core ShelfSmall is 1×1, three stacks per cell, PassThroughOnly, 10 stuff; Shelf is 2×1, 20 stuff. Both require ComplexFurniture. Fixed filters allow manufactured medicine and shells, excluding chunks/plants/buildings. Actual item filters and stack limits are checked by existing `SpawnStacks`; do not change finite stock totals.

### Mortar contract and hazard

Existing layout/encounter/controller/spawner hold one mortar, operating cell and magazine marker. Saved controller uses `p3Mortar` reference plus stable saved post IDs/flags/assignments and one shared operating-stock ledger. Migration design: retain legacy `p3Mortar` read, introduce saved collection of guns and corresponding mortar post IDs; after loading absent collection map the old reference to the existing mortar post, without spawning guns or replenishing anything. Every job must resolve its gun from its assigned post, and every gun has at most one operator. Initial barrel count sums loaded barrels once over guns, not once per pawn. Quest contracts keep zero extra artillery.

One usable cell plus walls/door is a 3×3 enclosure using ShelfSmall. Native mortar jobs fetch with PathEndMode.OnCell; this needs live verification rather than assuming doorway Touch access alone suffices. Roof only the store. Core explosive radius is base radius plus sqrt((stackCount−1) × explosiveExpandPerStackcount): twelve HE shells give 4.9976 cells, not a linear 7.7-cell radius. A roofed enclosure is not blastproof. Preserve modest finite stocks and separation from beds, fuel and sole retreats. The user's clarified adjacent operating-side entrance takes precedence over an arbitrary six-cell operator setback. Shared six-hour shell/barrel ledger must not be copied per gun.

### Defense budget

Current ordinary population distribution remains 4–12 with expected mean seven. Import allowance is the physical profile allowance multiplied by ordinary wealth, separate from resident equipment. Turret construction, initial fuel/reserve and added cover are charged against that same import limit; connected dispatchable headroom and exclusive guard/support staffing are independent constraints. Maximum groups currently min(4, exposed faces). Heavy selection orders uranium then autocannon then mini unconditionally, so autocannons can be shadowed. Core Turret_Autocannon requires HeavyTurrets (IndustrialLate in the current stage model); uranium requires SniperTurret. P4E.4 must compare a named ordinary-only increment and useful gun mix, preserving native quest budgets and roster.

## Evidence ledger

- P4E.0 source audit complete. Subsequent sections record live thermal, seating, shelving and single-magazine loading evidence; multiple-gun behavior and hazard acceptance remain open.
- P4E.2 complete below; P4E.3–P4E.5 pending. Do not infer integrated completion from a facility pass.

## P4E.1 — Laboratories and occupied-work-room climate

Appended Laboratory/Research/ResearchAnalyzer enum values (no shifted persisted values), with ordinary one-bench demand and compact-quest exclusion. Catalog capture now records resolved facility links/range and seating facts. Simple bench remains legal at primitive stages; hi-tech starts at IndustrialMid; eligible analyzers must have an explicit bench link and fit the eight-cell Core center-distance/clear-line rule. Laboratory finishes use the clean-room P4C category. Power/fuel and material summaries include the fixtures. Planner version is `p4e-facilities.1`; existing loaded layouts are not regenerated.

Shared occupied-room policy now includes Kitchen, Workshop and Laboratory. Existing freezer −10°C and occupied +21°C targets, thick-roof relief, outdoor-exhaust logic and legal primitive limits remain. No thermal/production simulator or autonomous research added.

The additional lab exposed packing and utility-frontage omissions. Initial full regression had five failures: a stale one-row bench test, the 12-map clutter corpus (7/12), two optional renewable placement assertions and a single-seed wind-zone assertion. Corrected the bench test to check the complete back row of deep benches; wind-zone validation now finds an actual turbine in a bounded six-seed corpus rather than requiring optional power on one obsolete seed. Added a bounded same-compound room-envelope/connecting-corridor extension and at most two utility-access branches per renewable source. All ordinary field approaches now reserve their connection before packing, including four residents. No field relocation, roster reduction or relaxed path/terrain rules. The exact clutter regression passes **12/12**, with three local-growth cases.

Disposable Core live run: map1, seven IndustrialLate residents, ten rooms, 240 crops, 468 finished floor cells, ten actual outdoor cooler exhausts, 242 conduits, 60 startup generator fuel and 204 finite wood reserve. `PROCEDURAL_SPAWN_COMPLETE` layout `67C5853BED407DC8`; playable arrival and clean startup/logs. Hi-tech bench's working cell is standable; one analyzer is genuinely linked and active, bench powered. Seats/medical shelves are intentionally still absent in this checkpoint (P4E.2).

| Observation | Game tick | Outdoor | Lab room | Workshop room | Operation |
|---|---:|---:|---:|---:|---|
| Initial settled | 3636 | 21.71°C | 21.00°C | 21.03°C | Powered heat/cool, active analyzer |
| HeatWave, started3647 | 17157 | 48.00°C | 21.00°C | 21.00°C | Powered coolers; ~13,500 settling ticks |
| ColdSnap, after ending HeatWave | 44741 | 1.76°C | 20.73°C | 20.78°C | Powered heaters; >20,000 settling ticks |

Debug climate probes use native temporary conditions and log actual map/tick; audits report actual room temperature, outdoor use, work cell, target/power/fuel and active links. The exact disposable process was stopped before rebuilding. Live samples establish this generated thermal arrangement, not seasonal or extreme primitive guarantees. Full integrated regression remains P4E.5.

## P4E.2 — Correct seats and finite medicine on shelves

2026-10-02. Resolved catalog metadata identifies supported seated work tables,
including inherited runtime types, separately from arbitrary interaction cells.
Kitchen, workshop and laboratory seats occupy the correct working cell with the
bench's native rotation. Dining seats face their associated table cell on every
side. Working cells and chairs are unique; the production validator rejects
missing/misoriented/illegal seats. Seats remain passable and material-budgeted;
no chair is placed at a mortar operating cell.

Appended MedicalStorage role (no shifted persisted enum values), preferring legal
ShelfSmall then Shelf. Medical room packing must fit the shelf or grow the room,
not discard medicine. Existing pooled stock placement puts medicine on that
shelf first, retaining actual filters, stuff, per-cell capacity and finite stock
identity. Neolithic Core has no legal shelf and explicitly retains ground
storage; Medieval and later get the resolved shelf. A catalog without ShelfSmall
resolves the legal Shelf fallback. Planner version is `p4e-facilities.2`.

Twelve seeded geometry cases exercise all four workstation rotations, every
dining side, uniqueness, legal medical storage, pooled shelf preference and
door/working-cell clearance. Primitive capability and missing-small-shelf
fallback fixtures pass. Full layout suite passes (440,982 ms), including 12/12
dense flat cases with four local-growth plans, P4D protected-objective cases,
materials, power, crops and full-perimeter regressions. Full planner/garrison
suite and the latest focused P4E suite pass; Release build has zero warnings or
errors. Three obsolete fixed-seed assertions were corrected to request two
gates explicitly, exercise the production joint field-site path, and find a
supported heavy emplacement in a bounded eight-seed corpus. Heavy support,
crop capacity and connectivity assertions remain intact.

The successful same-tile retry described in [P4B findings](p4b-findings.md)
provides live IndustrialMid evidence: four permanent residents, ten rooms,
correctly seated hi-tech research bench, stove, smithy, tailoring, machining and
stonecutting workstations. At tick18426 the powered laboratory was 21.00°C and
workshop 21.38°C with outdoors 27.05°C; all working cells were standable with
seats. No analyzer is legal at this stage; active IndustrialLate linkage remains
the separate P4E.1 observation.

The user clarified that **laboratories are decoration**. No NPC research job,
research progression, comfort-need injection or research validation probe is in
the resulting code. Only facility/seat geometry, legal equipment, power and
linkage are checked. The final isolated probe creates two genuinely new,
relation-free, gear-free pawns (never reuses saved residents/world pawns), issues
native eating and inventory-fetch jobs, then cleans up those temporary pawns.
It observes the actual `JobDriver_Ingest.GainingNutritionNow` chewing toil on a
dining chair next to an Eat surface, not merely an Ingest pawn walking over a
workstation chair.

Live final probe at tick19028: `researchPerformed=False`, `seatedEating=True`,
`medicineFetched=True`, eater at `(82,59)`, medicine Thing79299 with all thirteen
units and `medicineIdentityPreserved=True`. Cleanup returned that **same** Thing
to its original medical shelf. Audits before and after native active save/reload
at tick18426/18427 report one shelf stack, thirteen units, ID79299:13; later
audits retain the same count/identity. The permanent controller reloads four
defenders and four assignments, preserving finite partly consumed mortar stocks
without replenishment. The temporary probe does not become permanent staffing.

All game runs used the isolated five-mod profile, checked exact API actions and
one verified process; it was stopped before every build. Evidence is in ignored
`.runtime/logs/p4e-2-perimeter-medicine-pass-20261002.log` and
`.runtime/logs/p4e-2-east-transfer-gap-20261002.log` (the latter contains the final
chewing/fetch probe and the subsequently corrected cross-front route gap).
`p4e-2-east-transfer-fixed-20261002.log` verifies the corrected live transfer.
Shared skills and normal user saves/profiles were not changed.

P4E.3 multiple crews, P4E.4 defense allowance/autocannons and P4E.5 integrated
terrain/quest/hazard/migration acceptance remain pending. Magazine progress follows.

## P4E.3 — Magazine and operating-position pass (not multi-mortar closeout)

2026-10-02. Appended MortarMagazine room and MortarStorage role values. A
resolved legal ShelfSmall occupies one usable interior cell, with seven walls,
one door and a roof over the 3×3 enclosure only. Fixtures, walls, door and floor
participate in the existing material/finish/transaction ownership; the mortar
and operating cell remain unroofed. Shells and spare barrels move from ordinary
bulk storage to this actual shelf, preserving the existing encounter-wide
twelve HE/two incendiary/two spare-barrel quantities and the gun's one initial
embedded barrel. No new crew, extra shells or trade-stock borrowing are added.

The user's screenshot identified a valid correction: the first prototype was
the right design in the wrong place, with a ten-cell public loading route. The
entrance now sits at the operating cell or one cardinal step away, faces the
crew and lies on the working side. The public portion of loading is at most two
cells; the enclosed shelf lies two or three steps from the operating cell.
Production validation rejects a correctly shaped but distant magazine. The
six-cell operator/stock minimum was removed: it defeated the requested spatial
intent. Separation from beds/fuel, an unobstructed public retreat and finite
stocks remain; this is not a claim of blast immunity.

The courtyard fixture now supplies the actual native mortar rotation and
footprint-derived origin. Previously generation always spawned North despite
the planned working side. Preflight independently checks native InteractionCell
against the planned cell; realized audits require an actual one-cell roofed
room, shelf filter/capacity, open-sky gun/operator and native OnCell loading
reachability. All four rotations are covered by pure cases.

Optional artillery is packed after required rooms, renewable yards and the
power room. It cannot consume essential utility frontage or overlook a future
fuel store. If its added outline crosses illegal terrain/a geyser, the same
complete required-room pack is retried without artillery, without reducing the
roster or moving the field. At most two additional access branches and 128
court street candidates per pass remain bounded. Turret staffing reserves a
mortar operator only when a real court exists, not from the stage's capability
wish alone. The four/seven/twelve-resident clutter regression passes again.

Pure magazine fixtures retain artillery on ten of twelve seeded seven-resident
flat layouts after essential-utility priority, with all four rotations; every
settlement remains accepted. Explicit blocked-door and shifted-distant-store
regressions reject. Existing optional-network/renewable/doctrine tests now use
bounded same-terrain corpora instead of assuming one seed must own an optional
fixture. The network utility assertion matches the existing production rule
(four points for first coverage, one for crossfire, none for a third redundant
gun); existing funding, sniper support, blind-zone, power, collision and path
checks remain intact. Connectivity fixtures include actual intact terrain rock
as graph edges, never as extra roots or credit for planned excavation.

Disposable native Core case: six IndustrialLate residents, seed
`48AAC8553C6960A2`, layout `5051F06A17CD4E81`, map3. Mortar98517 operates at
`(37,42)`; magazine shelf is `(37,40)`, with the door `(37,41)` facing the
operating cell. The realized public route is one cell (the doorway outside is
the operating position itself). Visual inspection shows the compact store
directly behind the manning pawn, not elsewhere among the settlement rooms.

At tick2055, a real funded defender (98783) completed native barrel replacement,
shell fetching and manning: eleven loose HE + one loaded HE, two loose
incendiaries, one spare barrel and twenty barrel-fuel charges. The probe consumed
the initial embedded barrel explicitly to require physical replacement; it
created no pawn or stock. An automatic pause preserves the observation before
the disposable visitor can die and unload the map. ManTurret expiry increased
from 1200 to 3600 ticks to allow the bounded housing/service/loading journey;
an already loaded shell no longer requires another loose shell to keep manning.

Native save/reload at ticks2055/2056 retains gun98517, operator98783, HE stack
98749:11 plus its loaded shell, incendiary98750:2 and barrel98751:1. The same six
defenders/six assignments reload without stock replenishment. The one-cell shelf
remains roofed and reachable. Evidence: ignored logs
`p4e-3-close-loading-20261002.log` and `p4e-3-close-reload-20261002.log`.

Earlier forced failure after magazine stocks restored terrain/roof layers and
all 723 cleared plants. The exact saved tile/seed retried with identical input
and layout digests before the placement correction. Retry guards now exclude
wild animals from settlement occupancy and honor the selected wall-layer policy,
instead of silently forcing three layers; populated/generated sites remain
protected. Evidence: `p4e-3-magazine-rollback-20261002.log`.

This pass does not implement two/three mortars. Stable gun/post/operator
collections, old single-save migration, shared stock distribution, crew/gun
loss, exhaustion/reassignment, live multi-gun reload and representative magazine
hazard validation remain P4E.3 work. P4E.4 and P4E.5 are not complete.

Final required-utilities-first generation on the same saved tile/seed accepted
layout `5EABF8A431E20E58`: six residents, eleven rooms and one real magazine.
South-facing mortar98347 operates at `(107,21)`; shelf `(104,21)`, door
`(105,21)` and outside doorway `(106,21)` are beside the crew, matching the
operating-side diagram. The public route is two cells; no detour is needed.
At tick1680, funded defender98598 physically replaced the barrel, loaded one HE
shell and manned the gun. Native reload at tick1681 preserves gun98347,
operator98598 and HE98565:11/incendiary98566:2/barrel98567:1 plus the loaded HE
shell and twenty barrel-fuel charges. Evidence:
`p4e-3-close-final-live-20261002.log`.

Final full layout suite: **PASS, failures=0, 469,780 ms**, including the corrected
four/seven/twelve-resident clutter case, same-room-pack optional-artillery
fallback, P4C/P4D fixtures, all rotations and distant-store rejection. Full
planner/garrison suite passes. Release build has zero warnings/errors. The final
test console is retained at `.runtime/logs/p4e-3-layout-tests-final.txt`.

## P4E.3 — Multi-mortar draft, verification blocked

Historical checkpoint: the Docker block below was resolved when the user
started Docker Desktop; subsequent verification follows.

2026-10-03. Uncommitted source work now carries a court collection through all
layout clones, circulation/finish blockers, material ledgers and the spawner.
Each court has its own operating-side magazine and stable encounter post
(`mortar`, `mortar-1`, `mortar-2`). Ordinary permanent-resident capacity remains
1/2/3 at <=8/9/>=10; a funded ranged-loadout check also retains two direct-fire
defenders. Compact quest profiles remain artillery-free. Optional outline or
material failures remove the last artillery pair and retry the same required
room pack, without reducing the resident roster.

The single twelve-HE/two-incendiary/two-spare-barrel reserve is divided among
stores. Mortar construction and one embedded barrel per gun now participate in
the import check; runtime catalog and reproducible Core fixture include the
600-value ReinforcedBarrel. The extractor was repaired to preserve native
seating/facility metadata on regeneration (including the verified Core facility
default range of eight), rather than losing previously recorded fixture facts.

Controller draft saves parallel gun/post lists, reads legacy `p3Mortar` into
the original `mortar` post when lists are absent, resolves jobs by assigned post,
reserves exclusive crews, and invalidates unavailable/destroyed/exhausted guns.
Reassignment releases the previous native ManTurret reservation; carried split
stacks remain counted. Initial barrel accounting sums embedded barrels once
across guns. The disposable loading probe now observes all guns and distinct
operators; only one embedded barrel is deliberately consumed, so the two spare
barrels are not overdrawn. One-shot funded wealth overrides prepare the native
nine- and ten-resident cases without adding unfunded pawns.

New pure fixtures cover capacity boundaries, distinct stores/working cells,
clone retention, shared stock/cost, deterministic geometry, duplicate identity
rejection, crew/gun loss and exhaustion. **These C# changes and tests have not
been compiled or executed.** The configured Docker daemon is unavailable at
the current user's `.docker/run/docker.sock`; the user has been asked to start
Docker Desktop. No host SDK or tooling was installed, no game launched, no new
DLL built, and no commit made for this draft. `git diff --check`, fixture
regeneration and Python syntax verification pass; those are not C# acceptance.

Resume gate: recheck Docker and the exact stopped disposable process, run both
pure suites and Release build, fix regressions, then validate native two/three
guns, legacy migration, finite active reload, crew/gun loss and magazine hazard.
P4E.3 remains incomplete. Do not move to a validated P4E.4/P4E.5 closeout based
on this source draft; their allowance/autocannon and integrated live work remain.

## P4E.3 — Synthetic and native multi-gun verification

2026-10-03. Docker Release compilation has zero warnings/errors; the complete
planner/garrison suite and focused P4E suite pass. The first full updated layout
suite passed in 291,555 ms. A later captured-map packing repair has a new full
suite in progress; do not treat that earlier pass as its final acceptance.

Legacy single-gun migration initially missed empty (rather than null) missing
lists. Corrected and verified from the unmodified pre-multi save: gun98347,
operator98598, eleven loose HE plus one loaded, two incendiaries and one spare
barrel retain their identities and quantities. No gun, pawn or stock is spawned
by migration.

Native nine-resident case: exact world seed 6068788646BB71E3, map4, layout
AB7997E057603314. Two rotated mortars134676/134678 with distinct operating cells
and adjacent stores divide twelve HE six-and-six, two incendiaries one-and-one
and two spares one-and-one. The native crews loaded and fired; after deliberately
consuming the first embedded barrel, at tick13130 both guns were manned by
160/135087 and had one HE loaded, fuel15 each, zero loose HE, two incendiaries
and one spare. Native reload13131 preserves those exact gun/operator/stock
identities. Ten physical shots plus two loaded shells account for all twelve HE.

The initial multi-gun probe's exact loose-stock equality failed because one gun
could fire before the other finished barrel service. The corrected probe
permits bounded native consumption and reports firedBeforeAllReady; it still
requires distinct real operators, loaded guns, a physical barrel replacement,
and no replenishment. No new crew is generated. A real operator-loss probe
reassigned from the eight survivors; all-ammunition exhaustion made both guns
unavailable and released both crew roles. The final assignment rule prefers a
vacancy replacement over stealing another healthy gun's operator, with a pure
identity regression. Evidence: p4e-two-mortars-reload-loss-exhaustion-20261003.log.

Two real rejection mechanisms were exposed by the larger facility layout.
Vanilla eight-way movement can round a one-cell route corner, so the declared
transit envelope now includes one legal adjacent cell, not a large exterior
rectangle; existing 160-cell total, twelve exterior and eight exposed-cell
limits remain. Vanilla Building.DeSpawn also applies leaveTerrain for natural
rock even with Vanish, changing Rough to RoughHewn. Excavation and smoothing
now preserve original top/under terrain, including rollback; only explicit
finish choices own floor changes. Cell-level unowned-layer diagnostics remain.

The user's ten-resident rejection was captured unchanged as
p4e-ten-rejected-traits.rle (160x160, terrain ED06EE5676C0012E), with seed
15A894EE21B8FC83, climate -4..37, wood28352 and geysers(65,45)/(129,106).
Synthetic reproduction rejected before the fix. Larger ordinary rosters with
at least one million configured evaluations now use free-envelope filtering,
room-aware connecting corridors and same-field compound growth beyond the old
24-cell cap, bounded by map limits and the unchanged global budget. Tight
explicit harness budgets and smaller/quest rosters retain the frontage strategy.
This preserves prior four/seven/twelve-resident clutter acceptance while the
exact new fixture accepts all required rooms, ten residents and three guns.
No map resize, roster reduction or blanket path relaxation was used.

The same saved world tile and 160x160 native map then completed layout
21BD91C1F92F2E10: sixteen rooms, three distinct magazines, ten residents, 342
unroofed rice cells and an eligible autocannon/mini-turret pair. The input digest
A1418F09914B245A is identical to the earlier rejection. Generation's post-route
preflight now uses the same bounded multi-gate/center waypoint policy as the
runtime controller, rather than only a direct path or one gate. All nineteen
posts have at least two legal pawn paths; maximum complete route115, forty
via-gateway paths. Earlier forced-by-audit rollback restored all1281 plants and
terrain/roof layers. This is planning/spawning evidence; final three-crew
loading/reload/loss/hazard and P4E.4/P4E.5 closeout still follow.

The three-gun native loading probe then **passed at tick2280**: guns
63804/63806/63808 manned by distinct funded operators64252/64264/64313, each
with one HE loaded; seven loose HE and two native shots account for all twelve
initial shells. First-gun barrel replacement left one spare and loaded barrel
fuel20/19/19. Stock identities64187:4,64188:1,64189:2 HE;
64190:1,64191:1 incendiary;64192:1 spare persist through native reload2281,
as do all three operators and guns. No refill, duplicate or extra resident.

Representative native magazine cookoff: stock64187 with four HE, radius
3.995445 at(81,93), wick started2281, automatic pause2457 after120 observed
post-destruction ticks. The shell stack disappeared; five nearby buildings
were damaged and one destroyed. This establishes an actual hazard, **not**
blast immunity from walls/roof. The pre-hazard active save is preserved for
recovery and further loss testing.

Latest complete layout suite **PASS failures=0,289592 ms**, including the
new exact captured rejection, original bounded four/seven/twelve clutter,
P4C/P4D, materials, power, geometry and loadout fixtures. Evidence:
p4e-room-growth-final-tests-20261003.txt. Latest Release build has zero
warnings/errors; the remaining native gun-destruction check follows.

Native gun loss also passes: after removing gun63804, the controller releases
its crew and keeps operators64264/64313 on their original surviving guns.
All three available roles return only when explicitly reloading the preserved
pre-loss save, not by production replacement or replenishment. P4E.3 is
verified; ordinary budget/autocannon policy and integrated P4E.4/P4E.5 remain.

## P4E.4 — Paused draft checkpoint

2026-10-03, explicitly paused by the user. P4E.3 is committed as fd635da.
P4E.4 is uncommitted. The disposable game and all test containers are stopped;
no merge/push or tooling installation was performed.

Draft: ordinary IndustrialMid+ electric budgets get a named125% import
allowance and960W dispatchable reservation; equipment/population and primitive/
early/quest budgets stay unchanged. Extra generator construction and finite
fuel are charged. Heavy selection considers near/long-only approaches and
complementary groups instead of unconditional uranium-first selection.

Matched three-seed sweeps passed. Aggregate guns100→125%: Mid average7→7,
Mid wealthy6→11, Late average5→8, Late wealthy4→14. Each gun passed concurrent
guard/close-support audits;150% gave no extra guns in this corpus. Eight
autocannons and four uranium guns were selected. These are three-map totals.
Evidence: p4e-defense-allowance-sweep-20261003.txt.

Native autocannon63497 fired at2316, one actual projectile, fuel90→89.
Rearm timed out at12316 with fuel0, untouched reserveSteel260 and no service
pawn. Core WorkGiver_Refuel excludes turrets; the correct RearmTurrets worker
uses WorkGiver_Refuel_Turret and RearmTurret/Atomic jobs. Source now selects
that worker, but the latest correction is not rebuilt/live tested. Funded
critical logistics under threat has pure doctrine coverage. Seed already-
in-flight projectile IDs at probe-phase start to avoid counting stale launches.

The first full P4E.4 suite failed the old5500 import assertion (new6875) and
the added power room on clutter. Assertion is corrected. Upfront reserve-aware
demand now repairs four/seven-resident clutter but rejects twelve residents
with room_demand_exceeds_legal_dimensions before search (sites0/mutation0).
This still needs a fix, not a relaxed resident/load requirement. Do not mark
P4E.4 green. Evidence: p4e-defense-full-tests-20261003.txt and
p4e-autocannon-first-shot-rearm-reject-20261003.log.

Resume: repair funded twelve-resident power-room demand/packing; rebuild and
run both suites; validate real finite rearm, close support, power loss and a
fresh new-policy generation/reload. Then commit P4E.4 and finish P4E.5's
integrated primitive/terrain/quest/fallback/recovery matrix. Preserved save
RealisticBases-P4E3-three-manned.rws and the captured rejected-map regression
remain reusable. Normal user profiles/saves were not changed.

## P4E.4 — Policy and native service verification

The pause was explicitly revoked by the user's continuation. Corrected the
preflight power-room cap: MaxSplit=8 limits room splitting, not generator count.
The generator bound now comes from legal12x12 room area; actual furnishings
still enforce working cells, aisles and batteries. The added dispatchable load
is included in upfront demand, not hidden until late placement. Original
four/seven/twelve-resident clutter acceptance passes with the full roster.

Final matched three-map totals at100→125%: Mid normal7→7, Mid wealthy6→10,
Late normal5→8, Late wealthy4→14. 150% again provides no additional guns in
this corpus. Eight actual autocannons/four uranium guns are selected; every
claimed gun passes concurrent guard/close-support, geometry, cost and power
checks. Adopt125% provisionally, retaining the original wealth distribution
and equipment-funded mean7/cap4..12. Primitive/early/quest exclusions pass.
Latest complete suite: PASS failures0,776561ms; earlier reserve-aware full
suite PASS846486ms. Smoke top-up and new doctrine fixtures also pass.

Native autocannon63497 firing: actual projectile, fuel90→89. Rearm now uses
the actual RearmTurrets worker. Movement receives bounded gateway stages around
the native JobDriver_Refuel pipeline, retaining reservations, pickup/opportunity
pickup,240-tick service and Toils_Refuel finalization. At4889, real funded
worker64313 rearmed with75 existing steel, fuel0→37.5 and reserve260→185;
outsideBounds0. Artillery is explicitly depleted only by this disposable probe
to isolate direct-fire service and keep the visitor alive; no extra supplies.

Actual conduit loss:303 owned conduits removed, unpowered but fueled gun fires
zero shots and consumes no fuel over300ticks (37.5 unchanged). Native close
support: defender64212 fired Bullet_AssaultRifle from covered(61,21), about ten
cells from gun(62,31), while the gun was unpowered. These probes auto-pause.
Evidence: p4e-autocannon-rearm-powerloss-pass-20261003.log and
p4e-nearby-pawn-support-smoke-charge-20261003.log.

User-authorized logistics smoke: the existing support kit already resolves
Apparel_SmokepopBelt only at legal IndustrialMid+ research. Prefer its existing
carrier for critical logistics and preserve healthy crews. Native NPC
automatic activation is limited to nearby hostile-turret bullet impacts, so a
logistics worker may proactively pop before an already-approved exposed
exterior leg. Smoke never grants unbounded movement permission. Three finite
native charges; no automatic refills/technology grants. Native probe consumed
64313's existing charge3→2, then blind smoke was observed at the original cell
fifteen ticks later (queued explosion effect). Evidence:
p4e-native-smoke-observed-20261003.log.

If ordinary gear degradation would drop that legal pack, a separate bounded
utility allowance funds at most its actual value (cap300), without affecting
resident affordability/count. Otherwise no top-up is used. Pure boundary and
primitive/early legality cases pass; authoritative quest gear budgets remain
unchanged. Fresh new-policy generation/reload and integrated P4E.5 follow.

Fresh new-policy generation on the same saved160x160 tile/seed passed:
layout7DA8A26BC6B45C25, ten residents (unchanged), sixteen rooms,342 crops,
three magazines/mortars and five turrets in four coherent groups. All5/5 guns
are concurrently guarded and close-supported;470W, defense import4344.
Declared base imports8250→10313 at125%, dispatchable reservation960W,
four actual generators with60 startup/204 finite stored wood. No extra roster,
research or trade-stock appropriation. Native smoke was observed at the
original pop cell after15ticks with real charge3→2; the invocation-tick false
gas sample was queued-effect timing, not a refill.

Fresh active reload584→585 preserves ten defenders/assignments, three gun/post
identities and untouched twelve-HE/two-incendiary/two-spare ledger; medicine
remains the same25-unit shelf stack64076. Initial generation-tick power/room
temperature samples are not thermal settling evidence. P4E.4 is complete;
integrated settling/primitive/terrain/quest/recovery coverage remains P4E.5.
