using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
public class BrickCoinDropTests
{
    [Test]
    public void MBrickGate_HasThirtyPercentCoinDrop()
    {
        var brick = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MBrick Gate.prefab");
        var coin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");

        Assert.IsNotNull(brick);
        Assert.IsNotNull(coin);
        var effect = brick.GetComponent<CollisionEffect>();
        Assert.IsNotNull(effect);
        Assert.AreSame(coin, effect.coinPrefab);
        Assert.AreEqual(30f, effect.dropChance);
    }
}
#endif
