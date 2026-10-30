using UnityEngine;

public readonly struct PicoFloorArea
{
    private readonly Vector3[] _vertices;

    public PicoFloorArea(Vector3[] vertices)
    {
        _vertices = vertices;
    }

    public bool TryGetRandomPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (_vertices == null || _vertices.Length < 3)
        {
            return false;
        }

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minZ = float.MaxValue;
        var maxZ = float.MinValue;
        var height = 0f;
        foreach (var vertex in _vertices)
        {
            minX = Mathf.Min(minX, vertex.x);
            maxX = Mathf.Max(maxX, vertex.x);
            minZ = Mathf.Min(minZ, vertex.z);
            maxZ = Mathf.Max(maxZ, vertex.z);
            height += vertex.y;
        }

        height /= _vertices.Length;
        for (var i = 0; i < 100; i++)
        {
            var x = Random.Range(minX, maxX);
            var z = Random.Range(minZ, maxZ);
            if (!ContainsXZ(x, z))
            {
                continue;
            }

            point = new Vector3(x, height, z);
            return true;
        }

        return false;
    }

    private bool ContainsXZ(float x, float z)
    {
        var inside = false;
        for (int i = 0, j = _vertices.Length - 1; i < _vertices.Length; j = i++)
        {
            var a = _vertices[i];
            var b = _vertices[j];
            var crosses = (a.z > z) != (b.z > z)
                && x < (b.x - a.x) * (z - a.z) / (b.z - a.z) + a.x;
            if (crosses)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
