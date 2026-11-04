using UnityEngine;

public class CarCollisionEffects : MonoBehaviour
{
    public GameObject effectPrefab;
    public GameObject bombEffectPrefab;
    public float lifetime = 2f;
    public float effectScale = 1f;
    public float bombEffectScale = 1f;
    public float particleSpeedMultiplier = 3f;
    public float particleLifetimeMultiplier = 0.5f;

    public void Play(Collider other, Transform car)
    {
        if (effectPrefab == null || other == null || car == null)
        {
            return;
        }

        var position = other.ClosestPoint(car.position);
        var isBomb = other.CompareTag("Bomb");
        var selectedScale = isBomb ? bombEffectScale : effectScale;
        var effect = Instantiate(isBomb ? bombEffectPrefab : effectPrefab, position, effectPrefab.transform.rotation);
        effect.transform.localScale = effect.transform.localScale * selectedScale;
        foreach (var particleSystem in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particleSystem.main;
            if (main.scalingMode == ParticleSystemScalingMode.Hierarchy)
            {
                main.startSpeedMultiplier *= particleSpeedMultiplier;
                main.startLifetimeMultiplier *= particleLifetimeMultiplier;
                main.gravityModifierMultiplier *= selectedScale;
            }
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }
        Destroy(effect, lifetime);
    }
}
