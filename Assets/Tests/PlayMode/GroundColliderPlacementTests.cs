using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class GroundColliderPlacementTests
{
    private GameObject _ground;
    private GameObject _tracker;

    [TearDown]
    public void TearDown()
    {
        if (_ground != null) Object.DestroyImmediate(_ground);
        if (_tracker != null) Object.DestroyImmediate(_tracker);
    }

    [UnityTest]
    public IEnumerator Start_PlacesGroundBelowTracker()
    {
        _tracker = new GameObject("Tracker");
        _tracker.transform.position = new Vector3(1f, 2f, 3f);
        var driver = _tracker.AddComponent<MotionTrackerDriver>();
        driver.enabled = false;

        _ground = new GameObject("Ground");
        _ground.transform.position = new Vector3(10f, 10f, 10f);
        var placement = _ground.AddComponent<GroundColliderPlacement>();
        placement.tracker = driver;

        yield return null;

        var expected = new Vector3(1f, 1.8f, 3f);
        Assert.Less(Vector3.Distance(expected, _ground.transform.position), 0.0001f);

        _tracker.transform.position = new Vector3(9f, 9f, 9f);
        yield return null;

        Assert.Less(Vector3.Distance(expected, _ground.transform.position), 0.0001f);
    }

    [UnityTest]
    public IEnumerator Start_WithoutTrackerKeepsInitialPosition()
    {
        _ground = new GameObject("Ground");
        _ground.transform.position = new Vector3(4f, 5f, 6f);
        _ground.AddComponent<GroundColliderPlacement>();

        yield return null;

        Assert.AreEqual(new Vector3(4f, 5f, 6f), _ground.transform.position);
    }
}
