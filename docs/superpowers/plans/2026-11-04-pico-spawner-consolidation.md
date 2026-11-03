# PICO Spawner Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge fixed-bounds spawning into `PicoSceneSpawnPositions`, leave PowerUp scheduling in `SpawnPowerUps`, and remove the separate `PicoSpawnPositions` component without breaking scene references.

**Architecture:** `SpawnPowerUps` remains the scheduler and prefab selector. `PicoSceneSpawnPositions` becomes the complete spawn-position service: SceneCapture first, fixed `spawnBounds` on every failure path, with existing diagnostics and concurrency protection.

**Tech Stack:** Unity 2022.3.62f3, C#, PICO SceneCapture, NUnit Unity PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-11-04-pico-spawner-consolidation-design.md`

## Global Constraints

- Preserve `SpawnPowerUps` prefab selection, scene-configured `spawnInterval`, and maximum of 8 scheduled spawns.
- Preserve `PicoSceneSpawnPositions.StartSpawn()` as the public spawn entry point.
- Every unavailable or failed SceneCapture path must spawn through fixed `spawnBounds`.
- Reuse cached SceneCapture height for fallback when available; otherwise use the configured fixed-bounds height.
- Do not change input handling or unrelated game systems.

## Review Focus

- No `IPicoFloorAreaProvider`: expect fixed-bounds spawning; pin with `StartSpawn_NoProviderUsesFixedBounds` in Task 1.
- Empty or unusable floor areas: expect fixed-bounds spawning rather than a silent return; pin with `StartSpawn_NoFloorAreasUsesFixedBounds` in Task 1.
- Provider exception after a successful spawn: expect fallback at the cached height; pin with `StartSpawn_ProviderFailureWithCachedHeightReusesHeight` in Task 1.
- Null prefab: expect no exception and no instance; pin with `StartSpawn_NullPrefabDoesNotThrowOrSpawn` in Task 1.
- Concurrent async calls: expect only one provider query and one batch; retain `StartSpawn_ConcurrentCallsSpawnOnce` in Task 1.

---

### Task 1: Consolidate spawning behavior behind `PicoSceneSpawnPositions`

**Files:**
- Modify: `Assets/Scripts/Pico/PicoSceneSpawnPositions.cs`
- Modify: `Assets/Tests/PlayMode/PicoSceneSpawnPositionsTests.cs`
- Modify: `Assets/Tests/PlayMode/DebugInfoPanelTests.cs`
- Delete after its cases are migrated: `Assets/Tests/PlayMode/PicoSpawnPositionsTests.cs`

**Interfaces:**
- Consumes: existing `IPicoFloorAreaProvider.GetFloorAreasAsync()` and `PicoFloorArea.TryGetRandomPoint(out Vector3 point)`.
- Produces: `PicoSceneSpawnPositions.spawnBounds` (`Bounds`) and private fixed-bounds fallback used by `StartSpawn()`; removes public `fallback` and unused `playAreaBounds` fields.

- [ ] **Step 1: Migrate fixed-bounds tests and add failing fallback tests**

In `PicoSceneSpawnPositionsTests`, configure `spawnBounds` directly on the scene spawner instead of creating `_fallback`. Migrate the three cases from `PicoSpawnPositionsTests` without changing their assertions:

- `StartSpawn_SpawnsRequestedAmountInsideFixedBounds`: 3 instances inside configured local bounds after transforming the spawner to `(5, 0, -2)`.
- `StartSpawn_NullPrefabDoesNotThrowOrSpawn`.
- `Start_SpawnOnStartSpawnsOnce`: 2 instances when no provider exists.

Add:

- `StartSpawn_NoProviderUsesFixedBounds`: one instance inside `spawnBounds`.
- `StartSpawn_NoFloorAreasUsesFixedBounds`: provider returns an empty area list; one instance inside `spawnBounds`.
- `StartSpawn_ProviderFailureWithCachedHeightReusesHeight`: first spawn succeeds at `0.3f`; second spawn throws and its instance also has `y = 0.3f`.

Update `StartSpawn_ProviderFailureUsesFallback` to inspect `spawn.spawnBounds`. Update `DebugInfoPanelTests.Refresh_ShowsGroundDetectionMethodAndHeight` so it no longer creates or assigns `PicoSpawnPositions`.

- [ ] **Step 2: Run the PlayMode tests and verify the new cases fail**

Run: Unity **Test Runner → PlayMode → Run All**.

Expected: new no-provider, empty-area, and cached-height tests fail because those paths currently return early or depend on the separate fallback component; existing SceneCapture success and concurrency tests remain valid.

- [ ] **Step 3: Implement fixed-bounds fallback in `PicoSceneSpawnPositions`**

Add public `Bounds spawnBounds` and remove `fallback` and `playAreaBounds`. Keep SceneCapture success logic and `_isSpawning` guard intact. Route no-provider, empty/insufficient-points, and exception paths through one private fixed-bounds spawn helper with signature `void SpawnInFixedBounds(float? fixedHeight)`.

The helper must use `transform.TransformPoint`, `SpawnObject.transform.rotation`, `SpawnAmount`, and `fixedHeight ?? Random.Range(spawnBounds.min.y, spawnBounds.max.y)`. Set fallback diagnostics consistently and preserve the existing warning on provider exceptions. Do not change `SpawnPowerUps`.

- [ ] **Step 4: Delete the migrated test file and run all PlayMode tests**

Delete `Assets/Tests/PlayMode/PicoSpawnPositionsTests.cs` and its `.meta` only after all three cases exist in `PicoSceneSpawnPositionsTests`.

Run: Unity **Test Runner → PlayMode → Run All**.

Expected: all PlayMode tests pass, including fixed-bounds, SceneCapture success, all fallback paths, cached height, null prefab, SpawnOnStart, concurrency, and DebugInfoPanel tests.

### Task 2: Migrate scene serialization and remove the old component

**Files:**
- Modify: `Assets/Scenes/BikeScene.unity`
- Delete: `Assets/Scripts/Pico/PicoSpawnPositions.cs`
- Delete: `Assets/Scripts/Pico/PicoSpawnPositions.cs.meta`

**Interfaces:**
- Consumes: `PicoSceneSpawnPositions.spawnBounds` from Task 1.
- Produces: scenes with only `SpawnPowerUps` and/or `PicoSceneSpawnPositions`; no serialized references to `PicoSpawnPositions` GUID `63ce693c41f938a4895619152ba047b1`.

- [ ] **Step 1: Migrate both `BikeScene.unity` spawner configurations**

For fallback components `31291501` and `589298192`, copy their existing `spawnBounds` values onto scene spawners `31291502` and `589298193` respectively:

```yaml
spawnBounds:
  m_Center: {x: 0, y: 0.05, z: 1.5}
  m_Extent: {x: 0.75, y: 0.05, z: 0.75}
```

Remove each scene spawner's `fallback` and `playAreaBounds` entries, remove the two old `PicoSpawnPositions` MonoBehaviour blocks, and remove component-list references to `31291501` and `589298192`. Preserve each scene spawner's existing `SpawnOnStart`, `SpawnObject`, `SpawnAmount`, and `surfaceClearance`; preserve `SpawnPowerUps.spawnInterval: 2`.

- [ ] **Step 2: Remove the obsolete script and verify no references remain**

Delete `PicoSpawnPositions.cs` and `.meta`, then run:

```bash
rg -n "\bPicoSpawnPositions\b|63ce693c41f938a4895619152ba047b1|playAreaBounds|fallback:" Assets/Scripts Assets/Tests Assets/Scenes/BikeScene.unity
```

Expected: no matches; the word boundary prevents `PicoSceneSpawnPositions` from matching the deleted class name.

- [ ] **Step 3: Run full verification**

Run: Unity **Test Runner → PlayMode → Run All**, then open `BikeScene.unity` and enter Play Mode.

Expected: zero test failures and no Missing Script or console exceptions. SceneCapture success spawns on captured surfaces; with no provider or failed capture, the same configured objects spawn in fixed bounds. PowerUps still spawn every 2 seconds in this scene and stop after 8 scheduled batches.
