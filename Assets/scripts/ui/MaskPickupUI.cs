using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles all mask-pickup UI:
///   • Center-screen "Press [F] to collect" prompt
///   • Bottom-left toast notification showing icon + mask name
/// Everything is created via code — no prefabs or manual setup required.
/// Just place this script on any GameObject (or it auto-creates itself).
/// </summary>
public class MaskPickupUI : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────
    public static MaskPickupUI Instance { get; private set; }

    // ─── Settings ────────────────────────────────────────────
    [Header("Prompt")]
    public Color promptTextColor = Color.white;
    public int promptFontSize = 32;
    public Color promptKeyColor = new Color(1f, 0.85f, 0.2f, 1f); // gold

    [Header("Toast")]
    public float toastDuration = 3f;
    public float toastFadeTime = 0.5f;
    public int toastFontSize = 24;
    public int toastIconSize = 48;
    public Color toastBgColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    public Color toastTextColor = Color.white;

    // ─── Runtime refs ────────────────────────────────────────
    private Canvas canvas;
    private GameObject promptGO;
    private Text promptText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureCanvas();
        BuildPromptUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Auto-create if needed ───────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("MaskPickupUI");
        go.AddComponent<MaskPickupUI>();
        DontDestroyOnLoad(go);
    }

    // ─── Canvas ──────────────────────────────────────────────
    private void EnsureCanvas()
    {
        canvas = GetComponentInChildren<Canvas>();
        if (canvas != null) return;

        var cGO = new GameObject("MaskPickup_Canvas");
        cGO.transform.SetParent(transform);
        canvas = cGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120; // on top of most UI

        var scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        cGO.AddComponent<GraphicRaycaster>();
    }

    // ═════════════════════════════════════════════════════════
    //  PROMPT  (center of screen)
    // ═════════════════════════════════════════════════════════

    private void BuildPromptUI()
    {
        // Container
        promptGO = new GameObject("PickupPrompt");
        promptGO.transform.SetParent(canvas.transform, false);

        var rt = promptGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.35f);
        rt.anchorMax = new Vector2(0.5f, 0.35f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(500, 60);

        // Background panel
        var bg = promptGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        // Rounded feel — not a real rounded rect, but a slight visual
        bg.type = Image.Type.Sliced;

        // Text child
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(promptGO.transform, false);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(16, 4);
        textRT.offsetMax = new Vector2(-16, -4);

        promptText = textGO.AddComponent<Text>();
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (promptText.font == null)
            promptText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        promptText.fontSize = promptFontSize;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = promptTextColor;
        promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
        promptText.verticalOverflow = VerticalWrapMode.Overflow;
        promptText.supportRichText = true;

        // Add subtle shadow
        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(1, -1);

        promptGO.SetActive(false);
    }

    public void ShowPrompt(KeyCode key, string maskName)
    {
        if (promptGO == null) return;

        string keyStr = KeyCodeToNiceString(key);
        // Rich-text: key in gold/yellow, rest white
        string hex = ColorUtility.ToHtmlStringRGB(promptKeyColor);
        promptText.text = $"Press <color=#{hex}><b>[ {keyStr} ]</b></color> to collect";

        promptGO.SetActive(true);
    }

    public void HidePrompt()
    {
        if (promptGO != null)
            promptGO.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════
    //  TOAST  (bottom-left corner)
    // ═════════════════════════════════════════════════════════

    public void ShowToast(Sprite icon, string message, string title = "Mask Collected!")
    {
        StartCoroutine(ToastRoutine(icon, message, title));
    }

    private IEnumerator ToastRoutine(Sprite icon, string message, string title)
    {
        // ── Build toast GO ───────────────────────────────────
        var toastGO = new GameObject("MaskToast");
        toastGO.transform.SetParent(canvas.transform, false);

        var toastRT = toastGO.AddComponent<RectTransform>();
        toastRT.anchorMin = new Vector2(0f, 0f);
        toastRT.anchorMax = new Vector2(0f, 0f);
        toastRT.pivot = new Vector2(0f, 0f);
        toastRT.anchoredPosition = new Vector2(24, 24);
        toastRT.sizeDelta = new Vector2(320, 70);

        // Background
        var bgImg = toastGO.AddComponent<Image>();
        bgImg.color = toastBgColor;

        // Horizontal layout
        var hlg = toastGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.padding = new RectOffset(12, 12, 8, 8);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // ── Icon ─────────────────────────────────────────────
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(toastGO.transform, false);

        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(toastIconSize, toastIconSize);

        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;

        // ── Text column ──────────────────────────────────────
        var colGO = new GameObject("TextCol");
        colGO.transform.SetParent(toastGO.transform, false);

        var colRT = colGO.AddComponent<RectTransform>();
        colRT.sizeDelta = new Vector2(230, 54);

        var vlg = colGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2f;
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Title (small)
        var labelGO = CreateToastText(colGO.transform, title, toastFontSize - 6, new Color(0.75f, 0.75f, 0.75f));

        // Message (main)
        var nameGO = CreateToastText(colGO.transform, message, toastFontSize, toastTextColor, true);

        // ── Canvas group for fading ──────────────────────────
        var cg = toastGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // ── Slide in + fade in ───────────────────────────────
        float slideFrom = -toastRT.sizeDelta.x;
        float slideTo = 24f;
        float elapsed = 0f;

        while (elapsed < toastFadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / toastFadeTime);
            cg.alpha = t;
            toastRT.anchoredPosition = new Vector2(Mathf.Lerp(slideFrom, slideTo, t), 24);
            yield return null;
        }
        cg.alpha = 1f;
        toastRT.anchoredPosition = new Vector2(slideTo, 24);

        // ── Hold ─────────────────────────────────────────────
        yield return new WaitForSecondsRealtime(toastDuration);

        // ── Fade out ─────────────────────────────────────────
        elapsed = 0f;
        while (elapsed < toastFadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / toastFadeTime);
            cg.alpha = 1f - t;
            yield return null;
        }

        Destroy(toastGO);
    }

    private GameObject CreateToastText(Transform parent, string content, int fontSize, Color color, bool bold = false)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();

        var txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null)
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.supportRichText = true;

        if (bold)
            txt.text = "<b>" + content + "</b>";
        else
            txt.text = content;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.6f);
        shadow.effectDistance = new Vector2(1, -1);

        return go;
    }

    // ─── Helpers ─────────────────────────────────────────────

    private static string KeyCodeToNiceString(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.F: return "F";
            case KeyCode.E: return "E";
            case KeyCode.G: return "G";
            case KeyCode.Space: return "SPACE";
            case KeyCode.Return: return "ENTER";
            case KeyCode.LeftShift: return "L-SHIFT";
            default: return key.ToString().ToUpper();
        }
    }
}
