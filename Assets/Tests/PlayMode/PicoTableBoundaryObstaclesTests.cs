using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PicoTableBoundaryObstaclesTests
{
    private readonly List<GameObject> _roots = new List<GameObject>();
    private PicoTableBoundaryObstacles _generator;

    private sealed class FakeProvider : MonoBehaviour, IPicoFloorAreaProvider, IPicoFloorAreaUpdates
    {
        public IReadOnlyList<PicoFloorArea> Areas;
        public bool Throw;
        public event Action<IReadOnlyList<PicoFloorArea>> AreasUpdated;

        public Task<IReadOnlyList<PicoFloorArea>> GetFloorAreasAsync()
        {
            if (Throw) throw new InvalidOperationException("provider failed");
            return Task.FromResult(Areas);
        }

        public void Publish(IReadOnlyList<PicoFloorArea> areas)
        {
            AreasUpdated?.Invoke(areas);
        }
    }

    [SetUp]
    public void SetUp()
    {
        var root = new GameObject("BoundaryGenerator");
        _roots.Add(root);
        _generator = root.AddComponent<PicoTableBoundaryObstacles>();
        _generator.fencePrefab = CreateModel("MFence", new Vector3(1f, 0.1f, 0.08f));
        _generator.treePrefab = CreateModel("MTree", new Vector3(0.25f, 0.6f, 0.25f));
        _generator.tree2Prefab = CreateModel("MTree2", new Vector3(0.35f, 0.8f, 0.35f));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var root in _roots)
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
        _roots.Clear();
    }

    [Test]
    public void Rebuild_Rectangle_UsesAllThreePrefabsAcrossEdges()
    {
        _generator.Rebuild(Rectangle(Quaternion.identity, Vector3.zero));

        var instances = Children().ToArray();
        Assert.Greater(_generator.GeneratedCount, 0);
        Assert.IsTrue(instances.Any(item => item.name.StartsWith("MFence")));
        Assert.IsTrue(instances.Any(item => item.name.StartsWith("MTree2") == false && item.name.StartsWith("MTree")));
        Assert.IsTrue(instances.Any(item => item.name.StartsWith("MTree2")));
    }

    [Test]
    public void Rebuild_RotatedRectangle_FenceYawStaysWithinEightDegreesAndNeverPerpendicular()
    {
        var rotation = Quaternion.Euler(0f, 37f, 0f);
        var area = Rectangle(rotation, new Vector3(3f, 0f, -2f));

        _generator.Rebuild(area);

        var fences = Children().Where(item => item.name.StartsWith("MFence")).ToArray();
        Assert.Greater(fences.Length, 0);
        foreach (var fence in fences)
        {
            var axis = fence.rotation * Vector3.right;
            var edge = NearestEdge(area.Vertices, fence.position);
            var signedAngle = Vector3.SignedAngle(edge, axis, Vector3.up);
            var parallelError = Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(signedAngle, 0f)), Mathf.Abs(Mathf.DeltaAngle(signedAngle, 180f)));
            Assert.LessOrEqual(parallelError, 8.01f, $"Unexpected Fence yaw: {signedAngle}");
        }
    }

    [Test]
    public void Rebuild_GeneratedFootprintsStayInsideRotatedArea()
    {
        var area = Rectangle(Quaternion.Euler(0f, 37f, 0f), new Vector3(3f, 0.75f, -2f));

        _generator.Rebuild(area);

        Assert.Greater(_generator.GeneratedCount, 0);
        foreach (var instance in Children())
        {
            Assert.IsTrue(PicoObstacleGeometry.TryCapture(instance.gameObject, out var bounds));
            Assert.IsTrue(PicoObstacleGeometry.FitsInside(area, bounds, instance.position, instance.rotation, instance.localScale.x, _generator.edgeSafetyMargin), instance.name);
            var bottom = instance.position.y + bounds.min.y * instance.localScale.y;
            Assert.AreEqual(area.SurfaceHeight, bottom, 0.0001f, instance.name);
        }
    }

    [Test]
    public void Rebuild_TinyOrDegenerateArea_SkipsWithoutOutsideInstances()
    {
        var tiny = new PicoFloorArea(new[]
        {
            new Vector3(-0.01f, 0.75f, -0.01f),
            new Vector3(0.01f, 0.75f, -0.01f),
            new Vector3(0.01f, 0.75f, 0.01f),
            new Vector3(-0.01f, 0.75f, 0.01f)
        });
        Assert.DoesNotThrow(() => _generator.Rebuild(tiny));
        Assert.AreEqual(0, _generator.GeneratedCount);

        var degenerate = new PicoFloorArea(new[] { Vector3.zero, Vector3.right });
        Assert.DoesNotThrow(() => _generator.Rebuild(degenerate));
        Assert.AreEqual(0, _generator.GeneratedCount);
    }

    [UnityTest]
    public IEnumerator Rebuild_CalledTwice_ReplacesOnlyOwnedInstances()
    {
        var area = Rectangle(Quaternion.identity, Vector3.zero);
        _generator.Rebuild(area);
        var oldRoot = _generator.GeneratedRoot;
        var firstCount = _generator.GeneratedCount;
        var external = new GameObject("TableBoundaryObstacles");
        _roots.Add(external);

        _generator.Rebuild(area);
        yield return null;

        Assert.IsTrue(oldRoot == null);
        Assert.AreEqual(firstCount, _generator.GeneratedCount);
        Assert.AreNotSame(oldRoot, _generator.GeneratedRoot);
        Assert.IsFalse(external == null);
        Assert.AreNotSame(external.transform, _generator.GeneratedRoot);
    }

    [Test]
    public void RebuildAsync_EmptyThenValidProvider_ClearsAndRebuilds()
    {
        var provider = _generator.gameObject.AddComponent<FakeProvider>();
        _generator.sceneCaptureProvider = provider;
        provider.Areas = Array.Empty<PicoFloorArea>();

        var emptyTask = _generator.RebuildAsync();
        Assert.IsTrue(emptyTask.IsCompleted);
        Assert.AreEqual(0, _generator.GeneratedCount);

        provider.Areas = new[] { Rectangle(Quaternion.identity, Vector3.zero) };
        var validTask = _generator.RebuildAsync();
        Assert.IsTrue(validTask.IsCompleted);
        Assert.Greater(_generator.GeneratedCount, 0);

        provider.Throw = true;
        LogAssert.Expect(LogType.Warning, "Table boundary generation failed; provider unavailable");
        var failedTask = _generator.RebuildAsync();
        Assert.IsTrue(failedTask.IsCompleted);
        Assert.IsFalse(failedTask.IsFaulted);
        Assert.AreEqual(0, _generator.GeneratedCount);
    }

    [Test]
    public void AreasUpdated_ValidThenEmpty_RebuildsAndClears()
    {
        var provider = _generator.gameObject.AddComponent<FakeProvider>();
        _generator.sceneCaptureProvider = provider;
        _generator.enabled = false;
        _generator.enabled = true;

        provider.Publish(new[] { Rectangle(Quaternion.identity, Vector3.zero) });
        Assert.Greater(_generator.GeneratedCount, 0);

        provider.Publish(Array.Empty<PicoFloorArea>());
        Assert.AreEqual(0, _generator.GeneratedCount);
    }

    private GameObject CreateModel(string name, Vector3 dimensions)
    {
        var root = new GameObject(name);
        _roots.Add(root);
        var meshObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        meshObject.name = "LOD0";
        UnityEngine.Object.DestroyImmediate(meshObject.GetComponent<Collider>());
        meshObject.transform.SetParent(root.transform, false);
        meshObject.transform.localScale = dimensions;
        return root;
    }

    private IEnumerable<Transform> Children()
    {
        return _generator.GeneratedRoot == null
            ? Enumerable.Empty<Transform>()
            : _generator.GeneratedRoot.Cast<Transform>();
    }

    private static PicoFloorArea Rectangle(Quaternion rotation, Vector3 center)
    {
        center.y = center.y == 0f ? 0.75f : center.y;
        return new PicoFloorArea(new[]
        {
            center + rotation * new Vector3(-1f, 0f, -1f),
            center + rotation * new Vector3(1f, 0f, -1f),
            center + rotation * new Vector3(1f, 0f, 1f),
            center + rotation * new Vector3(-1f, 0f, 1f)
        });
    }

    private static Vector3 NearestEdge(Vector3[] vertices, Vector3 point)
    {
        var bestDistance = float.MaxValue;
        var bestEdge = Vector3.right;
        for (var i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Length];
            var segment = b - a;
            segment.y = 0f;
            var delta = point - a;
            delta.y = 0f;
            var t = Mathf.Clamp01(Vector3.Dot(delta, segment) / segment.sqrMagnitude);
            var distance = (delta - segment * t).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestEdge = segment.normalized;
            }
        }
        return bestEdge;
    }
}
