using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PicoTableBoundaryObstacles : MonoBehaviour
{
    [Header("Scene Capture")]
    public MonoBehaviour sceneCaptureProvider;

    [Header("Obstacle Prefabs")]
    public GameObject fencePrefab;
    public GameObject treePrefab;
    public GameObject tree2Prefab;

    [Header("Layout")]
    public float fenceLength = 0.24f;
    public float treeHeight = 0.14f;
    [Range(0f, 15f)] public float fenceMaxYaw = 8f;
    public float edgeSafetyMargin = 0.005f;
    public Vector2 spacingRange = new Vector2(0.01f, 0.04f);
    public Vector2 inwardOffsetRange = new Vector2(0f, 0.02f);
    public Vector2 scaleRange = new Vector2(0.9f, 1.1f);
    public int randomSeed = 20261118;

    private const int MaxCandidatesPerEdge = 64;
    private const int FitAttempts = 4;
    private const float InwardStep = 0.02f;
    private IPicoFloorAreaUpdates _subscribedProvider;

    public int GeneratedCount { get; private set; }
    public Transform GeneratedRoot { get; private set; }

    private void OnEnable()
    {
        SubscribeToProvider();
    }

    private void Start()
    {
        if (sceneCaptureProvider == null)
        {
            foreach (var candidate in FindObjectsOfType<MonoBehaviour>())
            {
                if (candidate is IPicoFloorAreaProvider)
                {
                    sceneCaptureProvider = candidate;
                    break;
                }
            }
        }
        SubscribeToProvider();
        _ = RebuildAsync();
    }

    public async Task RebuildAsync()
    {
        ClearGenerated();
        if (!(sceneCaptureProvider is IPicoFloorAreaProvider provider))
        {
            return;
        }

        try
        {
            var areas = await provider.GetFloorAreasAsync();
            if (this == null)
            {
                return;
            }
            if (areas != null && areas.Count > 0)
            {
                Rebuild(areas[0]);
            }
        }
        catch (Exception)
        {
            if (this != null)
            {
                ClearGenerated();
                Debug.LogWarning("Table boundary generation failed; provider unavailable", this);
            }
        }
    }

    public void Rebuild(PicoFloorArea area)
    {
        ClearGenerated();
        if (area.Vertices == null || area.Vertices.Length < 3)
        {
            return;
        }

        var root = new GameObject("TableBoundaryObstacles");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.SetParent(transform, true);
        GeneratedRoot = root.transform;
        var random = new System.Random(randomSeed);
        var prefabBag = new List<GameObject> { fencePrefab, treePrefab, tree2Prefab };
        Shuffle(prefabBag, random);
        var bagIndex = 0;

        for (var edgeIndex = 0; edgeIndex < area.Vertices.Length; edgeIndex++)
        {
            var edgeStart = area.Vertices[edgeIndex];
            var edgeEnd = area.Vertices[(edgeIndex + 1) % area.Vertices.Length];
            var edge = edgeEnd - edgeStart;
            edge.y = 0f;
            var edgeLength = edge.magnitude;
            if (edgeLength < 0.0001f)
            {
                continue;
            }

            var tangent = edge / edgeLength;
            if (!TryGetInward(area, edgeStart, edgeEnd, tangent, out var inward))
            {
                continue;
            }

            var cursor = 0f;
            for (var candidate = 0; candidate < MaxCandidatesPerEdge && cursor < edgeLength; candidate++)
            {
                if (bagIndex == prefabBag.Count)
                {
                    Shuffle(prefabBag, random);
                    bagIndex = 0;
                }
                var prefab = prefabBag[bagIndex++];
                var isFence = prefab == fencePrefab && prefab != null;
                if (prefab == null || !PicoObstacleGeometry.TryCapture(prefab, out var localBounds))
                {
                    cursor += 0.05f;
                    continue;
                }

                var baseDimension = isFence
                    ? Mathf.Max(localBounds.size.x, localBounds.size.z)
                    : localBounds.size.y;
                var targetDimension = isFence ? fenceLength : treeHeight;
                if (baseDimension <= 0.0001f || targetDimension <= 0f)
                {
                    cursor += 0.05f;
                    continue;
                }

                var scale = targetDimension / baseDimension * NextFloat(random, scaleRange);
                var yaw = isFence
                    ? NextFloat(random, -Mathf.Abs(fenceMaxYaw), Mathf.Abs(fenceMaxYaw)) + (random.Next(2) == 0 ? 0f : 180f)
                    : NextFloat(random, 0f, 360f);
                var rotation = isFence
                    ? PicoObstacleGeometry.AlignLongAxis(PicoObstacleGeometry.GetLongitudinalAxis(localBounds), tangent, yaw)
                    : Quaternion.Euler(0f, yaw, 0f);
                var projectedLength = ProjectedLength(localBounds, rotation, tangent, scale);
                var inset = NextFloat(random, inwardOffsetRange);
                var inwardHalfExtent = isFence
                    ? ProjectedLength(localBounds, rotation, inward, scale) * 0.5f
                    : 0f;
                var samplePosition = edgeStart + tangent * Mathf.Clamp(cursor + projectedLength * 0.5f, 0f, edgeLength)
                    + inward * inwardHalfExtent;

                if (TryPlace(area, prefab, localBounds, samplePosition, inward, isFence, inset, rotation, scale, out var instance))
                {
                    GeneratedCount++;
                }
                cursor += Mathf.Max(projectedLength, 0.05f) + NextFloat(random, spacingRange);
            }
        }
    }

    private bool TryPlace(PicoFloorArea area, GameObject prefab, Bounds localBounds, Vector3 samplePosition, Vector3 inward,
        bool isFence, float inset, Quaternion rotation, float initialScale, out GameObject instance)
    {
        instance = null;
        var scale = initialScale;
        for (var attempt = 0; attempt < FitAttempts; attempt++)
        {
            var position = samplePosition + inward * (inset + attempt * InwardStep);
            position.y = area.SurfaceHeight - localBounds.min.y * scale;
            var fits = isFence
                ? PicoObstacleGeometry.FitsInside(area, localBounds, position, rotation, scale, edgeSafetyMargin)
                : area.ContainsXZ(position, edgeSafetyMargin);
            if (fits)
            {
                instance = Instantiate(prefab, position, rotation, GeneratedRoot);
                instance.name = prefab.name;
                instance.transform.localScale = Vector3.one * scale;
                return true;
            }
            scale *= 0.9f;
        }
        return false;
    }

    private static bool TryGetInward(PicoFloorArea area, Vector3 edgeStart, Vector3 edgeEnd, Vector3 tangent, out Vector3 inward)
    {
        var midpoint = (edgeStart + edgeEnd) * 0.5f;
        inward = Vector3.Cross(Vector3.up, tangent).normalized;
        if (area.ContainsXZ(midpoint + inward * 0.001f))
        {
            return true;
        }
        inward = -inward;
        return area.ContainsXZ(midpoint + inward * 0.001f);
    }

    private static float ProjectedLength(Bounds bounds, Quaternion rotation, Vector3 tangent, float scale)
    {
        var right = rotation * Vector3.right;
        var forward = rotation * Vector3.forward;
        var halfLength = Mathf.Abs(Vector3.Dot(right, tangent)) * bounds.extents.x * scale
            + Mathf.Abs(Vector3.Dot(forward, tangent)) * bounds.extents.z * scale;
        return halfLength * 2f;
    }

    private void SubscribeToProvider()
    {
        var provider = sceneCaptureProvider as IPicoFloorAreaUpdates;
        if (_subscribedProvider == provider)
        {
            return;
        }
        UnsubscribeFromProvider();
        _subscribedProvider = provider;
        if (_subscribedProvider != null)
        {
            _subscribedProvider.AreasUpdated += OnAreasUpdated;
        }
    }

    private void UnsubscribeFromProvider()
    {
        if (_subscribedProvider != null)
        {
            _subscribedProvider.AreasUpdated -= OnAreasUpdated;
            _subscribedProvider = null;
        }
    }

    private void OnAreasUpdated(IReadOnlyList<PicoFloorArea> areas)
    {
        if (areas == null || areas.Count == 0)
        {
            ClearGenerated();
            return;
        }
        Rebuild(areas[0]);
    }

    private void ClearGenerated()
    {
        GeneratedCount = 0;
        if (GeneratedRoot != null)
        {
            GeneratedRoot.gameObject.SetActive(false);
            Destroy(GeneratedRoot.gameObject);
        }
        GeneratedRoot = null;
    }

    private void OnDisable()
    {
        UnsubscribeFromProvider();
    }

    private void OnDestroy()
    {
        ClearGenerated();
    }

    private static float NextFloat(System.Random random, Vector2 range)
    {
        return NextFloat(random, Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    private static float NextFloat(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private static void Shuffle<T>(IList<T> items, System.Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var other = random.Next(i + 1);
            (items[i], items[other]) = (items[other], items[i]);
        }
    }
}
