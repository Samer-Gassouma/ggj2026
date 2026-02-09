using UnityEngine;

public class DashUnlockPickup : MonoBehaviour
{
    [SerializeField] private string unlockMessage = "Dash Unlocked!";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var context = other.GetComponent<PlayerAbilityContext>();
        if (context == null) return;

        // Permanently unlock dash — persists across mask swaps
        context.dashPermanentlyUnlocked = true;
        context.dashEnabled = true;

        // If the dash mask is currently active, re-apply so handler picks it up
        var dashHandler = other.GetComponent<DashAbilityHandler>();
        if (dashHandler != null && dashHandler.enabled)
        {
            var maskController = other.GetComponent<PlayerMaskController>();
            if (maskController != null)
                maskController.ReactivateCurrentMask();
        }
        else
        {
            // Not wearing dash mask — add handler directly so dash works now
            dashHandler = other.GetComponent<DashAbilityHandler>();
            if (dashHandler == null)
                dashHandler = other.gameObject.AddComponent<DashAbilityHandler>();
            dashHandler.enabled = true;

            // Try to find a DashMaskAbility asset to set it up
            var dashAsset = Resources.Load<DashMaskAbility>("DashMask");
            if (dashAsset != null)
                dashHandler.Setup(dashAsset);
        }

        Debug.Log(unlockMessage);

        // Show UI toast
        if (MaskPickupUI.Instance != null)
            MaskPickupUI.Instance.ShowToast(null, unlockMessage, "New Ability");

        Destroy(gameObject);
    }
}
