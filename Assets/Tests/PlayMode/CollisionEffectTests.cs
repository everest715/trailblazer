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

    [Test]
    public void OnTriggerEnter_FullDropChance_CreatesOneCoinAtTargetPosition()
    {
        var targetPosition = new Vector3(2f, 3f, 4f);
        var rotation = Quaternion.Euler(10f, 20f, 30f);
        var scale = new Vector3(2f, 3f, 4f);
        _target.transform.position = targetPosition;
        _coinPrefab.transform.rotation = rotation;
        _coinPrefab.transform.localScale = scale;
        _effect.coinPrefab = _coinPrefab;
        _effect.dropChance = 100f;

        Trigger(_car);

        var coin = CoinInstances().Single();
        Assert.AreEqual(targetPosition, coin.transform.position);
        Assert.Less(Quaternion.Angle(rotation, coin.transform.rotation), 0.001f);
        Assert.AreEqual(scale, coin.transform.localScale);
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
    public void CollisionEffect_HasEarlierExecutionOrderThanGame()
    {
        var attribute = typeof(CollisionEffect).GetCustomAttribute<DefaultExecutionOrder>();

        Assert.IsNotNull(attribute);
        Assert.Less(attribute.order, 0);
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
