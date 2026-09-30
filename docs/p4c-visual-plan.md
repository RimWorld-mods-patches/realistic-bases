# P4C — Visually realistic bases

2026-09-30 · Planned and authorized for implementation · Based on the procedural P4A/P4B generator

## Outcome and scope

Make generated settlements look intentionally built and inhabited: coherent floors by room use, finished interior rock walls where appropriate, and restrained paths joining important entrances. Preserve the generated layout, terrain character, faction capabilities and ordinary population policy. A poor tribal settlement and a wealthy industrial settlement should differ through a small material palette and finish priorities, not unrelated decoration on every cell.

This is a bounded finishing pass over the existing procedural settlement. It does not introduce another room generator, new assets, a beauty optimizer or continuous cleaning/construction jobs. Existing Core definitions are sufficient for the first implementation. Research rooms, missing climate control, workstation seating, medicine shelving and mortar micro-storage remain P4E. Preserve the completed P4B implementation and its combat behavior; P5 stock/persistence remains separate.

## Finish policy

Select one primary utilitarian floor, a compatible domestic finish and at most one accent finish from resolved legal definitions. Reuse the palette across rooms. Vary choices through a purpose-separated seed and stable ordering; choosing finishes must not reroll geometry, crops, equipment, residents or defensive assignments. Use room function, faction stage, local materials and saved wealth/spend capacity. Research and resource legality take precedence over appearance.

| Area | Intended treatment | Limits and fallback |
|---|---|---|
| Kitchen, food processing, medical space | Continuous practical, cleanable floor across the usable room interior | Prefer affordable legal finishes; sterile flooring is an optional researched/funded medical upgrade, not a universal requirement. Do not assume a decorative floor supplies cleanliness beyond its resolved stats. |
| Bedrooms/barracks, dining and recreation | Coherent domestic finish; an affordable accent may distinguish shared social rooms | Wood, local stone or another supported finish only where appropriate; carpet is optional and capability/resource constrained. Poorer settlements use simpler finishes. |
| Prison | Basic durable floor matching the settlement palette | No forced luxury; preserve all containment/access geometry. |
| Workshops, storage and power/fuel rooms | Simple utilitarian finish; prefer nonflammable choices near stored fuel and power equipment | No blanket carpet/wood placement. Existing safe terrain or a cheaper legal finish is preferable to an unsuitable upgrade. |
| Excavated rooms and internal corridors | Smooth eligible natural rock floors/walls, or lay a coherent legal floor where appropriate | Use the original rock's supported smooth equivalent; preserve roof support, opacity and the mountain outline. Smoothed stone can itself be the final finish. |
| Public paths and door thresholds | Finish existing main internal connections and short entrance links where useful | Follow reserved circulation; do not pave every yard, exterior combat route or the whole enclosure. Use consistent thresholds instead of random checkerboard patches. |
| Crop fields and natural surroundings | Retain productive soil and terrain character | No floor on crop reservations, water, preserved landforms or unrelated terrain. Preserve unroofed fields and outdoor exhaust/mortar requirements. |

These are priorities, not mandatory exact def names. Resolve terrain construction requirements, research, costs, flammability, cleanliness and walkability from the installed game. Unknown or unsupported definitions receive a conservative omission/fallback reason. Do not enable arbitrary modded/DLC finishes by label or assumed equivalence. A warmer or colder climate does not justify changing a floor without an applicable game mechanic or material reason.

Prefer coherent room-sized choices over scattered partial luxury. Spend on functional interior finishes before domestic upgrades, then optional paths/accents. Use a bounded finish allowance within the existing resource/import accounting: account for construction value and material demand once, keep imported finishes within remaining allowance, and do not debit the physical operating/trade reserve a second time. Local construction material assumptions must be declared consistently with the existing planner; no infinite local stone or wood inferred merely from biome name. If the existing ledger cannot represent a finish, use a conservative explicit bound and document it rather than introducing a settlement-wide economy simulation. Finishes may downgrade or be omitted without rejecting an otherwise valid settlement or taking funding from already selected defenses/facilities.

## Interior rock smoothing

Identify retained natural-rock cells forming the boundaries of planned rooms or internal corridors. Smooth only selected owned cells adjacent to those interiors, prioritizing occupied/clean rooms and coherent contiguous sections. Leave exterior mountain faces, unrelated caves, ore deposits, unsupported modded rock and already foreign-owned structures untouched. A finished inner boundary may share a physical wall with the exterior; eligibility follows the actual whole-cell conversion and its consequences, not an assumption of face-only painting.

Inspect the installed smoothing API and resolved source-to-smooth definitions before implementation. Preserve cell footprint, impassability, opacity and roof-holder behavior. Record any durability/beauty or other gameplay-stat change; smoothing is not purely a texture substitution. Never briefly remove a supporting wall in a way that can collapse retained roofs. Revalidate natural-wall classification and roof support for smoothed equivalents, including downstream audits that currently recognize only natural rock. Omit unsafe conversions with a reason.

## Planning and transaction contract

1. Produce a small deterministic finish plan containing floor cell/def choices, rock conversions, reasons for exclusions/downgrades, cost summary and a version/digest. Integrate it after room/route/defense geometry is fixed but before the shared spawn transaction commits. Reuse current room/fixture/circulation masks; no new global spatial solver.
2. Include the full intended room interior, including appropriate cells under furniture, while excluding walls and all protected terrain/crop masks. Resolve conflicting room/threshold choices deterministically. Respect terrain support requirements and existing layers; this pass cannot create bridges, dry water or manufacture new buildable land. Confirm final fixtures and interaction cells remain legal.
3. Extend explicit mutation ownership to floor changes and selected retained-rock conversions. The current `P4SpawnTransaction` snapshots plants, excavated rock and roofs but has no floor terrain rollback; its unowned-rock checksum would also reject smoothing. Snapshot original terrain layers (including under-terrain where applicable) and the original rock state needed for exact restoration. Inspect current APIs rather than assuming one terrain def is enough.
4. Audit intended changes inside the declared finish footprint and unchanged state outside it. Update expected checksums with the explicit approved mutations; never broadly disable terrain, natural-rock or roof preservation checks. Share ownership with excavation/spawn correctly so rollback neither recreates a smoothed wall twice nor deletes unrelated things.
5. On forced failure after floors and after rock conversion, restore original terrain layers, rocks, roofs and cleared plants, then prove a retry succeeds. Generation rejection before mutation remains a zero-change path. Recheck final doors, jobs, public circulation, defense/service routes and retained roofs after finishes.
6. Ordinary save/reload must preserve committed floors and smooth walls. Do not reapply finishes to already loaded settlements or overwrite player changes. Record a compatible finish version/identity where required by the current generation-state contract; full unload/revisit reconciliation remains P5 and must not be claimed from an active reload test.

## Implementation checkpoints and progress

Commit each validated step separately and record concrete evidence in `docs/p4c-findings.md`. Retain prior milestone evidence; P4C introduces no requirement to repeat unrelated P4B work.

| Step | Scope | Validation gate | Status |
|---|---|---|---|
| P4C.0 | Inspect Core finish/smoothing APIs, catalog and transaction boundaries; freeze the small supported palette and ownership contract | Record resolved definitions, legality/cost assumptions, baseline examples and recovery design; no runtime success inferred | Pending |
| P4C.1 | Pure finish plan, room priorities, coherent palettes, bounded accounting and deterministic downgrade/omission | Same input gives same finish digest; original layout/loadout unchanged; floors exclude protected cells; stage/material/budget and constrained-terrain cases | Pending |
| P4C.2 | Transactional floors and selected rock smoothing, final audits and diagnostics | Complete relevant pure suites and Release build; forced failures after each mutation class restore exact originals and allow retry; paths/roofs/fields remain valid | Pending |
| P4C.3 | Visual review, active reload, compatibility and closeout | Representative live examples and screenshots, Core/available GL check, recorded costs/omissions, active save/reload, no generation/path/roof regression; findings/tracker updated | Pending |

## Evidence and acceptance

- **Pure cases:** poor tribal and industrial-mid/late profiles with their actual budgets; open ground and excavated rock; mixed underlying soil/rock; crop and shoreline exclusions; unavailable finish/smoothing definitions; insufficient allowance; doorway/room boundary joins; deterministic replay. Include an existing terrain-layer case and a retained roof-support wall in recovery tests. Verify cosmetic seed changes do not alter layout, residents or equipment.
- **Live examples:** an open-ground industrial settlement, a mountain settlement with eligible smoothed interior walls, and a modest tribal settlement. Include a water/crop boundary in the corpus. Compare before/after views of the same layout, or baseline and final terrain/finish overlays from one recorded generation, rather than attributing different random geometry to finishing. Capture an overview and close views of domestic rooms, kitchen/medical space, storage/workshop and mountain boundaries where present.
- **Visual review:** rooms read as coherent spaces; thresholds and corridors join sensibly; no random one-cell material noise, carpeted utility yards, paved crops, raw unexplained holes in otherwise finished rooms or smoothing of the whole mountain. Document intentional rough areas and budget downgrades. Fix observed defects; screenshots supplement mechanical checks.
- **Mechanical review:** actual resolved floors/rock replacements and costs match the plan; furniture and stock placement, interactions, public paths, gate/P3 routes, outdoor crops, exhaust spaces and retained roofs still pass. A material's real gameplay effects are acknowledged rather than called cosmetic. No extra residents, luxury loot stock or unlimited resources are spawned.
- **Recovery and compatibility:** force failure after floor placement and after smoothing, compare pre/post terrain layers and rock/roof/plant state, and retry. Save/reload a finished mountain or mixed-terrain map and verify finishes and ordinary behavior persist. Run a representative Geological Landforms case if available in the established disposable profile; unsupported terrain must be preserved or explicitly skipped, never rewritten to fit the palette. Missing compatibility evidence remains an open gate, not an assumed pass.
- Run the relevant existing pure suites and required Release build once the integrated change is ready. Use the repository's disposable live-game workflow, with verified process/profile ownership; never use normal saves. No new continuous simulation or broad screenshot-testing infrastructure is required.

**Exit:** the production procedural generator applies coherent, affordable and faction-appropriate finishes; eligible interior mountain rock is safely smoothed; crops/natural surroundings and functional/defensive geometry remain valid; forced rollback/retry and active reload pass; representative visual and compatibility evidence is recorded. Unavailable optional finishes degrade gracefully. P4C completion does not stand in for P4D quest support, P4E facilities or P5 persistence/supply work.
