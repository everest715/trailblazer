using System.Collections.Generic;
using UnityEngine;

public static class PicoObstacleGeometry
{
    public static bool TryCapture(GameObject root, out Bounds localBounds)
    {
        localBounds = new Bounds();
        if (root == null)
        {
            return false;
        }

        var meshFilters = GetLodZeroMeshFilters(root);
        var hasBounds = false;
        var worldToRoot = root.transform.worldToLocalMatrix;
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            var meshBounds = meshFilter.sharedMesh.bounds;
            var localToRoot = worldToRoot * meshFilter.transform.localToWorldMatrix;
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(x, y, z));
                        var rootCorner = localToRoot.MultiplyPoint3x4(corner);
                        if (hasBounds)
                        {
                            localBounds.Encapsulate(rootCorner);
                        }
                        else
                        {
                            localBounds = new Bounds(rootCorner, Vector3.zero);
                            hasBounds = true;
                        }
                    }
                }
            }
        }

        return hasBounds;
    }

    public static Vector3 GetLongitudinalAxis(Bounds localBounds)
    {
        return localBounds.size.x >= localBounds.size.z ? Vector3.right : Vector3.forward;
    }

    public static Quaternion AlignLongAxis(Vector3 localAxis, Vector3 edgeDirection, float yawOffset)
    {
        edgeDirection.y = 0f;
        localAxis.y = 0f;
        if (edgeDirection.sqrMagnitude < 0.000001f || localAxis.sqrMagnitude < 0.000001f)
        {
            return Quaternion.identity;
        }

        var axisYaw = Mathf.Atan2(localAxis.x, localAxis.z) * Mathf.Rad2Deg;
        var edgeYaw = Mathf.Atan2(edgeDirection.x, edgeDirection.z) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, edgeYaw - axisYaw + yawOffset, 0f);
    }

    public static bool FitsInside(PicoFloorArea area, Bounds localBounds, Vector3 worldPosition, Quaternion worldRotation, float uniformScale, float edgeMargin)
    {
        if (area.Vertices == null || area.Vertices.Length < 3 || uniformScale <= 0f || !IsFinite(uniformScale) ||
            !IsFinite(worldPosition) || !IsFinite(localBounds.center) || !IsFinite(localBounds.size) ||
            !IsFinite(worldRotation.x) || !IsFinite(worldRotation.y) || !IsFinite(worldRotation.z) || !IsFinite(worldRotation.w))
        {
            return false;
        }

        for (var x = -1; x <= 1; x += 2)
        {
            for (var z = -1; z <= 1; z += 2)
            {
                var localCorner = localBounds.center + new Vector3(localBounds.extents.x * x, 0f, localBounds.extents.z * z);
                var worldCorner = worldPosition + worldRotation * (localCorner * uniformScale);
                if (!area.ContainsXZ(worldCorner, edgeMargin))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static IEnumerable<MeshFilter> GetLodZeroMeshFilters(GameObject root)
    {
        var meshFilters = root.GetComponentsInChildren<MeshFilter>(false);
        var lodGroup = root.GetComponent<LODGroup>();
        if (lodGroup == null)
        {
            return meshFilters;
        }

        var higherLodRenderers = new HashSet<Renderer>();
        var lods = lodGroup.GetLODs();
        for (var i = 1; i < lods.Length; i++)
        {
            foreach (var renderer in lods[i].renderers)
            {
                if (renderer != null) higherLodRenderers.Add(renderer);
            }
        }

        var visibleFilters = new List<MeshFilter>();
        foreach (var meshFilter in meshFilters)
        {
            var renderer = meshFilter.GetComponent<Renderer>();
            if (renderer != null && !higherLodRenderers.Contains(renderer))
            {
                visibleFilters.Add(meshFilter);
            }
        }
        return visibleFilters;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
