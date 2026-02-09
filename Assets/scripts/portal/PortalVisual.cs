using UnityEngine;

/// <summary>
/// Portal visual — particles + pulsing light only, no mesh, no rotation.
/// Uses URP-compatible shaders. Drop on the Portal GameObject.
/// </summary>
public class PortalVisual : MonoBehaviour
{
    [Header("Size")]
    [SerializeField] private float radius = 2f;

    [Header("Colours")]
    [SerializeField] private Color coreColor = new Color(0.2f, 0.6f, 1f, 1f);    // blue-cyan
    [SerializeField] private Color edgeColor = new Color(0.6f, 0.2f, 1f, 1f);    // purple
    [SerializeField] private Color glowColor = new Color(0.3f, 0.5f, 1f, 1f);

    [Header("Light")]
    [SerializeField] private float pulseSpeed    = 2f;
    [SerializeField] private float pulseAmount   = 0.15f;
    [SerializeField] private float lightRange    = 8f;
    [SerializeField] private float lightIntensity = 2f;

    private Light portalLight;

    private void Start()
    {
        CreateParticles();
        CreateLight();
    }

    private void Update()
    {
        // Pulse the light — that's it, no rotation
        if (portalLight != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            portalLight.intensity = lightIntensity * pulse;
        }
    }

    // ─────────────────── URP Particle Material ────────────────────────
    private Material BuildURPParticleMat(Color tint, bool additive)
    {
        // Try URP particle shaders first, then fall back
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.name = "PortalParticle";

        // URP Particles/Unlit uses _BaseColor and _Surface
        mat.SetColor("_BaseColor", tint);
        mat.SetColor("_Color", tint);

        if (additive)
        {
            // Surface = Transparent, Blend = Additive
            mat.SetFloat("_Surface", 1f);  // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 1f);    // 0=Alpha, 1=Additive
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.renderQueue = 3000;

            // Keywords for URP transparency
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }

        // Vertex colours
        mat.EnableKeyword("_COLOROVERLAY_ON");

        return mat;
    }

    // ─────────────────── Particles ────────────────────────────────────
    private void CreateParticles()
    {
        Material additiveMat = BuildURPParticleMat(glowColor, true);

        // ── 1. Outer ring sparkles ──
        CreateRingSystem("PortalRing", radius * 0.95f, 60, 40f,
            1.5f, 0.5f, 0.15f, glowColor, coreColor, additiveMat);

        // ── 2. Inner glow ring (tighter, softer) ──
        CreateRingSystem("PortalInnerGlow", radius * 0.4f, 40, 25f,
            0.8f, 0.2f, 0.25f, coreColor, edgeColor, additiveMat);

        // ── 3. Rising wisps — float upward through the centre ──
        CreateWisps(additiveMat);

        // ── 4. Ambient dust / sparkle haze ──
        CreateDust(additiveMat);
    }

    private void CreateRingSystem(string name, float ringRadius, int maxParts,
        float rate, float lifetime, float speed, float size,
        Color startCol, Color endCol, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime      = lifetime;
        main.startSpeed         = speed;
        main.startSize          = size;
        main.startColor         = startCol;
        main.maxParticles       = maxParts;
        main.simulationSpace    = ParticleSystemSimulationSpace.Local;
        main.loop               = true;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = ringRadius;
        shape.arc       = 360f;
        shape.arcMode   = ParticleSystemShapeMultiModeValue.Random;

        // Colour fade
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(startCol, 0f),
                new GradientColorKey(endCol, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        // Shrink over lifetime
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = mat;
    }

    private void CreateWisps(Material mat)
    {
        GameObject go = new GameObject("PortalWisps");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor      = coreColor;
        main.maxParticles    = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop            = true;

        var emission = ps.emission;
        emission.rateOverTime = 18f;

        // Emit from within the portal circle area
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = radius * 0.6f;
        shape.arcMode   = ParticleSystemShapeMultiModeValue.Random;

        // Float upward
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.y = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

        // Fade out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(coreColor, 0f),
                new GradientColorKey(edgeColor, 0.5f),
                new GradientColorKey(glowColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.15f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        // Shrink
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f));

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = mat;
    }

    private void CreateDust(Material mat)
    {
        GameObject go = new GameObject("PortalDust");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor      = new Color(glowColor.r, glowColor.g, glowColor.b, 0.5f);
        main.maxParticles    = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop            = true;

        var emission = ps.emission;
        emission.rateOverTime = 20f;

        // Spread across the full portal area
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = radius * 0.8f;

        // Slow drift with noise
        var noise = ps.noise;
        noise.enabled   = true;
        noise.strength  = new ParticleSystem.MinMaxCurve(0.3f);
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.3f;
        noise.octaveCount = 2;

        // Fade in/out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(glowColor, 0f),
                new GradientColorKey(coreColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.4f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = mat;
    }

    // ─────────────────── Light ─────────────────────────────────────────
    private void CreateLight()
    {
        GameObject lightGO = new GameObject("PortalLight");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 0f, -0.5f);

        portalLight = lightGO.AddComponent<Light>();
        portalLight.type      = LightType.Point;
        portalLight.color     = glowColor;
        portalLight.range     = lightRange;
        portalLight.intensity = lightIntensity;
        portalLight.shadows   = LightShadows.None;
    }
}
