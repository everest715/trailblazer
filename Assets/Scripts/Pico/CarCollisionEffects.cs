using UnityEngine;

public class CarCollisionEffects : MonoBehaviour
{
    public GameObject effectPrefab;
    public float lifetime = 2f;

    public void Play(Collider other, Transform car)
    {
        if (effectPrefab == null || other == null || car == null)
        {
            return;
        }

        var position = other.ClosestPoint(car.position);
        var effect = Instantiate(effectPrefab, position, effectPrefab.transform.rotation);
        Destroy(effect, lifetime);
    }
}
