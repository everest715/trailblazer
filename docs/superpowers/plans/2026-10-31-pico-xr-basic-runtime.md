# PICO XR 基础运行版 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 `BikeScene` 使用 PICO XR 相机、手柄和视频透视运行，并用固定安全区域替代两处 MRUK 生成。

**Architecture:** 新增独立的 `Trailblazer.Pico` 运行时程序集承载生成器，保留现有玩法脚本和场景结构。先替换生成与 Rig，再配置透视并验证；Meta SDK 在本阶段保留，后续单独清理。

**Tech Stack:** Unity 2022.3.62f3、C#、URP 14.0.11、PICO Unity SDK 6.1.1、XR Interaction Toolkit 3.0.7、Unity Test Framework 1.1.33。

**Spec:** `docs/superpowers/specs/2026-10-31-pico-xr-basic-runtime-design.md`

## Global Constraints

- 仅面向 PICO OS 6 / PICO XR，不新增 Quest 兼容层。
- 保持 Unity 2022.3.62f3 和 URP 14.0.11。
- 不改变计分、轨迹、Gate、计时和道具效果。
- 本阶段不实现 Scene Capture / Spatial Mesh，不移除 Meta SDK。
- 所有 `.meta` 文件必须与对应 Unity 资源一起提交。

## Review Focus

- 场景出现重复 XR Origin、MainCamera 或 AudioListener：Task 2 在 Project Validation 与 Play 验证中逐项检查。
- 替换 Rig 后玩家、HUD 或轨迹失去相机引用：Task 2 检查所有引用并验证骑行与 HUD 跟随。
- 生成点超出安全区域或空 Prefab 引发异常：Task 1 用边界与 null 测试固定行为。
- 视频透视被 HDR 或后处理阻断：Task 3 检查 Mobile URP、Camera 与场景后处理设置。
- Meta 与 PICO 验证规则混淆：本阶段只处理 PICO Required 规则，Task 3 不要求 Meta 警告提前消失。

---

### Task 1: 固定安全区域生成器

**Files:**
- Create: `Assets/Scripts/Pico/Trailblazer.Pico.asmdef`
- Create: `Assets/Scripts/Pico/PicoSpawnPositions.cs`
- Create: `Assets/Tests/PlayMode/Trailblazer.Pico.PlayModeTests.asmdef`
- Create: `Assets/Tests/PlayMode/PicoSpawnPositionsTests.cs`

**Interfaces:**
- Consumes: 现有 `SpawnPowerUps` 对 `SpawnObject` 和 `StartSpawn()` 的调用，以及场景中 `SpawnOnStart`、`SpawnObject`、`SpawnAmount` 字段。
- Produces: `PicoSpawnPositions.SpawnObject : GameObject`、`PicoSpawnPositions.SpawnAmount : int`、`PicoSpawnPositions.SpawnOnStart : bool`、`PicoSpawnPositions.spawnBounds : Bounds`、`PicoSpawnPositions.StartSpawn() : void`。

- [ ] **Step 1: 写失败测试**

在 `PicoSpawnPositionsTests` 中建立生成器和一个最小 prefab，覆盖：
- `StartSpawn_SpawnsRequestedAmountInsideBounds`：`SpawnAmount = 3`，调用一次后生成 3 个实例，所有实例的位置都位于转换后的 `spawnBounds` 内。
- `StartSpawn_NullPrefabDoesNotThrowOrSpawn`：`SpawnObject = null`，调用不抛异常且不生成实例。
- `Start_SpawnOnStartSpawnsOnce`：以 `[UnityTest]` 等待一帧，启用组件且 `SpawnOnStart = true` 时只自动生成 `SpawnAmount` 个实例。

- [ ] **Step 2: 运行测试并确认失败**

在 Unity Test Runner 的 PlayMode 页运行 `PicoSpawnPositionsTests`。预期：因 `PicoSpawnPositions` 尚不存在而编译失败或测试失败。

- [ ] **Step 3: 实现生成器**

`PicoSpawnPositions` 使用 `spawnBounds` 的本地 X/Y/Z 范围随机取点，通过 `transform.TransformPoint` 转到世界空间；`StartSpawn()` 按 `SpawnAmount` 实例化 `SpawnObject`，null 时直接返回。`Start()` 仅在 `SpawnOnStart` 为 true 时调用一次 `StartSpawn()`。不加入房间识别、对象池或额外玩法。

- [ ] **Step 4: 运行测试并确认通过**

在 Test Runner 重跑三个测试。预期：3/3 PASS，无 Unity 编译错误。

- [ ] **Step 5: 提交生成器与测试**

```bash
git add Assets/Scripts/Pico Assets/Tests/PlayMode
git commit -m "feat: add PICO-safe spawn area fallback"
```

### Task 2: 替换 BikeScene 的 Rig 与 MRUK 生成组件

**Files:**
- Modify: `Assets/Scripts/SpawnPowerUps.cs`
- Modify: `Assets/Scenes/BikeScene.unity`
- Modify: `ProjectSettings/TagManager.asset`
- Modify: PICO SDK 已生成的 `Assets/Resources/PXR_*`、`Assets/XR/*` 与项目配置

**Interfaces:**
- Consumes: Task 1 的 `PicoSpawnPositions` 和 PICO XR Origin / Controller Tracking。
- Produces: 含一个 PICO XR Origin、一个 `PXR_Manager`、两处 `PicoSpawnPositions` 的可打开 `BikeScene`。

- [ ] **Step 1: 移除代码级 Meta 类型依赖**

将 `SpawnPowerUps` 的私有字段类型改为 `PicoSpawnPositions`，`GetComponent` 同步替换，删除 `Meta.XR.MRUtilityKit` using；保留 `powerUps`、2 秒场景配置和现有随机选择逻辑。

- [ ] **Step 2: 替换两处生成组件**

在 `SpawnPowerUps` 与 `SpawnCoinsAtStart` 对象上，用 `PicoSpawnPositions` 替换 Meta `FindSpawnPositions`。保留各对象已有的 Prefab 列表、`SpawnAmount`、`SpawnOnStart` 与 `spawnInterval`，分别设置不穿过玩家起步区的 `spawnBounds`。

- [ ] **Step 3: 替换 OVR Rig**

在 Editor 中通过 PICO Building Blocks 添加 XR Origin / PICO Controller Tracking，为 XR Origin 添加 `PXR_Manager`。把原 OVR Rig 下仍需保留的玩家对象与引用迁移到 PICO Rig，再禁用并删除 `OVRCameraRigInteraction` 和 `MRUK`；不得留下第二个启用的 XR Origin、MainCamera 或 AudioListener。

- [ ] **Step 4: 验证场景引用与基础运行**

运行 PICO Project Validation，确认不再缺少 `PXR_Manager`，且 MainCamera、AudioListener、XR Origin 数量各为一个。进入 Play 或 Live Preview，验证头显/手柄、HUD 跟随、轨迹、Gate、金币与道具生成；预期无 Missing Reference、无 MRUK 依赖。

- [ ] **Step 5: 提交场景迁移**

只暂存 Task 2 相关场景、脚本与 PICO 配置，不包含无关 TextMesh Pro 变化。

```bash
git add Assets/Scenes/BikeScene.unity Assets/Scripts/SpawnPowerUps.cs Assets/Resources/PXR_* Assets/XR Assets/Plugins/Android Packages/manifest.json Packages/packages-lock.json ProjectSettings/TagManager.asset ProjectSettings/EditorBuildSettings.asset ProjectSettings/ProjectSettings.asset
git commit -m "feat: migrate bike scene to PICO XR"
```

### Task 3: 视频透视与构建验证

**Files:**
- Modify: `Assets/Scenes/BikeScene.unity`
- Modify: `Assets/Resources/PXR_ProjectSetting.asset`
- Modify: `Assets/Settings/Mobile_RPAsset.asset`（仅当验证发现 HDR 被重新开启时）
- Modify: `ProjectSettings/ProjectSettings.asset`（仅 PICO Portal 要求项）

**Interfaces:**
- Consumes: Task 2 的 PICO XR Origin 与 `PXR_Manager`。
- Produces: 可构建的 PICO OS 6 基础 MR APK。

- [ ] **Step 1: 配置视频透视**

在 `PXR_Manager` 开启 Video Seethrough；Main Camera 设置 Solid Color、背景 RGBA 全 0。确认 Main Camera 后处理关闭、Mobile URP `m_SupportsHDR: 0`，场景没有启用的后处理遮挡透视。

- [ ] **Step 2: 修复 PICO Required 验证项**

打开 `Edit > Project Settings > XR Plug-in Management > Project Validation`，逐项处理 PICO Required；保留当前 Meta SDK 所产生的 OpenXR Loader 规则，不在本阶段强行清理。

- [ ] **Step 3: 构建 Android APK**

用 `BikeScene` 作为唯一启用场景执行 Android Build。预期：构建成功且没有 C# 编译错误；若失败，记录第一条真实错误并修复，不跳过 Required 规则。

- [ ] **Step 4: 验证 PICO 运行**

在 PICO Emulator 或 OS 6 真机验证透视、头显/手柄、金币与道具生成、轨迹自碰撞、计分和重新开始。模拟器不能证明真机透视/空间行为，相关项必须标注“待真机”直到设备验证。

- [ ] **Step 5: 提交透视与验证配置**

```bash
git add Assets/Scenes/BikeScene.unity Assets/Resources/PXR_ProjectSetting.asset Assets/Settings/Mobile_RPAsset.asset ProjectSettings/ProjectSettings.asset
git commit -m "feat: enable PICO video passthrough"
```
