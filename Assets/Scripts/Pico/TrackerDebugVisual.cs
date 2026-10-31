using UnityEngine;

public class TrackerDebugVisual : MonoBehaviour
{
    public bool showDebugVisual = true;

    private void Start()
    {
        if (!showDebugVisual)
        {
            return;
        }

        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.name = "TrackerDebugModel";
        model.transform.SetParent(transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = new Vector3(0.15f, 0.05f, 0.08f);
        SetMaterial(model, new Color(0f, 0.8f, 1f));
        RemoveCollider(model);

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "ForwardMarker";
        marker.transform.SetParent(model.transform, false);
        marker.transform.localPosition = new Vector3(0f, 0.02f, 0.075f);
        marker.transform.localScale = new Vector3(0.03f, 0.02f, 0.03f);
        SetMaterial(marker, Color.yellow);
        RemoveCollider(marker);
    }

    private static void SetMaterial(GameObject target, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        var material = new Material(shader);
        material.color = color;
        target.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void RemoveCollider(GameObject target)
    {
        var collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }
}
