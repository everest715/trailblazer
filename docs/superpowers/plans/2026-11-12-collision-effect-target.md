# 被碰撞物体独立特效 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让每个被碰撞物体通过自身组件配置并播放独立特效，替代小车上的全局 `CarCollisionEffects`。

**Architecture:** 新增挂在触发 Collider 同物体上的 `CollisionEffect`，自行接收 `OnTriggerEnter` 并筛选 Player。`Game` 只保留玩法逻辑；现有交互 Prefab 分别序列化自己的特效配置。

**Tech Stack:** Unity 2022.3、C#、NUnit PlayMode Tests、Unity YAML Prefab/Scene。

**Spec:** `docs/superpowers/specs/2026-11-12-collision-effect-target-design.md`

## Global Constraints

- 仅响应 `Player` Tag；空 Prefab 和非 Player 碰撞不生成特效。
- 保留最近点生成、Prefab 旋转、缩放、Hierarchy 粒子参数和自动销毁行为。
- 普通特效缩放 `0.005`，Bomb 缩放 `0.08`；生命周期 `2`、粒子速度倍率 `5`、粒子生命周期倍率 `0.5`。
- 不改变金币、积分、Gate、Bomb 等玩法逻辑，不增加冷却或范围触发。

## Review Focus

- 非 Player 刚体碰撞：不得生成特效，由 `OnTriggerEnter_NonPlayer_DoesNotCreateEffect` 验证。
- 未配置 Prefab：不得抛异常或生成空实例，由 `OnTriggerEnter_NullPrefab_DoesNotCreateEffect` 验证。
- 同一帧多个 Trigger 消息：每次消息生成一个独立实例，由 `OnTriggerEnter_Player_CreatesOneEffectAtClosestPoint` 的实例计数验证单次行为。
- Gate 在 Game 中立即禁用：特效须先执行，由 `CollisionEffect_HasEarlierExecutionOrderThanGame` 验证。
- Prefab 自身已有缩放和旋转：实例化不得覆盖其旋转且缩放应相乘，由 `OnTriggerEnter_Player_PreservesPrefabRotationAndMultipliesScale` 验证。

---

### Task 1: 目标物体特效组件

**Files:**
- Create: `Assets/Scripts/Pico/CollisionEffect.cs`
- Create: `Assets/Scripts/Pico/CollisionEffect.cs.meta`
- Create: `Assets/Tests/PlayMode/CollisionEffectTests.cs`
- Create: `Assets/Tests/PlayMode/CollisionEffectTests.cs.meta`

**Interfaces:**
- Produces: `CollisionEffect : MonoBehaviour`。
- Produces: 公共字段 `GameObject effectPrefab`、`float lifetime = 2f`、`float effectScale = 1f`、`float particleSpeedMultiplier = 3f`、`float particleLifetimeMultiplier = 0.5f`。
- Consumes: 同 GameObject 的 `Collider` 和碰撞方的 `Player` Tag。

- [ ] **Step 1: 写失败测试**

在 `CollisionEffectTests` 中建立带 BoxCollider 的目标物体、Player/非 Player 碰撞体和特效 Prefab，通过 `SendMessage("OnTriggerEnter", otherCollider)` 触发。添加以下测试：
- `OnTriggerEnter_Player_CreatesOneEffectAtClosestPoint`：断言仅一个实例且位置为目标 Collider 的最近点。
- `OnTriggerEnter_NonPlayer_DoesNotCreateEffect`、`OnTriggerEnter_NullPrefab_DoesNotCreateEffect`：断言无实例。
- `OnTriggerEnter_Player_PreservesPrefabRotationAndMultipliesScale`：断言旋转保持 Prefab 值，缩放为 Prefab 缩放乘 `effectScale`。
- `OnTriggerEnter_Player_AppliesHierarchyParticleSettings`：断言 Hierarchy 粒子的速度、生命周期、重力倍率正确，Shape 粒子的 scalingMode 不被改写。
- `CollisionEffect_HasEarlierExecutionOrderThanGame`：断言 `DefaultExecutionOrder` 小于 `0`。

- [ ] **Step 2: 运行并确认 RED**

```bash
mkdir -p .superpowers
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "CollisionEffectTests" -testResults ".superpowers/collision-effect-red.xml" -logFile ".superpowers/collision-effect-red.log"
```

预期：因 `CollisionEffect` 类型不存在而编译失败或测试失败。

- [ ] **Step 3: 实现 `CollisionEffect`**

使用 `[RequireComponent(typeof(Collider))]` 和 `[DefaultExecutionOrder(-100)]`。缓存自身 Collider；`OnTriggerEnter(Collider other)` 在 Player 且 Prefab 非空时，于 `_collider.ClosestPoint(other.transform.position)` 实例化 Prefab，按配置调整缩放和 Hierarchy 粒子，清理并播放粒子后以 `Destroy(effect, lifetime)` 销毁。使用 `effectPrefab.transform.rotation`，不区分 Bomb Tag。

- [ ] **Step 4: 重跑并确认 GREEN**

使用 Step 2 命令，将输出文件名改为 `collision-effect-green`。预期：`CollisionEffectTests` 全部通过。

- [ ] **Step 5: 提交组件与测试**

```bash
git add Assets/Scripts/Pico/CollisionEffect.cs Assets/Scripts/Pico/CollisionEffect.cs.meta Assets/Tests/PlayMode/CollisionEffectTests.cs Assets/Tests/PlayMode/CollisionEffectTests.cs.meta docs/superpowers/specs/2026-11-12-collision-effect-target-design.md docs/superpowers/plans/2026-11-12-collision-effect-target.md
git commit -m "feat(碰撞): 支持目标物体独立配置特效"
```

### Task 2: 移除小车全局特效调度

**Files:**
- Modify: `Assets/Scripts/Game.cs:56,257-263`
- Modify: `Assets/Scenes/BikeScene.unity`
- Delete: `Assets/Scripts/Pico/CarCollisionEffects.cs` 及其 `.meta`
- Delete: `Assets/Tests/PlayMode/CarCollisionEffectsTests.cs` 及其 `.meta`

**Interfaces:**
- Consumes: Task 1 的 `CollisionEffect`。
- Produces: 无全局特效字段的 `Game.OnTriggerEnter(Collider other)`。

- [ ] **Step 1: 移除旧引用和实现**

删除 `Game.collisionEffects` 以及 `OnTriggerEnter` 开头的特效调用，保留其余 Tag 分支原样。删除旧组件与旧测试文件。

- [ ] **Step 2: 清理 BikeScene**

从 `TrackedCar` 移除 fileID `1323473304` 的组件引用和对应 `CarCollisionEffects` MonoBehaviour 块，同时删除 Game 的 `collisionEffects` 序列化行；不改 TrackedCar 的 Player Tag、Rigidbody 或其他组件。

- [ ] **Step 3: 验证编译与相关测试**

```bash
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "CollisionEffectTests;GateGroundAlignmentTests" -testResults ".superpowers/collision-effect-game.xml" -logFile ".superpowers/collision-effect-game.log"
```

预期：编译成功，筛选测试全部通过；`rg -n "CarCollisionEffects|collisionEffects" Assets` 无结果。

- [ ] **Step 4: 提交解耦改动**

```bash
git add Assets/Scripts/Game.cs Assets/Scenes/BikeScene.unity Assets/Scripts/Pico/CarCollisionEffects.cs Assets/Scripts/Pico/CarCollisionEffects.cs.meta Assets/Tests/PlayMode/CarCollisionEffectsTests.cs Assets/Tests/PlayMode/CarCollisionEffectsTests.cs.meta
git commit -m "refactor(碰撞): 移除小车全局特效调度"
```

### Task 3: 迁移现有交互 Prefab 配置

**Files:**
- Modify: `Assets/Prefabs/Coin.prefab`、`Coin 2.prefab`、`3Coin.prefab`、`Speed.prefab`、`TimeBonus.prefab`、`Shield.prefab`、`Multiplier.prefab`、`Heal.prefab`、`Lightning.prefab`
- Modify: `Assets/Prefabs/Bomb.prefab`、`Bomb 1.prefab`
- Modify: `Assets/Prefabs/GateActive.prefab`、`GateBlocked.prefab`、`GateFinal.prefab`
- Modify: `Assets/Prefabs/PowerboxColHealth 1.prefab`、`PowerboxColHeart 1.prefab`、`PowerboxColQuestion 1.prefab`、`PowerboxColSkates 1.prefab`、`PowerboxColSkull 1.prefab`、`PowerboxColSpeed 1.prefab`、`PowerboxColStar 1.prefab`

**Interfaces:**
- Consumes: `CollisionEffect.effectPrefab` 及四个数值配置字段。
- Produces: 运行时无需 Game 引用即可播放特效的现有交互 Prefab。

- [ ] **Step 1: 给玩法 Trigger Collider 所在物体添加组件**

仅给带上述玩法 Tag 且具有 Trigger Collider 的 GameObject 添加 `CollisionEffect`，不为 Base、模型子物体或非 Trigger Collider 添加。普通物体引用 `Explosion Fire`（guid `7800b8f6b712bfe49bd663eeb3ebe1e4`），Bomb 引用 `GrenadeExplosionRed`（guid `8aea2a892ad46aa439b8be49adccdcad`），数值严格使用 Global Constraints。

- [ ] **Step 2: 静态核验 Prefab 配置**

用脚本检查上述 21 个 Prefab：每个目标 Tag 所在 GameObject 恰有一个新组件引用；组件的 `m_GameObject` 指向该物体，Prefab guid 和数值符合约束，场景中不再引用旧组件 guid `d55bc3635af013944b33e7b5edbc6e06`。

- [ ] **Step 3: 运行完整 PlayMode 回归**

```bash
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testResults ".superpowers/collision-effect-full.xml" -logFile ".superpowers/collision-effect-full.log"
```

预期：Unity 编译无错误，全部 PlayMode 测试通过。

- [ ] **Step 4: 检查最终差异并提交**

确认差异仅包含设计/计划、组件与测试、Game、BikeScene 和上述 Prefab；不暂存其他未跟踪资源。

```bash
git add Assets/Prefabs
git commit -m "chore(碰撞): 迁移现有交互物体特效配置"
```
