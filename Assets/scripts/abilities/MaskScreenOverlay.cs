using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen camera overlay that tints the screen edges/corners when a mask is active.
/// Strength = red, Dash = blue, Vision = purple.
/// Auto-creates itself — zero config needed.
/// </summary>
public class MaskScreenOverlay : MonoBehaviour
{
    public static MaskScreenOverlay Instance { get; private set; }

    // Overlay elements
    private Canvas overlayCanvas;
    private RawImage vignetteImage;
    private RawImage edgeGlowImage;
    private RawImage particleImage;

    // Animation
    private Color targetColor = Color.clear;
    private Color currentColor = Color.clear;
    private float fadeSpeed = 3f;
    private float pulseTimer;
    private float pulseSpeed = 1.5f;
    private bool isActive = false;

    // Particle-like animated dots
    private RawImage[] floatingParticles;
    private float[] particleAngles;
    private float[] particleSpeeds;
    private float[] particleRadii;
    private int particleCount = 6;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateOverlay();
    }

    private void Update()
    {
        if (!isActive && currentColor.a <= 0.001f) return;

        // Smooth fade toward target
        currentColor = Color.Lerp(currentColor, targetColor, Time.unscaledDeltaTime * fadeSpeed);

        // Pulse effect — subtle breathing
        pulseTimer += Time.unscaledDeltaTime * pulseSpeed;
        float pulse = 1f + Mathf.Sin(pulseTimer) * 0.15f; // 15% brightness oscillation

        // Apply to vignette
        if (vignetteImage != null)
        {
            Color vigCol = currentColor;
            vigCol.a *= pulse;
            vignetteImage.color = vigCol;
        }

        // Edge glow — slightly brighter, different pulse phase
        if (edgeGlowImage != null)
        {
            float edgePulse = 1f + Mathf.Sin(pulseTimer * 1.3f + 0.5f) * 0.2f;
            Color edgeCol = currentColor;
            edgeCol.a *= 0.6f * edgePulse;
            edgeGlowImage.color = edgeCol;
        }

        // Animate floating particles
        AnimateParticles();

        // Auto-disable when fully faded out
        if (!isActive && currentColor.a <= 0.005f)
        {
            currentColor = Color.clear;
            SetOverlayVisible(false);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Call to activate the screen overlay with a mask color.
    /// </summary>
    public static void Show(MaskOverlayType type)
    {
        EnsureInstance();
        Instance.ActivateOverlay(type);
    }

    /// <summary>
    /// Call to fade out and hide the overlay.
    /// </summary>
    public static void Hide()
    {
        if (Instance != null)
            Instance.DeactivateOverlay();
    }

    // ══════════════════════════════════════════════════════════
    //  INTERNAL
    // ══════════════════════════════════════════════════════════

    private void ActivateOverlay(MaskOverlayType type)
    {
        isActive = true;
        pulseTimer = 0f;
        SetOverlayVisible(true);

        switch (type)
        {
            case MaskOverlayType.Strength:
                targetColor = new Color(0.8f, 0.05f, 0.02f, 0.25f); // blood red
                break;
            case MaskOverlayType.Dash:
                targetColor = new Color(0.1f, 0.3f, 0.95f, 0.22f);  // ice blue
                break;
            case MaskOverlayType.Vision:
                targetColor = new Color(0.55f, 0.1f, 0.85f, 0.22f); // mystic purple
                break;
        }

        // Update particle colors immediately
        UpdateParticleColors(type);
    }

    private void DeactivateOverlay()
    {
        isActive = false;
        targetColor = Color.clear;
    }

    private void SetOverlayVisible(bool visible)
    {
        if (overlayCanvas != null)
            overlayCanvas.gameObject.SetActive(visible);
    }

    // ══════════════════════════════════════════════════════════
    //  OVERLAY CREATION (all via code)
    // ══════════════════════════════════════════════════════════

    private void CreateOverlay()
    {
        // ── Canvas ──
        GameObject canvasObj = new GameObject("MaskOverlayCanvas");
        canvasObj.transform.SetParent(transform, false);
        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 450; // above game, below crosshair (500)

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>().blockingMask = 0; // don't block clicks

        // ── Layer 1: Vignette (dark corners fading to transparent center) ──
        vignetteImage = CreateFullScreenImage(canvasObj.transform, "Vignette", CreateVignetteTexture(512));
        vignetteImage.color = Color.clear;

        // ── Layer 2: Edge glow (soft colored border) ──
        edgeGlowImage = CreateFullScreenImage(canvasObj.transform, "EdgeGlow", CreateEdgeGlowTexture(512));
        edgeGlowImage.color = Color.clear;

        // ── Layer 3: Floating particles around edges ──
        CreateFloatingParticles(canvasObj.transform);

        canvasObj.SetActive(false);
    }

    private RawImage CreateFullScreenImage(Transform parent, string name, Texture2D tex)
    {
        GameObject imgObj = new GameObject(name);
        imgObj.transform.SetParent(parent, false);

        RawImage img = imgObj.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;

        RectTransform rt = imgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return img;
    }

    /// <summary>
    /// Procedural vignette: dark at corners, transparent at center.
    /// </summary>
    private Texture2D CreateVignetteTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = center.magnitude;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float normalizedDist = dist / maxDist;

                // Smooth falloff — transparent center, opaque corners
                // Use a curve that's 0 in the center and ramps up at edges
                float alpha = Mathf.Pow(normalizedDist, 2.2f);  // power curve for smooth falloff
                alpha = Mathf.Clamp01(alpha);

                // Extra darkening in the very corners
                float cornerX = Mathf.Abs((float)x / size - 0.5f) * 2f;
                float cornerY = Mathf.Abs((float)y / size - 0.5f) * 2f;
                float cornerFactor = cornerX * cornerY;
                alpha = Mathf.Max(alpha, cornerFactor * 1.5f);
                alpha = Mathf.Clamp01(alpha);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Procedural edge glow: thin bright border with soft inner fade.
    /// </summary>
    private Texture2D CreateEdgeGlowTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float borderWidth = size * 0.12f;   // 12% border
        float innerFade = size * 0.25f;     // fade zone

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance from nearest edge
                float distFromEdge = Mathf.Min(
                    Mathf.Min(x, size - 1 - x),
                    Mathf.Min(y, size - 1 - y)
                );

                float alpha = 0f;

                if (distFromEdge < borderWidth)
                {
                    // Bright at the very edge, fading inward
                    alpha = 1f - (distFromEdge / borderWidth);
                    alpha = Mathf.Pow(alpha, 1.5f);
                }
                else if (distFromEdge < borderWidth + innerFade)
                {
                    // Soft inner glow
                    float t = (distFromEdge - borderWidth) / innerFade;
                    alpha = (1f - t) * 0.15f; // very subtle inner glow
                }

                // Boost corners
                float cornerX = Mathf.Abs((float)x / size - 0.5f) * 2f;
                float cornerY = Mathf.Abs((float)y / size - 0.5f) * 2f;
                float corner = Mathf.Pow(cornerX * cornerY, 0.8f);
                alpha = Mathf.Max(alpha, corner * 0.7f);

                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // ══════════════════════════════════════════════════════════
    //  FLOATING PARTICLES (small glowing dots orbiting edges)
    // ══════════════════════════════════════════════════════════

    private void CreateFloatingParticles(Transform parent)
    {
        floatingParticles = new RawImage[particleCount];
        particleAngles = new float[particleCount];
        particleSpeeds = new float[particleCount];
        particleRadii = new float[particleCount];

        Texture2D dotTex = CreateParticleDotTexture(32);

        for (int i = 0; i < particleCount; i++)
        {
            GameObject dotObj = new GameObject($"Particle_{i}");
            dotObj.transform.SetParent(parent, false);

            RawImage dot = dotObj.AddComponent<RawImage>();
            dot.texture = dotTex;
            dot.raycastTarget = false;
            dot.color = Color.clear;

            RectTransform rt = dotObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            float size = Random.Range(8f, 18f);
            rt.sizeDelta = new Vector2(size, size);

            floatingParticles[i] = dot;
            particleAngles[i] = Random.Range(0f, Mathf.PI * 2f);
            particleSpeeds[i] = Random.Range(0.3f, 0.8f) * (Random.value > 0.5f ? 1f : -1f);
            particleRadii[i] = Random.Range(0.35f, 0.48f); // distance from center (as fraction of screen half)
        }
    }

    private Texture2D CreateParticleDotTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.Clamp01(dist / radius);
                alpha = Mathf.Pow(alpha, 2f); // soft falloff
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void AnimateParticles()
    {
        if (floatingParticles == null) return;

        for (int i = 0; i < floatingParticles.Length; i++)
        {
            if (floatingParticles[i] == null) continue;

            // Orbit around edges
            particleAngles[i] += particleSpeeds[i] * Time.unscaledDeltaTime;

            float halfW = 960f * particleRadii[i]; // reference res half-width
            float halfH = 540f * particleRadii[i]; // reference res half-height

            // Elliptical path around screen edges
            float px = Mathf.Cos(particleAngles[i]) * halfW;
            float py = Mathf.Sin(particleAngles[i]) * halfH;

            RectTransform rt = floatingParticles[i].GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(px, py);

            // Fade particles with the overlay, plus individual twinkle
            float twinkle = 0.5f + 0.5f * Mathf.Sin(pulseTimer * 2f + i * 1.7f);
            Color pCol = currentColor;
            pCol.a = currentColor.a * twinkle * 1.5f;
            pCol.r = Mathf.Min(1f, pCol.r * 1.8f); // brighter particles
            pCol.g = Mathf.Min(1f, pCol.g * 1.8f);
            pCol.b = Mathf.Min(1f, pCol.b * 1.8f);
            floatingParticles[i].color = pCol;
        }
    }

    private void UpdateParticleColors(MaskOverlayType type)
    {
        // Randomize particle orbits on mask switch for fresh feel
        for (int i = 0; i < particleCount; i++)
        {
            particleAngles[i] = Random.Range(0f, Mathf.PI * 2f);
            particleSpeeds[i] = Random.Range(0.3f, 0.8f) * (Random.value > 0.5f ? 1f : -1f);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  SINGLETON BOOTSTRAP
    // ══════════════════════════════════════════════════════════

    private static void EnsureInstance()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("MaskScreenOverlay");
        go.AddComponent<MaskScreenOverlay>();
        // Awake() sets Instance and calls DontDestroyOnLoad
    }
}

/// <summary>
/// Enum for the 3 mask overlay types.
/// </summary>
public enum MaskOverlayType
{
    Strength,   // Red
    Dash,       // Blue
    Vision      // Purple
}
