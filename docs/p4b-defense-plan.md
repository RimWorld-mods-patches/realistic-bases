# P4B — Defensive layout planner

2026-09-30 · Active; P4B.0–P4B.1 complete, P4B.2–P4B.3 in progress · After P4/P4A, before P5

The user authorized a distinct defensive-layout milestone: extend the procedural settlement with actual outer-wall layers, gates, turrets, barricades and sandbags. Reuse the current procedural planner, transaction and P3 controller. This document specifies future work; it does not claim these defenses already work on the P4A path.

## Boundary and integration

P4 owns terrain, excavation, roof safety, natural boundaries and environmental compatibility. P4A owns facility demand, room/fixture geometry, public circulation, the baseline enclosure and gate endpoints. It must expose/reserve feasible space for later fortification. P4B owns additional perimeter layers, defensive positions/cover, turret placement and their operational integration.

The user clarified the P4B.2 field relationship: the **main crop field remains on unroofed outdoor ground but should be enclosed by a wall**, with defender posts outside that field-facing wall. This does not mean planting crops in spare rooms or the inner courtyard. Field footprint, field-facing wall and gates, defender positions, and route reservations must be chosen together before mutation. The earlier gate-flank exterior-post prototype did not establish this relationship and is not accepted as P4B.2 placement evidence.

### Accepted joint-site planning direction

Before detailed room placement, derive actual crop-cell demand and a compound room/circulation/utility envelope from the population, stage and climate profile. Search compact connected outdoor soil patches, counting plantable cells after wall, gate, lane and post-clearance exclusions. Pair each field candidate with a neighboring compound envelope before selecting either; prefer fertile field soil and less fertile building ground, but reject a fertile patch without a viable compound. Rock-protected candidates may pair an outdoor field with adjacent legal excavatable ground. Keep the candidate and evaluation bounds deterministic.

Carry explicit crop, hauling-lane, field-wall/gate, outside-post and compound-envelope reservations into the existing procedural room planner. Detailed packing may adjust the compound envelope or advance to another pair, but must not consume crop capacity or silently lower demand. P4B then realizes the field wall, crossings and outside positions in the same validated spawn transaction; it cannot remain a diagnostic candidate. The two enclosures share a connected boundary, with a planned gate between compound and field, unroofed crops and outside-wall defender access/retreat. Supported no-outdoor-field profiles may use a separate path, but ordinary field failures may not be disguised as a supply-only profile.

This is a stage of the same generation transaction, not a post-spawn decoration pass. Produce explicit defense footprints and interaction/service routes, merge them with the settlement plan, and validate final occupancy before mutation. Update power/material/stock requirements from selected defenses; do not consume space or power invisibly. Bounded adjustment may move an emplacement, change a legal equipment choice or request layout replanning. Report adjustments and reject impossible combinations; never disable connectivity validation to force acceptance.

2026-09-28 clarification: detailed defenses may be planned **after the base's rooms and main perimeter have been generated**. Reserve only necessary connection points (gates, field interface, essential access) before that pass. Fit cover/emplacements against the completed layout, then revalidate final pawn paths, maintenance access, arrival, occupancy and budgets before committing the same spawn transaction. Do not require a large speculative defense footprint during room packing; the initial attempt to do that regressed the flat-clutter acceptance corpus. Optional equipment may step down or be omitted without rejecting an otherwise usable settlement.

### Attackable perimeter faces, not only the field gate

The user's next clarification requires positions on every attackable side and turret placement against actual incoming approaches. The post-layout pass now floods conventional attacker ground from walkable map edges, excluding the planned enclosure and permanent obstacles, then samples firing ground outside actual south/east/north/west perimeter faces. Water does not automatically make a side safe: reachable ground across a shoreline can still supply ranged attacks. Intact rock blocks the sampled sightline; drop pods, sappers and later demolition are outside this initial exposure model.

Each exposed side receives a usable existing field post, a new connected exterior position with forward/flank screens and short gate access, or a terrain/material omission reason. A dry corner position may serve two sides only when it is near both faces, has adjacent incoming-side cover and sees useful approach samples for both. Missing optional positions are reported, not disguised as defended sides or used to reject an otherwise viable base. Open-ground fixtures require all four sides to have posts.

The user's near-gate clarification supersedes the first three-barricade side-post prototype and its long exterior routes. Fit the same small connected projection as the field posts: a cardinally attached rear wall return, four front screens, one exposed-flank screen and a six-cell route from the gate. Prefer a usable existing gate; otherwise an ordinary one-cell built perimeter wall can receive a defense-only sally port when the immediately inside cell belongs to the public circulation network. Do not cut rooms, natural rock or reinforced sections, bypass serial gate layers, assume bridges, or count a sally port as a reinforced civilian arrival gate. Charge its wall-to-door replacement and return/screens, include it in closed-wall/path validation, P3 breach detection and the spawn transaction. Report an omission rather than reverting to a long unprotected lap around the base.

Turret candidates are generated from exposed-front connection points. The current bounded network favors a new front, uncovered approach samples, and then a second firing source on singly covered samples. The accepted 2026-09-30 work below replaces that ranking with staffed mutual-support value: reinforcing an existing group may be better than starting another front. Prefer a shorter gate route for otherwise equivalent candidates. The current exposed-face group cap (at most four) remains a search bound pending evidence, not a requirement to put a turret on each face. All groups share one construction/cover/loaded-ammunition/reserve budget and one dispatchable/total power allowance. A further group may step down to a mini-turret or be omitted with a reason without removing earlier valid defenses or rejecting the base.

Revalidate the whole network after every addition: final opaque turret footprints, inter-group blast spacing, unobstructed advertised maintenance paths, pawn-post lanes and civilian arrival. Every group must share useful actual approach targets with another gun or a verified covered pawn position; proximity alone is not interlocking fire. A large gun still requires a separate mini-turret and at least one useful pawn position covering sampled reachable dead ground at that front; field posts behind the opposite opaque wall no longer count by assumption. Report uncovered, single-source and overlapping sampled approaches rather than claiming universal protection. This is geometric support eligibility, not proof that a compatible pawn is staffed or firing.

P3 prioritizes the threatened side while holding an intact perimeter as well as responding to a breach, preserves the existing reassignment cooldown, and represents a shared corner as one physical post with multiple supported sectors. Each short post route still has two exposed gate-axis crossing cells; front screens do **not** prove mechanical cover for every movement direction. Protected turret maintenance, live fire/cover and reload remain exit gates.

Code inspection at `783b826` found that `WallLayers` expands gatehouse bands, while the general enclosure boundary is not expanded into complete extra wall layers. Geometry-based P3 posts are chosen by proximity to gates/walls, without establishing usable firing lanes. Catalog entries alone do not demonstrate spawned, coordinated turret/cover placement. Earlier two-/three-layer reports must be read in this narrower sense, not as P4B completion evidence.

## Adopted simulation-review work — 2026-09-30

The user accepted the following directions after review of [offline simulation findings](defense-sim-findings.md) at `600ac24`. These extend the open P4B.2–P4B.4 steps; they are planned work, not new completion evidence. Reuse the existing bounded planner and P3 controller.

1. **Threatened perimeter segments (P4B.3).** Track recent hostile attacks on gates and ordinary perimeter walls, actual breaches, and nearby hostile concentrations. Rank threatened segments using observed activity instead of relying only on the hostile pawn nearest the settlement center. Select reachable firing positions against that segment using actual weapon range, line of fire, cover and route time. Retain reassignment cooldowns and stable tie-breaking; do not give defenders knowledge of the player's future route. A decoy on another side must not automatically pull every defender away from an active wall assault.
2. **Bounded mobile reserve (P4B.3).** Allocate a reserve from the funded roster when staffing permits, stage it near connected internal routes, and reinforce through suitable gates. Prefer internal movement and short protected exits over long exposed exterior laps. Longer transfers between defended positions may be allowed after route validation, but territorial bounds and bounded path work remain mandatory. Preserve safe relief and usable fallback; the simulation does not justify unlimited pursuit or disabling wounded withdrawal. Do not count protected endpoints as proof of protection along the whole route.
3. **Staffed mutual-support scoring (P4B.2).** Compare a bounded set of turret-group candidates by useful combined fire from turrets and available compatible defenders, vulnerable-approach coverage, overlapping support and response access. Remove the unconditional preference for a new front ahead of this value. Permit reinforcing a group when it provides more useful defense than spreading guns; report weak or responder-dependent fronts. Retain blast spacing, adjacent directional cover, heavy-gun dead-ground support, power, finite ammunition, maintenance access and retreat paths. No universal eight-gun battery or fixed stand-off distance is prescribed.
4. **Real staffing allocation (P4B.2–P4B.3).** Use the actual loadout and resident count to allocate marksmen, ordinary shooters, mortar crew and reserve. Distinguish simultaneously staffed positions from alternative positions used after a threat changes direction. One marksman cannot be credited as concurrent protection for separated groups. A post may support several groups only when its actual firing geometry permits it. Record unstaffed dependencies and omissions; do not add unfunded residents or silently reduce the selected population. Reassign or fall back when a required defender becomes unavailable.
5. **Field-to-compound defense in depth (P4B.2–P4B.4).** Treat the agricultural enclosure as an outer defensive area with a connected fallback behind the shared boundary, covering entrances to the main compound. Field penetration must not automatically trigger abandonment of viable compound positions. Validate gate states, fallback fire, public access and routes under final occupancy. Report field penetration, compound penetration and garrison defeat separately in raid evidence and offline comparisons; preserve the existing cheap-breach statistic with its original definition when comparing historical results.
6. **Supported long-range defense (P4B.3–P4B.4).** Retain staffed mortars and stage-legal long-range guns, including IndustrialLate uranium slug turrets, as options against stationary distant attackers. Validate actual targeting opportunities, close support, consumption, rearming and operator loss. Preserve finite shell/uranium reserves. The short simulated attack does not establish that twelve shells are enough for the planned visit or justify reducing stock. P5 still owns encounter reserve sizing and P6 owns broad difficulty/budget calibration.

Implement threat detection and bounded reinforcement first, then staffing-aware group scoring and compound fallback, before tuning spending. Keep the ordinary four-to-twelve/mean-seven population policy. Neither a blanket 3–4× defense-budget increase nor a larger roster is adopted from the simulation. If fixed-budget comparisons show a remaining equipment shortfall, record it for P5/P6 rather than hiding it with extra resources.

### Validation for the adopted work

- Pure deterministic fixtures cover a wall assault with intact gates, a decoy plus another active front, stable assignment under shifting threats, bounded reinforcement paths, incompatible or missing marksmen, competing mortar/reserve staffing, and loss of a staffed support position. Check that no pawn occupies two concurrent roles.
- Compare candidate scoring on the same terrain, roster, loadout and budget. Report approach coverage with actual staffing, response travel, optional omissions, spending and generation acceptance/work bounds. Concentration is a hypothesis to test, not permission to remove spacing or path validators.
- Live raids include a non-gate wall assault, a feint or split approach, stationary sniper harassment, field penetration followed by compound fallback, and a reinforcement route under fire. Verify real firing/cover, no pursuit beyond the defended area, finite operation, and active save/reload of the revised assignments. Keep the existing Core/GL and rollback gates.
- Offline results are supporting evidence only. Moving defenders are untargetable in the current harness, turret explosions are omitted, placement rules are relaxed, and the production baseline uses static posts rather than the complete live P3 controller. Its wounded-withdrawal model omits recovery/useful fallback. Before using numerical results to select a production policy, correct the relevant simplification or validate that policy in game. Broad simulator expansion, learned AI and a runtime combat optimizer are not P4B prerequisites.

## Bounded implementation steps

| Step | Work | Evidence before committing |
|---|---|---|
| P4B.0 | Establish contracts/reservations and the test checklist; inspect inherited implementation | Baseline facts and ownership recorded; existing validated behavior retained |
| P4B.1 | Generate 1–3 layers along eligible exterior-wall sections, coherent material choices, serial gate crossings and service/retreat access | Final occupancy across irregular outlines, corners, narrow terrain and all thicknesses; every entrance connected to the same public network |
| P4B.2 | Enclose the unroofed main field; fit exterior posts/cover and staffing-aware supporting turret groups against actual approaches; provide compound fallback positions | Field/wall/gate/route/roof occupancy, exposed-side inventory, actual staffing, range/line-of-fire, access and fixed-budget comparisons; final-map firing/cover observations |
| P4B.3 | Integrate segment-level threat response, bounded reserve movement and exclusive staffing with P3, actual power, finite reload inputs and the construction budget | Decoy/wall-assault response; supported turrets/mortars fire and consume inputs; rearm/service access, operator/power loss, depletion and field-to-compound fallback observed |
| P4B.4 | Raid, reload, terrain compatibility and rollback matrix; closeout | Representative Core/GL maps, split approaches, sniper harassment, reinforcement under fire, distinct field/compound outcomes, forced recovery and player raid pass; no traps/IEDs; findings and tracker updated |

A wall layer is additional physical thickness along a declared section, not a counter or an extra gate alone. Separate fighting positions/fallback lines express defense in depth; do not build three concentric occupied forts by default. Natural rock/water constraints must be respected. Log omitted/reduced sections and their reason; do not label an incomplete shell a complete three-layer wall.

Choose materials through faction capability, actual allowed stuff, local availability, bounded imports and relevant durability properties. Reserve crossings through every wall layer before solidifying them. Maintain room/job access, external arrival, and defender retreat independently. No outside detour may substitute for internal connectivity.

Use a few small adaptable arrangements (gate defense, covered position, turret emplacement). Their placement follows generated geometry, not fixed whole-settlement templates. Do not assume shooting through walls, walkable wall tops, embrasures or vertically elevated firing positions that the supported game does not provide. Barricades and sandbags are alternatives selected by capabilities/resources, not mandatory duplicates at every post.

## Turret eligibility, including uranium slug turrets

Include vanilla mini-turrets and autocannons at their supported stages, and **make uranium slug turrets eligible from `IndustrialLate` (high industrial), inherited by later stages**. Eligibility does not require every eligible settlement to own one. Terrain, construction/import value, power, maintenance inputs and encounter strength limit selection.

The installed Core 1.6 definitions establish:

| Item | Observed definition / consequence |
|---|---|
| Building | `Turret_Sniper`, displayed as uranium slug turret; gun `Gun_TurretSniper` |
| Research | `SniperTurret`, Industrial; prerequisites `MultiAnalyzer`, `HeavyTurrets`, hidden prerequisite `PrecisionRifling` |
| Current mod gap | Defense choices omit `Turret_Sniper`; the `IndustrialLate` research set omits `SniperTurret`. Update both, not just the display label. |
| Reload input | `CompRefuelable` accepts `Uranium`; this is separate from mortar shell/barrel stocks |
| Geometry and operation | The inspected definition specifies minimum range 11.9, maximum range 45.9, 150 W consumption and an explosive component with radius 5.9. Read resolved runtime defs instead of hardcoding these values. |
| Consumption | The inspected base values specify capacity 30, one fuel unit per shot and multiplier 0.5, with difficulty scaling enabled. Budget and test effective consumption; do not assume a fixed uranium cost across difficulty settings. |

Add the research and role/catalog entry through existing legality rules. Check prerequisite closure, lower-tier exclusions, runtime catalog parity and resolved turret footprint/range/power/reload data. Do not invent a uranium mortar-shell item. Include initial loaded ammunition in the finite supply ledger and separate operating uranium from sale stock.

A uranium turret needs a usable longer-range lane and another appropriate position/weapon covering its close blind zone where feasible. Respect actual line of fire, friendly movement and turret explosion consequences when spacing emplacements. Use simple checks against nearby vulnerable assets and critical paths, not a general blast simulator. Validate actual firing, uranium consumption/rearming, exhaustion and save/reload in a high-industrial encounter, plus a lower-tier rejection case. P4B proves operation with finite stocks; P5 calibrates encounter-wide reserves and ownership/persistence.

Evidence: installed turret definitions (`Data/Core/Defs/ThingDefs_Buildings/Buildings_Security_Turrets.xml:425` in the game folder) and research definition (`Data/Core/Defs/ResearchProjectDefs/ResearchProjects_4_MultiAnalyzer.xml:287` in the game folder). These are version-specific observations, not promises about arbitrary mods.

## A small amount of fortification theory

Historical bastions used projecting positions to reduce blind spots and create overlapping fire. Borrow the purpose, not a mandatory star-shaped footprint. [National Park Service: Bastions](https://www.nps.gov/casa/learn/historyculture/bastions.htm).

Historical defensive systems also connected positions with protected communication routes so troops and supplies could move between them. Borrow route continuity and mutual support, not trenches, earthworks or real-world dimensions. [National Park Service: The Fortification System](https://home.nps.gov/cwdw/learn/historyculture/the-fortification-system.htm).

Our proposed game adaptation is deliberately small:

- **Cover likely approaches:** score a bounded sample of traversable approach cells for actual weapon range and line of fire; report important uncovered approaches.
- **Reduce blind spots and support neighboring positions:** where geometry and budget permit, prefer positions that cover a gate's flank or support another post. Overlap is a soft preference, not a demand for universal double coverage.
- **Provide depth:** connect an outer position to an inner fallback already usable by the P3 controller. Extra wall thickness alone does not provide that behavior.
- **Protect movement and supplies:** preserve service/retreat routes and avoid routing routine traffic through intended firing lanes where practical. Do not infer that pure geometric separation guarantees no friendly fire.

These are design heuristics to test in RimWorld. Use current occupancy/pathing and actual weapon behavior as the authority. No global adversarial optimizer, complete star-fort generator, strategic siege simulator, terraformed moat, traps or IEDs. Keep exploitable tactical choices and difficulty budgets; the goal is a coherent, interesting settlement raid, not an invulnerable fortress.

### Coherent defense groups, not scattered turrets

The user's stronger fortification requirement supersedes optional close-blind-zone support for large guns: an autocannon/uranium emplacement must be accompanied by a legal mini-turret and useful pawn firing positions covering sampled dead ground. If that group cannot fit or be supplied, use a small emplacement or omit the gun; do not place an isolated large gun. Keep group members out of each other's explosion envelope, and use the resolved turret firing origin and ranges rather than granting a shot from any occupied footprint tile.

Defender positions need adjacent forward cover, exposed-flank protection, and a covered route from their gate. Turret cover must be immediately adjacent on the incoming-fire side, not a remote decorative line. Post-layout fitting now provides small connected rear wall returns, forward/flank screens and explicit routes to the two field posts. The two open gate-axis crossing cells are explicitly exposed; the lateral approach runs adjacent to its forward cover. An incomplete terrain-constrained arrangement carries an omission reason and does not enter P3 as a usable post. Broader bastion-like projections and protected turret-maintenance/retreat corridors remain work to validate.

The community wiki describes sandbags and barricades as equivalent 55% low cover, with cover supplied by adjacency to the defender. It also notes that in 1.6 enemies can stand and shoot on these objects, so they cannot be relied on to prevent enemy firing positions. [Sandbags](https://rimworldwiki.com/wiki/Sandbags), [Barricade](https://rimworldwiki.com/wiki/Barricade).

The wiki reports minimum ranges of 8.9 for autocannons and 11.9 for uranium guns, and a direction-sensitive cover issue for 2×2 turrets based on older empirical testing. Resolved Core defs establish our range facts; live hit/cover tests must establish current 1.6 cover behavior instead of assuming a ring of bags protects a large turret equally from every side. [Autocannon turret](https://rimworldwiki.com/wiki/Autocannon_turret), [Uranium slug turret](https://rimworldwiki.com/wiki/Uranium_slug_turret).

## Exit and downstream responsibilities

A final integrated plan must retain all P4A facility/job, public-root, gate, arrival, roof, terrain and recovery invariants after defenses are added. Every staffed ranged post/turret must have a demonstrated useful firing opportunity under its intended gate state, with reachable service and fallback positions. Closed opaque gates must not leave the entire garrison ineffective.

Test all supported wall thicknesses, irregular and mountain/water-constrained layouts, direct and alternate player approaches, a breached gate/wall, blocked firing lane, turret power loss, uranium depletion/rearm, active reload and forced partial-spawn rollback. Keep pure geometry evidence separate from actual raid observations. Commit each validated step; do not mark P4B complete from labels, catalog legality or screenshots alone.

P5 uses the selected defense plan to budget actual power, fuel, uranium, shells and support work, maintaining reserve/trade separation and persistent consequences. P6 tunes difficulty and optional specialized mortar tactics. P7 remains the final planned specialization milestone.
