using UnityEngine;

public class BossArenaTrigger : MonoBehaviour
{
    [SerializeField] private BossBrain boss;

    private void Awake()
    {
        // Auto-find BossBrain on parent or siblings if not assigned
        if (!boss) boss = GetComponentInParent<BossBrain>();
        if (!boss) boss = FindObjectOfType<BossBrain>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (boss) boss.StartBoss();
        gameObject.SetActive(false);
    }
}
