using UnityEngine;
using System.Collections.Generic;

public class RoomLightController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Vector3 roomCenter;
    public float roomRadius;
    public List<Light> lights = new List<Light>();

    [Header("Cycle")]
    public float onDuration = 10f;
    public float offDuration = 15f;
    public float fogOnEnd = 45f;
    public float fogOnEndNoFlashlight = 3f;
    public float fogOffEnd = 30f;
    public float ambientOnIntensity = 0.5f;
    public float ambientOffIntensity = 0f;
    public Color ambientOffColor = Color.black;
    public AudioClip clickClip;

    // 怪物读取此静态属性判断灯是否亮着
    public static bool LightsOn { get; private set; } = true;

    private bool activated;
    private bool lightsOn = true;
    private bool wasInRoom;
    private float timer;
    private AudioSource audioSrc;
    private float defaultFogEnd;
    private float defaultFogStart;
    private Color defaultAmbient;
    private float defaultAmbientIntensity;
    private List<Light> allOtherLights = new List<Light>();

    bool PlayerHasFlashlight()
    {
        if (player == null) return false;
        FlashlightController fc = player.GetComponentInChildren<FlashlightController>(true);
        return fc != null && fc.enabled;
    }

    float CurrentFogOnEnd => fogOnEnd;

    void Start()
    {
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 0f;
        clickClip = GenerateClick();
        defaultFogEnd = RenderSettings.fogEndDistance;
        defaultFogStart = RenderSettings.fogStartDistance;
        defaultAmbient = RenderSettings.ambientLight;
        defaultAmbientIntensity = RenderSettings.ambientIntensity;

        // Collect all lights in scene that are NOT room lights (corridor lights etc.)
        Light[] sceneLights = FindObjectsOfType<Light>();
        foreach (Light l in sceneLights)
        {
            if (!lights.Contains(l))
                allOtherLights.Add(l);
        }
    }

    void Update()
    {
        if (player == null) return;

        float playerDist = Vector3.Distance(
            new Vector3(player.position.x, 0, player.position.z),
            new Vector3(roomCenter.x, 0, roomCenter.z));
        bool inRoom = playerDist < roomRadius;

        // Only manage fog while player is in the room
        if (inRoom)
        {
            if (!wasInRoom)
            {
                // Just entered room
                wasInRoom = true;
                if (!activated)
                {
                    activated = true;
                    timer = 0f;
                }
                RenderSettings.fogEndDistance = lightsOn ? CurrentFogOnEnd : fogOffEnd;
                RenderSettings.fogStartDistance = 0f;
                RenderSettings.ambientIntensity = lightsOn ? ambientOnIntensity : ambientOffIntensity;
                RenderSettings.ambientLight = lightsOn ? defaultAmbient : ambientOffColor;
            }

            timer += Time.deltaTime;

            if (lightsOn && timer >= onDuration)
            {
                lightsOn = false;
                timer = 0f;
                PlayClick();
                SetLights(false);
            }
            else if (!lightsOn && timer >= offDuration)
            {
                lightsOn = true;
                timer = 0f;
                PlayClick();
                SetLights(true);
            }
        }
        else
        {
            // Player is outside the room - restore corridor fog immediately
            if (wasInRoom)
            {
                wasInRoom = false;
                RenderSettings.fogEndDistance = defaultFogEnd;
                RenderSettings.fogStartDistance = defaultFogStart;
                RenderSettings.ambientIntensity = defaultAmbientIntensity;
                RenderSettings.ambientLight = defaultAmbient;
                // Re-enable other lights
                foreach (var l in allOtherLights)
                    if (l != null) l.enabled = true;
            }
        }
    }

    void SetLights(bool on)
    {
        LightsOn = on;
        foreach (var l in lights)
            if (l != null) l.enabled = on;

        // When lights go off, also disable all other lights (corridor lights etc.)
        // When lights go on, re-enable them
        foreach (var l in allOtherLights)
        {
            if (l == null) continue;
            // Don't touch the flashlight
            FlashlightController fc = l.GetComponent<FlashlightController>();
            if (fc != null) continue;
            l.enabled = on;
        }

        if (wasInRoom)
        {
            RenderSettings.fogEndDistance = on ? CurrentFogOnEnd : fogOffEnd;
            RenderSettings.fogStartDistance = 0f;
            RenderSettings.ambientIntensity = on ? ambientOnIntensity : ambientOffIntensity;
            RenderSettings.ambientLight = on ? defaultAmbient : ambientOffColor;
        }
    }

    void PlayClick()
    {
        if (clickClip != null && audioSrc != null)
            audioSrc.PlayOneShot(clickClip, 0.7f);
    }

    AudioClip GenerateClick()
    {
        int sampleRate = 44100;
        int samples = sampleRate / 20;
        AudioClip clip = AudioClip.Create("Click", samples, 1, sampleRate, false);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float env = Mathf.Exp(-t * 30f);
            data[i] = (Random.value * 2f - 1f) * env * 0.5f;
        }
        clip.SetData(data, 0);
        return clip;
    }
}
