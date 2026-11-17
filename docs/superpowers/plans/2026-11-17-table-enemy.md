# Bobouncing 桌面敌人 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在桌面定时生成会随机游荡、躲避 Tracker 的 Bobouncing，并在被 Tracker 小车撞到后播放一次特效并销毁。

**Architecture:** 使用 `PicoFloorArea` 提供桌面多边形与安全区域采样，`TableEnemy` 分离状态/目标选择和 Kinematic Rigidbody 移动。`PicoSceneSpawnPositions` 以新增事件传递生成区域，`SpawnEnemies` 管理刷新；`CollisionEffect` 独立处理一次性特效和销毁。

**Tech Stack:** Unity 2022.3.62f3、C#、PICO XR、NUnit PlayMode Tests、Unity Prefab Variant 与 Scene YAML。

**Spec:** `docs/superpowers/specs/2026-11-17-table-enemy-design.md`

## Global Constraints

- 保持 Unity 2022.3、PICO OS 6 和 PICO XR，不引入 Quest 兼容层、NavMesh 或新依赖。
- 不修改第三方 `Assets/Super Mario Style/Prefabs/Bobouncing.prefab` 和 `EnemyController.cs`。
- 只响应 Tag 为 `Player` 的 Tracker 小车，不增加得分、伤害、攻击或生命值逻辑。
- 保留现有 `PicoSceneSpawnPositions.Spawned`、道具生成、Gate、金币和轨迹行为。
- 敌人只进行 XZ 移动，保持生成高度；无有效安全点时不越界、不产生 NaN。
- 遵循项目公共可调字段、`_camelCase` 私有字段和大括号独占一行的代码风格。
- Editor 测试通过不等于 PICO 真机验证通过，最终报告必须明确区分。

## Review Focus

- Tracker 位姿由有效变为无效：敌人必须恢复游荡，不能使用最后位置继续逃跑；由 Task 1 和 Task 3 的失效位姿测试覆盖。
- 桌面为空、退化或过窄：随机采样必须失败，敌人保持有限坐标且不越界；由 Task 2 和 Task 3 的退化/过窄区域测试覆盖。
- Tracker 与敌人 XZ 坐标重合：逃跑方向必须有稳定回退，不能归一化零向量；由 Task 3 的重合位置测试覆盖。
- 同一帧重复碰撞或特效 Prefab 为空：敌人最多生成一份特效，配置销毁时仍会销毁；由 Task 4 的重复触发和空 Prefab 测试覆盖。
- 固定区域回退与旧生成事件：回退区域必须使用世界坐标和实际生成高度，旧订阅者仍收到原事件；由 Task 5 的回退区域和双事件测试覆盖。

---

### Task 1: 暴露 Tracker 位姿有效性

**Files:**
- Modify: `Assets/Scripts/Pico/MotionTrackerDriver.cs`
- Test: `Assets/Tests/PlayMode/MotionTrackerDriverTests.cs`

**Interfaces:**
- Consumes: 现有 `IPicoTrackerPoseSource.TryGetPose(out Vector3, out Quaternion)`。
- Produces: `public bool DebugPoseValid { get; private set; }`，供 `TableEnemy` 判断 Tracker 是否可用。

- [ ] **Step 1: 写失败测试**

在 `MotionTrackerDriverTests` 添加 `Update_PoseValidityChanges_ExposesCurrentState`：先令 `FakePoseSource.HasPose = true` 并调用 `driver.Update()`，断言 `driver.DebugPoseValid` 为 `true`；再改为 `false` 并调用 `driver.Update()`，断言属性为 `false`，且位置不再更新。

```csharp
_source.HasPose = true;
driver.Update();
Assert.IsTrue(driver.DebugPoseValid);
_source.HasPose = false;
driver.Update();
Assert.IsFalse(driver.DebugPoseValid);
```

- [ ] **Step 2: 运行并确认 RED**

```bash
mkdir -p .superpowers
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "MotionTrackerDriverTests" -testResults ".superpowers/table-enemy-tracker-red.xml" -logFile ".superpowers/table-enemy-tracker-red.log"
```

预期：因 `DebugPoseValid` 不存在而编译失败。

- [ ] **Step 3: 实现最小改动**

新增属性，并在 `Update()` 和 `FixedUpdate()` 每次调用 `TryGetPose` 后立即把返回值赋给 `DebugPoseValid`；无效时继续沿用现有提前返回，不移动 Rigidbody。

- [ ] **Step 4: 重跑并确认 GREEN**

使用 Step 2 命令，将输出文件名改为 `table-enemy-tracker-green`。预期：`MotionTrackerDriverTests` 全部通过。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Pico/MotionTrackerDriver.cs Assets/Tests/PlayMode/MotionTrackerDriverTests.cs docs/superpowers/specs/2026-11-17-table-enemy-design.md docs/superpowers/plans/2026-11-17-table-enemy.md
git commit -m "feat(敌人): 暴露Tracker位姿有效状态"
```

### Task 2: 桌面安全区域采样

**Files:**
- Modify: `Assets/Scripts/Pico/PicoFloorArea.cs`
- Test: `Assets/Tests/PlayMode/PicoFloorAreaTests.cs`

**Interfaces:**
- Produces: `public bool ContainsXZ(Vector3 point, float edgeMargin)`。
- Produces: `public bool TryGetRandomPoint(float edgeMargin, out Vector3 point)`。
- Consumes: 现有无 margin 的 `ContainsXZ` 和 `TryGetRandomPoint`，其签名与默认行为保持不变。

- [ ] **Step 1: 写失败测试**

在 `PicoFloorAreaTests` 添加：
- `ContainsXZ_PositiveMargin_RejectsNearEdgeButKeepsCenter`：对 `[-1,1] × [-1,1]` 矩形，margin 为 `0.2`，中心通过、`x = 0.9` 不通过。
- `TryGetRandomPoint_PositiveMargin_ReturnsOnlySafePoints`：采样 20 次，断言所有点满足 `ContainsXZ(point, 0.2f)` 且 Y 保持桌面高度。
- `TryGetRandomPoint_MarginExceedsTable_ReturnsFalse`：对宽 `0.2` 的矩形使用 margin `0.2`，断言返回 `false`。
- `ContainsXZ_DegenerateEdgeAndZeroMargin_DoesNotReturnNaN`：包含重复顶点的多边形不抛异常，采样失败时 `out point` 为有限值。

- [ ] **Step 2: 运行并确认 RED**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "PicoFloorAreaTests" -testResults ".superpowers/table-enemy-area-red.xml" -logFile ".superpowers/table-enemy-area-red.log"
```

预期：因两个新重载不存在而编译失败。

- [ ] **Step 3: 实现安全区域 API**

无 margin 方法委托给新重载并传 `0f`。正 margin 除原射线法包含判断外，还要求点到每条 XZ 边的距离至少为 margin；零长度边退化为点到点的距离。`TryGetRandomPoint` 仅在安全包含判断通过时返回 `SurfaceHeight` 处的点，失败时返回 `Vector3.zero`。

- [ ] **Step 4: 重跑并确认 GREEN**

使用 Step 2 命令，将输出文件名改为 `table-enemy-area-green`。预期：`PicoFloorAreaTests` 全部通过，原有区域测试无回归。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Pico/PicoFloorArea.cs Assets/Tests/PlayMode/PicoFloorAreaTests.cs
git commit -m "feat(敌人): 支持桌面安全区域采样"
```

### Task 3: 游荡与逃跑状态及可迁移移动层

**Files:**
- Create: `Assets/Scripts/Pico/TableEnemy.cs` 及 `.meta`
- Test: `Assets/Tests/PlayMode/TableEnemyTests.cs` 及 `.meta`

**Interfaces:**
- Consumes: Task 1 的 `MotionTrackerDriver.DebugPoseValid` 和 Task 2 的安全区域 API。
- Produces: `TableEnemy` 内嵌 `public enum State { Wandering, Fleeing }`。
- Produces: `public State CurrentState { get; private set; }`、`public PicoFloorArea Area { get; private set; }`。
- Produces: `public void Configure(PicoFloorArea area, MotionTrackerDriver tracker)`。
- Produces: 公共字段 `tracker`、`wanderSpeed = 0.08f`、`fleeSpeed = 0.14f`、`rotationSpeed = 360f`、`detectionRadius = 0.3f`、`exitDetectionRadius = 0.4f`、`waypointMinDist = 0.02f`、`waypointTimeout = 3f`、`edgeMargin = 0.1f`、`fleeTargetDistance = 0.25f`、`fleeRepathInterval = 0.25f`。

- [ ] **Step 1: 写失败测试**

用带 Kinematic Rigidbody、CapsuleCollider 和 `TableEnemy` 的敌人，以及带 `MotionTrackerDriver`/`FakePoseSource` 的 Tracker 建立测试。添加：
- `FixedUpdate_TrackerOutsideDetection_WandersInsideSafeArea`：Tracker 在范围外，状态为 `Wandering`，多个物理帧后位置仍满足 `Area.ContainsXZ(position, edgeMargin)`。
- `FixedUpdate_TrackerEnterAndExitRadius_UsesHysteresis`：进入 `0.3` 后切到 `Fleeing`，位于 `0.3–0.4` 之间保持逃跑，超过 `0.4` 恢复游荡。
- `FixedUpdate_InvalidTrackerPose_ReturnsToWandering`：先近距离有效触发逃跑，再令位姿无效，断言恢复游荡。
- `FixedUpdate_FleeingOnOpenTable_IncreasesTrackerDistance`：安全桌面上逃跑后 XZ 距离增大，Y 保持生成值。
- `FixedUpdate_NarrowTableWithoutSafeTarget_StaysFiniteAndInside`：没有有效逃跑候选点时不移动出界，所有坐标有限。
- `FixedUpdate_CoincidentTracker_UsesFiniteFallbackDirection`：XZ 重合后位置与旋转均为有限值。
- `Configure_EmptyArea_DoesNotMove`：默认/空 `PicoFloorArea` 下不改变位置。
- `FixedUpdate_WithoutAnimator_StillMoves`：未挂 Animator 时移动不抛异常。

- [ ] **Step 2: 运行并确认 RED**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "TableEnemyTests" -testResults ".superpowers/table-enemy-movement-red.xml" -logFile ".superpowers/table-enemy-movement-red.log"
```

预期：因 `TableEnemy` 不存在而编译失败。

- [ ] **Step 3: 实现状态和目标选择**

`Configure` 保存区域、Tracker 和当前生成高度并清空旧目标；Awake 强制 Rigidbody 为 Kinematic 且关闭重力，Tracker 为空时查找场景中的 `MotionTrackerDriver`。FixedUpdate 仅用 XZ 距离与有效位姿切换状态，进入/退出状态使用不同半径，切换时清空目标。

游荡目标通过 `Area.TryGetRandomPoint(edgeMargin, out point)` 获取，到达或超时后重选。逃跑每 `fleeRepathInterval` 或目标失效时，从 12 个等角候选方向中选择安全点，评分固定为 `2 * Dot(direction, away) + (candidateDistance - currentDistance) / fleeTargetDistance`，选择最高正分；正后方无效时允许侧向，禁止零分或负分方向。Tracker 重合时使用 Tracker 水平后方，再回退到敌人当前前方。

- [ ] **Step 4: 实现独立移动执行与动画适配**

把移动集中在 `MoveToTarget(Vector3 target)`：目标 Y 重写为生成高度，使用 `MoveTowards` 和速度 × `Time.fixedDeltaTime` 计算下一步；下一步不满足安全区域时清空目标并停在原处，否则调用 `Rigidbody.MovePosition`，用 `Quaternion.RotateTowards` 平滑朝向。

Animator 更新放在独立方法中，仅当参数存在时设置 Bool `Move` 和 Integer `Speed`（游荡 `1`、逃跑 `2`），不得让动画缺失影响移动。

- [ ] **Step 5: 重跑并确认 GREEN**

使用 Step 2 命令，将输出文件名改为 `table-enemy-movement-green`。预期：`TableEnemyTests` 全部通过。

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/Pico/TableEnemy.cs Assets/Scripts/Pico/TableEnemy.cs.meta Assets/Tests/PlayMode/TableEnemyTests.cs Assets/Tests/PlayMode/TableEnemyTests.cs.meta
git commit -m "feat(敌人): 添加桌面游荡与逃跑行为"
```

### Task 4: 一次性碰撞特效与销毁

**Files:**
- Modify: `Assets/Scripts/Pico/CollisionEffect.cs`
- Test: `Assets/Tests/PlayMode/CollisionEffectTests.cs`

**Interfaces:**
- Produces: 公共字段 `bool triggerOnce`、`bool destroyOnHit`，默认均为 `false`。
- Consumes: 现有 Player 筛选、接触点特效、音效、掉币与生命周期行为。

- [ ] **Step 1: 写失败测试**

在 `CollisionEffectTests` 添加：
- `OnTriggerEnter_TriggerOnce_RepeatedPlayerMessagesCreateOneEffect`：触发两次，断言特效实例数为 `1`。
- `OnTriggerEnter_DefaultSettings_RepeatedPlayerMessagesKeepExistingBehavior`：默认配置触发两次，断言实例数为 `2`。
- `OnTriggerEnter_DestroyOnHit_CreatesEffectBeforeDestroyingTarget`：等待一帧，断言目标为空且独立特效仍存在。
- `OnTriggerEnter_DestroyOnHitWithNullEffect_StillDestroysTarget`：特效 Prefab 为空也能销毁，且不抛异常。
- `OnTriggerEnter_NonPlayerWithDestroyOnHit_KeepsTarget`：非 Player 不销毁。

- [ ] **Step 2: 运行并确认 RED**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "CollisionEffectTests" -testResults ".superpowers/table-enemy-collision-red.xml" -logFile ".superpowers/table-enemy-collision-red.log"
```

预期：因新字段不存在而编译失败。

- [ ] **Step 3: 实现最小改动**

在 Player 消息进入 `Play` 前检查 `(triggerOnce || destroyOnHit) && _hasPlayed`。`Play` 开始时记录 `_hasPlayed = true`，保留原音效、掉币和特效顺序，最后仅在 `destroyOnHit` 时 `Destroy(gameObject)`；空特效 Prefab 的提前返回也要走销毁路径。

- [ ] **Step 4: 重跑并确认 GREEN**

使用 Step 2 命令，将输出文件名改为 `table-enemy-collision-green`。预期：`CollisionEffectTests` 全部通过。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Pico/CollisionEffect.cs Assets/Tests/PlayMode/CollisionEffectTests.cs
git commit -m "feat(敌人): 支持碰撞后单次特效与销毁"
```

### Task 5: 传递生成区域并定时刷新敌人

**Files:**
- Modify: `Assets/Scripts/Pico/PicoSceneSpawnPositions.cs`
- Create: `Assets/Scripts/Pico/SpawnEnemies.cs` 及 `.meta`
- Test: `Assets/Tests/PlayMode/PicoSceneSpawnPositionsTests.cs`
- Test: `Assets/Tests/PlayMode/SpawnEnemiesTests.cs` 及 `.meta`

**Interfaces:**
- Consumes: Task 2 的安全采样和 Task 3 的 `TableEnemy.Configure(PicoFloorArea, MotionTrackerDriver)`。
- Produces: `PicoSceneSpawnPositions.surfaceEdgeMargin = 0f`，默认不改变现有生成位置分布。
- Produces: `event Action<GameObject, PicoFloorArea> SpawnedInArea`；原 `event Action<GameObject> Spawned` 保留。
- Produces: `SpawnEnemies.enemyPrefab`、`spawnInterval = 8f`、`maxCount = 3`、`tracker`、`ActiveCount` 和 `SpawnNow()`。

- [ ] **Step 1: 写生成上下文失败测试**

在 `PicoSceneSpawnPositionsTests` 添加：
- `StartSpawn_SceneCapture_RaisesBothEventsWithSameInstanceAndSourceArea`：两个事件收到同一实例，区域等于 FakeProvider 返回区域，原事件仍触发一次。
- `StartSpawn_Fallback_RaisesWorldSpaceAreaAtActualSpawnHeight`：移动并旋转生成器后回退，断言事件区域包含生成点、4 个顶点 Y 等于实例 Y，且区域位于变换后的固定范围。
- `StartSpawn_PositiveEdgeMargin_KeepsSpawnAwayFromEdges`：Scene Capture 和固定区域各采样多次，均满足对应安全边界。

- [ ] **Step 2: 写敌人刷新失败测试**

在 `SpawnEnemiesTests` 使用带 `TableEnemy` 的测试 Prefab 和 FakeProvider，添加：
- `SpawnNow_ConfiguresEnemyWithSourceAreaAndTracker`：实例的 `Area` 等于来源区域，`tracker` 为指定 Driver。
- `SpawnNow_StopsAtActiveCap`：interval `0.1`、cap `2`，等待后数量稳定为 `2`。
- `SpawnNow_DestroyedEnemyResumesAfterInterval`：销毁一个实例后，下一生成周期补充到 cap。
- `SpawnNow_RepeatedCallsDoNotDuplicateSpawning`：重复调用不会启动多个计时器。

- [ ] **Step 3: 运行并确认 RED**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "PicoSceneSpawnPositionsTests;SpawnEnemiesTests" -testResults ".superpowers/table-enemy-spawn-red.xml" -logFile ".superpowers/table-enemy-spawn-red.log"
```

预期：因新事件、字段或 `SpawnEnemies` 不存在而编译失败。

- [ ] **Step 4: 扩展生成上下文**

在 Scene Capture 路径中保留每个点对应的区域；`SpawnAt(Vector3 worldPosition, PicoFloorArea area)` 实例化后先调用原 `Spawned`，再调用 `SpawnedInArea`。固定区域每次生成时构造世界空间矩形，顶点使用变换后的 XZ 边界和该实例的实际 Y。`surfaceEdgeMargin` 同时约束 Scene Capture 采样与固定区域 XZ 随机范围；范围过窄时生成失败，不强行放置到边缘。

- [ ] **Step 5: 实现 `SpawnEnemies`**

复用 `SpawnPowerUps` 的存活列表、InvokeRepeating、暂停/恢复和清理模式，但只使用 `enemyPrefab`，每次将 `SpawnObject`、数量和 `surfaceEdgeMargin` 设置为 Prefab 上 `TableEnemy.edgeMargin`。订阅 `SpawnedInArea`，调用 `Configure(area, tracker)` 后加入存活列表；Tracker 为空时查找场景 Driver。Prefab 缺少 `TableEnemy` 时记录错误并销毁无效实例，不计入存活数。

- [ ] **Step 6: 重跑并确认 GREEN**

使用 Step 3 命令，将输出文件名改为 `table-enemy-spawn-green`。预期：两个测试类及原有生成测试全部通过。

- [ ] **Step 7: 提交**

```bash
git add Assets/Scripts/Pico/PicoSceneSpawnPositions.cs Assets/Scripts/Pico/SpawnEnemies.cs Assets/Scripts/Pico/SpawnEnemies.cs.meta Assets/Tests/PlayMode/PicoSceneSpawnPositionsTests.cs Assets/Tests/PlayMode/SpawnEnemiesTests.cs Assets/Tests/PlayMode/SpawnEnemiesTests.cs.meta
git commit -m "feat(敌人): 添加桌面区域生成与定时刷新"
```

### Task 6: Bobouncing Variant 与 BikeScene 接入

**Files:**
- Create: `Assets/Prefabs/BobouncingEnemy.prefab` 及 `.meta`
- Modify: `Assets/Scripts/Game.cs` 的 `StartGame()`
- Modify: `Assets/Scenes/BikeScene.unity`
- Test: `Assets/Tests/PlayMode/TableEnemyPrefabTests.cs` 及 `.meta`

**Interfaces:**
- Consumes: Task 3–5 的 `TableEnemy`、`CollisionEffect`、`SpawnEnemies` 和 `SpawnedInArea`。
- Produces: 场景中由 `Game.StartGame()` 启动的敌人生成器，以及可独立实例化的 `BobouncingEnemy` Variant。

- [ ] **Step 1: 写资源配置失败测试**

`TableEnemyPrefabTests` 通过 `AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BobouncingEnemy.prefab")` 加载 Variant，断言：
- Prefab 存在，根缩放为 `0.1`，Rigidbody 为 Kinematic 且关闭重力，Collider 非 Trigger。
- `TableEnemy` 存在且 `edgeMargin = 0.1f`；原名为 `EnemyController` 的组件不存在或处于禁用状态。
- `CollisionEffect.triggerOnce` 和 `destroyOnHit` 均为 `true`，`effectPrefab` 非空，`lifetime = 2f`、`effectScale = 0.25f`。
- `BikeScene.unity` 文本包含 `SpawnEnemies` 脚本 GUID、`BobouncingEnemy` GUID，且新增生成器引用现有 Table Provider fileID `242273341` 与 Tracker Driver fileID `1323473305`。

- [ ] **Step 2: 运行并确认 RED**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "TableEnemyPrefabTests" -testResults ".superpowers/table-enemy-prefab-red.xml" -logFile ".superpowers/table-enemy-prefab-red.log"
```

预期：Prefab 尚不存在，测试失败。

- [ ] **Step 3: 创建并配置 Variant**

在 Unity Editor 中从原 Bobouncing 创建 `Assets/Prefabs/BobouncingEnemy.prefab` Variant，根缩放设为 `0.1`。禁用原 `EnemyController`，关闭其循环移动 AudioSource 的 Play On Awake；保留模型和 Animator。配置 Kinematic/无重力/非 Trigger Collider，添加默认参数的 `TableEnemy`，并将 `edgeMargin` 设为 `0.1`。

`CollisionEffect` 使用 `Assets/Simple FX Kit/Prefabs/Explosion Smoke.prefab`，设置 `triggerOnce = true`、`destroyOnHit = true`、`lifetime = 2`、`effectScale = 0.25`，不配置掉币。

- [ ] **Step 4: 接入场景生成器和游戏启动**

在 `GameScripts` 下新增 `SpawnEnemies` 对象，挂载 `PicoSceneSpawnPositions` 与 `SpawnEnemies`，初始随父对象停用。配置：
- `enemyPrefab = BobouncingEnemy`、`spawnInterval = 8`、`maxCount = 3`。
- `SpawnOnStart = false`、`SpawnAmount = 1`、Table Provider 引用 `242273341`。
- `surfaceClearance = 0.01`、`surfaceEdgeMargin = 0.1`、`trackerExclusionRadius = 0.2`。
- 回退 `spawnBounds` 中心 `(0, 0.05, 1.5)`、尺寸 `(1.5, 0.1, 1.5)`。
- `SpawnEnemies.tracker` 引用 Driver `1323473305`，位置生成的 `tracker` 引用 TrackedCar Transform `1323473299`。

在 `Game.StartGame()` 中按现有 PowerUps 模式查找 `SpawnEnemies`，启用对象并调用 `SpawnNow()`；游戏结束仍通过停用 `gameScripts` 停止计时器，不新增第二套生命周期。

- [ ] **Step 5: 重跑资源测试并执行全量回归**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "TableEnemyPrefabTests" -testResults ".superpowers/table-enemy-prefab-green.xml" -logFile ".superpowers/table-enemy-prefab-green.log"
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testResults ".superpowers/table-enemy-full.xml" -logFile ".superpowers/table-enemy-full.log"
```

预期：资源测试通过；完整 PlayMode 编译成功且无失败。若出现与本次无关的既有失败，记录测试名和日志，不顺手修改。

- [ ] **Step 6: 检查差异并提交**

确认只暂存本任务和前面任务明确列出的文件，不暂存工作区大量未跟踪第三方资源。确认原 Bobouncing 与 `EnemyController.cs` 无差异。

```bash
git add Assets/Prefabs/BobouncingEnemy.prefab Assets/Prefabs/BobouncingEnemy.prefab.meta Assets/Scripts/Game.cs Assets/Scenes/BikeScene.unity Assets/Tests/PlayMode/TableEnemyPrefabTests.cs Assets/Tests/PlayMode/TableEnemyPrefabTests.cs.meta
git commit -m "feat(敌人): 接入Bobouncing桌面敌人生成"
```

## 最终验证与真机待办

- 汇总 `.superpowers/table-enemy-full.xml` 的通过/失败数量，不以 Unity 进程退出码单独判断测试结果。
- 在 Editor 手动 Play 检查游荡、进入/退出警戒范围、贴边逃跑、碰撞特效与刷新补充。
- 真机后续验证 Table 锚点范围、Tracker 有效/丢失位姿、实际模型尺寸、速度与碰撞手感；当前任务不宣称这些设备行为已验证。
