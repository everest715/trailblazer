using System.Collections.Generic;
using UnityEngine;

public class SpawnPowerUps : MonoBehaviour
{
    public GameObject[] powerUps;
    public float spawnInterval = 5f;
    public int maxCount = 8;

    private readonly List<GameObject> _activePowerUps = new List<GameObject>();
    private PicoSceneSpawnPositions _spawn;
    private bool _spawnRequested;
    private bool _timerRunning;

    public int ActiveCount
    {
        get
        {
            RemoveDestroyed();
            return _activePowerUps.Count;
        }
    }

    private void Awake()
    {
        _spawn = GetComponent<PicoSceneSpawnPositions>();
        _spawn.Spawned += OnSpawned;
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

        if (powerUps == null || powerUps.Length == 0)
        {
            return;
        }

        _spawn.SpawnObject = powerUps[Random.Range(0, powerUps.Length)];
        _spawn.SpawnAmount = 1;
        _spawn.StartSpawn();
    }

    private void OnSpawned(GameObject instance)
    {
        if (instance != null && !_activePowerUps.Contains(instance))
        {
            _activePowerUps.Add(instance);
        }

        if (ActiveCount >= maxCount)
        {
            PauseTimer();
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
        _activePowerUps.RemoveAll(item => item == null);
    }

    private void OnDestroy()
    {
        _spawnRequested = false;
        PauseTimer();
        if (_spawn != null)
        {
            _spawn.Spawned -= OnSpawned;
        }
    }
}
