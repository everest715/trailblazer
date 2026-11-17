# 虚拟桌面回退设计

## 目标

当 PICO Scene Capture 无法提供 Table 桌面时，由 `PicoSceneCaptureFloorAreaProvider` 返回同一个虚拟桌面，使 Gate、围墙、敌人和道具继续使用一致的区域与高度，而不是各自采用不同回退逻辑。

## 已选方案

- **方案 A（本次选择）**：Provider 统一返回虚拟 `PicoFloorArea`。所有现有消费者无需知道具体矩形采样方式，真实桌面出现后可自然替换。
- **方案 B（不采用）**：只在围墙生成器中虚拟桌面。围墙可用但 Gate、道具仍走其他回退区域，无法解决对齐问题。

## 配置与默认位置

Provider 增加 `useVirtualTableFallback = true`、`virtualTableCenter = (0, 0, 1.5)`、`virtualTableSize = (1.5, 1.5)` 和 `virtualTableYaw = 0`。这些值沿用当前道具固定区域的水平中心与尺寸，虚拟桌面高度固定为 `virtualTableCenter.y = 0`，不跟随头显移动，避免观察时围墙或赛道跳动。

仅在 `surfaceLabel == Table` 且开关启用时生效；Floor 查询保持原行为。尺寸必须是有限正数，无效配置不生成区域并记录 Warning。

## 触发与恢复

以下情况返回包含一个虚拟矩形的列表：Scene Capture Provider 启动或查询失败、场景识别调用失败、识别完成后仍没有桌面，以及后续锚点更新变为空。失败原因保留在诊断信息中，并记录 Warning；启用虚拟回退后不再把异常抛给每个消费者。

真实 Table 区域查询成功时始终优先使用真实结果。后续 `AreasUpdated` 收到真实区域时替换虚拟结果，Gate 和围墙沿用现有更新机制重新对齐；虚拟结果也触发相同事件。

## 诊断与调试显示

虚拟结果设置 `DebugFloorAreaCount = 1`、`DebugGroundHeight = virtualTableCenter.y`、`DebugGroundHeightMethod = "Virtual table fallback"`。`DebugQueryStatus` 和 `DebugCaptureDetails` 记录回退原因、中心、尺寸、Y 偏航及四个世界顶点，调试网格显示同一个矩形。道具生成器若发现 Provider 实现了 `ISceneCaptureDebugInfo`，同步显示该高度来源，避免把虚拟桌面误报为真实 Scene Capture。

## 验证与非目标

使用不依赖 PICO 原生接口的矩形构造测试验证中心、尺寸、高度、旋转、随机点包含关系和无效尺寸。Provider 的触发逻辑通过可测试的回退入口覆盖空结果、失败、真实结果优先和事件替换；Editor 无法验证真实 SDK 启动失败，真机仍需验证。

本期不改变正常真实桌面标定、Floor 行为、围墙排列算法、物体尺寸或游戏得分。保留现有场景和 Coin Prefab 未提交改动。

## 留给实现的细节

为减少原生 SDK 依赖，区域构造可放在 `PicoFloorArea` 或独立静态 helper 中；Provider 的回退入口保持私有，仅暴露正常公共 Provider API 与诊断属性。
