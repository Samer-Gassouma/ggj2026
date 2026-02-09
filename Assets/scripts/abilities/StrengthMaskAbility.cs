using UnityEngine;

[CreateAssetMenu(menuName = "Masks/Strength Mask", fileName = "StrengthMask")]
public class StrengthMaskAbility : MaskAbility
{
    [Header("Primary — Titan Power")]
    public float swordScaleMultiplier = 1.5f;
    public float damageMultiplier = 2f;
    public float attackKnockbackForce = 8f;

    [Header("Superpower — Shockwave")]
    public float shockwaveRadius = 6f;
    public float shockwaveDamage = 25f;
    public float shockwaveCooldown = 4f;
    public bool canBreakDestructibles = true;

    public override void Activate(PlayerAbilityContext context)
    {
        // Primary: bigger sword, more damage, heavy knockback
        context.swordScaleMultiplier = swordScaleMultiplier;
        context.damageMultiplier = damageMultiplier;
        context.attackKnockbackForce = attackKnockbackForce;

        // Screen overlay — blood red vignette
        MaskScreenOverlay.Show(MaskOverlayType.Strength);

        // Attach runtime handler (for shockwave & sword scaling)
        var handler = context.GetComponent<StrengthMaskHandler>();
        if (handler == null)
            handler = context.gameObject.AddComponent<StrengthMaskHandler>();
        handler.Setup(this, context);
        handler.enabled = true;

        if (superpowerUnlocked)
            ActivateSuperpower(context);
    }

    public override void Deactivate(PlayerAbilityContext context)
    {
        context.swordScaleMultiplier = 1f;
        context.damageMultiplier = 1f;
        context.attackKnockbackForce = 0f;
        context.shockwaveEnabled = false;
        context.canBreakDestructibles = false;

        // Remove screen overlay
        MaskScreenOverlay.Hide();

        var handler = context.GetComponent<StrengthMaskHandler>();
        if (handler != null) handler.enabled = false;

        // Restore sword scale
        StrengthMaskHandler.RestoreSwordScale();
    }

    protected override void ActivateSuperpower(PlayerAbilityContext context)
    {
        context.shockwaveEnabled = true;
        context.canBreakDestructibles = canBreakDestructibles;
        context.shockwaveRadius = shockwaveRadius;
        context.shockwaveDamage = shockwaveDamage;
        context.shockwaveCooldown = shockwaveCooldown;
    }
}

/// <summary>
/// Runtime handler attached to the player when Strength Mask is active.
/// Handles sword scaling and shockwave ground-slam (superpower).
/// </summary>
public class StrengthMaskHandler : MonoBehaviour
{
    private StrengthMaskAbility data;
    private PlayerAbilityContext context;
    private float shockwaveCooldownTimer;
    
    private static Transform swordTransform;
    private static Vector3 originalSwordScale;
    private static bool scaleStored = false;

    // Material swap
    private static Material[][] originalMaterialArrays; // per-renderer original material arrays
    private static Renderer[] swordRenderers;
    private static bool materialsStored = false;
    private static Material cachedBloodMaterial;

    public void Setup(StrengthMaskAbility ability, PlayerAbilityContext ctx)
    {
        data = ability;
        context = ctx;
        shockwaveCooldownTimer = 0f;
        ApplySwordScale();
    }

    private void OnEnable()
    {
        ApplySwordScale();
    }

    private void Update()
    {
        if (data == null || context == null) return;

        shockwaveCooldownTimer -= Time.deltaTime;

        // Superpower: Ground Slam — Press V while grounded
        if (context.shockwaveEnabled && shockwaveCooldownTimer <= 0f && Input.GetKeyDown(KeyCode.V))
        {
            PerformShockwave();
        }
    }

    private void ApplySwordScale()
    {
        if (swordTransform == null)
        {
            var swordCtrl = FindAnyObjectByType<SwordController>();
            if (swordCtrl != null)
            {
                swordTransform = swordCtrl.transform;
                if (!scaleStored)
                {
                    originalSwordScale = swordTransform.localScale;
                    scaleStored = true;
                }
            }
        }

        if (swordTransform != null && data != null)
        {
            swordTransform.localScale = originalSwordScale * data.swordScaleMultiplier;
        }

        ApplyBloodMaterial();
    }

    public static void RestoreSwordScale()
    {
        if (swordTransform != null && scaleStored)
        {
            swordTransform.localScale = originalSwordScale;
        }
        RestoreSwordMaterial();
    }

    private void ApplyBloodMaterial()
    {
        if (swordTransform == null) return;

        // Gather renderers once (skip Trail / Particle / Line renderers)
        if (!materialsStored)
        {
            var allRenderers = swordTransform.GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (var r in allRenderers)
            {
                if (r is TrailRenderer || r is ParticleSystemRenderer || r is LineRenderer) continue;
                list.Add(r);
            }
            swordRenderers = list.ToArray();

            // Store each renderer's full material array separately
            originalMaterialArrays = new Material[swordRenderers.Length][];
            for (int i = 0; i < swordRenderers.Length; i++)
            {
                // Clone sharedMaterials so we keep an untouched copy
                var shared = swordRenderers[i].sharedMaterials;
                originalMaterialArrays[i] = new Material[shared.Length];
                System.Array.Copy(shared, originalMaterialArrays[i], shared.Length);
            }
            materialsStored = true;
        }

        // Create (or reuse) the blood material
        if (cachedBloodMaterial == null)
            cachedBloodMaterial = CreateBloodMaterial();

        // Apply to every renderer, every material slot
        foreach (var r in swordRenderers)
        {
            if (r == null) continue;
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = cachedBloodMaterial;
            r.materials = mats;
        }

        Debug.Log($"🩸 Strength Mask: Sword material changed to BLOOD RED (x{data.damageMultiplier} damage)");
    }

    public static void RestoreSwordMaterial()
    {
        if (!materialsStored || swordRenderers == null || originalMaterialArrays == null) return;

        for (int i = 0; i < swordRenderers.Length; i++)
        {
            if (swordRenderers[i] == null) continue;
            swordRenderers[i].materials = originalMaterialArrays[i];
        }

        materialsStored = false;
        swordRenderers = null;
        originalMaterialArrays = null;
        Debug.Log("🗡️ Sword material restored to original");
    }

    private static Material CreateBloodMaterial()
    {
        // Try every common shader — URP Lit → URP Simple Lit → Standard → Diffuse
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");

        Material mat = new Material(shader);

        Color bloodColor = new Color(0.5f, 0.01f, 0.01f, 1f);   // dark blood red
        Color specColor  = new Color(0.8f, 0.1f, 0.1f, 1f);     // bright red specular
        Color emitColor  = new Color(0.35f, 0.0f, 0.0f, 1f);    // dim red glow

        // ── Clear any main texture so the color shows pure ──
        mat.SetTexture("_MainTex", null);          // Standard / Legacy
        mat.SetTexture("_BaseMap", null);           // URP Lit
        mat.SetTexture("_BumpMap", null);           // Normal map off
        mat.SetTexture("_MetallicGlossMap", null);  // No metal texture
        mat.SetTexture("_OcclusionMap", null);

        // ── Base color (set on all possible property names) ──
        mat.SetColor("_BaseColor", bloodColor);     // URP Lit
        mat.SetColor("_Color", bloodColor);          // Standard / Legacy
        mat.SetColor("_SpecColor", specColor);       // Simple Lit specular

        // ── Metallic / Smoothness ──
        mat.SetFloat("_Metallic", 0.75f);
        mat.SetFloat("_Smoothness", 0.9f);
        mat.SetFloat("_GlossMapScale", 0.9f);       // Standard smoothness

        // ── Emission — blood glow ──
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", emitColor);
        mat.SetColor("_EmissiveColor", emitColor);  // alternate name

        // ── Rendering ──
        mat.SetFloat("_Surface", 0);                 // 0 = Opaque (URP)
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

        return mat;
    }

    private void PerformShockwave()
    {
        shockwaveCooldownTimer = data.shockwaveCooldown;

        Vector3 origin = transform.position;

        // Damage all enemies in radius
        Collider[] hits = Physics.OverlapSphere(origin, data.shockwaveRadius);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null) enemy = hit.GetComponentInParent<EnemyBase>();

            if (enemy != null)
            {
                enemy.TakeDamage(Mathf.RoundToInt(data.shockwaveDamage));

                // Push enemies away
                Rigidbody rb = hit.attachedRigidbody;
                if (rb != null)
                {
                    rb.AddExplosionForce(data.shockwaveDamage * 30f, origin, data.shockwaveRadius, 2f, ForceMode.Impulse);
                }
            }

            // Break destructible objects (superpower)
            if (context.canBreakDestructibles && hit.CompareTag("Destructible"))
            {
                Rigidbody rb = hit.attachedRigidbody;
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.AddExplosionForce(500f, origin, data.shockwaveRadius, 1f, ForceMode.Impulse);
                }
            }
        }

        // Visual feedback — spawn a shockwave ring
        SpawnShockwaveVFX(origin);

        Debug.Log("⚡ SHOCKWAVE! Hit enemies in radius " + data.shockwaveRadius);
    }

    private void SpawnShockwaveVFX(Vector3 origin)
    {
        // Create an expanding ring using a LineRenderer
        var go = new GameObject("ShockwaveVFX");
        go.transform.position = origin;

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;

        // Use default material (URP Lit not needed for simple effect)
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(1f, 0.4f, 0.1f, 0.9f);
        line.endColor = new Color(1f, 0.2f, 0f, 0f);
        line.startWidth = 0.5f;
        line.endWidth = 0.1f;

        int segments = 32;
        line.positionCount = segments;

        // Start small, animate expansion via coroutine
        var animator = go.AddComponent<ShockwaveAnimator>();
        animator.Setup(data.shockwaveRadius, segments, line, origin);
    }
}

/// <summary>
/// Simple component that expands a circle LineRenderer then destroys itself.
/// </summary>
public class ShockwaveAnimator : MonoBehaviour
{
    private float maxRadius;
    private int segments;
    private LineRenderer line;
    private Vector3 center;
    private float duration = 0.5f;
    private float elapsed;

    public void Setup(float radius, int segs, LineRenderer lr, Vector3 pos)
    {
        maxRadius = radius;
        segments = segs;
        line = lr;
        center = pos;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float currentRadius = Mathf.Lerp(0.5f, maxRadius, t);
        float alpha = 1f - t;

        line.startColor = new Color(1f, 0.4f, 0.1f, alpha);
        line.endColor = new Color(1f, 0.2f, 0f, alpha * 0.5f);
        line.startWidth = Mathf.Lerp(0.5f, 0.05f, t);
        line.endWidth = Mathf.Lerp(0.3f, 0.02f, t);

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0.1f, Mathf.Sin(angle)) * currentRadius;
            line.SetPosition(i, pos);
        }
    }
}
