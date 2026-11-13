using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
public class PowerUpCollisionSoundTests
{
    private const string CoinSound = "Assets/Sounds/Coin.mp3";
    private const string TimeBonusSound = "Assets/Sounds/TimeBonus.mp3";
    private const string MultiplierSound = "Assets/Sounds/Multiplier.wav";
    private const string BombSound = "Assets/Polygon Arsenal/Sound/Wav/polygon_explosion_rocket.wav";

    [TestCase("Assets/Prefabs/Coin.prefab", CoinSound)]
    [TestCase("Assets/Prefabs/Coin 2.prefab", CoinSound)]
    [TestCase("Assets/Prefabs/3Coin.prefab", CoinSound)]
    [TestCase("Assets/Prefabs/TimeBonus.prefab", TimeBonusSound)]
    [TestCase("Assets/Prefabs/Multiplier.prefab", MultiplierSound)]
    [TestCase("Assets/Prefabs/Bomb.prefab", BombSound)]
    [TestCase("Assets/Prefabs/Bomb 1.prefab", BombSound)]
    public void Prefab_HasExpectedCollisionSound(string prefabPath, string soundPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var expectedSound = AssetDatabase.LoadAssetAtPath<AudioClip>(soundPath);

        Assert.IsNotNull(prefab, $"Missing prefab: {prefabPath}");
        Assert.IsNotNull(expectedSound, $"Missing sound: {soundPath}");
        var effect = prefab.GetComponent<CollisionEffect>();
        Assert.IsNotNull(effect, $"Missing CollisionEffect: {prefabPath}");
        Assert.AreSame(expectedSound, effect.effectSound);
    }
}
#endif
