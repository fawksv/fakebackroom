using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlashlightController : MonoBehaviour
{
    [Header("Light Settings")]
    public float maxIntensity = 8f;
    public float spotAngle = 40f;
    public float range = 30f;

    [Header("Position Offset")]
    public float lightYOffset = -0.2f;
    public float lightZOffset = 0.3f;

    [Header("Smooth Follow")]
    public float rotationSmoothness = 5f;

    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.T;
    public KeyCode replaceBatteryKey = KeyCode.R;

    [Header("Battery")]
    public float maxBattery = 100f;
    public float drainRate = 0.8f;
    public float lowBatteryThreshold = 20f;
    public float flickerThreshold = 10f;

    public static FlashlightController Instance { get; private set; }

    public float BatteryLevel { get; private set; }
    public float BatteryNormalized => BatteryLevel / maxBattery;
    public bool IsLowBattery => BatteryLevel <= lowBatteryThreshold;
    public bool IsOn => isOn;

    private Light spotLight;
    private Transform playerCam;
    private Inventory inventory;
    private bool isOn;
    private bool godModeBattery;
    private float pitchVel;
    private float yawVel;
    private float rollVel;

    void Start()
    {
        Instance = this;
        BatteryLevel = maxBattery;

        spotLight = GetComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.spotAngle = spotAngle;
        spotLight.range = range;
        spotLight.intensity = 0f;
        spotLight.color = Color.white;
        spotLight.shadows = LightShadows.Soft;
        spotLight.shadowBias = 0.005f;
        spotLight.shadowNearPlane = 0.1f;
        spotLight.enabled = false;

        Camera cam = GetComponentInParent<Camera>();
        if (cam == null)
            cam = Camera.main;
        playerCam = cam != null ? cam.transform : null;

        // Find inventory on player root
        inventory = GetComponentInParent<Inventory>();

        isOn = true;
        spotLight.enabled = true;
        spotLight.intensity = maxIntensity;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        // Dev backdoor: ~ toggles infinite battery
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            godModeBattery = !godModeBattery;
            Debug.Log($"[Dev] Infinite battery: {(godModeBattery ? "ON" : "OFF")}");
        }

        if (Input.GetKeyDown(toggleKey) && BatteryLevel > 0f)
        {
            isOn = !isOn;
            spotLight.enabled = isOn;
            spotLight.intensity = isOn ? maxIntensity : 0f;
        }

        // R key: replace battery from inventory
        if (Input.GetKeyDown(replaceBatteryKey) && inventory != null
            && inventory.HasItem(Inventory.ItemType.Battery)
            && BatteryLevel < maxBattery)
        {
            inventory.RemoveOne(Inventory.ItemType.Battery);
            RefillBattery();
        }

        // Drain battery when on (skip in god mode)
        if (isOn && BatteryLevel > 0f && !godModeBattery)
        {
            BatteryLevel = Mathf.Max(0f, BatteryLevel - drainRate * Time.unscaledDeltaTime);
            if (BatteryLevel <= 0f)
            {
                isOn = false;
                spotLight.enabled = false;
            }
        }

        if (!isOn || playerCam == null) return;

        // Flicker when battery is critically low
        float intensity = maxIntensity;
        if (BatteryLevel <= flickerThreshold)
        {
            float t = BatteryLevel / flickerThreshold;
            float flicker = Random.Range(0.2f, 1f);
            intensity = Mathf.Lerp(maxIntensity * flicker, maxIntensity, t);
        }
        spotLight.intensity = intensity;

        transform.position = playerCam.position + playerCam.TransformDirection(
            new Vector3(0f, lightYOffset, lightZOffset)
        );

        Vector3 camEuler = playerCam.rotation.eulerAngles;
        Vector3 curEuler = transform.rotation.eulerAngles;

        float pitch = Mathf.SmoothDampAngle(curEuler.x, camEuler.x, ref pitchVel, 1f / rotationSmoothness);
        float yaw   = Mathf.SmoothDampAngle(curEuler.y, camEuler.y, ref yawVel,   1f / rotationSmoothness);
        float roll  = Mathf.SmoothDampAngle(curEuler.z, camEuler.z, ref rollVel,  1f / rotationSmoothness);

        transform.rotation = Quaternion.Euler(pitch, yaw, roll);
    }

    public void RefillBattery()
    {
        BatteryLevel = maxBattery;
    }
}
