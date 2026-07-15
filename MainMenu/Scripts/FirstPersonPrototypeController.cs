using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlayerPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonPrototypeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform viewTransform;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedVerticalVelocity = -2f;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float minimumPitch = -80f;
        [SerializeField] private float maximumPitch = 80f;

        private CharacterController characterController;
        private float pitch;
        private float verticalVelocity;
        private bool cursorLocked;
        private bool missingViewLogged;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            mouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("PlayerPrototype.MouseSensitivity", mouseSensitivity), 0.5f, 10f);

            if (viewTransform != null)
            {
                pitch = ClampPitch(NormalizeAngle(viewTransform.localEulerAngles.x));
                viewTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void Start()
        {
            SetCursorLocked(true);
            LogMissingViewOnce();
        }

        private void Update()
        {
            UpdateCursorLock();

            if (viewTransform != null && cursorLocked)
            {
                UpdateLook();
            }
            else
            {
                LogMissingViewOnce();
            }

            UpdateMovement();
        }

        private void UpdateLook()
        {
            Vector2 lookInput = ReadLookInput() * mouseSensitivity;

            transform.Rotate(Vector3.up * lookInput.x, Space.Self);

            pitch = ClampPitch(pitch - lookInput.y);
            viewTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            Vector2 moveInput = ReadMoveInput();
            Vector3 horizontalMove = (transform.right * moveInput.x + transform.forward * moveInput.y) * moveSpeed;

            // Keep a small downward velocity while grounded so the controller stays snapped to slopes and floors.
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedVerticalVelocity;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = horizontalMove + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void UpdateCursorLock()
        {
            if (WasEscapePressed())
            {
                SetCursorLocked(false);
            }
            else if (WasLeftMousePressed())
            {
                SetCursorLocked(true);
            }
        }

        private void SetCursorLocked(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            input = ReadMoveInputSystem();
            if (input.sqrMagnitude > 0f)
            {
                return ClampMoveInput(input);
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
#endif

            return ClampMoveInput(input);
        }

        private Vector2 ReadLookInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue() * 0.1f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static Vector2 ReadMoveInputSystem()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            Vector2 input = Vector2.zero;

            if (keyboard.aKey.isPressed)
            {
                input.x -= 1f;
            }

            if (keyboard.dKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.sKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.wKey.isPressed)
            {
                input.y += 1f;
            }

            return input;
        }
#endif

        private bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private bool WasLeftMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Mouse0);
#else
            return false;
#endif
        }

        private static Vector2 ClampMoveInput(Vector2 input)
        {
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        public void SetMouseSensitivity(float value)
        {
            mouseSensitivity = Mathf.Clamp(value, 0.5f, 10f);
        }

        private float ClampPitch(float value)
        {
            return Mathf.Clamp(value, minimumPitch, maximumPitch);
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private void LogMissingViewOnce()
        {
            if (viewTransform != null || missingViewLogged)
            {
                return;
            }

            missingViewLogged = true;
            Debug.LogError("FirstPersonPrototypeController requires a view Transform reference. Create a Camera child and assign it in the inspector.", this);
        }
    }
}
