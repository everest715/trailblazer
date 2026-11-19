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

        Assert.AreEqual(0.22f, area.SurfaceHeight, 1e-5f);
        Assert.AreEqual(area.AverageHeight - 0.03f, area.SurfaceHeight);
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

        Assert.AreEqual(0.87f, area.SurfaceHeight, 1e-5f);
    }

    [Test]
    public void FromBox3D_CreatesTopFaceAtHighestHeight()
    {
        var area = PicoFloorArea.FromBox3D(
            new Vector3(10f, 1f, 2f), Quaternion.identity,
            new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 4f));

        Assert.AreEqual(1.97f, area.SurfaceHeight, 1e-5f);
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

        Assert.AreEqual(Quaternion.Euler(0f, 90f, 0f), area.SurfaceRotation);

        Assert.AreEqual(0.47f, area.SurfaceHeight, 1e-5f);
        Assert.AreEqual(4f, area.PlanarBounds.size.x, 1e-5f);
        Assert.AreEqual(2f, area.PlanarBounds.size.z, 1e-5f);
    }

    [Test]
    public void FromBox3D_RotatedBoxUsesHighestCornerAndHorizontalTopFace()
    {
        var area = PicoFloorArea.FromBox3D(
            Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
            Vector3.zero, new Vector3(2f, 1f, 4f));

        Assert.AreEqual(1.97f, area.SurfaceHeight, 1e-5f);
        foreach (var vertex in area.Vertices)
        {
            Assert.AreEqual(2f, vertex.y, 1e-5f);
        }
        Assert.AreEqual(2f, area.PlanarBounds.size.x, 1e-5f);
        Assert.AreEqual(1f, area.PlanarBounds.size.z, 1e-5f);
    }

    [Test]
    public void FromSceneCaptureBox3D_OfficialTableSample_KeepsTableTopCenterAtAnchorHeight()
    {
        var area = PicoFloorArea.FromSceneCaptureBox3D(
            new Vector3(-0.68f, 0.86f, 2.44f),
            new Quaternion(0.10730f, -0.69975f, -0.69748f, -0.11112f),
            new Vector3(0f, 0f, -0.37f),
            new Vector3(1.47f, 4.56f, 0.74f));

        Assert.AreEqual(0.86f, area.AverageHeight, 0.0001f);
    }

    [Test]
    public void ContainsXZ_PositiveMargin_RejectsNearEdgeButKeepsCenter()
    {
        var area = Rectangle(1f, 0.2f);

        Assert.IsTrue(area.ContainsXZ(Vector3.zero, 0.2f));
        Assert.IsFalse(area.ContainsXZ(new Vector3(0.9f, 0.2f, 0f), 0.2f));
    }

    [Test]
    public void TryGetRandomPoint_PositiveMargin_ReturnsOnlySafePoints()
    {
        var area = Rectangle(1f, 0.2f);

        for (var i = 0; i < 20; i++)
        {
            Assert.IsTrue(area.TryGetRandomPoint(0.2f, out var point));
            Assert.IsTrue(area.ContainsXZ(point, 0.2f));
            Assert.AreEqual(0.17f, point.y, 0.0001f);
        }
    }

    [Test]
    public void TryGetRandomPoint_MarginExceedsTable_ReturnsFalse()
    {
        var area = Rectangle(0.1f, 0.2f);

        Assert.IsFalse(area.TryGetRandomPoint(0.2f, out var point));
        Assert.AreEqual(Vector3.zero, point);
    }

    [Test]
    public void ContainsXZ_DegenerateEdgeAndZeroMargin_DoesNotReturnNaN()
    {
        var area = new PicoFloorArea(new[] { Vector3.zero, Vector3.zero, Vector3.right });

        Assert.DoesNotThrow(() => area.ContainsXZ(Vector3.zero, 0f));
        Assert.IsFalse(area.TryGetRandomPoint(0.1f, out var point));
        Assert.IsFalse(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z));
        Assert.IsFalse(float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z));
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
            Assert.AreEqual(0.77f, point.y, 1e-5f);
            Assert.IsTrue(area.ContainsXZ(point));
        }
    }

    [Test]
    public void TryCreateRectangle_RotatedSizeAndHeight_ReturnsExpectedWorldVertices()
    {
        var created = PicoFloorArea.TryCreateRectangle(new Vector3(1f, 0.4f, 2f), new Vector2(2f, 1f), 90f, out var area);

        Assert.IsTrue(created);
        var expected = new[]
        {
            new Vector3(0.5f, 0.4f, 3f),
            new Vector3(0.5f, 0.4f, 1f),
            new Vector3(1.5f, 0.4f, 1f),
            new Vector3(1.5f, 0.4f, 3f)
        };
        Assert.AreEqual(4, area.Vertices.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Less(Vector3.Distance(expected[i], area.Vertices[i]), 0.0001f);
        }
        Assert.AreEqual(0.37f, area.SurfaceHeight, 0.0001f);
        Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), area.SurfaceRotation), 0.001f);
        Assert.Less(Vector3.Distance(new Vector3(1f, 0.4f, 2f), area.PlanarBounds.center), 0.0001f);
        Assert.Less(Vector3.Distance(new Vector3(1f, 0f, 2f), area.PlanarBounds.size), 0.0001f);
    }

    [Test]
    public void TryCreateRectangle_RandomPointsStayInside()
    {
        Assert.IsTrue(PicoFloorArea.TryCreateRectangle(new Vector3(1f, 0.4f, 2f), new Vector2(2f, 1f), 37f, out var area));

        for (var i = 0; i < 20; i++)
        {
            Assert.IsTrue(area.TryGetRandomPoint(out var point));
            Assert.IsTrue(area.ContainsXZ(point));
            Assert.AreEqual(0.37f, point.y, 0.0001f);
        }
    }

    [Test]
    public void TryCreateRectangle_ZeroNegativeOrNonFiniteInput_ReturnsFalse()
    {
        Assert.IsFalse(PicoFloorArea.TryCreateRectangle(Vector3.zero, Vector2.zero, 0f, out var area));
        Assert.IsNull(area.Vertices);
        Assert.IsFalse(PicoFloorArea.TryCreateRectangle(Vector3.zero, new Vector2(-1f, 1f), 0f, out _));
        Assert.IsFalse(PicoFloorArea.TryCreateRectangle(new Vector3(float.NaN, 0f, 0f), Vector2.one, 0f, out _));
        Assert.IsFalse(PicoFloorArea.TryCreateRectangle(Vector3.zero, new Vector2(float.PositiveInfinity, 1f), 0f, out _));
        Assert.IsFalse(PicoFloorArea.TryCreateRectangle(Vector3.zero, Vector2.one, float.NaN, out _));
    }

    private static PicoFloorArea Rectangle(float halfSize, float height)
    {
        return new PicoFloorArea(new[]
        {
            new Vector3(-halfSize, height, -halfSize),
            new Vector3(halfSize, height, -halfSize),
            new Vector3(halfSize, height, halfSize),
            new Vector3(-halfSize, height, halfSize)
        });
    }
}
