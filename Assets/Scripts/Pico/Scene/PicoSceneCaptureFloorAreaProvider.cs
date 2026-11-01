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

    [Header("Debug Meshes")]
    public bool showDebugMeshes = true;
    public Color averageHeightColor = new Color(0.1f, 1f, 0.3f, 0.35f);
    public Color verticesColor = new Color(1f, 0.25f, 0.2f, 0.35f);

    private Transform _debugMeshRoot;
    private Material _averageHeightMaterial;
    private Material _verticesMaterial;

    public int DebugFloorAreaCount { get; private set; }
    public string DebugQueryStatus { get; private set; } = "Not queried";
    public float DebugGroundHeight { get; private set; }
    public string DebugGroundHeightMethod { get; private set; } = "Not detected";
    public Vector3 DebugAnchorPosition { get; private set; }

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

    private async Task<List<PicoFloorArea>> QueryFloorAreasAsync()
    {
        var query = await PXR_MixedReality.QuerySceneAnchorAsync(new[] { PxrSemanticLabel.Floor });
        if (query.result != PxrResult.SUCCESS)
        {
            DebugQueryStatus = $"Query failed: {query.result}";
            throw new InvalidOperationException($"QuerySceneAnchor failed: {query.result}");
        }

        var areas = new List<PicoFloorArea>();
        foreach (var anchorHandle in query.anchorHandleList)
        {
            if (PXR_MixedReality.GetScenePolygonData(anchorHandle, out var vertices) != PxrResult.SUCCESS || vertices.Length < 3)
            {
                continue;
            }

            if (PXR_MixedReality.LocateAnchor(anchorHandle, out var position, out var rotation) != PxrResult.SUCCESS)
            {
                continue;
            }

            if (areas.Count == 0)
            {
                DebugAnchorPosition = position;
            }

            var worldVertices = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                worldVertices[i] = position + rotation * new Vector3(vertices[i].x, vertices[i].y, 0f);
            }

            areas.Add(new PicoFloorArea(worldVertices));
        }

        DebugQueryStatus = $"Floor anchors: {areas.Count}";
        DebugFloorAreaCount = areas.Count;
        if (areas.Count > 0)
        {
            DebugGroundHeight = areas[0].AverageHeight;
            DebugGroundHeightMethod = "SceneCapture floor anchors";
        }
        Debug.Log($"Scene capture query: {DebugQueryStatus}");
        RebuildDebugMeshes(areas);
        return areas;
    }

    private void RebuildDebugMeshes(IReadOnlyList<PicoFloorArea> areas)
    {
        if (_debugMeshRoot == null)
        {
            _debugMeshRoot = new GameObject("FloorAreaDebugMeshes").transform;
        }

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
