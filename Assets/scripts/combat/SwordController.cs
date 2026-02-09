using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)] // Run before physics so sword never falls
public class SwordController : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int baseDamage = 10;
    [SerializeField] private int throwDamageMultiplier = 2;

    [Header("Input")]
    [SerializeField] private KeyCode pickupKey = KeyCode.F;
    [SerializeField] private KeyCode throwKey = KeyCode.G;

    [Header("Throw Settings")]
    [SerializeField] private float throwForce = 18f;
    [SerializeField] private float throwSpinSpeedDeg = 1200f;
    [SerializeField] private float aimMaxDistance = 80f;
    [SerializeField] private float arcUp = 0.15f; // small arc like a lob
    [SerializeField] private float throwSpawnForwardOffset = 0.35f; // avoid starting inside player colliders
    [SerializeField] private float throwCollisionGrace = 0.15f; // ignore collisions right after throw

    [Header("Landing / Pickup")]
    [SerializeField] private float settleDelay = 0.25f;         // wait a bit before switching to grounded
    [SerializeField] private float pickupTriggerRadius = 1.6f;
    [SerializeField] private string pickupPrompt = "Press F to pick up";

    [Header("Trail")]
    [SerializeField] private float trailTime = 0.15f;
    [SerializeField] private float trailStartWidth = 0.08f;
    [SerializeField] private float trailEndWidth = 0.02f;
    [SerializeField] private Color trailStartColor = new Color(1f, 0.9f, 0.5f, 1f);
    [SerializeField] private Color trailEndColor = new Color(1f, 0.5f, 0.2f, 0f);

    [Header("Hit Effects")]
    [SerializeField] private GameObject hitVFXPrefab; // Assign vfx_Implosion_01 in inspector
    [SerializeField] private Color damageTextColor = Color.yellow;
    [SerializeField] private float damageTextDuration = 1.5f;
    [SerializeField] private Font damageFont;

    [Header("SFX")]
    private AudioSource sfxSource;
    private AudioClip throwSFX;
    private AudioClip meleeSFX;

    [Header("Boomerang System")]
    [SerializeField] private float returnSpeed = 10f;
    [SerializeField] private float returnAcceleration = 5f;
    [SerializeField] private float autoCollectDistance = 2f;
    [SerializeField] private float returnArcHeight = 3f; // How high the return arc goes
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float autoReturnMinDistance = 15f; // Only auto-return if sword is farther than this
    [SerializeField] private float missReturnDelay = 5f; // Seconds to wait before auto-returning (only if far)


    [SerializeField] private UnityEngine.UI.Image crosshairDot; // assign the dot Image in inspector (the one you already create/use)
    [SerializeField] private Color crosshairNormal = Color.white;
    [SerializeField] private Color crosshairGrab = Color.cyan;

    private enum SwordState { Held, Flying, Returning, Grounded }
    private SwordState state = SwordState.Held;

    // Components
    private Rigidbody swordRb;
    private Animator swordAnimator;
    private TrailRenderer swordTrail;

    // Damage script (optional; if you have it)
    private SwordDamage swordDamage;

    // Player references
    private Transform playerTransform;
    private Transform camTransform;
    private Collider[] playerColliders;
    private Collider[] swordColliders;

    // Pickup trigger
    private SphereCollider pickupTrigger;
    private bool playerInsidePickup;

    // UI
    private GameObject pickupUICanvas;
    private Text pickupText;

    // Enhanced crosshair system
    private GameObject crosshairCanvas;
    private GameObject normalCrosshair;
    private GameObject aimingCrosshair;
    private Image normalCrosshairImage;
    private Image aimingCrosshairImage;
    private bool isAiming;

    // Death lockout
    private bool inputDisabled = false;

    // Held pose cache
    private Transform heldParent;
    private Vector3 heldLocalPos;
    private Quaternion heldLocalRot;
    private Vector3 heldLocalScale;

    // Throw timing
    private float throwTime = -999f;

    // Boomerang return system
    private Vector3 returnStartPosition;
    private float returnStartTime;
    private float returnDuration;
    private bool isReturning = false;

    // Cached reference for ability context (Strength Mask multiplier)
    private PlayerAbilityContext abilityContext;


    /// <summary>Completely disables all sword input (called on player death).</summary>
    public void DisableInput()
    {
        inputDisabled = true;
        // If currently aiming, cancel
        isAiming = false;
        // Hide pickup prompt
        SetPickupUI(false);
        // Stop trail
        if (swordTrail != null) swordTrail.emitting = false;
    }

    public void SetCrosshairNormal()
    {
        if (crosshairDot != null) crosshairDot.color = crosshairNormal;
    }

    public void SetCrosshairGrab()
    {
        if (crosshairDot != null) crosshairDot.color = crosshairGrab;
    }

    // Singleton to prevent duplicate swords
    private static SwordController _instance;

    /// <summary>Quick access to the active SwordController without a Find call.</summary>
    public static SwordController ActiveInstance => _instance;

    private void Awake()
    {
        // Destroy duplicate swords
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Duplicate SwordController detected! Destroying this one.");
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Player find
        GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerColliders = player.GetComponentsInChildren<Collider>(true);
            abilityContext = player.GetComponent<PlayerAbilityContext>();
        }

        camTransform = Camera.main ? Camera.main.transform : null;

        // Components
        swordAnimator = GetComponent<Animator>();
        swordDamage = GetComponent<SwordDamage>();

        // Colliders
        swordColliders = GetComponentsInChildren<Collider>(true);

        // Ensure no active RB on start FIRST (before anything else)
        swordRb = GetComponent<Rigidbody>();
        if (swordRb != null) DestroyImmediate(swordRb);
        swordRb = null;

        // Find the swordcontainer and cache it — do NOT reparent to it, so the sword stays at its scene position
        if (transform.parent != null && transform.parent.name == "swordcontainer")
        {
            heldParent = transform.parent;
        }
        else
        {
            Transform container = FindSwordContainer();
            heldParent = container; // may be null — will be found on pickup
        }

        // Hard-coded known-good position (for when it gets picked up)
        heldLocalPos = new Vector3(0.4450073f, -0.02f, 0.6600001f);
        heldLocalRot = Quaternion.Euler(90f, 90f, 0f);
        heldLocalScale = Vector3.one;

        // Sword starts on the ground — player must pick it up
        // Detach from swordcontainer so it stays in the world
        transform.SetParent(null, true);

        state = SwordState.Grounded;

        // Colliders as solid so the sword rests on geometry
        SetAllSwordCollidersTrigger(false);

        CreateOrConfigureTrail();

        // Remove any SphereCollider that might exist (we use distance-based pickup)
        SphereCollider existingSphere = GetComponent<SphereCollider>();
        if (existingSphere != null) DestroyImmediate(existingSphere);
        pickupTrigger = null;
        playerInsidePickup = false;

        CreatePickupUI();
        CreateCrosshairUI();

        SetPickupUI(false);
        SetCrosshair(true); // Always show crosshair

        // SFX setup — auto-load from Assets/SFX/
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0.6f; // Mostly 3D but audible everywhere
        sfxSource.volume = 0.7f;

        throwSFX = Resources.Load<AudioClip>("SFX/throw");
        meleeSFX = Resources.Load<AudioClip>("SFX/sword_melle");
    }

    private void Start()
    {
        // Sword starts grounded in the world — player must walk to it and press F
        // Keep any existing rigidbody so it can rest on geometry, or add one
        swordRb = GetComponent<Rigidbody>();
        if (swordRb == null) swordRb = gameObject.AddComponent<Rigidbody>();

        swordRb.mass = 1.5f;
        swordRb.useGravity = true;
        swordRb.isKinematic = false;
        swordRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Solid colliders so it sits on surfaces
        SetAllSwordCollidersTrigger(false);

        // Disable animator so it doesn't fight physics
        if (swordAnimator != null) swordAnimator.enabled = false;

        state = SwordState.Grounded;
    }

    private void Update()
    {
        switch (state)
        {
            case SwordState.Held:
                UpdateHeld();
                break;
            case SwordState.Grounded:
                UpdateGrounded();
                break;
            case SwordState.Flying:
                UpdateFlying();
                break;
            case SwordState.Returning:
                UpdateReturning();
                break;
        }
    }

    private void UpdateHeld()
    {
        if (inputDisabled) return;

        // Hold-to-aim throw (press/hold G to show enhanced crosshair, release to throw)
        if (Input.GetKeyDown(throwKey))
        {
            isAiming = true;
            SetAimingMode(true);
        }

        if (Input.GetKeyUp(throwKey) && isAiming)
        {
            isAiming = false;
            SetAimingMode(false);
            ThrowSword();
        }
        
        // Update crosshair position and accuracy indicator
        UpdateCrosshairAccuracy();
    }

    private void UpdateGrounded()
    {
        if (inputDisabled) return;
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // Auto-return if the sword is more than 60 units away or fell below the map
        if (dist > 60f || transform.position.y < playerTransform.position.y - 15f)
        {
            Debug.Log("Sword too far while grounded - auto-returning");
            StartAutoReturn();
            return;
        }

        bool inRange = dist <= pickupTriggerRadius;
        SetPickupUI(inRange);

        if (inRange && Input.GetKeyDown(pickupKey))
        {
            PickupSword();
        }
    }

    // =========================
    // Throw
    // =========================
    private void ThrowSword()
    {
        if (state != SwordState.Held) return;

        // Check mana cost for throw
        PlayerMana mana = PlayerMana.Instance;
        if (mana != null && !mana.TrySpendThrow()) return; // Not enough mana

        // Animator can override physics transforms, disable while thrown
        if (swordAnimator != null) swordAnimator.enabled = false;

        // Detach from hand/container, keep world transform
        transform.SetParent(null, true);

        // Make sword colliders solid for world collision
        SetAllSwordCollidersTrigger(false);

        // Ensure RB exists and configured
        swordRb = GetComponent<Rigidbody>();
        if (swordRb == null) swordRb = gameObject.AddComponent<Rigidbody>();

        swordRb.mass = 1.5f;
        swordRb.linearDamping = 0.05f;
        swordRb.angularDamping = 0.3f;
        swordRb.useGravity = true;
        swordRb.isKinematic = false;
        swordRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        swordRb.interpolation = RigidbodyInterpolation.Interpolate;

        // Ignore player collisions BEFORE first physics step
        IgnorePlayerCollision(true);

        // Aim from crosshair raycast
        Vector3 aimDir = GetAimDirection();

        // Push sword slightly forward to avoid starting inside player colliders
        transform.position += aimDir * throwSpawnForwardOffset;

        // Mark throw time (for collision grace)
        throwTime = Time.time;

        // Apply velocity + spin
        Vector3 throwDir = (aimDir + Vector3.up * arcUp).normalized;
        swordRb.linearVelocity = throwDir * throwForce;
        swordRb.angularVelocity = transform.right * (throwSpinSpeedDeg * Mathf.Deg2Rad);

        // Trail & damage
        if (swordTrail != null) swordTrail.emitting = true;

        if (swordDamage != null)
        {
            swordDamage.damage = baseDamage * throwDamageMultiplier;
            swordDamage.EnableDamage();
        }

        // No pickup while flying
        SetPickupUI(false);

        state = SwordState.Flying;
        LastThrowResult = ThrowResult.None; // reset for this new throw

        // Play throw SFX
        if (sfxSource != null && throwSFX != null)
            sfxSource.PlayOneShot(throwSFX, 0.85f);

        // Start monitoring — will freeze only once sword is resting on ground
        StartCoroutine(MonitorSwordFlight());

        Debug.Log($"Sword thrown from {transform.position} dir {throwDir}");
    }

    private Vector3 GetAimDirection()
    {
        if (Camera.main == null || camTransform == null)
            return transform.forward;

        // Use precise screen center for aiming
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Ray ray = Camera.main.ScreenPointToRay(screenCenter);
        
        // Multiple raycasts for better accuracy
        Vector3 targetPoint = Vector3.zero;
        bool hasTarget = false;
        
        // Primary raycast
        if (Physics.Raycast(ray, out RaycastHit hit, aimMaxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
            hasTarget = true;
        }
        
        if (hasTarget)
        {
            Vector3 aimDirection = targetPoint - transform.position;
            
            // Apply gravity compensation for more accurate throws
            if (isAiming)
            {
                float distance = aimDirection.magnitude;
                float timeToTarget = distance / throwForce;
                
                // Compensate for gravity drop
                float gravityComp = 0.5f * Physics.gravity.magnitude * timeToTarget * timeToTarget;
                aimDirection.y += gravityComp;
            }
            
            return aimDirection.normalized;
        }

        // Fallback: use camera forward with slight upward angle
        Vector3 fallbackDir = camTransform.forward;
        if (isAiming)
        {
            fallbackDir.y += 0.1f; // Slight upward compensation
        }
        
        return fallbackDir.normalized;
    }


    // =========================
    // Landing + Damage on hit
    // =========================
    private bool hasDealtThrowDamage = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (state != SwordState.Flying) return;

        // Grace period right after throw
        if (Time.time - throwTime < throwCollisionGrace)
            return;

        // Ignore player
        if (collision.collider.CompareTag("Player"))
            return;

        // Check if hit enemy
        var enemy = collision.gameObject.GetComponent<EnemyBase>() ??
                    collision.gameObject.GetComponentInParent<EnemyBase>();

        if (enemy != null)
        {
            // Store enemy health before damage
            int enemyHealthBefore = enemy.GetCurrentHealth();
            
            // Enemy hit: deal damage with Strength Mask multiplier
            float multiplier = GetDamageMultiplier();
            int dmg = Mathf.RoundToInt(baseDamage * throwDamageMultiplier * multiplier);
            enemy.TakeDamage(dmg);
            hasDealtThrowDamage = true;
            LastThrowResult = ThrowResult.HitEnemy;

            // Create hit effect at contact point
            Vector3 hitPoint = collision.GetContact(0).point;
            CreateHitEffect(hitPoint, dmg, false); // false = thrown attack
            
            // Check if enemy was killed and return sword accordingly
            bool enemyWasKilled = enemyHealthBefore <= dmg;
            StartBoomerangReturn(hitPoint);
            
            Debug.Log($"Hit enemy with throw for {dmg} damage (x{multiplier}){(enemyWasKilled ? " (ENEMY KILLED!)" : "")} - sword returning!");
        }
        else
        {
            // Hit non-enemy object: normal bounce behavior (land on ground)
            if (LastThrowResult == ThrowResult.None) LastThrowResult = ThrowResult.HitWall;
            if (swordRb != null)
            {
                swordRb.linearVelocity *= 0.3f;
                swordRb.angularVelocity *= 0.3f;
            }
            Debug.Log("Hit non-enemy object - sword will land normally");
        }
    }

    /// <summary>
    /// Started at throw time. Continuously monitors the sword and only
    /// freezes it once it has actually come to rest (on the ground).
    /// </summary>
    private IEnumerator MonitorSwordFlight()
    {
        // Give it at least 0.5s of flight before checking
        yield return new WaitForSeconds(0.5f);

        // Wait until velocity is very low for a sustained period
        float maxFlightTime = 8f;
        float elapsed = 0f;
        float stillTimer = 0f;

        while (elapsed < maxFlightTime)
        {
            if (state != SwordState.Flying) yield break; // Exit if state changed (e.g., returning)

            if (swordRb != null && swordRb.linearVelocity.magnitude < 0.15f)
            {
                stillTimer += 0.1f;
                if (stillTimer >= 0.3f) break; // still for 0.3s = resting on ground
            }
            else
            {
                stillTimer = 0f;
            }

            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        // Only freeze if still flying (not returning)
        if (state == SwordState.Flying)
        {
            // Freeze in place
            if (swordRb != null)
            {
                swordRb.linearVelocity = Vector3.zero;
                swordRb.angularVelocity = Vector3.zero;
                swordRb.isKinematic = true;
            }

            // Stop trail + reset damage
            if (swordTrail != null) swordTrail.emitting = false;
            if (swordDamage != null)
            {
                swordDamage.damage = baseDamage;
                swordDamage.DisableDamage();
            }

            hasDealtThrowDamage = false;

            float distToPlayer = playerTransform != null
                ? Vector3.Distance(transform.position, playerTransform.position)
                : 0f;

            if (distToPlayer > autoReturnMinDistance)
            {
                // Too far away — wait, then fly back automatically
                if (LastThrowResult == ThrowResult.None) LastThrowResult = ThrowResult.AutoReturned;
                Debug.Log($"Sword landed {distToPlayer:F1}m away (>{autoReturnMinDistance}m) - waiting {missReturnDelay}s then auto-returning");
                yield return new WaitForSeconds(missReturnDelay);

                // Double-check state hasn't changed during the wait (player might have picked it up)
                if (state == SwordState.Flying)
                {
                    Debug.Log("Sword auto-returning to player");
                    StartAutoReturn();
                }
            }
            else
            {
                // Close enough — just stay on the ground, player can walk to it
                if (LastThrowResult == ThrowResult.None) LastThrowResult = ThrowResult.HitWall;
                Debug.Log($"Sword landed {distToPlayer:F1}m away (<={autoReturnMinDistance}m) - staying grounded");
                state = SwordState.Grounded;
            }
        }
    }

    /// <summary>
    /// Called when the sword lands or is too far — triggers a boomerang return from Grounded/Flying.
    /// </summary>
    private void StartAutoReturn()
    {
        // Make sure we're in a throwable state
        if (state == SwordState.Held || state == SwordState.Returning) return;

        // Re-enable the rigidbody if it was made kinematic during grounding
        if (swordRb != null)
        {
            swordRb.isKinematic = false;
            swordRb.useGravity = false;
        }

        // Switch back to Flying so StartBoomerangReturn accepts it
        state = SwordState.Flying;
        StartBoomerangReturn(transform.position);
    }

    // =========================
    // Boomerang System
    // =========================
    private void UpdateFlying()
    {
        if (playerTransform == null) return;

        float distFromPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Auto-return if more than 10 units away or sword fell below the world
        if (distFromPlayer > 10f || transform.position.y < playerTransform.position.y - 15f)
        {
            if (transform.position.y < playerTransform.position.y - 15f)
                LastThrowResult = ThrowResult.FellInVoid;
            else if (LastThrowResult == ThrowResult.None)
                LastThrowResult = ThrowResult.AutoReturned;
            StartBoomerangReturn(transform.position);
        }
    }

    private void StartBoomerangReturn(Vector3 hitPoint)
    {
        if (state != SwordState.Flying) return;

        // Stop the landing coroutine
        StopAllCoroutines();

        // Change to returning state
        state = SwordState.Returning;
        isReturning = true;

        // Setup return parameters
        returnStartPosition = transform.position;
        returnStartTime = Time.time;
        
        // Calculate return duration based on distance
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        returnDuration = distanceToPlayer / returnSpeed;
        returnDuration = Mathf.Clamp(returnDuration, 0.5f, 3f); // Clamp between 0.5 and 3 seconds

        // Keep rigidbody active but disable gravity for smooth return
        if (swordRb != null)
        {
            swordRb.useGravity = false;
            swordRb.isKinematic = true; // Switch to kinematic for precise control
        }

        // Keep trail active during return
        if (swordTrail != null) swordTrail.emitting = true;

        Debug.Log($"Sword returning to player! Distance: {distanceToPlayer:F1}m, Duration: {returnDuration:F1}s");
    }

    private void UpdateReturning()
    {
        if (playerTransform == null) return;

        float elapsedTime = Time.time - returnStartTime;
        float progress = elapsedTime / returnDuration;

        if (progress >= 1f)
        {
            // Return completed - auto-collect
            AutoCollectSword();
            return;
        }

        // Calculate smooth arc path back to player
        Vector3 currentPlayerPos = playerTransform.position + Vector3.up * 1.5f; // Aim slightly above player
        Vector3 startPos = returnStartPosition;
        Vector3 endPos = currentPlayerPos;

        // Apply curve for smooth motion
        float curveProgress = returnCurve.Evaluate(progress);
        
        // Linear interpolation for base movement
        Vector3 linearPos = Vector3.Lerp(startPos, endPos, curveProgress);
        
        // Add arc height
        float arcOffset = Mathf.Sin(curveProgress * Mathf.PI) * returnArcHeight;
        Vector3 targetPos = linearPos + Vector3.up * arcOffset;

        // Move sword to target position
        transform.position = targetPos;

        // Rotate sword to face movement direction
        Vector3 moveDir = (targetPos - transform.position).normalized;
        if (moveDir.magnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(moveDir) * Quaternion.Euler(90f, 0f, 0f);
        }

        // Check for early auto-collection if close enough
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distToPlayer <= autoCollectDistance)
        {
            AutoCollectSword();
        }
    }

    private void AutoCollectSword()
    {
        Debug.Log("Auto-collecting sword!");
        
        // Stop any coroutines
        StopAllCoroutines();
        
        // Reset flags
        isReturning = false;

        // Remove RB
        var rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        swordRb = null;

        // Restore collisions with player
        IgnorePlayerCollision(false);

        // Colliders back to triggers for held melee
        SetAllSwordCollidersTrigger(true);

        // Hide pickup UI
        SetPickupUI(false);

        // Reattach to cached parent
        Transform container = heldParent != null ? heldParent : FindSwordContainer();
        if (container == null)
        {
            Debug.LogError("No held parent/swordcontainer found. Cannot reattach sword.");
            return;
        }

        transform.SetParent(container, false);
        transform.localPosition = heldLocalPos;
        transform.localRotation = heldLocalRot;
        transform.localScale = heldLocalScale;

        // Re-enable animator while held
        if (swordAnimator != null) swordAnimator.enabled = true;

        if (swordTrail != null) swordTrail.emitting = false;
        if (swordDamage != null) swordDamage.damage = baseDamage;

        state = SwordState.Held;
        hasDealtThrowDamage = false;
        ThrowCompletionCount++;

        Debug.Log($"Sword auto-collected! ThrowResult={LastThrowResult}, completions={ThrowCompletionCount}");
    }

    // =========================
    // Hit Effects System
    // =========================
    public void CreateMeleeHitEffect(Vector3 hitPosition, int damage)
    {
        CreateHitEffect(hitPosition, damage, true); // true = melee attack
    }

    private void CreateHitEffect(Vector3 hitPosition, int damage, bool isMelee)
    {
        // Check if this hit will kill the enemy (for extended effects)
        bool isKillingBlow = false;
        
        // Simple approach: check if we're dealing damage at hit position
        // The killing blow detection will be approximate but works reliably
        Collider hitCollider = Physics.OverlapSphere(hitPosition, 0.5f).Length > 0 ? 
            Physics.OverlapSphere(hitPosition, 0.5f)[0] : null;
        
        if (hitCollider != null)
        {
            var enemy = hitCollider.GetComponent<EnemyBase>() ?? hitCollider.GetComponentInParent<EnemyBase>();
            if (enemy != null && enemy.GetCurrentHealth() <= damage)
            {
                isKillingBlow = true;
            }
        }

        // Spawn VFX at hit location
        if (hitVFXPrefab != null)
        {
            GameObject vfx = Instantiate(hitVFXPrefab, hitPosition, Quaternion.identity);
            
            // Scale VFX based on damage and attack type
            float scale = isMelee ? 0.8f : 1.2f;
            if (isKillingBlow) scale *= 1.5f; // Bigger VFX for killing blows
            vfx.transform.localScale = Vector3.one * scale;
            
            // Extended duration for killing blows
            float vfxDuration = isKillingBlow ? 4f : 3f;
            Destroy(vfx, vfxDuration);
        }

        // Create damage text with extended duration for kills
        float textDuration = isKillingBlow ? 3f : damageTextDuration;
        CreateDamageText(hitPosition, damage, isMelee, textDuration);
        
        Debug.Log($"{(isMelee ? "Melee" : "Thrown")} hit effect created at {hitPosition} for {damage} damage{(isKillingBlow ? " (KILLING BLOW!)" : "")}");
    }

    private void CreateDamageText(Vector3 worldPosition, int damage, bool isMelee, float duration = -1f)
    {
        if (duration < 0) duration = damageTextDuration;
        
        // Create a world-space canvas for damage text
        GameObject damageTextObj = new GameObject("DamageText");
        damageTextObj.transform.position = worldPosition + Vector3.up * 0.5f; // Slightly above hit point

        // Create canvas
        Canvas canvas = damageTextObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 100;

        // Scale the canvas appropriately for world space
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2f, 1f);
        canvasRect.localScale = Vector3.one * 0.01f; // Small scale for world space

        // Create text object
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(damageTextObj.transform, false);

        // Setup text component
        UnityEngine.UI.Text text = textObj.AddComponent<UnityEngine.UI.Text>();
        text.text = damage.ToString();
        text.fontSize = 48;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = FontStyle.Bold;
        
        // Use provided font or fallback
        if (damageFont != null)
            text.font = damageFont;
        else
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Different colors for different attack types
        if (isMelee)
            text.color = new Color(1f, 0.8f, 0.2f, 1f); // Orange for melee
        else
            text.color = new Color(1f, 0.2f, 0.2f, 1f); // Red for thrown

        // Add outline for better visibility
        UnityEngine.UI.Outline outline = textObj.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, 2);

        // Setup text transform
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(200, 100);
        textRect.anchoredPosition = Vector2.zero;

        // Initial camera facing
        FaceCamera(damageTextObj);

        // Animate damage text with custom duration
        StartCoroutine(AnimateDamageText(damageTextObj, text, outline, duration));
    }

    private IEnumerator AnimateDamageText(GameObject damageTextObj, UnityEngine.UI.Text text, UnityEngine.UI.Outline outline, float duration)
    {
        float elapsed = 0f;
        Vector3 startPos = damageTextObj.transform.position;
        Vector3 endPos = startPos + Vector3.up * 1.5f; // Float upward

        Color startColor = text.color;
        Color startOutlineColor = outline.effectColor;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            
            // Float upward
            damageTextObj.transform.position = Vector3.Lerp(startPos, endPos, progress);
            
            // Scale animation (pop in then shrink)
            float scale;
            if (progress < 0.2f)
                scale = Mathf.Lerp(0.3f, 1.3f, progress / 0.2f); // Quick grow
            else if (progress < 0.4f)
                scale = Mathf.Lerp(1.3f, 1f, (progress - 0.2f) / 0.2f); // Settle
            else
                scale = Mathf.Lerp(1f, 0.8f, (progress - 0.4f) / 0.6f); // Gradual shrink

            damageTextObj.transform.localScale = Vector3.one * 0.01f * scale;
            
            // Fade out in last 40% of duration
            if (progress > 0.6f)
            {
                float fadeProgress = (progress - 0.6f) / 0.4f;
                Color fadeColor = startColor;
                fadeColor.a = 1f - fadeProgress;
                text.color = fadeColor;
                
                Color fadeOutlineColor = startOutlineColor;
                fadeOutlineColor.a = 1f - fadeProgress;
                outline.effectColor = fadeOutlineColor;
            }

            // Always face camera
            FaceCamera(damageTextObj);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(damageTextObj);
    }
    
    private void FaceCamera(GameObject obj)
    {
        if (Camera.main == null) return;
        
        Vector3 directionToCamera = Camera.main.transform.position - obj.transform.position;
        obj.transform.rotation = Quaternion.LookRotation(-directionToCamera);
    }

    // Pickup / Reattach
    // =========================
    private void PickupSword()
    {
        // Stop any settle coroutine
        StopAllCoroutines();

        // Remove RB
        var rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        swordRb = null;

        // Restore collisions with player
        IgnorePlayerCollision(false);

        // Colliders back to triggers for held melee
        SetAllSwordCollidersTrigger(true);

        // Hide pickup UI
        SetPickupUI(false);

        // Reattach to cached parent (or find swordcontainer)
        Transform container = heldParent != null ? heldParent : FindSwordContainer();
        if (container == null)
        {
            Debug.LogError("No held parent/swordcontainer found. Cannot reattach sword.");
            return;
        }

        transform.SetParent(container, false);
        transform.localPosition = heldLocalPos;
        transform.localRotation = heldLocalRot;
        transform.localScale = heldLocalScale;

        // Re-enable animator while held
        if (swordAnimator != null) swordAnimator.enabled = true;

        if (swordTrail != null) swordTrail.emitting = false;
        if (swordDamage != null) swordDamage.damage = baseDamage;

        state = SwordState.Held;
        ThrowCompletionCount++;
        Debug.Log("Sword picked up and re-attached!");
    }

    private Transform FindSwordContainer()
    {
        GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (player == null) return null;

        Transform t = player.transform.Find("swordcontainer");
        if (t != null) return t;

        foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            if (child.name == "swordcontainer")
                return child;

        return null;
    }



    // =========================
    // Collider / Collision helpers
    // =========================
    private void SetAllSwordCollidersTrigger(bool trigger)
    {
        swordColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in swordColliders)
        {
            if (col == null) continue;
            col.isTrigger = trigger;
        }
    }

    private void IgnorePlayerCollision(bool ignore)
    {
        if (playerColliders == null) return;

        swordColliders = GetComponentsInChildren<Collider>(true);
        foreach (var pc in playerColliders)
        {
            if (pc == null) continue;
            foreach (var sc in swordColliders)
            {
                if (sc == null) continue;
                Physics.IgnoreCollision(sc, pc, ignore);
            }
        }
    }

    // =========================
    // Trail
    // =========================
    private void CreateOrConfigureTrail()
    {
        swordTrail = GetComponentInChildren<TrailRenderer>();
        if (swordTrail == null)
        {
            GameObject trailObj = new GameObject("SwordTrail");
            trailObj.transform.SetParent(transform, false);
            trailObj.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            swordTrail = trailObj.AddComponent<TrailRenderer>();
        }

        swordTrail.time = trailTime;
        swordTrail.startWidth = trailStartWidth;
        swordTrail.endWidth = trailEndWidth;
        swordTrail.emitting = false;

        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        swordTrail.material = new Material(shader);

        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(trailStartColor, 0f), new GradientColorKey(trailEndColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        swordTrail.colorGradient = g;
    }

    // =========================
    // UI
    // =========================
    private void CreatePickupUI()
    {
        pickupUICanvas = new GameObject("SwordPickupUI");
        DontDestroyOnLoad(pickupUICanvas);

        var canvas = pickupUICanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        var scaler = pickupUICanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject textObj = new GameObject("PickupText");
        textObj.transform.SetParent(pickupUICanvas.transform, false);

        pickupText = textObj.AddComponent<Text>();
        pickupText.text = pickupPrompt;
        pickupText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pickupText.fontSize = 36;
        pickupText.color = Color.white;
        pickupText.alignment = TextAnchor.MiddleCenter;

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, 2);

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.4f);
        rt.anchorMax = new Vector2(0.5f, 0.4f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(700, 80);

        pickupUICanvas.SetActive(false);
    }

    private void SetPickupUI(bool show)
    {
        if (pickupUICanvas != null && pickupUICanvas.activeSelf != show)
            pickupUICanvas.SetActive(show);
    }

    private void CreateCrosshairUI()
    {
        crosshairCanvas = new GameObject("SwordCrosshair");
        DontDestroyOnLoad(crosshairCanvas);

        var canvas = crosshairCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = crosshairCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Create normal crosshair (small dot, always visible)
        CreateNormalCrosshair();
        
        // Create aiming crosshair (larger, enhanced, shown when aiming)
        CreateAimingCrosshair();

        crosshairCanvas.SetActive(true);
    }
    
    private void CreateNormalCrosshair()
    {
        normalCrosshair = new GameObject("NormalCrosshair");
        normalCrosshair.transform.SetParent(crosshairCanvas.transform, false);

        normalCrosshairImage = normalCrosshair.AddComponent<Image>();
        normalCrosshairImage.color = new Color(1f, 1f, 1f, 0.7f);

        // Create cross texture
        Texture2D crossTex = new Texture2D(16, 16);
        Color[] px = new Color[16 * 16];
        
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                // Create cross pattern - vertical and horizontal lines through center
                bool isVerticalLine = (x >= 7 && x <= 8) && (y >= 3 && y <= 12);
                bool isHorizontalLine = (y >= 7 && y <= 8) && (x >= 3 && x <= 12);
                
                px[y * 16 + x] = (isVerticalLine || isHorizontalLine) ? Color.white : Color.clear;
            }
        }
        
        crossTex.SetPixels(px);
        crossTex.Apply();
        normalCrosshairImage.sprite = Sprite.Create(crossTex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));

        RectTransform rt = normalCrosshair.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(32, 32);
    }
    
    private void CreateAimingCrosshair()
    {
        aimingCrosshair = new GameObject("AimingCrosshair");
        aimingCrosshair.transform.SetParent(crosshairCanvas.transform, false);

        aimingCrosshairImage = aimingCrosshair.AddComponent<Image>();
        aimingCrosshairImage.color = new Color(1f, 0.3f, 0.3f, 0.9f);

        // Create enhanced cross texture with targeting elements
        Texture2D aimTex = new Texture2D(64, 64);
        Color[] aimPx = new Color[64 * 64];
        Vector2 center = new Vector2(32, 32);
        
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);
                Color pixelColor = Color.clear;
                
                // Main cross lines (thicker)
                bool isMainVertical = (x >= 30 && x <= 33) && (y >= 8 && y <= 55);
                bool isMainHorizontal = (y >= 30 && y <= 33) && (x >= 8 && x <= 55);
                
                // Center intersection (small square)
                bool isCenterSquare = (x >= 30 && x <= 33) && (y >= 30 && y <= 33);
                
                // Corner brackets
                bool isCornerBracket = 
                    // Top-left corner
                    ((x >= 16 && x <= 18) && (y >= 16 && y <= 20)) ||
                    ((x >= 16 && x <= 20) && (y >= 16 && y <= 18)) ||
                    // Top-right corner
                    ((x >= 45 && x <= 47) && (y >= 16 && y <= 20)) ||
                    ((x >= 43 && x <= 47) && (y >= 16 && y <= 18)) ||
                    // Bottom-left corner
                    ((x >= 16 && x <= 18) && (y >= 43 && y <= 47)) ||
                    ((x >= 16 && x <= 20) && (y >= 45 && y <= 47)) ||
                    // Bottom-right corner
                    ((x >= 45 && x <= 47) && (y >= 43 && y <= 47)) ||
                    ((x >= 43 && x <= 47) && (y >= 45 && y <= 47));
                
                if (isCenterSquare)
                    pixelColor = Color.white;
                else if (isMainVertical || isMainHorizontal)
                    pixelColor = new Color(1f, 0.3f, 0.3f, 0.8f);
                else if (isCornerBracket)
                    pixelColor = new Color(1f, 0.5f, 0.5f, 0.6f);
                
                aimPx[y * 64 + x] = pixelColor;
            }
        }
        
        aimTex.SetPixels(aimPx);
        aimTex.Apply();
        aimingCrosshairImage.sprite = Sprite.Create(aimTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));

        RectTransform aimRt = aimingCrosshair.GetComponent<RectTransform>();
        aimRt.anchorMin = new Vector2(0.5f, 0.5f);
        aimRt.anchorMax = new Vector2(0.5f, 0.5f);
        aimRt.anchoredPosition = Vector2.zero;
        aimRt.sizeDelta = new Vector2(80, 80);
        
        aimingCrosshair.SetActive(false);
    }

    private void SetCrosshair(bool show)
    {
        if (crosshairCanvas != null && crosshairCanvas.activeSelf != show)
            crosshairCanvas.SetActive(show);
    }
    
    private void SetAimingMode(bool aiming)
    {
        if (normalCrosshair != null)
            normalCrosshair.SetActive(!aiming);
        if (aimingCrosshair != null)
            aimingCrosshair.SetActive(aiming);
    }
    
    private void UpdateCrosshairAccuracy()
    {
        if (!isAiming || aimingCrosshairImage == null) return;
        
        // Get current aim target information
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        bool hasValidTarget = Physics.Raycast(ray, out RaycastHit hit, aimMaxDistance, ~0, QueryTriggerInteraction.Ignore);
        
        // Change crosshair color based on target validity
        if (hasValidTarget)
        {
            float distance = Vector3.Distance(transform.position, hit.point);
            // Green for close targets, yellow for medium, red for far
            if (distance < aimMaxDistance * 0.3f)
                aimingCrosshairImage.color = new Color(0.3f, 1f, 0.3f, 0.9f); // Green
            else if (distance < aimMaxDistance * 0.6f)
                aimingCrosshairImage.color = new Color(1f, 1f, 0.3f, 0.9f); // Yellow
            else
                aimingCrosshairImage.color = new Color(1f, 0.6f, 0.3f, 0.9f); // Orange
        }
        else
        {
            aimingCrosshairImage.color = new Color(1f, 0.3f, 0.3f, 0.9f); // Red - no target
        }
    }

    // Animation Events (optional use)
    public void EnableTrail() { if (swordTrail != null && state == SwordState.Held) swordTrail.emitting = true; }
    public void DisableTrail() { if (swordTrail != null) swordTrail.emitting = false; }

    // Public API (for PlayerMovement & Tutorials)
    public bool IsHeld => state == SwordState.Held;
    /// <summary>Play the melee swing sound effect.</summary>
    public void PlayMeleeSFX()
    {
        if (sfxSource != null && meleeSFX != null)
            sfxSource.PlayOneShot(meleeSFX, 0.75f);
    }

    public bool IsThrown => state == SwordState.Flying;
    public bool IsGrounded => state == SwordState.Grounded;
    public bool IsReturning => state == SwordState.Returning;

    // ── Throw-result tracking (read by tutorials) ──
    public enum ThrowResult { None, HitEnemy, HitWall, FellInVoid, AutoReturned }
    public ThrowResult LastThrowResult { get; private set; } = ThrowResult.None;
    /// <summary>Incremented every time a throw completes (sword returns to hand).</summary>
    public int ThrowCompletionCount { get; private set; } = 0;

    /// <summary>
    /// Returns the current damage multiplier from the Strength Mask (or 1x if no mask active).
    /// </summary>
    private float GetDamageMultiplier()
    {
        return (abilityContext != null) ? abilityContext.damageMultiplier : 1f;
    }
}
