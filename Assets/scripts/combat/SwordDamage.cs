using UnityEngine;

public class SwordDamage : MonoBehaviour
{
    public int damage = 10;
    public string enemyTag = "Enemy";
    private bool canDealDamage = false;
    
    private SwordController swordController;
    private PlayerAbilityContext abilityContext;

    private void Awake()
    {
        swordController = GetComponent<SwordController>();
        if (swordController == null)
            swordController = GetComponentInParent<SwordController>();
    }

    private void Start()
    {
        // Find the player's ability context for damage multiplier
        var player = GameObject.FindWithTag("Player");
        if (player != null)
            abilityContext = player.GetComponent<PlayerAbilityContext>();
    }

    // Call this from an Animation Event at the start of the swing
    public void EnableDamage()
    {
        canDealDamage = true;
        
        // Also enable trail
        if (swordController != null)
            swordController.EnableTrail();
    }

    // Call this from an Animation Event at the end of the swing
    public void DisableDamage()
    {
        canDealDamage = false;
        
        // Also disable trail
        if (swordController != null)
            swordController.DisableTrail();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDealDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDealDamage(other);
    }

    private void TryDealDamage(Collider other)
    {
        if (!canDealDamage) return;

        var enemyAiTarget = other.GetComponentInParent<enemyAi>();
        var bossTarget = enemyAiTarget == null ? other.GetComponentInParent<BossBrain>() : null;
        var genericTarget = (enemyAiTarget == null && bossTarget == null) ? other.GetComponentInParent<EnemyBase>() : null;

        if (enemyAiTarget == null && bossTarget == null && genericTarget == null)
        {
            if (!other.CompareTag(enemyTag)) return;
            enemyAiTarget = other.GetComponent<enemyAi>();
            bossTarget = enemyAiTarget == null ? other.GetComponent<BossBrain>() : null;
            if (enemyAiTarget == null && bossTarget == null)
                genericTarget = other.GetComponent<EnemyBase>();
            if (enemyAiTarget == null && bossTarget == null && genericTarget == null)
                return;
        }

        int finalDamage = Mathf.RoundToInt(damage * (abilityContext != null ? abilityContext.damageMultiplier : 1f));

        if (enemyAiTarget != null)
        {
            enemyAiTarget.TakeDamage(finalDamage);
            canDealDamage = false;
            return;
        }

        if (bossTarget != null)
        {
            bossTarget.TakeDamage(finalDamage);
            canDealDamage = false;
            return;
        }

        if (genericTarget != null)
        {
            genericTarget.TakeDamage(finalDamage);
            canDealDamage = false;
        }
    }
}

