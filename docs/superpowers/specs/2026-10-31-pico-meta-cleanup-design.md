# PICO-only Meta 遗留清理设计

## 目标

移除 PICO 基础运行版不再使用的 Meta 场景、设置和 OpenXR Features，更新项目说明，并确认 APK 仍能构建。

## 当前状态

- `BikeScene`、PICO Rig、固定区域生成和视频透视已完成；Android 仅使用 PXR_Loader。
- `com.meta.xr.sdk.all` 已移除，APK 构建成功。
- `BasicScene`、Oculus/MetaXR 资源及 Meta OpenXR Features 仍残留。
- README 和 Agent.md 部分内容仍描述 Quest 工作流。

## 方案

直接删除不再引用的 Meta 资源；在 Android OpenXR 设置中仅禁用 Meta/Oculus 项，保留 PICO 和 Unity OpenXR 包。更新文档后重新构建验证。

## 非目标

- 本任务不实现 Scene Capture、Spatial Anchor 或 Spatial Mesh。
- 不进行真机验证（用户明确跳过）。
- 不重构玩法或升级 Unity、URP、XRI。

## 验收

- 项目脚本与启用场景中无 Meta/Oculus/MRUK 引用。
- Android 仅启用 PICO Loader，Meta/Oculus OpenXR Features 禁用。
- README 不再提供 Quest 关闭边界方案。
- Android APK 构建成功且只有一个 `libopenxr_loader.so`。
