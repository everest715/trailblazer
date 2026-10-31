# MR 观察者 + Tracker 遥控小车 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将头显从玩家逻辑中解耦，用 PICO Tracker 驱动小车并触发原有交互和碰撞特效。

**Architecture:** 继续使用方案 A 的最小重构。PICO 位姿源通过 `IPicoTrackerPoseSource` 隔离，驱动和特效可用 fake 做 PlayMode 测试；场景中的原 `PlayerCollider` 改为 TrackedCar，原有 Game 暂时保留以便以后迁移到 InteractionRouter。

**Tech Stack:** Unity 2022.3.62f3、PICO Unity SDK 6.1.1 Motion Tracking、C#、Unity Test Framework。

**Spec:** `docs/superpowers/specs/2026-10-31-tracked-car-design.md`

## Global Constraints

- 头显只观察，不作为 Player、轨迹或 Gate 的碰撞主体。
- 保留现有金币、Gate、计分和轨迹玩法。
- 无效 Tracker 位姿不得移动小车。
- 使用 `ByteDance.PICO.XR`，不恢复 Meta SDK。
- 真机 Tracker 校准与验证单独报告，不预先宣称完成。

## Review Focus

- 无效位姿或丢帧导致瞬移：Task 1 验证不调用 MovePosition/MoveRotation。
- 特效在拾取物销毁后丢失位置：Task 2 在销毁前用 ClosestPoint 创建。
- 生成点落入观察者或赛道外：Task 2 同时过滤 Scene Capture 与回退候选。
- 回退自动生成与场景自动生成重复：Task 3 保持回退 SpawnOnStart=false。

---

### Task 1: 可测试的 Tracker 驱动

**Files:**
- Create: `Assets/Scripts/Pico/IPicoTrackerPoseSource.cs`
- Create: `Assets/Scripts/Pico/MotionTrackerDriver.cs`
- Create: `Assets/Tests/PlayMode/MotionTrackerDriverTests.cs`

**Interfaces:**
- `IPicoTrackerPoseSource.TryGetPose(out Vector3 position, out Quaternion rotation) : bool`。
- `MotionTrackerDriver.positionOffset : Vector3`、`rotationOffset : Quaternion`。
- `MotionTrackerDriver.FixedUpdate()` 仅在有效位姿时移动 Rigidbody。

- [ ] **Step 1: 写失败测试**

用 fake pose source 验证：有效位姿应用位置/旋转偏移；无效位姿保持原 Rigidbody 状态；连续无效帧不累积位移。

- [ ] **Step 2: 运行并确认 RED**

运行新增 PlayMode 测试，预期类型不存在或断言失败。

- [ ] **Step 3: 实现最小驱动**

Awake 获取 Rigidbody 和 pose source；FixedUpdate 读取有效位姿并调用 MovePosition/MoveRotation。无效时直接返回，不写 Transform。

- [ ] **Step 4: 重跑并提交**

全部新测试及既有 8 个测试通过后提交：

```bash
git add Assets/Scripts/Pico Assets/Tests/PlayMode
git commit -m "feat: add tracker-driven car movement"
```

### Task 2: 碰撞特效与赛道过滤

**Files:**
- Create: `Assets/Scripts/CarCollisionEffects.cs`
- Modify: `Assets/Scripts/Game.cs`
- Modify: `Assets/Scripts/Pico/PicoSceneSpawnPositions.cs`
- Create: `Assets/Tests/PlayMode/CarCollisionEffectsTests.cs`
- Modify: `Assets/Tests/PlayMode/PicoSceneSpawnPositionsTests.cs`

**Interfaces:**
- `CarCollisionEffects.Play(Collider other, Transform car) : void`。
- `Game.collisionEffects : CarCollisionEffects`。
- `PicoSceneSpawnPositions.playAreaBounds : Bounds`。

- [ ] **Step 1: 写失败测试**

验证特效在最近点创建且只创建一次；Scene Capture 地面点超出 playAreaBounds 时走回退，范围内才使用 Scene Capture。

- [ ] **Step 2: 运行并确认 RED**

新增测试因类型或过滤逻辑缺失而失败。

- [ ] **Step 3: 实现特效与过滤**

特效在 `other.ClosestPoint(car.position)` 实例化并定时销毁；`Game.OnTriggerEnter` 在处理 Tag 前调用（StartingArea 除外）。生成器在加入 Scene Capture 候选点前检查 playAreaBounds。

- [ ] **Step 4: 重跑并提交**

全部测试通过后提交：

```bash
git add Assets/Scripts/Game.cs Assets/Scripts/CarCollisionEffects.cs Assets/Scripts/Pico/PicoSceneSpawnPositions.cs Assets/Tests/PlayMode
git commit -m "feat: add car collision effects and track bounds"
```

### Task 3: PICO 位姿源与场景迁移

**Files:**
- Create: `Assets/Scripts/Pico/Scene/PicoMotionTrackerPoseSource.cs`
- Modify: `Assets/Scripts/Pico/Scene/Trailblazer.Pico.Scene.asmdef`
- Modify: `Assets/Resources/PXR_ProjectSetting.asset`
- Modify: `Assets/Scenes/BikeScene.unity`
- Modify: `README.md`、`Agent.md`

**Interfaces:**
- `PicoMotionTrackerPoseSource` 实现 `IPicoTrackerPoseSource`，请求 `MotionTrackerNum.ONE` 并保存最新有效位姿。

- [ ] **Step 1: 实现 PICO 位姿源**

订阅请求完成回调，获取 Tracker ID；TryGetPose 调用 GetMotionTrackerLocation，成功且有效时输出 SDK 转换后的 Position/Quaternion，否则 false。开启 `bodyTracking: 1`。

- [ ] **Step 2: 迁移场景**

将 `PlayerCollider` 改名 `TrackedCar`，移除 FollowCamera，添加 MotionTrackerDriver、PicoMotionTrackerPoseSource 和 CarCollisionEffects；Game.collisionEffects 连接，DrawTrail.player 指向 TrackedCar，设置 playAreaBounds 和初始特效 Prefab。

- [ ] **Step 3: 验证并提交**

主工程编译无错误；场景只有小车使用 Player Tag；回退不自动重复生成；APK 构建成功。文档记录自由观察者、Tracker 配对和校准流程。

```bash
git add Assets/Scripts/Pico/Scene Assets/Resources/PXR_ProjectSetting.asset Assets/Scenes/BikeScene.unity README.md Agent.md
git commit -m "feat: make tracked car the MR player"
```
