using UnityEngine;

/// <summary>
/// Abstract base class for all enemy types.
/// Handles HP, damage, death, player reference, and auto-generated 3D health bar.
/// Subclasses implement their own behaviour via Update / state machines.
/// </summary>
public abstract class EnemyBase : MonoBehaviour
{
    // ───────── Health ─────────
    [Header("Health")]
    [SerializeField] protected int maxHealth = 100;
    protected int currentHealth;

    // ───────── Player Reference ─────────
    [Header("Player Reference")]
    [Tooltip("Assign the player Transform. Auto-finds by tag 'Player' if left empty.")]
    [SerializeField] protected Transform playerTransform;

    // ───────── Detection ─────────
    [Header("Detection")]
    [SerializeField] protected float detectionRadius = 15f;
    [SerializeField] protected float loseRadius = 20f;

    /// <summary>True while the player is within detection / lose range.</summary>
    protected bool playerDetected;

    // ───────── Health Bar Config ─────────
    [Header("Health Bar")]
    [Tooltip("World-space offset above the enemy pivot for the health bar.")]
    [SerializeField] protected float healthBarHeight = 1f;
    [Tooltip("Width of the bar in world units.")]
    [SerializeField] protected float healthBarWorldWidth = 1.2f;

    // ───────── Internals ─────────
    protected Vector3 spawnPoint;
    protected bool isDead;

    // Health bar internals (auto-generated)
    private Transform hbRoot;
    private Transform hbFillPivot;      // scaled on X to represent HP %
    private Renderer hbFillRenderer;
    private Renderer hbBgRenderer;
    private Camera mainCam;
    private Gradient hbGradient;
    private MaterialPropertyBlock hbBlock;
    private bool hbAutoCreated;

    // ───────── Unity Lifecycle ─────────

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        spawnPoint = transform.position;

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null) playerObj = GameObject.Find("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        mainCam = Camera.main;

        // Force Y position to 1 (ignore prefab overrides)
        healthBarHeight = 1f;

        // Destroy any old Canvas-based health bars left on prefabs
        foreach (Transform child in transform)
        {
            if (child.name == "EnemyHealthBar")
                Destroy(child.gameObject);
        }

        // Always auto-create the 3D health bar
        CreateHealthBar3D();
    }

    protected virtual void Update()
    {
        if (isDead) return;
        UpdateDetection();
        UpdateHealthBar();
    }

    // ═══════════════════════════════════════════
    //  3D HEALTH BAR  (two quads – bg + fill)
    // ═══════════════════════════════════════════

    private void CreateHealthBar3D()
    {
        hbAutoCreated = true;
        hbBlock = new MaterialPropertyBlock();

        float barW = healthBarWorldWidth;
        float barH = barW * 0.12f;          // thin bar
        float border = barW * 0.04f;        // border padding

        // ── Root (empty) ──
        GameObject root = new GameObject("HP_Bar");
        root.transform.SetParent(transform);
        root.transform.localPosition = new Vector3(0f, healthBarHeight, 0f);
        
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * 0.7f;
        hbRoot = root.transform;

        // ── Background quad ──
        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "HP_BG";
        bg.transform.SetParent(root.transform, false);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(barW + border, barH + border, 1f);
        Object.Destroy(bg.GetComponent<Collider>());  // no physics

        hbBgRenderer = bg.GetComponent<Renderer>();
        Material bgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        bgMat.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        // Make it render transparent so alpha works
        bgMat.SetFloat("_Surface", 1f);    // 1 = Transparent
        bgMat.SetFloat("_Blend", 0f);
        bgMat.SetOverrideTag("RenderType", "Transparent");
        bgMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        bgMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        bgMat.SetInt("_ZWrite", 0);
        bgMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // double-sided
        bgMat.renderQueue = 3000;
        hbBgRenderer.material = bgMat;
        hbBgRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hbBgRenderer.receiveShadows = false;

        // ── Fill pivot  (anchored at left edge so we can scale X from there) ──
        GameObject fillPivot = new GameObject("HP_FillPivot");
        fillPivot.transform.SetParent(root.transform, false);
        // Position at the left edge of the bar
        fillPivot.transform.localPosition = new Vector3(-barW * 0.5f, 0f, -0.001f);
        fillPivot.transform.localScale = Vector3.one;
        hbFillPivot = fillPivot.transform;

        // ── Fill quad (child of pivot) ──
        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "HP_Fill";
        fill.transform.SetParent(fillPivot.transform, false);
        // Offset so the left edge is at the pivot (quad mesh centre is at 0,0)
        fill.transform.localPosition = new Vector3(barW * 0.5f, 0f, 0f);
        fill.transform.localScale = new Vector3(barW, barH, 1f);
        Object.Destroy(fill.GetComponent<Collider>());

        hbFillRenderer = fill.GetComponent<Renderer>();
        Material fillMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        fillMat.color = Color.green;
        fillMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // double-sided
        hbFillRenderer.material = fillMat;
        hbFillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hbFillRenderer.receiveShadows = false;

        // ── Gradient ──
        hbGradient = new Gradient();
        hbGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.85f, 0.1f, 0.1f), 0f),   // red
                new GradientColorKey(new Color(0.95f, 0.75f, 0.1f), 0.45f), // yellow
                new GradientColorKey(new Color(0.15f, 0.85f, 0.15f), 1f)  // green
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        ApplyHealthBarVisual(1f);
    }

    /// <summary>Set fill and colour. t = 0..1 (0 = dead, 1 = full).</summary>
    private void ApplyHealthBarVisual(float t)
    {
        if (hbFillPivot == null) return;

        t = Mathf.Clamp01(t);

        // Scale the pivot on X to shrink the fill from the left
        Vector3 s = hbFillPivot.localScale;
        s.x = t;
        hbFillPivot.localScale = s;

        // Colour
        Color c = hbGradient.Evaluate(t);
        hbFillRenderer.material.color = c;
    }

    private void UpdateHealthBar()
    {
        if (hbRoot == null || mainCam == null) return;

        // Billboard – face camera
        hbRoot.rotation = mainCam.transform.rotation;
    }

    // ───────── Detection ─────────

    protected virtual void UpdateDetection()
    {
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        if (!playerDetected && dist <= detectionRadius)
            playerDetected = true;
        else if (playerDetected && dist > loseRadius)
            playerDetected = false;
    }

    // ───────── Damage / Death ─────────

    /// <summary>Deal damage to this enemy. Safe to call multiple times.</summary>
    public virtual void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(currentHealth - amount, 0);

        // Update 3D health bar
        ApplyHealthBarVisual((float)currentHealth / maxHealth);

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>Get the current health of this enemy.</summary>
    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    /// <summary>Alias used by the existing animation-event damage pipeline.</summary>
    public void TakeAnimationDamage(int amount)
    {
        TakeDamage(amount);
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        // Destroy health bar immediately
        if (hbRoot != null)
            Destroy(hbRoot.gameObject);

        // Try ragdoll first (reuses existing EnemyRagdollActivator)
        var ragdoll = GetComponent<EnemyRagdollActivator>();
        if (ragdoll != null)
        {
            ragdoll.ActivateRagdoll();
            Destroy(gameObject, 10f);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ───────── Helpers ─────────

    /// <summary>Distance to the player (or float.MaxValue if no player).</summary>
    protected float DistanceToPlayer()
    {
        if (playerTransform == null) return float.MaxValue;
        return Vector3.Distance(transform.position, playerTransform.position);
    }

    /// <summary>Direction toward the player (flat Y).</summary>
    protected Vector3 DirectionToPlayerFlat()
    {
        if (playerTransform == null) return Vector3.zero;
        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        return dir.normalized;
    }

    // ───────── Gizmos ─────────

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, loseRadius);
    }
}
