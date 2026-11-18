using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TableEnemyPrefabTests
{
    private const string PrefabPath = "Assets/Prefabs/BobouncingEnemy.prefab";

    [Test]
    [Order(1)]
    public void BobouncingEnemyPrefab_HasTableEnemyAndOneShotCollisionConfiguration()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.NotNull(prefab);
        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Assert.NotNull(instance);
        try
        {
            Assert.AreEqual(Vector3.one * 0.1f, instance.transform.localScale);
            var body = instance.GetComponent<Rigidbody>();
            Assert.NotNull(body);
            Assert.IsTrue(body.isKinematic);
            Assert.IsFalse(body.useGravity);
            Assert.AreEqual(RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ, body.constraints);
            var enemyCollider = instance.GetComponent<Collider>();
            Assert.NotNull(enemyCollider);
            Assert.IsFalse(enemyCollider.isTrigger);

            var enemy = instance.GetComponent<TableEnemy>();
            Assert.NotNull(enemy);
            Assert.AreEqual(0.1f, enemy.edgeMargin);
            var originalControllers = instance.GetComponents<MonoBehaviour>()
                .Where(component => component.GetType().Name == "EnemyController");
            Assert.IsTrue(originalControllers.All(component => !component.enabled));

            var collision = instance.GetComponent<CollisionEffect>();
            Assert.NotNull(collision);
            Assert.IsTrue(collision.triggerOnce);
            Assert.IsTrue(collision.destroyOnHit);
            Assert.NotNull(collision.effectPrefab);
            Assert.AreEqual(2f, collision.lifetime);
            Assert.AreEqual(0.25f, collision.effectScale);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void BobouncingEnemyPrefab_Configure_RaisesVisualBottomToSpawnHeight()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Assert.NotNull(instance);
        try
        {
            const float spawnHeight = 0.75f;
            instance.transform.position = new Vector3(0f, spawnHeight, 0f);
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            Assert.IsNotEmpty(renderers);
            var initialBottom = renderers.Min(renderer => renderer.bounds.min.y);
            Assert.Less(initialBottom, spawnHeight, "The model fixture must reproduce the partially buried enemy");
            var area = new PicoFloorArea(new[]
            {
                new Vector3(-1f, spawnHeight, -1f),
                new Vector3(1f, spawnHeight, -1f),
                new Vector3(1f, spawnHeight, 1f),
                new Vector3(-1f, spawnHeight, 1f)
            });

            instance.GetComponent<TableEnemy>().Configure(area, null);

            var visualBottom = renderers.Min(renderer => renderer.bounds.min.y);
            Assert.Greater(instance.transform.position.y, spawnHeight);
            Assert.AreEqual(spawnHeight, visualBottom, 0.0001f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    [UnityTest]
    [Order(2)]
    public IEnumerator Game_StartGame_SpawnsConfiguredEnemy()
    {
        var gameRoot = new GameObject("EnemyStartGameTest");
        gameRoot.AddComponent<AudioSource>();
        var gameType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .First(type => type.Name == "Game");
        var game = gameRoot.AddComponent(gameType);
        var timerText = new GameObject("EnemyStartGameTimer");
        timerText.transform.SetParent(gameRoot.transform);
        var timerLabel = timerText.AddComponent<TextMeshProUGUI>();
        var gameScripts = new GameObject("EnemyStartGameScripts");
        var positions = gameScripts.AddComponent<PicoSceneSpawnPositions>();
        positions.SpawnOnStart = false;
        positions.spawnBounds = new Bounds(Vector3.zero, new Vector3(2f, 0f, 2f));
        var spawner = gameScripts.AddComponent<SpawnEnemies>();
        var prefab = new GameObject("StartGameEnemyProbe");
        var body = prefab.AddComponent<Rigidbody>();
        body.isKinematic = true;
        prefab.AddComponent<TableEnemy>();
        spawner.enemyPrefab = prefab;
        spawner.spawnInterval = 0.05f;
        spawner.maxCount = 1;
        gameScripts.SetActive(false);
        var serialized = new SerializedObject(game);
        serialized.FindProperty("gameScripts").objectReferenceValue = gameScripts;
        serialized.FindProperty("timerText").objectReferenceValue = timerLabel;
        serialized.ApplyModifiedProperties();

        try
        {
            gameType.GetMethod("StartGame", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, null);
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(1, spawner.ActiveCount);
            Assert.IsTrue(UnityEngine.Object.FindObjectsOfType<GameObject>()
                .Any(item => item.name.StartsWith("StartGameEnemyProbe(")));
        }
        finally
        {
            foreach (var instance in UnityEngine.Object.FindObjectsOfType<GameObject>()
                         .Where(item => item.name.StartsWith("StartGameEnemyProbe(")))
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
            UnityEngine.Object.DestroyImmediate(gameScripts);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(gameRoot);
        }
    }

    [UnityTest]
    [Order(3)]
    public IEnumerator BikeScene_ContainsConfiguredEnemySpawner()
    {
        LogAssert.ignoreFailingMessages = true;
        SceneManager.LoadScene("BikeScene", LoadSceneMode.Single);
        yield return null;
        var scene = SceneManager.GetActiveScene();
        try
        {
            var spawner = UnityEngine.Object.FindObjectsOfType<SpawnEnemies>(true).SingleOrDefault();
            Assert.NotNull(spawner);
            var positions = spawner.GetComponent<PicoSceneSpawnPositions>();
            Assert.NotNull(positions);
            Assert.AreEqual("GameScripts", spawner.transform.parent.name);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), spawner.enemyPrefab);
            Assert.AreEqual(8f, spawner.spawnInterval);
            Assert.AreEqual(3, spawner.maxCount);
            Assert.NotNull(spawner.tracker);
            Assert.AreSame(spawner.tracker, positions.tracker.GetComponent<MotionTrackerDriver>());
            Assert.IsTrue(positions.sceneCaptureProvider is IPicoFloorAreaProvider);
            Assert.AreEqual("PICO", positions.sceneCaptureProvider.gameObject.name);
            Assert.IsFalse(positions.SpawnOnStart);
            Assert.AreEqual(1, positions.SpawnAmount);
            Assert.AreEqual(0.01f, positions.surfaceClearance);
            Assert.AreEqual(0.1f, positions.surfaceEdgeMargin);
            Assert.AreEqual(0.2f, positions.trackerExclusionRadius);
            Assert.AreEqual(new Bounds(new Vector3(0f, 0.05f, 1.5f), new Vector3(1.5f, 0.1f, 1.5f)),
                positions.spawnBounds);
        }
        finally
        {
            var emptyScene = SceneManager.CreateScene("TableEnemySceneTestCleanup");
            SceneManager.SetActiveScene(emptyScene);
            SceneManager.UnloadSceneAsync(scene);
            LogAssert.ignoreFailingMessages = false;
        }
        yield return null;
    }
}
