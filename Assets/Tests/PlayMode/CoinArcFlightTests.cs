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
        _flight.Launch(Vector3.zero, 0.6f, 0.6f, 0.05f, SquareAroundStart(0.25f));

        yield return new WaitForSeconds(0.1f);

        var position = _coin.transform.position;
        Assert.IsFalse(float.IsNaN(position.x) || float.IsInfinity(position.x));
        Assert.IsFalse(float.IsNaN(position.y) || float.IsInfinity(position.y));
        Assert.IsFalse(float.IsNaN(position.z) || float.IsInfinity(position.z));
        Assert.Less(Vector3.Distance(StartPosition, position), 0.0001f);
        Assert.IsTrue(_collider.enabled);
    }

    [UnityTest]
    public IEnumerator Launch_TableArea_ReflectsAtEdgeAndLandsInside()
    {
        _flight.Launch(Vector3.right, 0.5f, 0.3f, 0.1f, SquareAroundStart(0.25f));

        yield return new WaitForSeconds(0.2f);

        AssertVector3Equal(StartPosition, _coin.transform.position);
        Assert.IsTrue(_collider.enabled);
    }

    [UnityTest]
    public IEnumerator Launch_TableAreaMultipleEdges_UsesRemainingDistance()
    {
        _flight.Launch(Vector3.right, 0.8f, 0.3f, 0.1f, SquareAroundStart(0.25f));

        yield return new WaitForSeconds(0.2f);

        AssertVector3Equal(StartPosition + Vector3.left * 0.2f, _coin.transform.position);
    }

    [UnityTest]
    public IEnumerator Launch_TableAreaCorner_ReflectsBackInside()
    {
        var area = SquareAroundStart(0.25f);
        var direction = new Vector3(1f, 0f, 1f).normalized;
        _flight.Launch(direction, 0.4f, 0.3f, 0.1f, area);

        yield return new WaitForSeconds(0.2f);

        var final = _coin.transform.position;
        Assert.IsFalse(float.IsNaN(final.x) || float.IsInfinity(final.x));
        Assert.IsFalse(float.IsNaN(final.y) || float.IsInfinity(final.y));
        Assert.IsFalse(float.IsNaN(final.z) || float.IsInfinity(final.z));
        Assert.IsTrue(area.ContainsXZ(final), $"Coin landed outside the table at {final}");
    }

    private static PicoFloorArea SquareAroundStart(float halfSize)
    {
        return new PicoFloorArea(new[]
        {
            new Vector3(StartPosition.x - halfSize, StartPosition.y, StartPosition.z - halfSize),
            new Vector3(StartPosition.x + halfSize, StartPosition.y, StartPosition.z - halfSize),
            new Vector3(StartPosition.x + halfSize, StartPosition.y, StartPosition.z + halfSize),
            new Vector3(StartPosition.x - halfSize, StartPosition.y, StartPosition.z + halfSize)
        });
    }

    private static void AssertVector3Equal(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, 0.001f);
        Assert.AreEqual(expected.y, actual.y, 0.001f);
        Assert.AreEqual(expected.z, actual.z, 0.001f);
    }
}
