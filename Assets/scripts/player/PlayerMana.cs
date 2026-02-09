using UnityEngine;

/// <summary>
/// Manages the player's mana pool. Works exactly like PlayerHealth but for mana.
/// 
/// Mask rules:
///   • Equipping any mask costs <see cref="maskEquipCost"/> (20) mana up-front.
///   • After <see cref="sustainGracePeriod"/> (10 s) of wearing a mask, the mask
///     drains <see cref="sustainDrainAmount"/> (4) mana every <see cref="sustainDrainInterval"/> (2 s).
///   • If mana reaches 0 while wearing a mask the mask is forcibly removed.
///   • Mana regenerates <see cref="regenPerSecond"/> per second, but only after
///     <see cref="regenDelay"/> (20 s) of NOT wearing any mask.
///
/// Auto-setup: Finds the mana bar (healthbar) tagged "ManaBar" or assigned in inspector.
/// Also auto-hooks into PlayerMaskController on the same GameObject.
/// </summary>
public class PlayerMana : MonoBehaviour
{
    // ─── Pool ────────────────────────────────────────────────
    [Header("Mana Pool")]
    public int maxMana = 100;
    [HideInInspector] public int currentMana;

    // ─── Mask costs ──────────────────────────────────────────
    [Header("Mask Costs")]
    [Tooltip("Mana consumed instantly when a mask is equipped.")]
    public int maskEquipCost = 20;

    [Tooltip("Seconds after equipping before the sustain drain begins.")]
    public float sustainGracePeriod = 10f;

    [Tooltip("Mana drained per tick while sustaining a mask.")]
    public int sustainDrainAmount = 4;

    [Tooltip("Seconds between sustain drain ticks.")]
    public float sustainDrainInterval = 2f;

    // ─── Regen ───────────────────────────────────────────────
    [Header("Regen")]
    [Tooltip("Seconds after removing ALL masks before regen starts.")]
    public float regenDelay = 20f;

    [Tooltip("Mana regenerated per second once regen kicks in.")]
    public float regenPerSecond = 5f;

    // ─── Combat & Sprint Costs ───────────────────────────────
    [Header("Combat & Sprint Costs")]
    [Tooltip("Mana consumed per sword melee swing.")]
    public int meleeSwingCost = 8;

    [Tooltip("Mana consumed when throwing the sword.")]
    public int throwCost = 15;

    [Tooltip("Mana drained per second while sprinting.")]
    public float sprintDrainPerSecond = 6f;

    // ─── UI ──────────────────────────────────────────────────
    [Header("UI")]
    public healthbar manaBar;

    // ─── Singleton shortcut ──────────────────────────────────
    public static PlayerMana Instance { get; private set; }

    /// <summary>0-1 normalised mana for UI effects.</summary>
    public float ManaPercent => (float)currentMana / maxMana;

    /// <summary>True while a mask is actively worn.</summary>
    public bool IsMaskActive => _maskActive;

    // ─── Internal timers ─────────────────────────────────────
    private bool _maskActive;
    private float _maskEquipTime;       // Time.time when mask was put on
    private float _lastDrainTick;       // Time.time of last sustain tick
    private float _maskRemovedTime;     // Time.time when mask was taken off
    private bool _regenActive;
    private float _regenAccumulator;

    private PlayerMaskController _maskController;

    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        currentMana = maxMana;

        if (manaBar != null)
        {
            manaBar.SetMaxHealth(maxMana);

            // Force the mana bar gradient to blue
            Gradient blueGradient = new Gradient();
            blueGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.1f, 0.2f, 0.8f), 0f),   // dark blue (low mana)
                    new GradientColorKey(new Color(0.2f, 0.6f, 1f), 1f)      // bright blue (full mana)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
            manaBar.gradient = blueGradient;
            if (manaBar.fill != null)
                manaBar.fill.color = blueGradient.Evaluate(1f);
        }

        _maskController = GetComponent<PlayerMaskController>();
        _maskRemovedTime = -regenDelay; // allow instant regen at start
    }

    private void Update()
    {
        HandleSustainDrain();
        HandleRegen();
    }

    // ─── PUBLIC API ──────────────────────────────────────────

    /// <summary>
    /// Try to spend mana to equip a mask.
    /// Returns true if the player had enough mana and the cost was deducted.
    /// </summary>
    public bool TrySpendEquipCost()
    {
        if (currentMana < maskEquipCost)
            return false;

        SpendMana(maskEquipCost);
        return true;
    }

    /// <summary>
    /// Call when a mask is equipped (after mana was spent).
    /// Starts the sustain timer.
    /// </summary>
    public void OnMaskEquipped()
    {
        _maskActive = true;
        _maskEquipTime = Time.time;
        _lastDrainTick = Time.time;
        _regenActive = false;
    }

    /// <summary>
    /// Call when the mask is removed (voluntarily or forced).
    /// Stops sustain drain and starts the regen cooldown.
    /// </summary>
    public void OnMaskRemoved()
    {
        _maskActive = false;
        _maskRemovedTime = Time.time;
        _regenActive = false;
    }

    /// <summary>Spend an arbitrary amount of mana (clamped to 0).</summary>
    public void SpendMana(int amount)
    {
        currentMana = Mathf.Max(0, currentMana - amount);
        RefreshUI();
    }

    /// <summary>Restore an arbitrary amount of mana (clamped to max).</summary>
    public void RestoreMana(int amount)
    {
        currentMana = Mathf.Min(maxMana, currentMana + amount);
        RefreshUI();
    }

    /// <summary>Check if the player has at least this much mana.</summary>
    public bool HasMana(int amount) => currentMana >= amount;

    /// <summary>Spend mana for a melee swing. Returns false if not enough mana.</summary>
    public bool TrySpendMelee()
    {
        if (currentMana < meleeSwingCost) return false;
        SpendMana(meleeSwingCost);
        return true;
    }

    /// <summary>Spend mana for a sword throw. Returns false if not enough mana.</summary>
    public bool TrySpendThrow()
    {
        if (currentMana < throwCost) return false;
        SpendMana(throwCost);
        return true;
    }

    private float _sprintDrainAccumulator;

    /// <summary>Call every frame while sprinting. Drains mana over time.</summary>
    public void DrainSprint()
    {
        _sprintDrainAccumulator += sprintDrainPerSecond * Time.deltaTime;
        if (_sprintDrainAccumulator >= 1f)
        {
            int points = Mathf.FloorToInt(_sprintDrainAccumulator);
            _sprintDrainAccumulator -= points;
            SpendMana(points);
        }
    }

    // ─── SUSTAIN DRAIN ───────────────────────────────────────

    private void HandleSustainDrain()
    {
        if (!_maskActive) return;

        // Grace period hasn't elapsed yet
        float elapsed = Time.time - _maskEquipTime;
        if (elapsed < sustainGracePeriod) return;

        // Check if it's time for the next tick
        if (Time.time - _lastDrainTick >= sustainDrainInterval)
        {
            _lastDrainTick = Time.time;
            SpendMana(sustainDrainAmount);

            // Out of mana → force-remove the mask
            if (currentMana <= 0)
            {
                ForceRemoveMask();
            }
        }
    }

    // ─── REGEN ───────────────────────────────────────────────

    private void HandleRegen()
    {
        // No regen while wearing a mask
        if (_maskActive) return;

        // Already full
        if (currentMana >= maxMana) return;

        // Wait for regen delay after mask removal
        if (Time.time - _maskRemovedTime < regenDelay)
            return;

        _regenAccumulator += regenPerSecond * Time.deltaTime;
        if (_regenAccumulator >= 1f)
        {
            int points = Mathf.FloorToInt(_regenAccumulator);
            _regenAccumulator -= points;
            RestoreMana(points);
        }
    }

    // ─── HELPERS ─────────────────────────────────────────────

    private void ForceRemoveMask()
    {
        if (_maskController != null)
        {
            _maskController.ActivateNeutral();
            Debug.Log("⚡ Out of mana — switched to default mask!");
        }
        OnMaskRemoved();
    }

    private void RefreshUI()
    {
        if (manaBar != null)
            manaBar.SetHealthBar(currentMana);
    }
}
