using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PicoObstacleGeometryTests
{
    private readonly List<GameObject> _roots = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var root in _roots)
        {
            if (root != null) Object.DestroyImmediate(root);
        }
        _roots.Clear();
    }

    [Test]
    public void AlignLongAxis_RotatedEdgeAndSmallYaw_UsesEdgeAsReference()
    {
        var edge = Quaternion.Euler(0f, 37f, 0f) * Vector3.right;

        var rotation = PicoObstacleGeometry.AlignLongAxis(Vector3.right, edge, 8f);
        var alignedAxis = rotation * Vector3.right;

        Assert.AreEqual(8f, Vector3.SignedAngle(edge, alignedAxis, Vector3.up), 0.001f);
    }

    [Test]
    public void GetLongitudinalAxis_UsesLongerHorizontalDimension()
    {
        Assert.AreEqual(Vector3.forward, PicoObstacleGeometry.GetLongitudinalAxis(new Bounds(Vector3.zero, new Vector3(2f, 1f, 3f))));
        Assert.AreEqual(Vector3.right, PicoObstacleGeometry.GetLongitudinalAxis(new Bounds(Vector3.zero, new Vector3(3f, 1f, 2f))));
    }

    [Test]
    public void FitsInside_RotatedBoundsWithRootInside_RejectsOutsideCorner()
    {
        var area = Rectangle();
        var bounds = new Bounds(Vector3.zero, new Vector3(1.8f, 0.2f, 0.2f));
        var position = new Vector3(0.4f, area.SurfaceHeight, 0f);
        Assert.IsTrue(area.ContainsXZ(position));

        var fits = PicoObstacleGeometry.FitsInside(area, bounds, position, Quaternion.Euler(0f, 45f, 0f), 1f, 0f);

        Assert.IsFalse(fits);
    }

    [Test]
    public void FitsInside_PositiveMargin_ChecksAllCorners()
    {
        var area = Rectangle();
        var bounds = new Bounds(Vector3.zero, new Vector3(1.8f, 0.2f, 0.2f));

        var fits = PicoObstacleGeometry.FitsInside(area, bounds, new Vector3(0.4f, 0.75f, 0f), Quaternion.identity, 0.5f, 0.05f);
        var entersMargin = PicoObstacleGeometry.FitsInside(area, bounds, new Vector3(0.51f, 0.75f, 0f), Quaternion.identity, 0.5f, 0.05f);

        Assert.IsTrue(fits);
        Assert.IsFalse(entersMargin);
    }

    [Test]
    public void FitsInside_EmptyAreaOrInvalidScale_ReturnsFalse()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.one);

        Assert.IsFalse(PicoObstacleGeometry.FitsInside(default, bounds, Vector3.zero, Quaternion.identity, 1f, 0f));
        Assert.IsFalse(PicoObstacleGeometry.FitsInside(Rectangle(), bounds, Vector3.zero, Quaternion.identity, 0f, 0f));
    }

    [Test]
    public void TryCapture_LodGroupAndOffsetMesh_UsesOnlyLod0InRootSpace()
    {
        var root = new GameObject("Model");
        _roots.Add(root);
        root.transform.SetPositionAndRotation(new Vector3(5f, 2f, -1f), Quaternion.Euler(0f, 37f, 0f));
        var lod0 = CreateCube("LOD0", root.transform, new Vector3(1f, 2f, 3f), new Vector3(2f, 4f, 6f));
        var lod1 = CreateCube("LOD1", root.transform, new Vector3(20f, 0f, 0f), new Vector3(30f, 30f, 30f));
        var lodGroup = root.AddComponent<LODGroup>();
        lodGroup.SetLODs(new[]
        {
            new LOD(0.6f, new[] { lod0.GetComponent<Renderer>() }),
            new LOD(0.3f, new[] { lod1.GetComponent<Renderer>() })
        });

        var captured = PicoObstacleGeometry.TryCapture(root, out var bounds);

        Assert.IsTrue(captured);
        Assert.Less(Vector3.Distance(new Vector3(1f, 2f, 3f), bounds.center), 0.0001f);
        Assert.Less(Vector3.Distance(new Vector3(2f, 4f, 6f), bounds.size), 0.0001f);
    }

    [Test]
    public void TryCapture_NoMesh_ReturnsFalse()
    {
        var root = new GameObject("Empty");
        _roots.Add(root);

        var captured = PicoObstacleGeometry.TryCapture(root, out var bounds);

        Assert.IsFalse(captured);
        Assert.IsFalse(float.IsNaN(bounds.center.x) || float.IsInfinity(bounds.center.x));
        Assert.IsFalse(float.IsNaN(bounds.size.x) || float.IsInfinity(bounds.size.x));
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;
        return cube;
    }

    private static PicoFloorArea Rectangle()
    {
        return new PicoFloorArea(new[]
        {
            new Vector3(-1f, 0.75f, -1f),
            new Vector3(1f, 0.75f, -1f),
            new Vector3(1f, 0.75f, 1f),
            new Vector3(-1f, 0.75f, 1f)
        });
    }
}
