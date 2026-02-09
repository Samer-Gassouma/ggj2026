using UnityEngine;

/// <summary>
/// Place in the world at specific locations/challenges.
/// When the player enters the trigger, it awakens the superpower
/// of a specified mask (or the currently active mask).
/// </summary>
[RequireComponent(typeof(Collider))]
public class SuperpowerShrine : MonoBehaviour
{
    [Header("Which mask to unlock?")]
    [Tooltip("Leave null to unlock whichever mask the player currently has active.")]
    public MaskAbility specificMask;

    [Header("Settings")]
    public string playerTag = "Player";
    public bool destroyAfterUse = true;

    [Header("Optional VFX")]
    public GameObject activationVFXPrefab;

    private bool used = false;

    private void OnValidate()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used) return;
        if (!other.CompareTag(playerTag)) return;

        var controller = other.GetComponentInParent<PlayerMaskController>();
        if (controller == null) return;

        MaskAbility target = specificMask;

        // If no specific mask set, unlock the currently active one
        if (target == null)
            target = controller.ActiveMask;

        if (target == null || target.superpowerUnlocked) return;

        used = true;

        // Unlock!
        controller.UnlockSuperpower(target);

        // VFX
        if (activationVFXPrefab != null)
        {
            var vfx = Instantiate(activationVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 5f);
        }

        Debug.Log($"⚡ Shrine activated: {target.displayName} superpower awakened!");

        if (destroyAfterUse)
            Destroy(gameObject, 0.5f);
    }
}
