using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CoinEffectParticleTextureTests
{
    [TestCase("ParticleShiny")]
    [TestCase("ParticleFlash")]
    public void EffectTexture_HasAlphaForPicoPassthrough(string textureName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(
            $"Assets/Super Mario Style/Textures/Particles/{textureName}.mat");
        Assert.NotNull(material);

        var texture = material.GetTexture("_BaseMap");
        Assert.NotNull(texture);

        var texturePath = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        Assert.NotNull(importer);
        Assert.IsTrue(importer.DoesSourceTextureHaveAlpha(),
            $"{textureName} must have transparent pixels around the particle on PICO passthrough.");
        Assert.IsTrue(importer.alphaIsTransparency,
            $"{textureName} must enable Alpha Is Transparency to avoid dark edges.");
    }
}
