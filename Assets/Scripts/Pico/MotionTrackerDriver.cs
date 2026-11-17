using UnityEngine;

public class MotionTrackerDriver : MonoBehaviour
{
    public Vector3 positionOffset;
    public Vector3 positionScale = Vector3.one;
    public Quaternion rotationOffset = Quaternion.identity;
    public Transform trackingOrigin;
    public Vector3 DebugWorldPosition { get; private set; }
    public bool DebugPoseValid { get; private set; }

    private Rigidbody _body;
    private IPicoTrackerPoseSource _poseSource;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _poseSource = GetComponent<IPicoTrackerPoseSource>();
    }

    public void Update()
    {
        var hasPose = _poseSource.TryGetPose(out var position, out var rotation);
        DebugPoseValid = hasPose;
        if (!hasPose)
        {
            return;
        }

        var worldPosition = trackingOrigin == null
            ? Vector3.Scale(position, positionScale) + positionOffset
            : trackingOrigin.TransformPoint(Vector3.Scale(position, positionScale) + positionOffset);
        var worldRotation = trackingOrigin == null
            ? rotation * rotationOffset
            : trackingOrigin.rotation * rotation * rotationOffset;
        DebugWorldPosition = worldPosition;
        _body.transform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    private void FixedUpdate()
    {
        var hasPose = _poseSource.TryGetPose(out var position, out var rotation);
        DebugPoseValid = hasPose;
        if (!hasPose)
        {
            return;
        }

        var worldPosition = trackingOrigin == null
            ? Vector3.Scale(position, positionScale) + positionOffset
            : trackingOrigin.TransformPoint(Vector3.Scale(position, positionScale) + positionOffset);
        var worldRotation = trackingOrigin == null
            ? rotation * rotationOffset
            : trackingOrigin.rotation * rotation * rotationOffset;
        DebugWorldPosition = worldPosition;
        _body.MovePosition(worldPosition);
        _body.MoveRotation(worldRotation);
    }
}
