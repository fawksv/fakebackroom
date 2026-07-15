using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MiniMap : MonoBehaviour
{
    [Header("小地图尺寸")]
    [Tooltip("小地图占屏幕宽度的比例")]
    [SerializeField] private float sizeRatio = 0.2f;
    [Tooltip("距屏幕边缘的像素间距")]
    [SerializeField] private float margin = 10f;

    [Header("俯拍摄像机")]
    [Tooltip("俯拍区域中心 (世界坐标)")]
    [SerializeField] private Vector3 mapCenter = new Vector3(0f, 0f, -48f);
    [Tooltip("俯拍摄像机高度")]
    [SerializeField] private float camHeight = 150f;
    [Tooltip("正交大小 (覆盖范围半径, 需覆盖走廊最长边的一半)")]
    [SerializeField] private float orthoSize = 55f;
    [Tooltip("俯拍背景色")]
    [SerializeField] private Color camBgColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    [Header("标记样式")]
    [SerializeField] private float playerDotRadius = 6f;
    [SerializeField] private float monsterDotRadius = 4f;
    [SerializeField] private Color playerColor = Color.green;
    [SerializeField] private Color monsterColor = Color.red;
    [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private float borderWidth = 2f;

    private Camera mainCam;
    private Camera miniCam;
    private RenderTexture miniRT;
    private Texture2D circleTex;
    private Texture2D pixelTex;

    private Transform player;
    private readonly List<MonsterController> monsters = new List<MonsterController>();

    private Rect mapRect;

    void Start()
    {
        mainCam = GetComponent<Camera>();
        pixelTex = MakePixelTex();
        circleTex = MakeCircleTex(64);

        int res = Mathf.RoundToInt(Screen.width * sizeRatio);
        CreateMiniCamera(res);

        FindPlayer();
        RefreshMonsters();
    }

    // ────────────────── 摄像机创建 ──────────────────

    private void CreateMiniCamera(int resolution)
    {
        miniRT = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
        miniRT.antiAliasing = 4;

        var camObj = new GameObject("MiniMapCamera");
        miniCam = camObj.AddComponent<Camera>();
        miniCam.orthographic = true;
        miniCam.orthographicSize = orthoSize;
        miniCam.aspect = 1f;
        miniCam.transform.position = mapCenter + Vector3.up * camHeight;
        miniCam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        miniCam.targetTexture = miniRT;
        miniCam.clearFlags = CameraClearFlags.SolidColor;
        miniCam.backgroundColor = camBgColor;
        miniCam.depth = mainCam.depth - 1;
    }

    // ────────────────── 目标查找 ──────────────────

    private void FindPlayer()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            return;
        }

        // 后备: 通过 PlayerController 组件查找
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null)
            player = pc.transform;
    }

    private void RefreshMonsters()
    {
        monsters.Clear();
        var found = FindObjectsOfType<MonsterController>();
        foreach (var m in found)
        {
            if (m != null)
                monsters.Add(m);
        }
    }

    // ────────────────── 绘制 ──────────────────

    private void Update()
    {
        // 定期刷新怪物列表 (可能被动态生成或销毁)
        if (Time.frameCount % 60 == 0)
            RefreshMonsters();
    }

    private void OnGUI()
    {
        // 重新计算矩形 (适应屏幕尺寸变化)
        float size = Screen.width * sizeRatio;
        mapRect = new Rect(
            Screen.width - size - margin,
            margin,
            size, size
        );

        // 1. 半透明背景
        DrawColorRect(mapRect, bgColor);

        // 2. 俯拍画面
        if (miniRT != null && miniRT.IsCreated())
            GUI.DrawTexture(mapRect, miniRT, ScaleMode.ScaleToFit);

        // 3. 边框
        DrawBorder(mapRect, borderColor, borderWidth);

        // 4. 怪物红点
        for (int i = monsters.Count - 1; i >= 0; i--)
        {
            if (monsters[i] == null)
            {
                monsters.RemoveAt(i);
                continue;
            }
            DrawWorldDot(monsters[i].transform.position, monsterColor, monsterDotRadius);
        }

        // 5. 玩家绿点 (画在最上层)
        if (player != null)
            DrawWorldDot(player.position, playerColor, playerDotRadius);
    }

    // ────────────────── 工具方法 ──────────────────

    private void DrawWorldDot(Vector3 worldPos, Color color, float radius)
    {
        // 将世界坐标转换为俯拍摄像机的视口坐标 (0~1)
        Vector3 vp = miniCam.WorldToViewportPoint(worldPos);
        if (vp.z < 0) return;

        // 视口坐标 → 小地图屏幕坐标
        float x = mapRect.x + vp.x * mapRect.width;
        float y = mapRect.y + (1f - vp.y) * mapRect.height;

        // 裁剪: 只绘制小地图范围内的点
        if (x < mapRect.x - radius || x > mapRect.xMax + radius ||
            y < mapRect.y - radius || y > mapRect.yMax + radius)
            return;

        Color savedColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x - radius, y - radius, radius * 2, radius * 2), circleTex);
        GUI.color = savedColor;
    }

    private void DrawColorRect(Rect rect, Color color)
    {
        Color savedColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, pixelTex);
        GUI.color = savedColor;
    }

    private void DrawBorder(Rect rect, Color color, float width)
    {
        DrawColorRect(new Rect(rect.x, rect.y, rect.width, width), color);
        DrawColorRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
        DrawColorRect(new Rect(rect.x, rect.y, width, rect.height), color);
        DrawColorRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
    }

    private Texture2D MakePixelTex()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return tex;
    }

    private Texture2D MakeCircleTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;
        float aaRange = 1f; // 抗锯齿边缘宽度

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01((radius - dist) / aaRange);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    // ────────────────── 清理 ──────────────────

    private void OnDestroy()
    {
        if (miniRT != null)
        {
            miniRT.Release();
            DestroyImmediate(miniRT);
        }
        if (pixelTex != null) DestroyImmediate(pixelTex);
        if (circleTex != null) DestroyImmediate(circleTex);
        if (miniCam != null) DestroyImmediate(miniCam.gameObject);
    }
}
