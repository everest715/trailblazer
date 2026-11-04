using System.Collections;
using System.Collections.Generic;
using System.Net.Mail;
using TMPro;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AdaptivePerformance.VisualScripting;
using UnityEngine.Rendering.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class Game : MonoBehaviour
{
    public float startTimeout = 6f;
    public float startThreshold = 1.5f;
    public float trackScale = 1f;
    public float groundHeight;
    public float gateHeightOffset = 0.1f;
    
    public DrawTrail drawTrail;
    public float timer = 80f;
    public float multTimer = 7.5f;
    public int trailDelta = 5;
    public float timeDelta = 7.5f;
    
    public GameObject gameScripts;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public GameObject showMultiplier;

    public int score = 0;
    
    public AudioClip startSound1;
    public AudioClip startSound2;
    
    
    public AudioClip coinSound;
    public AudioClip multSound;
    public AudioClip multEndSound;
    public AudioClip timeBonusSound;
    public AudioClip errorSound;
    public AudioClip bombSound;
    // public AudioClip boostSound;

    public GameObject gate1;
    public GameObject gate2;
    public GameObject gate3;
    public GameObject gate4;

    public GameObject activeGate;
    public GameObject blockedGate;
    public GameObject finalGate;
    
    public VignetteController vignette;
    public CarCollisionEffects collisionEffects;

    public GameObject tryAgainText;
    public GameObject playAgainText;
    public GameObject startingArea;

    private int _mult = 1;
    private bool _gameStarted = false;
    private int _checkpoints = 0;

    private float _startTime;
    private AudioSource _audioSource;
    private PicoSceneCaptureFloorAreaProvider _surfaceProvider;
    
    private readonly Color _defaultColor = new Color(0f, 0.7f, 0.7f);
    private readonly Color _coinColor = new Color(0.9f, 0.9f, 0.4f);
    private readonly Color _3CoinColor = new Color(0.9f, 0.6f, 0.3f);
    private readonly Color _timeBonusColor = new Color(0.2f, 0.4f, 0.9f);
    private readonly Color _multiplierColor = new Color(0.4f, 0.9f, 0.4f);
    private readonly Color _bombColor = new Color(0.9f, 0.1f, 0.1f);
    private const float VignetteIntensity = 0.4f;

    private async void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        await AlignGatesToGround();
    }

    private void AlignGatesToHeadset()
    {
        if (gate1 == null || gate1.transform.parent == null || Camera.main == null)
        {
            return;
        }

        var headset = Camera.main.transform;
        var forward = headset.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            return;
        }

        forward.Normalize();
        var gatesRoot = gate1.transform.parent;
        var distance = new Vector2(gatesRoot.localPosition.x, gatesRoot.localPosition.z).magnitude;
        if (distance < 0.01f)
        {
            distance = 1.5f;
        }

        var position = headset.position + forward * distance;
        position.y = gatesRoot.position.y;
        gatesRoot.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
    }

    private async System.Threading.Tasks.Task AlignGatesToGround()
    {
        var providers = Resources.FindObjectsOfTypeAll<PicoSceneCaptureFloorAreaProvider>();
        var provider = providers.Length > 0 ? providers[0] : null;
        if (provider != null)
        {
            _surfaceProvider = provider;
            _surfaceProvider.AreasUpdated += OnSurfaceAreasUpdated;
            try
            {
                var areas = await provider.GetFloorAreasAsync();
                if (areas.Count > 0)
                {
                    AlignGatesToArea(areas[0]);
                }
            }
            catch (System.Exception)
            {
                // keep the fallback height when scene capture is unavailable
            }
        }

        foreach (var gate in new[] { gate1, gate2, gate3, gate4 })
        {
            if (gate != null)
            {
                gate.transform.position = new Vector3(gate.transform.position.x, groundHeight, gate.transform.position.z);
            }
        }
    }
    
    private void AlignGatesToArea(PicoFloorArea area)
    {
        var gates = new[] { gate1, gate2, gate3, gate4 };
        if (area.Vertices == null || area.Vertices.Length < 3 ||
            gate1 == null || gate2 == null || gate3 == null || gate4 == null)
        {
            return;
        }

        groundHeight = area.SurfaceHeight + gateHeightOffset;
        var target = area.PlanarBounds.center;
        target.y = groundHeight;
        if (!area.ContainsXZ(target))
        {
            if (!area.TryGetRandomPoint(out target))
            {
                return;
            }
        }

        var currentBounds = new Bounds(gates[0].transform.position, Vector3.zero);
        foreach (var gate in gates)
        {
            currentBounds.Encapsulate(gate.transform.position);
        }

        var scale = 1f;
        if (currentBounds.size.x > 0.001f)
        {
            scale = Mathf.Min(scale, area.PlanarBounds.size.x * 0.9f / currentBounds.size.x);
        }
        if (currentBounds.size.z > 0.001f)
        {
            scale = Mathf.Min(scale, area.PlanarBounds.size.z * 0.9f / currentBounds.size.z);
        }

        var fitted = new Vector3[gates.Length];
        for (var attempt = 0; attempt < 20; attempt++)
        {
            for (var i = 0; i < gates.Length; i++)
            {
                var offset = gates[i].transform.position - currentBounds.center;
                fitted[i] = target + offset * scale;
                fitted[i].y = groundHeight;
            }

            if (AllPointsInside(fitted, area))
            {
                break;
            }
            scale *= 0.9f;
        }

        if (!AllPointsInside(fitted, area))
        {
            for (var i = 0; i < fitted.Length; i++)
            {
                fitted[i] = target;
            }
        }

        var gatesRoot = gate1.transform.parent;
        if (gatesRoot != null)
        {
            gatesRoot.position = new Vector3(target.x, gatesRoot.position.y, target.z);
        }
        for (var i = 0; i < gates.Length; i++)
        {
            gates[i].transform.position = fitted[i];
        }
    }

    private static bool AllPointsInside(Vector3[] points, PicoFloorArea area)
    {
        foreach (var point in points)
        {
            if (!area.ContainsXZ(point))
            {
                return false;
            }
        }
        return true;
    }

    private void OnSurfaceAreasUpdated(IReadOnlyList<PicoFloorArea> areas)
    {
        if (areas.Count > 0)
        {
            AlignGatesToArea(areas[0]);
        }
    }

    private void OnDestroy()
    {
        if (_surfaceProvider != null)
        {
            _surfaceProvider.AreasUpdated -= OnSurfaceAreasUpdated;
        }
    }

    private void StartGame()
    {
        if (_gameStarted) return;
        _gameStarted = true;
        gameScripts.SetActive(true);
        var powerUpSpawner = gameScripts.GetComponentInChildren<SpawnPowerUps>(true);
        if (powerUpSpawner != null)
        {
            powerUpSpawner.gameObject.SetActive(true);
            powerUpSpawner.SpawnNow();
        }
        StartCoroutine(Timer());
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("StartingArea") && collisionEffects != null)
        {
            collisionEffects.Play(other, transform);
        }

        if (other.CompareTag("Gate"))
        {
            StartCoroutine(SetVignette(_defaultColor));
            StartCoroutine(HandleCheckpoint(other));
        }
        else if (other.CompareTag("DenyGate"))
        {
            _audioSource.PlayOneShot(errorSound);
            other.gameObject.SetActive(false);
        }
        else if (other.CompareTag("StartingArea"))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        else if (other.CompareTag("Coin"))
        {
            StartCoroutine(SetVignette(_coinColor));
            
            _audioSource.PlayOneShot(coinSound);
            Destroy(other.gameObject);
            UpdateScore(1*_mult);
        } 
        else if (other.CompareTag("3Coin"))
        {
            StartCoroutine(SetVignette(_3CoinColor));
            
            StartCoroutine(Handle3CoinSound());
            UpdateScore(3*_mult);
            Destroy(other.gameObject);
        } 
        else if (other.CompareTag("TimeBonus"))
        {
            StartCoroutine(SetVignette(_timeBonusColor));
            
            Destroy(other.gameObject);
            timer += timeDelta;
            _audioSource.PlayOneShot(timeBonusSound);
        } 
        // todo make boost work for multiplayer
        // else if (other.CompareTag("Boost"))
        // {
        //     
        //     Destroy(other.gameObject);
        //     GetComponent<AudioSource>().PlayOneShot(boostSound);
        // } 
        else if (other.CompareTag("Multiplier"))
        {
            Destroy(other.gameObject);
            StartCoroutine(Multiplier());
        }
        else if (other.CompareTag("Bomb"))
        {
            StartCoroutine(SetVignette(_bombColor));
            
            _audioSource.PlayOneShot(bombSound);
            Destroy(other.gameObject);
            UpdateScore(-1*_mult);
        } 
    }
    private void UpdateScore(int change)
    {
        score += change * _mult;
        scoreText.text = "" + score;
        for (int i = 0; i < change; i++)
        {
            drawTrail.segmentTotal += trailDelta;
        }
    }

    private IEnumerator Timer()
    {
        while (timer > 0)
        {
            timer -= 1;
            timerText.text = "" + timer;
            yield return new WaitForSeconds(1f);
        }
        gameScripts.SetActive(false);
        playAgainText.SetActive(true);
        startingArea.SetActive(true);
        
        drawTrail.vignette.SetIntensity(0f, 0.4f);
    }

    private IEnumerator Multiplier()
    {
        _audioSource.PlayOneShot(multSound);
        _mult = 2;
        showMultiplier.SetActive(true);
        for (int i = 1; i < multTimer; i++)
        {
            StartCoroutine(SetVignette(_multiplierColor));
            yield return new WaitForSeconds(1f);
        }
        yield return new WaitForSeconds(multTimer - math.floor(multTimer));
        _audioSource.PlayOneShot(multEndSound);
        showMultiplier.SetActive(false);
        _mult = 1;
    }
    
    private IEnumerator HandleCheckpoint(Collider gate)
    {
        switch (_checkpoints)
        {
            case 0:
                _startTime = Time.time;
                gate1.GetComponentInChildren<ParticleSystem>().Play();
                gate.gameObject.SetActive(false);
                yield return new WaitForSeconds(0.01f);
                gate2 = EnableGate(gate2);
                break;

            case 1:
                gate2.GetComponentInChildren<ParticleSystem>().Play();
                gate.gameObject.SetActive(false);
                yield return new WaitForSeconds(0.01f);
                gate3 = EnableGate(gate3);
                break;

            case 2:
                gate3.GetComponentInChildren<ParticleSystem>().Play();
                gate.gameObject.SetActive(false);
                yield return new WaitForSeconds(0.01f);

                var elapsedTime = Time.time - _startTime;
                if (elapsedTime < startThreshold || elapsedTime > startTimeout)
                {
                    tryAgainText.SetActive(true);
                    startingArea.SetActive(true);
                    gate4 = EnableGate(gate4, speedFault: true);
                }
                else
                {
                    gate4 = EnableGate(gate4, final: true);
                }
                break;

            case 3:
                gate4.GetComponentInChildren<ParticleSystem>().Play();
                _audioSource.PlayOneShot(startSound2);
                gate.gameObject.SetActive(false);
                yield return new WaitForSeconds(0.01f);
                StartGame();
                break;
        }
    }

    private GameObject EnableGate(GameObject g, bool final = false, bool speedFault = false)
    {
        _audioSource.PlayOneShot(startSound1);
        _checkpoints++;
        
        var position = g.transform.position;
        position.y = groundHeight;
        var rotation = g.transform.rotation;
        var scale = g.transform.lossyScale;
        Destroy(g);

        var gate = Instantiate(final ? finalGate : speedFault ? blockedGate : activeGate, position, rotation);
        gate.transform.localScale = scale;
        return gate;
    }

    private IEnumerator Handle3CoinSound()
    {
        _audioSource.PlayOneShot(coinSound);
        yield return new WaitForSeconds(0.1f);
        _audioSource.PlayOneShot(coinSound);
        yield return new WaitForSeconds(0.1f);
        _audioSource.PlayOneShot(coinSound);
    }

    public IEnumerator SetVignette(Color color, float intensity = VignetteIntensity)
    {
        vignette.SetColor(color, 0.25f);
        vignette.SetIntensity(intensity, 0.25f);
        yield return new WaitForSeconds(0.5f);
        vignette.SetIntensity(0, 0.5f);
    }
}
