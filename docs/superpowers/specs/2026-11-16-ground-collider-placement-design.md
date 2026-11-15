# GroundCollider 初始化位置设计

## 目标

`BikeScene` 启动时，将已有的 `GroundCollider` 放到 tracker（场景中的 `TrackedCar`）正下方 0.2 米，使碰撞掉落的金币能够停在地板碰撞体上。

## 方案

- 新增独立的 `GroundColliderPlacement` 组件，并挂到 `GroundCollider`。
- 组件持有 `MotionTrackerDriver tracker` 引用和默认值为 `0.2f` 的 `trackerHeightOffset`。
- `Start()` 中将世界坐标设置为 `tracker.transform.position + Vector3.down * trackerHeightOffset`，因此 X/Z 与 tracker 相同，Y 低 0.2 米。
- 未绑定 tracker 时保留 `GroundCollider` 原位置，避免空引用。
- 在 `BikeScene` 中绑定现有 `TrackedCar` 的 `MotionTrackerDriver`，偏移量为 `0.2`。

## 已考虑的替代方案

- **每帧跟随 tracker**：会让地板随小车移动，不符合“初始化时”的要求，因此不采用。
- **接入场景扫描高度**：会增加异步初始化和场景捕获依赖；本需求已明确使用 tracker 高度，因此不采用。
- **直接放入 `Game`**：能工作，但会把地面碰撞体职责混入关卡逻辑；独立组件更小且可单独测试。

## 范围与验证

- 只初始化一次，不在后续帧跟随 tracker。
- 不修改金币物理参数、场景扫描逻辑或现有未跟踪资源。
- 添加 PlayMode 测试，验证启动位置及未绑定 tracker 时的行为。
- 运行相关 PlayMode 测试；真机金币落地效果需在 PICO 上另行确认。
