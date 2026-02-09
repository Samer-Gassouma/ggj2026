using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Full-screen death screen with blood-edge transition, "YOU DIED" text,
/// and a "Try Again" button. 100% code-generated — no prefabs needed.
/// Auto-attaches to the Player via PlayerHealth.Die().
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    public static DeathScreenUI Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] private float bloodFadeInDuration = 0.6f;    // Blood edges creep in
    [SerializeField] private float blackFadeDuration = 0.5f;      // Centre goes black
    [SerializeField] private float textFadeDelay = 0.2f;          // After black, wait then show text
    [SerializeField] private float textFadeDuration = 0.5f;
    [SerializeField] private float buttonFadeDelay = 0.2f;        // After text, wait then show button

    // Runtime references
    private Canvas canvas;
    private RawImage bloodEdgesImage;
    private Image blackOverlay;
    private TextMeshProUGUI diedText;
    private Button tryAgainButton;
    private TextMeshProUGUI buttonLabel;
    private CanvasGroup rootGroup;
    private CanvasGroup buttonGroup;

    private bool isDead = false;

    private void Awake()
    {
        Instance = this;
        BuildUI();
        // Start fully hidden
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        canvas.gameObject.SetActive(false);
    }

    // ════════════════════════════════════════
    //  PUBLIC API
    // ════════════════════════════════════════

    /// <summary>Call this when the player dies.</summary>
    public void ShowDeathScreen()
    {
        if (isDead) return;
        isDead = true;

        canvas.gameObject.SetActive(true);

        // Unlock + show cursor so player can click Try Again
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Restore time immediately so UI input works properly
        Time.timeScale = 1f;

        // Enable root group for interaction
        rootGroup.alpha = 1f;
        rootGroup.interactable = true;
        rootGroup.blocksRaycasts = true;

        StartCoroutine(DeathTransitionRoutine());
    }

    // ════════════════════════════════════════
    //  TRANSITION COROUTINE
    // ════════════════════════════════════════

    private IEnumerator DeathTransitionRoutine()
    {
        // ── Phase 1: Blood edges creep in from the sides ──
        float t = 0f;
        while (t < bloodFadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / bloodFadeInDuration);
            // Ease-in curve for dramatic feel
            float eased = p * p;

            Color bc = bloodEdgesImage.color;
            bc.a = eased;
            bloodEdgesImage.color = bc;

            yield return null;
        }

        // ── Phase 2: Centre fades to black ──
        t = 0f;
        while (t < blackFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / blackFadeDuration);
            float eased = Mathf.SmoothStep(0f, 1f, p);

            Color oc = blackOverlay.color;
            oc.a = eased * 0.92f; // Not fully opaque — blood edges still peek through
            blackOverlay.color = oc;

            yield return null;
        }

        // ── Phase 3: "YOU DIED" fades in ──
        yield return new WaitForSecondsRealtime(textFadeDelay);

        t = 0f;
        Color textColor = diedText.color;
        while (t < textFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / textFadeDuration);

            textColor.a = p;
            diedText.color = textColor;

            // Slight scale punch
            float scale = Mathf.Lerp(1.3f, 1f, Mathf.SmoothStep(0f, 1f, p));
            diedText.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        // ── Phase 4: "Try Again" button slides in ──
        yield return new WaitForSecondsRealtime(buttonFadeDelay);

        t = 0f;
        // Animate the border wrapper (which contains the button)
        RectTransform btnRT = buttonGroup.GetComponent<RectTransform>();
        Vector2 btnTarget = btnRT.anchoredPosition;
        Vector2 btnStart = btnTarget + Vector2.down * 60f; // Slide up from below

        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.3f);
            float eased = Mathf.SmoothStep(0f, 1f, p);

            buttonGroup.alpha = eased;
            btnRT.anchoredPosition = Vector2.Lerp(btnStart, btnTarget, eased);

            yield return null;
        }

        buttonGroup.interactable = true;
        buttonGroup.blocksRaycasts = true;
    }

    // ════════════════════════════════════════
    //  TRY AGAIN
    // ════════════════════════════════════════

    private void OnTryAgain()
    {
        Debug.Log("Try Again clicked — reloading scene");

        // Restore everything before reload
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Prevent double-click
        if (tryAgainButton != null) tryAgainButton.interactable = false;

        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ════════════════════════════════════════
    //  BUILD ALL UI FROM CODE
    // ════════════════════════════════════════

    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasGO = new GameObject("DeathScreenCanvas");
        canvasGO.transform.SetParent(transform, false);

        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Topmost

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Ensure an EventSystem exists — buttons won't work without one
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        rootGroup = canvasGO.AddComponent<CanvasGroup>();

        // ── Blood Edges (procedural texture) ──
        GameObject bloodGO = new GameObject("BloodEdges", typeof(RectTransform));
        bloodGO.transform.SetParent(canvasGO.transform, false);
        StretchFull(bloodGO);

        bloodEdgesImage = bloodGO.AddComponent<RawImage>();
        bloodEdgesImage.texture = GenerateBloodEdgeTexture(1024, 1024);
        bloodEdgesImage.color = new Color(1f, 1f, 1f, 0f); // Start invisible
        bloodEdgesImage.raycastTarget = false;

        // ── Black Centre Overlay ──
        GameObject blackGO = new GameObject("BlackOverlay", typeof(RectTransform));
        blackGO.transform.SetParent(canvasGO.transform, false);
        StretchFull(blackGO);

        blackOverlay = blackGO.AddComponent<Image>();
        blackOverlay.color = new Color(0f, 0f, 0f, 0f); // Start invisible
        blackOverlay.raycastTarget = false;

        // ── "YOU DIED" Text ──
        GameObject textGO = new GameObject("YouDiedText", typeof(RectTransform));
        textGO.transform.SetParent(canvasGO.transform, false);

        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.5f, 0.55f);
        textRT.anchorMax = new Vector2(0.5f, 0.55f);
        textRT.pivot = new Vector2(0.5f, 0.5f);
        textRT.anchoredPosition = Vector2.zero;
        textRT.sizeDelta = new Vector2(900f, 160f);

        diedText = textGO.AddComponent<TextMeshProUGUI>();
        diedText.text = "YOU DIED";
        diedText.fontSize = 96;
        diedText.fontStyle = FontStyles.Bold;
        diedText.alignment = TextAlignmentOptions.Center;
        diedText.color = new Color(0.75f, 0.05f, 0.02f, 0f); // Blood red, start invisible
        diedText.enableWordWrapping = false;

        // Subtle outline / glow
        diedText.outlineWidth = 0.15f;
        diedText.outlineColor = new Color32(30, 0, 0, 255);

        // ── "Try Again" Button ──
        // Outer border glow
        GameObject btnBorderGO = new GameObject("TryAgainBorder", typeof(RectTransform));
        btnBorderGO.transform.SetParent(canvasGO.transform, false);

        RectTransform borderRect = btnBorderGO.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 0.35f);
        borderRect.anchorMax = new Vector2(0.5f, 0.35f);
        borderRect.pivot = new Vector2(0.5f, 0.5f);
        borderRect.anchoredPosition = Vector2.zero;
        borderRect.sizeDelta = new Vector2(380f, 85f);

        Image borderImg = btnBorderGO.AddComponent<Image>();
        borderImg.color = new Color(0.7f, 0.08f, 0.04f, 0.6f);
        borderImg.raycastTarget = false;

        // Actual button (slightly smaller, sits inside the border)
        GameObject btnGO = new GameObject("TryAgainButton", typeof(RectTransform));
        btnGO.transform.SetParent(btnBorderGO.transform, false);

        RectTransform btnRect = btnGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = Vector2.zero;
        btnRect.sizeDelta = new Vector2(360f, 72f);

        Image btnBg = btnGO.AddComponent<Image>();
        btnBg.color = new Color(0.45f, 0.03f, 0.02f, 0.95f);

        tryAgainButton = btnGO.AddComponent<Button>();
        tryAgainButton.targetGraphic = btnBg;

        // Hover / pressed colours — more noticeable transitions
        ColorBlock cb = tryAgainButton.colors;
        cb.normalColor = new Color(0.45f, 0.03f, 0.02f, 0.95f);
        cb.highlightedColor = new Color(0.85f, 0.12f, 0.06f, 1f);
        cb.pressedColor = new Color(1f, 0.2f, 0.1f, 1f);
        cb.selectedColor = cb.highlightedColor;
        cb.fadeDuration = 0.12f;
        tryAgainButton.colors = cb;
        tryAgainButton.onClick.AddListener(OnTryAgain);

        // ── Skull/dagger icon (text-based) ──
        GameObject iconGO = new GameObject("ButtonIcon", typeof(RectTransform));
        iconGO.transform.SetParent(btnGO.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0f);
        iconRT.anchorMax = new Vector2(0f, 1f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(18f, 0f);
        iconRT.sizeDelta = new Vector2(45f, 0f);

        TextMeshProUGUI iconText = iconGO.AddComponent<TextMeshProUGUI>();
        iconText.text = "\u2694"; // Crossed swords unicode
        iconText.fontSize = 32;
        iconText.alignment = TextAlignmentOptions.Center;
        iconText.color = new Color(0.9f, 0.6f, 0.5f, 0.8f);
        iconText.raycastTarget = false;

        // Button label
        GameObject lblGO = new GameObject("ButtonLabel", typeof(RectTransform));
        lblGO.transform.SetParent(btnGO.transform, false);
        StretchFull(lblGO);

        buttonLabel = lblGO.AddComponent<TextMeshProUGUI>();
        buttonLabel.text = "TRY AGAIN";
        buttonLabel.fontSize = 38;
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.color = new Color(0.95f, 0.88f, 0.82f, 1f);
        buttonLabel.characterSpacing = 8f; // Spaced-out letters for style

        // Right icon (mirror)
        GameObject iconRGO = new GameObject("ButtonIconR", typeof(RectTransform));
        iconRGO.transform.SetParent(btnGO.transform, false);
        RectTransform iconRRT = iconRGO.GetComponent<RectTransform>();
        iconRRT.anchorMin = new Vector2(1f, 0f);
        iconRRT.anchorMax = new Vector2(1f, 1f);
        iconRRT.pivot = new Vector2(1f, 0.5f);
        iconRRT.anchoredPosition = new Vector2(-18f, 0f);
        iconRRT.sizeDelta = new Vector2(45f, 0f);

        TextMeshProUGUI iconRText = iconRGO.AddComponent<TextMeshProUGUI>();
        iconRText.text = "\u2694";
        iconRText.fontSize = 32;
        iconRText.alignment = TextAlignmentOptions.Center;
        iconRText.color = new Color(0.9f, 0.6f, 0.5f, 0.8f);
        iconRText.raycastTarget = false;

        // Button group starts invisible — wraps the border (which contains the button)
        buttonGroup = btnBorderGO.AddComponent<CanvasGroup>();
        buttonGroup.alpha = 0f;
        buttonGroup.interactable = false;
        buttonGroup.blocksRaycasts = false;
    }

    // ════════════════════════════════════════
    //  PROCEDURAL BLOOD EDGE TEXTURE
    // ════════════════════════════════════════

    /// <summary>
    /// Generates a texture with bloody, dripping edges and a transparent centre.
    /// Uses layered noise for an organic, horror look.
    /// </summary>
    private Texture2D GenerateBloodEdgeTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float cx = width * 0.5f;
        float cy = height * 0.5f;

        // Blood colour palette
        Color bloodDeep = new Color(0.25f, 0.0f, 0.0f, 1f);
        Color bloodMid = new Color(0.5f, 0.02f, 0.01f, 1f);
        Color bloodBright = new Color(0.7f, 0.06f, 0.03f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (float)x / width;
                float ny = (float)y / height;

                // Distance from centre (normalised 0-1, 0 = centre)
                float dx = (x - cx) / cx;
                float dy = (y - cy) / cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // ── Base edge mask: transparent centre, opaque edges ──
                // Sharper falloff than the damage vignette
                float edgeMask = Mathf.SmoothStep(0f, 1f, (dist - 0.3f) / 0.5f);

                // ── Drip noise: vertical streaks from top and bottom edges ──
                float dripNoise1 = Mathf.PerlinNoise(nx * 8f + 3.7f, ny * 2.5f) * 0.5f;
                float dripNoise2 = Mathf.PerlinNoise(nx * 15f + 7.1f, ny * 1.2f) * 0.3f;

                // Drips hang from top/bottom edges
                float topDrip = Mathf.Clamp01(1f - ny * 2.5f) * (dripNoise1 + dripNoise2);
                float bottomDrip = Mathf.Clamp01((ny - 0.6f) * 2.5f) * (dripNoise1 * 0.7f);

                // ── Splatter noise: irregular blobs near edges ──
                float splatter1 = Mathf.PerlinNoise(nx * 12f + 1.3f, ny * 12f + 5.9f);
                float splatter2 = Mathf.PerlinNoise(nx * 25f + 9.2f, ny * 25f + 2.1f);
                float splatterMask = Mathf.Clamp01((dist - 0.45f) * 3f); // Only near edges
                float splatter = splatterMask * Mathf.Clamp01(splatter1 * splatter2 * 4f - 0.8f);

                // ── Organic edge wobble ──
                float wobble = Mathf.PerlinNoise(nx * 6f, ny * 6f) * 0.15f;
                edgeMask = Mathf.Clamp01(edgeMask + wobble * edgeMask);

                // ── Combine all layers ──
                float alpha = Mathf.Clamp01(edgeMask + topDrip + bottomDrip + splatter);

                // ── Colour variation based on layers ──
                Color col;
                if (splatter > 0.1f)
                    col = Color.Lerp(bloodBright, bloodMid, splatter);
                else if (topDrip + bottomDrip > 0.15f)
                    col = Color.Lerp(bloodMid, bloodDeep, (topDrip + bottomDrip) * 0.8f);
                else
                    col = Color.Lerp(bloodMid, bloodDeep, Mathf.Clamp01(dist - 0.5f));

                col.a = alpha;
                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply();
        return tex;
    }

    // ════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════

    private void StretchFull(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
