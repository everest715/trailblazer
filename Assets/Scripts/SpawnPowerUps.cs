using System.Collections;
using UnityEngine;

public class SpawnPowerUps : MonoBehaviour
{
    public GameObject[] powerUps;
    public float spawnInterval = 5f;
    
    private PicoSceneSpawnPositions _spawn;
    
    private void Start()
    {
        _spawn = GetComponent<PicoSceneSpawnPositions>();
        InvokeRepeating(nameof(SpawnObject), 0f, spawnInterval);
    }
    
    private void SpawnObject()
    {
        _spawn.SpawnObject = powerUps[Random.Range(0, powerUps.Length)];
        _spawn.StartSpawn();
    }
}
