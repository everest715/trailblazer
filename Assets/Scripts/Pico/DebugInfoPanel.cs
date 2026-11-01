using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

public class DebugInfoPanel : MonoBehaviour
{
    public TextMeshProUGUI output;
    public ITrackerDebugInfo tracker;
    public MotionTrackerDriver trackerDriver;
    public PicoSceneSpawnPositions[] spawners;
    public float refreshInterval = 0.5f;

    private float _nextRefresh;
    private bool _loggedFirstRefresh;
    private ISceneCaptureDebugInfo _sceneCapture;

    private void Awake()
    {
        Debug.Log("DebugInfoPanel Awake");
    }

    private void Start()
    {
        if (output == null) output = GetComponent<TextMeshProUGUI>();
        if (tracker == null) tracker = FindObjectsOfType<MonoBehaviour>().OfType<ITrackerDebugInfo>().FirstOrDefault();
        if (trackerDriver == null) trackerDriver = FindAnyObjectByType<MotionTrackerDriver>();
        if (spawners == null || spawners.Length == 0) spawners = Resources.FindObjectsOfTypeAll<PicoSceneSpawnPositions>();
        if (_sceneCapture == null) _sceneCapture = Resources.FindObjectsOfTypeAll<MonoBehaviour>().OfType<ISceneCaptureDebugInfo>().FirstOrDefault();
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefresh)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        _nextRefresh = Time.unscaledTime + refreshInterval;
        var text = new StringBuilder();
        if (tracker == null)
        {
            text.AppendLine("Tracker: not connected");
        }
        else
        {
            text.AppendLine($"Tracker: {tracker.DebugRequestStatus} | id={tracker.DebugTrackerId} | valid={tracker.DebugPoseValid}");
            var position = trackerDriver == null ? tracker.DebugPosition : trackerDriver.DebugWorldPosition;
            text.AppendLine($"Position: {position:F2}");
        }

        if (_sceneCapture != null)
        {
            text.AppendLine($"Ground data soruce: SceneCapture");
            text.AppendLine($"Ground detection: {_sceneCapture.DebugGroundHeightMethod}");
            text.AppendLine($"Ground height: {_sceneCapture.DebugGroundHeight:F2}");
            text.AppendLine($"Ground query: {_sceneCapture.DebugQueryStatus}");
            text.AppendLine($"Ground anchor: {_sceneCapture.DebugAnchorPosition:F2}");
            text.AppendLine($"Box local: {_sceneCapture.DebugBoxLocalPosition:F2}");
            text.AppendLine($"Box extent: {_sceneCapture.DebugBoxExtent:F2}");
            text.AppendLine($"Box center: {_sceneCapture.DebugBoxCenter:F2}");
            text.AppendLine($"Anchor rot: {_sceneCapture.DebugAnchorEuler:F1}");
            text.AppendLine($"Box local rot: {_sceneCapture.DebugBoxLocalEuler:F1}");
            if (Camera.main != null)
            {
                text.AppendLine($"Headset: {Camera.main.transform.position:F2}");
            }
        }
        else
        {
            var spawner = spawners != null && spawners.Length > 0 ? spawners[0] : null;
            if (spawner != null)
            {
                text.AppendLine($"Ground data soruce: Spawner");
                text.AppendLine($"Ground detection: {spawner.LastGroundHeightMethod}");
                text.AppendLine($"Ground height: {spawner.LastGroundHeight:F2}");
            }
        }

        output.text = text.ToString();
        if (!_loggedFirstRefresh)
        {
            _loggedFirstRefresh = true;
            Debug.Log($"DebugInfoPanel refreshed:\n{output.text}");
        }
    }
}
