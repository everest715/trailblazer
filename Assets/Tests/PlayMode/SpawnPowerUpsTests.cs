using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SpawnPowerUpsTests
{
    private GameObject _root;
    private GameObject _prefab;
    private SpawnPowerUps _spawner;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("PowerUpSpawnerTest");
        var positions = _root.AddComponent<PicoSceneSpawnPositions>();
        positions.SpawnOnStart = false;
        positions.SpawnAmount = 1;
        positions.spawnBounds = new Bounds(Vector3.zero, new Vector3(2f, 0f, 2f));

        _prefab = new GameObject("SpawnProbe");
        _spawner = _root.AddComponent<SpawnPowerUps>();
        _spawner.powerUps = new[] { _prefab };
        _spawner.spawnInterval = 0.1f;
        _spawner.maxCount = 2;
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        foreach (var instance in Instances())
        {
            Object.DestroyImmediate(instance);
        }
        if (_prefab != null) Object.DestroyImmediate(_prefab);
    }

    [UnityTest]
    public IEnumerator SpawnNow_StopsAtSceneConfiguredActiveCap()
    {
        _spawner.SpawnNow();

        yield return new WaitForSeconds(0.35f);

        Assert.AreEqual(2, _spawner.ActiveCount);
        Assert.AreEqual(2, Instances().Length);
        yield return new WaitForSeconds(0.2f);
        Assert.AreEqual(2, _spawner.ActiveCount);
        Assert.AreEqual(2, Instances().Length);
    }

    [UnityTest]
    public IEnumerator SpawnNow_DestroyedPowerUpResumesAfterInterval()
    {
        _spawner.spawnInterval = 0.2f;
        _spawner.SpawnNow();
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(2, _spawner.ActiveCount);

        Object.DestroyImmediate(Instances().First());
        yield return null;
        Assert.AreEqual(1, _spawner.ActiveCount);

        yield return new WaitForSeconds(0.05f);
        Assert.AreEqual(1, _spawner.ActiveCount);
        Assert.AreEqual(1, Instances().Length);

        yield return new WaitForSeconds(0.25f);
        Assert.AreEqual(2, _spawner.ActiveCount);
        Assert.AreEqual(2, Instances().Length);
    }

    [UnityTest]
    public IEnumerator SpawnNow_NullPrefabDoesNotIncreaseActiveCount()
    {
        _spawner.powerUps = new GameObject[] { null };
        _spawner.SpawnNow();

        yield return new WaitForSeconds(0.25f);

        Assert.AreEqual(0, _spawner.ActiveCount);
        Assert.AreEqual(0, Instances().Length);
    }

    [UnityTest]
    public IEnumerator SpawnNow_RepeatedCallsDoNotDuplicateSpawning()
    {
        _spawner.spawnInterval = 0.15f;
        _spawner.maxCount = 4;
        _spawner.SpawnNow();
        _spawner.SpawnNow();

        yield return new WaitForSeconds(0.23f);
        Assert.AreEqual(1, _spawner.ActiveCount);

        yield return new WaitForSeconds(0.16f);
        Assert.AreEqual(2, _spawner.ActiveCount);
    }

    private static GameObject[] Instances()
    {
        return Object.FindObjectsOfType<GameObject>()
            .Where(item => item.name.StartsWith("SpawnProbe("))
            .ToArray();
    }
}
