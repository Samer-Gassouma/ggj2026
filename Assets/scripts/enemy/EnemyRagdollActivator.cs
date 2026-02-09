using UnityEngine;
using UnityEngine.AI;

public class EnemyRagdollActivator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody[] ragdollBodies;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (ragdollBodies == null || ragdollBodies.Length == 0)
            ragdollBodies = GetComponentsInChildren<Rigidbody>();
        SetRagdollActive(false);
    }

    public void ActivateRagdoll()
    {
        // Disable animator
        if (animator != null)
            animator.enabled = false;

        // Disable NavMeshAgent if present
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;

        // Disable all MonoBehaviour scripts except this one
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != this)
                script.enabled = false;
        }

        // Disable all colliders on this root GameObject
        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        SetRagdollActive(true);
    }

    private void SetRagdollActive(bool active)
    {
        foreach (var rb in ragdollBodies)
        {
            rb.isKinematic = !active;
            rb.detectCollisions = active;
        }
    }
}
