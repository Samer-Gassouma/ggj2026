using UnityEngine;

public abstract class MaskAbility : ScriptableObject
{
    [Header("Identity")]
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;
    public Color maskColor = Color.white;

    [Header("Superpower")]
    [Tooltip("Has the secondary ability been awakened?")]
    public bool superpowerUnlocked = false;
    public Sprite superpowerIcon;
    [TextArea(2, 4)] public string superpowerDescription;

    /// <summary>Called when this mask becomes the active mask. Always grants primary ability.</summary>
    public abstract void Activate(PlayerAbilityContext context);

    /// <summary>Called when switching away from this mask.</summary>
    public abstract void Deactivate(PlayerAbilityContext context);

    /// <summary>Called when the superpower is awakened for the first time at a special location/challenge.</summary>
    public virtual void UnlockSuperpower(PlayerAbilityContext context)
    {
        superpowerUnlocked = true;
        // Re-activate to apply the superpower on top of primary
        ActivateSuperpower(context);
    }

    /// <summary>Applies the superpower effects. Only called if superpowerUnlocked is true.</summary>
    protected virtual void ActivateSuperpower(PlayerAbilityContext context) { }

    /// <summary>Removes the superpower effects.</summary>
    protected virtual void DeactivateSuperpower(PlayerAbilityContext context) { }
}
