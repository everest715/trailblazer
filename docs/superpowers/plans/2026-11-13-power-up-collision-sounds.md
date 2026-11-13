# 道具碰撞音效独立配置 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将道具碰撞音效从 `Game` 迁移到各 Prefab 的 `CollisionEffect.effectSound`，每次碰撞播放一次。

**Architecture:** `CollisionEffect` 负责粒子与音效表现，`Game` 只保留玩法和 Multiplier 延迟结束音。7 个现有道具 Prefab 直接序列化各自 AudioClip，不新增组件或重复播放参数。

**Tech Stack:** Unity 2022.3.62f3、C#、NUnit PlayMode Tests、Unity YAML Prefab/Scene。

**Spec:** `docs/superpowers/specs/2026-11-13-power-up-collision-sounds-design.md`

## Global Constraints

- `3Coin` 迁移后只播放一声，不增加重复播放参数。
- 保留 `MultiplierEnd`、Gate、起步、错误音和全部玩法逻辑；不处理已注释的 Boost。
- 不还原、暂存或提交用户已有的无关改动；`BikeScene` 不纳入自动提交。
- 使用 Unity `2022.3.62f3` 运行 PlayMode 测试，不宣称完成 PICO 真机验证。

## Review Focus

- 空粒子 Prefab + 有效音效：仍应在碰撞点播放，由 `OnTriggerEnter_NullPrefab_PlaysEffectSoundAtClosestPoint` 验证。
- 非 Player 碰撞：不得创建音效，由 `OnTriggerEnter_NonPlayer_DoesNotPlayEffectSound` 验证。
- `3Coin`：只绑定一次 `Coin.mp3`，不保留 `Handle3CoinSound` 的三次播放。
- Multiplier：碰撞开始音迁移，延迟结束音保留，由代码检查和完整 PlayMode 回归验证。
- 两个同名变体：`Coin 2` 与 `Bomb 1` 也必须配置，由 Prefab 参数化测试验证。

---

### Task 1: 音效播放独立于粒子 Prefab

**Files:**
- Modify: `Assets/Scripts/Pico/CollisionEffect.cs`
- Modify: `Assets/Tests/PlayMode/CollisionEffectTests.cs`
- Create: `Assets/Tests/PlayMode/CollisionEffectTests.cs.meta`（若 Unity 尚未生成）

**Interfaces:**
- Consumes: `CollisionEffect.effectSound`、`OnTriggerEnter(Collider other)`。
- Produces: 空 `effectPrefab` 时仍可在最近点播放一次 `effectSound`；非 Player 不播放。

- [ ] **Step 1: 写失败测试**

在 `CollisionEffectTests` 增加 `OnTriggerEnter_NullPrefab_PlaysEffectSoundAtClosestPoint`：创建运行时 AudioClip，将 `effectPrefab = null`，触发 Player 碰撞，断言一个 `One shot audio` 的 AudioSource 使用该 Clip 且位置为 `(0.5, 0, 0)`。增加 `OnTriggerEnter_NonPlayer_DoesNotPlayEffectSound`，断言无此 AudioSource；TearDown 清理这些对象。

- [ ] **Step 2: 运行并确认 RED**

```bash
mkdir -p .superpowers
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "CollisionEffectTests" -testResults ".superpowers/power-up-sound-red.xml" -logFile ".superpowers/power-up-sound-red.log"
```

预期：空 Prefab 测试因当前提前 `return` 而没有 AudioSource，断言失败。

- [ ] **Step 3: 最小修改 `CollisionEffect.Play(Vector3 position)`**

将音效播放放在空 `effectPrefab` 的提前返回之前；保留现有一次 `AudioSource.PlayClipAtPoint(effectSound, position)` 和全部粒子逻辑。

- [ ] **Step 4: 重跑并确认 GREEN**

使用 Step 2 命令，输出名改为 `power-up-sound-green`。预期：`CollisionEffectTests` 全部通过。

- [ ] **Step 5: 提交组件与测试**

```bash
git add -- Assets/Scripts/Pico/CollisionEffect.cs Assets/Tests/PlayMode/CollisionEffectTests.cs Assets/Tests/PlayMode/CollisionEffectTests.cs.meta docs/superpowers/specs/2026-11-13-power-up-collision-sounds-design.md docs/superpowers/plans/2026-11-13-power-up-collision-sounds.md
git commit -m "feat(碰撞): 支持目标物体独立播放音效"
```

### Task 2: 更新 7 个道具 Prefab

**Files:**
- Create: `Assets/Tests/PlayMode/PowerUpCollisionSoundTests.cs` 及 `.meta`
- Modify: `Assets/Prefabs/Coin.prefab`、`Coin 2.prefab`、`3Coin.prefab`
- Modify: `Assets/Prefabs/TimeBonus.prefab`、`Multiplier.prefab`
- Modify: `Assets/Prefabs/Bomb.prefab`、`Bomb 1.prefab`

**Interfaces:**
- Consumes: Task 1 的 `CollisionEffect.effectSound`。
- Produces: 每个目标 Prefab 的对应 AudioClip GUID。

- [ ] **Step 1: 写 Prefab 参数化失败测试**

在 Editor 下通过 `AssetDatabase.LoadAssetAtPath<GameObject>` 和 `AssetDatabase.LoadAssetAtPath<AudioClip>` 加载真实资产。参数化断言根物体的 `CollisionEffect.effectSound` 分别为：Coin/Coin 2/3Coin → `Assets/Sounds/Coin.mp3`；TimeBonus → `Assets/Sounds/TimeBonus.mp3`；Multiplier → `Assets/Sounds/Multiplier.wav`；Bomb/Bomb 1 → `Assets/Polygon Arsenal/Sound/Wav/polygon_explosion_rocket.wav`。

- [ ] **Step 2: 运行并确认 RED**

使用 Task 1 的 Unity 命令，将筛选改为 `PowerUpCollisionSoundTests`、输出名改为 `power-up-prefab-red`。预期：7 个用例因 `effectSound` 为空而失败。

- [ ] **Step 3: 写入 Prefab 配置**

仅在各根物体现有 `CollisionEffect` 的 `particleLifetimeMultiplier` 后添加 `effectSound`，引用对应 AudioClip 的 `fileID: 8300000` 和 GUID：Coin `71f8bc1ca0da14b5bbfcab9de2e83c6d`；TimeBonus `b388d32b5aebe4e29a2d93c13d2c5946`；Multiplier `62952af05fddb46629923f518f8d3602`；Bomb `c63c97718a9532c45903f33e38d138ce`。

- [ ] **Step 4: 重跑并确认 GREEN**

使用 Step 2 命令，输出名改为 `power-up-prefab-green`。预期：7 个用例全部通过，原粒子配置不变。

- [ ] **Step 5: 提交 Prefab 与测试**

```bash
git add -- Assets/Prefabs/Coin.prefab "Assets/Prefabs/Coin 2.prefab" Assets/Prefabs/3Coin.prefab Assets/Prefabs/TimeBonus.prefab Assets/Prefabs/Multiplier.prefab Assets/Prefabs/Bomb.prefab "Assets/Prefabs/Bomb 1.prefab" Assets/Tests/PlayMode/PowerUpCollisionSoundTests.cs Assets/Tests/PlayMode/PowerUpCollisionSoundTests.cs.meta
git commit -m "chore(道具): 配置独立碰撞音效"
```

### Task 3: 移除 Game 统一播放并回归

**Files:**
- Modify: `Assets/Scripts/Game.cs`
- Modify: `Assets/Scenes/BikeScene.unity`

**Interfaces:**
- Consumes: Task 2 的 Prefab 音效配置。
- Produces: 不含道具碰撞统一播放的 `Game.OnTriggerEnter(Collider other)`；保留 `Multiplier()` 中的 `multEndSound`。

- [ ] **Step 1: 运行静态检查并确认 RED**

```bash
! rg -n 'PlayOneShot\((coinSound|multSound|timeBonusSound|bombSound)\)|Handle3CoinSound' Assets/Scripts/Game.cs
```

预期：命令失败并列出仍存在的统一播放调用，证明检查能捕获旧实现。

- [ ] **Step 2: 删除旧播放与字段**

删除 `coinSound`、`multSound`、`timeBonusSound`、`bombSound` 字段，移除对应 `PlayOneShot`、3Coin 协程调用和整个 `Handle3CoinSound`；保留 `multEndSound` 与 Multiplier 结束播放。删除 `BikeScene` 中这四个已移除字段的序列化行，不改其他场景内容。

- [ ] **Step 3: 验证静态检查 GREEN 并运行完整 PlayMode**

先重跑 Step 1，预期成功且无输出。再运行 Unity `-runTests -testPlatform PlayMode`，输出 `.superpowers/power-up-sound-full.xml` 和 `.log`。预期：编译成功，完整测试无失败；若存在既有失败，逐项报告，不顺手修改。

- [ ] **Step 4: 检查最终差异并提交代码**

确认 7 个 Prefab 引用正确、`Game` 无重复播放、`multEndSound` 保留，且未暂存无关文件。仅提交 `Game.cs`；Scene 清理保留在工作区，避免混入用户已有场景改动。

```bash
git add -- Assets/Scripts/Game.cs
git commit -m "refactor(道具): 移除统一碰撞音效播放"
```
