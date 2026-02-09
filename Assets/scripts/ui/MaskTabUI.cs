using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the player's mask inventory when Tab is held.
/// Each icon displays the primary ability (left) and the superpower (right).
/// Locked superpowers are shown as a darkened half; clicking them shows a "not learned" toast.
/// </summary>
public class MaskTabUI : MonoBehaviour
{
    public static MaskTabUI Instance { get; private set; }

    [Header("Layout")]
    public Vector2 panelSize = new Vector2(600, 120);
    public int iconSize = 96;
    public int spacing = 12;

    private Canvas canvas;
    private GameObject panel;
    private PlayerMaskController playerMasks;
    private List<GameObject> iconGOs = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureCanvas();
        BuildPanel();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("MaskTabUI");
        go.AddComponent<MaskTabUI>();
        DontDestroyOnLoad(go);
    }

    private void EnsureCanvas()
    {
        canvas = GetComponentInChildren<Canvas>();
        if (canvas != null) return;

        var cGO = new GameObject("MaskTab_Canvas");
        cGO.transform.SetParent(transform);
        canvas = cGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        cGO.AddComponent<GraphicRaycaster>();
    }

    private void BuildPanel()
    {
        panel = new GameObject("MaskTabPanel");
        panel.transform.SetParent(canvas.transform, false);

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.95f);
        rt.anchorMax = new Vector2(0.5f, 0.95f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = panelSize;
        rt.anchoredPosition = Vector2.zero;

        var img = panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.35f);

        var hl = panel.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = spacing;
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;

        panel.SetActive(false);
    }

    private void Update()
    {
        // Show while Tab held
        bool show = Input.GetKey(KeyCode.Tab);
        if (show && !panel.activeSelf)
            Show();
        if (!show && panel.activeSelf)
            Hide();

        if (panel.activeSelf)
        {
            // Keep inventory up to date
            EnsurePlayerMasks();
            RebuildIconsIfNeeded();
        }
    }

    private void EnsurePlayerMasks()
    {
        if (playerMasks != null) return;
        playerMasks = FindObjectOfType<PlayerMaskController>();
    }

    private void Show()
    {
        EnsurePlayerMasks();
        panel.SetActive(true);
        RebuildIconsIfNeeded(force: true);
    }

    private void Hide()
    {
        panel.SetActive(false);
    }

    private void RebuildIconsIfNeeded(bool force = false)
    {
        if (playerMasks == null) return;
        var inv = playerMasks.Inventory;
        if (!force && inv.Count == iconGOs.Count) return;

        // Clear
        foreach (var g in iconGOs) Destroy(g);
        iconGOs.Clear();

        // Build icons
        foreach (var mask in inv)
        {
            var go = new GameObject("MaskIcon");
            go.transform.SetParent(panel.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            // Base dark icon
            var baseGO = new GameObject("Base");
            baseGO.transform.SetParent(go.transform, false);
            var baseRT = baseGO.AddComponent<RectTransform>();
            baseRT.anchorMin = Vector2.zero;
            baseRT.anchorMax = Vector2.one;
            baseRT.offsetMin = Vector2.zero;
            baseRT.offsetMax = Vector2.zero;
            var baseImg = baseGO.AddComponent<Image>();
            baseImg.sprite = mask.icon;
            baseImg.preserveAspect = true;
            baseImg.color = new Color(0.15f, 0.15f, 0.15f, 1f); // dark

            // Foreground (bright) cropped to left half or full if super unlocked
            var fgGO = new GameObject("Bright");
            fgGO.transform.SetParent(go.transform, false);
            var fgRT = fgGO.AddComponent<RectTransform>();
            fgRT.anchorMin = new Vector2(0f, 0f);
            fgRT.anchorMax = new Vector2(0.5f, 1f);
            fgRT.offsetMin = Vector2.zero;
            fgRT.offsetMax = Vector2.zero;
            var fgImg = fgGO.AddComponent<Image>();
            fgImg.sprite = mask.icon;
            fgImg.preserveAspect = true;
            fgImg.color = Color.white;

            // If superpower unlocked, expand bright to full
            if (mask.superpowerUnlocked)
            {
                fgRT.anchorMax = new Vector2(1f, 1f);
            }

            // Add interaction handler
            var iconHandler = go.AddComponent<MaskTabIcon>();
            iconHandler.Setup(mask, playerMasks);

            iconGOs.Add(go);
        }
    }
}
