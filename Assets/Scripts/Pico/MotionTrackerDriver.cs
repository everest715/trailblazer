using UnityEngine;

public class MotionTrackerDriver : MonoBehaviour
{
    public Vector3 positionOffset;
    public Quaternion rotationOffset = Quaternion.identity;

    private Rigidbody _body;
    private IPicoTrackerPoseSource _poseSource;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _poseSource = GetComponent<IPicoTrackerPoseSource>();
    }

    private void FixedUpdate()
    {
        if (!_poseSource.TryGetPose(out var position, out var rotation))
        {
            return;
        }

        _body.MovePosition(position + positionOffset);
        _body.MoveRotation(rotation * rotationOffset);
    }
}
