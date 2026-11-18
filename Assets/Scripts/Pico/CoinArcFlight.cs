using System.Collections.Generic;
using UnityEngine;

public class CoinArcFlight : MonoBehaviour
{
    private const float Epsilon = 0.00001f;
    private const int MaxBounces = 32;

    private Vector3 _start;
    private Vector3 _end;
    private Vector3[] _path;
    private float[] _segmentLengths;
    private float _pathLength;
    private float _height;
    private float _duration;
    private float _elapsed;
    private bool _isFlying;
    private Collider[] _colliders;
    private bool[] _colliderStates;

    public void Launch(Vector3 direction, float distance, float height, float duration, PicoFloorArea? area = null)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
        }

        _start = transform.position;
        _end = _start + direction * distance;
        _end.y = _start.y;
        BuildPath(area, direction, distance);
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

    private void BuildPath(PicoFloorArea? area, Vector3 direction, float distance)
    {
        if (!area.HasValue || area.Value.Vertices == null || area.Value.Vertices.Length < 3
            || direction.sqrMagnitude <= 0.0001f || distance <= Epsilon)
        {
            SetPath(new[] { _start, _end });
            return;
        }

        var vertices = area.Value.Vertices;
        var points = new List<Vector3> { _start };
        var current = ToPlanar(_start);
        var planarDirection = ToPlanar(direction).normalized;
        var remaining = distance;

        for (var bounce = 0; bounce < MaxBounces && remaining > Epsilon; bounce++)
        {
            if (!TryFindNearestEdge(current, planarDirection, vertices, remaining,
                    out var hit, out var hitDistance, out var normal))
            {
                current += planarDirection * remaining;
                remaining = 0f;
                break;
            }

            current = hit;
            remaining -= hitDistance;
            points.Add(ToWorld(current, _start.y));
            if (remaining <= Epsilon)
            {
                remaining = 0f;
                break;
            }

            if (normal.sqrMagnitude <= Epsilon)
            {
                break;
            }

            normal.Normalize();
            planarDirection = Vector2.Reflect(planarDirection, normal).normalized;
        }

        points.Add(ToWorld(current, _start.y));
        SetPath(points.ToArray());
    }

    private static bool TryFindNearestEdge(Vector2 origin, Vector2 direction, Vector3[] vertices,
        float maxDistance, out Vector2 hit, out float hitDistance, out Vector2 normal)
    {
        var nearest = float.MaxValue;
        var found = false;
        hit = Vector2.zero;
        normal = Vector2.zero;

        for (var i = 0; i < vertices.Length; i++)
        {
            var a = ToPlanar(vertices[i]);
            var b = ToPlanar(vertices[(i + 1) % vertices.Length]);
            var edge = b - a;
            if (edge.sqrMagnitude <= Epsilon)
            {
                continue;
            }

            var denominator = Cross(direction, edge);
            if (Mathf.Abs(denominator) <= Epsilon)
            {
                continue;
            }

            var delta = a - origin;
            var distance = Cross(delta, edge) / denominator;
            var edgePosition = Cross(delta, direction) / denominator;
            if (distance <= Epsilon || distance > maxDistance
                || edgePosition < -Epsilon || edgePosition > 1f + Epsilon)
            {
                continue;
            }

            var edgeNormal = new Vector2(-edge.y, edge.x).normalized;
            if (!found || distance < nearest - Epsilon)
            {
                nearest = distance;
                hit = origin + direction * distance;
                normal = edgeNormal;
                found = true;
            }
            else if (Mathf.Abs(distance - nearest) <= Epsilon)
            {
                normal += edgeNormal;
            }
        }

        hitDistance = nearest;
        return found;
    }

    private void SetPath(Vector3[] points)
    {
        _path = points;
        _segmentLengths = new float[points.Length - 1];
        _pathLength = 0f;
        for (var i = 0; i < _segmentLengths.Length; i++)
        {
            var segment = points[i + 1] - points[i];
            segment.y = 0f;
            _segmentLengths[i] = segment.magnitude;
            _pathLength += _segmentLengths[i];
        }

        _end = points[points.Length - 1];
    }

    private Vector3 EvaluatePath(float t)
    {
        if (_pathLength <= Epsilon)
        {
            return _end;
        }

        var remaining = _pathLength * t;
        for (var i = 0; i < _segmentLengths.Length; i++)
        {
            var length = _segmentLengths[i];
            if (remaining <= length || i == _segmentLengths.Length - 1)
            {
                return length <= Epsilon
                    ? _path[i + 1]
                    : Vector3.Lerp(_path[i], _path[i + 1], remaining / length);
            }

            remaining -= length;
        }

        return _end;
    }

    private void Update()
    {
        if (!_isFlying)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        var t = Mathf.Clamp01(_elapsed / _duration);
        var position = EvaluatePath(t);
        position.y = _start.y + 4f * _height * t * (1f - t);
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

    private static Vector2 ToPlanar(Vector3 point)
    {
        return new Vector2(point.x, point.z);
    }

    private static Vector3 ToWorld(Vector2 point, float y)
    {
        return new Vector3(point.x, y, point.y);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }
}
