using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DiagnosticTextLog : IDisposable
{
    private static readonly object SessionSync = new object();
    private static DiagnosticTextLog _current;

    private readonly object _sync = new object();
    private StreamWriter _writer;
    private bool _disposed;

    public string CurrentPath { get; }
    public string PreviousPath { get; }

    public DiagnosticTextLog(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A log directory is required.", nameof(directory));

        Directory.CreateDirectory(directory);
        CurrentPath = Path.Combine(directory, "diagnostics.txt");
        PreviousPath = Path.Combine(directory, "diagnostics.previous.txt");

        if (File.Exists(CurrentPath)) File.Copy(CurrentPath, PreviousPath, true);

        var stream = new FileStream(
            CurrentPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(false));
    }

    public static DiagnosticTextLog Start(string directory)
    {
        lock (SessionSync)
        {
            if (_current != null) return _current;

            DiagnosticTextLog session = null;
            try
            {
                session = new DiagnosticTextLog(directory);
                session.Write("Session", $"UnityVersion={Application.unityVersion}; LogPath={session.CurrentPath}");
                _current = session;
                Application.logMessageReceivedThreaded += OnUnityLogMessage;
                return session;
            }
            catch (Exception exception)
            {
                session?.Dispose();
                _current = null;
                Debug.LogWarning($"Diagnostic text log unavailable: {exception.Message}");
                return null;
            }
        }
    }

    public static void Stop()
    {
        lock (SessionSync)
        {
            if (_current == null) return;

            var session = _current;
            _current = null;
            Application.logMessageReceivedThreaded -= OnUnityLogMessage;
            session.Dispose();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallBeforeSceneLoad()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var session = Start(Application.persistentDataPath);
        if (session == null) return;

        session.Write("Stage", "BeforeSceneLoad");
        session.Write(
            "Device",
            $"Identifier={Application.identifier}; Version={Application.version}; " +
            $"Model={SystemInfo.deviceModel}; OS={SystemInfo.operatingSystem}");
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RecordAfterSceneLoad()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var scene = SceneManager.GetActiveScene();
        _current?.Write("Stage", $"AfterSceneLoad; Scene={scene.name}; ScenePath={scene.path}");
#endif
    }

    private static void OnUnityLogMessage(string condition, string stackTrace, LogType type)
    {
        _current?.Write(type.ToString(), condition, stackTrace);
    }

    public void Write(string level, string message, string stackTrace = null)
    {
        lock (_sync)
        {
            if (_disposed || _writer == null) return;

            try
            {
                _writer.Write(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                _writer.Write(" [");
                _writer.Write(string.IsNullOrEmpty(level) ? "Log" : level);
                _writer.Write("] ");
                _writer.WriteLine(message ?? string.Empty);
                if (!string.IsNullOrEmpty(stackTrace)) _writer.WriteLine(stackTrace);
                _writer.Flush();
            }
            catch (Exception)
            {
                // Diagnostics must never become another application failure.
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _writer?.Dispose();
            }
            catch (Exception)
            {
                // Diagnostics must never become another application failure.
            }
            finally
            {
                _writer = null;
            }
        }
    }
}
