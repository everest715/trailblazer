# PICO XR 基础运行版设计

## 目标

将 `BikeScene` 从 Meta Quest 运行时迁移到 PICO OS 6 / PICO XR，使玩家能在 PICO 设备上进入场景、使用头显与手柄、看到视频透视，并继续生成金币和道具。

本阶段先交付可构建、可玩、可验证的基础版本。MRUK 的房间生成先由固定安全区域生成替代，保证玩法可测试。

## 背景

- Unity 2022.3.62f3、URP 14.0.11、PICO Unity SDK 6.1.1 已安装，Android 平台与 PICO XR Portal 已配置。
- `BikeScene` 仍包含 `OVRCameraRigInteraction`、`MRUK`，没有 PICO 脚本或 `PXR_Manager`。
- `SpawnPowerUps` 与 `SpawnCoinsAtStart` 两处都使用 Meta `FindSpawnPositions`；`SpawnPowerUps.cs` 直接引用其类型。
- Meta SDK 暂时保留，因此 Project Validation 会同时出现 Meta 与 PICO 规则。

## 方案选择

1. **PICO XR（已选择）**：项目只支持 PICO OS 6，可直接使用 PXR_Manager、视频透视和后续 Scene Capture 能力。
2. Unity OpenXR：适合跨平台，但本项目已明确不保留 Quest，且会增加平台抽象，不采用。
3. PICO Spatial：适合 UI 为主或简单 3D 空间应用，不适合当前沉浸式 3D 骑行游戏，不采用。

## 本阶段范围

1. 用固定安全区域生成器替换两处 Meta `FindSpawnPositions`，保持 `SpawnObject`、`SpawnAmount`、`SpawnOnStart` 和 `StartSpawn()` 的现有调用方式。
2. 用 PICO XR Origin、`PXR_Manager` 和 PICO Controller Tracking 替换 OVR Rig，并保留现有玩家、HUD 与相机引用。
3. 配置 PICO 视频透视：Solid Color、透明背景、关闭 HDR 与后处理。
4. 通过 Unity 编译、Project Validation 和 PICO 设备/模拟器验证基础玩法。

## 非目标

- 本阶段不实现 Scene Capture、Spatial Anchor 或 Spatial Mesh 驱动生成。
- 本阶段不移除 Meta SDK；在场景和代码不再引用 Meta 后单独清理，避免把验证规则变化与功能迁移混在一起。
- 不保留 Quest 3 兼容层，不迁移到 PICO Spatial。

## 后续阶段

基础版本稳定后，使用 PICO Scene Capture 查询地面/可用区域并恢复房间感知生成；随后移除 Meta SDK、Meta OpenXR Features 与 Quest 资源，并最终只保留 PICO Loader。

## 约束与验收

- 不改变计分、轨迹、Gate、计时和道具效果。
- 固定生成区域必须可由 Inspector 调整，空 Prefab 不得导致异常。
- 场景只能有一个启用的 XR Origin、MainCamera 和 AudioListener。
- 透视时不得使用后处理；Android 使用 Mobile URP，HDR 保持关闭。
- 只有实际构建或设备验证通过的项目才能宣称移植完成。
