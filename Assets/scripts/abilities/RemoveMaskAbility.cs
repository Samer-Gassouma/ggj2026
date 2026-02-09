using UnityEngine;

[CreateAssetMenu(menuName = "Masks/Remove Masks", fileName = "RemoveMask")]
public class RemoveMaskAbility : MaskAbility
{
    public override void Activate(PlayerAbilityContext context)
    {
        context.ResetContext();
    }

    public override void Deactivate(PlayerAbilityContext context)
    {
        // Nothing needed
    }
}
