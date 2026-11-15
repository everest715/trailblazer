# Brick Coin Arc Drop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 砖块金币沿 Tracker → 砖块的水平碰撞方向抛物线抛出，飞行中禁用 Collider，落地后恢复拾取。

**Architecture:** 新增无 Rigidbody 依赖的 `CoinArcFlight`，负责确定性的抛物线运动和 Collider 状态。`CollisionEffect` 只负责计算碰撞方向、配置并启动运动。

**Tech Stack:** Unity 2022.3.62f3、C#、NUnit、Unity PlayMode Test Framework。

**Spec:** `docs/superpowers/specs/2026-11-15-brick-coin-arc-drop-design.md`

## Global Constraints

- 方向为 `brick.position - tracker.position` 去除 Y 后归一化。
- 默认距离 0.6 米、弧高 0.6 米、时长 0.65 秒；落点 Y 等于起点 Y。
- 飞行中禁用金币层级 Collider，落地仅恢复原本启用者。
- 保留 30% 概率、现有金币 Prefab 和既有特效、音效、缩放动画。
- 沿用用户指定的当前 `pico` 工作区，不触碰无关改动。

## Review Focus

- 方向入口不一致：Trigger 与 Collision 都应根据 Tracker 位置计算方向；由 Task 2 的 Trigger 集成测试及共享方向辅助方法保证。
- 水平位置重合：不能产生 NaN 或异常；由 Task 1 的零方向测试和 Task 2 的 Tracker forward 回退测试覆盖。
- Collider 初始状态不同：原本禁用者落地后仍禁用；由 Task 1 的状态恢复测试覆盖。
- 时长为零：不能除零或卡住；由 Task 1 的零时长落地测试覆盖。
- 只运动未恢复拾取：落地位置正确但 Collider 仍禁用会使金币无法拾取；由 Task 1 和 Task 2 的落地断言覆盖。

---

### Task 1: 抛物线运动与 Collider 生命周期

**Files:**
- Create: `Assets/Scripts/Pico/CoinArcFlight.cs`
- Create: `Assets/Scripts/Pico/CoinArcFlight.cs.meta`
- Create: `Assets/Tests/PlayMode/CoinArcFlightTests.cs`
- Create: `Assets/Tests/PlayMode/CoinArcFlightTests.cs.meta`

**Interfaces:**
- Consumes: Unity `Transform`、`Collider`、`Time.deltaTime`。
- Produces: `CoinArcFlight.Launch(Vector3 direction, float distance, float height, float duration)`；启动后立即禁用 Collider，并在落地恢复。

- [x] **Step 1: 写运动组件失败测试**

创建真实金币对象和 Collider 的 PlayMode 测试：
- `Launch_DisablesColliderUntilLanding`：启动后立即及飞行中禁用，落地后启用。
- `Launch_MovesUpAndBackwardThenLandsAtTarget`：从固定起点向 `Vector3.back` 飞行，中途高度上升且 Z 减小，最终位置为起点加 `Vector3.back * 0.6f`。
- `Launch_RestoresOnlyOriginallyEnabledColliders`：根 Collider 恢复，原本禁用的子 Collider 保持禁用。
- `Launch_ZeroDuration_LandsImmediately`：零时长下一帧到终点并恢复 Collider。
- `Launch_ZeroDirection_DoesNotProduceNaN`：零方向时垂直起落，所有坐标为有限值。

- [x] **Step 2: 运行并确认 RED**

同步资源到 `.superpowers/power-up-sound-test-project`，在该副本运行 `-testFilter "CoinArcFlightTests"`，结果写入 `.superpowers/brick-coin-arc-red.xml` 和 `.log`。

Expected: 因 `CoinArcFlight` 不存在而编译失败；实现后必须执行测试断言验证 GREEN。

- [x] **Step 3: 实现 CoinArcFlight**

实现 `Launch(Vector3 direction, float distance, float height, float duration)`：保存起点、计算同高度终点，将时长钳制到至少 0.01 秒；记录并禁用所有子级 Collider。`Update` 用 `t = elapsed / duration` 插值水平位置，叠加 `Vector3.up * (4f * height * t * (1f - t))`；`t >= 1` 时设置精确终点、恢复原始 Collider 状态并停用组件。

- [x] **Step 4: 重跑并确认 GREEN**

使用 Step 2 的副本命令，输出名改为 `brick-coin-arc-green`。

Expected: `CoinArcFlightTests` 全部通过，无编译错误。

- [x] **Step 5: 提交运动组件**

```bash
git add -- Assets/Scripts/Pico/CoinArcFlight.cs Assets/Scripts/Pico/CoinArcFlight.cs.meta Assets/Tests/PlayMode/CoinArcFlightTests.cs Assets/Tests/PlayMode/CoinArcFlightTests.cs.meta docs/superpowers/specs/2026-11-15-brick-coin-arc-drop-design.md docs/superpowers/plans/2026-11-15-brick-coin-arc-drop.md
git commit -m "feat(金币): 添加抛物线飞行动作"
```

### Task 2: 接入碰撞方向与金币生成

**Files:**
- Modify: `Assets/Scripts/Pico/CollisionEffect.cs`
- Modify: `Assets/Tests/PlayMode/CollisionEffectTests.cs`

**Interfaces:**
- Consumes: `CoinArcFlight.Launch(Vector3 direction, float distance, float height, float duration)`。
- Produces: `CollisionEffect.coinDropDistance = 0.6f`、`coinDropHeight = 0.6f`、`coinDropDuration = 0.65f`；生成金币立即启动运动。

- [x] **Step 1: 改写集成测试并确认 RED**

将原地生成断言改为抛物线生成：100% 概率下，金币带 `CoinArcFlight`，Collider 立即禁用；Tracker 位于砖块水平正前方时，落地位置等于砖块位置加 `Vector3.back * 0.6f`，Y 不变，落地后 Collider 恢复。另加 Tracker 与砖块水平位置重合时使用 Tracker 水平 forward 的测试。运行 `CollisionEffectTests`，输出 `brick-coin-arc-integration-red.xml`。

Expected: 因运动组件未接入或方向未传递而失败。

- [x] **Step 2: 接入方向和运动参数**

在 `CollisionEffect` 增加三个运动参数。Trigger 和 Collision 入口均用 `transform.position - tracker.transform.position` 计算去 Y 方向；零向量时回退到 Tracker 水平 forward，仍为零则传 `Vector3.zero`。把原 `Play(Vector3)` 扩展为接收方向，在实例化金币后添加 `CoinArcFlight` 并调用 Task 1 的 `Launch`。

- [x] **Step 3: 重跑集成测试并确认 GREEN**

运行 `CollisionEffectTests;BrickCoinDropTests;CoinArcFlightTests`，输出 `brick-coin-arc-integration-green.xml`。

Expected: 全部通过，30% 配置不变，既有特效与音效测试通过。

- [x] **Step 4: 完整回归并提交接入**

在同步副本运行完整 PlayMode，输出 `brick-coin-arc-full.xml`。预期无失败；检查 C# / Markdown `git diff --check`，只提交本任务代码与测试。

```bash
git add -- Assets/Scripts/Pico/CollisionEffect.cs Assets/Tests/PlayMode/CollisionEffectTests.cs
git commit -m "feat(砖块): 金币沿碰撞方向抛物线掉落"
```
