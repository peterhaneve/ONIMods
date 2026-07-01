# Candidate 0008: Enable Fast Track mesh-renderer subsystem on Aquatic/Unity 6

- Status: PARKED (MOOT — MeshRendererOptions already = All/0 in user config AND confirmed working in log; original audit misread enum 0=All not None; frame times already include this)
- Target: `MeshRendererOptions` (enum `FastTrackOptions.MeshRendererSettings`) — gates a cluster of visual/conduit patches (4 transpilers + several prefixes/postfixes)
- Risk class: Transpiler/IL (4) + Prefix/Postfix (tiles, ground, terrain setup)
- Gating flag: `MeshRendererOptions` (`None` / `AllButTiles` / `All`)
- Collision with Peter's rewrites: n/a (this IS Peter's code); tile path already carries fork `ponytail:` Aquatic fixes

## What and why
`MeshRendererOptions` replaces per-frame `Graphics.DrawMesh` calls for terrain background, conduit flow, falling water, the priority overlay, ground, and (optionally) tiles with persistent Unity `MeshRenderer` GameObjects. Big render-thread win on busy colonies. The concern: four of these patches are IL **transpilers** on hot render methods, and a stale anchor on Aquatic could in principle emit invalid IL and hard-crash. This candidate verifies each transpiler's anchors against the current decompile before the user enables the subsystem.

### Enum values and what each gates
- `All` — value 0, and the **constructor default** (`FastTrackOptions.cs:295`). Activates the general renderers PLUS the tile mesh renderer (`TileMeshPatches`, applied manually via `FastTrackCompat.CheckTileCompat`, auto-disabled if the **True Tiles** mod is active).
- `AllButTiles` — value 1. Activates terrain / conduit / falling water / prioritizable / ground / kanim renderers. No tiles.
- `None` — value 2. Everything off.

Branch logic: every general patch has `Prepare() => MeshRendererOptions != None`. Tiles are extra-gated by `== All` (FastTrackMod.cs:105-107, 167-168). Cleanup mirrors this in `OnEndGame` (162-168).

Note a discrepancy with the brief: the brief says it "ships None by default," but the constructor sets `All`. Either the user's saved config is `None`, or default is actually `All`. Worth confirming which the user is running before testing.

## Per-transpiler anchor verification (the crash question)

All four transpilers share one defensive pattern:
1. `GetMethodSafe(...)` reflection lookup of the target Unity method.
2. If found → `PPatchTools.ReplaceMethodCallSafe` / `RemoveMethodCall` (a **stream-wide find-and-replace**, not `CodeMatcher`/`MatchForward` position insertion).
3. If the lookup returns null → `PUtil.LogWarning("Unable to patch ...")` and return the **original** instructions unchanged.

Key fact from `PLibCore/PPatchTools.cs:223-267` (`DoReplaceMethodCalls`): non-matching instructions are `yield return`ed verbatim; if nothing matched it only logs "No method calls replaced" (DEBUG only) and emits well-formed IL. **A missing anchor cannot produce an `InvalidProgramException` here.** Worst case is a no-op: the DrawMesh isn't removed, so both the new MeshRenderer and the old DrawMesh draw = double-render visual glitch, not a crash. This is categorically different from a `CodeMatcher.ThrowIfInvalid` transpiler.

| Transpiler | Target (decompile) | Anchor it rewrites | Present on Aquatic? | Verdict |
|---|---|---|---|---|
| `FallingWater_Render_Patch` | `FallingWater.Render` (FallingWater.cs:656) | remove 8-arg `Graphics.DrawMesh(Mesh,V3,Quat,Mat,int,Camera,int,MPB)` | YES — line 717 | SAFE |
| `PrioritizableRenderer_RenderEveryTick_Patch` | `PrioritizableRenderer.RenderEveryTick` (:70) | remove 10-arg `DrawMesh(...,bool,bool)` + `Mesh.RecalculateBounds()`; prepend own `SetInstanceVisibility` | YES — DrawMesh line 140, RecalculateBounds line 139 | SAFE |
| `TerrainBG_LateUpdate_Patch` | `TerrainBG.LateUpdate` (:256) | remove 5-arg `DrawMesh(Mesh,V3,Quat,Mat,int)` + wrap `Material.SetTexture(string,Texture)` | YES — SetTexture line 273, 5-arg DrawMesh lines 274/324/325 | SAFE (crash) / see visual caveat |
| `End_Patch` (ConduitFlowMesh.End) | `ConduitFlowVisualizer.ConduitFlowMesh.End` (:91) | replace 5-arg `DrawMesh` with `PostUpdate` | YES — line 98 | SAFE |

The inserted leading `call SetInstanceVisibility` in the Prioritizable transpiler is a static void no-arg call to FastTrack's own method — valid regardless of the rest of the body.

### TerrainBG visual caveat (not a crash)
`TerrainBG_LateUpdate_Patch` blanket-removes **every** 5-arg `DrawMesh`. On the current build that also strips the draws at lines 297 (`largeImpactorDefeatedPlane`) and 301 (`northernLightsPlane` / `northernLightMaterial_ceres`), which the mesh-renderer path does **not** recreate (it only builds renderers for gas back/front + stars). Result: on affected asteroids the Ceres northern lights and the large-impactor-defeated overlay likely render blank when this is enabled. The worldPlane (line 320, 8-arg overload) is intentionally left alone. Flag as MEDIUM visual regression, worth an explicit visual check.

## Non-transpiler pieces
- **Tiles (`All` only) — `TileMeshPatches`**: all **prefixes returning false** (skip vanilla), no IL emission. The fork already applied Aquatic compat here (`ponytail:` comments, TileMeshPatches.cs:31-32,49): `BlockTileRenderer.AddBlock` became a 6-param method on Aquatic and the prefix + `GetMethodSafe` lookup were retargeted to it. Decompile confirms both the 5-param shim (BlockTileRenderer.cs:749) and the real 6-param impl (:754), plus all other prefix targets (HighlightCell/RemoveBlock/Rebuild/SelectCell/SetInvalidPlaceCell/LateUpdate). One residual risk: if any `GetMethodSafe` returned null, `harmony.Patch(null,...)` throws at load — but the 6-param target is confirmed present.
- **Ground (`GroundMeshRenderer`)**: `RenderData.Render` prefix returns false; ClearMesh prefix + ctor postfix. Not a transpiler. Targets confirmed. SAFE.
- **Terrain setup (`TerrainMeshRenderer` ctor)**: reads `TerrainBG.gasPlane/starsPlane/noiseVolume/gasMaterial/layer` — all confirmed present in decompile. SAFE.
- **Conduit setup (`ConduitMeshVisualizer`)**: reads `ConduitFlowMesh.mesh/material` — confirmed. SAFE.
- **`CreateMeshRenderer`** (ExtensionMethods.cs:81): stable Unity APIs only (MeshRenderer/MeshFilter/LightProbeUsage/etc.). No texture-format work. `grep unsafe` over VisualPatches/ + ConduitPatches/ = **zero** hits, so the "unsafe texture against changed formats" concern does not apply to this subsystem.

## Compat gating
`CheckTileCompat` (FastTrackCompat.cs:210) disables tile mesh renderers when True Tiles is active (`== All` only). No other cross-option coupling except `FullScreenDialogPatches`, which coordinates the ground renderer with the `RenderTicks` option. Nothing else conditionally disables the mesh subsystem.

## Go / No-Go
- **`AllButTiles`: GO (no crash risk).** All four transpilers verified against the current decompile; even a hypothetical anchor miss degrades to a logged warning + no-op, never bad IL. Watch for the TerrainBG visual regression above.
- **`All`: GO (no crash risk).** Tile path is prefix-only and already Aquatic-fixed; True Tiles auto-fallback in place. Larger visual surface (tiles), so validate after `AllButTiles`.
- **No transpiler must be fixed before enabling.** The subsystem is defensively written; the real exposure is visual (double-render on a missed anchor, or the TerrainBG sky-content disappearance), not a hard crash.

## Recommended test plan
1. Set `MeshRendererOptions = AllButTiles`. Load a colony. In Player.log watch for `Unable to patch ...`, `No method calls replaced ...` (DEBUG build emits these — an anchor miss diagnostic), and any `InvalidProgramException` / patch-time exception at load (none expected). Visually verify: conduit flow animation, falling water, priority overlay, terrain gas/stars background. Specifically check a Ceres asteroid's northern lights and any large-impactor-defeat overlay for disappearance.
2. If clean, set `= All`. Verify solid-tile rendering (ore/metal/insulated tiles), tile decor, tile highlight/selection, and the red invalid-placement overlay. If True Tiles is installed, expect the log line `Disabling tile mesh renderers: True Tiles active` and vanilla tile rendering.
3. Since `bin/Debug` is the active build, the "No method calls replaced" warnings will actually fire on any miss — use them as the primary signal that a specific renderer silently no-oped.

## Static gates (Stage 3)
- Compiles: y (fork builds clean per CLAUDE.md; tile path already carries Aquatic fixes)
- Unit test: na (game-coupled)
- IL verify: pass (by construction) — all four transpilers use `PPatchTools.ReplaceMethodCallSafe`/`RemoveMethodCall`, which cannot emit invalid IL on a missing anchor; all four anchors additionally confirmed present in the current decompile
- Thread-safety check: renderers created on main thread in OnSpawn/ctor/OnPrefabInit; na
- Save-compat: na (pure render path, no serialized state)

## Review (Stage 4)
Pending independent review -> (recommend a fresh agent verify the TerrainBG sky-content regression claim in-game).

## Measurement (Stage 5)
Pending A/B once enabled.

## Outcome
DESIGN — recommend enabling `AllButTiles` first under observation, then `All`. No pre-fix required for crash safety.
