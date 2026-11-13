# 道具碰撞音效独立配置设计

## 目标

将 `Game` 统一播放的道具碰撞音效迁移到各道具的 `CollisionEffect.effectSound`，让 Prefab 自己决定碰撞音效，避免小车控制器集中持有道具音频配置。

## 已选方案

- **采用：复用现有 `CollisionEffect.effectSound`，每次碰撞播放一次。** 不为 `3Coin` 增加重复播放参数；用户已确认 `3Coin` 迁移后只播放一声。
- **不采用：保留 `Game` 统一播放。** 这会继续产生重复音效，也无法让 Prefab 独立配置。
- **不采用：新增音效调度组件。** 当前需求只需单个 AudioClip，无需扩大组件职责。

## 实施约束

- `Coin.prefab`、`Coin 2.prefab`、`3Coin.prefab` 使用 `Assets/Sounds/Coin.mp3`。
- `TimeBonus.prefab` 使用 `Assets/Sounds/TimeBonus.mp3`；`Multiplier.prefab` 使用 `Assets/Sounds/Multiplier.wav`。
- `Bomb.prefab`、`Bomb 1.prefab` 使用 `Assets/Polygon Arsenal/Sound/Wav/polygon_explosion_rocket.wav`。
- 从 `Game` 删除 Coin、3Coin、TimeBonus、Multiplier 开始音和 Bomb 的统一播放及不再使用的字段；保留 `MultiplierEnd` 的延迟结束音、Gate、起步和错误音逻辑。
- `effectSound` 应独立于 `effectPrefab` 播放；未配置粒子 Prefab 但已配置音效时仍能发声。
- 清理 `BikeScene` 中已删除字段的序列化引用，不改其他场景修改。
- 不改变积分、计时、销毁、晕影或其他玩法行为；不处理已注释的 Boost 逻辑。

## 验收

- 上述 7 个 Prefab 的 `CollisionEffect.effectSound` 引用正确。
- Player 碰撞时由 `CollisionEffect` 播放一次对应音效，`Game` 不再重复播放。
- `3Coin` 只播放一声；Multiplier 结束音仍按原逻辑播放。
- 空 `effectPrefab` 不阻止音效播放；相关 PlayMode 测试及完整 PlayMode 回归通过。
