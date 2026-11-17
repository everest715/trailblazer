# Auto-handoff Idle Compaction 设计

## 目标

降低长时间 idle 后继续原会话时的 KV Cache Miss 成本。Agent 完全空闲后开始计时，持续 idle 达到 5 分钟且当前上下文超过 100,000 tokens 时，直接 compaction 当前会话，并将本次 compaction 摘要写入项目根目录的 `HANDOFF.md`。

## 当前问题

- 现有调度器从 `session_start` 起每 5 分钟固定检查，不从进入 idle 起计时。
- 现有定时保存单独调用模型生成 `HANDOFF.md`，不压缩会话；后续请求仍发送原有有效历史。
- 长 idle 后继续原会话时，服务端缓存失效会导致完整有效历史重新计算和计费。

## 方案选择

### 采用：idle 后 compaction 并复用摘要

使用 `agent_settled` 作为进入 idle 的信号，以一次性 `setTimeout` 取代固定 `setInterval`。新 Agent run 开始时取消计时；到时后再次确认 Agent 仍 idle、没有待处理消息、存在新内容且上下文超过 100,000 tokens，再调用 `ctx.compact()`。在 `onComplete` 中直接取得 `CompactionResult.summary`，原子写入 `HANDOFF.md`。

该方案直接减少后续请求上下文，并避免定时 Handoff 与 compaction 各调用一次模型。

### 不采用：idle 后自动创建新会话

新会话只注入 `HANDOFF.md` 可以进一步缩短上下文，但会改变用户继续原会话的预期，并丢失原会话保留的近期消息。用户已明确选择 compaction，因此不切换会话。

### 不采用：只写 Handoff 文件

不能减少原会话下一次请求的输入，无法解决目标问题。

## 行为细节

- idle 指 Agent run 完全结束并触发 `agent_settled`，不是用户停止键盘输入。
- idle 时长沿用 5 分钟；token 条件沿用严格大于 100,000。
- 新 run 开始、会话重载或关闭时取消未触发的 idle 计时。
- 计时到期后再次检查 `ctx.isIdle()`、待处理消息、上下文 tokens 和新内容，避免过期计时器触发。
- compaction 成功后写入其原始 `summary`，更新已保存内容标记，防止同一内容重复处理。
- compaction 失败或摘要为空时保留旧 `HANDOFF.md`，并通过已有 UI 通知报告错误。
- 下一条消息继续使用原会话，但上下文已经是 compaction 摘要和保留的近期消息；不额外注入本次 `HANDOFF.md`。
- 退出及 `new`/`resume` 会话切换时的现有保存行为保持不变；手动 `/compact` 和 Pi 自身自动 compaction 不额外同步 `HANDOFF.md`。
- 保留现有新会话/空启动注入 `HANDOFF.md` 的逻辑。

## 非目标

- 不自动创建或切换会话。
- 不修改 Pi 内建 compaction 的摘要格式、保留 token 数或触发策略。
- 不处理短于等于 100,000 tokens 的会话。
- 不承诺具体服务商的 KV Cache TTL 或成本数值。

## 验证

使用 Node 测试模拟计时器和 compaction 回调，覆盖：从 settled 起计时、活动取消、到期条件检查、成功后写入摘要、失败保留旧文件及重复触发防护。运行 auto-handoff 全部测试和 TypeScript 编译检查。
