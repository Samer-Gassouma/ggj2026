using System.Collections;
using UnityEngine;

public class BossAttacks : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public Transform handSocketR;
    public Transform shootCenter;

    [Header("Prefabs")]
    public GameObject smallRockPrefab;
    public GameObject bigRockPrefab;

    [Header("Spin Rocks")]
    public int rocksPerBurst = 16;
    public float spinRockSpeed = 5f;        // lowered from 12
    public float burstInterval = 0.15f;
    public float spinDuration = 2.0f;
    public float rockLifetime = 1.5f;       // how long rocks live before despawn
    public float rockMaxDistance = 10f;      // max distance from boss before destroyed

    [Header("Throw")]
    public float throwForce = 18f;
    public float throwUp = 2.0f;

    [Header("Shockwave")]
    public float shockwaveRadius = 8f;
    public float pushForce = 14f;
    public LayerMask playerMask;

    private GameObject heldRock;

    private void Awake()
    {
        // Auto-find player by tag
        if (!player)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) player = playerObj.transform;
        }

        // Auto-find hand socket by name convention
        if (!handSocketR)
            handSocketR = FindChildRecursive(transform, "HandSocketR")
                       ?? FindChildRecursive(transform, "Hand_R")
                       ?? FindChildRecursive(transform, "RightHand")
                       ?? FindChildRecursive(transform, "hand_r");

        // Auto-find shoot center by name convention
        if (!shootCenter)
            shootCenter = FindChildRecursive(transform, "ShootCenter")
                       ?? FindChildRecursive(transform, "shootCenter")
                       ?? FindChildRecursive(transform, "FirePoint");

        // Fallback shoot center to this transform
        if (!shootCenter) shootCenter = transform;

        // Auto-set player mask if not assigned
        if (playerMask == 0)
        {
            int layer = LayerMask.NameToLayer("Player");
            if (layer >= 0) playerMask = 1 << layer;
        }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return child;
            var found = FindChildRecursive(child, name);
            if (found) return found;
        }
        return null;
    }

    // Animation Event (start of spin clip)
    public void AE_StartSpinRocks()
    {
        StopAllCoroutines();
        StartCoroutine(SpinRoutine());
    }

    IEnumerator SpinRoutine()
    {
        float t = 0f;
        while (t < spinDuration)
        {
            FacePlayer(); // Face player during spin
            FireBurst();
            yield return new WaitForSeconds(burstInterval);
            t += burstInterval;
        }
    }

    void FireBurst()
    {
        if (!smallRockPrefab || !shootCenter) return;

        float step = 360f / rocksPerBurst;
        float offset = Random.Range(0f, step);

        for (int i = 0; i < rocksPerBurst; i++)
        {
            float angle = offset + step * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            dir.y = 0f;
            dir.Normalize();

            Vector3 spawnPos = shootCenter.position + dir * 1.5f;

            var rock = Instantiate(
                smallRockPrefab,
                spawnPos,
                Quaternion.LookRotation(dir)
            );

            var rb = rock.GetComponent<Rigidbody>();
            if (rb == null)
                rb = rock.AddComponent<Rigidbody>();

            rb.isKinematic = false;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = dir * spinRockSpeed;

            // Destroy after short lifetime so rocks don't fly forever
            Destroy(rock, rockLifetime);
        }
    }

    // Animation Event (when rock appears in hand)
    public void AE_CreateHandRock()
    {
        if (!bigRockPrefab || !handSocketR) return;

        if (heldRock) Destroy(heldRock);
        heldRock = Instantiate(bigRockPrefab, handSocketR.position, handSocketR.rotation, handSocketR);
    }

    // Animation Event (throw frame)
    public void AE_ThrowHandRock()
    {
        if (!heldRock || !player || !handSocketR) return;

        FacePlayer(); // Face player before throwing

        heldRock.transform.parent = null;

        Rigidbody rb = heldRock.GetComponent<Rigidbody>();
        if (!rb) rb = heldRock.AddComponent<Rigidbody>();

        Vector3 toPlayer = player.position - handSocketR.position;
        toPlayer.y = 0f;

        Vector3 dir = toPlayer.sqrMagnitude < 0.001f ? transform.forward : toPlayer.normalized;
        rb.linearVelocity = dir * throwForce + Vector3.up * throwUp;

        Destroy(heldRock, 5f); // Clean up rock after 5 seconds
        heldRock = null;
    }

    // Animation Event (slam impact)
    public void AE_Shockwave()
    {
        Vector3 center = shootCenter ? shootCenter.position : transform.position;

        Collider[] hits = Physics.OverlapSphere(center, shockwaveRadius, playerMask);
        foreach (var h in hits)
        {
            var receiver = h.GetComponentInParent<PlayerKnockbackReceiver>();
            if (receiver != null)
            {
                Vector3 dir = (receiver.transform.position - center);
                dir.y = 0f;
                dir = dir.sqrMagnitude < 0.001f ? Vector3.forward : dir.normalized;
                receiver.ApplyKnockback(dir * pushForce);
            }
        }
    }

    private void FacePlayer()
    {
        if (player == null) return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}

