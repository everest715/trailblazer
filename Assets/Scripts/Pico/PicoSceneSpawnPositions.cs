using System;
using System.Collections.Generic;
using UnityEngine;

public class PicoSceneSpawnPositions : MonoBehaviour
{
    public bool SpawnOnStart;
    public GameObject SpawnObject;
    public int SpawnAmount = 1;
    public MonoBehaviour sceneCaptureProvider;
    public float surfaceClearance = 0.05f;
    public Bounds spawnBounds = new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));

    public event Action<GameObject> Spawned;

    private bool _isSpawning;
    private float? _cachedSpawnHeight;

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
            var provider = sceneCaptureProvider == null
                ? GetComponent<IPicoFloorAreaProvider>()
                : sceneCaptureProvider as IPicoFloorAreaProvider;
            if (provider == null)
            {
                SpawnInFixedBounds(_cachedSpawnHeight);
                return;
            }

            var areas = await provider.GetFloorAreasAsync();
            var spawnPoints = new List<Vector3>();
            var batchHeight = _cachedSpawnHeight;
            if (areas != null)
            {
                for (var i = 0; i < SpawnAmount && areas.Count > 0; i++)
                {
                    var area = areas[UnityEngine.Random.Range(0, areas.Count)];
                    if (area.TryGetRandomPoint(out var point))
                    {
                        if (!batchHeight.HasValue)
                        {
                            batchHeight = point.y + surfaceClearance;
                        }
                        spawnPoints.Add(new Vector3(point.x, batchHeight.Value, point.z));
                    }
                }
            }

            if (spawnPoints.Count != SpawnAmount)
            {
                SpawnInFixedBounds(_cachedSpawnHeight);
                return;
            }

            _cachedSpawnHeight = batchHeight;
            LastSpawnSource = "SceneCapture";
            LastGroundHeight = spawnPoints[0].y;
            LastGroundHeightMethod = "SceneCapture floor anchors";
            LastSpawnPoint = spawnPoints[0];
            foreach (var point in spawnPoints)
            {
                SpawnAt(point);
            }
        }
        catch (Exception)
        {
            Debug.LogWarning("Scene capture spawn failed; using fallback");
            SpawnInFixedBounds(_cachedSpawnHeight);
        }
        finally
        {
            _isSpawning = false;
        }
    }

    private void SpawnAt(Vector3 worldPosition)
    {
        var instance = Instantiate(SpawnObject, worldPosition, SpawnObject.transform.rotation);
        Spawned?.Invoke(instance);
    }

    private void SpawnInFixedBounds(float? fixedHeight)
    {
        Vector3? firstSpawnPoint = null;
        for (var i = 0; i < SpawnAmount; i++)
        {
            var localPosition = new Vector3(
                UnityEngine.Random.Range(spawnBounds.min.x, spawnBounds.max.x),
                fixedHeight ?? UnityEngine.Random.Range(spawnBounds.min.y, spawnBounds.max.y),
                UnityEngine.Random.Range(spawnBounds.min.z, spawnBounds.max.z));
            var worldPosition = transform.TransformPoint(localPosition);
            SpawnAt(worldPosition);
            firstSpawnPoint ??= worldPosition;
        }

        LastSpawnSource = "固定区域";
        LastGroundHeightMethod = fixedHeight.HasValue ? "Cached first spawn height" : "Fixed bounds";
        LastGroundHeight = firstSpawnPoint?.y ?? fixedHeight ?? spawnBounds.center.y;
        if (firstSpawnPoint.HasValue)
        {
            LastSpawnPoint = firstSpawnPoint.Value;
        }
    }
}
