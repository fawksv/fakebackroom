using UnityEngine;

/// <summary>
/// 黑雾: 浮空呼吸 + 光照收缩 + 移动拖影
/// 控制子物体上的 BlackFog shader 材质参数
/// </summary>
public class BlackFogEntity : MonoBehaviour
{
    [Header("浮空呼吸")]
    [SerializeField] private float floatAmplitude = 0.12f;
    [SerializeField] private float floatSpeed = 1.0f;
    [SerializeField] private float driftAmplitude = 0.05f;
    [SerializeField] private float driftSpeed = 0.3f;

    [Header("光照收缩")]
    [Tooltip("检测光照的最大距离")]
    [SerializeField] private float lightDetectRange = 12f;
    [Tooltip("光照强度阈值")]
    [SerializeField] private float lightThreshold = 0.4f;
    [Tooltip("收缩过渡速度")]
    [SerializeField] private float shrinkLerpSpeed = 1.5f;

    [Header("材质引用 (自动查找)")]
    [SerializeField] private Material fogMaterial;

    private Renderer[] fogRenderers;
    private float currentShrink = 0f;
    private Vector3[] childBaseLocalPos;

    /// <summary>
    /// 生成程序化 Perlin 噪声纹理，替代外部 smoke 贴图
    /// </summary>
    public static Texture2D GenerateNoiseTexture(int size = 128)
    {
        var tex = new Texture2D(size, size, TextureFormat.R8, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        float scale = 0.08f;
        float[] offsets = { 0f, 1000f, 2000f };
        float[] weights = { 0.5f, 0.3f, 0.2f };

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float value = 0f;
                for (int o = 0; o < offsets.Length; o++)
                {
                    float nx = (x + offsets[o]) * scale * (1f + o);
                    float ny = (y + offsets[o]) * scale * (1f + o);
                    value += weights[o] * Mathf.PerlinNoise(nx, ny);
                }
                value = Mathf.Clamp01(value);
                pixels[y * size + x] = new Color(value, value, value, 1f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    void Start()
    {
        fogRenderers = GetComponentsInChildren<Renderer>();

        // 生成程序化噪声纹理，替换外部贴图
        var noiseTex = GenerateNoiseTexture(128);

        if (fogMaterial == null && fogRenderers.Length > 0)
            fogMaterial = fogRenderers[0].material;

        if (fogMaterial != null)
        {
            fogMaterial.SetTexture("_NoiseTex", noiseTex);
            fogMaterial.SetColor("_Color", Color.black);
        }

        // 记录所有子物体初始位置
        childBaseLocalPos = new Vector3[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            childBaseLocalPos[i] = transform.GetChild(i).localPosition;
    }

    void Update()
    {
        Floating();
        LightDetection();
    }

    void Floating()
    {
        float t = Time.time;
        float yOffset = Mathf.Sin(t * floatSpeed) * floatAmplitude;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            child.localPosition = new Vector3(
                childBaseLocalPos[i].x,
                childBaseLocalPos[i].y + yOffset,
                childBaseLocalPos[i].z
            );
        }

        if (fogMaterial != null)
        {
            float breath = (Mathf.Sin(t * 0.8f) + 1f) * 0.5f * 0.04f + 0.01f;
            fogMaterial.SetFloat("_Breath", breath);
        }
    }

    void LightDetection()
    {
        float totalLight = 0f;
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (!light.enabled || light.type == LightType.Directional) continue;
            float dist = Vector3.Distance(transform.position, light.transform.position);
            if (dist > lightDetectRange) continue;
            float attenuation = 1f - Mathf.Clamp01(dist / lightDetectRange);
            totalLight += light.intensity * attenuation;
        }

        totalLight = Mathf.Clamp01(totalLight / 4f);
        float targetShrink = Mathf.Clamp01((totalLight - lightThreshold) / (1f - lightThreshold));
        currentShrink = Mathf.Lerp(currentShrink, targetShrink, Time.deltaTime * shrinkLerpSpeed);

        if (fogMaterial != null)
            fogMaterial.SetFloat("_Shrink", currentShrink);
    }
}
