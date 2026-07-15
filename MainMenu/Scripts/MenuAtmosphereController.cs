using UnityEngine;

namespace PlayerPrototype
{
    public sealed class MenuAtmosphereController : MonoBehaviour
    {
        private const float FullCycle = Mathf.PI * 2f;
        private const float MinGlitchPulseInterval = 0.018f;
        private const float MaxGlitchPulseInterval = 0.045f;

        [Header("Title")]
        [SerializeField] private RectTransform titleRect;
        [SerializeField] private float titleMoveRangeX = 7f;
        [SerializeField] private float titleMoveRangeY = 5f;
        [SerializeField] private float titleRotationRange = 0.35f;
        [SerializeField] private float minTitleTwitchInterval = 0.35f;
        [SerializeField] private float maxTitleTwitchInterval = 1.25f;
        [SerializeField] private float minTitleTwitchDuration = 0.035f;
        [SerializeField] private float maxTitleTwitchDuration = 0.09f;

        [Header("Background")]
        [SerializeField] private RectTransform backgroundBase;
        [SerializeField] private RectTransform backgroundOverlay;
        [SerializeField] private CanvasGroup backgroundOverlayCanvasGroup;
        [SerializeField] private float backgroundMoveRange = 4f;
        [SerializeField] private float backgroundMoveSpeed = 0.08f;
        [SerializeField] private float backgroundScaleAmount = 0.015f;
        [SerializeField] private float backgroundScaleSpeed = 0.06f;

        [Header("Flicker")]
        [SerializeField] private float minFlickerInterval = 2.5f;
        [SerializeField] private float maxFlickerInterval = 7f;
        [SerializeField] private float flickerDuration = 0.12f;
        [SerializeField] private float normalOverlayAlpha = 0.15f;
        [SerializeField] private float flickerOverlayAlpha = 0.42f;
        [SerializeField] private float glitchOffset = 8f;

        private RectState titleInitialState;
        private RectState backgroundBaseInitialState;
        private RectState backgroundOverlayInitialState;
        private float backgroundOverlayInitialAlpha;
        private bool hasBackgroundOverlayInitialAlpha;

        private float nextTitleTwitchTime;
        private float titleTwitchTimer;
        private float titleTwitchDuration;
        private bool isTitleTwitching;
        private Vector3 currentTitleTwitchOffset;
        private float currentTitleTwitchRotation;

        private float nextFlickerTime;
        private float flickerTimer;
        private float nextGlitchPulseTime;
        private float currentGlitchAlpha;
        private bool isFlickering;
        private Vector3 currentGlitchOffset;
        private Vector3 currentGlitchScale;

        private void Awake()
        {
            // Capture once so temporary menu effects never accumulate across disable/enable cycles.
            titleInitialState = CaptureState(titleRect);
            backgroundBaseInitialState = CaptureState(backgroundBase);
            backgroundOverlayInitialState = CaptureState(backgroundOverlay);

            if (backgroundOverlayCanvasGroup != null)
            {
                backgroundOverlayInitialAlpha = backgroundOverlayCanvasGroup.alpha;
                hasBackgroundOverlayInitialAlpha = true;
            }
        }

        private void OnEnable()
        {
            ScheduleNextTitleTwitch();
            ScheduleNextFlicker();
            ApplyNormalOverlayAlpha();
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            float deltaTime = Time.unscaledDeltaTime;

            AnimateTitle(time, deltaTime);
            AnimateBackgroundBase(time);
            UpdateFlicker(time, deltaTime);
            AnimateBackgroundOverlay(time);
        }

        private void OnDisable()
        {
            RestoreInitialStates();
        }

        private void OnDestroy()
        {
            RestoreInitialStates();
        }

        private void AnimateTitle(float time, float deltaTime)
        {
            if (titleRect == null || !titleInitialState.IsValid)
            {
                return;
            }

            UpdateTitleTwitch(time, deltaTime);

            if (!isTitleTwitching)
            {
                titleRect.anchoredPosition3D = titleInitialState.AnchoredPosition;
                titleRect.localRotation = titleInitialState.LocalRotation;
                return;
            }

            float normalizedTime = Mathf.Clamp01(titleTwitchTimer / Mathf.Max(0.01f, titleTwitchDuration));
            float snap = 1f - normalizedTime;
            float stutter = Mathf.Sign(Mathf.Sin(normalizedTime * FullCycle * 3f));

            titleRect.anchoredPosition3D = titleInitialState.AnchoredPosition + currentTitleTwitchOffset * snap * stutter;
            titleRect.localRotation = titleInitialState.LocalRotation * Quaternion.Euler(0f, 0f, currentTitleTwitchRotation * snap);
        }

        private void UpdateTitleTwitch(float time, float deltaTime)
        {
            if (!isTitleTwitching)
            {
                if (time >= nextTitleTwitchTime)
                {
                    BeginTitleTwitch();
                }

                return;
            }

            titleTwitchTimer += deltaTime;

            if (titleTwitchTimer >= Mathf.Max(0.01f, titleTwitchDuration))
            {
                EndTitleTwitch();
            }
        }

        private void BeginTitleTwitch()
        {
            isTitleTwitching = true;
            titleTwitchTimer = 0f;

            float minDuration = Mathf.Max(0.01f, minTitleTwitchDuration);
            float maxDuration = Mathf.Max(minDuration, maxTitleTwitchDuration);
            titleTwitchDuration = Random.Range(minDuration, maxDuration);

            currentTitleTwitchOffset = new Vector3(
                Random.Range(-titleMoveRangeX, titleMoveRangeX),
                Random.Range(-titleMoveRangeY, titleMoveRangeY),
                0f);
            currentTitleTwitchRotation = Random.Range(-titleRotationRange, titleRotationRange);
        }

        private void EndTitleTwitch()
        {
            isTitleTwitching = false;
            titleTwitchTimer = 0f;
            currentTitleTwitchOffset = Vector3.zero;
            currentTitleTwitchRotation = 0f;
            ScheduleNextTitleTwitch();
        }

        private void ScheduleNextTitleTwitch()
        {
            float minInterval = Mathf.Max(0.01f, minTitleTwitchInterval);
            float maxInterval = Mathf.Max(minInterval, maxTitleTwitchInterval);
            nextTitleTwitchTime = Time.unscaledTime + Random.Range(minInterval, maxInterval);
        }

        private void AnimateBackgroundBase(float time)
        {
            if (backgroundBase == null || !backgroundBaseInitialState.IsValid)
            {
                return;
            }

            float offsetX = Mathf.Sin(time * backgroundMoveSpeed * FullCycle) * backgroundMoveRange;
            float offsetY = Mathf.Sin((time * backgroundMoveSpeed * 0.73f * FullCycle) + 2.4f) * backgroundMoveRange;
            float scaleOffset = Mathf.Sin(time * backgroundScaleSpeed * FullCycle) * backgroundScaleAmount;

            backgroundBase.anchoredPosition3D = backgroundBaseInitialState.AnchoredPosition + new Vector3(offsetX, offsetY, 0f);
            backgroundBase.localScale = backgroundBaseInitialState.LocalScale * (1f + scaleOffset);
        }

        private void UpdateFlicker(float time, float deltaTime)
        {
            if (!isFlickering)
            {
                if (time >= nextFlickerTime)
                {
                    BeginFlicker();
                }

                return;
            }

            flickerTimer += deltaTime;

            if (flickerTimer >= Mathf.Max(0.01f, flickerDuration))
            {
                EndFlicker();
                return;
            }

            if (time >= nextGlitchPulseTime)
            {
                RandomizeGlitchPulse(time);
            }
        }

        private void AnimateBackgroundOverlay(float time)
        {
            if (backgroundOverlay == null || !backgroundOverlayInitialState.IsValid)
            {
                return;
            }

            float driftX = Mathf.Sin((time * backgroundMoveSpeed * 1.13f * FullCycle) + 0.9f) * backgroundMoveRange * 0.5f;
            float driftY = Mathf.Sin((time * backgroundMoveSpeed * 0.91f * FullCycle) + 3.8f) * backgroundMoveRange * 0.5f;
            float scaleOffset = Mathf.Sin((time * backgroundScaleSpeed * 1.21f * FullCycle) + 1.6f) * backgroundScaleAmount;

            Vector3 position = backgroundOverlayInitialState.AnchoredPosition + new Vector3(driftX, driftY, 0f);
            Vector3 scale = backgroundOverlayInitialState.LocalScale * (1f + scaleOffset);

            if (isFlickering)
            {
                float normalizedTime = Mathf.Clamp01(flickerTimer / Mathf.Max(0.01f, flickerDuration));
                float fadeOut = 1f - normalizedTime;

                position += currentGlitchOffset * fadeOut;
                scale += currentGlitchScale * fadeOut;

                if (backgroundOverlayCanvasGroup != null)
                {
                    backgroundOverlayCanvasGroup.alpha = Mathf.Lerp(normalOverlayAlpha, currentGlitchAlpha, fadeOut);
                }
            }
            else if (backgroundOverlayCanvasGroup != null)
            {
                backgroundOverlayCanvasGroup.alpha = normalOverlayAlpha;
            }

            backgroundOverlay.anchoredPosition3D = position;
            backgroundOverlay.localScale = scale;
        }

        private void BeginFlicker()
        {
            isFlickering = true;
            flickerTimer = 0f;
            RandomizeGlitchPulse(Time.unscaledTime);
        }

        private void RandomizeGlitchPulse(float time)
        {
            nextGlitchPulseTime = time + Random.Range(MinGlitchPulseInterval, MaxGlitchPulseInterval);

            // Re-pick the pulse several times per burst for a broken-TV snap instead of a smooth blink.
            Vector2 offsetDirection = Random.insideUnitCircle;
            if (offsetDirection.sqrMagnitude < 0.01f)
            {
                offsetDirection = Vector2.right;
            }

            offsetDirection.Normalize();
            currentGlitchOffset = new Vector3(offsetDirection.x, offsetDirection.y, 0f) * Random.Range(glitchOffset * 0.35f, glitchOffset);
            currentGlitchScale = Vector3.one * Random.Range(-backgroundScaleAmount * 2f, backgroundScaleAmount * 2f);
            currentGlitchAlpha = Random.Range(normalOverlayAlpha, flickerOverlayAlpha);

            if (backgroundOverlayCanvasGroup != null)
            {
                backgroundOverlayCanvasGroup.alpha = currentGlitchAlpha;
            }
        }

        private void EndFlicker()
        {
            isFlickering = false;
            flickerTimer = 0f;
            nextGlitchPulseTime = 0f;
            currentGlitchAlpha = normalOverlayAlpha;
            currentGlitchOffset = Vector3.zero;
            currentGlitchScale = Vector3.zero;
            ScheduleNextFlicker();
            RestoreOverlayNormalState();
        }

        private void ScheduleNextFlicker()
        {
            float minInterval = Mathf.Max(0.01f, minFlickerInterval);
            float maxInterval = Mathf.Max(minInterval, maxFlickerInterval);
            nextFlickerTime = Time.unscaledTime + Random.Range(minInterval, maxInterval);
        }

        private void ApplyNormalOverlayAlpha()
        {
            if (backgroundOverlayCanvasGroup != null)
            {
                backgroundOverlayCanvasGroup.alpha = normalOverlayAlpha;
            }
        }

        private void RestoreOverlayNormalState()
        {
            if (backgroundOverlay != null && backgroundOverlayInitialState.IsValid)
            {
                backgroundOverlay.anchoredPosition3D = backgroundOverlayInitialState.AnchoredPosition;
                backgroundOverlay.localScale = backgroundOverlayInitialState.LocalScale;
                backgroundOverlay.localRotation = backgroundOverlayInitialState.LocalRotation;
            }

            ApplyNormalOverlayAlpha();
        }

        private void RestoreInitialStates()
        {
            isTitleTwitching = false;
            titleTwitchTimer = 0f;
            currentTitleTwitchOffset = Vector3.zero;
            currentTitleTwitchRotation = 0f;

            RestoreState(titleRect, titleInitialState);
            RestoreState(backgroundBase, backgroundBaseInitialState);
            RestoreState(backgroundOverlay, backgroundOverlayInitialState);

            if (backgroundOverlayCanvasGroup != null && hasBackgroundOverlayInitialAlpha)
            {
                backgroundOverlayCanvasGroup.alpha = backgroundOverlayInitialAlpha;
            }
        }

        private static RectState CaptureState(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return RectState.Empty;
            }

            return new RectState(
                rectTransform.anchoredPosition3D,
                rectTransform.localScale,
                rectTransform.localRotation);
        }

        private static void RestoreState(RectTransform rectTransform, RectState state)
        {
            if (rectTransform == null || !state.IsValid)
            {
                return;
            }

            rectTransform.anchoredPosition3D = state.AnchoredPosition;
            rectTransform.localScale = state.LocalScale;
            rectTransform.localRotation = state.LocalRotation;
        }

        private struct RectState
        {
            public static RectState Empty
            {
                get { return new RectState(Vector3.zero, Vector3.one, Quaternion.identity, false); }
            }

            public Vector3 AnchoredPosition;
            public Vector3 LocalScale;
            public Quaternion LocalRotation;
            public bool IsValid;

            public RectState(Vector3 anchoredPosition, Vector3 localScale, Quaternion localRotation)
                : this(anchoredPosition, localScale, localRotation, true)
            {
            }

            private RectState(Vector3 anchoredPosition, Vector3 localScale, Quaternion localRotation, bool isValid)
            {
                AnchoredPosition = anchoredPosition;
                LocalScale = localScale;
                LocalRotation = localRotation;
                IsValid = isValid;
            }
        }
    }
}
