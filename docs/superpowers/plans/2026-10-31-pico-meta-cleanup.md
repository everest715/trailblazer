# PICO-only Meta 遗留清理 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 删除 Meta 遗留配置与文档，保持 PICO-only 项目可构建。

**Architecture:** 仅做资源和配置清理，不改变当前玩法或 PICO Rig。Unity OpenXR 包继续作为 PICO SDK 依赖保留，Meta/Oculus Features 仅禁用。

**Tech Stack:** Unity 2022.3.62f3、PICO Unity SDK 6.1.1、Unity OpenXR 1.12.1、URP 14.0.11。

**Spec:** `docs/superpowers/specs/2026-10-31-pico-meta-cleanup-design.md`

## Global Constraints

- 仅支持 PICO OS 6 / PICO XR。
- 不删除 `com.unity.xr.openxr`，不修改玩法代码。
- 跳过真机验证，但不得据此宣称设备兼容已验证。
- 不提交无关 TextMesh Pro 资源变化。

## Review Focus

- 误删 PICO 所需 OpenXR 包：构建时必须确认包仍存在。
- Meta Feature 禁用不全：检查 Android 设置中所有 Meta/Oculus 项。
- 旧场景或文档继续引导 Quest 工作流：用文本检查确认。

---

### Task 1: 清理 Meta 资源并验证 PICO 构建

**Files:**
- Delete: `Assets/Scenes/BasicScene.unity` 及 `.meta`
- Delete: `Assets/Oculus/`、`Assets/Resources/` 中 Oculus/OVR/MetaXR 专属资源及 `.meta`
- Modify: `Assets/XR/Settings/OpenXR Package Settings.asset`
- Modify: `README.md`
- Modify: `Agent.md`

**Interfaces:**
- Consumes: 已完成的 PICO-only 基础运行版。
- Produces: 无 Meta 遗留、可继续接入 Scene Capture 的干净项目。

- [ ] **Step 1: 删除旧场景与资源**

删除 `BasicScene`、Oculus 目录和 Oculus/OVR/MetaXR 设置；保留 PICO、XRI、Input System 与 Unity OpenXR 文件。无关 `Bangers SDF.asset` 不纳入修改。

- [ ] **Step 2: 禁用 Android Meta/Oculus Features**

通过 Unity OpenXR 设置 API 将公司或类型属于 Meta、名称包含 Meta/Oculus/Quest 的 Feature 禁用，保留所有 PICO Feature 和 PXR_Loader。

- [ ] **Step 3: 更新文档**

README 改为 PICO 构建、APK 安装和当前固定生成区域说明；Agent.md 更新 Meta 包已移除、BikeScene 已迁移及 Scene Capture 待办，删除过时的“必须保留 Quest 边界配置”约定。

- [ ] **Step 4: 静态验证**

检查启用场景和项目脚本无 Oculus/Meta.XR/OVR/MRUK 引用；`Packages/manifest.json` 仍有 `com.unity.xr.openxr` 且无 `com.meta.xr.sdk.all`；Android 只有 PXR_Loader。

- [ ] **Step 5: 构建并提交**

重新构建 `Build/PICO/Trailblazer.apk`，要求 0 errors，ZIP 中只有一个 `libopenxr_loader.so` 且无 OVRPlugin。提交本任务文件：

```bash
git add Assets/Scenes/BasicScene.unity* Assets/Oculus Assets/Resources Assets/XR README.md Agent.md
git commit -m "chore: remove legacy Meta XR configuration"
```
