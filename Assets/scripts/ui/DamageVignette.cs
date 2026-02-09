using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen blood/red vignette that flashes on damage and shows persistent
/// red edges as the player's health decreases.
/// Entirely code-generated — no prefabs, no inspector setup needed.
/// Attach to the Player GameObject (next to PlayerHealth).
/// </summary>
public class DamageVignette : MonoBehaviour
{
    public static DamageVignette Instance { get; private set; }

    [Header("Vignette Settings")]
    [SerializeField] private float flashAlpha = 0.45f;
    [SerializeField] private float flashFadeSpeed = 3f;

    // Internals
    private RawImage vignetteImage;
    private float currentFlashAlpha;
    private float persistentAlpha;
    private Canvas canvas;

    private void Awake()
    {
        Instance = this;
        CreateVignetteUI();
    }

    private void Update()
    {
        // Fade out the flash portion
        if (currentFlashAlpha > 0f)
        {
            currentFlashAlpha -= flashFadeSpeed * Time.deltaTime;
            if (currentFlashAlpha < 0f) currentFlashAlpha = 0f;
        }

        // Final alpha = max(flash, persistent low-health tint)
        float alpha = Mathf.Max(currentFlashAlpha, persistentAlpha);
        if (vignetteImage != null)
        {
            Color c = vignetteImage.color;
            c.a = alpha;
            vignetteImage.color = c;
        }
    }

    /// <summary>
    /// Called by PlayerHealth when damage is taken.
    /// healthPercent is 0..1 (0 = dead, 1 = full).
    /// </summary>
    public void OnDamage(float healthPercent)
    {
        // Flash effect
        currentFlashAlpha = flashAlpha;

        // Persistent low-health vignette: stronger the lower the health
        // Starts appearing below 60% health, max at 0%
        float missing = 1f - healthPercent;
        persistentAlpha = Mathf.Clamp01(missing - 0.4f) * 0.6f; // 0 at >60%, 0.36 at 0%
    }

    // ───────── Code-Generated UI ─────────

    private void CreateVignetteUI()
    {
        // Create overlay Canvas
        GameObject canvasGO = new GameObject("DamageVignetteCanvas");
        canvasGO.transform.SetParent(transform);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // On top of everything

        // Canvas Scaler
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        // Vignette RawImage
        GameObject imgGO = new GameObject("VignetteImage");
        imgGO.transform.SetParent(canvasGO.transform, false);

        vignetteImage = imgGO.AddComponent<RawImage>();
        vignetteImage.texture = GenerateVignetteTexture(512, 512);
        vignetteImage.color = new Color(1f, 1f, 1f, 0f); // Start invisible
        vignetteImage.raycastTarget = false; // Don't block clicks

        // Stretch to fill entire screen
        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Generates a radial vignette texture: transparent center, dark red/blood edges.
    /// </summary>
    private Texture2D GenerateVignetteTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color bloodDark = new Color(0.4f, 0.0f, 0.0f, 1f);
        Color bloodBright = new Color(0.7f, 0.05f, 0.02f, 1f);

        float cx = width * 0.5f;
        float cy = height * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Normalised distance from centre (0 = centre, 1 = corner)
                float dx = (x - cx) / cx;
                float dy = (y - cy) / cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Vignette shape: transparent centre, solid edges
                // Start fading in at dist 0.5, fully opaque at dist 1.2+
                float alpha = Mathf.SmoothStep(0f, 1f, (dist - 0.35f) / 0.65f);

                // Add slight noise for organic blood look
                float noise = Mathf.PerlinNoise(x * 0.02f, y * 0.02f) * 0.3f;
                alpha = Mathf.Clamp01(alpha + noise * alpha);

                // Blend between dark and bright blood based on distance
                Color col = Color.Lerp(bloodBright, bloodDark, Mathf.Clamp01(dist - 0.5f));
                col.a = alpha;

                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply();
        return tex;
    }
}
