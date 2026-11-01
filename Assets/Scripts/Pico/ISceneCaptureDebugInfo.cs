using UnityEngine;

public interface ISceneCaptureDebugInfo
{
    int DebugFloorAreaCount { get; }
    string DebugQueryStatus { get; }
    float DebugGroundHeight { get; }
    string DebugGroundHeightMethod { get; }
    Vector3 DebugAnchorPosition { get; }
}
