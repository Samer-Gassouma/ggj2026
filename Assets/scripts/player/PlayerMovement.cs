using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private bool isCrouching = false;

    [Header("Movement Settings")]
    public float walkSpeed;
    public float runSpeed;
    public float jumpPower;
    public float gravity;

    [Header("Look Settings")]
    public Camera playerCamera;
    public float lookSpeed = 2f;
    public float lookXLimit = 90f;

    [Header("Crouch Settings")]
    public float defaultHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeed = 3f;

    [Header("Combat")]
    [SerializeField] private Animator swordAnimator; // assign in inspector

    [SerializeField] private PlayerAbilityContext abilityContext;

    // Health is managed by PlayerHealth component on the same GameObject

    private Vector3 moveDirection;
    private float rotationX;
    private CharacterController controller;

    private bool canMove = true;
    private bool isRunning;
    private bool wasGrounded = true;

    //movement save defaults
    private float currentJumpPower;
    private float currentRunSpeed;
    private float currentwalkSpeed;

    // Track remaining air jumps provided by abilities
    private int remainingAirJumps;

    private bool isDead = false;

    [Header("Fall Death")]
    [SerializeField] private float fallDeathY = -20f; // Y position below which player dies

    // Combo attack state
    private int swordComboStep = 0;
    private float lastAttackTime = 0f;
    [SerializeField] private float comboResetTime = 0.7f; // Max time between clicks for combo

    // ── External knockback (used by enemies) ──
    private Vector3 knockbackVelocity;
    [Header("Knockback")]
    [SerializeField] private float knockbackDecay = 3.5f;

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        currentJumpPower = jumpPower;
        currentRunSpeed = runSpeed;
        currentwalkSpeed = walkSpeed;

        // Ensure we have a context component
        if (abilityContext == null)
            abilityContext = GetComponent<PlayerAbilityContext>();
        if (abilityContext == null)
            abilityContext = gameObject.AddComponent<PlayerAbilityContext>();

        remainingAirJumps = abilityContext.maxAirJumps;
    }

    private void Update()
    {
        // ── Fall death detection ──
        if (!isDead && transform.position.y < fallDeathY)
        {
            isDead = true;
            PlayerHealth ph = PlayerHealth.Instance;
            if (ph != null)
            {
                ph.InstantDeath();
            }
            return;
        }

        HandleMovement();
        HandleJump();
        ApplyGravity();
        HandleCrouch();
        HandleMouseLook();
        HandleLanding();

        HandleSwordAttack();

        //make it fly when knockbacked, but also decay the knockback over time so it doesn't last forever
        if (knockbackVelocity.magnitude > 0.1f)
        {
            controller.Move(knockbackVelocity * Time.deltaTime);
            // Decay knockback velocity
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);


        }

        controller.Move(moveDirection * Time.deltaTime);
    }

    public void SetCanMove(bool enabled)
    {
        canMove = enabled;
        if (!canMove)
        {
            // Stop horizontal movement immediately while menu is open
            moveDirection.x = 0f;
            moveDirection.z = 0f;
        }
    }

    private void HandleSwordAttack()
    {
        // Auto-find the sword animator if the Inspector reference is missing or got destroyed
        if (swordAnimator == null)
        {
            SwordController sc = FindAnyObjectByType<SwordController>(FindObjectsInactive.Include);
            if (sc != null) swordAnimator = sc.GetComponent<Animator>();
            if (swordAnimator == null) return; // genuinely no sword in scene
        }
        
        // Cache sword controller reference (find once, reuse)
        SwordController swordCtrl = SwordController.ActiveInstance;
        if (swordCtrl == null)
            swordCtrl = FindAnyObjectByType<SwordController>(FindObjectsInactive.Include);

        // Can't melee while sword is mid-throw or on the ground
        if (swordCtrl != null && !swordCtrl.IsHeld) return;

        if (Input.GetMouseButtonDown(0))
        {
            // No mana = no attack
            PlayerMana mana = PlayerMana.Instance;
            if (mana != null && !mana.TrySpendMelee()) return;

            float timeSinceLast = Time.time - lastAttackTime;
            lastAttackTime = Time.time;

            if (timeSinceLast > comboResetTime)
            {
                swordComboStep = 0; // Reset combo if too slow
            }

            if (swordComboStep == 0)
            {
                swordAnimator.SetTrigger("Attack");
                swordComboStep = 1;
                if (swordCtrl != null) swordCtrl.PlayMeleeSFX();
            }
            else if (swordComboStep == 1)
            {
                swordAnimator.SetTrigger("Attack2");
                swordComboStep = 0; // Reset combo after second attack
                if (swordCtrl != null) swordCtrl.PlayMeleeSFX();
            }
        }
    }

    // ---------------------- MOVEMENT ----------------------
    void HandleMovement()
    {
        if (!canMove)
        {
            // Keep gravity/y while frozen; horizontal stays zero
            return;
        }

        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        isRunning = Input.GetKey(KeyCode.LeftShift);

        float baseSpeed = isRunning ? runSpeed : walkSpeed;
        float speed = baseSpeed * (abilityContext != null ? abilityContext.speedMultiplier : 1f);

        float curX = speed * Input.GetAxis("Vertical");
        float curY = speed * Input.GetAxis("Horizontal");

        // Drain mana while sprinting and actually moving
        bool isMoving = Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f || Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f;
        if (isRunning && isMoving)
        {
            PlayerMana mana = PlayerMana.Instance;
            if (mana != null)
            {
                if (mana.currentMana > 0)
                    mana.DrainSprint();
                else
                    isRunning = false; // Out of mana → fall back to walking
            }
        }

        // Recalculate speed if we fell back to walking
        if (!isRunning && Input.GetKey(KeyCode.LeftShift))
        {
            speed = walkSpeed * (abilityContext != null ? abilityContext.speedMultiplier : 1f);
            curX = speed * Input.GetAxis("Vertical");
            curY = speed * Input.GetAxis("Horizontal");
        }

        moveDirection.x = (forward * curX + right * curY).x;
        moveDirection.z = (forward * curX + right * curY).z;
    }

    // ---------------------- JUMP ----------------------
    void HandleJump()
    {
        if (!canMove) return;

        if (controller.isGrounded)
        {
            // Ensure we stay grounded (prevents "sticky" jump)
            if (moveDirection.y < 0f)
                moveDirection.y = -2f;

            if (Input.GetButtonDown("Jump"))
            {
                moveDirection.y = jumpPower;
                // Reset air jumps when jumping from ground
                remainingAirJumps = abilityContext != null ? abilityContext.maxAirJumps : 0;
            }
        }
        else if (Input.GetButtonDown("Jump") && remainingAirJumps > 0)
        {
            moveDirection.y = jumpPower;
            remainingAirJumps--;
        }
    }

    // ---------------------- GRAVITY ----------------------
    void ApplyGravity()
    {
        // Gravity always applies if not grounded
        if (!controller.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }
    }

    // ---------------------- CROUCH ----------------------
    void HandleCrouch()
    {
        if (Input.GetKey(KeyCode.LeftControl))
        {
            if (!isCrouching)
            {
                isCrouching = true;
                controller.height = crouchHeight;
                walkSpeed = crouchSpeed;
                runSpeed = crouchSpeed;
            }
        }
        else
        {
            if (isCrouching)
            {
                isCrouching = false;
                controller.height = defaultHeight;
                walkSpeed = currentwalkSpeed;
                runSpeed = currentRunSpeed;
            }
        }
    }

    // ---------------------- MOUSE LOOK ----------------------
    void HandleMouseLook()
    {
        if (!canMove) return;

        rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);

        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
    }

    // ---------------------- LANDING ----------------------
    void HandleLanding()
    {
        if (!wasGrounded && controller.isGrounded)
        {
            // Restore available air jumps on landing
            remainingAirJumps = abilityContext != null ? abilityContext.maxAirJumps : 0;
        }

        wasGrounded = controller.isGrounded;
    }

    // ---------------------- ENEMY STOMP (OPTIONAL) ----------------------
    // private void OnTriggerEnter(Collider other)
    // {
    //     if (other.CompareTag("EnemyHead"))
    //     {
    //         Destroy(other.transform.parent.gameObject);

    //         moveDirection.y = jumpPower * 1.5f;
    //     }
    // }

    /// <summary>
    /// Route damage through PlayerHealth component.
    /// Kept for backwards-compat so enemies calling PlayerMovement.TakeDamage still work.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (isDead) return;
        var ph = GetComponent<PlayerHealth>();
        if (ph != null) ph.TakeDamage(amount);
    }

    private void Die()
    {
        isDead = true;
        canMove = false;
        Debug.Log("Player died!");
    }

    /// <summary>
    /// Call from enemies / hazards to apply a knockback impulse.
    /// Direction should already be normalized.
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (isDead) return;

        knockbackVelocity = direction * force;
        // Big upward launch so the player really feels it
        knockbackVelocity.y = Mathf.Max(knockbackVelocity.y, force * 0.45f);
        // Also override current move direction Y to launch upward
        moveDirection.y = force * 0.35f;
    }

    public void ResetAirJumps()
    {
        remainingAirJumps = abilityContext != null ? abilityContext.maxAirJumps : 0;
    }

    public void SetJumpBoost(float boost)
    {
        moveDirection.y = boost;
    }
}
