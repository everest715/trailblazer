using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TableEnemyTests
{
    private GameObject _enemy;
    private GameObject _tracker;
    private TableEnemy _tableEnemy;
    private MotionTrackerDriver _driver;
    private FakePoseSource _source;

    private sealed class FakePoseSource : MonoBehaviour, IPicoTrackerPoseSource
    {
        public bool HasPose = true;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;

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
        _tracker = new GameObject("EnemyTestTracker");
        var trackerBody = _tracker.AddComponent<Rigidbody>();
        trackerBody.isKinematic = true;
        trackerBody.useGravity = false;
        _source = _tracker.AddComponent<FakePoseSource>();
        _driver = _tracker.AddComponent<MotionTrackerDriver>();

        _enemy = new GameObject("TableEnemyTest");
        _enemy.transform.position = new Vector3(0f, 0.4f, 0f);
        var body = _enemy.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        _enemy.AddComponent<CapsuleCollider>();
        _tableEnemy = _enemy.AddComponent<TableEnemy>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_enemy != null) Object.DestroyImmediate(_enemy);
        if (_tracker != null) Object.DestroyImmediate(_tracker);
    }

    [UnityTest]
    public IEnumerator FixedUpdate_TrackerOutsideDetection_WandersInsideSafeArea()
    {
        Configure(1f);
        SetTrackerPosition(new Vector3(2f, 0.4f, 0f));

        yield return FixedFrames(4);

        Assert.AreEqual(TableEnemy.State.Wandering, _tableEnemy.CurrentState);
        Assert.IsTrue(_tableEnemy.Area.ContainsXZ(_enemy.transform.position, _tableEnemy.edgeMargin));
    }

    [UnityTest]
    public IEnumerator FixedUpdate_TrackerEnterAndExitRadius_UsesHysteresis()
    {
        Configure(2f);
        SetTrackerPosition(_enemy.transform.position + Vector3.right * 0.2f);
        yield return FixedFrames(2);
        Assert.AreEqual(TableEnemy.State.Fleeing, _tableEnemy.CurrentState);

        SetTrackerPosition(_enemy.transform.position + Vector3.right * 0.35f);
        yield return FixedFrames(2);
        Assert.AreEqual(TableEnemy.State.Fleeing, _tableEnemy.CurrentState);

        SetTrackerPosition(_enemy.transform.position + Vector3.right * 0.5f);
        yield return FixedFrames(2);
        Assert.AreEqual(TableEnemy.State.Wandering, _tableEnemy.CurrentState);
    }

    [UnityTest]
    public IEnumerator FixedUpdate_InvalidTrackerPose_ReturnsToWandering()
    {
        Configure(2f);
        SetTrackerPosition(Vector3.right * 0.2f);
        yield return FixedFrames(2);
        Assert.AreEqual(TableEnemy.State.Fleeing, _tableEnemy.CurrentState);

        _source.HasPose = false;
        _driver.Update();
        yield return FixedFrames(2);

        Assert.IsFalse(_driver.DebugPoseValid);
        Assert.AreEqual(TableEnemy.State.Wandering, _tableEnemy.CurrentState);
    }

    [UnityTest]
    public IEnumerator FixedUpdate_FleeingOnOpenTable_IncreasesTrackerDistance()
    {
        Configure(2f);
        SetTrackerPosition(new Vector3(0.2f, 0.4f, 0f));
        var initialDistance = HorizontalDistance(_enemy.transform.position, _tracker.transform.position);

        yield return FixedFrames(10);

        var distance = HorizontalDistance(_enemy.transform.position, _tracker.transform.position);
        Assert.Greater(distance, initialDistance + 0.005f);
        Assert.AreEqual(0.4f, _enemy.transform.position.y, 0.0001f);
        Assert.IsTrue(_tableEnemy.Area.ContainsXZ(_enemy.transform.position, _tableEnemy.edgeMargin));
    }

    [UnityTest]
    public IEnumerator FixedUpdate_NarrowTableWithoutSafeTarget_StaysFiniteAndInside()
    {
        Configure(0.15f);
        _tableEnemy.edgeMargin = 0.14f;
        SetTrackerPosition(_enemy.transform.position);
        var initialPosition = _enemy.transform.position;

        yield return FixedFrames(5);

        AssertFinite(_enemy.transform.position);
        Assert.AreEqual(initialPosition, _enemy.transform.position);
        Assert.IsTrue(_tableEnemy.Area.ContainsXZ(_enemy.transform.position, _tableEnemy.edgeMargin));
    }

    [UnityTest]
    public IEnumerator FixedUpdate_CoincidentTracker_UsesFiniteFallbackDirection()
    {
        Configure(1f);
        SetTrackerPosition(_enemy.transform.position);

        yield return FixedFrames(4);

        AssertFinite(_enemy.transform.position);
        var rotation = _enemy.transform.rotation;
        Assert.IsFalse(float.IsNaN(rotation.x) || float.IsNaN(rotation.y) ||
            float.IsNaN(rotation.z) || float.IsNaN(rotation.w));
        Assert.IsFalse(float.IsInfinity(rotation.x) || float.IsInfinity(rotation.y) ||
            float.IsInfinity(rotation.z) || float.IsInfinity(rotation.w));
    }

    [UnityTest]
    public IEnumerator Configure_EmptyArea_DoesNotMove()
    {
        var initialPosition = _enemy.transform.position;
        SetTrackerPosition(Vector3.right * 0.2f);
        _tableEnemy.Configure(default, _driver);

        yield return FixedFrames(3);

        Assert.AreEqual(initialPosition, _enemy.transform.position);
    }

    [UnityTest]
    public IEnumerator FixedUpdate_WithoutAnimator_StillMoves()
    {
        Configure(2f);
        SetTrackerPosition(Vector3.right * 0.2f);
        var initialPosition = _enemy.transform.position;
        Assert.IsNull(_enemy.GetComponent<Animator>());

        yield return FixedFrames(8);

        Assert.Greater(Vector3.Distance(initialPosition, _enemy.transform.position), 0.005f);
    }

    private void Configure(float halfSize)
    {
        _tableEnemy.Configure(Rectangle(halfSize), _driver);
    }

    private void SetTrackerPosition(Vector3 position)
    {
        _source.HasPose = true;
        _source.Position = position;
        _driver.Update();
    }

    private static IEnumerator FixedFrames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return new WaitForFixedUpdate();
            yield return null;
        }
    }

    private static PicoFloorArea Rectangle(float halfSize)
    {
        return new PicoFloorArea(new[]
        {
            new Vector3(-halfSize, 0.4f, -halfSize),
            new Vector3(halfSize, 0.4f, -halfSize),
            new Vector3(halfSize, 0.4f, halfSize),
            new Vector3(-halfSize, 0.4f, halfSize)
        });
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static void AssertFinite(Vector3 value)
    {
        Assert.IsFalse(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z));
        Assert.IsFalse(float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
    }
}
