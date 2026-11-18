using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class SceneCaptureConcurrencyTests
{
    private GameObject _root;
    private TestSceneCaptureProvider _provider;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("SceneCaptureConcurrencyTests");
        _provider = _root.AddComponent<TestSceneCaptureProvider>();
        typeof(PicoSceneCaptureFloorAreaProvider)
            .GetField("_providerStarted", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_provider, true);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
        {
            typeof(PicoSceneCaptureFloorAreaProvider)
                .GetField("_providerStarted", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_provider, false);
            Object.DestroyImmediate(_root);
        }
    }

    [Test]
    public void GetFloorAreasAsync_ConcurrentCalls_SerializesSceneQueries()
    {
        var first = Task.Run(() => _provider.GetFloorAreasAsync());
        var second = Task.Run(() => _provider.GetFloorAreasAsync());

        var completed = Task.WhenAll(first, second).Wait(5000);

        Assert.IsTrue(completed, "Concurrent floor-area requests did not complete.");
        Assert.AreEqual(2, _provider.QueryCount);
        Assert.AreEqual(1, _provider.MaxActiveQueries,
            "Two public callers entered the PICO scene query at the same time.");
    }

    [Test]
    public void AnchorRefresh_DuringPublicQuery_DoesNotOverlap()
    {
        var publicRequest = Task.Run(() => _provider.GetFloorAreasAsync());
        Assert.IsTrue(SpinWait.SpinUntil(() => _provider.QueryCount == 1, 2000),
            "The initial public query did not start.");

        var refreshMethod = typeof(PicoSceneCaptureFloorAreaProvider).GetMethod(
            "RefreshAreas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(refreshMethod);
        var invoked = Task.Run(() => refreshMethod.Invoke(_provider, null));
        Assert.IsTrue(invoked.Wait(1000), "The anchor refresh could not be invoked.");

        var completed = SpinWait.SpinUntil(
            () => publicRequest.IsCompleted && _provider.QueryCount >= 2 && _provider.ActiveQueries == 0,
            5000);

        Assert.IsTrue(completed, "The public request and anchor refresh did not complete.");
        Assert.AreEqual(2, _provider.QueryCount);
        Assert.AreEqual(1, _provider.MaxActiveQueries,
            "Anchor refresh overlapped an in-flight public PICO scene query.");
    }

    private sealed class TestSceneCaptureProvider : PicoSceneCaptureFloorAreaProvider
    {
        public int ActiveQueries;
        public int MaxActiveQueries;
        public int QueryCount;

        protected override async Task<List<PicoFloorArea>> QueryFloorAreasAsync()
        {
            Interlocked.Increment(ref QueryCount);
            var active = Interlocked.Increment(ref ActiveQueries);
            UpdateMaximum(active);
            try
            {
                await Task.Delay(75).ConfigureAwait(false);
                PicoFloorArea.TryCreateRectangle(
                    new Vector3(0f, 0.75f, 1.5f), new Vector2(1.5f, 1.5f), 0f, out var area);
                return new List<PicoFloorArea> { area };
            }
            finally
            {
                Interlocked.Decrement(ref ActiveQueries);
            }
        }

        private void UpdateMaximum(int active)
        {
            while (true)
            {
                var previous = MaxActiveQueries;
                if (active <= previous ||
                    Interlocked.CompareExchange(ref MaxActiveQueries, active, previous) == previous)
                {
                    return;
                }
            }
        }
    }
}
