using UnityEngine;

[CreateAssetMenu(menuName = "Masks/Double Jump Mask", fileName = "DoubleJumpMask")]
public class DoubleJumpMaskAbility : MaskAbility
{
    [Min(0)] public int extraJumps = 1;

    public override void Activate(PlayerAbilityContext context)
    {
        context.maxAirJumps = extraJumps;
    }

    public override void Deactivate(PlayerAbilityContext context)
    {
    }
}

