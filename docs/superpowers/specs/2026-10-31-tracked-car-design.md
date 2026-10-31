# MR 观察者 + Tracker 遥控小车设计

## 目标

用户可以自由移动并以 MR 观察真实环境和虚拟物体；头显只负责观察。安装 PICO 体感追踪器的小车是唯一玩家实体，由 Tracker 驱动并触发虚拟交互与碰撞特效。

## 方案 A（已选择）

最小重构：将现有 `PlayerCollider` 改为 `TrackedCar`，保留 `Game`、Player Tag、Rigidbody、Collider 和现有玩法。头显不再驱动玩家位置。以后可在此基础上拆分 `InteractionRouter` 升级到方案 B。

## 组成

1. `MotionTrackerDriver`：通过 `IPicoTrackerPoseSource` 获取位姿，在 FixedUpdate 用 Rigidbody.MovePosition/MoveRotation 驱动小车；无效位姿不移动。
2. `PicoMotionTrackerPoseSource`：请求一个 Tracker，保存 ID 和有效位姿，使用 SDK 6.1.1 的 `ByteDance.PICO.XR`。
3. `CarCollisionEffects`：在小车碰撞时于最近点生成独立特效；`Game` 在各 Tag 分支前调用，保留金币、Gate、扣分等逻辑。
4. 赛道过滤：`PicoSceneSpawnPositions.playAreaBounds` 同时过滤 Scene Capture 点，避免生成到观察者或赛道外。
5. 场景：`FollowCamera` 移除；`DrawTrail.player` 改为 TrackedCar；HUD 继续跟头显；Body Tracking 开启。

## 约束

- 不锁死头显渲染追踪，不关闭视频透视。
- 头显不参与玩家碰撞、轨迹、Gate 或生成位置。
- Tracker 丢失时不瞬移；安装偏移可在 Inspector 校准。
- 运动追踪不与全身动捕混用。
- 保留原玩法与后续方案 B 的迁移空间。

## 验收

- 有效位姿驱动小车，无效位姿保持不动。
- 头显移动不影响小车。
- 小车碰撞保留原交互并生成一次特效。
- Scene Capture 与回退生成都受赛道范围限制。
- 新增测试通过，APK 构建成功；Tracker 实机校准单独验证。
