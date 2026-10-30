using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PicoSpawnPositionsTests
{
    private GameObject _spawner;
    private GameObject _prefab;

    [SetUp]
    public void SetUp()
    {
        _spawner = new GameObject("Spawner");
        _prefab = new GameObject("SpawnProbe");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var instance in Object.FindObjectsOfType<GameObject>().Where(item => item.name.StartsWith("SpawnProbe")))
        {
            Object.DestroyImmediate(instance);
        }

        Object.DestroyImmediate(_spawner);
        Object.DestroyImmediate(_prefab);
    }

    [Test]
    public void StartSpawn_SpawnsRequestedAmountInsideBounds()
    {
        var spawn = _spawner.AddComponent<PicoSpawnPositions>();
        spawn.SpawnOnStart = false;
        spawn.SpawnObject = _prefab;
        spawn.SpawnAmount = 3;
        spawn.spawnBounds = new Bounds(Vector3.zero, new Vector3(2f, 1f, 4f));
        _spawner.transform.position = new Vector3(5f, 0f, -2f);

        spawn.StartSpawn();

        var instances = Object.FindObjectsOfType<GameObject>().Where(item => item.name.StartsWith("SpawnProbe(")).ToArray();
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
        var spawn = _spawner.AddComponent<PicoSpawnPositions>();
        spawn.SpawnOnStart = false;
        spawn.SpawnObject = null;

        Assert.DoesNotThrow(spawn.StartSpawn);
        Assert.IsFalse(Object.FindObjectsOfType<GameObject>().Any(item => item.name.StartsWith("SpawnProbe(")));
    }

    [UnityTest]
    public IEnumerator Start_SpawnOnStartSpawnsOnce()
    {
        var spawn = _spawner.AddComponent<PicoSpawnPositions>();
        spawn.SpawnOnStart = true;
        spawn.SpawnObject = _prefab;
        spawn.SpawnAmount = 2;

        yield return null;
        yield return null;

        var instances = Object.FindObjectsOfType<GameObject>().Where(item => item.name.StartsWith("SpawnProbe(")).ToArray();
        Assert.AreEqual(2, instances.Length);
    }
}
