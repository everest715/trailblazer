using UnityEngine;

public interface ITrackerDebugInfo
{
    long DebugTrackerId { get; }
    bool DebugPoseValid { get; }
    Vector3 DebugPosition { get; }
    string DebugRequestStatus { get; }
}
