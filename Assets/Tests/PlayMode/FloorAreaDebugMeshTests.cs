using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FloorAreaDebugMeshTests
{
    private readonly List<GameObject> _roots = new List<GameObject>();
    private readonly List<Mesh> _meshes = new List<Mesh>();

    private static readonly Vector3[] UnevenQuad =
    {
        new Vector3(-1f, 0.1f, -1f),
        new Vector3(1f, 0.2f, -1f),
        new Vector3(1f, 0.3f, 1f),
        new Vector3(-1f, 0.4f, 1f)
    };

    [TearDown]
    public void TearDown()
    {
        foreach (var root in _roots)
        {
            if (root != null) Object.DestroyImmediate(root);
        }
        _roots.Clear();
        foreach (var mesh in _meshes)
        {
            if (mesh != null) Object.DestroyImmediate(mesh);
        }
        _meshes.Clear();
    }

    [Test]
    public void PicoFloorArea_ExposesConstructorVertices()
    {
        var area = new PicoFloorArea(UnevenQuad);

        CollectionAssert.AreEqual(UnevenQuad, area.Vertices);
    }

    [Test]
    public void CreatePlaneMesh_KeepsOriginalVertexPositions()
    {
        var mesh = PicoFloorAreaDebugMeshBuilder.CreatePlaneMesh(UnevenQuad);
        _meshes.Add(mesh);

        CollectionAssert.AreEqual(UnevenQuad, mesh.vertices);
    }

    [Test]
    public void CreatePlaneMesh_FlattensToUniformHeight()
    {
        var mesh = PicoFloorAreaDebugMeshBuilder.CreatePlaneMesh(UnevenQuad, 0.25f);
        _meshes.Add(mesh);

        foreach (var vertex in mesh.vertices)
        {
            Assert.AreEqual(0.25f, vertex.y, 1e-5f);
        }
        Assert.AreEqual(UnevenQuad[2].x, mesh.vertices[2].x);
        Assert.AreEqual(UnevenQuad[2].z, mesh.vertices[2].z);
    }

    [Test]
    public void CreatePlaneMesh_TriangulatesDoubleSided()
    {
        var mesh = PicoFloorAreaDebugMeshBuilder.CreatePlaneMesh(UnevenQuad);
        _meshes.Add(mesh);

        // quad: 2 triangles per side, double-sided = 4 triangles = 12 indices
        Assert.AreEqual(12, mesh.GetTriangles(0).Length);
    }

    [Test]
    public void Rebuild_CreatesAverageHeightAndVerticesMeshPerArea()
    {
        var root = new GameObject("DebugMeshRoot");
        _roots.Add(root);
        var areas = new List<PicoFloorArea> { new PicoFloorArea(UnevenQuad) };
        var material = new Material(Shader.Find("Sprites/Default"));

        PicoFloorAreaDebugMeshBuilder.Rebuild(root.transform, areas, material, material);

        Assert.AreEqual(2, root.transform.childCount);
        StringAssert.Contains("AverageHeight", root.transform.GetChild(0).name);
        StringAssert.Contains("Vertices", root.transform.GetChild(1).name);
        foreach (Transform child in root.transform)
        {
            Assert.IsNotNull(child.GetComponent<MeshFilter>().sharedMesh);
            Assert.IsNotNull(child.GetComponent<MeshRenderer>());
        }
        Object.DestroyImmediate(material);
    }
}
