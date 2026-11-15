using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
public class BrickCoinDropTests
{
    [Test]
    public void MBrickGate_HasConfiguredCoinDrop()
    {
        var brick = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MBrick Gate.prefab");
        var coin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MCoin 1.prefab");

        Assert.IsNotNull(brick);
        Assert.IsNotNull(coin);
        var effect = brick.GetComponent<CollisionEffect>();
        Assert.IsNotNull(effect);
        Assert.AreSame(coin, effect.coinPrefab);
        Assert.AreEqual(20f, effect.dropChance);
        Assert.AreEqual(0.5f, effect.coinDropDistance);
        Assert.AreEqual(0.3f, effect.coinDropHeight);
        Assert.AreEqual(0.5f, effect.coinDropDuration);
        Assert.IsTrue(effect.autoCoinDropDuration);
        Assert.AreEqual(9.81f, effect.coinDropGravity);
    }
}
#endif
