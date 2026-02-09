using UnityEngine;

/// <summary>
/// Production-quality URP particle effects created entirely from code.
/// Uses layered systems, stretched billboards, velocity control, and tight tuning.
/// </summary>
public static class RuntimeVFXFactory
{
    // ───────── Cached Materials ─────────

    private static Material _particleMat;
    private static Material _additiveMat;

    public static Material GetParticleMaterial()
    {
        if (_particleMat != null) return _particleMat;
        _particleMat = CreateMat(additive: false);
        return _particleMat;
    }

    public static Material GetAdditiveMaterial()
    {
        if (_additiveMat != null) return _additiveMat;
        _additiveMat = CreateMat(additive: true);
        return _additiveMat;
    }

    private static Material CreateMat(bool additive)
    {
        string[] names = {
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default"
        };
        Shader sh = null;
        foreach (var n in names) { sh = Shader.Find(n); if (sh != null) break; }
        if (sh == null) sh = Shader.Find("Hidden/InternalErrorShader");

        var m = new Material(sh);
        m.SetFloat("_Surface", 1f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive)
        {
            m.SetFloat("_Blend", 1f);
            m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = 3100;
        }
        else
        {
            m.SetFloat("_Blend", 0f);
            m.renderQueue = 3000;
        }
        return m;
    }

    // ══════════════════════════════════════════════
    //  AMBIENT EMBERS — subtle idle glow (looping)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Tiny slow-drifting embers that float around the enemy.
    /// Reads as magical / smoldering energy without being noisy.
    /// </summary>
    public static ParticleSystem CreateAmbientEmbers(Transform parent, Color color)
    {
        var ps = MakeSystem("AmbientEmbers", parent, Vector3.zero);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, BrightenColor(color, 0.6f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 30;
        main.gravityModifier = -0.08f;

        var emission = ps.emission;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;

        // Slow upward drift
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);

        // Fade in and out gently
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = MakeGradient(
            new[] { color, BrightenColor(color, 0.3f) },
            new float[] { 0f, 1f },
            new float[] { 0f, 0.8f, 0.8f, 0f },
            new float[] { 0f, 0.2f, 0.7f, 1f }
        );

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = GetAdditiveMaterial();
        r.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // ══════════════════════════════════════════════
    //  CHASE STREAKS — stretched speed-line wisps (looping)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Stretched-billboard particles trailing behind during movement.
    /// Reads as speed / forward momentum.
    /// </summary>
    public static ParticleSystem CreateChaseStreaks(Transform parent, Color color)
    {
        var ps = MakeSystem("ChaseStreaks", parent, Vector3.zero);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(BrightenColor(color, 0.5f), Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 30f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        // Fade out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = MakeGradient(
            new[] { Color.white, color },
            new float[] { 0f, 0.3f },
            new float[] { 0.9f, 0f },
            new float[] { 0f, 1f }
        );

        // Shrink over lifetime
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0f, 1f, 1f, 0.1f));

        // STRETCHED billboard — the key to looking like speed lines
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = GetAdditiveMaterial();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0f;
        r.lengthScale = 4f; // stretch factor based on size

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // ══════════════════════════════════════════════
    //  WIND-UP GATHER — particles pulling inward (looping)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Particles spawned on a sphere that accelerate INWARD.
    /// Reads as "gathering power" — the classic charge-up tell.
    /// </summary>
    public static ParticleSystem CreateGatherEffect(Transform parent, Color color)
    {
        var ps = MakeSystem("GatherVFX", parent, Vector3.zero);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.startSpeed = -3f; // negative = inward toward center
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 50f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2f;

        // Brighten as they converge (approaching center = brighter)
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = MakeGradient(
            new[] { color * 0.5f, Color.white },
            new float[] { 0f, 1f },
            new float[] { 0.3f, 1f },
            new float[] { 0f, 1f }
        );

        // Shrink on approach
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0f, 0.6f, 1f, 1f));

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = GetAdditiveMaterial();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.05f;
        r.lengthScale = 3f;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // ══════════════════════════════════════════════
    //  IMPACT HIT — layered one-shot (sparks + flash + ring)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Clean, punchy impact: directional sparks + brief flash + expanding ring.
    /// All 3 layers on one parent that auto-destroys.
    /// </summary>
    public static void SpawnImpactBurst(Vector3 pos, Color color, Vector3 hitNormal)
    {
        GameObject root = new GameObject("Impact_VFX");
        root.transform.position = pos;

        // ── Layer 1: Directional sparks ──
        CreateImpactSparks(root.transform, color, hitNormal);

        // ── Layer 2: Bright flash ──
        CreateImpactFlash(root.transform, color);

        // ── Layer 3: Expanding ground ring ──
        CreateImpactRing(root.transform, color);

        // Auto-cleanup
        Object.Destroy(root, 2f);
    }

    private static void CreateImpactSparks(Transform parent, Color color, Vector3 normal)
    {
        var ps = MakeSystem("Sparks", parent, Vector3.zero);
        var main = ps.main;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, BrightenColor(color, 0.5f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.gravityModifier = 1.5f; // sparks arc downward

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 30) });

        // Cone shape aimed along hit normal
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.05f;
        if (normal.sqrMagnitude > 0.01f)
            ps.transform.rotation = Quaternion.LookRotation(normal);

        // Shrink
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0f, 1f, 1f, 0f));

        // White → color → dark
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = MakeGradient(
            new[] { Color.white, color, color * 0.3f },
            new float[] { 0f, 0.15f, 1f },
            new float[] { 1f, 0.6f, 0f },
            new float[] { 0f, 0.4f, 1f }
        );

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = GetAdditiveMaterial();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 3f;

        ps.Play();
    }

    private static void CreateImpactFlash(Transform parent, Color color)
    {
        var ps = MakeSystem("Flash", parent, Vector3.zero);
        var main = ps.main;
        main.loop = false;
        main.startLifetime = 0.15f;
        main.startSpeed = 0f;
        main.startSize = 2.5f;
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        // Rapid pop then shrink
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0f, 1f, 0.15f, 0.5f, 1f, 0f));

        // White → color → gone
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = MakeGradient(
            new[] { Color.white, BrightenColor(color, 0.5f) },
            new float[] { 0f, 0.5f },
            new float[] { 1f, 0f },
            new float[] { 0f, 1f }
        );

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = GetAdditiveMaterial();
        r.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }

    private static void CreateImpactRing(Transform parent, Color color)
    {
        var ps = MakeSystem("Ring", parent, Vector3.zero);
        ps.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var main = ps.main;
        main.loop = false;
        main.startLifetime = 0.3f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, color);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;
        shape.arc = 360f;

        // Fade out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = MakeGradient(
            new[] { Color.white, color * 0.7f },
            new float[] { 0f, 0.3f },
            new float[] { 0.8f, 0f },
            new float[] { 0f, 1f }
        );

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, MakeCurve(0f, 1f, 1f, 0.2f));

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = GetAdditiveMaterial();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.02f;
        r.lengthScale = 2f;

        ps.Play();
    }

    // ══════════════════════════════════════════════
    //  UTILITY HELPERS
    // ══════════════════════════════════════════════

    private static ParticleSystem MakeSystem(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.AddComponent<ParticleSystem>();
    }

    private static Color BrightenColor(Color c, float t)
    {
        return Color.Lerp(c, Color.white, t);
    }

    /// <summary>Build a gradient from parallel arrays.</summary>
    private static Gradient MakeGradient(Color[] colors, float[] colorTimes, float[] alphas, float[] alphaTimes)
    {
        var g = new Gradient();
        var ck = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            ck[i] = new GradientColorKey(colors[i], colorTimes[i]);
        var ak = new GradientAlphaKey[alphas.Length];
        for (int i = 0; i < alphas.Length; i++)
            ak[i] = new GradientAlphaKey(alphas[i], alphaTimes[i]);
        g.SetKeys(ck, ak);
        return g;
    }

    /// <summary>Quick 2-key animation curve.</summary>
    private static AnimationCurve MakeCurve(float t0, float v0, float t1, float v1)
    {
        return new AnimationCurve(new Keyframe(t0, v0), new Keyframe(t1, v1));
    }

    /// <summary>Quick 3-key animation curve.</summary>
    private static AnimationCurve MakeCurve(float t0, float v0, float t1, float v1, float t2, float v2)
    {
        return new AnimationCurve(new Keyframe(t0, v0), new Keyframe(t1, v1), new Keyframe(t2, v2));
    }
}
