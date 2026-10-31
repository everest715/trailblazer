using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TrackerDebugVisualTests
{
    private GameObject _car;

    [SetUp]
    public void SetUp()
    {
        _car = new GameObject("TrackerCar");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_car);
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
