using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    public float baseIntensity = 1.5f;
    public float flickerSpeed = 8f;
    public float flickerDepth = 0.4f;
    public bool deadEndDim = false;
    public float deadEndMultiplier = 0.3f;

    private Light flickerLight;
    private float seed;
    private float targetIntensity;

    void Start()
    {
        flickerLight = GetComponent<Light>();
        if (flickerLight == null) flickerLight = GetComponentInChildren<Light>();
        seed = Random.Range(0f, 100f);
        targetIntensity = deadEndDim ? baseIntensity * deadEndMultiplier : baseIntensity;
    }

    void Update()
    {
        if (flickerLight == null) return;

        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed * 0.1f, seed);
        float flicker = 1f - flickerDepth * (1f - noise);
        flickerLight.intensity = targetIntensity * flicker;
    }
}
