using UnityEngine;

[CreateAssetMenu(menuName = "Masks/Dash Mask", fileName = "DashMask")]
public class DashMaskAbility : MaskAbility
{
    [Header("Primary — Dash + Double Jump")]
    public float dashDistance = 8f;
    public float dashSpeed = 15f;
    public float dashCooldown = 1f;
    public float explosionForce = 1.5f;
    public float explosionRadius = 0.5f;
    public float upwardsModifier = 1f;
    public float dashFadeTime = 0.2f;
    public float jumpBoost = 10f;
    [Min(0)] public int extraJumps = 1;

    [Header("Superpower — Momentum Mastery")]
    public int maxAirDashChains = 2;
    public float timeSlowScale = 0.3f;
    public float timeSlowDuration = 0.5f;

    public override void Activate(PlayerAbilityContext context)
    {
        // Primary: dash always available (also enabled permanently by pickup)
        context.dashEnabled = true;

        // Double jump only if unlocked
        if (context.doubleJumpUnlocked)
            context.maxAirJumps = extraJumps;
        else
            context.maxAirJumps = 0;

        // Screen overlay — ice blue vignette
        MaskScreenOverlay.Show(MaskOverlayType.Dash);

        var dashHandler = context.GetComponent<DashAbilityHandler>();
        if (dashHandler == null)
            dashHandler = context.gameObject.AddComponent<DashAbilityHandler>();
        dashHandler.Setup(this);
        dashHandler.enabled = true;

        if (superpowerUnlocked)
            ActivateSuperpower(context);
    }

    public override void Deactivate(PlayerAbilityContext context)
    {
        // Keep dash if permanently unlocked by pickup
        context.dashEnabled = context.dashPermanentlyUnlocked;
        context.maxAirJumps = 0;
        context.airDashChainEnabled = false;
        context.timeSlowDuringDash = false;
        context.phaseEnabled = false;

        // Remove screen overlay
        MaskScreenOverlay.Hide();

        var dashHandler = context.GetComponent<DashAbilityHandler>();
        if (dashHandler != null)
            dashHandler.enabled = false;
    }

    protected override void ActivateSuperpower(PlayerAbilityContext context)
    {
        context.airDashChainEnabled = true;
        context.timeSlowDuringDash = true;
        context.phaseEnabled = true;
    }
}

public class DashAbilityHandler : MonoBehaviour
{
    private DashMaskAbility dashData;
    private CharacterController controller;
    private Camera cam;
    private bool isDashing = false;
    private float dashCooldownTimer = 0f;
    private Vector3 dashDirection;
    private float dashDistanceLeft;
    private float dashFadeTimer = 0f;
    private bool isFading = false;
    private PlayerAbilityContext abilityContext;
    private PlayerMovement playerMovement;

    // Superpower: air dash chain tracking
    private int airDashesUsed = 0;
    private float timeSlowTimer = 0f;
    private bool isTimeSlowed = false;

    // Phase through barriers
    private int originalLayer;
    private bool isPhasing = false;

    public void Setup(DashMaskAbility data)
    {
        dashData = data;
        controller = GetComponent<CharacterController>();
        abilityContext = GetComponent<PlayerAbilityContext>();
        playerMovement = GetComponent<PlayerMovement>();
        if (controller == null)
            Debug.LogWarning("DashAbilityHandler: CharacterController not found.");
        cam = GetComponentInChildren<Camera>();
        if (cam == null)
            cam = Camera.main;
    }

    private void OnEnable()
    {
        isDashing = false;
        isFading = false;
        dashCooldownTimer = 0f;
        airDashesUsed = 0;
        isTimeSlowed = false;
    }

    private void OnDisable()
    {
        // Restore time if disabled mid-slow
        if (isTimeSlowed)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            isTimeSlowed = false;
        }

        if (isPhasing)
        {
            gameObject.layer = originalLayer;
            isPhasing = false;
        }
    }

    private void Update()
    {
        if (dashData == null || controller == null || cam == null) return;

        dashCooldownTimer -= Time.unscaledDeltaTime;

        // Reset air dashes when grounded
        if (controller.isGrounded)
        {
            airDashesUsed = 0;
        }

        // Time-slow timer management (superpower)
        if (isTimeSlowed)
        {
            timeSlowTimer -= Time.unscaledDeltaTime;
            if (timeSlowTimer <= 0f)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                isTimeSlowed = false;
            }
        }

        // Dash input: scroll wheel up
        bool wantsDash = !isDashing && !isFading && dashCooldownTimer <= 0f && Input.mouseScrollDelta.y > 0f;

        if (wantsDash)
        {
            // Grounded dash — always allowed
            if (controller.isGrounded)
            {
                StartDash();
            }
            // Air dash — only if superpower unlocked
            else if (abilityContext != null && abilityContext.airDashChainEnabled)
            {
                if (airDashesUsed < dashData.maxAirDashChains)
                {
                    StartDash();
                    airDashesUsed++;
                }
            }
            // Air dash attempted but not unlocked — show feedback (no dash)
            else
            {
                // Don't show feedback spam, just silently block
            }
        }

        if (isDashing)
        {
            float move = dashData.dashSpeed * Time.unscaledDeltaTime;
            Vector3 moveVec = dashDirection * Mathf.Min(move, dashDistanceLeft);

            if (Input.GetButtonDown("Jump") && playerMovement != null && controller.isGrounded)
            {
                isDashing = false;
                isFading = false;
                dashCooldownTimer = dashData.dashCooldown;

                playerMovement.SetJumpBoost(dashData.jumpBoost);
                playerMovement.ResetAirJumps();
            }
            else
            {
                controller.Move(moveVec);
                dashDistanceLeft -= moveVec.magnitude;

                float dashJumpThreshold = dashData.dashDistance * 0.2f;
                if (dashDistanceLeft <= dashJumpThreshold && playerMovement != null)
                {
                    playerMovement.ResetAirJumps();
                }

                if (dashDistanceLeft <= 0f)
                {
                    isDashing = false;
                    isFading = true;
                    dashFadeTimer = dashData.dashFadeTime;

                    // End phase
                    if (isPhasing)
                    {
                        gameObject.layer = originalLayer;
                        isPhasing = false;
                    }
                }
            }
        }
        else if (isFading)
        {
            dashFadeTimer -= Time.unscaledDeltaTime;
            float fadeT = Mathf.Clamp01(dashFadeTimer / dashData.dashFadeTime);
            float fadeSpeed = dashData.dashSpeed * fadeT * Time.unscaledDeltaTime;
            controller.Move(dashDirection * fadeSpeed);

            if (dashFadeTimer <= 0f)
            {
                isFading = false;
                dashCooldownTimer = dashData.dashCooldown;
            }
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if ((isDashing || isFading) && hit.collider.CompareTag("Explodable"))
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddExplosionForce(
                    dashData.explosionForce,
                    hit.point,
                    dashData.explosionRadius,
                    dashData.upwardsModifier,
                    ForceMode.Impulse
                );
            }
        }
    }

    private void StartDash()
    {
        isDashing = true;
        isFading = false;
        dashDistanceLeft = dashData.dashDistance;
        dashDirection = cam.transform.forward;
        dashDirection.y = 0f;
        dashDirection.Normalize();

        // Superpower: time-slow during dash
        if (abilityContext != null && abilityContext.timeSlowDuringDash)
        {
            Time.timeScale = dashData.timeSlowScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            isTimeSlowed = true;
            timeSlowTimer = dashData.timeSlowDuration;
        }

        // Superpower: phase through barriers
        if (abilityContext != null && abilityContext.phaseEnabled)
        {
            originalLayer = gameObject.layer;
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // pass through "Phaseable" tagged barriers
            isPhasing = true;
        }
    }
}



