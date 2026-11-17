# 虚拟桌面回退 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scene Capture 无真实 Table 时统一返回可配置的虚拟桌面，并让 Gate、围墙、敌人和道具使用其范围与当前高度。

**Architecture:** `PicoFloorArea` 提供无原生 SDK 依赖的矩形构造；Provider 在查询终点和锚点更新处集中应用回退并维护诊断。道具生成器读取 Provider 的来源，并在区域高度变化时更新缓存高度。

**Tech Stack:** Unity 2022.3.62f3、C#、PICO XR、NUnit PlayMode Tests。

**Spec:** `docs/superpowers/specs/2026-11-18-virtual-table-fallback-design.md`

## Global Constraints

- 保持 Unity 2022.3、PICO OS 6、PICO XR，不引入新依赖。
- 仅 `surfaceLabel == Table` 且 `useVirtualTableFallback = true` 时使用虚拟结果；Floor 与禁用回退时保持原行为。
- 默认中心 `(0, 0, 1.5)`、尺寸 `(1.5, 1.5)`、高度 0、Y 偏航 0；不跟随头显。
- 真实桌面优先，后续真实锚点更新替换虚拟结果。
- 不改变正常真实桌面标定、围墙排列、物体尺寸、Floor 行为或得分逻辑。
- 保留现有 `BikeScene.unity` 与 Coin Prefab 未提交修改；本计划依靠新字段默认值，无需改场景。
- Editor 测试不替代 PICO 真机 SDK 失败与恢复验证。

## Review Focus

- 首次查询为空但尚未执行 Scene Capture：必须仍然先请求一次识别，不能直接返回虚拟桌面；Task 1 的 `FirstEmptyQuery_RequestsCaptureOnlyOnce` 覆盖。
- 回退关闭或 Floor 查询失败：继续抛原异常，不能因新增 catch 而吞掉；Task 1 的 Floor/禁用测试与实现约束覆盖。
- 虚拟尺寸为 0、负数、NaN 或 Infinity：返回空区域并 Warning，不能产生非法多边形；Task 1 的无效参数测试覆盖。
- 真实桌面替换虚拟桌面后道具仍使用旧缓存高度：成功查询必须使用当前区域高度；Task 2 的高度变化测试覆盖。
- 重复空锚点事件：每次最多发布一个虚拟区域，不能把虚拟矩形追加成多张桌子；Task 1 的单区域诊断测试覆盖。

---

### Task 1: 虚拟矩形与 Provider 统一回退

**Files:**
- Modify: `Assets/Scripts/Pico/PicoFloorArea.cs`
- Modify: `Assets/Scripts/Pico/Scene/PicoSceneCaptureFloorAreaProvider.cs`
- Modify: `Assets/Tests/PlayMode/PicoFloorAreaTests.cs`
- Modify: `Assets/Tests/PlayMode/Trailblazer.Pico.PlayModeTests.asmdef`（增加 `Trailblazer.Pico.Scene`、`ByteDance.PICO.XR`）
- Test: `Assets/Tests/PlayMode/VirtualTableFallbackTests.cs` 及 `.meta`

**Interfaces:**
- Produces: `public static bool PicoFloorArea.TryCreateRectangle(Vector3 center, Vector2 size, float yawDegrees, out PicoFloorArea area)`。
- Produces Provider 字段：`useVirtualTableFallback = true`、`virtualTableCenter = (0,0,1.5)`、`virtualTableSize = (1.5,1.5)`、`virtualTableYaw = 0`。
- Produces 私有入口 `List<PicoFloorArea> ApplyVirtualTableFallback(List<PicoFloorArea> areas, string reason)`，供正常查询与刷新路径共用；测试以反射调用，不调用原生 SDK。
- Produces 私有纯条件 `static bool ShouldStartSceneCapture(int areaCount, bool captureRequested)`，控制首次识别时机。

- [ ] **Step 1: 写矩形构造失败测试**

在 `PicoFloorAreaTests` 添加：
- `TryCreateRectangle_RotatedSizeAndHeight_ReturnsExpectedWorldVertices`：中心 `(1,0.4,2)`、尺寸 `(2,1)`、偏航 90°，断言四顶点、SurfaceHeight 0.4、SurfaceRotation 和 PlanarBounds 符合手算结果。
- `TryCreateRectangle_RandomPointsStayInside`：对旋转矩形采样 20 次，均满足 `ContainsXZ` 且 Y 为中心高度。
- `TryCreateRectangle_ZeroNegativeOrNonFiniteInput_ReturnsFalse`：零/负尺寸、NaN/Infinity 中心、尺寸或偏航均返回 `false`，`out area.Vertices` 为空。

- [ ] **Step 2: 写 Provider 回退失败测试**

新增 `VirtualTableFallbackTests`，直接创建 Provider 但不调用 `GetFloorAreasAsync`。通过反射调用 `ApplyVirtualTableFallback`：
- `EmptyTable_ReturnsConfiguredVirtualAreaAndDiagnostics`：空列表返回恰好一个默认区域，中心/尺寸/高度正确，`DebugFloorAreaCount=1`、方法为 `Virtual table fallback`，详情包含原因与四个顶点。
- `RealAreas_ReturnsOriginalListWithoutVirtualReplacement`：非空列表返回同一实例，不覆盖真实高度来源。
- `FloorOrDisabledFallback_ReturnsOriginalEmptyList`：分别设置 Floor 和禁用开关，空列表原样返回。
- `InvalidVirtualSize_LogsWarningAndReturnsEmpty`：尺寸 0 时 Warning，返回空列表且 `DebugFloorAreaCount=0`。
- `FirstEmptyQuery_RequestsCaptureOnlyOnce`：`ShouldStartSceneCapture(0,false)` 为 true，`(0,true)` 与 `(1,false)` 均为 false。

- [ ] **Step 3: 运行并确认 RED**

```bash
"/d/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "PicoFloorAreaTests,VirtualTableFallbackTests" -testResults ".superpowers/virtual-table-red.xml" -logFile ".superpowers/virtual-table-red.log"
```

若逗号过滤器未被 Unity 接受，分两次运行两个 fixture。预期：因矩形 API、Provider 字段或回退入口不存在而编译失败。

- [ ] **Step 4: 实现矩形构造与 Provider 回退入口**

`TryCreateRectangle` 校验所有输入有限、尺寸严格大于 0，按 Y 偏航生成中心高度一致的四个顶点，构造 `PicoFloorArea(..., useHighestVertexHeight: true, surfaceRotation: rotation)`。

Provider 的 `ApplyVirtualTableFallback` 先原样返回非空区域；Floor、禁用开关也原样返回。空 Table 使用 `TryCreateRectangle`，失败则 Warning、数量设 0 并返回空；成功则返回单元素列表，更新高度、方法、查询状态和详细快照，并调用 `RebuildDebugMeshes` 显示同一矩形。

- [ ] **Step 5: 接入首次查询、失败与锚点刷新**

`GetFloorAreasAsync` 使用 `ShouldStartSceneCapture` 保持“第一次为空则请求一次 Scene Capture”的现有顺序，仅在识别后的最终结果上应用回退。启动/查询/识别异常在 Table 且启用回退时记录原因并返回虚拟结果；Floor 或禁用回退时继续抛出原异常。

`RefreshAreas` 在查询后应用空区域回退，再调用现有 `AreasUpdated` 一次；异常路径在启用 Table 回退时发布一个虚拟区域，否则维持原 Warning 行为。真实非空区域不经过虚拟替换，后续事件仍由现有消费者处理。

- [ ] **Step 6: 运行并确认 GREEN**

重跑两个 fixture，再跑完整 PlayMode，输出 `virtual-table-task1-full.xml`。预期：新增测试与完整回归均 `failed="0"`；恢复 Unity 批处理产生的 `ProjectSettings.runInBackground` 改动。

- [ ] **Step 7: 提交**

```bash
git add Assets/Scripts/Pico/PicoFloorArea.cs Assets/Scripts/Pico/Scene/PicoSceneCaptureFloorAreaProvider.cs Assets/Tests/PlayMode/PicoFloorAreaTests.cs Assets/Tests/PlayMode/VirtualTableFallbackTests.cs Assets/Tests/PlayMode/VirtualTableFallbackTests.cs.meta Assets/Tests/PlayMode/Trailblazer.Pico.PlayModeTests.asmdef docs/superpowers/specs/2026-11-18-virtual-table-fallback-design.md docs/superpowers/plans/2026-11-18-virtual-table-fallback.md
git commit -m "feat(桌面): 无识别结果时使用统一虚拟桌面"
```

### Task 2: 道具来源标识与高度缓存更新

**Files:**
- Modify: `Assets/Scripts/Pico/PicoSceneSpawnPositions.cs`
- Modify: `Assets/Tests/PlayMode/PicoSceneSpawnPositionsTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `ISceneCaptureDebugInfo.DebugGroundHeightMethod == "Virtual table fallback"` 和当前区域高度。
- Produces: 虚拟生成时 `LastSpawnSource == "VirtualTable"`、`LastGroundHeightMethod == "Virtual table fallback"`；真实来源保持 `SceneCapture`。

- [ ] **Step 1: 写失败测试**

让现有 FakeProvider 同时实现 `ISceneCaptureDebugInfo`，默认方法为 `SceneCapture table top`，其余诊断属性返回零值。添加：
- `StartSpawn_VirtualProvider_ReportsVirtualSourceAndHeight`：虚拟桌面高度 0、clearance 0.1，断言生成 Y 为 0.1，来源与方法均为虚拟标记。
- `StartSpawn_AreaHeightChanges_UsesCurrentHeightInsteadOfCachedHeight`：第一次真实区域高度 0.2 生成 Y 0.3，第二次虚拟区域高度 -0.4 生成 Y -0.3；第二个实例不得复用第一次的 0.3。

- [ ] **Step 2: 运行并确认 RED**

使用 `-testFilter "PicoSceneSpawnPositionsTests"`，输出 `virtual-table-spawn-red`。预期：虚拟来源断言失败，且第二次生成仍使用旧缓存高度。

- [ ] **Step 3: 实现来源传播与当前高度采样**

成功取得区域时，从实际 `provider` 对象读取 `ISceneCaptureDebugInfo`；虚拟方法对应 `LastSpawnSource = "VirtualTable"`，否则保持 `SceneCapture`。有诊断来源时同步给 `LastGroundHeightMethod`，无诊断 Fake 继续使用原默认文本。

每个成功查询批次使用本次采样点的 `point.y + surfaceClearance`，不再以 `_cachedSpawnHeight` 覆盖新区域高度；成功后更新缓存，仅供后续固定区域/失败回退使用。

- [ ] **Step 4: 运行定向与完整 GREEN**

先重跑 `PicoSceneSpawnPositionsTests`，再运行完整 PlayMode，输出 `virtual-table-final.xml`。预期：所有测试通过，`git diff --check` 无本功能格式错误，既有场景/Coin 修改仍未暂存。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Pico/PicoSceneSpawnPositions.cs Assets/Tests/PlayMode/PicoSceneSpawnPositionsTests.cs
git commit -m "fix(道具): 同步虚拟桌面来源与当前高度"
```
