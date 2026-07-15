using UnityEngine;

[DisallowMultipleComponent]
public sealed class Inventory : MonoBehaviour
{
    public enum ItemType { Battery }

    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.B;

    [Header("Layout")]
    [SerializeField] private int slotCount = 6;
    [SerializeField, Min(40f)] private float slotSize = 72f;
    [SerializeField, Min(2f)] private float slotSpacing = 8f;
    [SerializeField, Min(0f)] private float panelPadding = 20f;
    [SerializeField] private Color panelColor = new Color(0.03f, 0.03f, 0.04f, 0.88f);
    [SerializeField] private Color slotColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
    [SerializeField] private Color slotBorderColor = new Color(0.4f, 0.4f, 0.42f, 0.6f);
    [SerializeField] private Color batteryColor = new Color(0.15f, 0.6f, 0.15f, 1f);
    [SerializeField] private Color titleColor = new Color(0.76f, 0.74f, 0.66f, 0.9f);
    [SerializeField] private Font font;
    [SerializeField, Min(8)] private int titleFontSize = 20;
    [SerializeField, Min(8)] private int labelFontSize = 12;

    private ItemType?[] slots;
    private bool isOpen;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;

    public bool IsOpen => isOpen;
    public int SlotCount => slotCount;

    void Awake()
    {
        slots = new ItemType?[slotCount];
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            isOpen = !isOpen;
    }

    public bool AddItem(ItemType type)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = type;
                return true;
            }
        }
        return false;
    }

    public int CountItem(ItemType type)
    {
        int count = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == type)
                count++;
        }
        return count;
    }

    public bool HasItem(ItemType type)
    {
        return CountItem(type) > 0;
    }

    public bool RemoveOne(ItemType type)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == type)
            {
                slots[i] = null;
                return true;
            }
        }
        return false;
    }

    private void OnGUI()
    {
        if (!isOpen)
            return;

        EnsureStyles();

        float gridW = slotCount * slotSize + (slotCount - 1) * slotSpacing;
        float gridH = slotSize;
        float panelW = gridW + panelPadding * 2f;
        float panelH = gridH + panelPadding * 2f + 36f;

        float panelX = (Screen.width - panelW) * 0.5f;
        float panelY = (Screen.height - panelH) * 0.5f;

        // Panel background
        Color prev = GUI.color;
        GUI.color = panelColor;
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
        GUI.color = prev;

        // Title
        Rect titleRect = new Rect(panelX, panelY + 6f, panelW, 28f);
        GUI.color = titleColor;
        GUI.Label(titleRect, "背包  [B 关闭]", titleStyle);
        GUI.color = prev;

        // Slots
        float startX = panelX + panelPadding;
        float startY = panelY + 36f + panelPadding * 0.5f;

        for (int i = 0; i < slotCount; i++)
        {
            float sx = startX + i * (slotSize + slotSpacing);
            Rect slotRect = new Rect(sx, startY, slotSize, slotSize);

            // Slot background
            GUI.color = slotColor;
            GUI.DrawTexture(slotRect, Texture2D.whiteTexture);

            // Slot border
            GUI.color = slotBorderColor;
            DrawBorder(slotRect, 2f);
            GUI.color = prev;

            // Item icon
            if (slots[i] != null)
            {
                if (slots[i] == ItemType.Battery)
                    DrawBatteryIcon(slotRect);
            }
        }

        GUI.color = prev;
    }

    private void DrawBatteryIcon(Rect slotRect)
    {
        float cx = slotRect.x + slotRect.width * 0.5f;
        float cy = slotRect.y + slotRect.height * 0.5f;
        float bodyW = slotRect.width * 0.4f;
        float bodyH = slotRect.height * 0.55f;
        float capW = bodyW * 0.5f;
        float capH = bodyH * 0.12f;

        // Battery body
        GUI.color = batteryColor;
        GUI.DrawTexture(new Rect(cx - bodyW * 0.5f, cy - bodyH * 0.5f, bodyW, bodyH), Texture2D.whiteTexture);

        // Cap
        GUI.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        GUI.DrawTexture(new Rect(cx - capW * 0.5f, cy - bodyH * 0.5f - capH, capW, capH), Texture2D.whiteTexture);

        // Label
        GUI.color = Color.white;
        Rect labelRect = new Rect(slotRect.x, slotRect.y + slotRect.height - 16f, slotRect.width, 14f);
        GUI.Label(labelRect, "电池", labelStyle);
    }

    private void DrawBorder(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
    }

    private void EnsureStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                font = font,
                fontSize = titleFontSize,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.white;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                font = font,
                fontSize = labelFontSize
            };
            labelStyle.normal.textColor = Color.white;
        }
    }
}
