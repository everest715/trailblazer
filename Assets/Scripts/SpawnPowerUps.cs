using System.Collections;
using UnityEngine;

public class SpawnPowerUps : MonoBehaviour
{
    public GameObject[] powerUps;
    public float spawnInterval = 5f;
    
    private PicoSceneSpawnPositions _spawn;
    
    private void Awake()
    {
        _spawn = GetComponent<PicoSceneSpawnPositions>();
    }

    private void Start()
    {
        InvokeRepeating(nameof(SpawnObject), spawnInterval, spawnInterval);
    }

    public void SpawnNow()
    {
        SpawnObject();
    }
    
    private void SpawnObject()
    {
        _spawn.SpawnObject = powerUps[Random.Range(0, powerUps.Length)];
        _spawn.StartSpawn();
    }
}
