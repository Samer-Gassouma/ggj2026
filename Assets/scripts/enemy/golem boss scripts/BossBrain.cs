using System.Collections;
using UnityEngine;

public class BossBrain : MonoBehaviour
{
    public enum State { Inactive, Spawning, Idle, Attacking, Dead }

    [SerializeField] private Animator animator;
    [SerializeField] private Transform player;
    [SerializeField] private BossAttacks bossAttacks;

    [Header("Combat")]
    [SerializeField] private float idleBetweenAttacks = 1.0f;

    [Header("HP")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float slamHPThreshold = 35f;
    [SerializeField] private healthbar healthBar;

    [Header("Grounding")]
    [SerializeField] private float groundYOffset = 0f;
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private float groundCheckDistance = 10f;

    private float hp;
    private float groundY;
    private bool started;
    private State state = State.Inactive;

    void Awake()
    {
        hp = maxHP;

        // Ensure boss starts on ground: snap to ground at Awake
        SnapToGround();

        RecalculateGroundHeight();

        // Auto-find references
        if (!animator) animator = GetComponent<Animator>();
        if (!animator) animator = GetComponentInChildren<Animator>();

        if (!player)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) player = playerObj.transform;
        }

        if (!bossAttacks) bossAttacks = GetComponent<BossAttacks>();
        if (!bossAttacks) bossAttacks = GetComponentInChildren<BossAttacks>();

        // Auto-find health bar in children
        if (!healthBar) healthBar = GetComponentInChildren<healthbar>();

        if (healthBar) healthBar.SetMaxHealth((int)maxHP);
    }

    void Start()
    {
        // Snap to ground again in case of spawn jitter
        SnapToGround();
        RecalculateGroundHeight();
    }

    void Update()
    {
        if (!started) return;

        if (state != State.Dead)
        {
            if (transform.position.y < groundY - 0.05f)
            {
                Vector3 pos = transform.position;
                transform.position = new Vector3(pos.x, groundY, pos.z);
            }
        }

        if (player && (state == State.Idle || state == State.Attacking))
        {
            Vector3 dir = -(player.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 6f);
            }
        }
    }

    public void StartBoss()
    {
        if (started) return;
        started = true;

        state = State.Spawning;
        animator.SetTrigger("Spawn");
    }

    // Animation Event (end of spawn clip)
    public void AE_OnSpawnFinished()
    {
        state = State.Idle;
        StartCoroutine(CombatLoop());
    }

    IEnumerator CombatLoop()
    {
        while (state != State.Dead)
        {
            if (state != State.Idle) { yield return null; continue; }

            yield return new WaitForSeconds(idleBetweenAttacks);

            state = State.Attacking;

            bool lowHP = hp <= slamHPThreshold;

            int choice = lowHP ? Random.Range(0, 3) : Random.Range(0, 2);
            if (choice == 0) animator.SetTrigger("Spin");
            else if (choice == 1) animator.SetTrigger("Throw");
            else animator.SetTrigger("Slam");

            while (state == State.Attacking) yield return null;
        }
    }

    // Animation Event (end of each attack clip)
    public void AE_OnAttackFinished()
    {
        if (state == State.Dead) return;
        state = State.Idle;
    }

    public void TakeDamage(float dmg)
    {
        if (!started || state == State.Dead) return;

        hp = Mathf.Max(0f, hp - dmg);
        if (healthBar) healthBar.SetHealthBar((int)hp);

        if (hp <= 0f)
        {
            state = State.Dead;
            Die();
        }
    }

    private void Die()
    {
        // Disable all scripts and activate ragdoll if present
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

    private void RecalculateGroundHeight()
    {
        Vector3 origin = transform.position + Vector3.up * groundCheckDistance;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance * 2f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y + groundYOffset;
        }
        else
        {
            groundY = transform.position.y + groundYOffset;
        }
    }

    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundCheckDistance;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance * 2f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x, hit.point.y + groundYOffset, pos.z);
        }
    }
}
