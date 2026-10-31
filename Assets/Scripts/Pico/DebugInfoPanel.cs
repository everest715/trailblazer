using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

public class DebugInfoPanel : MonoBehaviour
{
    public TextMeshProUGUI output;
    public ITrackerDebugInfo tracker;
    public PicoSceneSpawnPositions[] spawners;
    public float refreshInterval = 0.5f;

    private float _nextRefresh;
    private bool _loggedFirstRefresh;

    private void Awake()
    {
        Debug.Log("DebugInfoPanel Awake");
    }

    private void Start()
    {
        if (output == null) output = GetComponent<TextMeshProUGUI>();
        if (tracker == null) tracker = FindObjectsOfType<MonoBehaviour>().OfType<ITrackerDebugInfo>().FirstOrDefault();
        if (spawners == null || spawners.Length == 0) spawners = Resources.FindObjectsOfTypeAll<PicoSceneSpawnPositions>();
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
            text.AppendLine($"Position: {tracker.DebugPosition:F2}");
        }

        if (spawners != null)
        {
            for (var i = 0; i < spawners.Length; i++)
            {
                var spawner = spawners[i];
                var provider = spawner.GetComponent<ISceneCaptureDebugInfo>();
                text.AppendLine($"Spawn {i + 1}: {spawner.LastSpawnSource} | {provider?.DebugQueryStatus}");
                if (provider != null)
                {
                    text.AppendLine($"Floor anchors: {provider.DebugFloorAreaCount}");
                }
                text.AppendLine($"Ground height: {spawner.LastGroundHeightMethod} | y={spawner.LastGroundHeight:F2}");
                text.AppendLine($"Last point: {spawner.LastSpawnPoint:F2}");
                text.AppendLine($"playArea: c={spawner.playAreaBounds.center:F2} s={spawner.playAreaBounds.size:F2}");
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
