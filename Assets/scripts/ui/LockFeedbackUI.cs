using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a centered "Not learned yet" style message when the player attempts to use a locked superpower.
/// Auto-creates itself at runtime if missing.
/// </summary>
public class LockFeedbackUI : MonoBehaviour
{
    public static LockFeedbackUI Instance { get; private set; }

    [Header("Appearance")]
    public int fontSize = 36;
    public Color textColor = Color.white;
    public Color bgColor = new Color(0f, 0f, 0f, 0.6f);
    public float displayTime = 1.6f;
    public float fadeTime = 0.25f;

    private Canvas canvas;
    private GameObject panel;
    private Text msgText;
    private CanvasGroup cg;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureCanvas();
        BuildUI();
        HideImmediate();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("LockFeedbackUI");
        go.AddComponent<LockFeedbackUI>();
        DontDestroyOnLoad(go);
    }

    private void EnsureCanvas()
    {
        canvas = GetComponentInChildren<Canvas>();
        if (canvas != null) return;

        var cGO = new GameObject("LockFeedback_Canvas");
        cGO.transform.SetParent(transform);
        canvas = cGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        cGO.AddComponent<CanvasScaler>();
        cGO.AddComponent<GraphicRaycaster>();
    }

    private void BuildUI()
    {
        panel = new GameObject("LockPanel");
        panel.transform.SetParent(canvas.transform, false);

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(540, 120);

        var img = panel.AddComponent<Image>();
        img.color = bgColor;

        var txtGO = new GameObject("Msg");
        txtGO.transform.SetParent(panel.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(16, 12);
        txtRT.offsetMax = new Vector2(-16, -12);

        msgText = txtGO.AddComponent<Text>();
        msgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (msgText.font == null) msgText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        msgText.fontSize = fontSize;
        msgText.alignment = TextAnchor.MiddleCenter;
        msgText.color = textColor;
        msgText.supportRichText = false;

        cg = panel.AddComponent<CanvasGroup>();
    }

    private void HideImmediate()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void ShowLocked(string title = "Locked", string message = "Not learned yet")
    {
        if (Instance == null) AutoCreate();
        if (panel == null || msgText == null) return;
        msgText.text = message;
        StopAllCoroutines();
        StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        panel.SetActive(true);
        cg.alpha = 0f;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / fadeTime);
            yield return null;
        }
        cg.alpha = 1f;

        float elapsed = 0f;
        while (elapsed < displayTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = 1f - Mathf.Clamp01(t / fadeTime);
            yield return null;
        }

        panel.SetActive(false);
    }
}
