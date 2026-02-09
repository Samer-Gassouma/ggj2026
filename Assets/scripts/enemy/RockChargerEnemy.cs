using UnityEngine;

/// <summary>
/// Rock Charger — a flying sphere enemy that hovers idly, chases the player,
/// winds up, then lunges in a straight-line charge burst.
///
/// Requires on prefab:
///   • Rigidbody  (UseGravity OFF, Freeze Rotation X/Y/Z, mass ~3, drag ~2)
///   • SphereCollider
///   • A child "Visual" transform containing the mesh renderer
///   • HoverMotion component (optional but recommended)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RockChargerEnemy : EnemyBase
{
    // ───────── State Machine ─────────
    private enum State { Idle, Chase, WindUp, Charge, Cooldown }

    [Header("State (read-only)")]
    [SerializeField] private State currentState = State.Idle;

    // ───────── Movement ─────────
    [Header("Chase Settings")]
    [SerializeField] private float chaseSpeed = 8f;
    [SerializeField] private float chaseTurnSpeed = 5f;

    // ───────── Attack ─────────
    [Header("Attack")]
    [SerializeField] private float attackRadius = 5f;
    [SerializeField] private float windUpDuration = 0.45f;
    [SerializeField] private float chargeSpeed = 32f;
    [SerializeField] private float chargeDuration = 0.7f;
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private float knockbackForce = 45f;

    [Header("Cooldown")]
    [SerializeField] private float hitCooldown = 1.5f;
    private float stateTimer;

    // ───────── VFX ─────────
    [Header("VFX")]
    [SerializeField] private bool enableVFX = true;
    [SerializeField] private Color vfxColor = new Color(1f, 0.45f, 0.1f, 1f);    // fiery orange

    [Header("Trail")]
    [SerializeField] private bool enableTrail = true;
    [SerializeField] private Color trailColor = new Color(1f, 0.65f, 0.0f, 0.9f);
    [SerializeField] private float trailWidth = 1.0f;
    [SerializeField] private float trailTime = 0.5f;

    private ParticleSystem ambientEmbers;   // idle: tiny floating sparks
    private ParticleSystem chaseStreaks;     // chase: stretched speed lines
    private ParticleSystem gatherEffect;    // wind-up: particles pulling inward
    private TrailRenderer trail;

    // ───────── References ─────────
    private Rigidbody rb;
    private HoverMotion hoverMotion;

    // Wind-up / charge direction (locked at start of charge so it doesn't curve)
    private Vector3 chargeDir;
    // Wind-up pull-back position
    private Vector3 windUpOrigin;

    // Track whether we already hit the player during the current charge
    private bool hasHitThisCharge;

    // ────────────────────────────────────────────
    // Unity Lifecycle
    // ────────────────────────────────────────────

    protected override void Awake()
    {
        // Tank enemy - 10 sword hits (100 HP / 10 dmg)
        maxHealth = 100;
        
        base.Awake();

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        hoverMotion = GetComponent<HoverMotion>();

        if (enableTrail)
            CreateTrailRenderer();

        if (enableVFX)
        {
            ambientEmbers = RuntimeVFXFactory.CreateAmbientEmbers(transform, vfxColor);
            chaseStreaks  = RuntimeVFXFactory.CreateChaseStreaks(transform, vfxColor);
            gatherEffect  = RuntimeVFXFactory.CreateGatherEffect(transform, vfxColor);
        }
    }

    protected override void Update()
    {
        base.Update(); // detection
        if (isDead) return;

        switch (currentState)
        {
            case State.Idle:     TickIdle();     break;
            case State.Chase:    TickChase();    break;
            case State.WindUp:   TickWindUp();   break;
            case State.Charge:   TickCharge();   break;
            case State.Cooldown: TickCooldown(); break;
        }

        UpdateTrail();
        UpdateVFX();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        switch (currentState)
        {
            case State.Chase:  FixedChase();  break;
            case State.Charge: FixedCharge(); break;
        }

        // Height maintenance — gently correct toward spawn Y so it stays floating
        if (currentState != State.Charge)
        {
            float targetY = spawnPoint.y;
            float yError = targetY - transform.position.y;
            rb.AddForce(Vector3.up * yError * 5f, ForceMode.Acceleration);
        }
    }

    // ────────────────────────────────────────────
    // State: Idle
    // ────────────────────────────────────────────

    private void TickIdle()
    {
        SetHover(true);

        // Gently decelerate
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 3f);

        if (playerDetected)
            TransitionTo(State.Chase);
    }

    // ────────────────────────────────────────────
    // State: Chase
    // ────────────────────────────────────────────

    private void TickChase()
    {
        if (!playerDetected)
        {
            TransitionTo(State.Idle);
            return;
        }

        if (DistanceToPlayer() <= attackRadius)
        {
            TransitionTo(State.WindUp);
            return;
        }

        FacePlayer();
    }

    private void FixedChase()
    {
        if (playerTransform == null) return;

        Vector3 toPlayer = (playerTransform.position - transform.position);
        Vector3 desiredVel = toPlayer.normalized * chaseSpeed;

        // Steer directly toward player — cancel perpendicular drift to prevent orbiting
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, desiredVel, Time.fixedDeltaTime * chaseTurnSpeed);
    }

    // ────────────────────────────────────────────
    // State: WindUp (brief pull-back before charge)
    // ────────────────────────────────────────────

    private void TickWindUp()
    {
        stateTimer -= Time.deltaTime;

        // Pull back slightly (opposite to charge direction) for visual anticipation
        float t = 1f - (stateTimer / windUpDuration); // 0→1
        Vector3 pullBack = windUpOrigin - chargeDir * 0.8f;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, (pullBack - transform.position) * 4f, Time.deltaTime * 8f);

        // Shake / vibrate the visual slightly during wind-up
        if (hoverMotion != null && hoverMotion.visualTransform != null)
        {
            float shake = 0.06f;
            hoverMotion.visualTransform.localPosition += new Vector3(
                Random.Range(-shake, shake), Random.Range(-shake, shake), Random.Range(-shake, shake));
        }

        if (stateTimer <= 0f)
            TransitionTo(State.Charge);
    }

    // ────────────────────────────────────────────
    // State: Charge (straight-line lunge)
    // ────────────────────────────────────────────

    private void TickCharge()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
            TransitionTo(State.Cooldown);
    }

    private void FixedCharge()
    {
        // Drive in a locked straight line — no steering, no orbiting
        rb.linearVelocity = chargeDir * chargeSpeed;
    }

    // ────────────────────────────────────────────
    // State: Cooldown (recover after charge / hit)
    // ────────────────────────────────────────────

    private void TickCooldown()
    {
        stateTimer -= Time.deltaTime;

        // Dampen velocity
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 6f);

        if (stateTimer <= 0f)
            TransitionTo(playerDetected ? State.Chase : State.Idle);
    }

    // ────────────────────────────────────────────
    // State Transitions
    // ────────────────────────────────────────────

    private void TransitionTo(State next)
    {
        currentState = next;

        switch (next)
        {
            case State.Idle:
                SetHover(true);
                rb.linearVelocity = Vector3.zero;
                break;

            case State.Chase:
                SetHover(true);
                break;

            case State.WindUp:
                SetHover(false);
                hasHitThisCharge = false;
                stateTimer = windUpDuration;
                windUpOrigin = transform.position;
                // Lock charge direction NOW so the lunge goes straight
                if (playerTransform != null)
                    chargeDir = (playerTransform.position - transform.position).normalized;
                else
                    chargeDir = transform.forward;
                FaceDirection(chargeDir);
                rb.linearVelocity = Vector3.zero;
                break;

            case State.Charge:
                SetHover(false);
                stateTimer = chargeDuration;
                // Reset visual shake
                if (hoverMotion != null) hoverMotion.ResetVisual();
                break;

            case State.Cooldown:
                SetHover(false);
                stateTimer = hitCooldown;
                break;
        }
    }

    // ────────────────────────────────────────────
    // Collision – damage + knockback + VFX
    // ────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision) { HandlePlayerHit(collision); }
    private void OnCollisionStay(Collision collision)  { HandlePlayerHit(collision); }

    private void HandlePlayerHit(Collision collision)
    {
        if (isDead) return;
        if (currentState != State.Charge && currentState != State.Chase) return;
        if (hasHitThisCharge) return;

        if (!IsPlayer(collision.gameObject)) return;

        hasHitThisCharge = true;

        // ── Damage ──
        PlayerHealth ph = collision.gameObject.GetComponent<PlayerHealth>();
        if (ph != null) ph.TakeDamage(attackDamage);

        // ── Knockback (uses the new PlayerMovement.ApplyKnockback) ──
        ApplyKnockback(collision.gameObject);

        // ── Impact VFX ──
        if (enableVFX)
        {
            Vector3 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.transform.position;
            Vector3 normal = collision.contactCount > 0
                ? collision.GetContact(0).normal
                : (collision.transform.position - transform.position).normalized;
            RuntimeVFXFactory.SpawnImpactBurst(point, vfxColor, normal);
        }

        // Bounce enemy backward
        rb.linearVelocity = -chargeDir * chargeSpeed * 0.3f;

        TransitionTo(State.Cooldown);
    }

    private void ApplyKnockback(GameObject player)
    {
        Vector3 dir = (player.transform.position - transform.position);
        dir.y = 0f;
        dir = dir.normalized;

        // Use the new PlayerMovement knockback system (works with CharacterController)
        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.ApplyKnockback(dir, knockbackForce);
            return;
        }

        // Fallback: try Rigidbody
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            dir.y = 0.3f;
            playerRb.AddForce(dir.normalized * knockbackForce, ForceMode.Impulse);
        }
    }

    private bool IsPlayer(GameObject go)
    {
        if (go.CompareTag("Player")) return true;
        if (go.GetComponent<PlayerHealth>() != null) return true;
        if (go.GetComponent<PlayerMovement>() != null) return true;
        return false;
    }

    // ────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────

    private void FacePlayer()
    {
        if (playerTransform == null) return;
        FaceDirection(playerTransform.position - transform.position);
    }

    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 12f);
    }

    private void SetHover(bool on)
    {
        if (hoverMotion != null) hoverMotion.active = on;
    }

    // ───────── Trail Renderer ─────────

    private void CreateTrailRenderer()
    {
        // Attach trail to visual child if available, otherwise to root
        Transform trailParent = (hoverMotion != null && hoverMotion.visualTransform != null)
            ? hoverMotion.visualTransform
            : transform;

        GameObject trailGo = new GameObject("Trail_VFX");
        trailGo.transform.SetParent(trailParent, false);
        trailGo.transform.localPosition = Vector3.back * 0.5f;

        trail = trailGo.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailWidth;
        trail.endWidth = 0.05f;
        trail.minVertexDistance = 0.08f;
        trail.numCornerVertices = 6;
        trail.numCapVertices = 6;
        trail.receiveShadows = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.autodestruct = false;

        // Additive material for glowing energy trail
        trail.material = RuntimeVFXFactory.GetAdditiveMaterial();

        // Gradient: white-hot core → golden → orange → transparent
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(trailColor, 0.2f),
                new GradientColorKey(trailColor * 0.7f, 0.6f),
                new GradientColorKey(trailColor * 0.3f, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.15f),
                new GradientAlphaKey(0.4f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = grad;

        // Width curve: thick start, taper off
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, 1f);
        widthCurve.AddKey(0.3f, 0.7f);
        widthCurve.AddKey(1f, 0f);
        trail.widthCurve = widthCurve;

        trail.emitting = false;
    }

    private void UpdateTrail()
    {
        if (trail == null) return;

        bool shouldEmit = currentState == State.Chase || currentState == State.Charge || currentState == State.WindUp;

        // Much thicker & longer trail during charge for DBZ energy effect
        if (currentState == State.Charge)
        {
            trail.time = trailTime * 3f;
            trail.startWidth = trailWidth * 2.5f;
        }
        else if (currentState == State.WindUp)
        {
            trail.time = trailTime * 0.5f;
            trail.startWidth = trailWidth * 1.5f;
        }
        else
        {
            trail.time = trailTime;
            trail.startWidth = trailWidth;
        }

        trail.emitting = shouldEmit;
    }

    // ───────── VFX State Management ─────────

    private void UpdateVFX()
    {
        if (!enableVFX) return;

        // Ambient embers: idle & cooldown only
        SetPS(ambientEmbers, currentState == State.Idle || currentState == State.Cooldown);

        // Chase streaks: chase & charge
        SetPS(chaseStreaks, currentState == State.Chase || currentState == State.Charge);

        // Gather effect: wind-up only
        SetPS(gatherEffect, currentState == State.WindUp);
    }

    private void SetPS(ParticleSystem ps, bool shouldPlay)
    {
        if (ps == null) return;
        if (shouldPlay && !ps.isPlaying) ps.Play();
        else if (!shouldPlay && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ───────── Death Override ─────────

    protected override void Die()
    {
        // Clean up VFX
        DestroyPS(ambientEmbers);
        DestroyPS(chaseStreaks);
        DestroyPS(gatherEffect);

        if (trail != null)
        {
            trail.emitting = false;
            Destroy(trail.gameObject, trail.time + 0.1f);
        }

        // Death impact
        if (enableVFX)
            RuntimeVFXFactory.SpawnImpactBurst(transform.position, vfxColor, Vector3.up);

        if (hoverMotion != null) hoverMotion.ResetVisual();

        base.Die();
    }

    private void DestroyPS(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(ps.gameObject, 0.5f);
    }

    // ───────── Gizmos ─────────

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}
