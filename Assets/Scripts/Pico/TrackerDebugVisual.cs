using UnityEngine;

public class TrackerDebugVisual : MonoBehaviour
{
    public bool showDebugVisual = true;
    public Transform trackingOrigin;
    public GameObject debugModelPrefab;
    public bool showRawDebugModel;

    private ITrackerDebugInfo _tracker;
    private GameObject _rawModel;

    private void Start()
    {
        if (!showDebugVisual)
        {
            return;
        }

        _tracker = GetComponent<ITrackerDebugInfo>();

        var model = debugModelPrefab == null
            ? GameObject.CreatePrimitive(PrimitiveType.Cube)
            : Instantiate(debugModelPrefab);
        model.name = "TrackerDebugModel";
        model.transform.SetParent(transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        var parentScale = transform.localScale;
        model.transform.localScale = debugModelPrefab == null
            ? new Vector3(0.15f / parentScale.x, 0.05f / parentScale.y, 0.08f / parentScale.z)
            : new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);

        if (debugModelPrefab == null)
        {
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

        if (_tracker != null && showRawDebugModel)
        {
            _rawModel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _rawModel.name = "TrackerRawDebugModel";
            _rawModel.transform.position = GetWorldPosition();
            _rawModel.transform.localScale = new Vector3(0.18f, 0.06f, 0.096f);
            SetMaterial(_rawModel, Color.red);
            RemoveCollider(_rawModel);
        }
    }

    private void LateUpdate()
    {
        if (_rawModel != null && _tracker != null && showRawDebugModel)
        {
            _rawModel.transform.position = GetWorldPosition();
        }
    }

    private Vector3 GetWorldPosition()
    {
        return trackingOrigin == null
            ? _tracker.DebugPosition
            : trackingOrigin.TransformPoint(_tracker.DebugPosition);
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
