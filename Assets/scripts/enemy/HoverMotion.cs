using UnityEngine;

/// <summary>
/// Smooth hovering bob applied to a child "visual" transform so the
/// physics collider on the parent stays rock-steady.
/// Attach to the same GameObject that has the Rigidbody; drag the visual
/// child mesh into <see cref="visualTransform"/>.
/// </summary>
public class HoverMotion : MonoBehaviour
{
    [Header("Hover Settings")]
    [Tooltip("Child transform that contains the visual mesh. If null, creates an offset on this transform (less stable).")]
    [SerializeField] private Transform _visualTransform;

    /// <summary>Public read access so other scripts (e.g. trail attachment) can find the visual child.</summary>
    public Transform visualTransform => _visualTransform;

    [SerializeField] private float bobAmplitude = 0.35f;
    [SerializeField] private float bobFrequency = 1.2f;

    [Header("Yaw Wobble (optional)")]
    [SerializeField] private bool enableYawWobble = true;
    [SerializeField] private float yawAmplitude = 8f;
    [SerializeField] private float yawFrequency = 0.6f;

    [Header("Control")]
    [Tooltip("When false the hover is paused (e.g. during charge).")]
    public bool active = true;

    // ───────── Internal ─────────
    private Vector3 visualLocalOrigin;
    private Quaternion visualLocalRotOrigin;
    private float timeOffset;

    private void Awake()
    {
        // Random offset so multiple enemies don't bob in unison
        timeOffset = Random.Range(0f, Mathf.PI * 2f);

        if (visualTransform != null)
        {
            visualLocalOrigin = visualTransform.localPosition;
            visualLocalRotOrigin = visualTransform.localRotation;
        }
    }

    private void LateUpdate()
    {
        if (visualTransform == null || !active) return;

        float t = Time.time + timeOffset;

        // Vertical bob
        float yOffset = Mathf.Sin(t * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        visualTransform.localPosition = visualLocalOrigin + Vector3.up * yOffset;

        // Yaw wobble
        if (enableYawWobble)
        {
            float yaw = Mathf.Sin(t * yawFrequency * Mathf.PI * 2f) * yawAmplitude;
            visualTransform.localRotation = visualLocalRotOrigin * Quaternion.Euler(0f, yaw, 0f);
        }
    }

    /// <summary>Reset visual to origin (call before destroy / ragdoll).</summary>
    public void ResetVisual()
    {
        if (visualTransform == null) return;
        visualTransform.localPosition = visualLocalOrigin;
        visualTransform.localRotation = visualLocalRotOrigin;
    }
}
