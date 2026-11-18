using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CollisionEffectTests
{
    private GameObject _target;
    private GameObject _car;
    private GameObject _effectPrefab;
    private GameObject _coinPrefab;
    private CollisionEffect _effect;
    private AudioClip _effectSound;

    private sealed class CollisionContactProbe : MonoBehaviour
    {
        public Vector3 ContactPoint { get; private set; }

        private void OnCollisionEnter(Collision collision)
        {
            ContactPoint = collision.GetContact(0).point;
        }
    }

    [SetUp]
    public void SetUp()
    {
        _target = new GameObject("CollisionTarget");
        _target.AddComponent<BoxCollider>();
        _effect = _target.AddComponent<CollisionEffect>();

        _car = new GameObject("Car");
        _car.tag = "Player";
        _car.transform.position = new Vector3(1.25f, 0f, 0f);
        _car.AddComponent<BoxCollider>();

        _effectPrefab = new GameObject("CollisionEffectProbe");
        _effect.effectPrefab = _effectPrefab;
        _coinPrefab = new GameObject("CoinDropProbe");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var instance in UnityEngine.Object.FindObjectsOfType<GameObject>()
                     .Where(item => item.name.StartsWith("CollisionEffectProbe")))
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        foreach (var audioSource in UnityEngine.Object.FindObjectsOfType<AudioSource>()
                     .Where(item => item.name == "One shot audio"))
        {
            UnityEngine.Object.DestroyImmediate(audioSource.gameObject);
        }

        foreach (var instance in UnityEngine.Object.FindObjectsOfType<GameObject>()
                     .Where(item => item.name.StartsWith("CoinDropProbe(")))
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        UnityEngine.Object.DestroyImmediate(_target);
        UnityEngine.Object.DestroyImmediate(_car);
        UnityEngine.Object.DestroyImmediate(_effectPrefab);
        UnityEngine.Object.DestroyImmediate(_coinPrefab);
        UnityEngine.Object.DestroyImmediate(_effectSound);
    }

    [Test]
    public void OnTriggerEnter_Player_CreatesOneEffectAtClosestPoint()
    {
        Trigger(_car);

        var instances = EffectInstances();
        Assert.AreEqual(1, instances.Length);
        Assert.AreEqual(new Vector3(0.5f, 0f, 0f), instances[0].transform.position);
    }

    [Test]
    public void OnTriggerEnter_NonPlayer_DoesNotCreateEffect()
    {
        _car.tag = "Untagged";

        Trigger(_car);

        Assert.IsEmpty(EffectInstances());
    }

    [Test]
    public void OnTriggerEnter_NullPrefab_DoesNotCreateEffect()
    {
        _effect.effectPrefab = null;

        Trigger(_car);

        Assert.IsEmpty(EffectInstances());
    }

    [Test]
    public void OnTriggerEnter_Player_PreservesPrefabRotationAndMultipliesScale()
    {
        var rotation = Quaternion.Euler(10f, 20f, 30f);
        _effectPrefab.transform.rotation = rotation;
        _effectPrefab.transform.localScale = new Vector3(2f, 3f, 4f);
        _effect.effectScale = 0.5f;

        Trigger(_car);

        var instance = EffectInstances().Single();
        Assert.Less(Quaternion.Angle(rotation, instance.transform.rotation), 0.001f);
        Assert.AreEqual(new Vector3(1f, 1.5f, 2f), instance.transform.localScale);
    }

    [Test]
    public void OnTriggerEnter_Player_AppliesHierarchyParticleSettings()
    {
        var hierarchy = _effectPrefab.AddComponent<ParticleSystem>();
        var hierarchyMain = hierarchy.main;
        hierarchyMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
        hierarchyMain.startSpeed = 2f;
        hierarchyMain.startLifetime = 4f;
        hierarchyMain.gravityModifier = 1f;

        var shapeObject = new GameObject("ShapeParticle");
        shapeObject.transform.SetParent(_effectPrefab.transform);
        var shape = shapeObject.AddComponent<ParticleSystem>();
        var shapeMain = shape.main;
        shapeMain.scalingMode = ParticleSystemScalingMode.Shape;

        _effect.effectScale = 0.01f;
        _effect.particleSpeedMultiplier = 3f;
        _effect.particleLifetimeMultiplier = 0.5f;

        Trigger(_car);

        var instance = EffectInstances().Single();
        var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        var actualHierarchy = particles.Single(item => item.gameObject == instance).main;
        Assert.AreEqual(6f, actualHierarchy.startSpeedMultiplier, 0.0001f);
        Assert.AreEqual(2f, actualHierarchy.startLifetimeMultiplier, 0.0001f);
        Assert.AreEqual(0.01f, actualHierarchy.gravityModifier.constant, 0.0001f);
        Assert.AreEqual(ParticleSystemScalingMode.Shape,
            particles.Single(item => item.gameObject != instance).main.scalingMode);

        UnityEngine.Object.DestroyImmediate(shapeObject);
    }

    [UnityTest]
    public IEnumerator OnCollisionEnter_Player_CreatesOneEffectAtContactPoint()
    {
        var contactProbe = _target.AddComponent<CollisionContactProbe>();
        var targetBody = _target.AddComponent<Rigidbody>();
        targetBody.isKinematic = true;
        var carBody = _car.AddComponent<Rigidbody>();
        carBody.useGravity = false;
        _car.transform.position = new Vector3(0.75f, 0f, 0f);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        var instances = EffectInstances();
        Assert.AreEqual(1, instances.Length);
        Assert.AreEqual(contactProbe.ContactPoint, instances[0].transform.position);
    }

    [Test]
    public void OnTriggerEnter_ZeroDropChance_DoesNotCreateCoin()
    {
        _effect.coinPrefab = _coinPrefab;
        _effect.dropChance = 0f;

        Trigger(_car);

        Assert.IsEmpty(CoinInstances());
    }

    [UnityTest]
    public IEnumerator OnTriggerEnter_FullDropChance_LaunchesCoinAlongTrackerCollisionDirection()
    {
        var targetPosition = new Vector3(2f, 3f, 4f);
        var rotation = Quaternion.Euler(10f, 20f, 30f);
        var scale = new Vector3(2f, 3f, 4f);
        _target.transform.position = targetPosition;
        _car.transform.position = targetPosition + Vector3.forward * 2f + Vector3.up * 3f;
        _coinPrefab.transform.rotation = rotation;
        _coinPrefab.transform.localScale = scale;
        _coinPrefab.AddComponent<SphereCollider>();
        _effect.coinPrefab = _coinPrefab;
        _effect.dropChance = 100f;

        Trigger(_car);

        var coin = CoinInstances().Single();
        Assert.IsNotNull(coin.GetComponent<CoinArcFlight>());
        Assert.IsFalse(coin.GetComponent<SphereCollider>().enabled);
        Assert.AreEqual(targetPosition, coin.transform.position);
        Assert.Less(Quaternion.Angle(rotation, coin.transform.rotation), 0.001f);
        Assert.AreEqual(scale, coin.transform.localScale);
        yield return new WaitForSeconds(0.7f);

        Assert.Less(Vector3.Distance(targetPosition + Vector3.back * 0.4f,
            coin.transform.position), 0.0001f);
        Assert.IsTrue(coin.GetComponent<SphereCollider>().enabled);
    }

    [UnityTest]
    public IEnumerator OnTriggerEnter_CoincidentTracker_UsesTrackerForwardForCoinDrop()
    {
        var targetPosition = new Vector3(2f, 3f, 4f);
        _target.transform.position = targetPosition;
        _car.transform.position = targetPosition + Vector3.up * 2f;
        _car.transform.rotation = Quaternion.LookRotation(Vector3.right);
        _coinPrefab.AddComponent<SphereCollider>();
        _effect.coinPrefab = _coinPrefab;
        _effect.dropChance = 100f;

        Trigger(_car);
        yield return new WaitForSeconds(0.7f);

        var coin = CoinInstances().Single();
        Assert.Less(Vector3.Distance(targetPosition + Vector3.right * 0.4f,
            coin.transform.position), 0.0001f);
        Assert.IsTrue(coin.GetComponent<SphereCollider>().enabled);
    }

    [UnityTest]
    public IEnumerator OnTriggerEnter_AutoDuration_UsesHeightAndGravity()
    {
        var targetPosition = new Vector3(2f, 3f, 4f);
        _target.transform.position = targetPosition;
        _car.transform.position = targetPosition + Vector3.forward * 2f;
        _coinPrefab.AddComponent<SphereCollider>();
        _effect.coinPrefab = _coinPrefab;
        _effect.dropChance = 100f;
        _effect.coinDropDistance = 0.4f;
        _effect.coinDropHeight = 0.3f;
        _effect.autoCoinDropDuration = true;
        _effect.coinDropGravity = 9.81f;
        _effect.coinDropDuration = 0.65f;

        Trigger(_car);

        var collider = CoinInstances().Single().GetComponent<SphereCollider>();
        yield return new WaitForSeconds(0.4f);
        Assert.IsFalse(collider.enabled);
        yield return new WaitForSeconds(0.15f);
        Assert.IsTrue(collider.enabled);
    }

    [UnityTest]
    public IEnumerator OnTriggerEnter_ManualDuration_OverridesAutoDuration()
    {
        var targetPosition = new Vector3(2f, 3f, 4f);
        _target.transform.position = targetPosition;
        _car.transform.position = targetPosition + Vector3.forward * 2f;
        _coinPrefab.AddComponent<SphereCollider>();
        _effect.coinPrefab = _coinPrefab;
        _effect.dropChance = 100f;
        _effect.coinDropDistance = 0.4f;
        _effect.coinDropHeight = 0.3f;
        _effect.autoCoinDropDuration = false;
        _effect.coinDropDuration = 0.2f;

        Trigger(_car);
        yield return new WaitForSeconds(0.25f);

        var coin = CoinInstances().Single();
        Assert.Less(Vector3.Distance(targetPosition + Vector3.back * 0.4f,
            coin.transform.position), 0.0001f);
        Assert.IsTrue(coin.GetComponent<SphereCollider>().enabled);
    }

    [UnityTest]
    public IEnumerator OnTriggerEnter_ZeroGravity_DoesNotProduceNaNAndLands()
    {
        var targetPosition = new Vector3(2f, 3f, 4f);
        _target.transform.position = targetPosition;
        _car.transform.position = targetPosition + Vector3.forward * 2f;
        _coinPrefab.AddComponent<SphereCollider>();
        _effect.coinPrefab = _coinPrefab;
        _effect.dropChance = 100f;
        _effect.coinDropDistance = 0.4f;
        _effect.coinDropHeight = 0.0001f;
        _effect.autoCoinDropDuration = true;
        _effect.coinDropGravity = 0f;

        Trigger(_car);
        yield return new WaitForSeconds(0.35f);

        var coin = CoinInstances().Single();
        var position = coin.transform.position;
        Assert.IsFalse(float.IsNaN(position.x) || float.IsInfinity(position.x));
        Assert.IsFalse(float.IsNaN(position.y) || float.IsInfinity(position.y));
        Assert.IsFalse(float.IsNaN(position.z) || float.IsInfinity(position.z));
        Assert.Less(Vector3.Distance(targetPosition + Vector3.back * 0.4f,
            position), 0.0001f);
        Assert.IsTrue(coin.GetComponent<SphereCollider>().enabled);
    }

    [UnityTest]
    public IEnumerator OnTriggerEnter_ConfiguredTableArea_BouncesCoinAtEdge()
    {
        var targetPosition = new Vector3(2f, 3f, 4f);
        _target.transform.position = targetPosition;
        _car.transform.position = targetPosition + Vector3.left;
        _coinPrefab.AddComponent<SphereCollider>();
        _effect.coinPrefab = _coinPrefab;
        _effect.dropChance = 100f;
        _effect.coinDropDistance = 0.5f;
        _effect.coinDropHeight = 0.3f;
        _effect.autoCoinDropDuration = false;
        _effect.coinDropDuration = 0.1f;
        _effect.ConfigureCoinDropArea(SquareAround(targetPosition, 0.25f));

        Trigger(_car);
        yield return new WaitForSeconds(0.2f);

        var coin = CoinInstances().Single();
        Assert.Less(Vector3.Distance(targetPosition, coin.transform.position), 0.001f);
        Assert.IsTrue(coin.GetComponent<SphereCollider>().enabled);
    }

    [Test]
    public void OnTriggerEnter_NullCoinPrefab_DoesNotCreateCoin()
    {
        _effect.coinPrefab = null;
        _effect.dropChance = 100f;

        Trigger(_car);

        Assert.IsEmpty(CoinInstances());
    }

    [Test]
    public void OnTriggerEnter_NonPlayer_DoesNotCreateCoin()
    {
        _car.tag = "Untagged";
        _effect.coinPrefab = _coinPrefab;
        _effect.dropChance = 100f;

        Trigger(_car);

        Assert.IsEmpty(CoinInstances());
    }

    [Test]
    public void OnTriggerEnter_NullPrefab_PlaysEffectSoundAtClosestPoint()
    {
        _effect.effectPrefab = null;
        CreateEffectSound();

        Trigger(_car);

        var audioSource = EffectSoundInstances().Single();
        Assert.AreSame(_effectSound, audioSource.clip);
        Assert.AreEqual(new Vector3(0.5f, 0f, 0f), audioSource.transform.position);
    }

    [Test]
    public void OnTriggerEnter_NonPlayer_DoesNotPlayEffectSound()
    {
        _car.tag = "Untagged";
        CreateEffectSound();

        Trigger(_car);

        Assert.IsEmpty(EffectSoundInstances());
    }

    [Test]
    public void OnTriggerEnter_TriggerOnce_RepeatedPlayerMessagesCreateOneEffect()
    {
        _effect.triggerOnce = true;

        Trigger(_car);
        Trigger(_car);

        Assert.AreEqual(1, EffectInstances().Length);
    }

    [Test]
    public void OnTriggerEnter_DefaultSettings_RepeatedPlayerMessagesKeepExistingBehavior()
    {
        Trigger(_car);
        Trigger(_car);

        Assert.AreEqual(2, EffectInstances().Length);
    }

    [UnityTest]
    public IEnumerator OnTriggerEnter_DestroyOnHit_CreatesEffectBeforeDestroyingTarget()
    {
        _effect.destroyOnHit = true;

        Trigger(_car);
        yield return null;

        Assert.IsTrue(_target == null);
        Assert.AreEqual(1, EffectInstances().Length);
    }

    [UnityTest]
    public IEnumerator OnTriggerEnter_DestroyOnHitWithNullEffect_StillDestroysTarget()
    {
        _effect.effectPrefab = null;
        _effect.destroyOnHit = true;

        Assert.DoesNotThrow(() => Trigger(_car));
        yield return null;

        Assert.IsTrue(_target == null);
        Assert.IsEmpty(EffectInstances());
    }

    [UnityTest]
    public IEnumerator OnTriggerEnter_NonPlayerWithDestroyOnHit_KeepsTarget()
    {
        _car.tag = "Untagged";
        _effect.destroyOnHit = true;

        Trigger(_car);
        yield return null;

        Assert.IsTrue(_target != null);
        Assert.IsEmpty(EffectInstances());
    }

    [Test]
    public void CollisionEffect_HasEarlierExecutionOrderThanGame()
    {
        var attribute = typeof(CollisionEffect).GetCustomAttribute<DefaultExecutionOrder>();

        Assert.IsNotNull(attribute);
        Assert.Less(attribute.order, 0);
    }

    private static PicoFloorArea SquareAround(Vector3 center, float halfSize)
    {
        return new PicoFloorArea(new[]
        {
            new Vector3(center.x - halfSize, center.y, center.z - halfSize),
            new Vector3(center.x + halfSize, center.y, center.z - halfSize),
            new Vector3(center.x + halfSize, center.y, center.z + halfSize),
            new Vector3(center.x - halfSize, center.y, center.z + halfSize)
        });
    }

    private void Trigger(GameObject other)
    {
        _target.SendMessage("OnTriggerEnter", other.GetComponent<Collider>());
    }

    private void CreateEffectSound()
    {
        _effectSound = AudioClip.Create("CollisionEffectSoundProbe", 441, 1, 44100, false);
        _effect.effectSound = _effectSound;
    }

    private static AudioSource[] EffectSoundInstances()
    {
        return UnityEngine.Object.FindObjectsOfType<AudioSource>()
            .Where(item => item.name == "One shot audio")
            .ToArray();
    }

    private static GameObject[] EffectInstances()
    {
        return UnityEngine.Object.FindObjectsOfType<GameObject>()
            .Where(item => item.name.StartsWith("CollisionEffectProbe("))
            .ToArray();
    }

    private static GameObject[] CoinInstances()
    {
        return UnityEngine.Object.FindObjectsOfType<GameObject>()
            .Where(item => item.name.StartsWith("CoinDropProbe("))
            .ToArray();
    }
}
