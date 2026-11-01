using UnityEngine;

public readonly struct PicoFloorArea
{
    private readonly Vector3[] _vertices;

    public Vector3[] Vertices => _vertices;

    public float AverageHeight { get; }

    public float SurfaceHeight { get; }

    public Bounds PlanarBounds { get; }

    public Quaternion SurfaceRotation { get; }

    public PicoFloorArea(Vector3[] vertices, bool useHighestVertexHeight = false, Quaternion? surfaceRotation = null)
    {
        _vertices = vertices;
        SurfaceRotation = surfaceRotation ?? Quaternion.identity;
        AverageHeight = 0f;
        SurfaceHeight = 0f;
        PlanarBounds = new Bounds();
        if (vertices == null || vertices.Length == 0)
        {
            return;
        }

        var height = 0f;
        var highest = float.MinValue;
        var bounds = new Bounds(vertices[0], Vector3.zero);
        foreach (var vertex in vertices)
        {
            height += vertex.y;
            highest = Mathf.Max(highest, vertex.y);
            bounds.Encapsulate(vertex);
        }

        PlanarBounds = bounds;
        AverageHeight = height / vertices.Length;
        SurfaceHeight = useHighestVertexHeight ? highest : AverageHeight;
    }

    public static PicoFloorArea FromBox3D(Vector3 anchorPosition, Quaternion anchorRotation, Vector3 localPosition, Vector3 extent)
    {
        var half = extent * 0.5f;
        var bestDot = float.MinValue;
        var bestFace = new Vector3[0];
        for (var axis = 0; axis < 3; axis++)
        {
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var normal = anchorRotation * GetFaceNormal(axis, sign);
                var dot = Vector3.Dot(normal, Vector3.up);
                if (dot <= bestDot)
                {
                    continue;
                }

                bestDot = dot;
                bestFace = GetFaceVertices(axis, sign, half);
            }
        }

        for (var i = 0; i < bestFace.Length; i++)
        {
            bestFace[i] = anchorPosition + anchorRotation * (localPosition + bestFace[i]);
        }
        return new PicoFloorArea(bestFace, useHighestVertexHeight: true, surfaceRotation: anchorRotation);
    }

    private static Vector3 GetFaceNormal(int axis, int sign)
    {
        var normal = Vector3.zero;
        normal[axis] = sign;
        return normal;
    }

    private static Vector3[] GetFaceVertices(int axis, int sign, Vector3 half)
    {
        switch (axis)
        {
            case 0:
                return new[]
                {
                    new Vector3(sign * half.x, -half.y, -half.z),
                    new Vector3(sign * half.x, half.y, -half.z),
                    new Vector3(sign * half.x, half.y, half.z),
                    new Vector3(sign * half.x, -half.y, half.z)
                };
            case 1:
                return new[]
                {
                    new Vector3(-half.x, sign * half.y, -half.z),
                    new Vector3(-half.x, sign * half.y, half.z),
                    new Vector3(half.x, sign * half.y, half.z),
                    new Vector3(half.x, sign * half.y, -half.z)
                };
            default:
                return new[]
                {
                    new Vector3(-half.x, -half.y, sign * half.z),
                    new Vector3(half.x, -half.y, sign * half.z),
                    new Vector3(half.x, half.y, sign * half.z),
                    new Vector3(-half.x, half.y, sign * half.z)
                };
        }
    }

    public bool TryGetRandomPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (_vertices == null || _vertices.Length < 3)
        {
            return false;
        }

        for (var i = 0; i < 100; i++)
        {
            var x = Random.Range(PlanarBounds.min.x, PlanarBounds.max.x);
            var z = Random.Range(PlanarBounds.min.z, PlanarBounds.max.z);
            if (!ContainsXZ(x, z))
            {
                continue;
            }

            point = new Vector3(x, SurfaceHeight, z);
            return true;
        }

        return false;
    }

    public bool ContainsXZ(Vector3 point)
    {
        return ContainsXZ(point.x, point.z);
    }

    private bool ContainsXZ(float x, float z)
    {
        if (_vertices == null || _vertices.Length < 3)
        {
            return false;
        }

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
