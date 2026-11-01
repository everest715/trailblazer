using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TrackerDebugVisualTests
{
    private GameObject _car;
    private GameObject _rawModel;

    private sealed class FakeTracker : MonoBehaviour, ITrackerDebugInfo
    {
        public long DebugTrackerId => 1;
        public bool DebugPoseValid => true;
        public Vector3 DebugPosition { get; set; }
        public string DebugRequestStatus => "Connected";
    }

    [SetUp]
    public void SetUp()
    {
        _car = new GameObject("TrackerCar");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_car);
        if (_rawModel != null) Object.DestroyImmediate(_rawModel);
    }

    [UnityTest]
    public IEnumerator Update_RootModelUsesRawTrackerPosition()
    {
        var tracker = _car.AddComponent<FakeTracker>();
        _car.AddComponent<TrackerDebugVisual>();

        yield return null;

        _rawModel = GameObject.Find("TrackerRawDebugModel");
        Assert.IsNotNull(_rawModel);
        Assert.IsNull(_rawModel.transform.parent);

        tracker.DebugPosition = new Vector3(1f, 2f, 3f);
        yield return null;

        Assert.AreEqual(tracker.DebugPosition, _rawModel.transform.position);
        StringAssert.Contains("Sphere", _rawModel.GetComponent<MeshFilter>().sharedMesh.name);
        Assert.AreEqual(new Vector3(0.18f, 0.06f, 0.096f), _rawModel.transform.localScale);
        Assert.IsFalse(_rawModel.GetComponent<Collider>());
    }

    [UnityTest]
    public IEnumerator Start_CreatesNonCollidingModelAtCarOrigin()
    {
        _car.transform.localScale = new Vector3(0.15f, 0.05f, 0.15f);
        _car.AddComponent<TrackerDebugVisual>();

        yield return null;

        var model = _car.transform.Find("TrackerDebugModel");
        Assert.IsNotNull(model);
        Assert.AreEqual(Vector3.zero, model.localPosition);
        var worldScale = Vector3.Scale(model.localScale, _car.transform.localScale);
        Assert.AreEqual(new Vector3(0.15f, 0.05f, 0.08f), worldScale);
        Assert.IsFalse(model.GetComponentsInChildren<Collider>().Any());
        Assert.IsNotNull(model.Find("ForwardMarker"));
    }
}
