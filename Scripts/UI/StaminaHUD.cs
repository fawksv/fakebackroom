using UnityEngine;

[DisallowMultipleComponent]
public sealed class StaminaHUD : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField, Min(80f)] private float barWidth = 210f;
    [SerializeField, Min(4f)] private float barHeight = 9f;
    [SerializeField, Min(0f)] private float leftMargin = 34f;
    [SerializeField, Min(0f)] private float bottomMargin = 80f;

    [Header("Appearance")]
    [SerializeField] private Color fillColor = new Color(0.78f, 0.68f, 0.38f, 1f);
    [SerializeField] private Color exhaustedColor = new Color(0.55f, 0.16f, 0.12f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.02f, 0.025f, 0.025f, 0.72f);
    [SerializeField] private Color labelColor = new Color(0.76f, 0.74f, 0.66f, 0.82f);
    [SerializeField] private Font font;
    [SerializeField, Min(8)] private int fontSize = 11;

    [Header("Behaviour")]
    [SerializeField, Min(0f)] private float fullHideDelay = 0.8f;
    [SerializeField, Min(0.1f)] private float fadeSpeed = 4f;
    [SerializeField, Min(0.1f)] private float fillSmoothSpeed = 8f;

    private PlayerController player;
    private GUIStyle labelStyle;
    private float displayedFill = 1f;
    private float alpha;
    private float fullTimer;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (player != null)
            displayedFill = player.StaminaNormalized;
    }

    private void Update()
    {
        if (player == null)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        displayedFill = Mathf.MoveTowards(
            displayedFill,
            player.StaminaNormalized,
            fillSmoothSpeed * deltaTime);

        bool isFull = player.StaminaNormalized >= 0.999f;
        fullTimer = isFull ? fullTimer + deltaTime : 0f;
        float targetAlpha = !isFull || fullTimer < fullHideDelay ? 1f : 0f;
        alpha = Mathf.MoveTowards(alpha, targetAlpha, fadeSpeed * deltaTime);
    }

    private void OnGUI()
    {
        if (player == null
            || alpha <= 0.001f
            || Time.timeScale <= 0f
            || Cursor.lockState != CursorLockMode.Locked)
            return;

        EnsureStyle();

        float x = leftMargin;
        float y = Screen.height - bottomMargin - barHeight;
        Rect labelRect = new Rect(x, y - 20f, barWidth, 18f);
        Rect outerRect = new Rect(x, y, barWidth, barHeight);
        Rect innerRect = new Rect(x + 2f, y + 2f, (barWidth - 4f) * displayedFill, barHeight - 4f);

        Color previousColor = GUI.color;

        GUI.color = WithAlpha(labelColor, alpha);
        GUI.Label(labelRect, player.IsExhausted ? "EXHAUSTED" : "STAMINA", labelStyle);

        GUI.color = WithAlpha(backgroundColor, alpha);
        GUI.DrawTexture(outerRect, Texture2D.whiteTexture);

        GUI.color = WithAlpha(player.IsExhausted ? exhaustedColor : fillColor, alpha);
        GUI.DrawTexture(innerRect, Texture2D.whiteTexture);

        GUI.color = previousColor;
    }

    private void EnsureStyle()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.LowerLeft,
            font = font,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };
        labelStyle.normal.textColor = Color.white;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }
}
