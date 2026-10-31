using UnityEngine;

public class CarCollisionEffects : MonoBehaviour
{
    public GameObject effectPrefab;
    public float lifetime = 2f;
    public float effectScale = 1f;

    public void Play(Collider other, Transform car)
    {
        if (effectPrefab == null || other == null || car == null)
        {
            return;
        }

        var position = other.ClosestPoint(car.position);
        var effect = Instantiate(effectPrefab, position, effectPrefab.transform.rotation);
        effect.transform.localScale *= effectScale;
        Destroy(effect, lifetime);
    }
}
