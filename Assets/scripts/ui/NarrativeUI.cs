using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays narrative text at the bottom of the screen with a typewriter effect.
/// Builds its own Canvas ▸ Panel ▸ TMP_Text entirely from code — no Inspector wiring needed.
/// </summary>
public class NarrativeUI : MonoBehaviour
{
    [Header("Typewriter Settings")]
    [SerializeField] private float charDelay = 0.03f;
    [SerializeField] private float fadeSpeed = 2f;

    // Auto-created at runtime
    private TextMeshProUGUI narrativeText;
    private CanvasGroup canvasGroup;
    private Coroutine typewriterRoutine;

    private void Awake()
    {
        BuildUI();
    }

    // ───────────────── Auto-build the entire UI hierarchy ─────────────────
    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasGO = new GameObject("NarrativeCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // on top but below key prompts

        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel (dark strip at the bottom) ──
        GameObject panelGO = new GameObject("NarrativePanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);

        Image panelBg = panelGO.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.75f);

        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 0f);
        panelRT.anchorMax = new Vector2(1f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(0f, 120f);

        canvasGroup = panelGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // ── TMP Text ──
        GameObject textGO = new GameObject("NarrativeText", typeof(RectTransform));
        textGO.transform.SetParent(panelGO.transform, false);

        narrativeText = textGO.AddComponent<TextMeshProUGUI>();
        narrativeText.text = "";
        narrativeText.fontSize = 30;
        narrativeText.color = Color.white;
        narrativeText.alignment = TextAlignmentOptions.Center;
        narrativeText.enableWordWrapping = true;

        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(40f, 10f);
        textRT.offsetMax = new Vector2(-40f, -10f);
    }

    // ─────────────────────────── Public API ───────────────────────────────
    /// <summary>
    /// Show a line of narrative with typewriter effect, then optionally auto-hide.
    /// </summary>
    public void ShowNarrative(string message, float displayDuration = 0f)
    {
        if (typewriterRoutine != null)
            StopCoroutine(typewriterRoutine);

        typewriterRoutine = StartCoroutine(TypewriterRoutine(message, displayDuration));
    }

    /// <summary>
    /// Immediately hide the narrative panel.
    /// </summary>
    public void HideNarrative()
    {
        if (typewriterRoutine != null)
            StopCoroutine(typewriterRoutine);

        StartCoroutine(FadeOut());
    }

    // ─────────────────────────── Coroutines ───────────────────────────────
    private IEnumerator TypewriterRoutine(string message, float displayDuration)
    {
        narrativeText.text = "";
        yield return StartCoroutine(FadeIn());

        foreach (char c in message)
        {
            narrativeText.text += c;
            yield return new WaitForSeconds(charDelay);
        }

        if (displayDuration > 0f)
        {
            yield return new WaitForSeconds(displayDuration);
            yield return StartCoroutine(FadeOut());
        }
    }

    private IEnumerator FadeIn()
    {
        while (canvasGroup.alpha < 1f)
        {
            canvasGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOut()
    {
        while (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}
