using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class SceneCaptureCoordinateSpaceTests
{
    private GameObject _origin;

    [TearDown]
    public void TearDown()
    {
        if (_origin != null) Object.DestroyImmediate(_origin);
    }

    [Test]
    public void SceneAnchorPose_ConvertsTrackingSpaceToUnityWorld()
    {
        _origin = new GameObject("Camera Offset");
        _origin.transform.SetPositionAndRotation(
            new Vector3(10f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
        var method = typeof(PicoSceneCaptureFloorAreaProvider).GetMethod(
            "TransformAnchorToWorld", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method, "Scene anchors must be converted from tracking space to Unity world space.");
        var arguments = new object[]
        {
            _origin.transform,
            new Vector3(1f, 0.5f, 2f),
            Quaternion.identity
        };

        method.Invoke(null, arguments);

        Assert.Less(Vector3.Distance(new Vector3(12f, 2.5f, 2f), (Vector3)arguments[1]), 0.0001f);
        Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), (Quaternion)arguments[2]), 0.0001f);
    }
}
