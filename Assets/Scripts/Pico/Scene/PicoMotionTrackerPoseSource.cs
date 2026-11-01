using ByteDance.PICO.XR;
using UnityEngine;

public class PicoMotionTrackerPoseSource : MonoBehaviour, IPicoTrackerPoseSource, ITrackerDebugInfo
{
    private long _trackerId = -1;
    private MotionTrackerLocation _location;
    private float _nextPoseLogTime;
    private bool _hasLoggedValidPose;

    public long DebugTrackerId => _trackerId;
    public bool DebugPoseValid { get; private set; }
    public Vector3 DebugPosition { get; private set; }
    public string DebugRequestStatus { get; private set; } = "Not requested";
    public event System.Action PoseBecameValid;

    private void Start()
    {
        PXR_MotionTracking.RequestMotionTrackerCompleteAction += OnRequestComplete;
        DebugRequestStatus = "Requesting";
        Debug.Log("Motion tracker request sent");
        var result = PXR_MotionTracking.CheckMotionTrackerNumber(MotionTrackerNum.ONE);
        if (result != 0)
        {
            DebugRequestStatus = $"Failed: {result}";
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
            DebugRequestStatus = "Connected";
        }
        else
        {
            _trackerId = -1;
            DebugRequestStatus = data.result == PxrResult.SUCCESS ? "Not connected" : $"Failed: {data.result}";
        }

        Debug.Log($"Motion tracker request complete: result={data.result}, count={data.trackerCount}, id={_trackerId}");
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
        DebugPoseValid = isValidPose;
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
        DebugPosition = position;
        if (!_hasLoggedValidPose)
        {
            _hasLoggedValidPose = true;
            Debug.Log($"Motion tracker pose acquired: {position}");
            PoseBecameValid?.Invoke();
        }

        return true;
    }
}
