using UnityEngine;

public class ObstaclePush : MonoBehaviour
{
    [SerializeField]
    private float forceMagnitude;
    [SerializeField]
    private float maxPushVelocity;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;

        // Only push objects tagged as "Pushable" (optional, for control)
        // if (!hit.gameObject.CompareTag("Pushable")) return;

        // Only push if collision is mostly horizontal
        Vector3 pushDir = hit.moveDirection;
        pushDir.y = 0;

        // Apply a gentle force, similar to CharacterController's default push
        body.linearVelocity = pushDir * forceMagnitude;

        // Optionally, clamp max velocity for stability

        if (body.linearVelocity.magnitude > maxPushVelocity)
        {
            body.linearVelocity = body.linearVelocity.normalized * maxPushVelocity;
        }
    }
}
