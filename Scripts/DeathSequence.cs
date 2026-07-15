using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 死亡演出：怪物突脸5s（剧烈摇晃+BacteriaScream）→ 怪物保持+蓝屏频闪5s → 蓝屏常亮3s → 回到主菜单
/// 所有 UI 覆盖层在代码中自动创建，无需手动搭建 Canvas。
/// </summary>
public class DeathSequence : MonoBehaviour
{
    [Header("时间设置 (秒)")]
    [Tooltip("突脸持续时间（期间剧烈摇晃+播放BacteriaScream）")]
    [SerializeField] private float jumpScareDuration = 5f;
    [Tooltip("怪物保持+蓝屏频闪持续时间")]
    [SerializeField] private float flickerDuration = 5f;
    [Tooltip("蓝屏常亮持续时间")]
    [SerializeField] private float blueScreenHoldDuration = 3f;

    [Header("视觉效果")]
    [Tooltip("怪物突脸贴图（放在 Assets/Resources/ 下，填写文件名不含扩展名）")]
    [SerializeField] private string monsterFaceResourceName = "MonsterFace";
    [Tooltip("相机抖动强度")]
    [SerializeField] private float shakeIntensity = 0.3f;
    [Tooltip("蓝屏颜色（默认 Windows BSOD 蓝）")]
    [SerializeField] private Color blueScreenColor = new Color(0f, 0.4706f, 0.8431f, 1f);
    [Tooltip("频闪速度（每秒闪烁次数）")]
    [SerializeField] private float flickerSpeed = 12f;

    [Header("音频")]
    [Tooltip("突脸后播放的音效（放在 Assets/Resources/ 下，填写文件名不含扩展名）")]
    [SerializeField] private string roarClipResourceName = "BacteriaScream";
    [SerializeField] private float roarVolume = 1f;

    [Header("相机引用 (留空自动查找)")]
    [SerializeField] private Transform cameraTransform;

    /// <summary>
    /// 死亡演出结束后调用，用于触发复活/回到存档点。
    /// 存档系统完成后在此接入复活逻辑。
    /// </summary>
    public System.Action OnRespawnRequested;

    private Canvas deathCanvas;
    private Image jumpScareImage;
    private Image blueScreenImage;
    private AudioSource audioSource;
    private bool isPlaying;

    private void Awake()
    {
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        CreateDeathCanvas();
    }

    private void CreateDeathCanvas()
    {
        var canvasObj = new GameObject("DeathCanvas");
        canvasObj.transform.SetParent(transform, false);

        deathCanvas = canvasObj.AddComponent<Canvas>();
        deathCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        deathCanvas.sortingOrder = 1000;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // 突脸图片
        jumpScareImage = CreateOverlayImage("JumpScareImage", new Color(1, 1, 1, 0));
        Texture2D faceTex = LoadResourceTexture(monsterFaceResourceName);
        if (faceTex != null)
        {
            jumpScareImage.sprite = Sprite.Create(
                faceTex,
                new Rect(0, 0, faceTex.width, faceTex.height),
                Vector2.one * 0.5f
            );
            jumpScareImage.type = Image.Type.Simple;
            jumpScareImage.preserveAspect = true;
        }

        // 蓝屏覆盖
        blueScreenImage = CreateOverlayImage("BlueScreen", new Color(blueScreenColor.r, blueScreenColor.g, blueScreenColor.b, 0));
    }

    private Image CreateOverlayImage(string name, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(deathCanvas.transform, false);
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

    private Texture2D LoadResourceTexture(string resName)
    {
        if (string.IsNullOrEmpty(resName)) return null;
        return Resources.Load<Texture2D>(resName);
    }

    /// <summary>
    /// 触发死亡演出
    /// </summary>
    public void PlayDeathSequence()
    {
        if (isPlaying) return;
        isPlaying = true;

        if (deathCanvas != null)
            deathCanvas.gameObject.SetActive(true);

        StartCoroutine(DeathCoroutine());
    }

    /// <summary>
    /// 重置死亡演出状态，以便下次死亡时可以再次播放。
    /// 由 CheckpointManager 在复活后调用。
    /// </summary>
    public void ResetSequence()
    {
        isPlaying = false;

        if (deathCanvas != null)
            deathCanvas.gameObject.SetActive(false);

        if (jumpScareImage != null)
            jumpScareImage.color = new Color(1, 1, 1, 0);

        if (blueScreenImage != null)
            blueScreenImage.color = new Color(blueScreenColor.r, blueScreenColor.g, blueScreenColor.b, 0);
    }

    private IEnumerator DeathCoroutine()
    {
        // 禁用玩家控制
        var playerController = GetComponent<PlayerController>();
        if (playerController != null)
            playerController.enabled = false;

        Vector3 camOriginalLocalPos = cameraTransform != null ? cameraTransform.localPosition : Vector3.zero;
        Quaternion camOriginalLocalRot = cameraTransform != null ? cameraTransform.localRotation : Quaternion.identity;

        // ---- 阶段 1: 怪物突脸5s + 剧烈摇晃 + BacteriaScream ----
        if (jumpScareImage.sprite != null)
            jumpScareImage.color = Color.white;
        else
            // 没有贴图也要有视觉反馈，用纯白屏替代
            jumpScareImage.color = new Color(0, 0, 0, 1);

        // 播放 BacteriaScream
        AudioClip roarClip = null;
        if (!string.IsNullOrEmpty(roarClipResourceName))
            roarClip = Resources.Load<AudioClip>(roarClipResourceName);
        if (roarClip != null)
            audioSource.PlayOneShot(roarClip, roarVolume);

        float elapsed = 0f;
        while (elapsed < jumpScareDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            // 剧烈相机抖动（保持高强度，仅最后阶段略微衰减）
            if (cameraTransform != null)
            {
                float shakeT = 1f - (elapsed / jumpScareDuration);
                // 最低保持 60% 强度，确保全程剧烈
                float currentShake = shakeIntensity * Mathf.Max(0.6f, shakeT);
                Vector3 shakeOffset = Random.insideUnitSphere * currentShake;
                cameraTransform.localPosition = camOriginalLocalPos + shakeOffset;
                cameraTransform.localRotation = camOriginalLocalRot * Quaternion.Euler(
                    Random.Range(-1f, 1f) * currentShake * 90f,
                    Random.Range(-1f, 1f) * currentShake * 90f,
                    Random.Range(-1f, 1f) * currentShake * 90f
                );
            }

            yield return null;
        }

        // 隐藏突脸
        jumpScareImage.color = new Color(1, 1, 1, 0);

        // 恢复相机
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = camOriginalLocalPos;
            cameraTransform.localRotation = camOriginalLocalRot;
        }

        // ---- 阶段 2: 怪物保持占满屏幕 + 蓝屏频闪5s ----
        // 怪物重新显示并全程保持
        if (jumpScareImage.sprite != null)
            jumpScareImage.color = Color.white;
        else
            jumpScareImage.color = new Color(0, 0, 0, 1);

        elapsed = 0f;
        while (elapsed < flickerDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            // 频闪：正弦波快速切换蓝屏可见性，加入随机毛刺模拟故障
            float flicker = Mathf.Sin(elapsed * flickerSpeed * Mathf.PI * 2f);
            // 随机毛刺：偶尔短暂全黑
            float glitch = Random.value > 0.85f ? 0f : 1f;
            float alpha = flicker > 0 ? glitch : 0f;
            blueScreenImage.color = new Color(blueScreenColor.r, blueScreenColor.g, blueScreenColor.b, alpha);

            yield return null;
        }

        // ---- 阶段 3: 蓝屏常亮3s ----
        blueScreenImage.color = blueScreenColor;
        // 隐藏怪物贴图
        jumpScareImage.color = new Color(1, 1, 1, 0);

        yield return new WaitForSecondsRealtime(blueScreenHoldDuration);

        // ---- 阶段 4: 回到复活点 ----
        OnRespawnRequested?.Invoke();

        if (OnRespawnRequested == null)
        {
            Debug.Log("[DeathSequence] 死亡演出结束，无复活订阅者，返回主菜单。");
            SceneManager.LoadScene(0);
        }
    }
}
