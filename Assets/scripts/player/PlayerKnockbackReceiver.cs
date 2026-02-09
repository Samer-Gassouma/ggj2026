using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerKnockbackReceiver : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 knockbackVelocity;
    [SerializeField] private float knockbackDrag = 5f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            controller.Move(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDrag * Time.deltaTime);
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }
    }

    public void ApplyKnockback(Vector3 force)
    {
        knockbackVelocity += force;
    }
}
