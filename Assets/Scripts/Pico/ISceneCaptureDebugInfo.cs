using UnityEngine;

public interface ISceneCaptureDebugInfo
{
    int DebugFloorAreaCount { get; }
    string DebugQueryStatus { get; }
    float DebugGroundHeight { get; }
    string DebugGroundHeightMethod { get; }
    Vector3 DebugAnchorPosition { get; }
    Vector3 DebugBoxLocalPosition { get; }
    Vector3 DebugBoxExtent { get; }
    Vector3 DebugAnchorEuler { get; }
    Vector3 DebugBoxLocalEuler { get; }
    Vector3 DebugBoxCenter { get; }
}
