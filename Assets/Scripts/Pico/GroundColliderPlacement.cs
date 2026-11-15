using UnityEngine;

public class GroundColliderPlacement : MonoBehaviour
{
    public MotionTrackerDriver tracker;
    public float trackerHeightOffset = 0.2f;

    private void Start()
    {
        if (tracker == null)
        {
            return;
        }

        transform.position = tracker.transform.position + Vector3.down * trackerHeightOffset;
    }
}
