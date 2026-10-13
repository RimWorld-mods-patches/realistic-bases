# Review of the proposed settlement architecture

2026-09-24. Original recommendation reviewed on 2026-09-24. The user subsequently accepted the bounded ideas into future P5/P6 work and requested P7 specialization as the final planned milestone; see [implementation plan](implementation-plan.md). The assessment below is retained as review evidence, not an instruction to expand P4/P4A.

**Recommendation: keep our current P4A architecture and borrow a few targeted ideas.** The document is useful as a checklist of spatial relationships and failure cases. It is not a reason to restart the planner, import a large room taxonomy, or introduce a general optimization framework. Our immediate priority remains finishing and validating the procedural generator.

**Evidence and limits.** I read the supplied conversation export (not in the repository) and compared it with implementation commit `110c78f711cb41ea34c1545a63108633f7665fc5` on `codex/p4-finish`, including the current progress tracker. The main checkout contains older planning documents; the comparison uses the implementation worktree. Code inspection and recorded runtime findings are distinguished below; I did not run a new game test for this review.

The export is a proposed design, not a validated specification. Its claimed 55×55 matrix and 333 relations are in linked `sandbox:/mnt/data/...` attachments that are not included in the supplied Markdown. Only the visible excerpt and examples were reviewable. Room counts, DLC coverage, numerical weights and claimed wiki-derived rules should not become requirements without checking the relevant game definitions. The colleague's requests for Multiplayer and friendly visits do not automatically extend our scope.

**What we already have**

| Proposal in the export | Current implementation / decision |
|---|---|
| Separate settlement requirements, a pure layout, and spawned game objects | Already present in `LayoutRequirements`, `ProceduralLayoutInput`, `ProceduralLayout`, and `P4ProceduralSpawner`. Keep these boundaries. |
| Weighted room relationships with hard constraints | Already present in `LayoutRelationship`: near/far preferences, weights and hard maximum route distance. No matrix subsystem is needed. |
| Vary preferences by seed | Already present: soft relationship weights receive deterministic perturbations; hard rules are not perturbed. |
| Plan using actual terrain rather than landform names | Existing terrain snapshots, excavation ownership and geometry checks follow this approach. Complete the procedural terrain matrix rather than add a second terrain-provider hierarchy. |
| Keep public circulation separate from private rooms | The procedural validator already floods a public component and checks individual room access. Preserve this as a correctness rule. |
| Validate before spawning and audit the actual map | Already part of our design and runtime work, including transactional recovery and cooler-exhaust checks. Complete the outstanding coverage; do not duplicate validators to match the proposed class list. |
| Stable settlement seed, tech profile and diagnostics | Seeds, faction stages, planner identifiers, digests and bounded search statistics exist. This is a foundation, not proof of full revisit persistence or cross-version reproduction. |

Relevant code: [relationship model](../Source/RealisticBases/Planning/ProceduralLayoutModel.cs#L405), [placement scoring](../Source/RealisticBases/Planning/ProceduralLayoutPlanner.cs#L1377), [final layout validation](../Source/RealisticBases/Planning/ProceduralLayoutValidator.cs#L16), and [runtime planning inputs](../Source/RealisticBases/Integration/P4AMapPlanning.cs#L26). These paths identify the reviewed implementation; line positions may change during ongoing work.

**Ideas worth using, in priority order**

| Idea | Smallest useful application | Timing |
|---|---|---|
| Distinguish proximity, access and isolation | Keep reachability mandatory and convenience mostly soft. A nearby room does not necessarily need a shared wall or direct door. Add specific functional checks only where an actual facility needs them. | Apply during current P4/P4A validation; largely existing behavior. |
| Measure important routes, not just visual proximity | Report a handful of route lengths over final occupancy. Start with food storage–kitchen, food storage–dining, workshop–materials and defensive post–medical access. | Small diagnostic addition now; change placement only when evidence warrants it. |
| Add a few missing relationship preferences | Consider food storage–dining and medical–defense proximity. Reuse the current sparse relationship list and existing geometry; do not create every pair in a universal matrix. | After the procedural matrix is stable, unless a live failure makes one urgent. |
| Treat hazardous storage as a placement concern | Review where actual explosive fuel/shell definitions are stored. If needed, reserve a small separate accessible stock area away from beds, food and the sole evacuation route. | With P5 stock placement; earlier only for a demonstrated severe encounter defect. |
| Preserve deterministic decisions and useful diagnostics | Keep stable candidate ordering and fixed work budgets; record the inputs needed to reproduce a failed plan. | Maintain now; no Multiplayer project required. |
| Judge defenses as an encounter | Use raid observations to check firing opportunities, retreat, and breach response. Avoid optimizing exclusively for defender invulnerability. | Existing P4A/P3 regression, then P6 balance. |

The route improvement is particularly concrete. Placement currently scores relationships using Manhattan distance between door exterior cells. The validator already checks **hard** relationships using actual public walking distance. Thus the gap is route quality for soft preferences, not a missing connectivity engine.

Start by measuring routes on the accepted plan. Use the existing public mask plus permitted local paths to actual fixtures or stock cells; do not make private rooms shortcuts. Cache a distance field per relevant endpoint instead of running every room-to-room path query for every placement candidate. These are distance proxies, not measured daily hauling times.

Only if the reports reveal repeated bad arrangements should we score a small, fixed number of already valid alternatives by those routes. Keep the current hard validator authoritative. Do not turn convenience penalties into new rejection conditions on difficult terrain. Compare validity rate, route lengths, layout diversity and search work against the same seed/terrain set before accepting the change.

The current relationship list already covers kitchen–food, kitchen–dining, housing–dining, housing–medical, housing–jail separation, workshop–storage and workshop–power. Adding those again would accomplish nothing. A food-storage–dining preference is a plausible incremental improvement because kitchen proximity alone does not express residents' route to meals.

Hazard isolation deserves attention without becoming a blast simulator. The reviewed spawner puts spare fuel into storage and also puts combat supplies there. The actual risk depends on the selected definitions and realized geometry; this review does not claim a demonstrated chain explosion. A separate stock area, where justified, is a smaller change than adding mandatory ammunition, chemfuel, armoury and logistics buildings to every settlement. Neither reserves nor trade goods should be counted twice.

**Where the proposal needs correction**

1. **A symmetric adjacency matrix combines different meanings.** Desire for short travel, shared walls, clean-room separation, security and blast separation are not interchangeable weights. A directed workflow such as ingredients → stove → meals also need not imply asymmetric walking distance. Keep a sparse list of explicit purposes, with only the constraints we actually need.

2. **Some suggested separation is stronger than necessary.** Kitchen and butchery can be separate rooms without being far apart. A negative distance weight is not a model of cleanliness. Likewise, a medical room should have a usable protected route from defenders, but a separate triage building is not required for every small base. Do not introduce unsupported mechanics such as generic workshop noise penalties.

3. **Furniture must participate in planning.** One pipeline puts furnishing after final candidate selection. That is too late if benches, coolers or multi-cell buildings can block movement or require a particular wall. Keep P4A's fixture footprints, interactions and exhaust reservations in the plan, then validate the realized map as well.

4. **Defense cannot be an unconstrained final decoration pass.** Reserve perimeter thickness, gates, crossings, firing space and retreat access while laying out the settlement. The document's separation of responsibilities is useful; its sequential picture must not recreate our earlier sealed second-wall problem.

5. **Player-colony advice is not automatically an NPC requirement.** We need a believable hostile settlement with essential support for the visit, not a colony that wins every raid type and operates indefinitely. No new prison-break, anomaly-containment, nursery or infestation simulation is warranted for the current milestone. Existing needs relief, refueling and reload behavior remain required.

6. **A seed and version string do not preserve a destroyed settlement.** Reproduction also depends on terrain, definitions, settings and algorithm behavior. Recording a version does not retain its old implementation. Nor does keeping every visited map indefinitely settle lifecycle and performance questions. Keep P5's explicit depletion, casualties, ownership and trade reconciliation requirements.

7. **Do not copy the suggested integration hook literally.** The export itself says its XML/Harmony approach needs version-specific verification. Our integration already has runtime evidence and specific generation ordering; changing inheritance or hooks just to match the proposal would add risk without improving layouts.

**Defer or reject at this stage**

| Proposal | Decision and reason |
|---|---|
| Simulated annealing, many room mutations, swappable solvers or Wave Function Collapse | Defer. Moving or merging a room invalidates fixtures, corridors, power, gates and roof checks. First measure whether the bounded existing planner fails our quality targets. |
| Mandatory cluster planner | Defer. Existing room ordering and preferences may be sufficient at current sizes. If larger settlements become incoherent, try a small placement-order/grouping heuristic before a second layout hierarchy. |
| Full 55-room catalog and general room-merging engine | Defer. Add supported facilities when a profile or gameplay feature needs them. A combined dining/recreation furnishing pattern does not require arbitrary room merging. |
| Fourteen modules, service registries and an interface for every stage | Reject as an immediate refactor. Extract components when complexity or a real alternate implementation justifies them. Class count is not a quality target. |
| XML Defs for every adjacency and architecture rule | Defer until external customization is an actual requirement. Current typed data is easier to validate during rapid planner changes. |
| Five movement/access layers | Defer. Public circulation, room-local access and existing military geometry are enough for current hostile settlements. A visitor permission system solves a different feature. |
| Construction history, ruins, friendly visits, gravships and broad DLC room support | Separate future features. They do not help close the current procedural raid generator. |
| Schedule simulation and traffic estimates from production chains | Defer. A few route weights are enough; full labor/economic optimization conflicts with our bounded MVP sustainability scope. |
| Traps, IEDs, trap tunnels or a trap-density setting | Reject. Explicitly excluded by the user, including tribal profiles. Do not import these catalog entries or doctrine defaults. |
| Multiplayer integration and certification | Outside the initial release. Retain deterministic engineering practices without adding synchronization adapters or claiming support. |

Multiplayer's own API documentation distinguishes synchronized methods, fields and object serialization; seed control alone cannot establish compatibility. If support is requested later, it needs a separate audit and actual multi-client tests of generation, UI commands, AI, reload and recovery. [Multiplayer API documentation](https://github.com/rwmt/MultiplayerAPI/wiki).

Geological Landforms remains in current scope. Keep reading the generated map through our existing snapshot. Add a narrow optional metadata adapter only when a specific decision cannot be made from map cells; do not create two parallel geometry implementations. The upstream project is a reference for a tested adapter, not evidence that every landform already works with our mod. [Geological Landforms source](https://github.com/m00nl1ght-dev/GeologicalLandforms).

**Recommended implementation follow-up**

| Order | Bounded action | Completion evidence |
|---|---|---|
| 1 | Finish the existing P4/P4A procedural runtime matrix. Use this review to check actual job access, food access, exhaust, gate crossings and retreat routes. | Required current gates pass on the procedural path; no new milestone is introduced. |
| 2 | Add the small route report if it helps explain observed layout problems. | A barrier/detour fixture proves routes follow occupancy; same inputs produce the same report; search limits remain bounded. |
| 3 | Only where those results justify it, add missing soft relations or bounded route-based candidate scoring. | Same-seed comparison demonstrates improvement without unacceptable rejection rates, lost variety or search cost. |
| 4 | During P5, review reserve/trade storage and hazardous stock placement together. | Real quantities, access, no duplication and depletion/revisit behavior pass the existing P5 checks. |

Trade remains an important omission from the colleague's design: a trading zone and economic specialization do not make spawned goods match the settlement's actual offer. Our [supply and MVP scope](supply-and-mvp-scope.md) remains authoritative, including bounded encounter readiness and deferred long-term sustainability.

The useful contribution is better spatial criteria and a few focused diagnostics. The proposed architecture, larger feature catalog and optimization machinery should not delay a working, tested procedural settlement raid.
