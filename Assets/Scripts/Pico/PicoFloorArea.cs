using UnityEngine;

public readonly struct PicoFloorArea
{
    private readonly Vector3[] _vertices;

    public Vector3[] Vertices => _vertices;

    public float AverageHeight { get; }

    public float SurfaceHeight { get; }

    public Bounds PlanarBounds { get; }

    public PicoFloorArea(Vector3[] vertices, bool useHighestVertexHeight = false)
    {
        _vertices = vertices;
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
        var top = extent.y * 0.5f;
        var halfX = extent.x * 0.5f;
        var halfZ = extent.z * 0.5f;
        var vertices = new[]
        {
            localPosition + new Vector3(-halfX, top, -halfZ),
            localPosition + new Vector3(halfX, top, -halfZ),
            localPosition + new Vector3(halfX, top, halfZ),
            localPosition + new Vector3(-halfX, top, halfZ)
        };

        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = anchorPosition + anchorRotation * vertices[i];
        }

        return new PicoFloorArea(vertices, useHighestVertexHeight: true);
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
