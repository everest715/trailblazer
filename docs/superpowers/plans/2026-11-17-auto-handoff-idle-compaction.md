# Auto-handoff Idle Compaction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 从 Agent 完全空闲时开始计时，持续 idle 5 分钟且上下文超过 100,000 tokens 后 compaction 原会话，并将本次摘要写入 `HANDOFF.md`。

**Architecture:** 用一次性 idle 定时器替换固定周期调度；`agent_start` 取消计时，`agent_settled` 开始计时。idle compaction 通过可测试的桥接函数复用 `CompactionResult.summary`，原子更新 Handoff，不再为定时路径单独生成摘要。

**Tech Stack:** Pi Extension API、TypeScript、Node.js `node:test`、Node mock timers、原子文件写入。

**Spec:** `docs/superpowers/specs/2026-11-17-auto-handoff-idle-compaction-design.md`

## Global Constraints

- idle 阈值固定为 300,000 ms，token 条件固定为严格大于 100,000。
- 只压缩原会话，不创建或切换新会话。
- 退出和 `new`/`resume` 切换时的现有保存行为保持不变。
- 手动 `/compact` 和 Pi 自身自动 compaction 不额外同步 `HANDOFF.md`。
- compaction 失败、同步抛错或空摘要必须保留旧 Handoff。
- 不新增第三方依赖。

## Review Focus

- 到期前后重新开始活动：旧计时器不得 compaction；Task 1 的活动取消测试。
- 到期时 busy、存在 queued message 或无新内容：不得 compaction；Task 1 的条件测试。
- compaction 异步失败、同步抛错或空摘要：旧文件不变；Task 2 的失败输入测试。
- 会话关闭时仍有未完成 compaction：等待旧操作结束后才执行现有退出保存，避免并发写文件；Task 1 的停止等待测试和 Task 2 的关闭顺序。
- 同一内容成功处理后不得重复 compaction；Task 1 的无新内容测试和 Task 2 的标记更新。

---

### Task 1: Idle 调度核心

**Files:**
- Modify: `.pi/extensions/auto-handoff/core.ts`
- Test: `.pi/extensions/auto-handoff/core.test.ts`

**Interfaces:**
- Consumes: 现有 `writeHandoffAtomically` 不受影响。
- Produces: `shouldAttemptIdleCompaction({ tokens, hasNewContent, isIdle, hasPendingMessages }): boolean`。
- Produces: `HandoffScheduler({ idleMs, shouldSave, save })`，公开 `enterIdle(): void`、`enterBusy(): void`、`stop(): Promise<void>`。

- [ ] **Step 1: 写入失败的条件判断测试**

将旧 `shouldAttemptTimedSave` 测试改为 `shouldAttemptIdleCompaction`：tokens 为 `null`、`undefined`、`99999`、`100000` 时均返回 `false`，`100001` 返回 `true`；分别断言 `hasNewContent: false`、`isIdle: false`、`hasPendingMessages: true` 会阻止执行。

- [ ] **Step 2: 写入失败的 idle 定时器测试**

用 `setTimeout` mock timer 替换旧固定周期测试，覆盖：
- `enterIdle()` 后 299,999 ms 不执行，再前进 1 ms 执行一次。
- `enterBusy()` 取消未到期计时，前进完整阈值仍不执行。
- 连续 `enterIdle()` 以最后一次调用重新计时。
- 到期 `shouldSave: false` 时不执行也不自动重建固定周期。
- `stop()` 取消未触发计时；若 `save` 已开始，则等待其完成。

- [ ] **Step 3: 运行测试确认 RED**

Run: `node --test .pi/extensions/auto-handoff/core.test.ts`
Expected: 因新接口不存在或旧固定周期间隔行为不符合预期而失败。

- [ ] **Step 4: 实现最小 idle 调度器**

在 `core.ts` 中重命名条件函数并加入两个布尔条件。将 `HandoffScheduler` 改为一次性 `setTimeout`：`enterIdle` 清理旧 timer 后重建，`enterBusy` 只清理 timer，`save` 保持单 flight，`stop` 清理 timer 并等待当前 Promise。删除 `start` 和 `saveNow`，不添加周期性调度。

- [ ] **Step 5: 运行核心测试确认 GREEN**

Run: `node --test .pi/extensions/auto-handoff/core.test.ts`
Expected: 核心测试全部通过；若 `index.ts` 尚未迁移，不影响此 Node 测试文件。

- [ ] **Step 6: 提交调度核心**

```bash
git add .pi/extensions/auto-handoff/core.ts .pi/extensions/auto-handoff/core.test.ts docs/superpowers/specs/2026-11-17-auto-handoff-idle-compaction-design.md docs/superpowers/plans/2026-11-17-auto-handoff-idle-compaction.md
git commit -m "refactor(交接): 改为空闲后启动交接计时"
```

### Task 2: Compaction 与 Handoff 集成

**Files:**
- Modify: `.pi/extensions/auto-handoff/core.ts`
- Modify: `.pi/extensions/auto-handoff/index.ts`
- Test: `.pi/extensions/auto-handoff/core.test.ts`

**Interfaces:**
- Consumes: Task 1 的 `HandoffScheduler.enterIdle/enterBusy/stop` 和 `shouldAttemptIdleCompaction`。
- Produces: `compactAndWriteHandoff({ cwd, compact }): Promise<string>`；`compact` 接收 `onComplete({ summary })` 与 `onError(error)` 回调，返回值为成功写入的文件路径。

- [ ] **Step 1: 写入失败的 compaction 结果测试**

使用临时目录和真实 `writeHandoffAtomically`：
- fake `compact` 调用 `onComplete({ summary: "# Compacted" })`，断言 Promise 解析到 Handoff 路径且文件内容一致。
- 预置 `# Keep me` 后让 fake 调用 `onError(new Error("failed"))`，断言 reject 且旧文件不变。
- 分别覆盖 `compact` 同步抛错和 `onComplete({ summary: "  " })`，断言 reject 且旧文件不变。

- [ ] **Step 2: 运行测试确认 RED**

Run: `node --test .pi/extensions/auto-handoff/core.test.ts`
Expected: 因 `compactAndWriteHandoff` 不存在而失败。

- [ ] **Step 3: 实现 compaction 写入桥接**

在 `core.ts` 新增 `compactAndWriteHandoff`：把回调式 compaction 包成 Promise；成功回调只复用结果的 `summary` 并调用现有原子写入；写文件失败、空摘要、同步抛错和 `onError` 均 reject，不在此函数中吞错或发 UI 通知。

- [ ] **Step 4: 将 idle 路径接入 Pi**

在 `index.ts`：
- scheduler 使用 `idleMs: TIMED_HANDOFF_INTERVAL_MS`，`save` 改为新的 idle compaction 函数。
- `shouldSave` 调用 Task 1 新谓词，并传入 `ctx.isIdle()`、`ctx.hasPendingMessages()`、tokens 和新内容标记。
- 注册 `agent_start` 调用 `enterBusy`，`agent_settled` 调用 `enterIdle`；`session_start` 不再启动固定周期。
- idle compaction 前保留有效对话检查；成功后更新 `lastSavedLeafId` 为当前节点、`hasSavedInSession = true` 并通知文件路径；失败通过已有错误通知报告。
- 会话切换和关闭的自定义摘要保存仍调用原 `saveAndTrack`，先 `await scheduler.stop()` 再保存，避免与 idle compaction 并发。

- [ ] **Step 5: 运行全部扩展测试与语法检查**

Run: `node --test .pi/extensions/auto-handoff/core.test.ts`
Expected: 全部测试通过，0 failures。

Run: `node --check .pi/extensions/auto-handoff/index.ts`
Expected: exit 0，无语法错误。

- [ ] **Step 6: 检查最终差异并提交**

```bash
git diff --check -- .pi/extensions/auto-handoff docs/superpowers/specs/2026-11-17-auto-handoff-idle-compaction-design.md docs/superpowers/plans/2026-11-17-auto-handoff-idle-compaction.md
git add .pi/extensions/auto-handoff docs/superpowers/specs/2026-11-17-auto-handoff-idle-compaction-design.md docs/superpowers/plans/2026-11-17-auto-handoff-idle-compaction.md
git commit -m "feat(交接): 空闲后压缩会话并保存摘要"
```

## 真机/实际会话验证

- `/reload` 后完成一次超过 100,000 tokens 的 Agent run，确认 5 分钟内无 compaction。
- 新 run 开始时确认旧 idle 计时取消。
- 持续 idle 5 分钟后确认发生一次 compaction，`HANDOFF.md` 与本次 compaction 摘要一致。
- 后续消息仍位于原会话，上下文显示为压缩后的用量；失败时旧 Handoff 不变。
