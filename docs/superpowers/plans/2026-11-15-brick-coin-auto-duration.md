# Brick Coin Auto Duration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 金币使用 0.4 米距离和 0.3 米弧高，并默认按真实抛物线公式自动计算约 0.495 秒的飞行时间。

**Architecture:** `CollisionEffect` 在调用 `CoinArcFlight.Launch` 前选择自动或手动时长。自动模式仅使用高度和重力，现有运动组件无需改动。

**Tech Stack:** Unity 2022.3.62f3、C#、NUnit、Unity PlayMode Test Framework。

**Spec:** `docs/superpowers/specs/2026-11-15-brick-coin-auto-duration-design.md`

## Global Constraints

- `duration = sqrt(8 * height / gravity)`，默认高度 0.3、重力 9.81、距离 0.4。
- 自动模式默认开启；手动模式使用 `coinDropDuration = 0.5f`。
- 非正重力使用 0.01 下限；零高度由现有运动组件钳制到 0.01 秒。
- 保持 30% 概率、碰撞方向、轨迹形状和 Collider 生命周期不变。
- 在当前 `pico` 工作区实施，不触碰无关改动。

## Review Focus

- 自动公式错误：会提前或延后恢复拾取；由自动模式落地时间测试覆盖。
- 手动覆盖失效：关闭自动后仍使用自动值会破坏演出配置；由手动时长测试覆盖。
- 非法重力：零或负重力不能产生 NaN；由非正重力测试覆盖。
- Prefab 旧序列化：显式保存新参数并由配置测试验证。
- 距离与高度修改影响既有方向：由现有碰撞方向和完整回归测试覆盖。

---

### Task 1: 自动时长与参数配置

**Files:**
- Modify: `Assets/Scripts/Pico/CollisionEffect.cs`
- Modify: `Assets/Tests/PlayMode/CollisionEffectTests.cs`
- Modify: `Assets/Tests/PlayMode/BrickCoinDropTests.cs`
- Modify: `Assets/Prefabs/MBrick Gate.prefab`
- Create: `docs/superpowers/specs/2026-11-15-brick-coin-auto-duration-design.md`
- Create: `docs/superpowers/plans/2026-11-15-brick-coin-auto-duration.md`

**Interfaces:**
- Consumes: `CoinArcFlight.Launch(Vector3 direction, float distance, float height, float duration)`。
- Produces: `autoCoinDropDuration`、`coinDropGravity`，以及更新后的距离、高度和手动时长默认值。

- [x] **Step 1: 写自动和手动时长失败测试**

在 `CollisionEffectTests` 添加：
- 自动模式：高度 0.3、重力 9.81，金币在 0.4 秒时仍飞行，约 0.55 秒时已落地并恢复 Collider；该断言可区分旧 0.65 秒时长。
- 手动模式：关闭自动、设置时长 0.2 秒，0.25 秒后已落地并恢复 Collider。
- 非正重力：自动模式在重力 0 时不产生 NaN，并能最终落地。

- [x] **Step 2: 扩展 Prefab 配置失败测试**

在 `BrickCoinDropTests` 断言 `MBrick Gate` 的距离为 0.4、高度为 0.3、自动时长开启、重力为 9.81、手动时长为 0.5。

- [x] **Step 3: 运行并确认 RED**

同步到测试副本，运行 `CollisionEffectTests;BrickCoinDropTests`，输出 `.superpowers/brick-coin-auto-duration-red.xml` 和 `.log`。

Expected: 因自动时长字段不存在或参数未配置而失败。

- [x] **Step 4: 实现自动时长选择**

在 `CollisionEffect` 增加 `autoCoinDropDuration = true`、`coinDropGravity = 9.81f`，将距离改为 0.4、高度改为 0.3、手动时长默认值改为 0.5。调用 `Launch` 前：自动模式计算 `Mathf.Sqrt(8f * coinDropHeight / Mathf.Max(0.01f, coinDropGravity))`，高度上限不小于 0；手动模式使用 `coinDropDuration`。

- [x] **Step 5: 显式配置 MBrick Gate**

在现有金币掉落字段后保存距离 0.4、高度 0.3、时长 0.5、自动开启和重力 9.81，不改变概率和引用。

- [x] **Step 6: 运行目标测试并确认 GREEN**

使用 Step 3 命令，输出名改为 `brick-coin-auto-duration-green`。

Expected: 自动、手动、非正重力和 Prefab 配置测试全部通过。

- [x] **Step 7: 完整回归并提交**

在同步副本运行完整 PlayMode，输出 `brick-coin-auto-duration-full.xml`。检查 C# / Markdown `git diff --check`，只提交本功能文件。

```bash
git add -- Assets/Scripts/Pico/CollisionEffect.cs Assets/Tests/PlayMode/CollisionEffectTests.cs Assets/Tests/PlayMode/BrickCoinDropTests.cs "Assets/Prefabs/MBrick Gate.prefab" docs/superpowers/specs/2026-11-15-brick-coin-auto-duration-design.md docs/superpowers/plans/2026-11-15-brick-coin-auto-duration.md
git commit -m "feat(金币): 根据高度自动计算飞行时间"
```
