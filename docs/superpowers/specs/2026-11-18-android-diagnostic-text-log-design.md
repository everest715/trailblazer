# Android 启动崩溃文本诊断日志设计

## 目标

在无法使用 ADB 的远端 PICO 设备上，将应用启动阶段、Unity 日志和托管异常保存为 UTF-8 纯文本，便于通过 Windows 文件资源管理器复制后用文本编辑器或浏览器打开。

## 方案与取舍

- **采用：TXT 文件。** 使用 `Application.persistentDataPath/diagnostics.txt`，实现简单，Windows 可直接查看。
- 未采用 HTML：用户明确不需要 HTML 格式。
- 未采用 HTTP 服务：额外网络服务会增加权限、生命周期和崩溃风险。
- 仅依靠文件不能获得 native 崩溃堆栈；它只能保留崩溃前最后的启动阶段和 Unity/C# 日志，native 堆栈仍需 logcat/tombstone。

## 设计

- 通过 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 自动安装，不依赖场景组件。
- 路径由 `Application.persistentDataPath` 决定；当前 Android 包名对应 `/sdcard/Android/data/com.CoffeeXR.trailblazer/files/diagnostics.txt`，不硬编码旧包名目录。
- 启动时若已有当前日志，将其保留为 `diagnostics.previous.txt`，再创建本次日志，避免重启覆盖崩溃前记录。
- 记录时间、日志类型、启动阶段、设备/应用信息，以及 `Application.logMessageReceivedThreaded` 收到的 Log、Warning、Error 和 Exception。
- 写文件时加锁，并在每条记录后 Flush，尽量保留崩溃前最后一条信息。
- 只增加诊断，不修改 SceneCapture、Tracker 或游戏业务逻辑。

## 验证

以 PlayMode 单元测试覆盖消息格式、异常堆栈保留、当前/上次日志轮换和并发写入；真机上确认文件路径、MTP 可见性和启动崩溃前最后标记。
