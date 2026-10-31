using System;
using System.Collections.Generic;
using UnityEngine;

public class PicoSceneSpawnPositions : MonoBehaviour
{
    public bool SpawnOnStart;
    public GameObject SpawnObject;
    public int SpawnAmount = 1;
    public float surfaceClearance = 0.05f;
    public Bounds playAreaBounds = new Bounds(Vector3.zero, new Vector3(10f, 1f, 10f));
    public PicoSpawnPositions fallback;

    private bool _isSpawning;

    public string LastSpawnSource { get; private set; } = "Not spawned";
    public Vector3 LastSpawnPoint { get; private set; }
    public float LastGroundHeight { get; private set; }
    public string LastGroundHeightMethod { get; private set; } = "Not detected";

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
                LastGroundHeight = fallback.spawnBounds.center.y;
                LastGroundHeightMethod = "Fixed bounds";
                LastSpawnSource = "固定区域";
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
                    if (area.TryGetRandomPoint(out var point) && playAreaBounds.Contains(point))
                    {
                        spawnPoints.Add(point + Vector3.up * surfaceClearance);
                    }
                }
            }

            if (spawnPoints.Count != SpawnAmount)
            {
                LastGroundHeight = fallback.spawnBounds.center.y;
                LastGroundHeightMethod = "Fixed bounds";
                LastSpawnSource = "固定区域";
                fallback.StartSpawn();
                return;
            }

            LastSpawnSource = "SceneCapture";
            LastGroundHeight = spawnPoints[0].y;
            LastGroundHeightMethod = "SceneCapture floor anchors";
            LastSpawnPoint = spawnPoints[0];
            foreach (var point in spawnPoints)
            {
                Instantiate(SpawnObject, point, SpawnObject.transform.rotation);
            }
        }
        catch (Exception)
        {
            LastGroundHeight = fallback.spawnBounds.center.y;
            LastGroundHeightMethod = "Fixed bounds";
            LastSpawnSource = "固定区域";
            Debug.LogWarning("Scene capture spawn failed; using fallback");
            fallback.StartSpawn();
        }
        finally
        {
            _isSpawning = false;
        }
    }
}
