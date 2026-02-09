using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds and manages the WASD + Space key-prompt overlay displayed during
/// the CH tutorial scene.  Creates its own Canvas ▸ Panel ▸ key buttons
/// entirely from code — no Inspector wiring needed.
///
/// Layout mirrors a real keyboard:
///
///        [ W ]
///   [ A ][ S ][ D ]
///       [ SPACE ]
///
/// Each key lights up when pressed. Once every key has been pressed at least
/// once the prompt can be dismissed.
/// </summary>
public class KeyPromptUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private float keySize = 80f;
    [SerializeField] private float spacing = 10f;
    [SerializeField] private float spaceBarWidth = 260f;
    [SerializeField] private float cornerRadius = 8f;

    [Header("Fade")]
    [SerializeField] private float fadeSpeed = 2f;

    // Auto-created at runtime
    private CanvasGroup canvasGroup;
    private RectTransform container;
    private KeyClickEffect[] keyEffects;

    /// <summary>True when the player has pressed every prompted key at least once.</summary>
    public bool AllKeysPressed => keyEffects != null && keyEffects.All(k => k.HasBeenPressed);

    private void Awake()
    {
        BuildUI();
    }

    // ─────────────────── Build everything from code ───────────────────────
    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasGO = new GameObject("KeyPromptCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // on top of narrative

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Wrapper panel (centred on screen) ──
        GameObject wrapperGO = new GameObject("KeyPromptPanel", typeof(RectTransform));
        wrapperGO.transform.SetParent(canvasGO.transform, false);

        RectTransform wrapperRT = wrapperGO.GetComponent<RectTransform>();
        wrapperRT.anchorMin = new Vector2(0.5f, 0.5f);
        wrapperRT.anchorMax = new Vector2(0.5f, 0.5f);
        wrapperRT.pivot     = new Vector2(0.5f, 0.5f);
        wrapperRT.anchoredPosition = new Vector2(200f, -50f); // shifted right, slightly below centre

        canvasGroup = wrapperGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // ── Container for the key icons ──
        GameObject containerGO = new GameObject("Container", typeof(RectTransform));
        containerGO.transform.SetParent(wrapperGO.transform, false);
        container = containerGO.GetComponent<RectTransform>();

        // ── Build each key ──
        var keys = new (KeyCode code, string label, int row, float col, float widthMul)[]
        {
            (KeyCode.W,     "W",     0, 1f,   1f),
            (KeyCode.A,     "A",     1, 0f,   1f),
            (KeyCode.S,     "S",     1, 1f,   1f),
            (KeyCode.D,     "D",     1, 2f,   1f),
            (KeyCode.Space, "SPACE", 2, 0.25f, spaceBarWidth / keySize),
        };

        keyEffects = new KeyClickEffect[keys.Length];
        float unit = keySize + spacing;

        for (int i = 0; i < keys.Length; i++)
        {
            var (code, label, row, col, widthMul) = keys[i];

            float w = widthMul * keySize + (widthMul > 1f ? (widthMul - 1f) * spacing : 0f);

            GameObject keyGO = CreateKeyGO(container, label, w);

            RectTransform rt = keyGO.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(col * unit, -row * unit);

            Image bg             = keyGO.GetComponent<Image>();
            TextMeshProUGUI tmp  = keyGO.GetComponentInChildren<TextMeshProUGUI>();

            KeyClickEffect fx = keyGO.GetComponent<KeyClickEffect>();
            if (fx == null) fx = keyGO.AddComponent<KeyClickEffect>();
            fx.Init(code, label, bg, tmp);

            keyEffects[i] = fx;
        }

        // Size the container so anchoring works
        container.sizeDelta = new Vector2(3f * unit, 3f * unit);
        wrapperRT.sizeDelta = container.sizeDelta;
    }

    /// <summary>Creates a single key button: dark rounded rect + white label.</summary>
    private GameObject CreateKeyGO(RectTransform parent, string label, float width)
    {
        GameObject go = new GameObject("Key_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        // ── Background ──
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        bg.type  = Image.Type.Sliced;

        RectTransform goRT = go.GetComponent<RectTransform>();
        goRT.sizeDelta = new Vector2(width, keySize);

        // ── Outline (slightly lighter border) ──
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor    = new Color(0.4f, 0.4f, 0.4f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        // ── Label ──
        GameObject textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = label == "SPACE" ? 22 : 32;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        return go;
    }

    // ─────────────────────────── Show / Hide ──────────────────────────────
    public void Show()  => StartCoroutine(Fade(1f));
    public void Hide()  => StartCoroutine(Fade(0f));

    private IEnumerator Fade(float target)
    {
        while (!Mathf.Approximately(canvasGroup.alpha, target))
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        canvasGroup.alpha = target;
    }
}
