using UnityEngine;

/// <summary>
/// Enemy projectile component: handles damage on hit and auto-destruction.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    private int damage = 10;
    private float lifeTime = 5f;
    private float spawnTime;
    private bool hasHit = false;
    
    public void Initialize(int dmg, float lifetime)
    {
        damage = dmg;
        lifeTime = lifetime;
        spawnTime = Time.time;
    }
    
    private void Update()
    {
        // Auto-destroy after lifetime
        if (Time.time - spawnTime > lifeTime)
        {
            Destroy(gameObject);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        
        // Check if it's the player
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            hasHit = true;
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        
        // Check if it's the player movement (root component)
        PlayerMovement playerMov = other.GetComponent<PlayerMovement>();
        if (playerMov != null)
        {
            hasHit = true;
            PlayerHealth ph = other.GetComponentInParent<PlayerHealth>();
            if (ph != null)
                ph.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        
        // Also handle non-trigger collisions
        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            hasHit = true;
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        
        PlayerMovement playerMov = collision.gameObject.GetComponent<PlayerMovement>();
        if (playerMov != null)
        {
            hasHit = true;
            PlayerHealth ph = collision.gameObject.GetComponentInParent<PlayerHealth>();
            if (ph != null)
                ph.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        
        // Hit something else (wall/obstacle) - destroy
        Destroy(gameObject);
    }
}
