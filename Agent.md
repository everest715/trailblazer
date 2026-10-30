# Agent.md — Trailblazer（Unity 2022.3 混合现实骑行项目）

本文件是项目上下文说明。在探索仓库之前请先阅读，它说明了这个项目是什么、各模块如何协作，以及任何改动都必须遵守的约定。

> 本文基于对工作区文件系统的静态分析生成（本次会话未连接 Unity Editor 工具）。凡是标注 **未验证** 的内容，请先在 Editor 中确认后再依赖。

## Added Memories
- 当前项目默认使用中文与用户交流；除非用户明确要求其他语言，否则请用中文回答。
- 项目后续只兼容 **PICO OS 6**，采用 **PICO Unity SDK 6.1 的 PICO XR 模式**；不再保留 Quest 3 兼容目标。

---

## 0. PICO OS 6 目标与 SDK 资料（优先阅读）

### 0.1 当前目标与遗留状态

- 目标设备平台为 **Android / PICO OS 6**，目标开发模式为 **PICO XR**。不要为该移植新增 Quest 兼容层，也不要混用 Unity OpenXR、PICO Spatial 与 PICO XR 的配置和 API。
- `BikeScene` 已迁移到 PICO XR，视频透视已开启，Android 仅启用 PXR_Loader，Meta SDK 包与 Meta/Oculus 资源已移除。当前金币和道具使用固定安全区域生成；PICO Scene Capture 房间感知生成是下一阶段任务。用户已明确暂时跳过真机验证，因此不得宣称 PICO OS 6 设备行为已验证。
- 保持 Unity **2022.3.62f3** 与 URP **14.0.11**。PICO Unity SDK 6.1 文档要求 PICO XR 模式使用 Unity 2022.3.15f1 或更高版本，当前编辑器版本满足该要求。
- PICO Unity SDK 导入地址：
  `https://github.com/Pico-Developer/PICO-Unity-SDK.git?path=/XR#main`
- 导入后通过 `PICO > Portal > PICO XR` 执行 `Apply All`，再检查 `Project Validation`。具体配置以当前 SDK Portal 和官方文档为准，不要手抄其他模式的设置。

### 0.2 权威资料入口

**PICO Unity SDK 6.1 LLM 文档索引**：
<https://developer-cn.picoxr.com/llmstxt/document/unity/zh/llms.txt>

该索引覆盖 Unity OpenXR、PICO Spatial、PICO XR、平台服务、开发入门、渲染、输入与追踪、混合现实、SecureMR、SpatialML、性能、调试和工具链。它是**文档路由索引，不是完整的 API 参考**：实现功能前必须继续打开对应的 Markdown 页面，核对环境要求、类名、参数、权限、限制和示例，不能只根据索引标题或既有 Meta 经验猜测 API。

默认使用 **6.1** 文档。若 SDK、PICO OS 或 Unity 版本变化，必须重新核对对应版本的兼容性、变更记录和已知问题。设备行为及未收录 API 不能只依赖该语料，需通过 Editor、PICO Emulator 和目标真机验证。

### 0.3 开发时的资料使用规则

1. 开始移植或新增能力前，先读 PICO XR 模式介绍、项目教程、项目配置验证和功能支持矩阵，再读对应功能页。
2. 运行时能力只采用 **PICO XR** 文档；平台服务问题单独查账号、RTC、匹配、社交、排行榜、成就、IAP、DLC、订阅或云存档文档。
3. PICO Developer Center、Emulator、Metrics HUD、RenderDoc、Debugger 和 CLI 是工具链资料，不能单独作为运行时 SDK 行为的依据。
4. 性能问题同时查 PICO XR 性能文档和工具文档；兼容性、升级与异常行为同时查项目验证、已知问题和问题记录。
5. 不用 Meta/Oculus API 的名称、权限或生命周期推断 PICO 实现。替换 MRUK、透视、锚点、空间网格和交互时，以 PICO 对应功能文档为准。

### 0.4 本项目文档路由

| 任务 | 实现前必读 |
|---|---|
| 环境、模式与 SDK 导入 | [环境要求](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-getting-started_环境要求.md)、[各开发模式支持的功能](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-getting-started_各开发模式支持的功能.md)、[导入 SDK 并选择开发模式](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-getting-started_快速开始_第二步：导入-pico-unity-sdk-并选择开发模式.md) |
| PICO XR 项目与配置 | [PICO XR 模式介绍](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_pico-xr-模式介绍.md)、[从 0 到 1 搭建 PICO XR 项目](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_教程：从-0-到-1-搭建一个-pico-xr-项目.md)、[项目配置验证](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_项目配置验证.md)、[Android Manifest](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_android-manifest-文件.md) |
| 相机、手柄与交互 | [交互套件概览](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_交互_交互套件概览.md)、[手柄与头戴输入映射](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_交互_手柄&头戴输入映射.md) |
| 透视与 MRUK 替代 | [环境感知套件概览](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_环境感知（混合现实）_环境感知套件概览.md)、[视频透视](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_环境感知（混合现实）_视频透视.md)、[空间网格](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_环境感知（混合现实）_空间网格.md)、[场景标定](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_环境感知（混合现实）_场景标定.md)、[空间锚点](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_环境感知（混合现实）_空间锚点_空间锚点.md) |
| URP、性能与验证 | [URP](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_渲染_通用渲染管线（urp）.md)、[性能目标](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-pico-xr_性能优化_优化应用性能_性能目标.md)、[PICO Emulator](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-getting-started_开发者工具_pico-emulator.md)、[Metrics HUD](https://developer-cn.picoxr.com/llmstxt/document/unity/zh/unity-getting-started_开发者工具_头戴端性能监测工具-(metrics-hud).md) |

完整文档目录持续以上述 `llms.txt` 为准，不在本文件中复制整份索引，避免资料过期。

---

## 1. 项目概览

**Trailblazer** 是一款最初面向 **Meta Quest 3**、现正移植到 **PICO OS 6** 的**交互式混合现实（MR）骑行体验**。玩家骑着真实的自行车，通过头显与手柄追踪把现实中的位移映射到场景里：一边收集金币与道具，一边**躲避自己行进轨迹拖出的光带**——碰到自己的轨迹会扣 20 分。依据来源：`README.md` 与 `Assets/Scripts/Game.cs`。

- **引擎版本**：Unity **2022.3.62f3**（`ProjectSettings/ProjectVersion.txt`，revision `96770f904ca7`）
- **移植前平台**：Android / Meta Quest 3。`AndroidTargetArchitectures: 2`（ARM64）、IL2CPP 后端、`minSdkVersion 32`；`AndroidManifest.xml` 中 `com.oculus.supportedDevices` 锁定为 `quest3`。
- **渲染管线**：**URP 14.0.11**
  - 默认（Graphics 设置）：`Assets/Settings/PC_RPAsset.asset`
  - 移动端质量档覆盖：`Assets/Settings/Mobile_RPAsset.asset`
- **XR 技术栈**：PICO Unity SDK 6.1.1（`com.bytedance.pico.xr`）+ PXR_Loader + XR Plug-in Management 4.5.0 + XR Interaction Toolkit 3.0.7。Unity OpenXR 1.12.1 作为 PICO SDK 依赖保留，Meta SDK 包已移除。
- **视频透视**：`PXR_ProjectSetting.asset` 已启用 `videoSeeThrough`；Main Camera 使用 Solid Color、RGBA 全 0，Mobile URP HDR 与相机后处理关闭。Quest 的 Boundaryless 配置已移除，不能照搬旧边界方案。
- **其他关键包**：Input System 1.11.2、Adaptive Performance 5.0.0（+ Google Android provider 1.2.0）、AI Navigation 1.1.5、Timeline、Visual Scripting、uGUI、2D Sprite、Test Framework 1.1.33。

产品信息：`productName: trailblazer`、`companyName: Nour Elfangary`、`bundleVersion: 0.1.0`（`ProjectSettings/ProjectSettings.asset`）。

---

## 2. 关键场景与入口

| 场景 | 构建索引 | 作用 |
|---|---|---|
| `Assets/Scenes/BikeScene.unity` | 0（启用） | **正式游戏场景**，构建中唯一启用的场景 |
| `Assets/Samples/XR Interaction Toolkit/3.0.7/Starter Assets/DemoScene.unity` | — | 包内示例 |
| `Assets/SkymonIconPackFree/skymon-icons-free-scene.unity` | — | 第三方图标包演示 |
| `Assets/Simple FX Kit/Scenes/Demo.unity` | — | 第三方特效包演示 |

**入口**：打开 `Assets/Scenes/BikeScene.unity` 后按 **Play**，或构建 APK 部署到 PICO OS 6。在 Play 模式下加载其它场景无法还原真实体验。

`BikeScene` 的根对象（读取场景 YAML 得到）：`PlayerCollider`、`Trail`、`Trace`、`Outline`、`HUD`、`GameScripts`（挂载 `Game` 与 `VignetteController`）、`Gates`（gate1–gate4）、`StartingArea`、`SpawnPowerUps`、`SpawnCoinsAtStart`、`GameOver` / `TryAgain` 面板、`Score`、`Timer`、`Multiplier`。`Game` 与 `DrawTrail` 各挂载一次，`VignetteController` 挂载两次（背景晕影 + 轨迹晕影）。

---

## 3. 核心玩法架构

核心玩法代码位于 `Assets/Scripts/`（既有 11 个文件，编译进 `Assembly-CSharp`）；PICO 专属代码位于 `Assets/Scripts/Pico/`，使用独立 `Trailblazer.Pico` 程序集。设计刻意保持精简：**由单个 `Game` 控制器驱动基于 Tag 的触发器碰撞**，表现层脚本挂在 prefab 上。

### 3.1 `Game.cs` —— 状态机、计分与计时
- 四个 Gate 检查点，由 `HandleCheckpoint` 内的 `_checkpoints` switch 串联：
  - 第 0 个检查点开始计时；1–2 推进流程；第 2 个还会用 `startThreshold` / `startTimeout` 判断起步圈耗时，决定生成 *final* 门还是 *blocked* 门；第 3 个调用 `StartGame()`。
- `EnableGate(...)` 会**销毁未激活的 Gate，并在相同 Transform 上 Instantiate** `activeGate` / `blockedGate` / `finalGate`。也就是说 Gate 在运行时被重建，**不要在检查点之后依赖 `gate1..gate4` 的持久引用**。
- 计时器：`timer = 80f`，`Timer()` 协程每秒递减；归零时关闭 `gameScripts`，显示 `playAgainText` 与 `startingArea`。
- 计分：`UpdateScore(int change)` 会乘以 `_mult`，**同时增长轨迹长度**（`drawTrail.segmentTotal += trailDelta * change`）——分数与难度是耦合的。
- 拾取物在 `OnTriggerEnter` 中按 **Tag** 分发：`Coin`、`3Coin`、`TimeBonus`、`Multiplier`、`Gate`、`DenyGate`、`StartingArea`。`Boost` 被刻意注释掉（注释为 "todo make boost work for multiplayer"）。
- 重新进入 `StartingArea` 会重载场景（硬重置）。
- 晕影反馈：`SetVignette(color)` 按拾取类型淡入不同颜色的晕影；调色板以 `readonly Color` 字段集中在类顶部定义。

### 3.2 `DrawTrail.cs` —— 核心机制
- 用**三个 `LineRenderer`**（`line`、`outline`、`trace`）绘制玩家路径，并通过 `LineRenderer.BakeMesh` 在运行时生成 `MeshCollider`。
- `InvokeRepeating`：`QueuePosition()` 每 `segmentDuration`（0.25 秒）在固定 `trailHeight` 上采样玩家 XZ 坐标；`AddSegment()` 延迟 `offset` 秒消费队列，使轨迹滞后于玩家。
- `segmentTotal`（初值 `minimumLineLength = 150`）限制存活段数；`RemoveOldSegment()` 通过整体前移坐标实现"尾巴窗口"。`Game` 每次拾取都会增大该值。
- 自碰撞（`OnTriggerEnter`，Tag 为 `Player`）：清空所有线，**扣 20 分**（下限 0），播放 `damageSound`，闪红晕影，并在 `grace`（2 秒）后重新开始绘制。
- `UpdateVignetteIntensity()` 持续根据玩家与轨迹最近点的距离抬高晕影强度——这就是"危险"反馈循环。

### 3.3 拾取物与生成链路
- `SpawnPowerUps.cs` 与 `SpawnCoinsAtStart` 使用 `PicoSpawnPositions`：在 Inspector 可调的 `spawnBounds` 内生成。当前是固定安全区域方案，不再依赖 MRUK；后续用 PICO Scene Capture 恢复房间感知生成。
- `Spawn.cs`：弹出式缩放动画（sin 过冲）。
- `BouncePickUp.cs`：正弦上下浮动；`SpinPickUp.cs`：旋转（注意其中的类名是 **`AnimatePickUp`**，不是 `SpinPickUp`）。
- `FaceCamera.cs` 与 `FollowCamera.cs`：让道具/相机朝向 PICO 头显，Meta/Oculus using 已移除。
- `VignetteController.cs` 通过 `MaterialPropertyBlock` 直接改写 URP Fullscreen Vignette 的着色器属性（`_Color0..2`、`_GradientPosition0..2`）。**它不是 Volume 驱动的后处理**，而是直接写 Renderer 的材质属性。
- `HUD.cs` 用 SmoothDamp 把 HUD 平滑地保持在相机前方；`UIBounce.cs` 做 UI 缩放脉动。

### 3.4 Tag 约定（改名 Tag 必须同步修改 `Game.cs` / `DrawTrail.cs`）
`ProjectSettings/TagManager.asset` 中的自定义 Tag：`Coin`、`3Coin`、`Boost`、`Multiplier`、`TimeBonus`、`Trigger`、`Gate`、`Gate1`–`Gate4`、`DenyGate`、`StartingArea`，以及内置的 `Player`。
Prefab 与 Tag 对应关系：`Coin.prefab`→`Coin`，`Boost.prefab`→`Boost`，`Trigger.prefab`→`Trigger`；各 Gate、`3Coin`、`Multiplier`、`TimeBonus` 在 prefab 中为 `Untagged`（依赖实例设置或其它逻辑）。

---

## 4. 核心资源

- **Prefab**（`Assets/Prefabs/`）：`Coin`、`3Coin`、`Boost`、`Multiplier`、`TimeBonus`、`Trigger`，以及 Gate 系列 `GateInactive`、`GateActive`、`GateBlocked`、`GateFinal`（后三者是 `Game.EnableGate` 在运行时替换的变体）。拾取物通常挂载 `Spawn` + `SpinPickUp`/`BouncePickUp` + `FaceCamera`。
- **材质**（`Assets/Materials/`）：`Trail`、`TrailOutline`、`Trace`、`Gate`、`GateFail`、`GateStart`、`Base`、`BaseUnlit`、`GateUnlit`，以及 `PowerUps/` 子目录。
- **音频**（`Assets/Sounds/`）：`Coin`、`Boost`、`Damage`、`Error`、`Multiplier`、`MultiplierEnd`、`TimeBonus`、`Countdown*`（由 `Game` 的 `AudioClip` 字段引用；`AudioSource` 挂在 `Game` 对象上）。
- **渲染配置**（`Assets/Settings/`）：`PC_RPAsset`、`Mobile_RPAsset`、`PC_Renderer` / `Mobile_Renderer`、`DefaultVolumeProfile`、`UniversalRenderPipelineGlobalSettings`。
- **XR 配置**：`Assets/XR/Loaders`（PXR + Simulation）、`Assets/XR/Settings`（PXR、OpenXR 包设置、XR 模拟）、`Assets/XRI/Settings`、`ProjectSettings/XRPackageSettings.asset`。Android 仅启用 PXR_Loader。
- **输入配置**：`Assets/InputSystem_Actions.inputactions`、PICO Debugger input actions 和 XRI Starter Assets。
- **第三方资源**：`Assets/Simple FX Kit/`、`Assets/SkymonIconPackFree/`、`Assets/TextMesh Pro/`、`Assets/Samples/XR Interaction Toolkit/3.0.7/`。这些属于引入的第三方内容，**避免修改**。
- 注意：工作区中 `Assets/Resources/PolySpatialSettings.asset` 已被**删除**（PolySpatial 被移除）。`git status` 中会出现一批与本次任务无关的改动，**不要擅自还原**。

---

## 5. 构建与运行

### Editor 常规流程
1. 使用 **Unity 2022.3.62f3** 打开工程。
2. 打开 `Assets/Scenes/BikeScene.unity`。
3. 若要在没有头显的情况下测试 XR Rig，可在 XR Plug-in Management 中启用 `Assets/XR/Loaders/SimulationLoader.asset`（**未验证**：请先检查各平台的 loader 配置）。
4. 按 **Play**。金币和道具在当前 `spawnBounds` 内生成；设备行为仍需后续真机验证。

### PICO OS 6 构建
`File → Build Settings` → Android（IL2CPP、ARM64、min SDK 32）→ **Build**。当前输出为 `Build/PICO/Trailblazer.apk`；连接设备后可运行 `adb install -r Build/PICO/Trailblazer.apk`。

### 命令行（Batchmode）
仓库中**没有** `Assets/Editor/` 目录，也**没有** `BuildScript.cs`，因此目前无法用 `-executeMethod`。通用批处理构建命令如下：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . -buildTarget Android \
  -logFile build.log
```

该命令只会使用 `EditorBuildSettings.asset` 中已启用的场景（目前仅 `BikeScene`）。若需要可复现的构建流程，应新增 `Assets/Editor/BuildScript.cs` 并提供静态 `PerformBuild()` 方法——目前不存在（见 §9）。

### 测试
已安装 `com.unity.test-framework` 1.1.33。`Assets/Tests/PlayMode/` 包含 3 个 `PicoSpawnPositions` 测试，已在独立 Unity 测试工作区通过；仍需在主工程 Test Runner 复跑。

---

## 6. 开发约定

以下约定来自 `Assets/Scripts/`（提交 `e421d18 "unified code style"` 曾统一代码风格）：

- **一个文件一个 MonoBehaviour**，使用 `public class`，既有玩法脚本不使用命名空间或 asmdef；PICO 专属程序集用于隔离依赖和支持测试。
- **大括号独占一行**，4 空格缩进，局部变量优先 `var`，不使用表达式体成员或 lambda 风格。
- **私有字段**：`_camelCase`（带下划线前缀）；**可调参数**：纯 `camelCase` 的 `public` 字段，直接暴露到 Inspector。本项目**不使用** `[SerializeField] private` 模式——需要调参的字段就是 `public`。
- **常量**：编译期常量用 `private const`，每实例的配色用 `private readonly`；着色器属性 ID 缓存为 `static readonly int XxxID = Shader.PropertyToID(...)`。
- **协程**（`IEnumerator` / `StartCoroutine` / `InvokeRepeating`）是标准异步写法；不使用 async/await，能用协程的地方不用 Update 计时。
- **注释**：克制使用，小写，已知缺口用 `// todo ...`。修改时保持这种语气。
- **目录结构**：`Scripts/` 是平铺的——新增玩法脚本直接放 `Assets/Scripts/`，除非新子系统足够大需要单独子目录。资源按类型分组（`Prefabs/`、`Materials/`、`Sounds/`、`Scenes/`、`Settings/`、`Resources/`），不按功能分组。
- **命名**：资源文件名用 PascalCase 且与类/prefab 名一致（`TimeBonus.prefab`、`GateActive.prefab`）。已知例外：`SpinPickUp.cs` 内定义的是 `AnimatePickUp`；`HUD.cs` 定义 `HUD`。代码中引用时以**类名**为准。
- **第三方目录**（`Oculus/`、`XR/`、`XRI/`、`TextMesh Pro/`、`Samples/`、`Simple FX Kit/`、`SkymonIconPackFree/`、`Plugins/`、`Adaptive Performance/`）属于引入内容，不要重排格式或"顺手清理"。
- 这些文件里存在未使用的 `using`（例如 `Game.cs` 中的 `System.Net.Mail`、`Unity.VisualScripting`），不要在无关改动中批量删除。

---

## 7. 包与依赖列表

摘自 `Packages/manifest.json`（重点项）：

| 包 | 版本 | 作用 |
|---|---|---|
| `com.bytedance.pico.xr` | 6.1.1（Git main 锁定 hash） | PICO XR、透视、交互与后续环境感知 |
| `com.unity.render-pipelines.universal` | 14.0.11 | 渲染；移动端 + PC 两套资产 |
| `com.unity.xr.openxr` | 1.12.1 | XR 运行时 |
| `com.unity.xr.management` | 4.5.0 | Loader 配置 |
| `com.unity.xr.interaction.toolkit` | 3.0.7 | XRI Rig；示例位于 `Assets/Samples/` |
| `com.unity.inputsystem` | 1.11.2 | 输入动作（`Assets/Resources/InputActions.asset`） |
| `com.unity.adaptiveperformance`（+ Google Android） | 5.0.0 / 1.2.0 | Quest 上的温控与性能调优 |
| `com.unity.ai.navigation` | 1.1.5 | NavMesh（当前脚本未使用） |
| `com.unity.visualscripting` | 1.9.5 | 已安装，`Assets/Scripts` 未使用 |
| `com.unity.test-framework` | 1.1.33 | `PicoSpawnPositions` PlayMode 测试 |

其余条目均为 Unity 官方 `com.unity.modules.*` 引擎模块。PICO SDK 使用 Git URL 依赖，具体 hash 见 `Packages/packages-lock.json`。

脚本宏定义（Android 与 Standalone 相同）：
`ISDK_OPENXR_HAND;USE_INPUT_SYSTEM_POSE_CONTROL;USE_STICK_CONTROL_THUMBSTICKS`。

---

## 8. 版本控制提示

`.gitignore` 为 Unity 官方标准模板，忽略：`Library/`、`Temp/`、`Obj/`、`Build/`、`Builds/`、`Logs/`、`UserSettings/`、IDE 生成文件（`*.csproj`、`*.sln`）、`*.apk`、`*.aab`。

- 需要提交：`Assets/`（含全部 `*.meta`）、`ProjectSettings/`、`Packages/manifest.json`、`Packages/packages-lock.json`、`README.md`。
- 根目录还有**生成物/噪声**应忽略：`Assembly-CSharp*.csproj`、`trailblazer-2022.3.sln`、`mono_crash.*.json`、`RuntimeActionBindings.json`、`.codely-cli/`、`.DS_Store`。
- 当前工作区存在无关改动（PolySpatial 移除、脚本 `.meta` 变动、TextMesh Pro 示例清理）。**除非用户明确要求，不要还原或提交这些改动。**
- 最近的提交历史只有 README 修改，尚无代码提交规范；写提交信息时请说明"为什么"而非"改了什么"。

---

## 9. TODO / 未决问题

- **无构建自动化**：没有 `Assets/Editor/BuildScript.cs`、没有 CI 工作流、没有 shell 脚本。批处理构建无法指定自定义场景列表。需确认发布方式（Meta 商店构建？APK 侧载？）。
- **测试缺口**：已有 3 个生成器 PlayMode 测试，但还需在主工程复跑；`Game` 与 `DrawTrail` 仍无测试。
- **`Boost` 拾取物被禁用**（`Game.cs` 注释：等多人支持）。Prefab 与 Tag 仍在——在没有玩法决策前不要"修好"它。
- **文件名与类名不一致**：`SpinPickUp.cs` → `AnimatePickUp`。本身无害，但重命名请通过 Editor 进行以保留 `.meta` 的 GUID（直接 `mv` 会破坏 prefab 引用）。
- **遗留空 meta**：存在 `Assets/Editor.meta` 但无对应 `Assets/Editor/` 目录，属删除目录后的残留，可忽略。
- **房间感知生成**：当前使用固定 `spawnBounds`；下一阶段按 PICO Scene Capture 文档查询地面/可用区域，不能照搬 Quest 的关闭边界方案。
- **未验证项**：用户暂时跳过真机验证；PICO 透视、追踪、设备帧率和完整玩法仍未确认。