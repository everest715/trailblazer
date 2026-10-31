using UnityEngine;

public interface IPicoTrackerPoseSource
{
    bool TryGetPose(out Vector3 position, out Quaternion rotation);
}
