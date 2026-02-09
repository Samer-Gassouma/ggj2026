using UnityEngine;

public class PlayerAbilityContext : MonoBehaviour
{
    // ─── Movement modifiers ──────────────────────────────────
    [Min(0f)] public float speedMultiplier = 1f;

    // ─── Jump modifiers ──────────────────────────────────────
    [Min(0)] public int maxAirJumps = 0;

    // ─── Strength Mask ───────────────────────────────────────
    [Header("Strength Mask")]
    public float swordScaleMultiplier = 1f;
    public float damageMultiplier = 1f;
    public float attackKnockbackForce = 0f;       // extra push force on hit
    public bool shockwaveEnabled = false;          // superpower
    public bool canBreakDestructibles = false;     // superpower
    public float shockwaveRadius = 6f;
    public float shockwaveDamage = 25f;
    public float shockwaveCooldown = 4f;

    // ─── Dash Mask ───────────────────────────────────────────
    [Header("Dash Mask")]
    public bool dashEnabled = false;
    public bool doubleJumpUnlocked = false;        // unlocked after completing platforming section
    public bool dashPermanentlyUnlocked = false;   // unlocked by DashUnlockPickup — persists across mask swaps
    public bool airDashChainEnabled = false;       // superpower
    public bool timeSlowDuringDash = false;        // superpower
    public bool phaseEnabled = false;              // superpower

    // ─── Vision Mask ─────────────────────────────────────────
    [Header("Vision Mask")]
    public bool visionEnabled = false;
    public bool canInteractHidden = false;         // superpower
    public bool canGrabUnseen = false;             // superpower
    public bool altPuzzleSolutions = false;        // superpower

    // ─── Reset to defaults when switching masks ──────────────
    public void ResetContext()
    {
        speedMultiplier = 1f;
        maxAirJumps = 0;

        // Strength
        swordScaleMultiplier = 1f;
        damageMultiplier = 1f;
        attackKnockbackForce = 0f;
        shockwaveEnabled = false;
        canBreakDestructibles = false;

        // Dash — keep doubleJumpUnlocked and dashPermanentlyUnlocked persistent
        dashEnabled = dashPermanentlyUnlocked; // stays on if unlocked by pickup
        airDashChainEnabled = false;
        timeSlowDuringDash = false;
        phaseEnabled = false;

        // Vision
        visionEnabled = false;
        canInteractHidden = false;
        canGrabUnseen = false;
        altPuzzleSolutions = false;
    }
}
