using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PicoSceneSpawnPositionsTests
{
    private GameObject _spawner;
    private GameObject _prefab;
    private GameObject _providerObject;

    private sealed class FakeProvider : MonoBehaviour, IPicoFloorAreaProvider
    {
        public IReadOnlyList<PicoFloorArea> Areas;
        public Task<IReadOnlyList<PicoFloorArea>> Pending;
        public bool Throw;
        public int Calls;

        public Task<IReadOnlyList<PicoFloorArea>> GetFloorAreasAsync()
        {
            Calls++;
            if (Throw) throw new InvalidOperationException("provider failed");
            return Pending ?? Task.FromResult(Areas);
        }
    }

    [SetUp]
    public void SetUp()
    {
        _spawner = new GameObject("SceneSpawner");
        _prefab = new GameObject("SpawnProbe");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var instance in UnityEngine.Object.FindObjectsOfType<GameObject>().Where(item => item.name.StartsWith("SpawnProbe")))
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        if (_providerObject != null) UnityEngine.Object.DestroyImmediate(_providerObject);
        UnityEngine.Object.DestroyImmediate(_spawner);
        UnityEngine.Object.DestroyImmediate(_prefab);
    }

    [Test]
    public void TryGetRandomPoint_ReturnsPointInsideRectangle()
    {
        var area = Rectangle();

        for (var i = 0; i < 20; i++)
        {
            Assert.IsTrue(area.TryGetRandomPoint(out var point));
            Assert.GreaterOrEqual(point.x, -1f);
            Assert.LessOrEqual(point.x, 1f);
            Assert.GreaterOrEqual(point.z, -1f);
            Assert.LessOrEqual(point.z, 1f);
            Assert.AreEqual(0.2f, point.y, 0.0001f);
        }
    }

    [Test]
    public void TryGetRandomPoint_DegeneratePolygonReturnsFalse()
    {
        var area = new PicoFloorArea(new[] { Vector3.zero, Vector3.right });

        Assert.IsFalse(area.TryGetRandomPoint(out _));
    }

    [Test]
    public void StartSpawn_UsesProviderFloorArea()
    {
        var spawn = CreateSceneSpawner();
        var provider = _spawner.AddComponent<FakeProvider>();
        provider.Areas = new[] { Rectangle() };
        spawn.spawnBounds = new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));

        spawn.StartSpawn();

        var instance = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe("));
        Assert.GreaterOrEqual(instance.transform.position.x, -1f);
        Assert.LessOrEqual(instance.transform.position.x, 1f);
        Assert.GreaterOrEqual(instance.transform.position.z, -1f);
        Assert.LessOrEqual(instance.transform.position.z, 1f);
        Assert.AreEqual(0.3f, instance.transform.position.y, 0.0001f);
    }

    [Test]
    public void StartSpawn_PointOutsideFixedBoundsStillUsesSurfaceArea()
    {
        var spawn = CreateSceneSpawner();
        var provider = _spawner.AddComponent<FakeProvider>();
        provider.Areas = new[] { Rectangle() };
        spawn.spawnBounds = new Bounds(new Vector3(10f, 0f, 10f), new Vector3(2f, 1f, 2f));

        spawn.StartSpawn();

        var instance = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe("));
        Assert.GreaterOrEqual(instance.transform.position.x, -1f);
        Assert.LessOrEqual(instance.transform.position.x, 1f);
        Assert.GreaterOrEqual(instance.transform.position.z, -1f);
        Assert.LessOrEqual(instance.transform.position.z, 1f);
        Assert.AreEqual(0.3f, instance.transform.position.y, 0.0001f);
    }

    [Test]
    public void StartSpawn_ProviderFailureUsesFallback()
    {
        var spawn = CreateSceneSpawner();
        var provider = _spawner.AddComponent<FakeProvider>();
        provider.Throw = true;
        LogAssert.Expect(LogType.Warning, "Scene capture spawn failed; using fallback");

        spawn.StartSpawn();

        var instance = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe("));
        Assert.IsTrue(spawn.spawnBounds.Contains(instance.transform.position));
    }

    [Test]
    public void StartSpawn_NoProviderUsesFixedBounds()
    {
        var spawn = CreateSceneSpawner();

        spawn.StartSpawn();

        var instance = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe("));
        Assert.IsTrue(spawn.spawnBounds.Contains(spawn.transform.InverseTransformPoint(instance.transform.position)));
    }

    [Test]
    public void StartSpawn_NoFloorAreasUsesFixedBounds()
    {
        var spawn = CreateSceneSpawner();
        var provider = _spawner.AddComponent<FakeProvider>();
        provider.Areas = Array.Empty<PicoFloorArea>();

        spawn.StartSpawn();

        var instance = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe("));
        Assert.IsTrue(spawn.spawnBounds.Contains(spawn.transform.InverseTransformPoint(instance.transform.position)));
    }

    [Test]
    public void StartSpawn_ProviderFailureWithCachedHeightReusesHeight()
    {
        var spawn = CreateSceneSpawner();
        var provider = _spawner.AddComponent<FakeProvider>();
        provider.Areas = new[] { Rectangle() };
        spawn.StartSpawn();
        var first = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe("));
        Assert.AreEqual(0.3f, first.transform.position.y, 0.0001f);

        provider.Throw = true;
        LogAssert.Expect(LogType.Warning, "Scene capture spawn failed; using fallback");
        spawn.StartSpawn();

        var second = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe(") && item != first);
        Assert.AreEqual(0.3f, second.transform.position.y, 0.0001f);
    }

    [Test]
    public void StartSpawn_SpawnsRequestedAmountInsideFixedBounds()
    {
        var spawn = CreateSceneSpawner();
        spawn.SpawnAmount = 3;
        spawn.spawnBounds = new Bounds(Vector3.zero, new Vector3(2f, 1f, 4f));
        _spawner.transform.position = new Vector3(5f, 0f, -2f);

        spawn.StartSpawn();

        var instances = UnityEngine.Object.FindObjectsOfType<GameObject>().Where(item => item.name.StartsWith("SpawnProbe(")).ToArray();
        Assert.AreEqual(3, instances.Length);
        foreach (var instance in instances)
        {
            var localPosition = _spawner.transform.InverseTransformPoint(instance.transform.position);
            Assert.IsTrue(spawn.spawnBounds.Contains(localPosition));
        }
    }

    [Test]
    public void StartSpawn_NullPrefabDoesNotThrowOrSpawn()
    {
        var spawn = CreateSceneSpawner();
        spawn.SpawnObject = null;

        Assert.DoesNotThrow(spawn.StartSpawn);
        Assert.IsFalse(UnityEngine.Object.FindObjectsOfType<GameObject>().Any(item => item.name.StartsWith("SpawnProbe(")));
    }

    [UnityTest]
    public IEnumerator Start_SpawnOnStartSpawnsOnce()
    {
        var spawn = CreateSceneSpawner();
        spawn.SpawnOnStart = true;
        spawn.SpawnAmount = 2;

        yield return null;
        yield return null;

        var instances = UnityEngine.Object.FindObjectsOfType<GameObject>().Where(item => item.name.StartsWith("SpawnProbe(")).ToArray();
        Assert.AreEqual(2, instances.Length);
    }

    [Test]
    public void StartSpawn_ReferencedProviderOnAnotherObjectIsUsed()
    {
        var spawn = CreateSceneSpawner();
        _providerObject = new GameObject("ExternalProvider");
        var provider = _providerObject.AddComponent<FakeProvider>();
        provider.Areas = new[] { Rectangle() };
        spawn.sceneCaptureProvider = provider;

        spawn.StartSpawn();

        var instance = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe("));
        Assert.AreEqual(0.3f, instance.transform.position.y, 0.0001f);
        Assert.AreEqual("SceneCapture", spawn.LastSpawnSource);
    }

    [Test]
    public void StartSpawn_ConcurrentCallsSpawnOnce()
    {
        var spawn = CreateSceneSpawner();
        var provider = _spawner.AddComponent<FakeProvider>();
        var completion = new TaskCompletionSource<IReadOnlyList<PicoFloorArea>>();
        provider.Pending = completion.Task;

        spawn.StartSpawn();
        spawn.StartSpawn();
        Assert.AreEqual(1, provider.Calls);

        completion.SetResult(new[] { Rectangle() });
        Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<GameObject>().Count(item => item.name.StartsWith("SpawnProbe(")));
    }

    [Test]
    public void StartSpawn_SceneCapture_RaisesBothEventsWithSameInstanceAndSourceArea()
    {
        var spawn = CreateSceneSpawner();
        var provider = _spawner.AddComponent<FakeProvider>();
        var area = Rectangle();
        provider.Areas = new[] { area };
        GameObject originalEventInstance = null;
        GameObject areaEventInstance = null;
        PicoFloorArea? eventArea = null;
        var originalEventCalls = 0;
        spawn.Spawned += instance =>
        {
            originalEventCalls++;
            originalEventInstance = instance;
        };
        spawn.SpawnedInArea += (instance, spawnedArea) =>
        {
            areaEventInstance = instance;
            eventArea = spawnedArea;
        };

        spawn.StartSpawn();

        Assert.AreEqual(1, originalEventCalls);
        Assert.NotNull(originalEventInstance);
        Assert.AreSame(originalEventInstance, areaEventInstance);
        Assert.IsTrue(eventArea.HasValue);
        Assert.AreEqual(area.SurfaceHeight, eventArea.Value.SurfaceHeight);
        CollectionAssert.AreEqual(area.Vertices, eventArea.Value.Vertices);
    }

    [Test]
    public void StartSpawn_Fallback_RaisesWorldSpaceAreaAtActualSpawnHeight()
    {
        var spawn = CreateSceneSpawner();
        _spawner.transform.SetPositionAndRotation(new Vector3(5f, 2f, -1f), Quaternion.Euler(0f, 90f, 0f));
        spawn.spawnBounds = new Bounds(new Vector3(0f, 0.3f, 1.5f), new Vector3(2f, 0f, 2f));
        GameObject instance = null;
        PicoFloorArea? eventArea = null;
        spawn.SpawnedInArea += (spawnedInstance, area) =>
        {
            instance = spawnedInstance;
            eventArea = area;
        };

        spawn.StartSpawn();

        Assert.NotNull(instance);
        Assert.IsTrue(eventArea.HasValue);
        Assert.AreEqual(4, eventArea.Value.Vertices.Length);
        Assert.IsTrue(eventArea.Value.ContainsXZ(instance.transform.position));
        foreach (var vertex in eventArea.Value.Vertices)
        {
            Assert.AreEqual(instance.transform.position.y, vertex.y, 0.0001f);
        }
        var expectedCenter = _spawner.transform.TransformPoint(spawn.spawnBounds.center);
        Assert.AreEqual(expectedCenter.x, eventArea.Value.PlanarBounds.center.x, 0.0001f);
        Assert.AreEqual(expectedCenter.z, eventArea.Value.PlanarBounds.center.z, 0.0001f);
    }

    [Test]
    public void StartSpawn_PositiveEdgeMargin_KeepsSpawnAwayFromEdges()
    {
        var spawn = CreateSceneSpawner();
        spawn.surfaceEdgeMargin = 0.2f;
        var area = Rectangle();
        var provider = _spawner.AddComponent<FakeProvider>();
        provider.Areas = new[] { area };
        var events = new List<KeyValuePair<GameObject, PicoFloorArea>>();
        spawn.SpawnedInArea += (instance, spawnedArea) =>
            events.Add(new KeyValuePair<GameObject, PicoFloorArea>(instance, spawnedArea));

        spawn.StartSpawn();
        UnityEngine.Object.DestroyImmediate(provider);
        spawn.sceneCaptureProvider = null;
        spawn.StartSpawn();

        Assert.AreEqual(2, events.Count);
        foreach (var item in events)
        {
            Assert.IsTrue(item.Value.ContainsXZ(item.Key.transform.position, 0.2f));
        }
    }

    private PicoSceneSpawnPositions CreateSceneSpawner()
    {
        var spawn = _spawner.AddComponent<PicoSceneSpawnPositions>();
        spawn.SpawnOnStart = false;
        spawn.SpawnObject = _prefab;
        spawn.SpawnAmount = 1;
        spawn.surfaceClearance = 0.1f;
        spawn.spawnBounds = new Bounds(Vector3.zero, new Vector3(2f, 0f, 2f));
        return spawn;
    }

    private static PicoFloorArea Rectangle()
    {
        return new PicoFloorArea(new[]
        {
            new Vector3(-1f, 0.2f, -1f),
            new Vector3(1f, 0.2f, -1f),
            new Vector3(1f, 0.2f, 1f),
            new Vector3(-1f, 0.2f, 1f)
        });
    }
}
