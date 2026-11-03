using UnityEngine;

public class BouncePickUp : MonoBehaviour
{
    public float height = 0.01f;
    public float speed = 1f;

    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.localPosition;
    }

    private void Update()
    {
        var newY = _startPosition.y + (Mathf.Sin(Time.time * speed) + 1) * height;
        transform.localPosition = new Vector3(_startPosition.x, newY, _startPosition.z);
    }
}
