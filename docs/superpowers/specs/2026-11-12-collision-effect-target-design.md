# 被碰撞物体独立特效设计

## 目标

将碰撞特效从小车上的全局 `CarCollisionEffects` 移到被碰撞物体上。每个物体可在 Inspector 独立配置特效 Prefab，被 `Player` 小车碰撞后播放；未配置时不播放。

## 方案

- **已选择：物体自行监听碰撞。** 新增 `CollisionEffect`，与 Collider 挂在同一 GameObject，通过 `OnTriggerEnter` 和 `OnCollisionEnter` 响应小车碰撞，物体自身控制特效配置和播放。
- **不采用：Game 转发。** 保留 `Game.OnTriggerEnter` 查找并调用目标组件虽然改动少，但小车仍负责特效调度，不符合解耦目标。

## 实现约束

- `CollisionEffect` 提供 `effectPrefab`、`lifetime`、`effectScale`、`particleSpeedMultiplier`、`particleLifetimeMultiplier` 配置。
- 仅响应 `Player` Tag；空 Prefab、非 Player 碰撞均不生成特效。
- Trigger 在自身 Collider 距小车位置最近点生成一次特效，实体碰撞使用首个接触点；沿用 Prefab 旋转，并保留层级粒子缩放、速度、生命周期及自动销毁逻辑。
- 特效组件先于 `Game` 执行，避免 Gate 被玩法逻辑禁用后来不及播放。
- 删除 `Game.collisionEffects` 及其调用，移除旧 `CarCollisionEffects`，不改金币、积分、门和炸弹等玩法逻辑。
- 将原场景配置迁移到现有交互 Prefab：普通特效使用 `Explosion Fire`、缩放 `0.005`；Bomb 使用 `GrenadeExplosionRed`、缩放 `0.08`；生命周期 `2`、粒子速度倍率 `5`、粒子生命周期倍率 `0.5`。
- 不为其他碰撞体自动添加组件，不增加冷却或范围触发等额外功能。

## 验收

- Player 碰撞已配置物体时，在最近点生成一个对应 Prefab 特效。
- 非 Player 碰撞或未配置 Prefab 时不生成。
- 粒子参数和自动销毁行为保持有效。
- 现有交互 Prefab 使用各自配置，Game 不再持有全局特效引用。
- 相关 PlayMode 测试通过，Unity 工程无编译错误。
