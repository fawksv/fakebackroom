using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 通关逃离演出：禁用玩家控制 → 向前快跑 → 回头看一眼 → 继续向前跑 → 渐白
/// 所有 UI 覆盖层在代码中自动创建，无需手动搭建 Canvas。
/// </summary>
public class EscapeSequence : MonoBehaviour
{
    [Header("时间设置 (秒)")]
    [SerializeField] private float runForwardDuration = 1.5f;
    [SerializeField] private float lookBackDuration = 1.5f;
    [SerializeField] private float runForwardAgainDuration = 2f;
    [SerializeField] private float whiteFadeDuration = 2f;
    [Tooltip("白屏持续后返回主菜单")]
    [SerializeField] private float whiteHoldDuration = 5f;

    [Header("移动设置")]
    [SerializeField] private float escapeRunSpeed = 7f;

    [Header("转头设置")]
    [SerializeField] private float turnDuration = 0.5f;
    [SerializeField] private float lookBackAngle = 180f;

    [Header("脚步声")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float footstepVolume = 1f;
    [SerializeField] private float footstepInterval = 0.28f;

    [Header("相机引用 (留空自动查找)")]
    [SerializeField] private Transform cameraTransform;

    /// <summary>
    /// 逃离演出结束后调用，用于切场景或显示通关界面。
    /// </summary>
    public System.Action OnEscapeComplete;

    private Canvas escapeCanvas;
    private Image whiteFadeImage;
    private AudioSource audioSource;
    private bool isPlaying;

    private void Awake()
    {
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        CreateEscapeCanvas();
    }

    private void CreateEscapeCanvas()
    {
        var canvasObj = new GameObject("EscapeCanvas");
        canvasObj.transform.SetParent(transform, false);

        escapeCanvas = canvasObj.AddComponent<Canvas>();
        escapeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        escapeCanvas.sortingOrder = 1000;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        whiteFadeImage = CreateOverlayImage("WhiteFade", new Color(1, 1, 1, 0));
    }

    private Image CreateOverlayImage(string name, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(escapeCanvas.transform, false);
        var img = obj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    private void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0 || audioSource == null)
            return;
        int idx = Random.Range(0, footstepClips.Length);
        audioSource.PlayOneShot(footstepClips[idx], footstepVolume);
    }

    /// <summary>
    /// 触发逃离演出
    /// </summary>
    public void PlayEscapeSequence()
    {
        if (isPlaying) return;
        isPlaying = true;
        StartCoroutine(EscapeCoroutine());
    }

    private IEnumerator EscapeCoroutine()
    {
        // 禁用玩家控制
        var playerController = GetComponent<PlayerController>();
        if (playerController != null)
            playerController.enabled = false;

        var controller = GetComponent<CharacterController>();
        Vector3 forwardDir = transform.forward;

        // 记录相机原始状态
        Quaternion camOriginalRot = cameraTransform != null ? cameraTransform.localRotation : Quaternion.identity;

        // ---- 阶段 1: 向前快跑 ----
        float elapsed = 0f;
        float footstepTimer = 0f;
        while (elapsed < runForwardDuration)
        {
            elapsed += Time.deltaTime;
            footstepTimer += Time.deltaTime;
            if (controller != null)
                controller.Move(forwardDir * escapeRunSpeed * Time.deltaTime);
            if (footstepTimer >= footstepInterval)
            {
                PlayFootstep();
                footstepTimer = 0f;
            }
            yield return null;
        }

        // ---- 阶段 2: 回头看 ----
        if (cameraTransform != null)
        {
            Quaternion startRot = cameraTransform.localRotation;
            Quaternion targetRot = Quaternion.Euler(0, -lookBackAngle, 0);

            // 转头过去
            elapsed = 0f;
            while (elapsed < turnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / turnDuration);
                // 缓动曲线让转头更自然
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                cameraTransform.localRotation = Quaternion.Slerp(startRot, targetRot, easedT);
                yield return null;
            }

            // 保持回头看的持续时间（减去转头时间）
            float holdTime = Mathf.Max(0f, lookBackDuration - turnDuration);
            if (holdTime > 0f)
                yield return new WaitForSeconds(holdTime);

            // 转回来
            elapsed = 0f;
            Quaternion currentRot = cameraTransform.localRotation;
            while (elapsed < turnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / turnDuration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                cameraTransform.localRotation = Quaternion.Slerp(currentRot, camOriginalRot, easedT);
                yield return null;
            }
        }

        // ---- 阶段 3: 继续向前跑 ----
        elapsed = 0f;
        footstepTimer = 0f;
        while (elapsed < runForwardAgainDuration)
        {
            elapsed += Time.deltaTime;
            footstepTimer += Time.deltaTime;
            if (controller != null)
                controller.Move(forwardDir * escapeRunSpeed * Time.deltaTime);
            if (footstepTimer >= footstepInterval)
            {
                PlayFootstep();
                footstepTimer = 0f;
            }
            yield return null;
        }

        // ---- 阶段 4: 渐白 ----
        elapsed = 0f;
        while (elapsed < whiteFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / whiteFadeDuration);
            whiteFadeImage.color = new Color(1, 1, 1, t);
            yield return null;
        }
        whiteFadeImage.color = Color.white;

        // ---- 阶段 5: 白屏持续5s → 回到主菜单 ----
        yield return new WaitForSeconds(whiteHoldDuration);

        OnEscapeComplete?.Invoke();

        if (OnEscapeComplete == null)
        {
            Debug.Log("[EscapeSequence] 逃离演出结束，返回主菜单。");
            SceneManager.LoadScene(0);
        }
    }
}
