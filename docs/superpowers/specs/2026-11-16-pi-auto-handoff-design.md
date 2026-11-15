# Pi 自动 Handoff 设计

## 意图

在 Kimi Coding Plan 的 KV cache 失效后，避免恢复整段旧会话。让 pi 自动把当前会话压缩为项目根目录的 `HANDOFF.md`，并在后续新会话的第一条用户消息前注入。

## 目标

- `/new`、`/resume` 和正常退出时保存当前有效会话。
- 新会话或不含用户消息的启动会话自动注入一次 `HANDOFF.md`。
- 空会话、空摘要或生成失败不得覆盖旧文件。
- 恢复、重载和 fork 会话不得重复注入。
- 仅在保存时调用一次当前模型，不在每轮后生成摘要。

## 非目标

- 不控制 Coding Plan 的 KV cache 或 TTL。
- 不保证强制终止进程时完成保存。
- 不自动删除或提交 `HANDOFF.md`，不修改现有 Unity 资源。

## 方案

采用项目级扩展目录 `.pi/extensions/auto-handoff/`。`index.ts` 负责 pi 生命周期、模型摘要和上下文注入；`core.ts` 负责可独立测试的注入判断、有效会话判断和原子写入。

考虑过在每轮 `agent_settled` 后保存，但会增加摘要请求并产生额外用量，因此不采用。考虑过只保存原始对话，但恢复时仍会消耗大量 token，因此也不采用。

## 行为细节

- 保存触发为 `session_before_switch`（`new`/`resume`）以及 `session_shutdown`（仅 `quit`）；同一次切换只尝试一次。
- 使用当前模型和 `cacheRetention: "none"` 生成包含目标、进度、决策、相关文件、当前错误、剩余步骤及验证命令的简洁 Markdown。
- 使用同目录临时文件加重命名原子替换 `HANDOFF.md`；任何失败保留旧文件并通知用户。
- 仅在 `new`，或 `startup` 且当前分支没有用户消息时读取文件；在首个 `before_agent_start` 用户运行中返回持久自定义消息。
- 不修改系统提示词，避免额外破坏缓存前缀；文件不存在或为空时正常继续。

## 验证

Node 24 的 `node:test` 覆盖注入条件、有效会话识别、原子替换及空摘要保护。随后通过 pi 加载扩展做实际启动、保存和新会话注入检查。
