using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class DebugInfoPanelTests
{
    private readonly List<GameObject> _roots = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var root in _roots)
        {
            if (root != null) Object.DestroyImmediate(root);
        }
        _roots.Clear();
    }

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
        public float DebugGroundHeight => 1.25f;
        public string DebugGroundHeightMethod => "SceneCapture floor anchors";
        public Vector3 DebugAnchorPosition => new Vector3(0f, -1.59f, 2f);
        public Vector3 DebugBoxLocalPosition => Vector3.zero;
        public Vector3 DebugBoxExtent => Vector3.zero;
        public Vector3 DebugAnchorEuler => Vector3.zero;
        public Vector3 DebugBoxLocalEuler => Vector3.zero;
        public Vector3 DebugBoxCenter => Vector3.zero;

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
    public void Start_AssignsTrackerFromScene()
    {
        var root = new GameObject("DebugRoot");
        _roots.Add(root);
        var output = root.AddComponent<TextMeshProUGUI>();
        var panel = root.AddComponent<DebugInfoPanel>();
        var trackerObject = new GameObject("TrackerObject");
        _roots.Add(trackerObject);
        var tracker = trackerObject.AddComponent<FakeTracker>();
        panel.output = output;
        panel.tracker = null;
        panel.spawners = new PicoSceneSpawnPositions[0];

        panel.SendMessage("Start");

        Assert.AreEqual(tracker, panel.tracker);
    }

    [Test]
    public void Start_FindsInactiveSpawners()
    {
        var root = new GameObject("DebugRoot");
        _roots.Add(root);
        var output = root.AddComponent<TextMeshProUGUI>();
        var panel = root.AddComponent<DebugInfoPanel>();
        var tracker = root.AddComponent<FakeTracker>();
        var spawnerObject = new GameObject("InactiveSpawner");
        _roots.Add(spawnerObject);
        spawnerObject.AddComponent<PicoSceneSpawnPositions>();
        spawnerObject.SetActive(false);
        panel.output = output;
        panel.tracker = tracker;
        panel.spawners = null;

        panel.SendMessage("Start");

        Assert.AreEqual(1, panel.spawners.Length);
    }

    [Test]
    public void Start_FindsInactiveSceneCaptureProvider()
    {
        var root = new GameObject("DebugRoot");
        _roots.Add(root);
        var output = root.AddComponent<TextMeshProUGUI>();
        var panel = root.AddComponent<DebugInfoPanel>();
        var tracker = root.AddComponent<FakeTracker>();
        var providerObject = new GameObject("InactiveProvider");
        _roots.Add(providerObject);
        providerObject.AddComponent<FakeProvider>();
        providerObject.SetActive(false);
        panel.output = output;
        panel.tracker = tracker;
        panel.spawners = new PicoSceneSpawnPositions[0];

        panel.SendMessage("Start");

        StringAssert.Contains("Ground query: Floor anchors: 1", output.text);
    }

    [Test]
    public void Refresh_UsesGameGroundHeightWhenAvailable()
    {
        var root = new GameObject("DebugRoot");
        _roots.Add(root);
        var output = root.AddComponent<TextMeshProUGUI>();
        var panel = root.AddComponent<DebugInfoPanel>();
        var tracker = root.AddComponent<FakeTracker>();
        var spawner = root.AddComponent<PicoSceneSpawnPositions>();
        root.AddComponent<FakeProvider>();
        panel.output = output;
        panel.tracker = tracker;
        panel.spawners = new[] { spawner };

        panel.SendMessage("Start");

        StringAssert.Contains("Ground detection: SceneCapture floor anchors", output.text);
        StringAssert.Contains("Ground height: 1.25", output.text);
        StringAssert.Contains("Ground query: Floor anchors: 1", output.text);
        StringAssert.Contains("Ground anchor: (0.00, -1.59, 2.00)", output.text);
        StringAssert.DoesNotContain("Not detected", output.text);
    }

    [Test]
    public void Refresh_ShowsGroundDetectionMethodAndHeight()
    {
        var root = new GameObject("DebugRoot");
        _roots.Add(root);
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
        StringAssert.Contains("Ground detection", output.text);
        StringAssert.Contains("SceneCapture floor anchors", output.text);
        StringAssert.Contains("Ground height", output.text);
        StringAssert.DoesNotContain("playArea", output.text);
        StringAssert.DoesNotContain("Floor anchors: 1", output.text);
    }
}
