using UnityEngine;

/// <summary>
/// Place on a trigger collider to unlock a mask's superpower when the player enters.
/// Assign the mask to unlock in the inspector. The zone will only unlock once.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SuperPowerUnlockZone : MonoBehaviour
{
    public MaskAbility maskToUnlock;
    public string playerTag = "Player";

    private bool unlocked = false;

    private void OnValidate()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (unlocked) return;
        if (!other.CompareTag(playerTag)) return;

        var controller = other.GetComponentInParent<PlayerMaskController>();
        if (controller == null || maskToUnlock == null) return;

        controller.UnlockSuperpower(maskToUnlock);
        unlocked = true;

        // Optionally destroy zone after use
        // Destroy(gameObject);
    }
}
