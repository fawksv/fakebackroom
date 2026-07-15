using UnityEngine;

[DisallowMultipleComponent]
public sealed class BatteryHUD : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField, Min(80f)] private float barWidth = 280f;
    [SerializeField, Min(4f)] private float barHeight = 16f;
    [SerializeField, Min(0f)] private float rightMargin = 34f;
    [SerializeField, Min(0f)] private float topMargin = 34f;

    [Header("Appearance")]
    [SerializeField] private Color fullColor = new Color(0.2f, 0.7f, 0.3f, 1f);
    [SerializeField] private Color midColor = new Color(0.85f, 0.7f, 0.2f, 1f);
    [SerializeField] private Color lowColor = new Color(0.75f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color deadColor = new Color(0.4f, 0.08f, 0.05f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.02f, 0.025f, 0.025f, 0.72f);
    [SerializeField] private Color labelColor = new Color(0.76f, 0.74f, 0.66f, 0.82f);
    [SerializeField] private Color warningColor = new Color(1f, 0.3f, 0.15f, 1f);
    [SerializeField] private Font font;
    [SerializeField, Min(8)] private int fontSize = 16;
    [SerializeField, Min(8)] private int warningFontSize = 26;

    [Header("Behaviour")]
    [SerializeField, Min(0.1f)] private float fillSmoothSpeed = 8f;
    [SerializeField, Min(0.1f)] private float warningBlinkSpeed = 2f;

    private GUIStyle labelStyle;
    private GUIStyle warningStyle;
    private float displayedFill = 1f;
    private bool warningVisible = true;
    private Inventory inventory;

    private void Awake()
    {
        inventory = GetComponent<Inventory>();
    }

    private void Update()
    {
        if (FlashlightController.Instance == null)
            return;

        float deltaTime = Time.unscaledDeltaTime;

        displayedFill = Mathf.MoveTowards(
            displayedFill,
            FlashlightController.Instance.BatteryNormalized,
            fillSmoothSpeed * deltaTime);

        if (FlashlightController.Instance.IsLowBattery)
            warningVisible = Mathf.Repeat(Time.unscaledTime * warningBlinkSpeed, 1f) > 0.4f;
        else
            warningVisible = false;
    }

    private void OnGUI()
    {
        if (FlashlightController.Instance == null
            || Time.timeScale <= 0f
            || Cursor.lockState != CursorLockMode.Locked)
            return;

        EnsureStyles();

        float x = Screen.width - rightMargin - barWidth;
        float y = topMargin;

        // Label
        Rect labelRect = new Rect(x, y, barWidth, 26f);
        GUI.color = labelColor;
        GUI.Label(labelRect, "BATTERY", labelStyle);
        GUI.color = Color.white;

        // Bar background
        y += 28f;
        Rect outerRect = new Rect(x, y, barWidth, barHeight);
        Rect innerRect = new Rect(x + 2f, y + 2f, (barWidth - 4f) * displayedFill, barHeight - 4f);

        GUI.color = backgroundColor;
        GUI.DrawTexture(outerRect, Texture2D.whiteTexture);

        Color fillColor = GetFillColor(displayedFill);
        GUI.color = fillColor;
        GUI.DrawTexture(innerRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Percentage + battery count
        float pct = displayedFill * 100f;
        int batCount = inventory != null ? inventory.CountItem(Inventory.ItemType.Battery) : 0;
        Rect pctRect = new Rect(x, y + barHeight + 6f, barWidth, 26f);
        GUI.color = fillColor;
        GUI.Label(pctRect, $"{Mathf.RoundToInt(pct)}%  [×{batCount}]", labelStyle);
        GUI.color = Color.white;

        // Low battery warning
        if (warningVisible)
        {
            string msg = batCount > 0
                ? "电量不足！按 R 更换电池"
                : "电量不足！请拾取电池";
            Rect warnRect = new Rect(0f, y + barHeight + 38f, Screen.width, 42f);
            GUI.color = warningColor;
            GUI.Label(warnRect, msg, warningStyle);
            GUI.color = Color.white;
        }
    }

    private Color GetFillColor(float fill)
    {
        if (fill <= 0.001f) return deadColor;
        if (fill <= 0.2f) return Color.Lerp(deadColor, lowColor, fill / 0.2f);
        if (fill <= 0.5f) return Color.Lerp(lowColor, midColor, (fill - 0.2f) / 0.3f);
        return Color.Lerp(midColor, fullColor, (fill - 0.5f) / 0.5f);
    }

    private void EnsureStyles()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                font = font,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
        }

        if (warningStyle == null)
        {
            warningStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                font = font,
                fontSize = warningFontSize,
                fontStyle = FontStyle.Bold
            };
            warningStyle.normal.textColor = Color.white;
        }
    }
}
