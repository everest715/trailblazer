# 砖块金币桌边反弹 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 砖块生成的金币在抛物线碰到当前桌面边缘时反射回桌内，同时保留现有弧线、时长和收集碰撞。

**Architecture:** `CoinArcFlight` 使用可选 `PicoFloorArea` 在 XZ 平面构造分段反射路径。`PicoSceneSpawnPositions` 把实际生成区域经 `CollisionEffect` 传给金币；没有区域的调用保持原直线轨迹。

**Tech Stack:** Unity `2022.3.62f3`、C#、NUnit PlayMode tests、PICO XR。

**Spec:** `docs/superpowers/specs/2026-11-18-brick-coin-table-bounce-design.md`

## Global Constraints

- 保留现有飞行距离、弧线高度、持续时间、金币收集和碰撞体恢复逻辑。
- 不新增物理围墙或 Rigidbody，不修改第三方模型或 Prefab。
- 真实桌面、虚拟桌面和固定区域回退共用同一行为；无区域时保持旧行为。
- 在当前 `pico` 分支工作，不创建 worktree；保留用户现有 Prefab、场景和设置改动。
- 不自动执行 Unity/Batchmode 测试；RED/GREEN 均由用户在 Test Runner 手动确认。

## Review Focus

- 轨迹跨越多条边：剩余距离必须继续反射，最终点仍在桌内；由 Task 1 连续反弹测试覆盖。
- 轨迹正好穿过桌角：两条边的法线共同反射，不得穿出或产生 NaN；由 Task 1 桌角测试覆盖。
- 无区域或零方向：保持旧行为且不得产生 NaN；由 Task 1 既有测试和零方向区域测试覆盖。
- 场景识别与固定回退：实例必须收到本次实际区域；由 Task 2 两种来源测试覆盖。
- 落地恢复：只恢复原本启用的碰撞体；由 Task 1 既有测试及新增反弹测试共同覆盖。

---

### Task 1: 金币分段反射轨迹

**Files:**
- Modify: `Assets/Scripts/Pico/CoinArcFlight.cs`
- Test: `Assets/Tests/PlayMode/CoinArcFlightTests.cs`

**Interfaces:**
- Consumes: `PicoFloorArea.Vertices`（世界坐标多边形）。
- Produces: `CoinArcFlight.Launch(Vector3 direction, float distance, float height, float duration, PicoFloorArea? area = null)`；原四参数调用继续编译并保持旧行为。

- [ ] **Step 1: 编写失败测试**

在 `CoinArcFlightTests` 添加以 `StartPosition` 为中心的方形区域工厂及以下测试：

- `Launch_TableArea_ReflectsAtEdgeAndLandsInside`：半边长 `0.25 m`，向右飞 `0.5 m`，最终回到 `StartPosition`，碰撞体恢复启用。
- `Launch_TableAreaMultipleEdges_UsesRemainingDistance`：同区域向右飞 `0.8 m`，最终为 `StartPosition + Vector3.left * 0.2f`。
- `Launch_TableAreaCorner_ReflectsBackInside`：沿 `(1,0,1)` 归一化方向飞 `0.4 m`，最终点所有分量有限且 `area.ContainsXZ(final)` 为真。
- 将既有零方向测试改为传入方形区域，断言位置不变且没有 NaN。

- [ ] **Step 2: 用户验证 RED**

请用户在 Test Runner 运行 `CoinArcFlightTests`。预期：新增测试因当前 `Launch` 不接受区域或仍直线飞出而失败；记录首个失败项，不自动运行。

- [ ] **Step 3: 实现最小反射路径**

扩展上述 `Launch` 签名。无有效区域时保留现有 `_start/_end` 路径；有区域时将水平路径拆成线段，按总进度插值，垂直分量仍使用 `4f * _height * t * (1f - t)`。

每条线段用二维射线与所有非退化边求最近正交点，反射方向为 `d - 2 * dot(d,n) * n`；相同交点的多条边合并法线以处理桌角。使用小 epsilon 防止重复命中同一点，并把剩余距离带入下一段。路径结束后沿用原碰撞体恢复和禁用组件逻辑。

- [ ] **Step 4: 用户验证 GREEN**

请用户运行 `CoinArcFlightTests`，预期全部通过；再运行 `CollisionEffectTests`，确认旧四参数调用未回归。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Pico/CoinArcFlight.cs Assets/Tests/PlayMode/CoinArcFlightTests.cs
git commit -m "feat(金币): 支持桌面边缘轨迹反射"
```

### Task 2: 将实际生成区域接入砖块金币

**Files:**
- Modify: `Assets/Scripts/Pico/CollisionEffect.cs`
- Modify: `Assets/Scripts/Pico/PicoSceneSpawnPositions.cs`
- Test: `Assets/Tests/PlayMode/CollisionEffectTests.cs`
- Test: `Assets/Tests/PlayMode/PicoSceneSpawnPositionsTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `Launch(..., PicoFloorArea? area = null)`。
- Produces: `CollisionEffect.ConfigureCoinDropArea(PicoFloorArea area)` 和只读 `CollisionEffect.CoinDropArea`（类型 `PicoFloorArea?`）。

- [ ] **Step 1: 编写失败测试**

- 在 `CollisionEffectTests` 添加 `OnTriggerEnter_ConfiguredTableArea_BouncesCoinAtEdge`：砖块与跟踪器分离以产生向右方向，配置半边长 `0.25 m` 区域、`100%` 掉落、`0.5 m` 距离和短时长；落地后金币回到起点且碰撞体恢复。
- 在 `PicoSceneSpawnPositionsTests` 添加场景识别与固定回退两个用例：给 `_prefab` 添加 `BoxCollider`、`CollisionEffect`，生成后断言 `CoinDropArea` 与 `SpawnedInArea` 收到的顶点一致；固定回退使用旋转后的 spawner，验证世界坐标区域。

- [ ] **Step 2: 用户验证 RED**

请用户运行上述两个测试类。预期：`ConfigureCoinDropArea`/`CoinDropArea` 尚不存在或区域未传递而失败；不自动运行。

- [ ] **Step 3: 实现区域传递**

在 `CollisionEffect` 保存可空区域并提供上述配置接口，生成金币时把区域传给 `Launch`。在 `PicoSceneSpawnPositions.SpawnAt` 中，于两个生成事件之前获取实例上的 `CollisionEffect` 并调用 `ConfigureCoinDropArea(area)`；无该组件时不受影响。

- [ ] **Step 4: 用户验证 GREEN 与完整回归**

请用户运行 `CoinArcFlightTests`、`CollisionEffectTests`、`PicoSceneSpawnPositionsTests`、`BrickCoinDropTests`，预期全部通过；随后运行完整 PlayMode 并报告首个失败项。最后构建 PICO 包，在真实桌面、虚拟桌面分别验证金币触边反弹、桌内收集及固定回退。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Pico/CollisionEffect.cs Assets/Scripts/Pico/PicoSceneSpawnPositions.cs Assets/Tests/PlayMode/CollisionEffectTests.cs Assets/Tests/PlayMode/PicoSceneSpawnPositionsTests.cs
git commit -m "feat(砖块): 将桌面区域传递给掉落金币"
```
