using System;
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
    private PicoSpawnPositions _fallback;

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
        _fallback = _spawner.AddComponent<PicoSpawnPositions>();
        _fallback.SpawnOnStart = false;
        _fallback.SpawnObject = _prefab;
        _fallback.SpawnAmount = 1;
        _fallback.spawnBounds = new Bounds(Vector3.zero, new Vector3(2f, 0f, 2f));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var instance in UnityEngine.Object.FindObjectsOfType<GameObject>().Where(item => item.name.StartsWith("SpawnProbe")))
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

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
        spawn.playAreaBounds = new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));

        spawn.StartSpawn();

        var instance = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe("));
        Assert.GreaterOrEqual(instance.transform.position.x, -1f);
        Assert.LessOrEqual(instance.transform.position.x, 1f);
        Assert.GreaterOrEqual(instance.transform.position.z, -1f);
        Assert.LessOrEqual(instance.transform.position.z, 1f);
        Assert.AreEqual(0.3f, instance.transform.position.y, 0.0001f);
    }

    [Test]
    public void StartSpawn_FloorPointOutsidePlayAreaUsesFallback()
    {
        var spawn = CreateSceneSpawner();
        var provider = _spawner.AddComponent<FakeProvider>();
        provider.Areas = new[] { Rectangle() };
        spawn.playAreaBounds = new Bounds(new Vector3(10f, 0f, 10f), new Vector3(2f, 1f, 2f));

        spawn.StartSpawn();

        var instance = UnityEngine.Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("SpawnProbe("));
        Assert.IsTrue(_fallback.spawnBounds.Contains(instance.transform.position));
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
        Assert.IsTrue(_fallback.spawnBounds.Contains(instance.transform.position));
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

    private PicoSceneSpawnPositions CreateSceneSpawner()
    {
        var spawn = _spawner.AddComponent<PicoSceneSpawnPositions>();
        spawn.SpawnOnStart = false;
        spawn.SpawnObject = _prefab;
        spawn.SpawnAmount = 1;
        spawn.surfaceClearance = 0.1f;
        spawn.fallback = _fallback;
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
