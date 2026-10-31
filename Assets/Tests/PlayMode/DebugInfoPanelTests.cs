using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class DebugInfoPanelTests
{
    private sealed class FakeTracker : MonoBehaviour, ITrackerDebugInfo
    {
        public long DebugTrackerId => 7;
        public bool DebugPoseValid => true;
        public Vector3 DebugPosition => new Vector3(1f, 0f, 2f);
        public string DebugRequestStatus => "Connected";
    }

    private sealed class FakeProvider : MonoBehaviour, IPicoFloorAreaProvider, ISceneCaptureDebugInfo
    {
        public int DebugFloorAreaCount => 1;
        public string DebugQueryStatus => "Floor anchors: 1";

        public Task<IReadOnlyList<PicoFloorArea>> GetFloorAreasAsync()
        {
            return Task.FromResult<IReadOnlyList<PicoFloorArea>>(new[]
            {
                new PicoFloorArea(new[]
                {
                    new Vector3(-1f, 0f, -1f),
                    new Vector3(1f, 0f, -1f),
                    new Vector3(1f, 0f, 1f),
                    new Vector3(-1f, 0f, 1f)
                })
            });
        }
    }

    [Test]
    public void Refresh_ShowsTrackerAndSceneCaptureInfo()
    {
        var root = new GameObject("DebugRoot");
        var output = root.AddComponent<TextMeshProUGUI>();
        var panel = root.AddComponent<DebugInfoPanel>();
        var tracker = root.AddComponent<FakeTracker>();
        var spawner = root.AddComponent<PicoSceneSpawnPositions>();
        var provider = root.AddComponent<FakeProvider>();
        spawner.SpawnObject = new GameObject("Probe");
        spawner.SpawnAmount = 1;
        spawner.fallback = root.AddComponent<PicoSpawnPositions>();
        spawner.fallback.SpawnObject = spawner.SpawnObject;
        spawner.fallback.SpawnAmount = 1;
        panel.output = output;
        panel.tracker = tracker;
        panel.spawners = new[] { spawner };

        spawner.StartSpawn();
        panel.Refresh();

        StringAssert.Contains("Tracker", output.text);
        StringAssert.Contains("SceneCapture", output.text);
        StringAssert.Contains("Floor anchors: 1", output.text);
        StringAssert.Contains("playArea", output.text);
        Object.DestroyImmediate(root);
    }
}
