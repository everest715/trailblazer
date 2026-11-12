using UnityEngine;

[RequireComponent(typeof(Collider))]
[DefaultExecutionOrder(-100)]
public class CollisionEffect : MonoBehaviour
{
    public GameObject effectPrefab;
    public float lifetime = 2f;
    public float effectScale = 1f;
    public float particleSpeedMultiplier = 3f;
    public float particleLifetimeMultiplier = 0.5f;

    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player"))
        {
            return;
        }

        if (_collider == null)
        {
            _collider = GetComponent<Collider>();
        }

        Play(_collider.ClosestPoint(other.transform.position));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0 || collision.collider == null ||
            !collision.collider.CompareTag("Player"))
        {
            return;
        }

        Play(collision.GetContact(0).point);
    }

    private void Play(Vector3 position)
    {
        if (effectPrefab == null)
        {
            return;
        }

        var effect = Instantiate(effectPrefab, position, effectPrefab.transform.rotation);
        effect.transform.localScale = effect.transform.localScale * effectScale;
        foreach (var particleSystem in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particleSystem.main;
            if (main.scalingMode == ParticleSystemScalingMode.Hierarchy)
            {
                main.startSpeedMultiplier *= particleSpeedMultiplier;
                main.startLifetimeMultiplier *= particleLifetimeMultiplier;
                main.gravityModifierMultiplier *= effectScale;
            }
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }
        Destroy(effect, lifetime);
    }
}
