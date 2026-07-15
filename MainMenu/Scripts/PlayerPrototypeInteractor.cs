using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlayerPrototype
{
    public sealed class PlayerPrototypeInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform viewTransform;

        [Header("Screen Prompt")]
        [SerializeField] private bool drawScreenPrompt = true;
        [SerializeField] private Font promptFont;
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private Color promptColor = Color.white;
        [SerializeField] private int crosshairFontSize = 24;
        [SerializeField] private int promptFontSize = 24;

        [Header("Interaction")]
        [SerializeField] private float interactionDistance = 2.5f;
        [SerializeField] private string interactKeyLabel = "E";

        private IPrototypeInteractable currentTarget;
        private string currentPrompt;
        private bool missingViewLogged;

        private void Start()
        {
            LogMissingViewOnce();
        }

        private void Update()
        {
            currentTarget = FindTarget();
            UpdatePrompt();

            if (currentTarget != null && WasInteractPressed())
            {
                currentTarget.Interact();
            }
        }

        private IPrototypeInteractable FindTarget()
        {
            if (viewTransform == null)
            {
                LogMissingViewOnce();
                return null;
            }

            Ray ray = new Ray(viewTransform.position, viewTransform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<IPrototypeInteractable>();
        }

        private void UpdatePrompt()
        {
            currentPrompt = currentTarget != null ? interactKeyLabel + "  " + currentTarget.InteractionPrompt : string.Empty;
        }

        private void OnGUI()
        {
            if (!drawScreenPrompt)
            {
                return;
            }

            DrawCenteredLabel("+", Screen.height * 0.5f - 18f, crosshairFontSize, crosshairColor);

            if (!string.IsNullOrEmpty(currentPrompt))
            {
                DrawCenteredLabel(currentPrompt, Screen.height * 0.5f + 32f, promptFontSize, promptColor);
            }
        }

        private bool WasInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        private void LogMissingViewOnce()
        {
            if (viewTransform != null || missingViewLogged)
            {
                return;
            }

            missingViewLogged = true;
            Debug.LogError("PlayerPrototypeInteractor requires a view Transform reference. Assign the same Camera child used by FirstPersonPrototypeController.", this);
        }

        private void DrawCenteredLabel(string text, float y, int fontSize, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = fontSize;
            style.normal.textColor = color;

            if (promptFont != null)
            {
                style.font = promptFont;
            }

            GUI.Label(new Rect(0f, y, Screen.width, fontSize + 12f), text, style);
        }
    }
}
