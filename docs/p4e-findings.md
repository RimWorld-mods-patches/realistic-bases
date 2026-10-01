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

One usable cell plus walls/door is a 3×3 enclosure using ShelfSmall. Source-level feasibility: pass-through shelf supports Touch item access from the doorway; live fetching is still required before accepting the design. Roof only the store. HE/incendiary shells explode when damaged (Core base radius 2.9, expansion 0.4 per stack count); a roofed enclosure is not blastproof. Prefer small divided stacks away from operators, beds, fuel and sole retreats. Shared six-hour shell/barrel ledger must not be copied per gun.

### Defense budget

Current ordinary population distribution remains 4–12 with expected mean seven. Import allowance is the physical profile allowance multiplied by ordinary wealth, separate from resident equipment. Turret construction, initial fuel/reserve and added cover are charged against that same import limit; connected dispatchable headroom and exclusive guard/support staffing are independent constraints. Maximum groups currently min(4, exposed faces). Heavy selection orders uranium then autocannon then mini unconditionally, so autocannons can be shadowed. Core Turret_Autocannon requires HeavyTurrets (IndustrialLate in the current stage model); uranium requires SniperTurret. P4E.4 must compare a named ordinary-only increment and useful gun mix, preserving native quest budgets and roster.

## Evidence ledger

- P4E.0 source audit complete. One-cell store fetching, thermal settling, seated use and multiple-gun behavior are not yet live-verified.
- P4E.1–P4E.5 pending. Do not infer completion from this audit.
