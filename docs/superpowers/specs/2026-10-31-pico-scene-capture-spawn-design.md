# PICO Scene Capture 房间感知生成设计

## 目标

用 PICO Scene Capture 的 Floor 场景锚点替代固定区域作为首选生成来源；没有房间数据、权限失败或查询异常时继续使用当前固定 `spawnBounds`。

## 方案

- `PicoFloorArea`：保存世界空间地面多边形，提供 XZ 包含判断和随机点采样。
- `IPicoFloorAreaProvider`：隔离 PICO API 与生成逻辑，测试使用 fake provider。
- `PicoSceneCaptureFloorAreaProvider`：启动 Scene Capture provider，查询 Floor 锚点；无数据时请求一次系统场景标定，再查询并转换多边形。
- `PicoSceneSpawnPositions`：保持 `SpawnObject`、`SpawnAmount`、`SpawnOnStart`、`StartSpawn()` 调用方式，防止并发重复生成，失败时调用现有 `PicoSpawnPositions` 回退。
- 两个生成对象保留原 `PicoSpawnPositions` 作为回退，新增 provider 与场景生成组件；回退组件不再自行 Start 生成。

## 约束

- 使用 SDK 6.1.1 实际命名空间 `ByteDance.PICO.XR`。
- 开启 `sceneCapture`，不额外开启 Spatial Mesh 或 Spatial Anchor。
- 不改变道具 Prefab、生成数量、间隔、计分和轨迹玩法。
- 用户明确跳过真机验证；设备端标定和透视不得标记为已验证。

## 验收

- 地面多边形采样、失败回退和并发调用测试通过。
- `BikeScene` 两处都使用 Scene Capture 首选生成并保留回退。
- `sceneCapture: 1`，Android APK 构建成功且无 Meta/OVR 冲突。
