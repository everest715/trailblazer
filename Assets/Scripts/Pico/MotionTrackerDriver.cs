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

    public void Update()
    {
        if (!_poseSource.TryGetPose(out var position, out var rotation))
        {
            return;
        }

        _body.transform.SetPositionAndRotation(position + positionOffset, rotation * rotationOffset);
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
