# GroundCollider Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `BikeScene` 初始化时把 `GroundCollider` 放到 tracker 正下方 0.2 米，且之后不跟随 tracker。

**Architecture:** 新增一个只负责初始定位的 `GroundColliderPlacement` 组件，通过序列化引用读取 `TrackedCar` 上 `MotionTrackerDriver` 的世界坐标。组件在 `Start()` 中只设置一次位置；PlayMode 测试覆盖定位、未绑定引用和不跟随行为。

**Tech Stack:** Unity 2022.3.62f3、C#、NUnit Unity PlayMode tests、Unity YAML 场景。

**Spec:** `docs/superpowers/specs/2026-11-16-ground-collider-placement-design.md`

## Global Constraints

- 默认且场景配置的 `trackerHeightOffset` 均为 `0.2f`。
- 使用世界坐标：目标为 `tracker.transform.position + Vector3.down * trackerHeightOffset`。
- 只初始化一次，不持续跟随 tracker，不改变金币物理参数或场景扫描逻辑。
- 保留用户尚未提交的 `GroundCollider` 场景对象，不修改或暂存三个 TMP 字体资源及其他未跟踪资源。

## Review Focus

- tracker 位于非原点：期望 X/Z 相同且 Y 正好低 0.2 米；由 `Start_PlacesGroundBelowTracker` 验证。
- tracker 引用未绑定：期望保留初始位置且无异常；由 `Start_WithoutTrackerKeepsInitialPosition` 验证。
- 初始化后 tracker 移动：期望地板保持原位；由 `Start_PlacesGroundBelowTracker` 验证。

---

### Task 1: 初始定位组件与测试

**Files:**
- Create: `Assets/Scripts/Pico/GroundColliderPlacement.cs`
- Create: `Assets/Scripts/Pico/GroundColliderPlacement.cs.meta`
- Test: `Assets/Tests/PlayMode/GroundColliderPlacementTests.cs`
- Create: `Assets/Tests/PlayMode/GroundColliderPlacementTests.cs.meta`

**Interfaces:**
- Consumes: 现有 `MotionTrackerDriver.transform`。
- Produces: `GroundColliderPlacement.tracker : MotionTrackerDriver`、`GroundColliderPlacement.trackerHeightOffset : float`（默认 `0.2f`）。

- [ ] **Step 1: 编写失败的 PlayMode 测试**

创建 `GroundColliderPlacementTests`：

- `Start_PlacesGroundBelowTracker`：tracker 世界坐标设为 `(1, 2, 3)`，地板初始位置不同；绑定组件后等待一帧，断言地板位置与 `(1, 1.8, 3)` 的距离小于 `0.0001f`。随后把 tracker 移到 `(9, 9, 9)` 再等待一帧，断言地板仍为 `(1, 1.8, 3)`。
- `Start_WithoutTrackerKeepsInitialPosition`：不设置 `tracker`，等待一帧，断言地板保持 `(4, 5, 6)`。
- 测试中的 `MotionTrackerDriver` 禁用，避免测试依赖 PICO 位姿源；每个测试后销毁创建的对象。

- [ ] **Step 2: 运行测试并确认 RED**

```bash
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "GroundColliderPlacementTests" -testResults ".superpowers/ground-collider-placement-red.xml" -logFile ".superpowers/ground-collider-placement-red.log"
```

预期：因 `GroundColliderPlacement` 类型不存在而编译失败，或新测试失败；确认原因来自缺少新组件，而非既有错误。

- [ ] **Step 3: 实现最小组件**

创建 `GroundColliderPlacement : MonoBehaviour`，公开上述两个字段，将 `trackerHeightOffset` 默认设为 `0.2f`。在私有 `Start()` 中，仅当 `tracker != null` 时设置一次 `transform.position = tracker.transform.position + Vector3.down * trackerHeightOffset`；不添加 `Update()`。

- [ ] **Step 4: 运行测试并确认 GREEN**

使用 Step 2 的 Unity 命令，将输出文件名中的 `red` 改为 `green`。

预期：`GroundColliderPlacementTests` 两项测试全部通过。

- [ ] **Step 5: 提交组件和测试**

```bash
git add -- Assets/Scripts/Pico/GroundColliderPlacement.cs Assets/Scripts/Pico/GroundColliderPlacement.cs.meta Assets/Tests/PlayMode/GroundColliderPlacementTests.cs Assets/Tests/PlayMode/GroundColliderPlacementTests.cs.meta
git commit -m "feat: initialize ground collider below tracker"
```

### Task 2: BikeScene 绑定与回归验证

**Files:**
- Modify: `Assets/Scenes/BikeScene.unity`（现有 `GroundCollider`，GameObject fileID `285215936`）

**Interfaces:**
- Consumes: Task 1 的 `GroundColliderPlacement` 脚本 GUID 和字段。
- Produces: 启动时自动定位的场景内 `GroundCollider`。

- [ ] **Step 1: 绑定场景组件**

在用户已添加的 `GroundCollider` 上增加 `GroundColliderPlacement` 组件，并保留现有 Transform 与 BoxCollider：

- `tracker: {fileID: 1323473305}`，指向 `TrackedCar` 的 `MotionTrackerDriver`。
- `trackerHeightOffset: 0.2`。
- 将新组件 fileID 加入 GameObject 的 `m_Component` 列表，不改动其他场景对象。

- [ ] **Step 2: 检查场景差异**

Run: `git diff -- Assets/Scenes/BikeScene.unity`

预期：差异仅包含用户原有的 `GroundCollider`、SceneRoots 对应项，以及新组件引用和配置；不包含字体或其他资源。

- [ ] **Step 3: 运行完整 PlayMode 回归**

```bash
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testResults ".superpowers/ground-collider-placement-full.xml" -logFile ".superpowers/ground-collider-placement-full.log"
```

检查 XML 汇总和日志。预期：编译成功且完整测试无失败；若存在既有失败，报告首个失败及相关测试名，不顺手修改。

- [ ] **Step 4: 提交场景绑定**

```bash
git add -- Assets/Scenes/BikeScene.unity
git commit -m "feat: place BikeScene ground below tracker"
```

真机验证不属于自动测试结论：在 PICO 上进入 `BikeScene`，撞砖产生金币，确认金币落在 tracker 初始位置下方 0.2 米的碰撞地板上。
