# 桌面不规则围墙 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用 MFence、MTree、MTree2 沿真实桌面边缘生成不越界的不规则障碍围墙，MFence 以桌边方向为基准仅叠加小角度随机偏航。

**Architecture:** `PicoObstacleGeometry` 负责模型本地 Bounds、长轴对齐与完整水平占地校验；`PicoTableBoundaryObstacles` 负责边缘采样、随机混合、向内调整、缩放和生命周期。生成器消费 `PicoFloorArea` 并跟随 Provider 更新，场景仅负责引用配置。

**Tech Stack:** Unity 2022.3.62f3、C#、PICO XR、NUnit PlayMode Tests、Unity Scene YAML。

**Spec:** `docs/superpowers/specs/2026-11-18-table-boundary-obstacles-design.md`

## Global Constraints

- 保持 Unity 2022.3、PICO OS 6、PICO XR，不引入新依赖或其他头显兼容层。
- 使用现有 `MFence.prefab`、`MTree.prefab`、`MTree2.prefab`，不修改第三方 Prefab。
- 障碍物完整水平占地范围不得越过桌面；仅根节点在桌面内不算通过。
- MFence 默认随机偏航为 ±8°，Inspector 限制为 0–15°；允许桌边正向或反向，不允许垂直。
- 不改变金币、敌人、Gate、Tracker 或桌面高度算法。
- 保留 `BikeScene.unity` 和两个 Coin Prefab 中已有的未提交修改，不全量覆盖或意外暂存。
- Editor 测试不替代 PICO 真机外观与标定验证。

## Review Focus

- 旋转后的桌面：长轴对齐不能假定桌边固定为世界 X/Z；Task 1 用旋转边方向测试，Task 2 用旋转矩形整体验证。
- 根节点在桌内但旋转模型角点越界：必须拒绝或调整；Task 1 的旋转 Bounds 测试覆盖。
- 空模型、退化多边形、过窄桌面：不能产生 NaN、无限循环或越界实例；Task 1 和 Task 2 的空输入/小桌面测试覆盖。
- 高频标定更新或重复手动重建：只能保留一份生成结果；Task 2 的重复重建测试覆盖。
- Prefab 含多个 LOD 和偏心网格：只能按 LOD0 计算根节点本地 Bounds，不能把多个 LOD 叠加；Task 1 的 LOD/偏心网格测试覆盖。

---

### Task 1: 模型占地与桌边方向几何

**Files:**
- Create: `Assets/Scripts/Pico/PicoObstacleGeometry.cs` 及 `.meta`
- Test: `Assets/Tests/PlayMode/PicoObstacleGeometryTests.cs` 及 `.meta`

**Interfaces:**
- Produces: `public static bool PicoObstacleGeometry.TryCapture(GameObject root, out Bounds localBounds)`。
- Produces: `public static Vector3 PicoObstacleGeometry.GetLongitudinalAxis(Bounds localBounds)`，返回 `Vector3.right` 或 `Vector3.forward`。
- Produces: `public static Quaternion PicoObstacleGeometry.AlignLongAxis(Vector3 localAxis, Vector3 edgeDirection, float yawOffset)`。
- Produces: `public static bool PicoObstacleGeometry.FitsInside(PicoFloorArea area, Bounds localBounds, Vector3 worldPosition, Quaternion worldRotation, float uniformScale, float edgeMargin)`。

- [ ] **Step 1: 写失败测试**

在 `PicoObstacleGeometryTests` 添加：
- `AlignLongAxis_RotatedEdgeAndSmallYaw_UsesEdgeAsReference`：边方向为世界 Y 轴旋转 37° 后的 X 方向，局部长轴为 X，偏航 8°，断言结果长轴相对桌边的 `Vector3.SignedAngle` 为 8°，而非世界角度 8°。
- `FitsInside_RotatedBoundsWithRootInside_RejectsOutsideCorner`：桌面为 `[-1,1] × [-1,1]`，本地 Bounds 长 1.8、宽 0.2，根节点 `(0.4,0,0)`、旋转 45°，断言根点虽在桌内但完整 Bounds 返回 `false`。
- `FitsInside_PositiveMargin_ChecksAllCorners`：同一 Bounds 在中心、缩放 0.5、margin 0.05 时通过，再放大至角点进入边缘带后失败。
- `TryCapture_LodGroupAndOffsetMesh_UsesOnlyLod0InRootSpace`：根节点下 LOD0 与 LOD1 使用不同尺寸和偏移，断言结果只包含 LOD0，坐标转换到根节点空间。
- `TryCapture_NoMesh_ReturnsFalse`：空 GameObject 返回 `false`，输出有限 Bounds。

- [ ] **Step 2: 运行并确认 RED**

```bash
"/d/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "PicoObstacleGeometryTests" -testResults ".superpowers/table-boundary-geometry-red.xml" -logFile ".superpowers/table-boundary-geometry-red.log"
```

预期：因 `PicoObstacleGeometry` 不存在而编译失败。

- [ ] **Step 3: 实现几何 API**

`TryCapture` 优先使用根节点 LODGroup 的第 0 层 Renderer 对应 MeshFilter；无 LODGroup 时使用所有激活 MeshFilter。用根节点逆变换把每个 `sharedMesh.bounds` 的八角点转换到根空间后 Encapsulate，不叠加高 LOD。

`GetLongitudinalAxis` 比较本地 Bounds 的 X/Z 尺寸。`AlignLongAxis` 将边方向投影到 XZ 并归一化，使用局部轴偏航 `Atan2(axis.x, axis.z)`、桌边偏航 `Atan2(edge.x, edge.z)`，返回 Y 轴四元数 `edgeYaw - axisYaw + yawOffset`。

`FitsInside` 按 position/rotation/uniformScale 变换本地 Bounds 的四个不同 XZ 角点，逐点调用 `area.ContainsXZ(corner, edgeMargin)`；空区域、非正缩放或非有限输入返回 `false`。

- [ ] **Step 4: 运行并确认 GREEN**

使用 Step 2 命令，文件名改为 `table-boundary-geometry-green`。预期：`PicoObstacleGeometryTests` 全部通过。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Pico/PicoObstacleGeometry.cs Assets/Scripts/Pico/PicoObstacleGeometry.cs.meta Assets/Tests/PlayMode/PicoObstacleGeometryTests.cs Assets/Tests/PlayMode/PicoObstacleGeometryTests.cs.meta docs/superpowers/specs/2026-11-18-table-boundary-obstacles-design.md docs/superpowers/plans/2026-11-18-table-boundary-obstacles.md
git commit -m "feat(桌面): 添加障碍物占地与边缘方向校验"
```

### Task 2: 不规则边缘生成与重建生命周期

**Files:**
- Create: `Assets/Scripts/Pico/PicoTableBoundaryObstacles.cs` 及 `.meta`
- Test: `Assets/Tests/PlayMode/PicoTableBoundaryObstaclesTests.cs` 及 `.meta`

**Interfaces:**
- Consumes: Task 1 的四个 `PicoObstacleGeometry` API，现有 `IPicoFloorAreaProvider.GetFloorAreasAsync()` 和具体 Provider 的 `AreasUpdated`。
- Produces: `public async Task RebuildAsync()` 与 `public void Rebuild(PicoFloorArea area)`。
- Produces: `public int GeneratedCount { get; private set; }` 和 `public Transform GeneratedRoot { get; private set; }`。
- Produces Inspector 字段：`MonoBehaviour sceneCaptureProvider`、三个 Prefab、`fenceLength = 0.24f`、`treeHeight = 0.28f`、限制为 `[Range(0f, 15f)] fenceMaxYaw = 8f`、`edgeSafetyMargin = 0.005f`、`spacingRange = (0.01f, 0.04f)`、`inwardOffsetRange = (0f, 0.02f)`、`scaleRange = (0.9f, 1.1f)`、`randomSeed = 20261118`。

- [ ] **Step 1: 写失败测试**

测试 Prefab 使用带真实 MeshFilter/MeshRenderer 的长方体，树与 Fence 使用不同根对象名称。添加：
- `Rebuild_Rectangle_UsesAllThreePrefabsAcrossEdges`：在 `[-1,1]` 矩形上重建，断言 `GeneratedCount > 0`，生成根下出现 `MFence`、`MTree`、`MTree2` 三类实例。
- `Rebuild_RotatedRectangle_FenceYawStaysWithinEightDegreesAndNeverPerpendicular`：矩形旋转 37°，对每个 Fence 找最近桌边，计算无向 0–90° 夹角；正向/反向均允许，但相对最近平行方向的偏差不得超过 8.01°。
- `Rebuild_GeneratedFootprintsStayInsideRotatedArea`：对同一旋转矩形的所有实例调用 Task 1 `FitsInside` 复查完整 Bounds，断言全部通过且底部 Y 等于桌面高度。
- `Rebuild_TinyOrDegenerateArea_SkipsWithoutOutsideInstances`：过小矩形和少于三个顶点的区域均不抛异常、不留下实例、不进入无限循环。
- `Rebuild_CalledTwice_ReplacesOnlyOwnedInstances`：记录旧根与数量，在场景放一个同名但非本生成器所有的对象；再次重建后旧根销毁、数量不翻倍、外部对象仍存在。
- `RebuildAsync_EmptyThenValidProvider_ClearsAndRebuilds`：Fake Provider 先返回空区域再返回有效区域，断言先清空后生成；异常时输出既有围栏生成失败 Warning 且不抛到调用方。

- [ ] **Step 2: 运行并确认 RED**

使用 Unity PlayMode 命令，`-testFilter "PicoTableBoundaryObstaclesTests"`，结果与日志命名为 `table-boundary-generator-red`。预期：因生成器不存在而编译失败。

- [ ] **Step 3: 实现确定性的边缘采样**

`Rebuild` 先销毁并清空自己的旧根，再用局部 `System.Random(randomSeed)` 创建结果，禁止修改 `UnityEngine.Random` 全局状态。遍历桌面每条边，将方向投影到 XZ；以两个候选垂直方向中能把边中点带入桌内的方向作为 inward。

每条边使用重复洗牌的 `[fence, tree, tree2]` 类型袋，保证桌面足够大时三种模型都出现。游标按模型沿桌边投影长度加随机间距推进，并设置每边最多 64 个候选的硬上限；跳过候选也必须推进，退化边直接跳过。

Fence 使用 `AlignLongAxis`，在 ±`fenceMaxYaw` 内随机偏航并随机增加 0°/180°；树木随机 0–360°。Fence 按 `fenceLength / 本地XZ最长尺寸` 缩放，树按 `treeHeight / 本地Bounds.size.y` 缩放，再乘 `scaleRange` 随机值。

- [ ] **Step 4: 实现向内调整、落地与安全跳过**

候选初始位置为边上的采样点加随机 inward 偏移。每个候选最多尝试 4 次：每次增加 0.02m inward 偏移并把均匀缩放乘 0.9；以 `SurfaceHeight - localBounds.min.y * scale` 设置根 Y，使模型底部落在桌面。只有 `FitsInside` 通过才实例化到生成根下；始终失败则跳过，不保留越界对象。

- [ ] **Step 5: 实现 Provider 生命周期**

`Start` 在引用为空时查找 `PicoSceneCaptureFloorAreaProvider`，随后调用 `RebuildAsync`。`RebuildAsync` 通过 `sceneCaptureProvider as IPicoFloorAreaProvider` 获取区域，空结果清空，首个有效区域调用 `Rebuild`，异常记录 Warning。`OnEnable`/`OnDisable` 成对订阅具体 Provider 的 `AreasUpdated`，事件只重建首个区域；`OnDestroy` 只清理自己的生成根。

- [ ] **Step 6: 运行并确认 GREEN**

重跑 `PicoTableBoundaryObstaclesTests`，结果命名为 `table-boundary-generator-green`，再运行 Task 1 测试。预期：两者全部通过。

- [ ] **Step 7: 提交**

```bash
git add Assets/Scripts/Pico/PicoTableBoundaryObstacles.cs Assets/Scripts/Pico/PicoTableBoundaryObstacles.cs.meta Assets/Tests/PlayMode/PicoTableBoundaryObstaclesTests.cs Assets/Tests/PlayMode/PicoTableBoundaryObstaclesTests.cs.meta
git commit -m "feat(桌面): 沿桌边生成不规则障碍围墙"
```

### Task 3: BikeScene 配置与完整回归

**Files:**
- Modify: `Assets/Scenes/BikeScene.unity`（PICO GameObject `242273339` 的组件列表及新增组件块）
- Test: `Assets/Tests/PlayMode/TableBoundaryObstacleSceneTests.cs` 及 `.meta`

**Interfaces:**
- Consumes: Task 2 生成器 `.meta` GUID、Provider 组件 `242273341`、Prefab GUID `90638a590311a104b94a39177afb78a3` / `8bd4db4730a85174cbf65a95c99f0b79` / `927d2bb5075ea8941af7255c8d96e391`。
- Produces: 打开 BikeScene 即可随 Table Scene Capture 自动生成围墙。

- [ ] **Step 1: 写失败场景测试**

打开 `BikeScene`，添加 `BikeScene_ConfiguresTableBoundaryObstacles`：断言场景唯一生成器连接到现有 Table Provider，三个 Prefab 名称依次为 `MFence`、`MTree`、`MTree2`，`fenceMaxYaw` 为 8°，且 Provider 原有 `surfaceLabel = Table`、Tracker 高度覆盖和调试网格配置未改变。测试不保存场景。

- [ ] **Step 2: 运行并确认 RED**

使用 `-testFilter "TableBoundaryObstacleSceneTests"`，输出 `table-boundary-scene-red`。预期：找不到生成器而失败。

- [ ] **Step 3: 定点接入场景**

先记录当前场景 diff。只给 PICO GameObject 增加一个唯一 fileID 的 `PicoTableBoundaryObstacles` 组件，连接 Provider 和三个 Prefab，保留设计默认值；使用精确文本替换，不通过 Unity 全量保存场景，不修改现有场景其他 hunk。

- [ ] **Step 4: 验证场景与完整回归**

先运行场景测试并确认 GREEN，再运行完整 PlayMode：

```bash
"/d/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testResults ".superpowers/table-boundary-full.xml" -logFile ".superpowers/table-boundary-full.log"
```

预期：完整结果 `failed="0"`。若 Unity 仅因批处理把 `ProjectSettings.runInBackground` 改为 1，恢复该生成性变化。运行 `git diff --check`，检查场景 diff 仅新增本功能 hunk 加原用户改动。

- [ ] **Step 5: 仅提交本功能改动**

暂存新测试后，对 `BikeScene.unity` 使用 `git add -p` 或等价 index patch，只暂存新增生成器组件 hunk；明确保留两个 Coin Prefab 与原有场景 hunk 未暂存。检查 `git diff --cached` 后提交：

```bash
git add Assets/Tests/PlayMode/TableBoundaryObstacleSceneTests.cs Assets/Tests/PlayMode/TableBoundaryObstacleSceneTests.cs.meta
git commit -m "feat(场景): 启用桌面不规则障碍围墙"
```
