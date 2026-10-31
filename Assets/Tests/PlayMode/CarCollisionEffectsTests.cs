using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class CarCollisionEffectsTests
{
    private GameObject _car;
    private GameObject _other;
    private GameObject _effectPrefab;

    [SetUp]
    public void SetUp()
    {
        _car = new GameObject("Car");
        _other = new GameObject("Other");
        _other.AddComponent<BoxCollider>();
        _effectPrefab = new GameObject("EffectProbe");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var instance in Object.FindObjectsOfType<GameObject>().Where(item => item.name.StartsWith("EffectProbe")))
        {
            Object.DestroyImmediate(instance);
        }

        Object.DestroyImmediate(_car);
        Object.DestroyImmediate(_other);
        Object.DestroyImmediate(_effectPrefab);
    }

    [Test]
    public void Play_CreatesSingleEffectAtClosestPoint()
    {
        var effects = _car.AddComponent<CarCollisionEffects>();
        effects.effectPrefab = _effectPrefab;
        effects.effectScale = 0.25f;
        _car.transform.position = new Vector3(1.25f, 0f, 0f);
        var otherCollider = _other.GetComponent<Collider>();

        effects.Play(otherCollider, _car.transform);

        var instance = Object.FindObjectsOfType<GameObject>().Single(item => item.name.StartsWith("EffectProbe("));
        Assert.AreEqual(new Vector3(0.5f, 0f, 0f), instance.transform.position);
        Assert.AreEqual(0.25f, instance.transform.localScale.x);
    }
}
