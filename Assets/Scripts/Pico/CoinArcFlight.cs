using UnityEngine;

public class CoinArcFlight : MonoBehaviour
{
    private Vector3 _start;
    private Vector3 _end;
    private float _height;
    private float _duration;
    private float _elapsed;
    private bool _isFlying;
    private Collider[] _colliders;
    private bool[] _colliderStates;

    public void Launch(Vector3 direction, float distance, float height, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
        }

        _start = transform.position;
        _end = _start + direction * distance;
        _end.y = _start.y;
        _height = height;
        _duration = Mathf.Max(0.01f, duration);
        _elapsed = 0f;

        _colliders = GetComponentsInChildren<Collider>(true);
        _colliderStates = new bool[_colliders.Length];
        for (var i = 0; i < _colliders.Length; i++)
        {
            _colliderStates[i] = _colliders[i].enabled;
            _colliders[i].enabled = false;
        }

        _isFlying = true;
    }

    private void Update()
    {
        if (!_isFlying)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        var t = Mathf.Clamp01(_elapsed / _duration);
        var position = Vector3.Lerp(_start, _end, t);
        position.y += 4f * _height * t * (1f - t);
        transform.position = position;

        if (t < 1f)
        {
            return;
        }

        for (var i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
            {
                _colliders[i].enabled = _colliderStates[i];
            }
        }

        _isFlying = false;
        enabled = false;
    }
}
