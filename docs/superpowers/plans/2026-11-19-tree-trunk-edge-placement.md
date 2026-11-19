# 树木树干贴边定位实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让树干根部仅保留 `0–2 cm` 的桌内扰动，并允许树冠伸出桌面。

**Architecture:** 在现有 `PicoTableBoundaryObstacles` 生成流程中区分树木和围墙。树木按根部位置校验，围墙继续按完整可视模型校验，不改变 prefab、场景配置或桌面区域来源。

**Tech Stack:** Unity 2022.3.62f3、C#、NUnit、Unity PlayMode Tests。

**Spec:** `docs/superpowers/specs/2026-11-19-tree-trunk-edge-placement-design.md`

## Global Constraints

- 树木沿用 `inwardOffsetRange = (0, 0.02)`，不增加场景配置项。
- 树木底部仍贴合 `area.SurfaceHeight`。
- 围墙的完整模型边界校验、随机朝向、缩放与间距行为不变。
- 保留并隔离当前工作区中未提交的桌面高度、prefab 和设置修改。

## Review Focus

- 旋转桌面：树根部仍需位于桌面多边形内，由新距离测试与更新后的旋转区域测试覆盖。
- 真实树木 prefab：允许树冠外伸但根部不得越界，由真实资源测试覆盖。
- 围墙：完整模型仍需在桌面内，由现有及更新后的旋转区域测试覆盖。
- 贴地：树木底部仍需等于桌面高度，由现有贴地断言覆盖。

---

### Task 1: 区分树木根部与围墙完整模型定位

**Files:**
- Modify: `Assets/Scripts/Pico/PicoTableBoundaryObstacles.cs`
- Test: `Assets/Tests/PlayMode/PicoTableBoundaryObstaclesTests.cs`

**Interfaces:**
- Consumes: `PicoFloorArea.ContainsXZ(Vector3 point, float edgeMargin)`、`PicoObstacleGeometry.FitsInside(...)`。
- Produces: 树木根部贴边生成；不新增公共 API。

- [ ] **Step 1: 编写失败的树木根部距离测试**

新增 `Rebuild_Trees_KeepRootsWithinConfiguredInwardOffset`：设置 `scaleRange = Vector2.one`、`inwardOffsetRange = new Vector2(0.01f, 0.02f)`，重建矩形桌面；对每个树木实例断言根部位于区域内，且到最近桌边距离不超过 `0.0201f`。该测试应能捕获“按半个树冠宽度内移”的回归。

- [ ] **Step 2: 验证测试按预期失败**

在独立 Unity 副本运行 `PicoTableBoundaryObstaclesTests.Rebuild_Trees_KeepRootsWithinConfiguredInwardOffset`。预期旧实现因根部距离包含半个树冠宽度而失败。

- [ ] **Step 3: 实现树木根部定位**

在 `Rebuild` 中仅对围墙附加 `inwardHalfExtent`；树木的 `samplePosition` 不再包含该项。在 `TryPlace` 中传入候选是否为围墙：围墙继续使用 `PicoObstacleGeometry.FitsInside`，树木改用 `area.ContainsXZ(position, edgeSafetyMargin)`。保留现有底部贴地、拟合重试和随机流程。

- [ ] **Step 4: 更新树冠外伸对应的旧测试契约**

将旋转区域和真实 prefab 测试中的树木断言改为根部 `ContainsXZ` 与底部贴地；围墙仍断言完整模型 `FitsInside`。运行全部 `PicoTableBoundaryObstaclesTests`，预期通过。

- [ ] **Step 5: 完整回归验证**

在排除当前未提交用户修改的独立副本上运行完整 PlayMode 测试，确认没有新增失败。真机构建后检查树干仅轻微内移、树冠可外伸、围墙位置不变。

- [ ] **Step 6: 经用户确认后提交**

仅暂存本任务代码、测试、设计和计划文档，使用中文 Conventional Commits；不带入其他工作区修改，不主动推送。
