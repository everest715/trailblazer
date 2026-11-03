# PowerUp Active Cap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `SpawnPowerUps.maxCount` a scene-configurable active-instance cap that pauses spawning at the cap and resumes one instance per configured interval after spawned objects are destroyed.

**Architecture:** `PicoSceneSpawnPositions` reports each successful `Instantiate` through an event. `SpawnPowerUps` owns the active-instance list and scheduling state, removing Unity-destroyed instances and pausing/resuming its existing `InvokeRepeating` timer.

**Tech Stack:** Unity 2022.3.62f3, C#, NUnit Unity PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-11-04-power-up-active-cap-design.md`

## Global Constraints

- `maxCount` is public with default `8`; `BikeScene` stores `maxCount: 8` and retains `spawnInterval: 2`.
- Only successful instantiation changes the active count.
- Resume uses the configured interval and spawns one instance per interval; never immediately refill the whole cap.
- Do not change collision, scoring, prefab, or spawn-position logic.

## Review Focus

- Destroyed Unity objects: expect removal from active tracking and interval-based resume; pin with `SpawnNow_DestroyedPowerUpResumesAfterInterval`.
- Failed/null prefab spawn: expect no active-count increase; pin with `SpawnNow_NullPrefabDoesNotIncreaseActiveCount`.
- Repeated `SpawnNow`: expect one timer rather than accelerated spawning; pin with `SpawnNow_RepeatedCallsDoNotDuplicateSpawning`.
- Cap reached while timer is active: expect no further instances; pin with `SpawnNow_StopsAtSceneConfiguredActiveCap`.

---

### Task 1: Track active PowerUps and regulate the existing timer

**Files:**
- Move to: `Assets/Scripts/Pico/SpawnPowerUps.cs` (from `Assets/Scripts/SpawnPowerUps.cs`, preserving `.meta` GUID for scene references and test assembly access)
- Modify: `Assets/Scripts/Pico/PicoSceneSpawnPositions.cs`
- Modify: `Assets/Scenes/BikeScene.unity`
- Create: `Assets/Tests/PlayMode/SpawnPowerUpsTests.cs`

**Interfaces:**
- Consumes: existing `PicoSceneSpawnPositions.StartSpawn()` and scene-configured `spawnInterval`.
- Produces: `PicoSceneSpawnPositions.Spawned` (`event Action<GameObject>`), `SpawnPowerUps.maxCount` (`public int`), and `SpawnPowerUps.ActiveCount` (`public int` read-only).

- [ ] **Step 1: Write failing PlayMode tests**

Create `SpawnPowerUpsTests` with a real `PicoSceneSpawnPositions`, no floor provider (fixed-bounds path), and a `SpawnProbe` prefab. Each test sets `maxCount` and a short `spawnInterval`, then observes real instances:

- `SpawnNow_StopsAtSceneConfiguredActiveCap`: set cap 2, start spawning, wait for at least 4 intervals, assert `ActiveCount == 2`, wait again, assert only 2 instances exist.
- `SpawnNow_DestroyedPowerUpResumesAfterInterval`: reach cap 2, destroy one instance, assert count drops; before the next interval assert no replacement; after the next interval assert exactly one replacement and count returns to 2.
- `SpawnNow_NullPrefabDoesNotIncreaseActiveCount`: use `powerUps = new GameObject[] { null }`, wait past an interval, assert zero count and zero instances.
- `SpawnNow_RepeatedCallsDoNotDuplicateSpawning`: call `SpawnNow()` twice and verify instances appear at the single configured cadence rather than doubled.

- [ ] **Step 2: Run the new tests and verify RED**

Run Unity in batch mode without `-quit`:

```bash
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "SpawnPowerUpsTests" -testResults ".superpowers/power-up-active-cap-red.xml" -logFile ".superpowers/power-up-active-cap-red.log"
```

Expected: failure because `maxCount` is not configurable, `ActiveCount` does not exist, and current spawning stops permanently after 8 calls instead of tracking active objects.

- [ ] **Step 3: Emit successful spawn notifications**

Add `PicoSceneSpawnPositions.Spawned` as `event Action<GameObject>`. Route both SceneCapture and fixed-bounds `Instantiate` calls through one private helper that instantiates the current prefab at the supplied world point and raises `Spawned` with the resulting instance. Do not raise the event for null-prefab or rejected concurrent calls.

- [ ] **Step 4: Implement active-cap scheduling in `SpawnPowerUps`**

Make `maxCount` public with default 8 and remove the `Start()` reset. Subscribe to `PicoSceneSpawnPositions.Spawned` in `Awake`, unsubscribe and cancel the timer in `OnDestroy`, and retain spawned instances in a list.

`ActiveCount` removes destroyed Unity objects before returning the list count. `SpawnNow()` starts one repeating timer only when below the cap. Each timer tick removes destroyed objects, pauses at the cap, otherwise selects a random prefab, sets the scene spawner amount to 1, and calls `StartSpawn()`. `Update()` detects destroyed objects and resumes one timer when below the cap; the resumed call retains `spawnInterval` as its initial delay.

- [ ] **Step 5: Store the scene cap and run focused tests**

Add `maxCount: 8` beneath `spawnInterval: 2` in `BikeScene.unity`. Run the Step 2 command with GREEN output paths. Expected: all `SpawnPowerUpsTests` pass and no other behavior is changed.

- [ ] **Step 6: Run regression verification**

Run all PlayMode tests and inspect the NUnit XML summary. Expected: all spawner, tracker, and DebugInfoPanel tests pass. If the two previously documented unrelated scale tests still fail, report them by name and do not alter them in this task.
