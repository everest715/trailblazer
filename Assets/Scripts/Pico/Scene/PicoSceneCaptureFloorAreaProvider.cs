using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ByteDance.PICO.XR;
using UnityEngine;

public class PicoSceneCaptureFloorAreaProvider : MonoBehaviour, IPicoFloorAreaProvider, ISceneCaptureDebugInfo
{
    private bool _providerStarted;
    private bool _captureRequested;

    public int DebugFloorAreaCount { get; private set; }
    public string DebugQueryStatus { get; private set; } = "Not queried";

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

            var worldVertices = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                worldVertices[i] = position + rotation * new Vector3(vertices[i].x, 0f, vertices[i].y);
            }

            areas.Add(new PicoFloorArea(worldVertices));
        }

        DebugQueryStatus = $"Floor anchors: {areas.Count}";
        DebugFloorAreaCount = areas.Count;
        return areas;
    }

    private void OnDestroy()
    {
        if (_providerStarted)
        {
            PXR_MixedReality.StopSenseDataProvider(PxrSenseDataProviderType.SceneCapture);
        }
    }
}
