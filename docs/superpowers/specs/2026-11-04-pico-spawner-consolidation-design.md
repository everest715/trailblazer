# PICO 刷物脚本合并设计

## 目的

将当前三层刷物结构整理为两层，保留现有道具刷新节奏，同时让 SceneCapture 失败时的固定区域后备行为完整且一致。

## 已选方案

保留两个脚本：

- `SpawnPowerUps`：只负责随机选择道具、5 秒刷新间隔和最多 8 次刷新。
- `PicoSceneSpawnPositions`：负责 SceneCapture 选点、固定区域后备、实例化和刷物调试状态。

删除独立的 `PicoSpawnPositions` 脚本，并将其固定区域能力并入 `PicoSceneSpawnPositions`。

未选择全部并入 `SpawnPowerUps`，因为那会混合游戏刷新节奏与空间定位逻辑，也不利于其他刷物需求复用。

## 目标行为

1. `Game.StartGame()` 仍通过 `SpawnPowerUps.SpawnNow()` 启动刷新，间隔和次数不变。
2. 有有效 SceneCapture 地面区域时，仍按当前逻辑选点、统一批次高度并生成对象。
3. 没有 provider、没有有效区域、选点数量不足或发生异常时，均使用固定 `spawnBounds` 后备生成。
4. 有缓存的场景高度时，后备生成沿用缓存高度；否则使用固定区域内的高度。
5. 保留并发保护、调试状态以及 `SpawnOnStart` 行为。

## 场景与测试

- 将原 `PicoSpawnPositions.spawnBounds` 配置迁移到同对象上的 `PicoSceneSpawnPositions`，移除旧 fallback 组件和引用。
- 将固定区域测试迁移到 `PicoSceneSpawnPositionsTests`。
- 增加“无 provider”和“无有效区域”的后备测试，保留 SceneCapture 成功、异常后备和并发测试。
- 更新所有受类型删除或字段变化影响的测试、场景和预制体引用。

## 非目标

- 不修改 PowerUp 的 prefab、生成间隔、最大次数或随机选择方式。
- 不修改 SceneCapture provider 的获取方式。
- 不调整输入系统或其他 Polygon Arsenal 脚本。
