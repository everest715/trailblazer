using UnityEngine;

public class PicoSpawnPositions : MonoBehaviour
{
    public bool SpawnOnStart;
    public GameObject SpawnObject;
    public int SpawnAmount = 1;
    public Bounds spawnBounds = new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));

    private void Start()
    {
        if (SpawnOnStart)
        {
            StartSpawn();
        }
    }

    public void StartSpawn()
    {
        if (SpawnObject == null)
        {
            return;
        }

        for (var i = 0; i < SpawnAmount; i++)
        {
            var localPosition = new Vector3(
                Random.Range(spawnBounds.min.x, spawnBounds.max.x),
                Random.Range(spawnBounds.min.y, spawnBounds.max.y),
                Random.Range(spawnBounds.min.z, spawnBounds.max.z));
            var worldPosition = transform.TransformPoint(localPosition);
            Instantiate(SpawnObject, worldPosition, SpawnObject.transform.rotation);
        }
    }
}
