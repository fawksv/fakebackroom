using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInteraction : MonoBehaviour
{
    [Header("射线交互")]
    [SerializeField] private Transform viewTransform;
    [SerializeField, Min(0.1f)] private float interactionDistance = 3.5f;
    [SerializeField, Min(0f)] private float interactionRadius = 0.18f;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private LayerMask interactionLayers = ~0;

    [Header("屏幕提示")]
    [SerializeField] private bool drawCrosshair = true;

    public bool DrawCrosshair
    {
        get => drawCrosshair;
        set => drawCrosshair = value;
    }
    [SerializeField] private bool drawInteractionPrompt = true;
    [SerializeField] private Font promptFont;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private Color promptColor = new Color(1f, 0.85f, 0.4f, 1f);
    [SerializeField, Min(10)] private int crosshairFontSize = 22;
    [SerializeField, Min(10)] private int promptFontSize = 22;

    private IInteractable currentTarget;
    private string currentPrompt;
    private GUIStyle crosshairStyle;
    private GUIStyle promptStyle;
    private bool missingViewLogged;

    public Transform PlayerTransform => transform;

    private void Awake()
    {
        ResolveViewTransform();
        drawCrosshair = PlayerPrefs.GetInt("PlayerPrototype.ShowCrosshair", 1) == 1;
    }

    private void OnEnable()
    {
        ResolveViewTransform();
    }

    private void OnDisable()
    {
        ClearTarget();
    }

    private void Update()
    {
        // Menus release the cursor and pause time. Interaction must stop with them.
        if (Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked)
        {
            ClearTarget();
            return;
        }

        currentTarget = FindTarget();
        currentPrompt = currentTarget != null
            ? $"[{interactKey}] {currentTarget.InteractionPrompt}"
            : string.Empty;

        if (currentTarget != null && Input.GetKeyDown(interactKey))
            currentTarget.Interact(this);
    }

    private IInteractable FindTarget()
    {
        if (viewTransform == null)
        {
            ResolveViewTransform();
            return null;
        }

        Ray ray = new Ray(viewTransform.position, viewTransform.forward);
        RaycastHit[] hits = Physics.SphereCastAll(
            ray,
            interactionRadius,
            interactionDistance,
            interactionLayers,
            QueryTriggerInteraction.Collide);

        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                // A sphere cast can begin overlapped with the player's own
                // CharacterController. Never let the player block their own
                // interaction query.
                if (hit.collider.transform.IsChildOf(transform))
                    continue;

                IInteractable target = hit.collider.GetComponentInParent<IInteractable>();
                if (target != null && target.CanInteract)
                    return target;
            }
        }

        return FindAimedFlashlightFallback();
    }

    private IInteractable FindAimedFlashlightFallback()
    {
        FlashlightPickup[] pickups = FindObjectsByType<FlashlightPickup>(FindObjectsSortMode.None);
        FlashlightPickup bestPickup = null;
        float bestScore = float.PositiveInfinity;
        const float aimTolerance = 0.45f;

        foreach (FlashlightPickup pickup in pickups)
        {
            if (pickup == null || !pickup.CanInteract || !pickup.gameObject.activeInHierarchy)
                continue;

            Vector3 toPickup = pickup.transform.position - viewTransform.position;
            float distance = toPickup.magnitude;
            if (distance <= 0.001f || distance > interactionDistance)
                continue;

            float forwardDistance = Vector3.Dot(viewTransform.forward, toPickup);
            if (forwardDistance <= 0f)
                continue;

            float lateralDistanceSq = Mathf.Max(
                0f,
                toPickup.sqrMagnitude - forwardDistance * forwardDistance);
            if (lateralDistanceSq > aimTolerance * aimTolerance)
                continue;

            // Line-of-sight: skip if a wall is between the player and the pickup.
            if (Physics.Linecast(viewTransform.position, pickup.transform.position,
                out RaycastHit blockHit, interactionLayers, QueryTriggerInteraction.Ignore))
            {
                if (blockHit.transform != pickup.transform &&
                    !blockHit.transform.IsChildOf(pickup.transform))
                    continue;
            }

            float score = lateralDistanceSq + distance * 0.001f;
            if (score < bestScore)
            {
                bestScore = score;
                bestPickup = pickup;
            }
        }

        return bestPickup;
    }

    private void ResolveViewTransform()
    {
        if (viewTransform != null)
            return;

        Camera playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera != null)
        {
            viewTransform = playerCamera.transform;
            return;
        }

        if (!missingViewLogged)
        {
            missingViewLogged = true;
            Debug.LogError("PlayerInteraction: Player 下没有找到用于射线检测的 Camera。", this);
        }
    }

    private void ClearTarget()
    {
        currentTarget = null;
        currentPrompt = string.Empty;
    }

    private void OnGUI()
    {
        if (Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked)
            return;

        EnsureStyles();

        if (drawCrosshair)
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float s = 6f;
            Color prevColor = GUI.color;
            GUI.color = crosshairColor;
            GUI.DrawTexture(new Rect(cx - s * 0.5f, cy - 0.5f, s, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 0.5f, cy - s * 0.5f, 1f, s), Texture2D.whiteTexture);
            GUI.color = prevColor;
        }

        if (drawInteractionPrompt && !string.IsNullOrEmpty(currentPrompt))
        {
            Rect promptRect = new Rect(0f, Screen.height * 0.5f + 42f, Screen.width, 40f);
            GUI.Label(promptRect, currentPrompt, promptStyle);
        }
    }

    private void EnsureStyles()
    {
        if (crosshairStyle == null)
        {
            crosshairStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = crosshairFontSize,
                font = promptFont
            };
            crosshairStyle.normal.textColor = crosshairColor;
        }

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = promptFontSize,
                font = promptFont
            };
            promptStyle.normal.textColor = promptColor;
        }
    }
}
