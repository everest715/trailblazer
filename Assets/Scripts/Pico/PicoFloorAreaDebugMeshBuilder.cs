using System.Collections.Generic;
using UnityEngine;

public static class PicoFloorAreaDebugMeshBuilder
{
    public static Mesh CreatePlaneMesh(IReadOnlyList<Vector3> vertices, float? uniformHeight = null)
    {
        var mesh = new Mesh();
        var meshVertices = new Vector3[vertices.Count];
        var heightOffset = 0f;
        if (uniformHeight.HasValue && vertices.Count > 0)
        {
            var averageHeight = 0f;
            foreach (var vertex in vertices)
            {
                averageHeight += vertex.y;
            }
            averageHeight /= vertices.Count;
            heightOffset = uniformHeight.Value - averageHeight;
        }

        for (var i = 0; i < vertices.Count; i++)
        {
            meshVertices[i] = vertices[i] + Vector3.up * heightOffset;
        }

        var triangles = new List<int>();
        for (var i = 1; i < meshVertices.Length - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
            // reversed winding so the plane is visible from both sides
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i);
        }

        mesh.SetVertices(meshVertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static void Rebuild(Transform root, IReadOnlyList<PicoFloorArea> areas, Material averageHeightMaterial, Material verticesMaterial)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(root.GetChild(i).gameObject);
        }

        for (var i = 0; i < areas.Count; i++)
        {
            var vertices = areas[i].Vertices;
            if (vertices == null || vertices.Length < 3)
            {
                continue;
            }

            CreateMeshObject(root, $"FloorArea{i}_AverageHeight", CreatePlaneMesh(vertices, areas[i].AverageHeight), averageHeightMaterial);
            CreateMeshObject(root, $"FloorArea{i}_Vertices", CreatePlaneMesh(vertices), verticesMaterial);
        }
    }

    private static void CreateMeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
}
