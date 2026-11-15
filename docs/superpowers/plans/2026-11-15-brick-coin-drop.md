# Brick Coin Drop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 玩家撞碎 `MBrick Gate` 后，以可配置的 30% 概率生成一个现有可拾取金币。

**Architecture:** 在现有 `CollisionEffect` 的玩家碰撞流程末尾增加独立金币掉落判定。组件默认不掉落，仅配置目标砖块 Prefab，避免影响其他道具。

**Tech Stack:** Unity 2022.3.62f3、C#、NUnit、Unity PlayMode Test Framework。

**Spec:** `docs/superpowers/specs/2026-11-15-brick-coin-drop-design.md`

## Global Constraints

- 金币使用 `Assets/Prefabs/Coin.prefab`，目标砖块概率为 30%。
- 金币生成在砖块 `transform.position`，保持 Prefab 旋转和缩放。
- 未配置金币、概率为 0% 或非玩家碰撞时不生成金币。
- 不改变现有碎裂特效、音效及其他道具默认行为。
- 不暂存或修改无关的既有工作区改动。

## Review Focus

- 概率边界：0% 必须从不掉落，100% 必须总是掉落；由 Task 1 的边界测试覆盖。
- 空引用：概率大于 0 但 `coinPrefab` 为空时不能异常或生成对象；由 Task 1 的空引用测试覆盖。
- 碰撞来源：非玩家不能触发金币；由 Task 1 的非玩家测试覆盖。
- 重复生成：一次有效碰撞最多生成一个金币；由 Task 1 的 100% 测试计数覆盖。
- 配置遗漏：代码存在但砖块未接线会导致功能不可见；由 Task 1 的 Prefab 配置测试覆盖。

---

### Task 1: 金币掉落逻辑与砖块配置

**Files:**
- Modify: `Assets/Scripts/Pico/CollisionEffect.cs`
- Modify: `Assets/Tests/PlayMode/CollisionEffectTests.cs`
- Create: `Assets/Tests/PlayMode/BrickCoinDropTests.cs`
- Create: `Assets/Tests/PlayMode/BrickCoinDropTests.cs.meta`
- Modify: `Assets/Prefabs/MBrick Gate.prefab`
- Modify: `docs/superpowers/specs/2026-11-15-brick-coin-drop-design.md`
- Modify: `docs/superpowers/plans/2026-11-15-brick-coin-drop.md`

**Interfaces:**
- Consumes: 现有 `CollisionEffect.OnTriggerEnter(Collider)`、`CollisionEffect.OnCollisionEnter(Collision)` 和 `Play(Vector3 position)`。
- Produces: `public GameObject coinPrefab`、`[Range(0f, 100f)] public float dropChance`；不改变现有公开字段。

- [x] **Step 1: 写掉落逻辑失败测试**

在 `CollisionEffectTests` 增加独立金币 Prefab 及实例清理逻辑，并添加：
- `OnTriggerEnter_ZeroDropChance_DoesNotCreateCoin`：配置金币、`dropChance = 0`，触发后无金币实例。
- `OnTriggerEnter_FullDropChance_CreatesOneCoinAtTargetPosition`：`dropChance = 100`，触发后只有一个实例，位置为 `_target.transform.position`，旋转与 Prefab 相同。
- `OnTriggerEnter_NullCoinPrefab_DoesNotCreateCoin`：`dropChance = 100`、金币为空，触发后无金币实例。
- `OnTriggerEnter_NonPlayer_DoesNotCreateCoin`：`dropChance = 100`，非玩家触发后无金币实例。

- [x] **Step 2: 写砖块配置失败测试**

创建 `BrickCoinDropTests`，在编辑器下通过 `AssetDatabase` 加载 `Assets/Prefabs/MBrick Gate.prefab` 和 `Assets/Prefabs/Coin.prefab`，断言砖块的 `CollisionEffect.coinPrefab` 引用金币且 `dropChance == 30f`。

- [x] **Step 3: 运行测试并确认 RED**

Run:

```bash
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "CollisionEffectTests;BrickCoinDropTests" -testResults ".superpowers/brick-coin-drop-red.xml" -logFile ".superpowers/brick-coin-drop-red.log"
```

Expected: 编译成功；新增测试因字段或配置不存在而失败，既有测试不失败。

- [x] **Step 4: 实现最小掉落逻辑**

在 `CollisionEffect` 增加 `coinPrefab` 与百分比 `dropChance`。`Play` 保留原特效和音效流程，并仅执行一次判定：`coinPrefab != null` 且 `dropChance > 0`；`dropChance >= 100` 时直接通过，否则 `Random.value < dropChance / 100f`。成功时以 `transform.position` 和 `coinPrefab.transform.rotation` 实例化，不修改缩放、不设置父节点。

- [x] **Step 5: 配置 MBrick Gate**

在 `Assets/Prefabs/MBrick Gate.prefab` 的 `CollisionEffect` 中引用 `Assets/Prefabs/Coin.prefab`，设置 `dropChance: 30`。其他 Prefab 不修改。

- [x] **Step 6: 运行目标测试并确认 GREEN**

Run:

```bash
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "CollisionEffectTests;BrickCoinDropTests" -testResults ".superpowers/brick-coin-drop-green.xml" -logFile ".superpowers/brick-coin-drop-green.log"
```

Expected: `CollisionEffectTests` 和 `BrickCoinDropTests` 全部通过，无编译错误。

- [x] **Step 7: 运行完整回归验证**

Run:

```bash
"D:/Program Files/Unity/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testResults ".superpowers/brick-coin-drop-full.xml" -logFile ".superpowers/brick-coin-drop-full.log"
```

Expected: 完整 PlayMode 测试无失败；若存在失败，先与改动前行为对照并如实报告，不顺带修复无关问题。

- [x] **Step 8: 检查最终差异并提交**

确认差异只包含本计划文件、掉落代码、测试和 `MBrick Gate` 配置；`git diff --check` 对 C# / Markdown 无新增空白错误。Unity 生成 YAML 的既有尾随空格不自动重写。

```bash
git add -- "Assets/Scripts/Pico/CollisionEffect.cs" "Assets/Tests/PlayMode/CollisionEffectTests.cs" "Assets/Tests/PlayMode/BrickCoinDropTests.cs" "Assets/Tests/PlayMode/BrickCoinDropTests.cs.meta" "Assets/Prefabs/MBrick Gate.prefab" "docs/superpowers/specs/2026-11-15-brick-coin-drop-design.md" "docs/superpowers/plans/2026-11-15-brick-coin-drop.md"
git commit -m "feat(砖块): 撞碎后概率生成金币"
```
