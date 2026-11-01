using NUnit.Framework;
using UnityEngine;

public class PicoFloorAreaTests
{
    [Test]
    public void SurfaceHeight_UsesAverageHeightByDefault()
    {
        var area = new PicoFloorArea(new[]
        {
            new Vector3(-1f, 0.1f, -1f),
            new Vector3(1f, 0.2f, -1f),
            new Vector3(1f, 0.3f, 1f),
            new Vector3(-1f, 0.4f, 1f)
        });

        Assert.AreEqual(0.25f, area.SurfaceHeight, 1e-5f);
        Assert.AreEqual(area.AverageHeight, area.SurfaceHeight);
    }

    [Test]
    public void SurfaceHeight_CanUseHighestVertex()
    {
        var area = new PicoFloorArea(new[]
        {
            new Vector3(-1f, 0.1f, -1f),
            new Vector3(1f, 0.2f, -1f),
            new Vector3(1f, 0.9f, 1f),
            new Vector3(-1f, 0.4f, 1f)
        }, useHighestVertexHeight: true);

        Assert.AreEqual(0.9f, area.SurfaceHeight, 1e-5f);
    }

    [Test]
    public void FromBox3D_CreatesTopFaceAtHighestHeight()
    {
        var area = PicoFloorArea.FromBox3D(
            new Vector3(10f, 1f, 2f), Quaternion.identity,
            new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 4f));

        Assert.AreEqual(2f, area.SurfaceHeight, 1e-5f);
        Assert.AreEqual(4, area.Vertices.Length);
        Assert.AreEqual(new Vector3(10f, 2f, 2f), area.PlanarBounds.center);
        Assert.AreEqual(new Vector3(2f, 0f, 4f), area.PlanarBounds.size);
        Assert.IsTrue(area.ContainsXZ(new Vector3(10f, 0f, 2f)));
        Assert.IsFalse(area.ContainsXZ(new Vector3(12f, 0f, 2f)));
    }

    [Test]
    public void FromBox3D_AppliesAnchorRotation()
    {
        var area = PicoFloorArea.FromBox3D(
            Vector3.zero, Quaternion.Euler(0f, 90f, 0f),
            Vector3.zero, new Vector3(2f, 1f, 4f));

        Assert.AreEqual(0.5f, area.SurfaceHeight, 1e-5f);
        Assert.AreEqual(4f, area.PlanarBounds.size.x, 1e-5f);
        Assert.AreEqual(2f, area.PlanarBounds.size.z, 1e-5f);
    }

    [Test]
    public void FromBox3D_RotatedBoxUsesHighestCornerAndHorizontalTopFace()
    {
        var area = PicoFloorArea.FromBox3D(
            Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
            Vector3.zero, new Vector3(2f, 1f, 4f));

        Assert.AreEqual(2f, area.SurfaceHeight, 1e-5f);
        foreach (var vertex in area.Vertices)
        {
            Assert.AreEqual(2f, vertex.y, 1e-5f);
        }
        Assert.AreEqual(2f, area.PlanarBounds.size.x, 1e-5f);
        Assert.AreEqual(1f, area.PlanarBounds.size.z, 1e-5f);
    }

    [Test]
    public void TryGetRandomPoint_UsesSurfaceHeight()
    {
        var area = new PicoFloorArea(new[]
        {
            new Vector3(-1f, 0f, -1f),
            new Vector3(1f, 0f, -1f),
            new Vector3(1f, 0.8f, 1f),
            new Vector3(-1f, 0.8f, 1f)
        }, useHighestVertexHeight: true);

        for (var i = 0; i < 20; i++)
        {
            Assert.IsTrue(area.TryGetRandomPoint(out var point));
            Assert.AreEqual(0.8f, point.y, 1e-5f);
            Assert.IsTrue(area.ContainsXZ(point));
        }
    }
}
