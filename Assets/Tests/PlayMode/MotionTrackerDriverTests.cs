using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MotionTrackerDriverTests
{
    private GameObject _car;
    private Rigidbody _body;
    private FakePoseSource _source;

    private sealed class FakePoseSource : MonoBehaviour, IPicoTrackerPoseSource
    {
        public bool HasPose;
        public Vector3 Position;
        public Quaternion Rotation;

        public bool TryGetPose(out Vector3 position, out Quaternion rotation)
        {
            position = Position;
            rotation = Rotation;
            return HasPose;
        }
    }

    [SetUp]
    public void SetUp()
    {
        _car = new GameObject("TrackedCarTest");
        _body = _car.AddComponent<Rigidbody>();
        _body.isKinematic = true;
        _source = _car.AddComponent<FakePoseSource>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_car);
    }

    [UnityTest]
    public IEnumerator FixedUpdate_ValidPoseAppliesOffsets()
    {
        _source.HasPose = true;
        _source.Position = new Vector3(2f, 0f, 3f);
        _source.Rotation = Quaternion.Euler(0f, 90f, 0f);
        var driver = _car.AddComponent<MotionTrackerDriver>();
        driver.positionOffset = new Vector3(0.5f, 0f, 0f);
        driver.rotationOffset = Quaternion.Euler(0f, 15f, 0f);

        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.AreEqual(new Vector3(2.5f, 0f, 3f), _body.position);
        Assert.Less(Quaternion.Angle(_source.Rotation * driver.rotationOffset, _body.rotation), 0.01f);
    }

    [UnityTest]
    public IEnumerator FixedUpdate_InvalidPoseKeepsRigidbodyStill()
    {
        var initialPosition = new Vector3(1f, 2f, 3f);
        var initialRotation = Quaternion.Euler(0f, 30f, 0f);
        _body.position = initialPosition;
        _body.rotation = initialRotation;
        _source.HasPose = false;
        _source.Position = new Vector3(10f, 10f, 10f);
        _source.Rotation = Quaternion.Euler(0f, 180f, 0f);
        _car.AddComponent<MotionTrackerDriver>();

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.AreEqual(initialPosition, _body.position);
        Assert.Less(Quaternion.Angle(initialRotation, _body.rotation), 0.01f);
    }
}
