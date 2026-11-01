using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.Rendering;

public class PicoSceneCaptureFloorAreaProvider : MonoBehaviour, IPicoFloorAreaProvider, ISceneCaptureDebugInfo
{
    private bool _providerStarted;
    private bool _captureRequested;
    private bool _eventsSubscribed;
    private bool _isQuerying;
    private bool _refreshPending;
    private PicoMotionTrackerPoseSource _tracker;

    [Header("Scene Capture")]
    public PxrSemanticLabel surfaceLabel = PxrSemanticLabel.Floor;

    [Header("Temporary Table Height")]
    public bool useTrackerForTableHeight = true;
    public float trackerTableHeightOffset = 0.05f;

    [Header("Debug Meshes")]
    public bool showDebugMeshes;
    public Color averageHeightColor = new Color(0.1f, 1f, 0.3f, 0.35f);
    public Color verticesColor = new Color(1f, 0.25f, 0.2f, 0.35f);

    private Transform _debugMeshRoot;
    private Material _averageHeightMaterial;
    private Material _verticesMaterial;
    private readonly List<DebugBox> _debugBoxes = new List<DebugBox>();

    private struct DebugBox
    {
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector3 Extent;
    }

    public int DebugFloorAreaCount { get; private set; }
    public event Action<IReadOnlyList<PicoFloorArea>> AreasUpdated;
    public string DebugQueryStatus { get; private set; } = "Not queried";
    public float DebugGroundHeight { get; private set; }
    public string DebugGroundHeightMethod { get; private set; } = "Not detected";
    public Vector3 DebugAnchorPosition { get; private set; }
    public Vector3 DebugBoxLocalPosition { get; private set; }
    public Vector3 DebugBoxExtent { get; private set; }
    public Vector3 DebugAnchorEuler { get; private set; }
    public Vector3 DebugBoxLocalEuler { get; private set; }
    public Vector3 DebugBoxCenter { get; private set; }

    public async Task<IReadOnlyList<PicoFloorArea>> GetFloorAreasAsync()
    {
        if (!_providerStarted)
        {
            var startResult = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.SceneCapture);
            if (startResult != PxrResult.SUCCESS)
            {
                DebugQueryStatus = $"Provider failed: {startResult}";
                throw new InvalidOperationException($"StartSenseDataProvider failed: {startResult}");
            }

            _providerStarted = true;
            DebugQueryStatus = "Provider started";
            SubscribeToAnchorUpdates();
        }

        var areas = await QueryFloorAreasAsync();
        if (areas.Count == 0 && !_captureRequested)
        {
            _captureRequested = true;
            var captureResult = await PXR_MixedReality.StartSceneCaptureAsync();
            if (captureResult != PxrResult.SUCCESS)
            {
                DebugQueryStatus = $"Capture failed: {captureResult}";
                throw new InvalidOperationException($"StartSceneCapture failed: {captureResult}");
            }

            DebugQueryStatus = "Capture completed";
            areas = await QueryFloorAreasAsync();
        }

        return areas;
    }

    private void SubscribeToAnchorUpdates()
    {
        if (_eventsSubscribed) return;
        PXR_Manager.SceneAnchorDataUpdated += OnSceneAnchorDataUpdated;
        _eventsSubscribed = true;
    }

    private void UnsubscribeFromAnchorUpdates()
    {
        if (!_eventsSubscribed) return;
        PXR_Manager.SceneAnchorDataUpdated -= OnSceneAnchorDataUpdated;
        _eventsSubscribed = false;
    }

    private void OnSceneAnchorDataUpdated()
    {
        RefreshAreas();
    }

    private void OnTrackerPoseValid()
    {
        RefreshAreas();
    }

    private async void RefreshAreas()
    {
        if (_isQuerying)
        {
            _refreshPending = true;
            return;
        }

        _isQuerying = true;
        try
        {
            var areas = await QueryFloorAreasAsync();
            AreasUpdated?.Invoke(areas);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Scene anchor update failed: {exception.Message}");
        }
        finally
        {
            _isQuerying = false;
            if (_refreshPending)
            {
                _refreshPending = false;
                RefreshAreas();
            }
        }
    }

    private void OnEnable()
    {
        if (_providerStarted)
        {
            SubscribeToAnchorUpdates();
            if (_tracker != null)
            {
                _tracker.PoseBecameValid -= OnTrackerPoseValid;
                _tracker.PoseBecameValid += OnTrackerPoseValid;
            }
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromAnchorUpdates();
        UnsubscribeFromTracker();
    }

    private void UnsubscribeFromTracker()
    {
        if (_tracker != null)
        {
            _tracker.PoseBecameValid -= OnTrackerPoseValid;
        }
    }

    private async Task<List<PicoFloorArea>> QueryFloorAreasAsync()
    {
        var query = await PXR_MixedReality.QuerySceneAnchorAsync(new[] { surfaceLabel });
        if (query.result != PxrResult.SUCCESS)
        {
            DebugQueryStatus = $"Query failed: {query.result}";
            throw new InvalidOperationException($"QuerySceneAnchor failed: {query.result}");
        }

        var areas = new List<PicoFloorArea>();
        _debugBoxes.Clear();
        foreach (var anchorHandle in query.anchorHandleList)
        {
            if (PXR_MixedReality.LocateAnchor(anchorHandle, out var position, out var rotation) != PxrResult.SUCCESS)
            {
                continue;
            }

            if (areas.Count == 0)
            {
                DebugAnchorPosition = position;
                DebugAnchorEuler = rotation.eulerAngles;
            }

            if (surfaceLabel == PxrSemanticLabel.Table)
            {
                if (PXR_MixedReality.GetSceneBox3DData(anchorHandle, out var localPosition, out var localRotation, out var extent) == PxrResult.SUCCESS)
                {
                    // LocateAnchor converts its pose to Unity's left-handed space, but Box3D's
                    // local center is returned unchanged. Apply the same Z conversion here.
                    localPosition.z = -localPosition.z;
                    DebugBoxLocalPosition = localPosition;
                    DebugBoxExtent = extent;
                    DebugBoxLocalEuler = localRotation.eulerAngles;
                    DebugBoxCenter = position + rotation * localPosition;
                    _debugBoxes.Add(new DebugBox
                    {
                        Center = position + rotation * localPosition,
                        Rotation = rotation,
                        Extent = extent
                    });
                    areas.Add(PicoFloorArea.FromBox3D(position, rotation, localPosition, extent));
                }
                continue;
            }

            if (surfaceLabel != PxrSemanticLabel.Floor ||
                PXR_MixedReality.GetScenePolygonData(anchorHandle, out var vertices) != PxrResult.SUCCESS || vertices.Length < 3)
            {
                continue;
            }

            var worldVertices = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                worldVertices[i] = position + rotation * new Vector3(vertices[i].x, vertices[i].y, 0f);
            }

            areas.Add(new PicoFloorArea(worldVertices, surfaceRotation: rotation));
        }

        DebugQueryStatus = $"{surfaceLabel} anchors: {areas.Count}";
        DebugFloorAreaCount = areas.Count;
        if (areas.Count > 0)
        {
            DebugGroundHeight = areas[0].SurfaceHeight;
            DebugGroundHeightMethod = surfaceLabel == PxrSemanticLabel.Table
                ? "SceneCapture table top"
                : "SceneCapture floor anchors";
            ApplyTrackerTableHeight(areas);
        }
        Debug.Log($"Scene capture query: {DebugQueryStatus}");
        RebuildDebugMeshes(areas);
        return areas;
    }

    private void ApplyTrackerTableHeight(List<PicoFloorArea> areas)
    {
        if (surfaceLabel != PxrSemanticLabel.Table || !useTrackerForTableHeight)
        {
            return;
        }

        var tracker = FindAnyObjectByType<PicoMotionTrackerPoseSource>();
        if (tracker == null)
        {
            return;
        }

        if (_tracker != tracker)
        {
            UnsubscribeFromTracker();
            _tracker = tracker;
            _tracker.PoseBecameValid += OnTrackerPoseValid;
        }

        if (!tracker.DebugPoseValid)
        {
            return;
        }

        var trackerDriver = FindAnyObjectByType<MotionTrackerDriver>();
        var trackerPosition = trackerDriver == null
            ? tracker.DebugPosition
            : trackerDriver.DebugWorldPosition;
        var height = trackerPosition.y - trackerTableHeightOffset;
        for (var i = 0; i < areas.Count; i++)
        {
            var heightDelta = height - areas[i].SurfaceHeight;
            if (i < _debugBoxes.Count)
            {
                var debugBox = _debugBoxes[i];
                debugBox.Center.y += heightDelta;
                _debugBoxes[i] = debugBox;
            }

            var vertices = (Vector3[])areas[i].Vertices.Clone();
            for (var vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                vertices[vertexIndex] += Vector3.up * heightDelta;
            }
            areas[i] = new PicoFloorArea(vertices, useHighestVertexHeight: true, surfaceRotation: areas[i].SurfaceRotation);
        }

        DebugGroundHeight = height;
        DebugGroundHeightMethod = $"Tracker height - {trackerTableHeightOffset:F2}m";
    }

    private void RebuildDebugMeshes(IReadOnlyList<PicoFloorArea> areas)
    {
        if (_debugMeshRoot == null)
        {
            _debugMeshRoot = new GameObject("FloorAreaDebugMeshes").transform;
        }
        _debugMeshRoot.gameObject.SetActive(showDebugMeshes);

        if (!showDebugMeshes)
        {
            for (var i = _debugMeshRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_debugMeshRoot.GetChild(i).gameObject);
            }
            return;
        }

        _averageHeightMaterial ??= CreateDebugMaterial(averageHeightColor);
        _verticesMaterial ??= CreateDebugMaterial(verticesColor);
        PicoFloorAreaDebugMeshBuilder.Rebuild(_debugMeshRoot, areas, _averageHeightMaterial, _verticesMaterial);
        for (var i = 0; i < _debugBoxes.Count; i++)
        {
            CreateDebugBox(_debugMeshRoot, _debugBoxes[i], i);
        }
    }

    private void CreateDebugBox(Transform root, DebugBox debugBox, int index)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = $"TableBox{index}_Debug";
        box.transform.SetParent(root, false);
        box.transform.SetPositionAndRotation(debugBox.Center, debugBox.Rotation);
        box.transform.localScale = debugBox.Extent;
        box.GetComponent<MeshRenderer>().sharedMaterial = _verticesMaterial;
        var collider = box.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private void OnValidate()
    {
        if (_debugMeshRoot != null)
        {
            _debugMeshRoot.gameObject.SetActive(showDebugMeshes);
        }
    }

    private static Material CreateDebugMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            return new Material(Shader.Find("Sprites/Default")) { color = color };
        }

        var material = new Material(shader) { color = color };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private void OnDestroy()
    {
        UnsubscribeFromAnchorUpdates();
        UnsubscribeFromTracker();
        if (_providerStarted)
        {
            PXR_MixedReality.StopSenseDataProvider(PxrSenseDataProviderType.SceneCapture);
        }

        if (_debugMeshRoot != null)
        {
            Destroy(_debugMeshRoot.gameObject);
        }

        if (_averageHeightMaterial != null)
        {
            Destroy(_averageHeightMaterial);
        }

        if (_verticesMaterial != null)
        {
            Destroy(_verticesMaterial);
        }
    }
}
