# Pi Timed Handoff Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 每 5 分钟检查一次，仅在上下文超过 100,000 tokens 且会话有新内容时自动更新 `HANDOFF.md`。

**Architecture:** `core.ts` 提供无 pi 依赖的阈值判断和可调度的保存锁；`index.ts` 用会话叶节点检测变化，并把定时保存与现有切换/退出保存接入同一个调度器。定时器在 session 生命周期内启动和清理。

**Tech Stack:** TypeScript、Node 24 `node:test` fake timers、pi 1.5.2 Extension API。

**Spec:** `docs/superpowers/specs/2026-11-16-pi-timed-handoff-design.md`

## Global Constraints

- 生产间隔固定 `300000ms`，阈值严格为 `tokens > 100000`；阈值仅限制定时保存。
- 无新内容或 context 使用量不可读时不调用模型；生成失败不覆盖旧文件。
- `/new`、`/resume`、`quit` 保持原保存逻辑，不受阈值限制。
- 不增加 npm 依赖，不修改 Unity 资源或系统自动提交 `HANDOFF.md`。

## Review Focus

- 恰好 100,000 tokens：跳过；由阈值边界测试验证。
- 使用量未知或没有新叶节点：跳过；由阈值输入测试验证。
- 慢保存与后续 tick/quit 重叠：共享同一个 Promise；由防重入测试验证。
- 保存期间新增消息：只记录摘要开始时的叶节点，下轮仍应保存；由 Task 2 接入检查和冒烟验证。
- reload/new/resume/fork/quit：定时器必须停止且不得重复启动；由 scheduler stop/start 测试及 Task 2 生命周期验证。

---

### Task 1: 阈值与定时调度器

**Files:**
- Modify: `.pi/extensions/auto-handoff/core.ts`
- Test: `.pi/extensions/auto-handoff/core.test.ts`

**Interfaces:**
- Produces: `TIMED_HANDOFF_INTERVAL_MS = 300000`、`TIMED_HANDOFF_TOKEN_THRESHOLD = 100000`。
- Produces: `shouldAttemptTimedSave(options: { tokens: number | null | undefined; hasNewContent: boolean }): boolean`。
- Produces: `new HandoffScheduler(options: { intervalMs: number; shouldSave: () => boolean; save: () => Promise<void> })`，方法 `start(): void`、`saveNow(): Promise<void>`、`stop(): Promise<void>`。

- [ ] **Step 1: 编写失败测试**

在现有测试中增加：

- `timed save requires new content and more than 100k tokens`：`null`、`undefined`、99999、100000 均不触发；100001 且有新内容触发；任何 token 下无新内容均跳过。
- `scheduler ticks every five minutes and stops`：fake timers 在 299999ms 时无调用、300000ms 时调用一次；stop 后继续 tick 不增加。
- `repeated start creates only one timer`：连续 start 两次，单个周期只调用一次。
- `saveNow coalesces concurrent saves and stop awaits completion`：用一个手动 resolve 的 Promise 同时调用两次 `saveNow()`，断言保存只执行一次；stop 等待 resolve，完成后无后续 tick。

- [ ] **Step 2: 运行测试并确认 RED**

Run: `node --test .pi/extensions/auto-handoff/core.test.ts`

Expected: 因新导出不存在而 FAIL，既有 10 项测试不受影响。

- [ ] **Step 3: 实现核心逻辑**

实现上述精确接口。`start()` 幂等并调用 timer `unref()`；tick 先执行 `shouldSave()`，再调用 `saveNow()`。`saveNow()` 共享 pending Promise 并在完成后清除；`stop()` 清除 interval 并等待 pending 完成。

- [ ] **Step 4: 运行测试并确认 GREEN**

Run: `node --test .pi/extensions/auto-handoff/core.test.ts`

Expected: 全部测试 PASS。

- [ ] **Step 5: 提交核心调度**

```bash
git add -- .pi/extensions/auto-handoff/core.ts .pi/extensions/auto-handoff/core.test.ts
git commit -m "feat(pi): add timed handoff scheduler"
```

### Task 2: 接入 pi 会话与验证

**Files:**
- Modify: `.pi/extensions/auto-handoff/index.ts`

**Interfaces:**
- Consumes: Task 1 的常量、阈值判断和 `HandoffScheduler`。
- Produces: 每个会话独立的 5 分钟调度器；现有注入和生命周期保存行为保持不变。

- [ ] **Step 1: 接入变更跟踪和调度生命周期**

让 `saveHandoff()` / `trySaveHandoff()` 返回成功摘要时的会话叶节点（无保存或失败返回 `undefined`）。在 `session_start`：

- 停止旧调度器，初始化 `lastSavedLeafId` 为当前叶节点。
- 创建调度器；`shouldSave` 读取 `ctx.getContextUsage()?.tokens`，并比较当前叶节点与 `lastSavedLeafId`。
- 只有成功写入才更新 `lastSavedLeafId`，且使用摘要开始时的叶节点，避免吞掉生成期间的新消息。

`session_before_switch` 和符合条件的 `session_shutdown` 通过 `scheduler.saveNow()` 保存，绕过阈值但共享保存锁；所有 shutdown 原因最终调用 `stop()`。

- [ ] **Step 2: 运行静态和单元验证**

```bash
node --check .pi/extensions/auto-handoff/index.ts
node --check .pi/extensions/auto-handoff/core.ts
node --test .pi/extensions/auto-handoff/core.test.ts
```

Expected: 两个语法检查退出 0，全部 Node 测试 PASS。

- [ ] **Step 3: 真实 pi 冒烟测试**

在 `.superpowers` 的临时副本中加载扩展，仅把副本的间隔改为 100ms、阈值改为 0，生产文件保持 300000/100000。运行独立 pi 会话并保持至少两个 tick，确认：

- 定时路径实际生成非空 `HANDOFF.md`。
- `/reload` 后没有重复定时器或扩展错误。
- 正常退出没有重复摘要或空文件覆盖。

- [ ] **Step 4: 审查并提交**

检查 diff 仅包含 `index.ts` 的预期接入，且生产常量仍为 300000/100000。

```bash
git add -- .pi/extensions/auto-handoff/index.ts
git commit -m "feat(pi): schedule handoff above 100k tokens"
```

完成后在当前 pi 执行 `/reload` 启用新逻辑；真实 5 分钟/100k 触发在后续会话达到条件时观察。
