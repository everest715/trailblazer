using System;
using System.Collections.Generic;
using UnityEngine;

public class PicoSceneSpawnPositions : MonoBehaviour
{
    public bool SpawnOnStart;
    public GameObject SpawnObject;
    public int SpawnAmount = 1;
    public float surfaceClearance = 0.05f;
    public PicoSpawnPositions fallback;

    private bool _isSpawning;

    private void Start()
    {
        if (SpawnOnStart)
        {
            StartSpawn();
        }
    }

    public async void StartSpawn()
    {
        if (SpawnObject == null || _isSpawning)
        {
            return;
        }

        _isSpawning = true;
        try
        {
            var provider = GetComponent<IPicoFloorAreaProvider>();
            if (provider == null)
            {
                fallback.StartSpawn();
                return;
            }

            var areas = await provider.GetFloorAreasAsync();
            var spawnPoints = new List<Vector3>();
            if (areas != null)
            {
                for (var i = 0; i < SpawnAmount && areas.Count > 0; i++)
                {
                    var area = areas[UnityEngine.Random.Range(0, areas.Count)];
                    if (area.TryGetRandomPoint(out var point))
                    {
                        spawnPoints.Add(point + Vector3.up * surfaceClearance);
                    }
                }
            }

            if (spawnPoints.Count != SpawnAmount)
            {
                fallback.StartSpawn();
                return;
            }

            foreach (var point in spawnPoints)
            {
                Instantiate(SpawnObject, point, SpawnObject.transform.rotation);
            }
        }
        catch (Exception)
        {
            Debug.LogWarning("Scene capture spawn failed; using fallback");
            fallback.StartSpawn();
        }
        finally
        {
            _isSpawning = false;
        }
    }
}
