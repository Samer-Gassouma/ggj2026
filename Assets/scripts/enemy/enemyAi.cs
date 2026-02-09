using UnityEngine;
using System.Collections;
using UnityEngine.AI;


public class enemyAi : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;


    [Header("Layers")]
    [SerializeField] private LayerMask terrainLayer;
    [SerializeField] private LayerMask playerLayerMask;


    [Header("Patrol Settings")]
    [SerializeField] private float patrolRadius = 10f;
    private Vector3 currentPatrolPoint;
    private bool hasPatrolPoint;


    [Header("Combat Settings")]
    [SerializeField] private float attackCooldown = 1f;
    private bool isOnAttackCooldown;
    [SerializeField] private float forwardShotForce = 10f;
    [SerializeField] private float verticalShotForce = 5f;


    [Header("Detection Ranges")]
    [SerializeField] private float visionRange = 20f;
    [SerializeField] private float engagementRange = 10f;


    private bool isPlayerVisible;
    private bool isPlayerInRange;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Attack Ranges")]
    [SerializeField] private float meleeRange;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;
    [SerializeField] private healthbar healthBar;

    [Header("Leash Settings")]
    [SerializeField] private float leashDistance = 30f;
    private Vector3 spawnPoint;
    private bool returningToSpawn = false;

    private void Awake()
    {
        // Auto-find player by tag
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (navAgent != null)
            navAgent.updateRotation = false; // We handle rotation manually

        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Auto-find health bar in children
        if (healthBar == null)
            healthBar = GetComponentInChildren<healthbar>();

        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.SetMaxHealth(maxHealth);

        spawnPoint = transform.position;
    }

    private void Start()
    {
        // Ensure health bar is initialized if not set in Awake (optional)
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(maxHealth);
        }
    }

    private void Update()
    {
        DetectPlayer();
        UpdateBehaviourState();
        UpdateAnimation();
        UpdateAttackAnimation();
        HandleLeash();

        if (playerTransform != null && isPlayerVisible)
        {
            FaceTarget(playerTransform.position, 12f);

            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }
    }
    private void UpdateAnimation()
    {
        float speed = navAgent.velocity.magnitude;
        animator.SetFloat("Speed", speed);
    }
    private void UpdateAttackAnimation()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool isStopped = navAgent.velocity.magnitude < 0.1f;

        // Only set IsAttacking true if in range and stopped, otherwise set false
        bool shouldAttack = distance <= engagementRange && isStopped;
        animator.SetBool("IsAttacking", shouldAttack);

        // Remove animator.ResetTrigger("Attack"); since "Attack" parameter does not exist
        // If you want to use triggers, add a trigger parameter named "Attack" in the Animator
        // Otherwise, just use IsAttacking as you do now
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, engagementRange);


        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);
    }


    private void DetectPlayer()
    {
        isPlayerVisible = Physics.CheckSphere(transform.position, visionRange, playerLayerMask);
        isPlayerInRange = Physics.CheckSphere(transform.position, engagementRange, playerLayerMask);
    }


    private void FireProjectile()
    {
        if (projectilePrefab == null || firePoint == null) return;


        Rigidbody projectileRb = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity).GetComponent<Rigidbody>();
        projectileRb.AddForce(transform.forward * forwardShotForce, ForceMode.Impulse);
        projectileRb.AddForce(transform.up * verticalShotForce, ForceMode.Impulse);


        Destroy(projectileRb.gameObject, 3f);
    }


    private void FindPatrolPoint()
    {
        float randomX = Random.Range(-patrolRadius, patrolRadius);
        float randomZ = Random.Range(-patrolRadius, patrolRadius);


        Vector3 potentialPoint = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);


        if (Physics.Raycast(potentialPoint, -transform.up, 2f, terrainLayer))
        {
            currentPatrolPoint = potentialPoint;
            hasPatrolPoint = true;
        }
    }


    private IEnumerator AttackCooldownRoutine()
    {
        isOnAttackCooldown = true;
        yield return new WaitForSeconds(attackCooldown);
        isOnAttackCooldown = false;
    }




    private void PerformPatrol()
    {
        if (!hasPatrolPoint)
            FindPatrolPoint();


        if (hasPatrolPoint)
            navAgent.SetDestination(currentPatrolPoint);


        if (Vector3.Distance(transform.position, currentPatrolPoint) < 1f)
            hasPatrolPoint = false;
    }


    private void PerformChase()
    {
        animator.SetBool("IsAttacking", false);
        navAgent.isStopped = false;

        if (playerTransform != null)
        {
            navAgent.SetDestination(playerTransform.position);
            FaceTarget(playerTransform.position, 12f);
        }
    }


    private void PerformAttack()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        if (distance <= meleeRange)
        {
            navAgent.isStopped = true;
            FaceTarget(playerTransform.position, 15f);
            animator.SetBool("IsAttacking", true);
            return;
        }

        navAgent.isStopped = false;
        animator.SetBool("IsAttacking", false);
        navAgent.SetDestination(playerTransform.position);
        FaceTarget(playerTransform.position, 10f);

        if (!isOnAttackCooldown)
        {
            FireProjectile();
            StartCoroutine(AttackCooldownRoutine());
        }
    }

    private void HandleLeash()
    {
        if (returningToSpawn)
        {
            navAgent.SetDestination(spawnPoint);
            animator.SetBool("IsAttacking", false);

            // If close to spawn, stop returning
            if (Vector3.Distance(transform.position, spawnPoint) < 1.5f)
            {
                returningToSpawn = false;
                hasPatrolPoint = false; // Resume normal patrol
            }
            return;
        }

        // If too far from spawn, start returning
        float distFromSpawn = Vector3.Distance(transform.position, spawnPoint);
        if (distFromSpawn > leashDistance)
        {
            returningToSpawn = true;
        }
    }

    private void UpdateBehaviourState()
    {
        if (returningToSpawn)
        {
            // Don't do anything else while returning to spawn
            return;
        }

        // If player is not visible (not in vision range), return to spawn
        if (!isPlayerVisible)
        {
            returningToSpawn = true;
            return;
        }

        if (!isPlayerVisible && !isPlayerInRange)
        {
            PerformPatrol();
        }
        else if (isPlayerVisible && !isPlayerInRange)
        {
            PerformChase();
        }
        else if (isPlayerVisible && isPlayerInRange)
        {
            PerformAttack();
        }
    }

    public void DealMeleeDamage()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        if (distance <= meleeRange)
        {
            PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(10);
            }
        }
    }

    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(currentHealth - amount, 0);
        if (healthBar != null)
        {
            healthBar.SetHealthBar(currentHealth);
        }
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        var ragdoll = GetComponent<EnemyRagdollActivator>();
        if (ragdoll != null)
        {
            ragdoll.ActivateRagdoll();
            Destroy(gameObject, 10f);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void FaceTarget(Vector3 targetPosition, float rotateSpeed)
    {
        Vector3 lookDir = targetPosition - transform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }
}