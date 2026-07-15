using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayerPrototype
{
    public sealed class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("Animation")]
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float pressedScale = 0.96f;
        [SerializeField] private float animationSpeed = 12f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private float hoverVolume = 0.5f;
        [SerializeField] private float clickVolume = 0.8f;

        private Button button;
        private Vector3 initialScale;
        private Vector3 desiredScale;
        private bool pointerInside;
        private bool hoverSoundPlayed;

        private void Awake()
        {
            if (targetRect == null)
            {
                targetRect = GetComponent<RectTransform>();
            }

            button = GetComponent<Button>();

            if (targetRect != null)
            {
                // The authored scale is the baseline for hover and pressed feedback.
                initialScale = targetRect.localScale;
                desiredScale = initialScale;
            }
        }

        private void Update()
        {
            if (targetRect == null)
            {
                return;
            }

            // Unscaled time keeps menu feedback responsive while gameplay is paused.
            float t = Mathf.Clamp01(animationSpeed * Time.unscaledDeltaTime);
            targetRect.localScale = Vector3.Lerp(targetRect.localScale, desiredScale, t);
        }

        private void OnDisable()
        {
            pointerInside = false;
            hoverSoundPlayed = false;
            RestoreInitialScale();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;

            if (!CanPlayFeedback())
            {
                desiredScale = initialScale;
                return;
            }

            desiredScale = initialScale * hoverScale;

            if (!hoverSoundPlayed)
            {
                // Play hover audio once per continuous stay on this button.
                PlayOneShot(hoverClip, hoverVolume);
                hoverSoundPlayed = true;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            hoverSoundPlayed = false;
            desiredScale = initialScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanPlayFeedback())
            {
                desiredScale = initialScale;
                return;
            }

            desiredScale = initialScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!CanPlayFeedback())
            {
                desiredScale = initialScale;
                return;
            }

            desiredScale = pointerInside ? initialScale * hoverScale : initialScale;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (CanPlayFeedback())
            {
                PlayOneShot(clickClip, clickVolume);
            }
        }

        private bool CanPlayFeedback()
        {
            return button == null || button.interactable;
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        private void RestoreInitialScale()
        {
            desiredScale = initialScale;

            if (targetRect != null)
            {
                targetRect.localScale = initialScale;
            }
        }
    }
}
