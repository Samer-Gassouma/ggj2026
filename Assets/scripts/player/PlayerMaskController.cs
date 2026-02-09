using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerAbilityContext))]
public class PlayerMaskController : MonoBehaviour
{
    [SerializeField] private List<MaskAbility> inventory = new List<MaskAbility>();
    [SerializeField] private MaskAbility activeAbility;

    [Header("Optional Input")]
    public bool enableInputSelection = true;

    private PlayerAbilityContext context;

    /// <summary>The currently active mask (read-only).</summary>
    public MaskAbility ActiveMask => activeAbility;

    private void Awake()
    {
        context = GetComponent<PlayerAbilityContext>();
    }

    private void Update()
    {
        if (!enableInputSelection || inventory.Count == 0) return;

        // Number keys 1..9 select masks
        for (int i = 0; i < Mathf.Min(9, inventory.Count); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                ActivateMask(inventory[i]);
                break;
            }
        }

        // DEBUG: Press P to unlock superpower on active mask
        if (Input.GetKeyDown(KeyCode.P) && activeAbility != null)
        {
            UnlockSuperpower(activeAbility);
            // Re-activate so the superpower effects apply immediately
            activeAbility.Deactivate(context);
            context.ResetContext();
            activeAbility.Activate(context);
            Debug.Log($"🔓 DEBUG: Force-unlocked superpower for {activeAbility.displayName}");
        }
    }

    public void AddAbility(MaskAbility ability)
    {
        if (ability == null) return;

        if (!inventory.Contains(ability))
            inventory.Add(ability);

        // Auto-activate first acquired ability
        if (activeAbility == null)
            ActivateMask(ability);
    }

    public void ActivateMask(MaskAbility ability)
    {
        if (ability == activeAbility) return;

        // Deactivate current ability, reset context, then activate new
        if (activeAbility != null)
            activeAbility.Deactivate(context);

        context.ResetContext();

        activeAbility = ability;

        if (activeAbility != null)
            activeAbility.Activate(context);
    }

    /// <summary>
    /// Awaken the superpower of a specific mask. 
    /// Call from special locations/challenges in the world.
    /// </summary>
    public void UnlockSuperpower(MaskAbility mask)
    {
        if (mask == null || mask.superpowerUnlocked) return;

        mask.UnlockSuperpower(context);

        // If this mask is currently active, re-activate to apply superpower immediately
        if (mask == activeAbility)
        {
            mask.Deactivate(context);
            context.ResetContext();
            mask.Activate(context);
        }

        // Show a toast notification about the superpower unlock
        if (MaskPickupUI.Instance != null)
        {
            Sprite iconToShow = mask.superpowerIcon != null ? mask.superpowerIcon : mask.icon;
            MaskPickupUI.Instance.ShowToast(iconToShow, mask.displayName + " — Superpower Awakened!");
        }

        Debug.Log($"🔓 Superpower unlocked for {mask.displayName}!");
    }

    /// <summary>
    /// Unlock the superpower of the currently active mask.
    /// </summary>
    public void UnlockActiveSuperpower()
    {
        if (activeAbility != null)
            UnlockSuperpower(activeAbility);
    }

    /// <summary>
    /// Check if a specific mask in inventory has its superpower unlocked.
    /// </summary>
    public bool IsSuperUnlocked(MaskAbility mask)
    {
        return mask != null && mask.superpowerUnlocked;
    }

    private void Cycle(int direction)
    {
        if (inventory.Count == 0) return;

        int idx = Mathf.Max(0, inventory.IndexOf(activeAbility));
        idx = (idx + direction + inventory.Count) % inventory.Count;
        ActivateMask(inventory[idx]);
    }

    public int NeutralIndex => -1;
    public IReadOnlyList<MaskAbility> Inventory => inventory;
    public int ActiveIndex => activeAbility == null ? NeutralIndex : inventory.IndexOf(activeAbility);

    public void ActivateIndex(int index)
    {
        if (index == NeutralIndex)
        {
            ActivateNeutral();
            return;
        }
        if (index < 0 || index >= inventory.Count) return;
        ActivateMask(inventory[index]);
    }

    public void ActivateNeutral()
    {
        if (activeAbility != null)
            activeAbility.Deactivate(context);

        context.ResetContext();
        activeAbility = null;
    }

    public void SetInputSelectionEnabled(bool enabled)
    {
        enableInputSelection = enabled;
    }

    /// <summary>
    /// Re-activates the currently equipped mask to refresh abilities (e.g., after unlocking double jump).
    /// </summary>
    public void ReactivateCurrentMask()
    {
        if (activeAbility != null)
        {
            var context = GetComponent<PlayerAbilityContext>();
            if (context != null)
            {
                activeAbility.Deactivate(context);
                activeAbility.Activate(context);
            }
        }
    }
}
