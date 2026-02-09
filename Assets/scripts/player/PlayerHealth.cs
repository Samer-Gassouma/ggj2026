using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    public healthbar healthBar;

    /// <summary>Singleton-style reference so anything can find the player health easily.</summary>
    public static PlayerHealth Instance { get; private set; }

    /// <summary>Normalised health 0-1 for UI effects.</summary>
    public float HealthPercent => (float)currentHealth / maxHealth;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.SetMaxHealth(maxHealth);

        // Auto-add damage vignette if not present
        if (GetComponent<DamageVignette>() == null)
            gameObject.AddComponent<DamageVignette>();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (healthBar != null)
            healthBar.SetHealthBar(currentHealth);

        // Trigger screen damage vignette
        if (DamageVignette.Instance != null)
            DamageVignette.Instance.OnDamage(HealthPercent);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private bool hasDied = false;

    /// <summary>Instantly kills the player (e.g. falling off the map). Bypasses damage.</summary>
    public void InstantDeath()
    {
        currentHealth = 0;
        if (healthBar != null)
            healthBar.SetHealthBar(0);
        Die();
    }

    private void Die()
    {
        if (hasDied) return; // Prevent double-death
        hasDied = true;

        Debug.Log("Player died");

        // ── Disable ALL player controls ──

        // Movement + mouse look
        var pm = GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.SetCanMove(false);
            pm.enabled = false; // Fully stop Update() — no mouse look, no sword attacks, nothing
        }

        // Sword — disable all input so player can't throw/pickup/swing while dead
        SwordController sword = FindAnyObjectByType<SwordController>();
        if (sword != null)
        {
            sword.DisableInput();
        }

        // Auto-add death screen if needed, then show it
        DeathScreenUI deathUI = DeathScreenUI.Instance;
        if (deathUI == null)
        {
            deathUI = gameObject.AddComponent<DeathScreenUI>();
        }
        deathUI.ShowDeathScreen();
    }
}
