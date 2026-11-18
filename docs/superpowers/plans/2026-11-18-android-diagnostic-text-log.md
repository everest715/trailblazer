# Android Diagnostic Text Log Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 PICO 应用私有外部目录保存本次及上一次启动的 UTF-8 Unity 诊断日志。

**Architecture:** 可独立实例化和释放的 `DiagnosticTextLog` 负责文件轮换、格式化、加锁写入与 Flush；仅 Android 播放器生效的启动钩子负责安装 Unity 日志回调及记录启动阶段。核心写入器不依赖 PICO SDK，可直接在 PlayMode 测试中验证。

**Tech Stack:** Unity 2022.3、C#、`System.IO`、`Application.logMessageReceivedThreaded`、NUnit PlayMode Tests。

**Spec:** `docs/superpowers/specs/2026-11-18-android-diagnostic-text-log-design.md`

## Global Constraints

- 当前日志为 `Application.persistentDataPath/diagnostics.txt`，上次日志为 `diagnostics.previous.txt`，不得硬编码旧包名目录。
- 仅增加诊断，不修改 SceneCapture、Tracker 或游戏业务逻辑。
- 文本使用 UTF-8，每条日志写入后 Flush，允许其他进程读取。
- native 崩溃堆栈仍依赖 logcat/tombstone，不宣称文本文件能替代它们。
- 不带入或还原工作区现有的 TextMesh Pro、OpenXR 设置修改，不主动推送。

## Review Focus

- 应用重启覆盖证据：由 `Constructor_ExistingCurrentLog_PreservesItAsPrevious` 验证轮换。
- 后台线程同时写日志：由 `Write_ConcurrentCalls_PreservesEveryMessage` 验证加锁。
- 应用运行时复制文件：由 `Write_AllowsReadingWhileLogIsOpen` 验证 `FileShare.Read`。
- 异常与多行堆栈丢失：由 `Write_Exception_PreservesMessageAndStackTrace` 验证内容。
- 日志系统自身引发二次异常：由 `Write_AfterDispose_DoesNotThrow` 验证关闭后的写入安全；安装钩子还须捕获初始化异常。

---

### Task 1: TXT 日志核心、Android 启动钩子和回归测试

**Files:**
- Create: `Assets/Scripts/Pico/Diagnostics/DiagnosticTextLog.cs`
- Create: `Assets/Scripts/Pico/Diagnostics/DiagnosticTextLog.cs.meta`（由 Unity 生成）
- Create: `Assets/Tests/PlayMode/DiagnosticTextLogTests.cs`
- Create: `Assets/Tests/PlayMode/DiagnosticTextLogTests.cs.meta`（由 Unity 生成）

**Interfaces:**
- Produces: `DiagnosticTextLog(string directory)`、`Write(string level, string message, string stackTrace = null)`、`Dispose()`、`CurrentPath`、`PreviousPath`。
- Produces: 静态诊断生命周期 `DiagnosticTextLog.Start(string directory)` 与 `DiagnosticTextLog.Stop()`；Android 内部入口为 `InstallBeforeSceneLoad()` 和 `RecordAfterSceneLoad()`。

- [ ] **Step 1: 写失败的 PlayMode 测试**

在 `DiagnosticTextLogTests.cs` 中为每个测试创建唯一临时目录，并在 TearDown 删除。测试及关键断言：

1. `Write_Exception_PreservesMessageAndStackTrace`：写入 `Error`、消息和模拟堆栈，释放后读取文本，断言三项均存在，并包含 `LogType`、UTC 时间和 Unity 版本等记录格式所需字段中的消息对应行。
2. `Constructor_ExistingCurrentLog_PreservesItAsPrevious`：第一实例写入 `first-run` 后释放，第二实例写入 `second-run`；断言 previous 含 `first-run`，current 含 `second-run` 且不含 `first-run`。
3. `Write_ConcurrentCalls_PreservesEveryMessage`：用 `Task.Run` 并行写入唯一消息 `message-0` 至 `message-19`，释放后断言每个消息恰好出现一次。
4. `Write_AllowsReadingWhileLogIsOpen`：实例未释放时写入一条消息，使用共享读取流读取并断言内容存在；Windows 上该测试可直接发现错误文件共享模式。
5. `Write_AfterDispose_DoesNotThrow`：释放后调用 `Write`，使用 `Assert.DoesNotThrow`。

- [ ] **Step 2: 运行测试并确认 RED**

当前 Unity Editor 已打开本项目，先关闭 Editor，再运行：

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/Work/CoffeeXR/trailblazer-2022.3" -runTests -testPlatform PlayMode -testFilter "DiagnosticTextLogTests" -testResults "D:/Work/CoffeeXR/trailblazer-2022.3/Temp/diagnostic-log-red.xml" -logFile "D:/Work/CoffeeXR/trailblazer-2022.3/Temp/diagnostic-log-red.log"
```

预期：因 `DiagnosticTextLog` 类型尚不存在而编译失败，或测试因缺少实现失败；不得把编译配置错误误当成有效 RED。

- [ ] **Step 3: 实现最小核心写入器**

`DiagnosticTextLog` 构造时创建目录，将已有 current 复制覆盖到 previous，再以 `FileMode.Create`、`FileShare.ReadWrite`、UTF-8 打开 current。`Write` 在私有锁内写入一条以 ISO 8601 UTC 时间和 level 开头的记录，消息和 stack trace 分行保留，随后 Flush；释放后及 I/O 失败时不得向应用抛出二次异常。`Dispose` 在锁内释放 writer，并保证幂等。

- [ ] **Step 4: 运行核心测试并确认 GREEN**

使用 Step 2 的 Unity 命令，将结果和日志文件名改为 `diagnostic-log-green.xml/.log`。预期：5 个 `DiagnosticTextLogTests` 全部通过，无编译错误。

- [ ] **Step 5: 为启动安装行为增加失败测试**

增加 `Start_CalledTwice_ReusesSessionAndCapturesUnityLogOnce`：在临时目录调用两次 `DiagnosticTextLog.Start(directory)`，断言返回同一实例；`Debug.Log` 输出唯一标记后调用 `DiagnosticTextLog.Stop()`，读取文件并断言该标记只出现一次。测试必须放在 `try/finally` 中并确保 `Stop()`，避免泄漏全局 Unity 日志订阅。同时断言首条会话信息包含 Unity 版本和实际日志路径。

- [ ] **Step 6: 实现诊断生命周期和仅 Android 生效的启动钩子**

`Start` 在静态锁内复用现有实例，否则构造写入器、记录包含 Unity 版本和实际路径的会话信息，并只订阅一次 `Application.logMessageReceivedThreaded`；初始化失败不得向游戏抛出异常。`Stop` 取消订阅、释放实例并清空静态引用，且可重复调用。

在 `#if UNITY_ANDROID && !UNITY_EDITOR` 内：

- `BeforeSceneLoad` 调用 `Start(Application.persistentDataPath)`，记录 `BeforeSceneLoad`、`Application.identifier`、应用版本、设备型号和操作系统。
- `AfterSceneLoad` 记录 `AfterSceneLoad` 与首个场景名。
- Unity 回调将 `LogType`、`condition`、`stackTrace` 交给 `Write`；不修改原有 `Debug.Log` 行为。
- 类域重载导致重复调用时，由 `Start` 的实例守卫避免重复订阅和文件轮换。

- [ ] **Step 7: 运行目标测试和完整 PlayMode 套件**

先运行 `DiagnosticTextLogTests`，再移除 `-testFilter` 运行完整 PlayMode 套件，结果写入 `Temp/diagnostic-log-all.xml`。预期：目标测试与项目已有测试全部通过；任何既有失败须单独报告，不能隐瞒。

- [ ] **Step 8: 静态检查与真机交付说明**

运行 `git diff --check` 并检查最终 diff 只包含本任务及设计/计划文档。真机构建后确认文件位于：

```text
内部共享存储/Android/data/com.CoffeeXR.trailblazer/files/diagnostics.txt
```

通过 Windows 文件资源管理器复制后用记事本或浏览器打开；若应用重启，另取 `diagnostics.previous.txt`。确认真机阶段前，不声称已捕获 native 崩溃堆栈。

- [ ] **Step 9: 独立提交本任务**

只暂存本任务代码、测试、meta 和文档，使用中文 Conventional Commit：

```bash
git commit -m "feat(诊断): 添加 Android 启动文本日志"
```

不提交 TextMesh Pro 和 OpenXR 的既有修改，不推送。
