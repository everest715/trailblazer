using ByteDance.PICO.XR;
using UnityEngine;

public class PicoMotionTrackerPoseSource : MonoBehaviour, IPicoTrackerPoseSource
{
    private long _trackerId = -1;
    private MotionTrackerLocation _location;
    private float _nextPoseLogTime;
    private bool _hasLoggedValidPose;

    private void Start()
    {
        PXR_MotionTracking.RequestMotionTrackerCompleteAction += OnRequestComplete;
        Debug.Log("Motion tracker request sent");
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

        Debug.Log($"Motion tracker request complete: result={data.result}, count={data.trackerCount}, id={_trackerId}");
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
            if (Time.unscaledTime >= _nextPoseLogTime)
            {
                _nextPoseLogTime = Time.unscaledTime + 2f;
                Debug.LogWarning($"Motion tracker pose unavailable: result={result}, valid={isValidPose}, id={_trackerId}");
            }

            return false;
        }

        position = _location.pose.Position.ToVector3();
        rotation = _location.pose.Orientation.ToQuat();
        if (!_hasLoggedValidPose)
        {
            _hasLoggedValidPose = true;
            Debug.Log($"Motion tracker pose acquired: {position}");
        }

        return true;
    }
}
