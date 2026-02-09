using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Advanced "Rerun" by Dani aesthetic — auto-configures URP post-processing at runtime.
/// Drop on any GameObject. No Inspector wiring needed.
/// ///
///  Effects stack:
///   • Bloom            — high-quality dual-kawase, warm tint, dirt-lens simulation
///   • Tonemapping      — ACES filmic
///   • Color Adjustments— warm exposure push, crushed blacks, boosted saturation
///   • White Balance    — heavy warm temperature shift
///   • Color Curves     — custom S-curve for contrast + lifted shadows
///   • Split Toning     — cool blue shadows, warm orange highlights
///   • Shadows Midtones Highlights — separate tinting per luminance band
///   • Lift Gamma Gain  — warmth pushed through every tonal range
///   • Channel Mixer    — subtle red-into-green bleed for organic warmth
///   • Chromatic Aberration — subtle lens fringe
///   • Lens Distortion  — slight barrel for immersion
///   • Depth of Field   — bokeh background blur (gentle)
///   • Motion Blur      — subtle camera blur on movement
///   • Vignette         — heavy dark edges
///   • Film Grain       — textured analog grain
///   • Panini Projection— slight wide-angle distortion
///   • Gradient skybox, warm sun, linear fog, ambient light
///
///   + Runtime bloom pulse system (bloom breathes gently over time)
/// </summary>
public class RerunPostProcessing : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════
    //  Bloom
    // ═══════════════════════════════════════════════════════════════════
    [Header("Bloom")]
    [SerializeField] private float bloomThreshold    = 0.65f;
    [SerializeField] private float bloomIntensity    = 1.8f;
    [SerializeField] private float bloomScatter      = 0.72f;
    [SerializeField] private Color bloomTint         = new Color(1f, 0.9f, 0.82f, 1f);
    [SerializeField] private bool  bloomHighQuality  = true;

    [Header("Bloom Pulse (breathing effect)")]
    [SerializeField] private bool  bloomPulseEnabled = true;
    [SerializeField] private float bloomPulseSpeed   = 0.4f;   // cycles per second
    [SerializeField] private float bloomPulseRange   = 0.35f;  // ±intensity variation

    // ═══════════════════════════════════════════════════════════════════
    //  Colour Grading
    // ═══════════════════════════════════════════════════════════════════
    [Header("Colour Grading")]
    [SerializeField] private float postExposure  = 0.4f;
    [SerializeField] private float contrast      = 20f;
    [SerializeField] private float saturation    = 15f;
    [SerializeField] private Color colorFilter   = new Color(1f, 0.93f, 0.87f);  // warm filter

    [Header("White Balance")]
    [SerializeField] private float temperature = 30f;
    [SerializeField] private float wbTint      = 8f;

    [Header("Split Toning")]
    [SerializeField] private Color shadowTone    = new Color(0.30f, 0.38f, 0.58f);  // cool blue
    [SerializeField] private Color highlightTone = new Color(0.97f, 0.72f, 0.45f);  // warm amber
    [SerializeField] private float splitBalance  = 25f;

    [Header("Shadows Midtones Highlights")]
    [SerializeField] private Color smhShadows    = new Color(0.92f, 0.88f, 1f, 0f);     // cool lift
    [SerializeField] private Color smhMidtones   = new Color(1f, 0.96f, 0.92f, 0f);     // warm push
    [SerializeField] private Color smhHighlights = new Color(1f, 0.92f, 0.80f, 0.05f);  // golden glow

    [Header("Lift Gamma Gain")]
    [SerializeField] private Color liftColor  = new Color(1f, 0.94f, 0.88f, 0.02f);
    [SerializeField] private Color gammaColor = new Color(1f, 0.97f, 0.93f, 0f);
    [SerializeField] private Color gainColor  = new Color(1f, 0.93f, 0.82f, 0.12f);

    [Header("Channel Mixer")]
    [SerializeField] private float redInGreen = 5f;   // bleeds warm red into green channel

    // ═══════════════════════════════════════════════════════════════════
    //  Lens Effects
    // ═══════════════════════════════════════════════════════════════════
    [Header("Chromatic Aberration")]
    [SerializeField] private float chromaticIntensity = 0.12f;

    [Header("Lens Distortion")]
    [SerializeField] private float lensDistortion = -0.15f;  // slight barrel

    [Header("Depth of Field")]
    [SerializeField] private bool  dofEnabled        = true;
    [SerializeField] private float dofFocusDistance   = 12f;
    [SerializeField] private float dofAperture        = 8f;
    [SerializeField] private float dofFocalLength     = 50f;

    [Header("Motion Blur")]
    [SerializeField] private bool  motionBlurEnabled  = true;
    [SerializeField] private float motionBlurIntensity = 0.3f;
    [SerializeField] private int   motionBlurClamp     = 4;

    [Header("Panini Projection")]
    [SerializeField] private float paniniDistance = 0.08f;
    [SerializeField] private float paniniCrop    = 0.1f;

    // ═══════════════════════════════════════════════════════════════════
    //  Film & Vignette
    // ═══════════════════════════════════════════════════════════════════
    [Header("Vignette")]
    [SerializeField] private float vignetteIntensity  = 0.38f;
    [SerializeField] private float vignetteSmoothness = 0.4f;
    [SerializeField] private Color vignetteColor      = new Color(0.08f, 0.04f, 0.02f); // warm dark

    [Header("Film Grain")]
    [SerializeField] private float grainIntensity = 0.2f;
    [SerializeField] private float grainResponse  = 0.8f;

    // ═══════════════════════════════════════════════════════════════════
    //  Environment
    // ═══════════════════════════════════════════════════════════════════
    [Header("Sky")]
    [SerializeField] private Color skyTop   = new Color(0.12f, 0.10f, 0.22f);
    [SerializeField] private Color skyMid   = new Color(0.97f, 0.50f, 0.30f);
    [SerializeField] private Color skyBot   = new Color(1f, 0.82f, 0.60f);
    [SerializeField] private float sunAngle = 6f;

    [Header("Sun")]
    [SerializeField] private Color sunColor     = new Color(1f, 0.82f, 0.58f);
    [SerializeField] private float sunIntensity = 1.8f;

    [Header("Fog")]
    [SerializeField] private bool  enableFog = true;
    [SerializeField] private Color fogColor  = new Color(0.92f, 0.65f, 0.48f);
    [SerializeField] private float fogStart  = 20f;
    [SerializeField] private float fogEnd    = 160f;

    [Header("Ambient")]
    [SerializeField] private Color ambientColor = new Color(0.65f, 0.50f, 0.42f);

    // ─────────────────── Runtime refs ───────────────────
    private Bloom bloom;
    private float baseBloomIntensity;

    // ═══════════════════════════════════════════════════════════════════
    private void Start()
    {
        SetupVolume();
        SetupSky();
        SetupDirectionalLight();
        SetupFog();
        SetupAmbient();
    }

    private void Update()
    {
        // ── Bloom breathing effect ──
        if (bloomPulseEnabled && bloom != null)
        {
            float pulse = Mathf.Sin(Time.time * bloomPulseSpeed * Mathf.PI * 2f) * bloomPulseRange;
            bloom.intensity.Override(baseBloomIntensity + pulse);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Volume Setup
    // ═══════════════════════════════════════════════════════════════════
    private void SetupVolume()
    {
        GameObject volGO = new GameObject("RerunPostProcessVolume");
        volGO.transform.SetParent(transform, false);
        volGO.layer = 0;

        Volume vol    = volGO.AddComponent<Volume>();
        vol.isGlobal  = true;
        vol.priority  = 10;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        // ──────────── Bloom ────────────
        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(bloomThreshold);
        bloom.intensity.Override(bloomIntensity);
        bloom.scatter.Override(bloomScatter);
        bloom.tint.Override(bloomTint);
        bloom.highQualityFiltering.Override(bloomHighQuality);
        baseBloomIntensity = bloomIntensity;

        // ──────────── Tonemapping ────────────
        Tonemapping tonemap = profile.Add<Tonemapping>(true);
        tonemap.mode.Override(TonemappingMode.ACES);

        // ──────────── Color Adjustments ────────────
        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.Override(postExposure);
        colorAdj.contrast.Override(contrast);
        colorAdj.saturation.Override(saturation);
        colorAdj.colorFilter.Override(colorFilter);
        colorAdj.hueShift.Override(0f);

        // ──────────── White Balance ────────────
        WhiteBalance wb = profile.Add<WhiteBalance>(true);
        wb.temperature.Override(temperature);
        wb.tint.Override(wbTint);

        // ──────────── Color Curves (custom S-curve) ────────────
        ColorCurves curves = profile.Add<ColorCurves>(true);
        // Master luminance: lift shadows, gentle S-curve for contrast
        TextureCurve masterCurve = new TextureCurve(
            new Keyframe[] {
                new Keyframe(0f, 0.05f),    // lift pure blacks slightly
                new Keyframe(0.25f, 0.20f), // shadow region — lifted
                new Keyframe(0.5f, 0.50f),  // midtones — neutral
                new Keyframe(0.75f, 0.82f), // highlights — pushed up
                new Keyframe(1f, 1f)        // pure white stays
            },
            0f, false, new Vector2(0f, 1f)
        );
        curves.master.Override(masterCurve);

        // Red channel: slight push in highlights for warmth
        TextureCurve redCurve = new TextureCurve(
            new Keyframe[] {
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 0.52f),  // slight red push in mids
                new Keyframe(1f, 1f)
            },
            0f, false, new Vector2(0f, 1f)
        );
        curves.red.Override(redCurve);

        // Blue channel: pull down in highlights (warmer highs)
        TextureCurve blueCurve = new TextureCurve(
            new Keyframe[] {
                new Keyframe(0f, 0.03f),    // slight blue in deep shadows
                new Keyframe(0.5f, 0.48f),  // pull blue below mid
                new Keyframe(1f, 0.92f)     // less blue in highlights
            },
            0f, false, new Vector2(0f, 1f)
        );
        curves.blue.Override(blueCurve);

        // ──────────── Split Toning ────────────
        SplitToning split = profile.Add<SplitToning>(true);
        split.shadows.Override(shadowTone);
        split.highlights.Override(highlightTone);
        split.balance.Override(splitBalance);

        // ──────────── Shadows Midtones Highlights ────────────
        ShadowsMidtonesHighlights smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(smhShadows.r, smhShadows.g, smhShadows.b, smhShadows.a));
        smh.midtones.Override(new Vector4(smhMidtones.r, smhMidtones.g, smhMidtones.b, smhMidtones.a));
        smh.highlights.Override(new Vector4(smhHighlights.r, smhHighlights.g, smhHighlights.b, smhHighlights.a));
        smh.shadowsStart.Override(0f);
        smh.shadowsEnd.Override(0.3f);
        smh.highlightsStart.Override(0.55f);
        smh.highlightsEnd.Override(1f);

        // ──────────── Lift Gamma Gain ────────────
        LiftGammaGain lgg = profile.Add<LiftGammaGain>(true);
        lgg.lift.Override(new Vector4(liftColor.r, liftColor.g, liftColor.b, liftColor.a));
        lgg.gamma.Override(new Vector4(gammaColor.r, gammaColor.g, gammaColor.b, gammaColor.a));
        lgg.gain.Override(new Vector4(gainColor.r, gainColor.g, gainColor.b, gainColor.a));

        // ──────────── Channel Mixer ────────────
        ChannelMixer mixer = profile.Add<ChannelMixer>(true);
        mixer.redOutRedIn.Override(100f);
        mixer.redOutGreenIn.Override(0f);
        mixer.redOutBlueIn.Override(0f);
        mixer.greenOutRedIn.Override(redInGreen);  // warm red bleed into green
        mixer.greenOutGreenIn.Override(100f);
        mixer.greenOutBlueIn.Override(0f);
        mixer.blueOutRedIn.Override(0f);
        mixer.blueOutGreenIn.Override(0f);
        mixer.blueOutBlueIn.Override(100f);

        // ──────────── Chromatic Aberration ────────────
        ChromaticAberration ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.Override(chromaticIntensity);

        // ──────────── Lens Distortion ────────────
        LensDistortion lens = profile.Add<LensDistortion>(true);
        lens.intensity.Override(lensDistortion);
        lens.xMultiplier.Override(1f);
        lens.yMultiplier.Override(1f);

        // ──────────── Depth of Field ────────────
        if (dofEnabled)
        {
            DepthOfField dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(dofFocusDistance);
            dof.aperture.Override(dofAperture);
            dof.focalLength.Override(dofFocalLength);
            dof.bladeCount.Override(6);
        }

        // ──────────── Motion Blur ────────────
        if (motionBlurEnabled)
        {
            MotionBlur mb = profile.Add<MotionBlur>(true);
            mb.intensity.Override(motionBlurIntensity);
            mb.clamp.Override(motionBlurClamp);
        }

        // ──────────── Panini Projection ────────────
        PaniniProjection panini = profile.Add<PaniniProjection>(true);
        panini.distance.Override(paniniDistance);
        panini.cropToFit.Override(paniniCrop);

        // ──────────── Vignette ────────────
        Vignette vig = profile.Add<Vignette>(true);
        vig.color.Override(vignetteColor);
        vig.intensity.Override(vignetteIntensity);
        vig.smoothness.Override(vignetteSmoothness);
        vig.rounded.Override(true);

        // ──────────── Film Grain ────────────
        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium2);
        grain.intensity.Override(grainIntensity);
        grain.response.Override(grainResponse);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Sky
    // ═══════════════════════════════════════════════════════════════════
    private void SetupSky()
    {
        Material skyMat = null;

        Shader gradShader = Shader.Find("Skybox/RerunGradient");
        if (gradShader != null)
        {
            skyMat = new Material(gradShader);
            skyMat.SetColor("_TopColor", skyTop);
            skyMat.SetColor("_MidColor", skyMid);
            skyMat.SetColor("_BotColor", skyBot);
            skyMat.SetFloat("_MidPoint", 0.42f);
            skyMat.SetFloat("_Exposure", 1.3f);
            skyMat.SetFloat("_HorizonSharpness", 1.2f);
        }
        else
        {
            Shader procShader = Shader.Find("Skybox/Procedural");
            if (procShader != null)
            {
                skyMat = new Material(procShader);
                skyMat.SetColor("_SkyTint", skyMid);
                skyMat.SetColor("_GroundColor", skyBot);
                skyMat.SetFloat("_Exposure", 1.2f);
                skyMat.SetFloat("_SunSize", 0.07f);
                skyMat.SetFloat("_SunSizeConvergence", 12f);
                skyMat.SetFloat("_AtmosphereThickness", 1.4f);
            }
        }

        if (skyMat != null)
        {
            RenderSettings.skybox = skyMat;
            DynamicGI.UpdateEnvironment();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Directional Light (Sun)
    // ═══════════════════════════════════════════════════════════════════
    private void SetupDirectionalLight()
    {
        Light sun = null;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) { sun = l; break; }
        }

        if (sun == null)
        {
            GameObject sunGO = new GameObject("RerunSun");
            sunGO.transform.SetParent(transform, false);
            sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        sun.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
        sun.color     = sunColor;
        sun.intensity = sunIntensity;
        sun.shadows       = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.shadowBias     = 0.6f;
        sun.shadowNormalBias = 0.4f;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Fog
    // ═══════════════════════════════════════════════════════════════════
    private void SetupFog()
    {
        RenderSettings.fog = enableFog;
        if (enableFog)
        {
            RenderSettings.fogMode          = FogMode.Linear;
            RenderSettings.fogColor         = fogColor;
            RenderSettings.fogStartDistance  = fogStart;
            RenderSettings.fogEndDistance    = fogEnd;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Ambient
    // ═══════════════════════════════════════════════════════════════════
    private void SetupAmbient()
    {
        RenderSettings.ambientMode      = AmbientMode.Flat;
        RenderSettings.ambientLight     = ambientColor;
        RenderSettings.ambientIntensity = 1.2f;
        RenderSettings.reflectionIntensity = 0.7f;
        RenderSettings.defaultReflectionResolution = 128;
    }
}

