# PICO Scene Capture 房间感知生成 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用 PICO Floor 场景锚点生成金币和道具，并在 Scene Capture 不可用时回退到固定安全区域。

**Architecture:** 用 `IPicoFloorAreaProvider` 隔离 PICO API。生成与多边形逻辑留在 `Trailblazer.Pico`，SDK provider 放在新的 `Trailblazer.Pico.Scene` 程序集，从而在主工程 PlayMode 测试中使用 fake provider 验证全部非设备行为。

**Tech Stack:** Unity 2022.3.62f3、C# async/await、PICO Unity SDK 6.1.1 Scene Capture、Unity Test Framework。

**Spec:** `docs/superpowers/specs/2026-10-31-pico-scene-capture-spawn-design.md`

## Global Constraints

- 使用 `ByteDance.PICO.XR`，不使用 Meta API。
- 保留 `PicoSpawnPositions` 作为回退，不改变现有 Prefab、生成数量、间隔或玩法。
- 只开启 `sceneCapture`，不额外开启 Spatial Mesh/Anchor。
- 真机验证按用户要求跳过。

## Review Focus

- 多边形退化或采样失败：Task 1 测试退化输入并回退。
- provider 异常/取消：Task 1 验证回退，Task 2 保证 SDK 错误转为空结果或异常由生成器捕获。
- 重复 StartSpawn 导致重复生成：Task 1 用未完成的 Task 验证并发拦截。
- 两个组件同时 Start 导致双份生成：Task 2 场景迁移时关闭回退组件的 SpawnOnStart。

---

### Task 1: 多边形与可测试生成逻辑

**Files:**
- Create: `Assets/Scripts/Pico/PicoFloorArea.cs`
- Create: `Assets/Scripts/Pico/IPicoFloorAreaProvider.cs`
- Create: `Assets/Scripts/Pico/PicoSceneSpawnPositions.cs`
- Modify: `Assets/Tests/PlayMode/PicoSpawnPositionsTests.cs` 或新增同目录测试文件

**Interfaces:**
- `PicoFloorArea(Vector3[] vertices)`；`TryGetRandomPoint(out Vector3 point) : bool`。
- `IPicoFloorAreaProvider.GetFloorAreasAsync() : Task<IReadOnlyList<PicoFloorArea>>`。
- `PicoSceneSpawnPositions.StartSpawn() : async void`；`fallback : PicoSpawnPositions`；`surfaceClearance : float`。

- [ ] **Step 1: 写失败测试**

覆盖：矩形内采样、少于 3 顶点返回 false、使用 provider 地面生成、provider 异常时固定区域回退、并发两个 StartSpawn 只生成一次。

- [ ] **Step 2: 在主工程运行测试并确认失败**

用 Unity Test Runner API 运行新增 PlayMode 测试，预期因类型不存在而编译失败。

- [ ] **Step 3: 实现最小代码**

多边形用 XZ 包围盒 rejection sampling + ray casting，最多 100 次；生成器先计算全部点，再实例化，异常或无可用点时只走回退，并用 `_isSpawning` 防重入。

- [ ] **Step 4: 重跑测试并确认通过**

新增测试全部 PASS，既有 3 个测试也保持 PASS。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Pico Assets/Tests/PlayMode
git commit -m "feat: add testable floor-aware spawning"
```

### Task 2: 接入 PICO Scene Capture 与场景

**Files:**
- Create: `Assets/Scripts/Pico/Scene/Trailblazer.Pico.Scene.asmdef`
- Create: `Assets/Scripts/Pico/Scene/PicoSceneCaptureFloorAreaProvider.cs`
- Modify: `Assets/Scripts/SpawnPowerUps.cs`
- Modify: `Assets/Scenes/BikeScene.unity`
- Modify: `Assets/Resources/PXR_ProjectSetting.asset`

**Interfaces:**
- `PicoSceneCaptureFloorAreaProvider` 实现 Task 1 的 provider；`SpawnPowerUps` 改用 `PicoSceneSpawnPositions`。

- [ ] **Step 1: 实现 SDK provider**

启动 `PxrSenseDataProviderType.SceneCapture`，查询 `PxrSemanticLabel.Floor`；无数据时只请求一次 `StartSceneCaptureAsync` 并重查。将 Polygon 的 `(x,y)` 按 `(x,0,y)` 经锚点位姿转换到世界空间；OnDestroy 停止已启动 provider。

- [ ] **Step 2: 迁移两个生成对象**

保留原 `PicoSpawnPositions` 回退并设其 `SpawnOnStart=false`；添加 provider 和 `PicoSceneSpawnPositions`，复制 Prefab、数量和自动启动设置。`SpawnPowerUps` 改用新组件。开启 `sceneCapture: 1`。

- [ ] **Step 3: 验证并提交**

主工程编译无错误，静态检查两个新组件与回退均存在，重建 APK 要求 0 errors 和单一 OpenXR loader。

```bash
git add Assets/Scripts/Pico/Scene Assets/Scripts/SpawnPowerUps.cs Assets/Scenes/BikeScene.unity Assets/Resources/PXR_ProjectSetting.asset
git commit -m "feat: use PICO Scene Capture for spawning"
```
