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
