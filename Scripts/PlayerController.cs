using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    private const string MouseSensitivityKey = "PlayerPrototype.MouseSensitivity";

    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 1.5f;

    [Header("Stamina")]
    [Min(1f)] public float maxStamina = 100f;
    [Min(0f)] public float staminaDrainRate = 18f;
    [Min(0f)] public float staminaRegenRate = 14f;
    [Min(0f)] public float staminaRegenDelay = 1.2f;
    [Tooltip("Stamina required before sprinting is available again after exhaustion.")]
    [Min(0f)] public float staminaRestartThreshold = 15f;

    [Header("Jump Feel")]
    [Tooltip("Maximum jump height in metres.")]
    [Range(0.5f, 5f)] public float jumpHeight = 2f;
    [Tooltip("Upward gravity. A value closer to zero gives a slower, floatier jump.")]
    [Range(-50f, -5f)] public float gravity = -16f;
    [Tooltip("Extra gravity while falling, for a smooth rise and a responsive landing.")]
    [Range(1f, 3f)] public float fallGravityMultiplier = 1.15f;
    [Tooltip("How long jump input is remembered before touching the ground.")]
    [Range(0f, 0.25f)] public float jumpBufferTime = 0.12f;
    [Tooltip("How long jumping remains possible after walking off an edge.")]
    [Range(0f, 0.25f)] public float coyoteTime = 0.1f;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    [Range(0f, 0.3f)] public float lookLag = 0.12f;

    [Header("Crouch")]
    public float crouchCamHeight = 0.6f;
    public float crouchTransitionSpeed = 10f;
    public float crouchColliderHeight = 1.2f;

    [Header("Head Bob")]
    public float walkBobFrequency = 9f;
    public float walkBobAmplitude = 0.04f;
    public float sprintBobFrequency = 8f;
    public float sprintBobAmplitude = 0.05f;
    public float crouchBobFrequency = 6f;
    public float crouchBobAmplitude = 0.02f;
    public float bobBlendSpeed = 8f;

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float footstepVolume = 1.0f;

    [Header("Debug")]
    [Tooltip("按此键触发死亡（仅测试用）")]
    [SerializeField] private KeyCode debugDeathKey = KeyCode.K;
    [Tooltip("按此键触发逃生演出（仅测试用）")]
    [SerializeField] private KeyCode debugEscapeKey = KeyCode.L;

    private CharacterController controller;
    private Transform cam;
    private float pitch;
    private float yawVel;
    private float pitchVel;
    private float verticalVel;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private float bobTimer;
    private float bobWeight;
    private float camBaseY;
    private float standCamY = 1.2f;
    private float standColliderHeight = 2f;
    private bool isCrouching;
    private float currentCamY;
    private float currentStamina;
    private float staminaRegenTimer;
    private bool isExhausted;
    private float lastFootstepPhase;

    // Cheat: triple-tap Tab within 1 second to auto-collect all tapes
    private float[] tabPressTimes = new float[3];
    private int tabPressIndex;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaNormalized => maxStamina > 0f ? currentStamina / maxStamina : 0f;
    public bool IsExhausted => isExhausted;
    public bool HasKey { get; set; }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        mouseSensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat(MouseSensitivityKey, mouseSensitivity),
            0.5f,
            10f);
        standColliderHeight = controller.height;
        controller.center = new Vector3(0f, standColliderHeight * 0.5f, 0f);
        cam = GetComponentInChildren<Camera>().transform;
        standCamY = 1.2f;
        camBaseY = standCamY;
        currentCamY = standCamY;
        currentStamina = maxStamina;
        cam.localPosition = new Vector3(cam.localPosition.x, currentCamY, cam.localPosition.z);
        Cursor.lockState = CursorLockMode.Locked;

        if (GetComponent<StaminaHUD>() == null)
            gameObject.AddComponent<StaminaHUD>();
    }

    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = Mathf.Clamp(value, 0.5f, 10f);
    }

    private void OnDisable()
    {
        // Do not carry camera smoothing momentum through a pause/menu.
        yawVel = 0f;
        pitchVel = 0f;
    }

    void Update()
    {
        // The pause menu releases the cursor and sets timeScale to zero. Input
        // axes are not time-scaled, so they must be blocked explicitly here.
        // Escape is also checked to prevent a one-frame camera twitch if this
        // Update happens before MainMenuController opens the menu.
        if (Time.timeScale <= 0f
            || Cursor.lockState != CursorLockMode.Locked
            || Input.GetKeyDown(KeyCode.Escape))
        {
            yawVel = 0f;
            pitchVel = 0f;
            return;
        }

        // Debug keys: K triggers death, L triggers escape sequence
        if (Input.GetKeyDown(debugDeathKey))
        {
            var deathSeq = GetComponent<DeathSequence>();
            if (deathSeq != null)
                deathSeq.PlayDeathSequence();
        }

        if (Input.GetKeyDown(debugEscapeKey))
        {
            var escapeSeq = GetComponent<EscapeSequence>();
            if (escapeSeq != null)
                escapeSeq.PlayEscapeSequence();
        }

        // Cheat: triple-tap Tab within 1 second to auto-collect all 20 tapes
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            float now = Time.time;
            if (tabPressIndex > 0 && now - tabPressTimes[tabPressIndex - 1] > 1f)
                tabPressIndex = 0;
            tabPressTimes[tabPressIndex] = now;
            tabPressIndex++;
            if (tabPressIndex >= 3)
            {
                tabPressIndex = 0;
                int needed = 20 - TapeInteract.tapesCollected;
                if (needed > 0)
                {
                    TapeInteract.tapesCollected = 20;
                    TapeInteract[] tapes = FindObjectsByType<TapeInteract>(FindObjectsSortMode.None);
                    foreach (var t in tapes) Destroy(t.gameObject);
                    Debug.Log($"[CHEAT] Auto-collected {needed} tapes. Total: {TapeInteract.tapesCollected}/20");
                }
            }
        }

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        float targetYaw = mx;
        float targetPitch = -my;
        yawVel = Mathf.Lerp(yawVel, targetYaw, 1f - lookLag);
        pitchVel = Mathf.Lerp(pitchVel, targetPitch, 1f - lookLag);

        pitch = Mathf.Clamp(pitch + pitchVel, -85f, 85f);
        transform.Rotate(0f, yawVel, 0f);
        cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Crouch state
        isCrouching = Input.GetKey(KeyCode.LeftControl);

        // Smooth camera height transition
        float targetCamY = isCrouching ? crouchCamHeight : standCamY;
        currentCamY = Mathf.Lerp(currentCamY, targetCamY, Time.deltaTime * crouchTransitionSpeed);

        // Adjust collider when crouching
        float targetHeight = isCrouching ? crouchColliderHeight : standColliderHeight;
        if (!Mathf.Approximately(controller.height, targetHeight))
        {
            controller.height = targetHeight;
            controller.center = new Vector3(0f, targetHeight * 0.5f, 0f);
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 move = transform.right * h + transform.forward * v;
        if (move.sqrMagnitude > 1f) move.Normalize();

        bool inputMoving = (Mathf.Abs(h) + Mathf.Abs(v)) > 0.1f;
        bool wantsToSprint = !isCrouching
            && Input.GetKey(KeyCode.LeftShift)
            && inputMoving
            && controller.isGrounded;
        bool sprinting = UpdateStamina(wantsToSprint);

        if (Input.GetButtonDown("Jump"))
            lastJumpPressedTime = Time.time;

        float speed;
        if (isCrouching)
            speed = crouchSpeed;
        else if (sprinting)
            speed = sprintSpeed;
        else
            speed = moveSpeed;
        move *= speed;

        bool wasGrounded = controller.isGrounded;
        if (wasGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVel < 0f)
                verticalVel = -2f;
        }

        bool hasBufferedJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canUseCoyoteTime = Time.time - lastGroundedTime <= coyoteTime;
        if (!isCrouching && hasBufferedJump && canUseCoyoteTime)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
        }

        float activeGravity = verticalVel < 0f ? gravity * fallGravityMultiplier : gravity;
        verticalVel += activeGravity * Time.deltaTime;
        move.y = verticalVel;

        controller.Move(move * Time.deltaTime);

        // Head Bob - only when moving on ground
        bool isMoving = controller.isGrounded && inputMoving;
        float freq, amp;
        if (isCrouching)
        {
            freq = crouchBobFrequency;
            amp = crouchBobAmplitude;
        }
        else if (sprinting)
        {
            freq = sprintBobFrequency;
            amp = sprintBobAmplitude;
        }
        else
        {
            freq = walkBobFrequency;
            amp = walkBobAmplitude;
        }

        float targetBobWeight = isMoving ? 1f : 0f;
        bobWeight = Mathf.MoveTowards(bobWeight, targetBobWeight, bobBlendSpeed * Time.deltaTime);

        if (isMoving)
            bobTimer += Time.deltaTime * freq;
        else if (bobWeight <= 0f)
            bobTimer = 0f;

        float bobY = Mathf.Sin(bobTimer * 2f) * amp * bobWeight;
        float bobX = Mathf.Cos(bobTimer) * amp * 0.5f * bobWeight;

        // Footstep audio: trigger when the bob sine wave crosses zero upward
        if (isMoving && footstepSource != null && footstepClips != null && footstepClips.Length > 0)
        {
            float currentPhase = Mathf.Sin(bobTimer * 2f);
            if (lastFootstepPhase <= 0f && currentPhase > 0f)
            {
                int idx = Random.Range(0, footstepClips.Length);
                footstepSource.PlayOneShot(footstepClips[idx], footstepVolume);
            }
            lastFootstepPhase = currentPhase;
        }
        else if (!isMoving)
        {
            lastFootstepPhase = 0f;
        }

        cam.localPosition = new Vector3(
            bobX,
            currentCamY + bobY,
            cam.localPosition.z
        );
    }

    private bool UpdateStamina(bool wantsToSprint)
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        staminaRestartThreshold = Mathf.Clamp(staminaRestartThreshold, 0f, maxStamina);

        bool canSprint = wantsToSprint && !isExhausted && currentStamina > 0f;
        if (canSprint)
        {
            currentStamina = Mathf.Max(0f, currentStamina - staminaDrainRate * Time.deltaTime);
            staminaRegenTimer = 0f;

            if (currentStamina <= 0f)
            {
                isExhausted = true;
                canSprint = false;
            }
        }
        else
        {
            staminaRegenTimer += Time.deltaTime;
            if (staminaRegenTimer >= staminaRegenDelay)
            {
                currentStamina = Mathf.MoveTowards(
                    currentStamina,
                    maxStamina,
                    staminaRegenRate * Time.deltaTime);
            }
        }

        if (isExhausted && currentStamina >= staminaRestartThreshold)
            isExhausted = false;

        return canSprint;
    }
}
