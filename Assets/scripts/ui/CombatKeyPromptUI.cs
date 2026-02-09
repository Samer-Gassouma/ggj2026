using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds and manages the combat key-prompt overlay for the CH1 tutorial.
/// Creates its own Canvas ▸ Panel ▸ key buttons entirely from code.
///
/// Three modes controlled by the CH1TutorialManager:
///   ShowPickupOnly()  →  [ F ]  Pick up
///   ShowCombatKeys()  →  [ LMB ] Melee   [ G ] Throw
///   Hide()            →  fade out
/// </summary>
public class CombatKeyPromptUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private float keyWidth   = 100f;
    [SerializeField] private float keyHeight  = 70f;
    [SerializeField] private float spacing    = 16f;

    [Header("Fade")]
    [SerializeField] private float fadeSpeed = 2.5f;

    // Auto-created
    private CanvasGroup canvasGroup;
    private GameObject  pickupGroup;   // [ F ] Pick up
    private GameObject  combatGroup;   // [ LMB ] Melee  ·  [ G ] Throw

    private void Awake()
    {
        BuildUI();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Build everything from code — zero Inspector wiring
    // ──────────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasGO = new GameObject("CombatPromptCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas    = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 105; // above narrative, below fade overlays

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Wrapper (centre-bottom area) ──
        GameObject wrapperGO = new GameObject("PromptWrapper", typeof(RectTransform));
        wrapperGO.transform.SetParent(canvasGO.transform, false);

        RectTransform wrapperRT = wrapperGO.GetComponent<RectTransform>();
        wrapperRT.anchorMin        = new Vector2(0.5f, 0f);
        wrapperRT.anchorMax        = new Vector2(0.5f, 0f);
        wrapperRT.pivot            = new Vector2(0.5f, 0f);
        wrapperRT.anchoredPosition = new Vector2(0f, 150f); // just above the narrative bar
        wrapperRT.sizeDelta        = new Vector2(700f, 200f);

        canvasGroup       = wrapperGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // ── Pickup group:  [ F ]  Pick up ──
        pickupGroup = BuildKeyRow(wrapperGO.transform, 0f,
            new KeyDef("F", KeyCode.F, 1f),
            "Pick up sword"
        );
        pickupGroup.SetActive(false);

        // ── Combat group:  [ LMB ] Melee  ·  [ G ] Throw ──
        combatGroup = BuildCombatRow(wrapperGO.transform, 0f);
        combatGroup.SetActive(false);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Build helpers
    // ──────────────────────────────────────────────────────────────────
    private struct KeyDef
    {
        public string label;
        public KeyCode code;
        public float widthMul;
        public KeyDef(string l, KeyCode c, float w) { label = l; code = c; widthMul = w; }
    }

    /// <summary>Creates a row with ONE key + description label.</summary>
    private GameObject BuildKeyRow(Transform parent, float yOffset, KeyDef key, string description)
    {
        GameObject rowGO = new GameObject("Row", typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);

        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin        = new Vector2(0.5f, 0.5f);
        rowRT.anchorMax        = new Vector2(0.5f, 0.5f);
        rowRT.pivot            = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(0f, yOffset);
        rowRT.sizeDelta        = new Vector2(400f, keyHeight + 10f);

        // Key button
        float kw = keyWidth * key.widthMul;
        GameObject keyGO = CreateKeyButton(rowGO.transform, key.label, kw, keyHeight);
        RectTransform keyRT = keyGO.GetComponent<RectTransform>();
        keyRT.anchoredPosition = new Vector2(-kw / 2f - spacing, 0f);

        // Add KeyClickEffect for visual feedback
        KeyClickEffect fx = keyGO.AddComponent<KeyClickEffect>();
        fx.Init(key.code, key.label,
                keyGO.GetComponent<Image>(),
                keyGO.GetComponentInChildren<TextMeshProUGUI>());

        // Description label
        GameObject descGO = new GameObject("Desc", typeof(RectTransform));
        descGO.transform.SetParent(rowGO.transform, false);

        TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text      = description;
        descTMP.fontSize  = 26;
        descTMP.color     = new Color(0.85f, 0.85f, 0.85f, 1f);
        descTMP.alignment = TextAlignmentOptions.Left;

        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin        = new Vector2(0.5f, 0.5f);
        descRT.anchorMax        = new Vector2(0.5f, 0.5f);
        descRT.pivot            = new Vector2(0f, 0.5f);
        descRT.anchoredPosition = new Vector2(kw / 2f + spacing, 0f);
        descRT.sizeDelta        = new Vector2(260f, keyHeight);

        return rowGO;
    }

    /// <summary>Creates the two-row combat prompt:  LMB → Melee  /  G → Throw</summary>
    private GameObject BuildCombatRow(Transform parent, float yOffset)
    {
        GameObject groupGO = new GameObject("CombatGroup", typeof(RectTransform));
        groupGO.transform.SetParent(parent, false);

        RectTransform groupRT = groupGO.GetComponent<RectTransform>();
        groupRT.anchorMin        = new Vector2(0.5f, 0.5f);
        groupRT.anchorMax        = new Vector2(0.5f, 0.5f);
        groupRT.pivot            = new Vector2(0.5f, 0.5f);
        groupRT.anchoredPosition = new Vector2(0f, yOffset);
        groupRT.sizeDelta        = new Vector2(500f, (keyHeight + spacing) * 2f);

        float rowY = (keyHeight + spacing) * 0.5f;

        // Row 1:  [ LMB ]  Melee attack
        BuildInlineKeyRow(groupGO.transform, rowY, "LMB", "Melee attack (swing sword)");

        // Row 2:  [ G ]    Throw sword
        BuildInlineKeyRow(groupGO.transform, -rowY, "G", "Hold to aim, release to throw");

        return groupGO;
    }

    /// <summary>One inline row:  [ KEY ]  description</summary>
    private void BuildInlineKeyRow(Transform parent, float y, string keyLabel, string desc)
    {
        GameObject rowGO = new GameObject("Row_" + keyLabel, typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);

        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin        = new Vector2(0.5f, 0.5f);
        rowRT.anchorMax        = new Vector2(0.5f, 0.5f);
        rowRT.pivot            = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(0f, y);
        rowRT.sizeDelta        = new Vector2(500f, keyHeight);

        float kw = keyLabel == "LMB" ? keyWidth * 1.3f : keyWidth;

        // Key box
        GameObject keyGO = CreateKeyButton(rowGO.transform, keyLabel, kw, keyHeight);
        RectTransform keyRT = keyGO.GetComponent<RectTransform>();
        keyRT.anchoredPosition = new Vector2(-130f, 0f);

        // For LMB we can't easily detect a KeyCode for visual feedback, but
        // we still add KeyClickEffect so it flashes on mouse-down
        KeyCode code = keyLabel == "LMB" ? KeyCode.Mouse0 : KeyCode.G;

        KeyClickEffect fx = keyGO.AddComponent<KeyClickEffect>();
        fx.Init(code, keyLabel,
                keyGO.GetComponent<Image>(),
                keyGO.GetComponentInChildren<TextMeshProUGUI>());

        // Description
        GameObject descGO = new GameObject("Desc", typeof(RectTransform));
        descGO.transform.SetParent(rowGO.transform, false);

        TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text      = desc;
        descTMP.fontSize  = 24;
        descTMP.color     = new Color(0.85f, 0.85f, 0.85f, 1f);
        descTMP.alignment = TextAlignmentOptions.Left;

        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin        = new Vector2(0.5f, 0.5f);
        descRT.anchorMax        = new Vector2(0.5f, 0.5f);
        descRT.pivot            = new Vector2(0f, 0.5f);
        descRT.anchoredPosition = new Vector2(-130f + kw / 2f + spacing, 0f);
        descRT.sizeDelta        = new Vector2(300f, keyHeight);
    }

    /// <summary>Creates a single dark key button with centred white label.</summary>
    private GameObject CreateKeyButton(Transform parent, string label, float w, float h)
    {
        GameObject go = new GameObject("Key_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image bg  = go.AddComponent<Image>();
        bg.color  = new Color(0.13f, 0.13f, 0.13f, 0.92f);
        bg.type   = Image.Type.Sliced;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        Outline outline       = go.AddComponent<Outline>();
        outline.effectColor    = new Color(0.5f, 0.5f, 0.5f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Label
        GameObject textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = label.Length > 2 ? 22 : 30;
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

    // ──────────────────────────────────────────────────────────────────
    //  Public API  (called by CH1TutorialManager)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Show only the  [ F ] Pick up  row.</summary>
    public void ShowPickupOnly()
    {
        pickupGroup.SetActive(true);
        combatGroup.SetActive(false);
        StartCoroutine(Fade(1f));
    }

    /// <summary>Show  [ LMB ] Melee  +  [ G ] Throw  rows.</summary>
    public void ShowCombatKeys()
    {
        pickupGroup.SetActive(false);
        combatGroup.SetActive(true);
        StartCoroutine(Fade(1f));
    }

    /// <summary>Fade out and disable all groups.</summary>
    public void Hide()
    {
        StartCoroutine(FadeAndDisable());
    }

    private IEnumerator Fade(float target)
    {
        while (!Mathf.Approximately(canvasGroup.alpha, target))
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    private IEnumerator FadeAndDisable()
    {
        yield return StartCoroutine(Fade(0f));
        pickupGroup.SetActive(false);
        combatGroup.SetActive(false);
    }
}
