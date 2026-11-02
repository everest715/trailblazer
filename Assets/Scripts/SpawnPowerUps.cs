using System.Collections;
using UnityEngine;

public class SpawnPowerUps : MonoBehaviour
{
    public GameObject[] powerUps;
    public float spawnInterval = 5f;

    private int maxCount = 8;
    
    private PicoSceneSpawnPositions _spawn;
    
    private void Awake()
    {
        _spawn = GetComponent<PicoSceneSpawnPositions>();
    }

    private void Start()
    {
        maxCount = 8;
    }

    public void SpawnNow()
    {
        InvokeRepeating(nameof(SpawnObject), spawnInterval, spawnInterval);
    }
    
    private void SpawnObject()
    {
        _spawn.SpawnObject = powerUps[Random.Range(0, powerUps.Length)];
        _spawn.StartSpawn();
        
        maxCount--;
        if (maxCount == 0)
        {
            CancelInvoke(nameof(SpawnObject));
        }
    }
}
