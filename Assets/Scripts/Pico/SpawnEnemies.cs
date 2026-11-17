using System.Collections.Generic;
using UnityEngine;

public class SpawnEnemies : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnInterval = 8f;
    public int maxCount = 3;
    public MotionTrackerDriver tracker;

    private readonly List<GameObject> _activeEnemies = new List<GameObject>();
    private PicoSceneSpawnPositions _spawn;
    private bool _spawnRequested;
    private bool _timerRunning;

    public int ActiveCount
    {
        get
        {
            RemoveDestroyed();
            return _activeEnemies.Count;
        }
    }

    private void Awake()
    {
        _spawn = GetComponent<PicoSceneSpawnPositions>();
        _spawn.SpawnedInArea += OnSpawned;
        FindTrackerIfNeeded();
    }

    public void SpawnNow()
    {
        _spawnRequested = true;
        StartTimerIfNeeded();
    }

    private void Update()
    {
        if (!_spawnRequested)
        {
            return;
        }

        if (ActiveCount >= maxCount)
        {
            PauseTimer();
        }
        else
        {
            StartTimerIfNeeded();
        }
    }

    private void SpawnObject()
    {
        if (ActiveCount >= maxCount)
        {
            PauseTimer();
            return;
        }

        if (enemyPrefab == null)
        {
            return;
        }

        var enemy = enemyPrefab.GetComponent<TableEnemy>();
        _spawn.SpawnObject = enemyPrefab;
        _spawn.SpawnAmount = 1;
        _spawn.surfaceEdgeMargin = enemy == null ? 0f : enemy.edgeMargin;
        _spawn.StartSpawn();
    }

    private void OnSpawned(GameObject instance, PicoFloorArea area)
    {
        if (instance == null)
        {
            return;
        }

        var enemy = instance.GetComponent<TableEnemy>();
        if (enemy == null)
        {
            Debug.LogError("Enemy prefab requires TableEnemy", instance);
            Destroy(instance);
            return;
        }

        FindTrackerIfNeeded();
        enemy.Configure(area, tracker);
        if (!_activeEnemies.Contains(instance))
        {
            _activeEnemies.Add(instance);
        }

        if (ActiveCount >= maxCount)
        {
            PauseTimer();
        }
    }

    private void FindTrackerIfNeeded()
    {
        if (tracker == null)
        {
            tracker = FindAnyObjectByType<MotionTrackerDriver>();
        }
    }

    private void StartTimerIfNeeded()
    {
        if (_timerRunning || ActiveCount >= maxCount)
        {
            return;
        }

        InvokeRepeating(nameof(SpawnObject), spawnInterval, spawnInterval);
        _timerRunning = true;
    }

    private void PauseTimer()
    {
        if (!_timerRunning)
        {
            return;
        }

        CancelInvoke(nameof(SpawnObject));
        _timerRunning = false;
    }

    private void RemoveDestroyed()
    {
        _activeEnemies.RemoveAll(item => item == null);
    }

    private void OnDestroy()
    {
        _spawnRequested = false;
        PauseTimer();
        if (_spawn != null)
        {
            _spawn.SpawnedInArea -= OnSpawned;
        }
    }
}
