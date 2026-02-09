using UnityEngine;

[RequireComponent(typeof(Collider)), RequireComponent(typeof(Rigidbody))]
public class MaskPickup : MonoBehaviour
{
    public MaskAbility maskToGive;
    public string playerTag = "Player";

    [Header("Interaction")]
    public float bobSpeed = 2f;
    public float bobHeight = 0.25f;
    public float autoCollectDelay = 0.3f; // Small delay before auto-pickup

    private bool pickedUp = false;
    private PlayerMaskController cachedController;
    private Vector3 startPos;
    private float collectTimer = -1f;

    private void OnValidate()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        // Gentle bobbing animation
        Vector3 pos = startPos;
        pos.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = pos;

        // Slow rotation
        transform.Rotate(Vector3.up, 30f * Time.deltaTime, Space.World);

        // Auto-collect countdown
        if (collectTimer >= 0f)
        {
            collectTimer -= Time.deltaTime;
            if (collectTimer <= 0f)
            {
                Collect();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp) return;
        if (!other.CompareTag(playerTag)) return;

        cachedController = other.GetComponentInParent<PlayerMaskController>();
        if (cachedController == null || maskToGive == null) return;

        // Start auto-collect countdown
        collectTimer = autoCollectDelay;

        // Show toast notification
        MaskPickupUI.Instance.ShowToast(maskToGive.icon, $"Collecting {maskToGive.displayName}...");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        collectTimer = -1f; // Cancel auto-collect
        cachedController = null;

        // Hide prompt
        MaskPickupUI.Instance.HidePrompt();
    }

    private void Collect()
    {
        if (cachedController == null || maskToGive == null) return;

        pickedUp = true;
        collectTimer = -1f;

        // Add to inventory
        cachedController.AddAbility(maskToGive);

        // Hide any prompt, show success toast
        MaskPickupUI.Instance.HidePrompt();
        MaskPickupUI.Instance.ShowToast(maskToGive.icon, $"{maskToGive.displayName} collected! Press TAB to equip.");

        // Remove pickup from scene
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        // Safety: hide prompt if object is disabled while collecting
        if (collectTimer >= 0f && MaskPickupUI.Instance != null)
            MaskPickupUI.Instance.HidePrompt();
    }
}
