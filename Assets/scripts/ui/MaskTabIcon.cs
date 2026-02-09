using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles clicks on a mask icon in the Tab UI. Detects left/right half clicks.
/// </summary>
public class MaskTabIcon : MonoBehaviour, IPointerClickHandler
{
    private MaskAbility mask;
    private PlayerMaskController playerMasks;

    public void Setup(MaskAbility mask, PlayerMaskController ctrl)
    {
        this.mask = mask;
        this.playerMasks = ctrl;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (mask == null) return;

        RectTransform rt = transform as RectTransform;
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out localPoint))
            return;

        float half = rt.rect.width * 0.5f;
        float x = localPoint.x + rt.rect.width * 0.5f; // convert to 0..width

        bool leftSide = x <= half;
        if (leftSide)
        {
            // Primary ability selected: switch active mask
            if (playerMasks != null)
                playerMasks.ActivateMask(mask);
        }
        else
        {
            // Right side: attempt to toggle/use superpower
            if (mask.superpowerUnlocked)
            {
                // If unlocked, activate mask and ensure effects apply
                if (playerMasks != null)
                {
                    playerMasks.ActivateMask(mask);
                    // Already unlocked; show small confirmation
                    if (MaskPickupUI.Instance != null)
                        MaskPickupUI.Instance.ShowToast(mask.superpowerIcon ?? mask.icon, mask.displayName + " — Superpower Ready", "Superpower");
                }
            }
            else
            {
                // Not learned yet — show centered locked UI
                if (LockFeedbackUI.Instance != null)
                    LockFeedbackUI.Instance.ShowLocked("Locked", "Not learned yet");
                else if (MaskPickupUI.Instance != null)
                    MaskPickupUI.Instance.ShowToast(mask.icon, "Not learned yet", "Locked");
            }
        }
    }
}
