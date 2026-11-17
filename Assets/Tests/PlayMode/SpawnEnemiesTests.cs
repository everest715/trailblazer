using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SpawnEnemiesTests
{
    private GameObject _root;
    private GameObject _prefab;
    private GameObject _tracker;
    private SpawnEnemies _spawner;
    private FakeProvider _provider;
    private MotionTrackerDriver _driver;

    private sealed class FakeProvider : MonoBehaviour, IPicoFloorAreaProvider
    {
        public IReadOnlyList<PicoFloorArea> Areas;

        public Task<IReadOnlyList<PicoFloorArea>> GetFloorAreasAsync()
        {
            return Task.FromResult(Areas);
        }
    }

    private sealed class FakePoseSource : MonoBehaviour, IPicoTrackerPoseSource
    {
        public bool TryGetPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }
    }

    [SetUp]
    public void SetUp()
    {
        _tracker = new GameObject("EnemySpawnTracker");
        var trackerBody = _tracker.AddComponent<Rigidbody>();
        trackerBody.isKinematic = true;
        _tracker.AddComponent<FakePoseSource>();
        _driver = _tracker.AddComponent<MotionTrackerDriver>();

        _root = new GameObject("EnemySpawnerTest");
        var positions = _root.AddComponent<PicoSceneSpawnPositions>();
        positions.SpawnOnStart = false;
        positions.SpawnAmount = 1;
        positions.spawnBounds = new Bounds(Vector3.zero, new Vector3(2f, 0f, 2f));
        _provider = _root.AddComponent<FakeProvider>();
        _provider.Areas = new[] { Rectangle() };

        _prefab = new GameObject("EnemySpawnProbe");
        var body = _prefab.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        _prefab.AddComponent<TableEnemy>();

        _spawner = _root.AddComponent<SpawnEnemies>();
        _spawner.enemyPrefab = _prefab;
        _spawner.spawnInterval = 0.1f;
        _spawner.maxCount = 2;
        _spawner.tracker = _driver;
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
        if (_tracker != null) Object.DestroyImmediate(_tracker);
    }

    [UnityTest]
    public IEnumerator SpawnNow_ConfiguresEnemyWithSourceAreaAndTracker()
    {
        _spawner.SpawnNow();

        yield return new WaitForSeconds(0.15f);

        var instance = Instances().Single();
        var enemy = instance.GetComponent<TableEnemy>();
        Assert.AreSame(_driver, enemy.tracker);
        Assert.AreEqual(Rectangle().SurfaceHeight, enemy.Area.SurfaceHeight);
        CollectionAssert.AreEqual(Rectangle().Vertices, enemy.Area.Vertices);
    }

    [UnityTest]
    public IEnumerator SpawnNow_StopsAtActiveCap()
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
    public IEnumerator SpawnNow_DestroyedEnemyResumesAfterInterval()
    {
        _spawner.spawnInterval = 0.2f;
        _spawner.SpawnNow();
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(2, _spawner.ActiveCount);

        Object.DestroyImmediate(Instances().First());
        yield return null;
        Assert.AreEqual(1, _spawner.ActiveCount);

        yield return new WaitForSeconds(0.05f);
        Assert.AreEqual(1, Instances().Length);

        yield return new WaitForSeconds(0.25f);
        Assert.AreEqual(2, _spawner.ActiveCount);
        Assert.AreEqual(2, Instances().Length);
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
            .Where(item => item.name.StartsWith("EnemySpawnProbe("))
            .ToArray();
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
