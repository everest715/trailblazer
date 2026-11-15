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

    public AudioClip effectSound;

    [Header("Coin Drop")]
    public GameObject coinPrefab;
    [Range(0f, 100f)]
    public float dropChance;
    public float coinDropDistance = 0.4f;
    public float coinDropHeight = 0.3f;
    public float coinDropDuration = 0.5f;
    public bool autoCoinDropDuration = true;
    public float coinDropGravity = 9.81f;

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

        Play(_collider.ClosestPoint(other.transform.position), GetCoinDropDirection(other.transform));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0 || collision.collider == null ||
            !collision.collider.CompareTag("Player"))
        {
            return;
        }

        Play(collision.GetContact(0).point, GetCoinDropDirection(collision.collider.transform));
    }

    private Vector3 GetCoinDropDirection(Transform tracker)
    {
        var direction = transform.position - tracker.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = tracker.forward;
            direction.y = 0f;
        }

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private void Play(Vector3 position, Vector3 coinDropDirection)
    {
        if (effectSound)
        {
            AudioSource.PlayClipAtPoint(effectSound, position);
        }

        if (coinPrefab != null && dropChance > 0f &&
            (dropChance >= 100f || Random.value < dropChance / 100f))
        {
            var coin = Instantiate(coinPrefab, transform.position, coinPrefab.transform.rotation);
            var duration = autoCoinDropDuration
                ? Mathf.Sqrt(8f * Mathf.Max(0f, coinDropHeight) / Mathf.Max(0.01f, coinDropGravity))
                : coinDropDuration;
            coin.AddComponent<CoinArcFlight>().Launch(coinDropDirection,
                coinDropDistance, coinDropHeight, duration);
        }

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
