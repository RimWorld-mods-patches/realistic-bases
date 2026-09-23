# P4B — Defensive layout planner

2026-09-24 · Planned, not started · After P4/P4A, before P5

The user authorized a distinct defensive-layout milestone: extend the procedural settlement with actual outer-wall layers, gates, turrets, barricades and sandbags. Reuse the current procedural planner, transaction and P3 controller. This document specifies future work; it does not claim these defenses already work on the P4A path.

## Boundary and integration

P4 owns terrain, excavation, roof safety, natural boundaries and environmental compatibility. P4A owns facility demand, room/fixture geometry, public circulation, the baseline enclosure and gate endpoints. It must expose/reserve feasible space for later fortification. P4B owns additional perimeter layers, defensive positions/cover, turret placement and their operational integration.

This is a stage of the same generation transaction, not a post-spawn decoration pass. Produce explicit defense footprints and interaction/service routes, merge them with the settlement plan, and validate final occupancy before mutation. Update power/material/stock requirements from selected defenses; do not consume space or power invisibly. Bounded adjustment may move an emplacement, change a legal equipment choice or request layout replanning. Report adjustments and reject impossible combinations; never disable connectivity validation to force acceptance.

Code inspection at `783b826` found that `WallLayers` expands gatehouse bands, while the general enclosure boundary is not expanded into complete extra wall layers. Geometry-based P3 posts are chosen by proximity to gates/walls, without establishing usable firing lanes. Catalog entries alone do not demonstrate spawned, coordinated turret/cover placement. Earlier two-/three-layer reports must be read in this narrower sense, not as P4B completion evidence.

## Bounded implementation steps

| Step | Work | Evidence before committing |
|---|---|---|
| P4B.0 | Establish contracts/reservations and the test checklist; inspect inherited implementation | Baseline facts and ownership recorded; existing validated behavior retained |
| P4B.1 | Generate 1–3 layers along eligible exterior-wall sections, coherent material choices, serial gate crossings and service/retreat access | Final occupancy across irregular outlines, corners, narrow terrain and all thicknesses; every entrance connected to the same public network |
| P4B.2 | Place defender posts, sandbags/barricades, and turret candidates around actual approaches | Range/line-of-fire and access checks; no ranged post accepted merely because it is next to a wall; final-map firing/cover observations |
| P4B.3 | Integrate selected defenses with P3, actual power, finite reload inputs and the construction budget | Supported turrets fire and consume inputs; rearm/service access, loss of power, depletion and local fallback observed |
| P4B.4 | Raid, reload, terrain compatibility and rollback matrix; closeout | Representative Core/GL procedural maps, forced recovery and player raid pass; no traps/IEDs; findings and tracker updated |

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

Evidence: installed [turret definitions](</Users/roman_gr/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Data/Core/Defs/ThingDefs_Buildings/Buildings_Security_Turrets.xml:425>) and [research definition](</Users/roman_gr/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Data/Core/Defs/ResearchProjectDefs/ResearchProjects_4_MultiAnalyzer.xml:287>). These are version-specific observations, not promises about arbitrary mods.

## A small amount of fortification theory

Historical bastions used projecting positions to reduce blind spots and create overlapping fire. Borrow the purpose, not a mandatory star-shaped footprint. [National Park Service: Bastions](https://www.nps.gov/casa/learn/historyculture/bastions.htm).

Historical defensive systems also connected positions with protected communication routes so troops and supplies could move between them. Borrow route continuity and mutual support, not trenches, earthworks or real-world dimensions. [National Park Service: The Fortification System](https://home.nps.gov/cwdw/learn/historyculture/the-fortification-system.htm).

Our proposed game adaptation is deliberately small:

- **Cover likely approaches:** score a bounded sample of traversable approach cells for actual weapon range and line of fire; report important uncovered approaches.
- **Reduce blind spots and support neighboring positions:** where geometry and budget permit, prefer positions that cover a gate's flank or support another post. Overlap is a soft preference, not a demand for universal double coverage.
- **Provide depth:** connect an outer position to an inner fallback already usable by the P3 controller. Extra wall thickness alone does not provide that behavior.
- **Protect movement and supplies:** preserve service/retreat routes and avoid routing routine traffic through intended firing lanes where practical. Do not infer that pure geometric separation guarantees no friendly fire.

These are design heuristics to test in RimWorld. Use current occupancy/pathing and actual weapon behavior as the authority. No global adversarial optimizer, complete star-fort generator, strategic siege simulator, terraformed moat, traps or IEDs. Keep exploitable tactical choices and difficulty budgets; the goal is a coherent, interesting settlement raid, not an invulnerable fortress.

## Exit and downstream responsibilities

A final integrated plan must retain all P4A facility/job, public-root, gate, arrival, roof, terrain and recovery invariants after defenses are added. Every staffed ranged post/turret must have a demonstrated useful firing opportunity under its intended gate state, with reachable service and fallback positions. Closed opaque gates must not leave the entire garrison ineffective.

Test all supported wall thicknesses, irregular and mountain/water-constrained layouts, direct and alternate player approaches, a breached gate/wall, blocked firing lane, turret power loss, uranium depletion/rearm, active reload and forced partial-spawn rollback. Keep pure geometry evidence separate from actual raid observations. Commit each validated step; do not mark P4B complete from labels, catalog legality or screenshots alone.

P5 uses the selected defense plan to budget actual power, fuel, uranium, shells and support work, maintaining reserve/trade separation and persistent consequences. P6 tunes difficulty and optional specialized mortar tactics. P7 remains the final planned specialization milestone.
