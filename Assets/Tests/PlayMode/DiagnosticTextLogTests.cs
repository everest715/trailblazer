using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class DiagnosticTextLogTests
{
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "TrailblazerDiagnosticLogTests",
            Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        DiagnosticTextLog.Stop();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public void Write_Exception_PreservesMessageAndStackTrace()
    {
        using (var log = new DiagnosticTextLog(_directory))
        {
            log.Write(
                LogType.Error.ToString(),
                "managed crash 托管崩溃",
                "at Game.Start()\nat Native.Do()");
        }

        var text = File.ReadAllText(Path.Combine(_directory, "diagnostics.txt"));
        StringAssert.Contains(LogType.Error.ToString(), text);
        StringAssert.Contains("managed crash 托管崩溃", text);
        StringAssert.Contains("at Game.Start()\nat Native.Do()", text);
        StringAssert.IsMatch(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}.*Z", text);
    }

    [Test]
    public void Constructor_ExistingCurrentLog_PreservesItAsPrevious()
    {
        using (var first = new DiagnosticTextLog(_directory))
        {
            first.Write("Stage", "first-run");
        }

        using (var second = new DiagnosticTextLog(_directory))
        {
            second.Write("Stage", "second-run");
        }

        var current = File.ReadAllText(Path.Combine(_directory, "diagnostics.txt"));
        var previous = File.ReadAllText(Path.Combine(_directory, "diagnostics.previous.txt"));
        StringAssert.Contains("first-run", previous);
        StringAssert.Contains("second-run", current);
        StringAssert.DoesNotContain("first-run", current);
    }

    [Test]
    public void Write_ConcurrentCalls_PreservesEveryMessage()
    {
        using (var log = new DiagnosticTextLog(_directory))
        {
            var writes = new Task[20];
            for (var i = 0; i < writes.Length; i++)
            {
                var index = i;
                writes[i] = Task.Run(() => log.Write("Concurrent", $"[message-{index}]"));
            }

            Task.WaitAll(writes);
        }

        var text = File.ReadAllText(Path.Combine(_directory, "diagnostics.txt"));
        for (var i = 0; i < 20; i++)
        {
            var marker = $"[message-{i}]";
            Assert.AreEqual(1, CountOccurrences(text, marker), marker);
        }
    }

    [Test]
    public void Write_AllowsReadingWhileLogIsOpen()
    {
        using (var log = new DiagnosticTextLog(_directory))
        {
            log.Write("Stage", "readable-before-dispose");
            using (var stream = new FileStream(log.CurrentPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                StringAssert.Contains("readable-before-dispose", reader.ReadToEnd());
            }
        }
    }

    [Test]
    public void Write_AfterDispose_DoesNotThrow()
    {
        var log = new DiagnosticTextLog(_directory);
        log.Dispose();

        Assert.DoesNotThrow(() =>
        {
            log.Write("Stage", "ignored-after-dispose");
            log.Dispose();
        });
    }

    [Test]
    public void Start_CalledTwice_ReusesSessionAndCapturesUnityLogOnce()
    {
        var marker = $"unity-log-{Guid.NewGuid():N}";
        DiagnosticTextLog first;

        try
        {
            first = DiagnosticTextLog.Start(_directory);
            Assert.NotNull(first);
            Assert.AreSame(first, DiagnosticTextLog.Start(_directory));
            Debug.Log(marker);
        }
        finally
        {
            DiagnosticTextLog.Stop();
        }

        var text = File.ReadAllText(first.CurrentPath);
        Assert.AreEqual(1, CountOccurrences(text, marker));
        StringAssert.Contains(Application.unityVersion, text);
        StringAssert.Contains(first.CurrentPath, text);
    }

    private static int CountOccurrences(string text, string value)
    {
        return Regex.Matches(text, Regex.Escape(value)).Count;
    }
}
