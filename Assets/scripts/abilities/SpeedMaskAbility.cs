using UnityEngine;

/// <summary>
/// DEPRECATED — Speed is no longer a standalone mask in the new 3-mask system.
/// Kept for backwards compatibility with existing ScriptableObject assets.
/// </summary>
[CreateAssetMenu(menuName = "Masks/Speed Mask (Legacy)", fileName = "SpeedMask")]
public class SpeedMaskAbility : MaskAbility
{
    [Min(0.1f)] public float speedMultiplier = 1.5f;

    public override void Activate(PlayerAbilityContext context)
    {
        context.speedMultiplier = speedMultiplier;
    }

    public override void Deactivate(PlayerAbilityContext context)
    {
        // Optional: rely on controller ResetContext before activate
    }
}
