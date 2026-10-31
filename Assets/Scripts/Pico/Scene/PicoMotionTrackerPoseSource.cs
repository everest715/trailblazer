using ByteDance.PICO.XR;
using UnityEngine;

public class PicoMotionTrackerPoseSource : MonoBehaviour, IPicoTrackerPoseSource
{
    private long _trackerId = -1;
    private MotionTrackerLocation _location;

    private void Start()
    {
        PXR_MotionTracking.RequestMotionTrackerCompleteAction += OnRequestComplete;
        var result = PXR_MotionTracking.CheckMotionTrackerNumber(MotionTrackerNum.ONE);
        if (result != 0)
        {
            Debug.LogWarning($"Motion tracker request failed: {result}");
        }
    }

    private void OnDestroy()
    {
        PXR_MotionTracking.RequestMotionTrackerCompleteAction -= OnRequestComplete;
    }

    private void OnRequestComplete(RequestMotionTrackerCompleteEventData data)
    {
        if (data.result == PxrResult.SUCCESS && data.trackerCount > 0)
        {
            _trackerId = data.trackerIds[0];
        }
    }

    public bool TryGetPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (_trackerId < 0)
        {
            return false;
        }

        var isValidPose = false;
        var result = PXR_MotionTracking.GetMotionTrackerLocation(_trackerId, ref _location, ref isValidPose);
        if (result != 0 || !isValidPose)
        {
            return false;
        }

        position = _location.pose.Position.ToVector3();
        rotation = _location.pose.Orientation.ToQuat();
        return true;
    }
}
