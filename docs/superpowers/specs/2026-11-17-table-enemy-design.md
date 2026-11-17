# Bobouncing 桌面敌人方案 A 设计

## 目标

将 `Bobouncing.prefab` 接入当前 Tracker 小车玩法：敌人定时生成在桌面内，平时随机游荡；Tracker 进入警戒范围后，敌人试图远离 Tracker。被 Tracker 小车撞到后，在接触位置生成一次特效，随后销毁敌人。

## 已确认的行为

- 敌人生成在 PICO Scene Capture 提供的桌面区域内。
- 初始状态为随机游荡，不主动追逐 Tracker。
- Tracker 进入警戒范围后切换到逃跑状态，离开范围后恢复游荡。
- 敌人不能离开桌面，也不能因重力或碰撞掉下桌面。
- 碰撞只负责特效和销毁，本期不增加得分、扣分、生命值或玩家受伤逻辑。
- 方案 A 使用简单、可控的运动学移动，但保留以后迁移到原 `EnemyController` 物理移动方案的空间。

## 方案比较

### 方案 A：桌面区域约束脚本（本次选择）

新增项目自己的 `TableEnemy`，将状态判断、目标选择和移动执行分开。前期使用 Kinematic Rigidbody 与 `MovePosition`，行为可控，不需要运行时 NavMesh。

### 方案 B：改造原 `EnemyController`（后续可选）

复用资源包中的动画、声音和物理移动，但需要先解除对原 `PlayerController`、`PlayerHealth` 的依赖，并处理重力和桌边掉落。方案 A 的状态和目标选择逻辑可以继续使用，只替换移动执行层。

### 方案 C：运行时 NavMesh（不采用）

适合复杂障碍地图，但当前桌面移动不需要导航网格，还会增加 MR 桌面动态烘焙和运行时维护成本。

## 组成

### 1. 敌人 Prefab

新增项目资源 `Assets/Prefabs/BobouncingEnemy.prefab`，作为原 `Bobouncing.prefab` 的 Variant，不直接修改第三方 Prefab。

- 禁用或移除原 `EnemyController`，避免其玩家依赖和巡逻逻辑同时运行。
- 保留模型、Animator 和可复用的声音资源。
- Rigidbody 设置为 Kinematic，关闭重力。
- 保留非 Trigger 的 CapsuleCollider，与当前 `TrackedCar` 的 Player 碰撞体系兼容。
- 添加 `TableEnemy` 和 `CollisionEffect`。
- 尺寸、Collider 和特效比例按当前约 0.15 m 车长调整为 Inspector 可配置值。

### 2. `TableEnemy`：状态和目标选择

内部状态只包含 `Wandering` 与 `Fleeing`。

- `Wandering`：从桌面安全区域内选择随机目标，到达目标或超时后重新选择。
- `Fleeing`：优先选择远离 Tracker 的目标；正后方被桌边阻挡时，从侧向候选点中选择能继续拉开距离且位于桌面内的点。
- 使用进入半径和略大的退出半径，避免 Tracker 在边界附近移动时频繁切换状态。
- Tracker 引用为空或位姿无效时保持游荡，不根据过期的 Tracker 位姿逃跑。
- 所有目标点都限制在桌面多边形内，并按敌人半径预留桌边距离。
- 找不到有效逃跑点时允许短暂停留或转向，但不能跨出桌面。

公开配置包括 Tracker、游荡速度、逃跑速度、旋转速度、进入/退出警戒半径、目标到达距离、目标超时和桌边安全距离。

### 3. 独立的移动执行

`TableEnemy` 的移动执行集中在单独的方法中：

- 只处理 XZ 平面移动，Y 固定在生成高度。
- 使用 `Rigidbody.MovePosition` 移动，使用旋转插值朝向运动方向。
- 每个物理步长都校验目标位置，不能把单步长位移直接推出桌面。
- Animator 参数驱动与运动计算分开；缺少 Animator 参数时不能影响移动。

迁移到方案 B 时，保留状态切换、逃跑目标和桌面约束，把该方法替换为原控制器的速度或力驱动，再接入其动画、声音和攻击表现。本期不提前引入移动接口或额外抽象层。

### 4. 桌面生成与定时刷新

复用 `PicoSceneCaptureFloorAreaProvider` 的 `Table` 查询和 `PicoSceneSpawnPositions` 的随机生成、Tracker 避让与固定区域回退能力。

- 新增 `SpawnEnemies`，参照 `SpawnPowerUps` 维护存活列表、刷新间隔和最大数量。
- 游戏开始后启动刷新，游戏对象停用时停止刷新；敌人销毁后，后续刷新周期可以补充数量。
- 生成时把本次使用的 `PicoFloorArea` 和 Tracker 传给 `TableEnemy`。
- 为保持现有 `Spawned` 事件兼容，生成器需要以非破坏方式补充区域上下文，不能改变现有道具生成器的订阅签名。
- Scene Capture 不可用时，固定区域回退也要构造对应的矩形活动范围，保证敌人仍受边界约束。

### 5. 碰撞特效与销毁

扩展现有 `CollisionEffect`，默认保持当前物体行为不变。

- 增加可选的「只触发一次」和「命中后销毁自身」配置。
- 敌人 Variant 同时启用这两项。
- 仅响应 Tag 为 `Player` 的 Tracker 小车。
- 在碰撞接触点实例化独立特效，由现有 `lifetime` 清理特效。
- 先创建特效，再销毁敌人；同一物理帧的重复碰撞不能生成多份特效。

## 约束与非目标

- 不修改第三方 `Bobouncing.prefab` 和 `EnemyController.cs`。
- 不引入 NavMesh、障碍规避、攻击、发射物或伤害系统。
- 不改变现有金币、道具、Gate 和轨迹玩法。
- 保持 Unity 2022.3、PICO OS 6 和 PICO XR 技术栈。
- Editor 测试不能替代 Tracker 与 Scene Capture 的真机验证。

## 验收标准

1. 敌人从桌面有效点生成，生成时避开 Tracker，且不超过最大存活数量。
2. Tracker 在警戒范围外时，敌人持续在桌面内随机游荡。
3. Tracker 进入范围后，敌人优先远离 Tracker；离开退出范围后恢复游荡。
4. 游荡和逃跑均不会越过桌面边界，敌人不会坠落。
5. Tracker 位姿无效时，敌人不根据过期位置进入逃跑状态。
6. Tracker 撞到敌人后只生成一次特效，敌人消失，特效能独立播放并被清理。
7. 现有道具生成与碰撞行为保持不变，相关 PlayMode 测试通过。
8. 真机上的桌面识别、实际尺寸、速度和碰撞手感后续单独验证。

## 留给实现与调校的细节

- 敌人 Variant 的具体缩放、移动速度、警戒半径、刷新间隔和最大数量。
- 选用现有特效资源及其缩放、生命周期。
- Bobouncing Animator 中游荡与逃跑对应的具体参数和状态。
- 逃跑候选点数量与采样策略，在保证简单实现的同时避免贴边抖动。
