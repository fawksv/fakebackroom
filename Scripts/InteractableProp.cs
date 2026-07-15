using UnityEngine;

public class InteractableProp : MonoBehaviour
{
    [Header("Interaction")]
    public string promptText = "按 E 交互";
    public float interactionRange = 2.5f;
    public float effectDuration = 1.0f;

    [Header("Blackout (故障灯短路)")]
    public bool blackoutOnInteract = false;
    public float blackoutIntensity = 0.05f;

    [Header("Door (挡板掩体)")]
    public bool toggleDoor = false;
    public Vector3 closedRotation = new Vector3(0, 90, 0);
    public Vector3 openRotation = new Vector3(0, 0, 0);

    private bool isDoorClosed = false;
    private bool isOnCooldown = false;
    private GameObject player;
    private Light[] affectedLights;
    private float[] originalIntensities;

    void Start()
    {
        player = GameObject.Find("Player");
        if (blackoutOnInteract)
        {
            affectedLights = FindObjectsOfType<Light>();
            originalIntensities = new float[affectedLights.Length];
            for (int i = 0; i < affectedLights.Length; i++)
                originalIntensities[i] = affectedLights[i].intensity;
        }
    }

    void Update()
    {
        if (player == null || isOnCooldown) return;

        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(player.transform.position.x, 0, player.transform.position.z)
        );

        if (dist <= interactionRange && Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
    }

    void Interact()
    {
        if (blackoutOnInteract)
        {
            TriggerBlackout();
        }
        else if (toggleDoor)
        {
            ToggleDoor();
        }
    }

    void TriggerBlackout()
    {
        isOnCooldown = true;
        Debug.Log("[Interactable] Blackout triggered!");
        foreach (Light l in affectedLights)
            l.intensity *= blackoutIntensity;
        Invoke("RestoreLights", effectDuration);
    }

    void RestoreLights()
    {
        for (int i = 0; i < affectedLights.Length; i++)
            if (affectedLights[i] != null)
                affectedLights[i].intensity = originalIntensities[i];
        isOnCooldown = false;
    }

    void ToggleDoor()
    {
        isDoorClosed = !isDoorClosed;
        transform.localRotation = Quaternion.Euler(isDoorClosed ? closedRotation : openRotation);
        Debug.Log("[Interactable] Door " + (isDoorClosed ? "closed" : "opened"));
    }
}
