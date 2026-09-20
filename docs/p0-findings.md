# P0 technical findings

Updated: 2026-09-20

## Result

P0 is **complete**. The prototype builds and its required behaviors were observed in live RimWorld 1.6.4871 rev597 runs using isolated data folders. P0 establishes integration and recovery contracts; it does not implement the P1 planner or claim the full mod is playable.

The guarded generator is off by default. The prototype adds no traps or IEDs, no recurring free production, no player-faction conversion, and no global enemy-pawn behavior patch.

## Environment and verification

| Item | Tested value |
|---|---|
| Baseline commit | `0f121144b212c283298281d3fcefce2b9e93ea84` |
| Compile references | `Krafs.Rimworld.Ref` 1.6.4871; `Lib.Harmony` 2.4.1 |
| Build environment | `mcr.microsoft.com/dotnet/sdk:8.0` with persistent `rimworld-nuget`, per `.claude/skills/build-mod` |
| Executable runtime | RimWorld 1.6.4871 rev597 |
| Installed metadata | `Version.txt` says rev595; accepted as stale text because the tested executable and pinned API agree on 1.6.4871 |
| Optional profile | Geological Landforms 1.7.13.1 (44 landforms) and Map Preview |
| Isolated profiles | `/tmp/realistic-bases-p0-core`, `/tmp/realistic-bases-p0-gl` |
| Disposable save | `/tmp/realistic-bases-p0-core/Saves/RealisticBases-P0-Disposable.rws` during the completed run |
| Final build | Release, 0 warnings, 0 errors |

The initial 2.3.3 Harmony compile reference produced a live `TypeLoadException` against installed Harmony 2.4.1. Pinning 2.4.1 resolved it; post-fix Core and optional-mod runs loaded `BOOT` without a Realistic Bases XML, assembly, or patch error.

## Generation hook and terrain order

The accepted integration point is one postfix on `Settlement.MapGeneratorDef`. It substitutes `RB_P0_Base_Faction` only when the setting is enabled and the object is exactly an ordinary settlement with a non-player humanlike faction, no explicit generator, and vanilla result `Base_Faction`.

The generator inherits `MapCommonBase`. Custom settlement and power steps run at orders 400 and 401, after terrain and roads. The settlement step selects a roofless, standable, buildable site with a pass-doors route to an edge. It computes terrain/roof checksums before and immediately after its own mutations; a separate final audit catches later vanilla/mod changes.

Observed Core events included `GEN_SELECT`, `RB_P0_Settlement order=400`, `RB_P0_SettlementPower order=401`, `terrainPreserved=True`, `noTrapsOrIEDs=True`, and `edgeReachablePassDoors=True`. Some maps had a different final checksum because later generation steps or mutators changed terrain; the settlement-time checksum remained equal, proving the owned step did not overwrite terrain.

In the optional profile, Geological Landforms generated a `GL_Cliff` map. The audit recorded `GL=True`, `MapPreview=True`, settlement/power at 400/401, `prohibitedDefenses=0`, and a playable pass-doors edge route. Map Preview source limits preview generation to steps below order 230, so the physical settlement is intentionally absent from previews; full generation coexistence was observed live.

## Hostile Lord and work adapter

The custom Lord assigns one pawn a small-radius hold duty and the other a settlement-work duty. After a real blunt injury, the holder stayed within 4.24 cells of its post and retained duty `RB_P0_HoldPost`.

The work probe delegates to actual vanilla `WorkGiver_Refuel`, `WorkGiver_DoBill`, and `WorkGiver_GrowerSow` instances. Facilities remain hostile-owned and inputs are finite: 60 wood and 20 rice. Observed stages were:

1. generator refueled from the finite wood stack;
2. fueled stove refueled;
3. one simple meal completed from the finite rice;
4. rice sowed in the hostile-owned hydroponics basin.

`WORK_PROBE_SPAWNED` reported `noPlayerFactionChange=true`. The game was saved during stage 1, reloaded with the same worker pawn ID and generator fuel state, then continued through stages 2–4. Completion, not job assignment, is the adapter acceptance boundary.

## Lifecycle and defeat

Two serialized scopes are accepted:

- `P0WorldState`, keyed by settlement world-object ID, stores generation count, wood remaining, marker-wall destruction, defeat, original faction, and diagnostic pawn IDs;
- `P0MapState` stores loaded-map cells/checksums and reconciles resource/building changes into world state on removal.

The deterministic unload/re-entry test observed:

- first removal persisted wood 30 and intact marker wall;
- the test delta set wood to 0 and marker wall destroyed;
- re-entry logged `revisit=True restoredWood=0 restoredMarkerWallDestroyed=True`;
- the second removal again persisted wood 0 and destroyed marker state.

Defenders deliberately regenerate on re-entry (`rosterReused=False`). Individual identity, injuries, gear, and casualties are not persisted in P0; this is an accepted simplification because identity continuity is not required for the early settlement encounter. A literal caravan retreat was replaced with deterministic map removal/reopen, which directly exercises the persistence boundary without expanding the spike into caravan behavior.

The defeat action killed the generated defenders and invoked vanilla settlement defeat handling. The parent became `DestroyedSettlement`, followed by `SETTLEMENT_OWNERSHIP_ENDED`; controller activity stopped.

## Recovery and playable arrival

Two one-shot failures were tested in both Core and the optional-mod profile:

- **Pre-spawn rejection:** `PLAN_REJECT mutationCount=0`, followed by the compact 5x5 fallback.
- **Partial-spawn failure:** one owned wall and one terrain mutation were deliberately made; recovery logged `ownedThingsRemoved=1 terrainCellsRestored=1 checksumRestored=True`, followed by the compact fallback.

Every fallback reported settlement-time `terrainPreserved=True`, `noTrapsOrIEDs=True`, and `edgeReachablePassDoors=True`. The playable action moved an existing player colonist—never an injected pawn—to a valid edge cell. Final optional-profile runs reported `targetReachablePassDoors=True currentMap=True` for normal, rejection, and partial-failure maps. Hostile doors remain hostile and can require ordinary breaching; “pass doors” proves topology and arrival viability rather than granting access.

## Reproduction protocol

Build using the repository skill's Docker command, then launch with `-savedatafolder=/tmp/realistic-bases-p0-core -quicktest` or the GL profile. The developer category is **Realistic Bases P0**.

1. Run **Generate playable hostile P0 settlement**, then **Log current map generation audit**.
2. Run **Spawn hostile garrison/work probe**, let all four stages complete, and run **Damage P0 hold pawn**.
3. Save/reload with the disposable-save actions during active work and observe continued progress.
4. Generate a non-playable isolated settlement, allow removal, run **Mark last isolated P0 settlement depleted/damaged**, then **Reopen last isolated P0 settlement**.
5. Run **Defeat current P0 settlement** on an active P0 map.
6. Arm each failure action in turn and generate a playable settlement; verify rollback, fallback, arrival, and prohibited-defense logs.
7. Repeat normal generation and both failures in the GL profile; confirm `GL=True MapPreview=True` and the ordered step list.

The `/tmp` profiles are disposable and may need recreation after a restart. No Steam process is required when the already-installed mods are exposed locally.

## P0 handoff to P1

P1 may rely on the guarded generator, order-400 placement, final audit, map-scoped vanilla work adapter, coarse world/map persistence, and rollback-before-fallback pattern. It must retain the no-trap/no-IED invariant and must not imply defender identity persistence. P1 should replace the fixed 9x9/5x5 diagnostic layouts with the planned bounded terrain-aware planner and validation harness.
