using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CoinArcFlightTests
{
    private static readonly Vector3 StartPosition = new Vector3(1f, 2f, 3f);

    private GameObject _coin;
    private SphereCollider _collider;
    private CoinArcFlight _flight;

    [SetUp]
    public void SetUp()
    {
        _coin = new GameObject("CoinArcFlightProbe");
        _coin.transform.position = StartPosition;
        _collider = _coin.AddComponent<SphereCollider>();
        _flight = _coin.AddComponent<CoinArcFlight>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_coin);
    }

    [UnityTest]
    public IEnumerator Launch_DisablesColliderUntilLanding()
    {
        _flight.Launch(Vector3.back, 0.6f, 0.6f, 0.2f);

        Assert.IsFalse(_collider.enabled);
        yield return null;
        Assert.IsFalse(_collider.enabled);
        yield return new WaitForSeconds(0.25f);

        Assert.IsTrue(_collider.enabled);
    }

    [UnityTest]
    public IEnumerator Launch_MovesUpAndBackwardThenLandsAtTarget()
    {
        _flight.Launch(Vector3.back, 0.6f, 0.6f, 0.4f);

        yield return new WaitForSeconds(0.2f);

        Assert.Greater(_coin.transform.position.y, StartPosition.y + 0.1f);
        Assert.Less(_coin.transform.position.z, StartPosition.z);
        yield return new WaitForSeconds(0.25f);

        Assert.Less(Vector3.Distance(StartPosition + Vector3.back * 0.6f,
            _coin.transform.position), 0.0001f);
    }

    [UnityTest]
    public IEnumerator Launch_RestoresOnlyOriginallyEnabledColliders()
    {
        var child = new GameObject("DisabledCollider");
        child.transform.SetParent(_coin.transform);
        var childCollider = child.AddComponent<BoxCollider>();
        childCollider.enabled = false;

        _flight.Launch(Vector3.back, 0.6f, 0.6f, 0.05f);

        Assert.IsFalse(_collider.enabled);
        Assert.IsFalse(childCollider.enabled);
        yield return new WaitForSeconds(0.1f);

        Assert.IsTrue(_collider.enabled);
        Assert.IsFalse(childCollider.enabled);
    }

    [UnityTest]
    public IEnumerator Launch_ZeroDuration_LandsImmediately()
    {
        _flight.Launch(Vector3.back, 0.6f, 0.6f, 0f);

        Assert.IsFalse(_collider.enabled);
        yield return new WaitForSeconds(0.02f);

        Assert.Less(Vector3.Distance(StartPosition + Vector3.back * 0.6f,
            _coin.transform.position), 0.0001f);
        Assert.IsTrue(_collider.enabled);
    }

    [UnityTest]
    public IEnumerator Launch_ZeroDirection_DoesNotProduceNaN()
    {
        _flight.Launch(Vector3.zero, 0.6f, 0.6f, 0.05f);

        yield return new WaitForSeconds(0.1f);

        var position = _coin.transform.position;
        Assert.IsFalse(float.IsNaN(position.x) || float.IsInfinity(position.x));
        Assert.IsFalse(float.IsNaN(position.y) || float.IsInfinity(position.y));
        Assert.IsFalse(float.IsNaN(position.z) || float.IsInfinity(position.z));
        Assert.Less(Vector3.Distance(StartPosition, position), 0.0001f);
        Assert.IsTrue(_collider.enabled);
    }
}
