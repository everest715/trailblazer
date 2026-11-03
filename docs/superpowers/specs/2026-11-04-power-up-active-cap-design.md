# PowerUp 活跃数量上限设计

## 目的

将 `SpawnPowerUps.maxCount` 从累计刷新次数改为场上活跃道具数量上限，并允许在 Unity 场景中配置。达到上限时暂停；道具销毁后按既有刷新间隔逐个补充。

## 已选行为

- `maxCount` 为公开字段，默认 `8`，场景可覆盖。
- `spawnInterval` 继续使用场景值，当前 `BikeScene` 为 `2` 秒。
- `PicoSceneSpawnPositions` 只在实际 `Instantiate` 成功后通知 `SpawnPowerUps`，异步或失败调用不虚增数量。
- `SpawnPowerUps` 保存活跃实例，并提供只读 `ActiveCount`。
- 活跃数量达到上限后取消定时刷新。
- 被 Coin、3Coin、TimeBonus、Multiplier 等碰撞逻辑销毁的道具通过 Unity 对象失效自动移除，不修改 `Game`。
- 数量低于上限后恢复定时刷新；每次间隔只生成一个，等待 `spawnInterval` 后开始补充，不立即补足。
- `SpawnNow()` 重复调用不会创建重复定时器；组件销毁时取消刷新并退订事件。

## 非目标

- 不修改道具 prefab、碰撞计分或销毁逻辑。
- 不改变固定区域和 SceneCapture 的选点方式。
- 不修改初始生成延迟；仍由现有 `spawnInterval` 控制。

## 测试

- 场景可设置的 `maxCount=2` 会暂停在两个活跃实例，继续等待也不会超出。
- 销毁一个实例后数量减少，间隔尚未到时不会补充，到达间隔后只补充一个并再次暂停。
- 生成失败不会增加 `ActiveCount`。
- 重复调用 `SpawnNow()` 不会加速或重复生成。
