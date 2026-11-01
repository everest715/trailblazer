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
        if (_sceneCapture == null) _sceneCapture = FindObjectsOfType<MonoBehaviour>().OfType<ISceneCaptureDebugInfo>().FirstOrDefault();
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
            text.AppendLine($"Ground detection: {_sceneCapture.DebugGroundHeightMethod}");
            text.AppendLine($"Ground height: {_sceneCapture.DebugGroundHeight:F2}");
        }
        else
        {
            var spawner = spawners != null && spawners.Length > 0 ? spawners[0] : null;
            if (spawner != null)
            {
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
