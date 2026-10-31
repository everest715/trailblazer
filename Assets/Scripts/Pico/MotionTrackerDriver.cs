using UnityEngine;

public class MotionTrackerDriver : MonoBehaviour
{
    public Vector3 positionOffset;
    public Quaternion rotationOffset = Quaternion.identity;
    public Transform trackingOrigin;
    public Vector3 DebugWorldPosition { get; private set; }

    private Rigidbody _body;
    private IPicoTrackerPoseSource _poseSource;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _poseSource = GetComponent<IPicoTrackerPoseSource>();
        if (trackingOrigin == null && Camera.main != null)
        {
            trackingOrigin = Camera.main.transform;
        }
    }

    public void Update()
    {
        if (!_poseSource.TryGetPose(out var position, out var rotation))
        {
            return;
        }

        var worldPosition = trackingOrigin == null
            ? position + positionOffset
            : trackingOrigin.TransformPoint(position + positionOffset);
        var worldRotation = trackingOrigin == null
            ? rotation * rotationOffset
            : trackingOrigin.rotation * rotation * rotationOffset;
        DebugWorldPosition = worldPosition;
        _body.transform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    private void FixedUpdate()
    {
        if (!_poseSource.TryGetPose(out var position, out var rotation))
        {
            return;
        }

        var worldPosition = trackingOrigin == null
            ? position + positionOffset
            : trackingOrigin.TransformPoint(position + positionOffset);
        var worldRotation = trackingOrigin == null
            ? rotation * rotationOffset
            : trackingOrigin.rotation * rotation * rotationOffset;
        DebugWorldPosition = worldPosition;
        _body.MovePosition(worldPosition);
        _body.MoveRotation(worldRotation);
    }
}
