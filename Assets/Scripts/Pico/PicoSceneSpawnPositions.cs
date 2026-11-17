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
    public float surfaceEdgeMargin;
    public Bounds spawnBounds = new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));
    public Transform tracker;
    public float trackerExclusionRadius = 0.05f;

    public event Action<GameObject> Spawned;
    public event Action<GameObject, PicoFloorArea> SpawnedInArea;

    private const int TrackerAvoidanceAttempts = 20;
    private bool _isSpawning;
    private float? _cachedSpawnHeight;

    public string LastSpawnSource { get; private set; } = "Not spawned";
    public Vector3 LastSpawnPoint { get; private set; }
    public float LastGroundHeight { get; private set; }
    public string LastGroundHeightMethod { get; private set; } = "Not detected";

    private void Awake()
    {
        if (tracker == null)
        {
            var driver = FindAnyObjectByType<MotionTrackerDriver>();
            if (driver != null)
            {
                tracker = driver.transform;
            }
        }
    }

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
            var spawnAreas = new List<PicoFloorArea>();
            float? batchHeight = null;
            if (areas != null)
            {
                for (var i = 0; i < SpawnAmount && areas.Count > 0; i++)
                {
                    var area = areas[UnityEngine.Random.Range(0, areas.Count)];
                    if (TryGetAreaPointAwayFromTracker(area, out var point))
                    {
                        if (!batchHeight.HasValue)
                        {
                            batchHeight = point.y + surfaceClearance;
                        }
                        spawnPoints.Add(new Vector3(point.x, batchHeight.Value, point.z));
                        spawnAreas.Add(area);
                    }
                }
            }

            if (spawnPoints.Count != SpawnAmount)
            {
                SpawnInFixedBounds(_cachedSpawnHeight);
                return;
            }

            _cachedSpawnHeight = batchHeight;
            var debugInfo = provider as ISceneCaptureDebugInfo;
            var heightMethod = debugInfo?.DebugGroundHeightMethod;
            LastSpawnSource = heightMethod == "Virtual table fallback" ? "VirtualTable" : "SceneCapture";
            LastGroundHeight = spawnPoints[0].y;
            LastGroundHeightMethod = string.IsNullOrEmpty(heightMethod) ? "SceneCapture floor anchors" : heightMethod;
            LastSpawnPoint = spawnPoints[0];
            for (var i = 0; i < spawnPoints.Count; i++)
            {
                SpawnAt(spawnPoints[i], spawnAreas[i]);
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

    private bool TryGetAreaPointAwayFromTracker(PicoFloorArea area, out Vector3 point)
    {
        for (var attempt = 0; attempt < TrackerAvoidanceAttempts; attempt++)
        {
            if (area.TryGetRandomPoint(surfaceEdgeMargin, out point) && !IsNearTracker(point))
            {
                return true;
            }
        }

        point = default;
        return false;
    }

    private bool IsNearTracker(Vector3 worldPosition)
    {
        if (tracker == null || trackerExclusionRadius <= 0f)
        {
            return false;
        }

        var delta = worldPosition - tracker.position;
        delta.y = 0f;
        return delta.sqrMagnitude < trackerExclusionRadius * trackerExclusionRadius;
    }

    private void SpawnAt(Vector3 worldPosition, PicoFloorArea area)
    {
        var instance = Instantiate(SpawnObject, worldPosition, SpawnObject.transform.rotation);
        Spawned?.Invoke(instance);
        SpawnedInArea?.Invoke(instance, area);
    }

    private void SpawnInFixedBounds(float? fixedHeight)
    {
        Vector3? firstSpawnPoint = null;
        var minX = spawnBounds.min.x + surfaceEdgeMargin;
        var maxX = spawnBounds.max.x - surfaceEdgeMargin;
        var minZ = spawnBounds.min.z + surfaceEdgeMargin;
        var maxZ = spawnBounds.max.z - surfaceEdgeMargin;
        for (var i = 0; i < SpawnAmount && minX <= maxX && minZ <= maxZ; i++)
        {
            var worldPosition = Vector3.zero;
            for (var attempt = 0; attempt < TrackerAvoidanceAttempts; attempt++)
            {
                var localPosition = new Vector3(
                    UnityEngine.Random.Range(minX, maxX),
                    fixedHeight ?? UnityEngine.Random.Range(spawnBounds.min.y, spawnBounds.max.y),
                    UnityEngine.Random.Range(minZ, maxZ));
                worldPosition = transform.TransformPoint(localPosition);
                if (!IsNearTracker(worldPosition))
                {
                    break;
                }
            }
            SpawnAt(worldPosition, CreateFallbackArea(worldPosition.y));
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

    private PicoFloorArea CreateFallbackArea(float height)
    {
        var localVertices = new[]
        {
            new Vector3(spawnBounds.min.x, 0f, spawnBounds.min.z),
            new Vector3(spawnBounds.max.x, 0f, spawnBounds.min.z),
            new Vector3(spawnBounds.max.x, 0f, spawnBounds.max.z),
            new Vector3(spawnBounds.min.x, 0f, spawnBounds.max.z)
        };
        var worldVertices = new Vector3[localVertices.Length];
        for (var i = 0; i < localVertices.Length; i++)
        {
            worldVertices[i] = transform.TransformPoint(localVertices[i]);
            worldVertices[i].y = height;
        }
        return new PicoFloorArea(worldVertices, useHighestVertexHeight: true, surfaceRotation: transform.rotation);
    }
}
