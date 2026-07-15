using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PlayerPrototype
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Menu References")]
        [SerializeField] private GameObject mainMenuRoot;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private bool hideMainMenuSiblingsDuringGameplay = true;

        [Header("Button References")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button backToMainMenuButton;

        [Header("State")]
        [SerializeField] private bool canContinue = false;
        [SerializeField] private bool pauseGameWhileMenuOpen = true;

        [Header("Menu Music")]
        [SerializeField] private AudioSource menuMusicSource;

        [Header("Gameplay BGM")]
        [SerializeField] private AudioSource gameplayBgmSource;

        [Header("Start Transition")]
        [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.8f;
        [SerializeField, Min(0f)] private float blackHoldDuration = 0.25f;
        [SerializeField, Min(0f)] private float fadeFromBlackDuration = 1.6f;

        private AudioListener menuAudioListener;
        private bool continueUnavailableWarned;
        private bool gameplayStarted;
        private bool settingsOpenedFromGameplay;
        private static bool pendingNewGame;
        private static bool pendingFadeIn;
        private bool isTransitioning;
        private Camera menuCamera;
        private global::PlayerController playerController;
        private GameObject fadeCanvasObject;
        private CanvasGroup fadeCanvasGroup;

        private void Awake()
        {
            ResolveSettingsButtonReferences();
        }

        private void OnEnable()
        {
            RegisterButtonListeners();
        }

        private void Start()
        {
            LogMissingReferences();

            if (pendingNewGame)
            {
                pendingNewGame = false;
                Time.timeScale = 1f;
                LockAndHideCursor();
                gameplayStarted = true;
                SetObjectActive(mainMenuRoot, false);
                SetObjectActive(settingsPanel, false);
                SetMainMenuSiblingVisualsActive(false);
                SetObjectActive(gameplayRoot, true);

                if (pendingFadeIn)
                {
                    pendingFadeIn = false;
                    StartCoroutine(FadeInFromBlack());
                }
                return;
            }

            SetObjectActive(mainMenuRoot, true);
            SetObjectActive(settingsPanel, false);
            SetMainMenuSiblingVisualsActive(true);
            SetObjectActive(gameplayRoot, false);
            SetMenuCameraActive(true);
            ConfigureSettingsButtons(false);
            RefreshContinueButton();
            PlayMenuMusic();

            if (pauseGameWhileMenuOpen)
            {
                Time.timeScale = 0f;
            }

            UnlockAndShowCursor();
        }

        private void OnDisable()
        {
            UnregisterButtonListeners();
        }

        private void Update()
        {
            if (isTransitioning)
                return;

            if (!WasEscapePressed())
            {
                return;
            }

            if (IsObjectVisible(settingsPanel))
            {
                CloseSettingsPanel();
                return;
            }

            if (IsObjectVisible(mainMenuRoot))
            {
                return;
            }

            OpenGameplaySettings();
        }

        public void StartNewGame()
        {
            if (!isTransitioning)
            {
                StartCoroutine(StartNewGameWithFade());
            }
        }

        private IEnumerator StartNewGameWithFade()
        {
            isTransitioning = true;
            EnsureFadeOverlay();
            fadeCanvasObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;

            yield return FadeOverlayTo(1f, fadeToBlackDuration);

            CheckpointManager.StaticClearSave();
            TVInteract.submittedTapes = 0;

            pendingNewGame = true;
            pendingFadeIn = true;
            StopMenuMusic();
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.name);
        }

        private IEnumerator FadeInFromBlack()
        {
            EnsureFadeOverlay();
            fadeCanvasObject.SetActive(true);
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true;

            if (blackHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(blackHoldDuration);

            yield return FadeOverlayTo(0f, fadeFromBlackDuration);

            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasObject.SetActive(false);
            isTransitioning = false;
        }

        public void ContinueGame()
        {
            if (!CheckpointManager.StaticHasSave())
            {
                if (!continueUnavailableWarned)
                {
                    continueUnavailableWarned = true;
                    Debug.Log("Continue game is unavailable because no save exists.", this);
                }
                return;
            }

            if (!isTransitioning)
            {
                StartCoroutine(ContinueGameWithFade());
            }
        }

        private IEnumerator ContinueGameWithFade()
        {
            isTransitioning = true;
            EnsureFadeOverlay();
            fadeCanvasObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;

            yield return FadeOverlayTo(1f, fadeToBlackDuration);

            Time.timeScale = 1f;
            LockAndHideCursor();
            gameplayStarted = true;
            SetObjectActive(mainMenuRoot, false);
            SetObjectActive(settingsPanel, false);
            SetMainMenuSiblingVisualsActive(false);
            SetObjectActive(gameplayRoot, true);
            SetMenuCameraActive(false);
            StopMenuMusic();
            PlayGameplayBgm();

            // Ensure GameManager exists
            if (GameManager.Instance == null)
            {
                var gm = new GameObject("GameManager");
                gm.AddComponent<GameManager>();
            }

            if (GameManager.Instance != null)
                GameManager.Instance.ContinueGame();

            yield return null;

            if (blackHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(blackHoldDuration);

            yield return FadeOverlayTo(0f, fadeFromBlackDuration);

            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasObject.SetActive(false);
            isTransitioning = false;
        }

        public void OpenSettings()
        {
            settingsOpenedFromGameplay = false;
            SetMainMenuSiblingVisualsActive(true);
            SetObjectActive(mainMenuRoot, true);
            SetObjectActive(settingsPanel, true);
            ConfigureSettingsButtons(false);
            PauseIfMenuPausesGame();
            UnlockAndShowCursor();
        }

        public void CloseSettings()
        {
            CloseSettingsPanel();
        }

        public void ReturnToMainMenu()
        {
            gameplayStarted = false;
            settingsOpenedFromGameplay = false;
            SetMainMenuSiblingVisualsActive(true);
            SetObjectActive(mainMenuRoot, true);
            SetObjectActive(settingsPanel, false);
            SetObjectActive(gameplayRoot, false);
            SetMenuCameraActive(true);
            StopGameplayBgm();
            PlayMenuMusic();
            ConfigureSettingsButtons(false);
            PauseIfMenuPausesGame();
            UnlockAndShowCursor();
            RefreshContinueButton();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RegisterButtonListeners()
        {
            RegisterButton(startButton, StartNewGame);
            RegisterButton(continueButton, ContinueGame);
            RegisterButton(settingsButton, OpenSettings);
            RegisterButton(quitButton, QuitGame);
            RegisterButton(backButton, CloseSettings);
            RegisterButton(backToMainMenuButton, ReturnToMainMenu);
        }

        private void UnregisterButtonListeners()
        {
            UnregisterButton(startButton, StartNewGame);
            UnregisterButton(continueButton, ContinueGame);
            UnregisterButton(settingsButton, OpenSettings);
            UnregisterButton(quitButton, QuitGame);
            UnregisterButton(backButton, CloseSettings);
            UnregisterButton(backToMainMenuButton, ReturnToMainMenu);
        }

        private static void RegisterButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void UnregisterButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private void LogMissingReferences()
        {
            LogMissing(mainMenuRoot, nameof(mainMenuRoot));
            LogMissing(settingsPanel, nameof(settingsPanel));
            LogMissing(startButton, nameof(startButton));
            LogMissing(continueButton, nameof(continueButton));
            LogMissing(settingsButton, nameof(settingsButton));
            LogMissing(quitButton, nameof(quitButton));
            LogMissing(backButton, nameof(backButton));
            LogMissing(backToMainMenuButton, nameof(backToMainMenuButton));
        }

        private void ResolveSettingsButtonReferences()
        {
            if (settingsPanel == null)
            {
                return;
            }

            if (backButton == null)
            {
                backButton = FindButtonInSettingsPanel("BackButton");
            }

            if (backToMainMenuButton == null)
            {
                backToMainMenuButton = FindButtonInSettingsPanel("BackToMainMenuButton");
            }
        }

        private Button FindButtonInSettingsPanel(string buttonName)
        {
            Button[] buttons = settingsPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button != null && button.name == buttonName)
                {
                    return button;
                }
            }

            return null;
        }

        private void OpenGameplaySettings()
        {
            if (!gameplayStarted)
            {
                ReturnToMainMenu();
                return;
            }

            settingsOpenedFromGameplay = true;
            SetObjectActive(mainMenuRoot, false);
            SetObjectActive(settingsPanel, true);
            SetMainMenuSiblingVisualsActive(false);
            ConfigureSettingsButtons(true);
            PauseIfMenuPausesGame();
            UnlockAndShowCursor();
        }

        private void CloseSettingsPanel()
        {
            SetObjectActive(settingsPanel, false);

            if (settingsOpenedFromGameplay && gameplayStarted)
            {
                settingsOpenedFromGameplay = false;
                SetObjectActive(mainMenuRoot, false);
                SetMainMenuSiblingVisualsActive(false);
                SetObjectActive(gameplayRoot, true);
                Time.timeScale = 1f;
                LockAndHideCursor();
                return;
            }

            settingsOpenedFromGameplay = false;
            SetMainMenuSiblingVisualsActive(true);
            SetObjectActive(mainMenuRoot, true);
            PauseIfMenuPausesGame();
            UnlockAndShowCursor();
        }

        private void ConfigureSettingsButtons(bool openedFromGameplay)
        {
            SetButtonVisible(backButton, openedFromGameplay);
            SetButtonVisible(backToMainMenuButton, true);
        }

        private void RefreshContinueButton()
        {
            bool hasSave = CheckpointManager.StaticHasSave();
            if (continueButton != null)
            {
                continueButton.interactable = hasSave;
            }
        }

        private void SetMainMenuSiblingVisualsActive(bool active)
        {
            if (!hideMainMenuSiblingsDuringGameplay || mainMenuRoot == null)
            {
                return;
            }

            Transform mainMenuTransform = mainMenuRoot.transform;
            Transform parent = mainMenuTransform.parent;
            if (parent == null)
            {
                return;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null || child == transform)
                {
                    continue;
                }

                GameObject childObject = child.gameObject;
                if (childObject == mainMenuRoot || childObject == settingsPanel || childObject == gameplayRoot)
                {
                    continue;
                }

                childObject.SetActive(active);
            }
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private void PauseIfMenuPausesGame()
        {
            if (pauseGameWhileMenuOpen)
            {
                Time.timeScale = 0f;
            }
        }

        private static void LockAndHideCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void UnlockAndShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static void SetObjectActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static bool IsObjectVisible(GameObject target)
        {
            return target != null && target.activeSelf;
        }

        private void PlayMenuMusic()
        {
            if (menuMusicSource != null && menuMusicSource.clip != null)
            {
                menuMusicSource.Stop();
                menuMusicSource.Play();
            }
        }

        private void StopMenuMusic()
        {
            if (menuMusicSource != null)
            {
                menuMusicSource.Stop();
            }
        }

        private void PlayGameplayBgm()
        {
            if (gameplayBgmSource != null && gameplayBgmSource.clip != null)
            {
                gameplayBgmSource.Stop();
                gameplayBgmSource.Play();
            }
        }

        private void StopGameplayBgm()
        {
            if (gameplayBgmSource != null && gameplayBgmSource.isPlaying)
            {
                gameplayBgmSource.Stop();
            }
        }

        private void SetMenuCameraActive(bool active)
        {
            if (active && menuCamera == null)
            {
                GameObject cameraObject = new GameObject("RuntimeMenuCamera");
                menuCamera = cameraObject.AddComponent<Camera>();
                menuAudioListener = cameraObject.AddComponent<AudioListener>();

                menuCamera.clearFlags = CameraClearFlags.SolidColor;
                menuCamera.backgroundColor = Color.black;
                menuCamera.cullingMask = 0;
                menuCamera.depth = -100f;
            }

            if (menuCamera != null)
                menuCamera.enabled = active;

            if (menuAudioListener != null)
                menuAudioListener.enabled = active;
        }

        private void SetPlayerControlEnabled(bool enabled)
        {
            if (playerController == null)
            {
                playerController = FindObjectOfType<global::PlayerController>(true);
            }

            if (playerController != null)
            {
                playerController.enabled = enabled;
            }
        }

        private void EnsureFadeOverlay()
        {
            if (fadeCanvasObject != null)
                return;

            fadeCanvasObject = new GameObject(
                "RuntimeFadeCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup),
                typeof(GraphicRaycaster));

            Canvas canvas = fadeCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;

            fadeCanvasGroup = fadeCanvasObject.GetComponent<CanvasGroup>();
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.interactable = false;
            fadeCanvasGroup.blocksRaycasts = false;

            GameObject imageObject = new GameObject(
                "BlackFade",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(fadeCanvasObject.transform, false);

            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            fadeCanvasObject.SetActive(false);
        }

        private IEnumerator FadeOverlayTo(float targetAlpha, float duration)
        {
            float startAlpha = fadeCanvasGroup.alpha;

            if (duration <= 0f)
            {
                fadeCanvasGroup.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, easedProgress);
                yield return null;
            }

            fadeCanvasGroup.alpha = targetAlpha;
        }

        private void LogMissing(UnityEngine.Object target, string fieldName)
        {
            if (target == null)
            {
                Debug.LogError("MainMenuController is missing required Inspector reference: " + fieldName + ".", this);
            }
        }

        private bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
