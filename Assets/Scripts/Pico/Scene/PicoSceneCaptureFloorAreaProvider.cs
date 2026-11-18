using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.Rendering;

public class PicoSceneCaptureFloorAreaProvider : MonoBehaviour, IPicoFloorAreaProvider, IPicoFloorAreaUpdates, ISceneCaptureDebugInfo
{
    private bool _providerStarted;
    private bool _captureRequested;
    private bool _eventsSubscribed;
    private bool _isQuerying;
    private bool _refreshPending;
    private readonly SemaphoreSlim _sceneCaptureOperations = new SemaphoreSlim(1, 1);
    private PicoMotionTrackerPoseSource _tracker;

    [Header("Scene Capture")]
    public PxrSemanticLabel surfaceLabel = PxrSemanticLabel.Floor;
    public Transform trackingOrigin;

    [Header("Virtual Table Fallback")]
    public bool useVirtualTableFallback = true;
    public Vector3 virtualTableCenter = new Vector3(0f, 0f, 1.5f);
    public Vector2 virtualTableSize = new Vector2(1.5f, 1.5f);
    public float virtualTableYaw;

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
    public string DebugCaptureDetails { get; private set; } = "Not queried";

    public async Task<IReadOnlyList<PicoFloorArea>> GetFloorAreasAsync()
    {
        await _sceneCaptureOperations.WaitAsync();
        try
        {
            return await GetFloorAreasCoreAsync();
        }
        finally
        {
            _sceneCaptureOperations.Release();
        }
    }

    private async Task<IReadOnlyList<PicoFloorArea>> GetFloorAreasCoreAsync()
    {
        try
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
            if (ShouldStartSceneCapture(areas.Count, _captureRequested))
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

            return ApplyVirtualTableFallback(areas, "No table anchors after scene capture");
        }
        catch (Exception exception)
        {
            if (surfaceLabel != PxrSemanticLabel.Table || !useVirtualTableFallback)
            {
                throw;
            }
            Debug.LogWarning($"Scene capture unavailable; using virtual table: {exception.Message}");
            return ApplyVirtualTableFallback(new List<PicoFloorArea>(), exception.Message);
        }
    }

    private static bool ShouldStartSceneCapture(int areaCount, bool captureRequested)
    {
        return areaCount == 0 && !captureRequested;
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
        await _sceneCaptureOperations.WaitAsync();
        try
        {
            var areas = await QueryFloorAreasAsync();
            areas = ApplyVirtualTableFallback(areas, "Scene anchor update contains no table");
            AreasUpdated?.Invoke(areas);
        }
        catch (Exception exception)
        {
            if (surfaceLabel == PxrSemanticLabel.Table && useVirtualTableFallback)
            {
                var fallback = ApplyVirtualTableFallback(new List<PicoFloorArea>(), exception.Message);
                if (fallback.Count > 0)
                {
                    AreasUpdated?.Invoke(fallback);
                }
            }
            Debug.LogWarning($"Scene anchor update failed: {exception.Message}");
        }
        finally
        {
            _sceneCaptureOperations.Release();
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

    protected virtual async Task<List<PicoFloorArea>> QueryFloorAreasAsync()
    {
        var details = new StringBuilder();
        details.AppendLine($"Snapshot frame={Time.frameCount} time={Time.unscaledTime:F3}");
        details.AppendLine($"Config label={surfaceLabel} trackerOverride={useTrackerForTableHeight} trackerOffset={trackerTableHeightOffset:F4} debugMeshes={showDebugMeshes}");
        var query = await PXR_MixedReality.QuerySceneAnchorAsync(new[] { surfaceLabel });
        details.AppendLine($"Query result={query.result} returnedAnchors={query.anchorHandleList?.Count ?? 0}");
        if (query.result != PxrResult.SUCCESS)
        {
            DebugQueryStatus = $"Query failed: {query.result}";
            DebugCaptureDetails = details.ToString();
            throw new InvalidOperationException($"QuerySceneAnchor failed: {query.result}");
        }

        var areas = new List<PicoFloorArea>();
        var rawAreas = new List<PicoFloorArea>();
        var anchorOrigin = ResolveTrackingOrigin();
        _debugBoxes.Clear();
        for (var anchorIndex = 0; anchorIndex < query.anchorHandleList.Count; anchorIndex++)
        {
            var anchorHandle = query.anchorHandleList[anchorIndex];
            var locateResult = PXR_MixedReality.LocateAnchor(anchorHandle, out var position, out var rotation);
            var semanticResult = PXR_MixedReality.GetSceneSemanticLabel(anchorHandle, out var semanticLabel);
            var componentResult = PXR_MixedReality.GetSceneAnchorComponentTypes(anchorHandle, out var componentTypes);
            details.AppendLine($"Anchor[{anchorIndex}] handle={anchorHandle} locate={locateResult}");
            details.AppendLine($"  Locate pos={position:F4} rotQ={rotation.ToString("F4")} rotE={rotation.eulerAngles:F2}");
            details.AppendLine($"  Semantic result={semanticResult} label={semanticLabel} componentsResult={componentResult} types={string.Join(",", componentTypes)}");
            if (locateResult != PxrResult.SUCCESS)
            {
                continue;
            }

            TransformAnchorToWorld(anchorOrigin, ref position, ref rotation);
            details.AppendLine($"  World pos={position:F4} rotQ={rotation.ToString("F4")} origin={(anchorOrigin == null ? "null" : anchorOrigin.name)}");
            if (areas.Count == 0)
            {
                DebugAnchorPosition = position;
                DebugAnchorEuler = rotation.eulerAngles;
            }

            if (surfaceLabel == PxrSemanticLabel.Table)
            {
                var boxResult = PXR_MixedReality.GetSceneBox3DData(anchorHandle, out var localPosition, out var localRotation, out var extent);
                details.AppendLine($"  Box3D result={boxResult} localPos={localPosition:F4} extent={extent:F4}");
                details.AppendLine($"  Box3D localRotQ={localRotation.ToString("F4")} localRotE={localRotation.eulerAngles:F2}");
                if (boxResult == PxrResult.SUCCESS)
                {
                    DebugBoxLocalPosition = localPosition;
                    DebugBoxExtent = extent;
                    DebugBoxLocalEuler = localRotation.eulerAngles;
                    DebugBoxCenter = position + rotation * localPosition;
                    var sdkTopCenter = DebugBoxCenter + rotation * (Vector3.up * extent.y * 0.5f);
                    details.AppendLine($"  Box3D worldCenter={DebugBoxCenter:F4} usedWorldRotQ={rotation.ToString("F4")}");
                    details.AppendLine($"  SDK topCenter(no localRot)={sdkTopCenter:F4} topY={sdkTopCenter.y:F4}");
                    _debugBoxes.Add(new DebugBox
                    {
                        Center = position + rotation * localPosition,
                        Rotation = rotation,
                        Extent = extent
                    });
                    var tableArea = PicoFloorArea.FromSceneCaptureBox3D(position, rotation, localPosition, extent);
                    areas.Add(tableArea);
                    rawAreas.Add(tableArea);
                }
                continue;
            }

            Vector2[] vertices = null;
            var polygonResult = PxrResult.ERROR_HANDLE_INVALID;
            if (surfaceLabel == PxrSemanticLabel.Floor)
            {
                polygonResult = PXR_MixedReality.GetScenePolygonData(anchorHandle, out vertices);
            }
            details.AppendLine($"  Polygon result={polygonResult} localVertices={(vertices?.Length ?? 0)}");
            if (surfaceLabel != PxrSemanticLabel.Floor || polygonResult != PxrResult.SUCCESS || vertices == null || vertices.Length < 3)
            {
                continue;
            }

            var worldVertices = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                worldVertices[i] = position + rotation * new Vector3(vertices[i].x, vertices[i].y, 0f);
            }

            var floorArea = new PicoFloorArea(worldVertices, surfaceRotation: rotation);
            areas.Add(floorArea);
            rawAreas.Add(floorArea);
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
        details.AppendLine($"Final height={DebugGroundHeight:F4} method={DebugGroundHeightMethod}");
        for (var areaIndex = 0; areaIndex < areas.Count; areaIndex++)
        {
            var rawArea = rawAreas[areaIndex];
            var finalArea = areas[areaIndex];
            details.AppendLine($"Area[{areaIndex}] rawHeight={rawArea.SurfaceHeight:F4} finalHeight={finalArea.SurfaceHeight:F4} delta={finalArea.SurfaceHeight - rawArea.SurfaceHeight:F4}");
            details.AppendLine($"  Raw avgHeight={rawArea.AverageHeight:F4} boundsCenter={rawArea.PlanarBounds.center:F4} boundsSize={rawArea.PlanarBounds.size:F4}");
            details.Append("  Raw worldVertices:");
            for (var vertexIndex = 0; vertexIndex < rawArea.Vertices.Length; vertexIndex++)
            {
                details.Append($" {rawArea.Vertices[vertexIndex]:F4}");
            }
            details.AppendLine();
            details.Append("  Final worldVertices:");
            for (var vertexIndex = 0; vertexIndex < finalArea.Vertices.Length; vertexIndex++)
            {
                details.Append($" {finalArea.Vertices[vertexIndex]:F4}");
            }
            details.AppendLine();
        }
        DebugCaptureDetails = details.ToString();
        Debug.Log($"Scene capture query: {DebugQueryStatus}\n{DebugCaptureDetails}");
        RebuildDebugMeshes(areas);
        return areas;
    }

    private Transform ResolveTrackingOrigin()
    {
        if (trackingOrigin != null)
        {
            return trackingOrigin;
        }

        var trackerDriver = FindAnyObjectByType<MotionTrackerDriver>();
        return trackerDriver == null ? null : trackerDriver.trackingOrigin;
    }

    private static void TransformAnchorToWorld(Transform origin, ref Vector3 position, ref Quaternion rotation)
    {
        if (origin == null)
        {
            return;
        }

        position = origin.TransformPoint(position);
        rotation = origin.rotation * rotation;
    }

    private List<PicoFloorArea> ApplyVirtualTableFallback(List<PicoFloorArea> areas, string reason)
    {
        if (areas != null && areas.Count > 0)
        {
            return areas;
        }
        if (surfaceLabel != PxrSemanticLabel.Table || !useVirtualTableFallback)
        {
            return areas;
        }

        areas ??= new List<PicoFloorArea>();
        if (!PicoFloorArea.TryCreateRectangle(virtualTableCenter, virtualTableSize, virtualTableYaw, out var virtualArea))
        {
            Debug.LogWarning("Virtual table fallback config invalid", this);
            DebugFloorAreaCount = 0;
            DebugQueryStatus = $"Virtual table config invalid: {reason}";
            DebugGroundHeightMethod = "Virtual table config invalid";
            DebugCaptureDetails = $"Virtual table config invalid\nReason: {reason}";
            RebuildDebugMeshes(areas);
            return areas;
        }

        var fallback = new List<PicoFloorArea> { virtualArea };
        DebugFloorAreaCount = 1;
        DebugGroundHeight = virtualArea.SurfaceHeight;
        DebugGroundHeightMethod = "Virtual table fallback";
        DebugQueryStatus = $"Virtual table fallback: {reason}";
        var details = new StringBuilder();
        details.AppendLine($"Virtual table fallback reason={reason}");
        details.AppendLine($"Virtual center={virtualTableCenter:F4} size={virtualTableSize:F4} yaw={virtualTableYaw:F2}");
        details.AppendLine($"Final height={DebugGroundHeight:F4} method={DebugGroundHeightMethod}");
        for (var i = 0; i < virtualArea.Vertices.Length; i++)
        {
            details.AppendLine($"  Virtual worldVertex[{i}]={virtualArea.Vertices[i]:F4}");
        }
        DebugCaptureDetails = details.ToString();
        RebuildDebugMeshes(fallback);
        return fallback;
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
