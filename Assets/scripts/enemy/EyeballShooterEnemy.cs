using UnityEngine;

/// <summary>
/// Eyeball Shooter Enemy: follows player and shoots projectiles from its eye.
/// Auto-detects pupil transform (fire point) and tail bones.
/// Includes tail sway animation and Rigidbody-based movement.
/// </summary>
public class EyeballShooterEnemy : EnemyBase
{
    [Header("Pupil / Firing")]
    [SerializeField] private Transform eyePupil;
    [SerializeField] private string[] pupilNameHints = { "FlyerPupil", "EyePupil", "Pupil", "Iris", "Eyeball", "EyeBall", "eye_pupil" };
    
    [Header("Tail Animation")]
    [SerializeField] private Transform[] tailBones;
    [SerializeField] private string tailPrefix = "FlyerTail";
    [SerializeField] private int maxTailBones = 8;
    [SerializeField] private float tailSwayAmplitude = 15f;
    [SerializeField] private float tailSwayFrequency = 2f;
    [SerializeField] private float tailPhaseOffset = 0.3f;
    
    [Header("Movement")]
    [SerializeField] private float followSpeed = 6f;
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField] private float pupilTurnSpeed = 12f;
    [SerializeField] private float shootRadius = 15f;
    [SerializeField] private float preferredDistance = 10f;
    
    [SerializeField] private GameObject HatPrefab; 
    [Header("Shooting")]
    [SerializeField] private float fireCooldown = 1.5f;
    [SerializeField] private float chargeTime = 0.8f; // Time to charge before shooting
    [SerializeField] private float projectileSpeed = 20f;
    [SerializeField] private float projectileSize = 0.05f; // Smaller purple glowing orb
    [SerializeField] private int projectileDamage = 10;
    [SerializeField] private float projectileLifetime = 5f;
    
    [Header("VFX - Assign Prefabs from Asset Store")]
    [SerializeField] private GameObject muzzleFlashVfxPrefab; // e.g., vfx_MuzzleFlash_01
    [SerializeField] private GameObject projectileVfxPrefab; // e.g., vfx_Projectile_01
    [SerializeField] private GameObject ShieldHitVfxPrefab; // e.g., vfx_ShieldHit_01
    // SFX
    private AudioSource sfxSource;
    private AudioClip shotSFX;

    // State machine
    private enum State { Idle, Follow, AimShoot, Charge, Cooldown }
    private State currentState = State.Idle;
    
    // Movement
    private Rigidbody rb;
    private Vector3 moveVelocity = Vector3.zero;
    private float stoppingDistance = 0.5f;
    
    // Shooting
    private float shootCooldownTimer = 0f;
    private float chargeTimer = 0f;
    private GameObject chargeVfx; // Gather/charge VFX
    private GameObject chargedProjectile; // Projectile held during charge
    private ParticleSystem chargeParticles;
    private LineRenderer chargeLine; // Visual line during charging
    
    // Tail animation
    private Quaternion[] tailBaseRotations;
    private bool hasTails = false;
    
    protected override void Awake()
    {
        // Mid-tier enemy - 5 sword hits (50 HP / 10 dmg)
        maxHealth = 50;
        
        // Call base Awake first (initializes health, playerTransform, etc.)
        base.Awake();
        
        // Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
        
        // Auto-detect transforms
        AutoFindEyePupil();
        AutoFindTailBones();

        // SFX setup
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 1f; // Fully 3D
        sfxSource.maxDistance = 30f;
        sfxSource.rolloffMode = AudioRolloffMode.Linear;
        sfxSource.volume = 0.8f;

        shotSFX = Resources.Load<AudioClip>("SFX/shot");
    }
    
    protected override void Update()
    {
        if (isDead)
            return;
        
        // Call base for detection system
        base.Update();
        
        UpdateState();
        UpdateTailSway();
        UpdateShooting();

        // Optional: add simple bobbing motion
        Vector3 bobOffset = Vector3.up * Mathf.Sin(Time.time * 2f) * 0.1f;
        rb.MovePosition(transform.position + bobOffset * Time.deltaTime);

        //Make the hat bob up and down with the enemy
        if (HatPrefab != null){
            HatPrefab.transform.position = transform.position + bobOffset;
        }
    }
    
    private void UpdateState()
    {
        // Handle charge state separately
        if (currentState == State.Charge)
        {
            ChargeState();
            return;
        }
        
        if (!playerDetected)
        {
            currentState = State.Idle;
            IdleState();
            return;
        }
        
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        
        if (distToPlayer < shootRadius)
        {
            currentState = State.AimShoot;
            AimShootState();
        }
        else
        {
            currentState = State.Follow;
            FollowState(distToPlayer);
        }
    }
    
    private void IdleState()
    {
        // Slow drift (optional hover)
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 2f);
    }
    
    private void FollowState(float distToPlayer)
    {
        Vector3 flatDir = GetFlatDirToPlayer();
        Quaternion targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);

        AimPupilAtPlayer();
        
        // Move toward player, but maintain preferred distance
        float moveSpeed = followSpeed;
        if (distToPlayer < preferredDistance)
            moveSpeed *= 0.5f; // Slow down if too close
        
        Vector3 targetVel = flatDir * moveSpeed;
        targetVel.y = 0; // Keep level (optional: could add gentle bobbing)
        
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.deltaTime * 3f);
        rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxSpeed);
    }
    
    private void AimShootState()
    {
        Vector3 flatDir = GetFlatDirToPlayer();
        Quaternion targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed * 1.5f);

        AimPupilAtPlayer();
        
        // Slight movement (hovering)
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 5f);
    }

    private void AimPupilAtPlayer()
    {
        if (eyePupil == null || playerTransform == null)
            return;

        Vector3 targetPos = playerTransform.position + Vector3.up * 1.2f;
        Vector3 dir = targetPos - eyePupil.position;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        eyePupil.rotation = Quaternion.Slerp(eyePupil.rotation, targetRot, Time.deltaTime * pupilTurnSpeed);
    }
    
    private void UpdateShooting()
    {
        if (shootCooldownTimer > 0f)
            shootCooldownTimer -= Time.deltaTime;
        
        // Transition to charge state when ready to shoot
        if (currentState == State.AimShoot && shootCooldownTimer <= 0f && chargeTimer <= 0f)
        {
            currentState = State.Charge;
            chargeTimer = chargeTime;
        }
    }
    
    private void ChargeState()
    {
        Vector3 flatDir = GetFlatDirToPlayer();
        Quaternion targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed * 1.5f);
        AimPupilAtPlayer();
        
        // Hover
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 5f);
        
        // Initialize charge VFX on first frame of charging
        if (chargeVfx == null)
        {
            // Create gather/charge VFX at enemy position (cyan charge color)
            chargeParticles = RuntimeVFXFactory.CreateGatherEffect(transform, new Color(0.3f, 0.8f, 1f));
            if (chargeParticles != null)
                chargeVfx = chargeParticles.gameObject;
            
            // Pre-create the projectile but keep it hidden
            chargedProjectile = CreateChargeProjectile();
            
            // Create action line renderer
            chargeLine = gameObject.AddComponent<LineRenderer>();
            chargeLine.material = new Material(Shader.Find("Sprites/Default"));
            chargeLine.startColor = new Color(0.2f, 0.9f, 1f, 0.8f); // Cyan
            chargeLine.endColor = new Color(0.2f, 0.9f, 1f, 0.3f); // Fade out
            chargeLine.startWidth = 0.1f;
            chargeLine.endWidth = 0.15f;
            chargeLine.sortingOrder = -1;
        }
        
        // Update action line from pupil to projectile during charge
        if (chargeLine != null && chargedProjectile != null && eyePupil != null)
        {
            chargeLine.positionCount = 2;
            chargeLine.SetPosition(0, eyePupil.position);
            chargeLine.SetPosition(1, chargedProjectile.transform.position);
            
            // Pulse the line color with charge progress
            float chargeProgress = 1f - (chargeTimer / chargeTime);
            float intensity = 0.5f + Mathf.Sin(Time.time * 8f) * 0.5f * chargeProgress;
            chargeLine.startColor = new Color(0.2f, 0.9f, 1f, 0.8f * intensity);
            chargeLine.endColor = new Color(0.2f, 0.9f, 1f, 0.3f * intensity);
        }
        
        // Count down charge timer
        chargeTimer -= Time.deltaTime;
        if (chargeTimer <= 0f)
        {
            // Charge complete, release the projectile!
            if (chargedProjectile != null)
            {
                // Unhide and fire it
                MeshRenderer mr = chargedProjectile.GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.enabled = true;
                
                // Release the projectile with proper velocity
                Rigidbody projRb = chargedProjectile.GetComponent<Rigidbody>();
                if (projRb != null && playerTransform != null)
                {
                    Vector3 fireDir = (playerTransform.position - chargedProjectile.transform.position).normalized;
                    projRb.linearVelocity = fireDir * projectileSpeed;
                }

                // Play shot SFX
                if (sfxSource != null && shotSFX != null)
                    sfxSource.PlayOneShot(shotSFX, 0.9f);
                
                chargedProjectile = null;
            }
            
            // Clean up charge VFX
            if (chargeVfx != null)
            {
                Destroy(chargeVfx, 0.5f);
                chargeVfx = null;
                chargeParticles = null;
            }
            
            // Clean up action line
            if (chargeLine != null)
            {
                Destroy(chargeLine);
                chargeLine = null;
            }
            
            // Spawn muzzle flash at fire point
            Transform firePoint = eyePupil != null ? eyePupil : transform;
            if (muzzleFlashVfxPrefab != null)
            {
                Vector3 fireDir = (playerTransform.position - firePoint.position).normalized;
                GameObject muzzleVfx = Instantiate(muzzleFlashVfxPrefab, firePoint.position, Quaternion.LookRotation(fireDir));
                Destroy(muzzleVfx, 2f);
            }
            
            chargeTimer = 0f;
            currentState = State.AimShoot;
            shootCooldownTimer = fireCooldown;
        }
    }
    
    private GameObject CreateChargeProjectile()
    {
        Transform firePoint = eyePupil != null ? eyePupil : transform;
        Vector3 firePos = firePoint.position;
        Vector3 fireDir = firePoint.forward;
        if (playerTransform != null)
            fireDir = (playerTransform.position - firePos).normalized;
        
        // Create projectile
        GameObject projectileGO = new GameObject("ChargedProjectile");
        projectileGO.transform.position = firePos;
        projectileGO.transform.rotation = Quaternion.LookRotation(fireDir);
        
        // Add collider (sphere collider)
        SphereCollider collider = projectileGO.AddComponent<SphereCollider>();
        collider.radius = projectileSize * 1f; // 2x bigger
        collider.isTrigger = true;
        
        // Add rigidbody (velocity set to zero initially)
        Rigidbody projRb = projectileGO.AddComponent<Rigidbody>();
        projRb.useGravity = false;
        projRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        projRb.linearVelocity = Vector3.zero;
        
        // Add renderer using a temp sphere to get the mesh
        GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        MeshFilter mf = projectileGO.AddComponent<MeshFilter>();
        mf.sharedMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tempSphere);
        
        MeshRenderer mr = projectileGO.AddComponent<MeshRenderer>();
        mr.material = CreateProjectileChargeMaterial();
        mr.enabled = false; // Hidden until charge completes
        
        // Scale to size - small projectile
        projectileGO.transform.localScale = Vector3.one * projectileSize * 0.5f;
        
        // Add trail renderer for glowing tail effect
        TrailRenderer trail = projectileGO.AddComponent<TrailRenderer>();
        trail.time = 0.3f;
        trail.startWidth = projectileSize * 0.4f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Standard"));
        trail.material.SetColor("_Color", new Color(1f, 0.4f, 1f, 0.8f)); // Bright purple glow
        trail.material.SetColor("_EmissionColor", new Color(1f, 0.4f, 1f, 1f) * 2f);
        
        // Attach VFX prefab (scaled down to match projectile)
        if (projectileVfxPrefab != null)
        {
            GameObject vfxInstance = Instantiate(projectileVfxPrefab, projectileGO.transform);
            vfxInstance.transform.localPosition = Vector3.zero;
            vfxInstance.transform.localRotation = Quaternion.identity;
            vfxInstance.transform.localScale = Vector3.one * 0.05f; // Scale VFX way down
        }
        
        // Add damage component
        EnemyProjectile projScript = projectileGO.AddComponent<EnemyProjectile>();
        projScript.Initialize(projectileDamage, projectileLifetime);
        
        return projectileGO;
    }
    
    private void Shoot()
    {
        Transform firePoint = eyePupil != null ? eyePupil : transform;
        Vector3 firePos = firePoint.position;
        Vector3 fireDir = firePoint.forward;
        if (playerTransform != null)
            fireDir = (playerTransform.position - firePos).normalized;
        
        // Spawn muzzle flash VFX at fire point
        if (muzzleFlashVfxPrefab != null && ShieldHitVfxPrefab != null)
        {
            //Make the size of it smaller
            ShieldHitVfxPrefab.transform.localScale = Vector3.one * 0.1f;
            GameObject shieldHitVfx = Instantiate(ShieldHitVfxPrefab, firePos, Quaternion.LookRotation(fireDir));
            // Auto-destroy after 2 seconds

            GameObject muzzleVfx = Instantiate(muzzleFlashVfxPrefab, firePos, Quaternion.LookRotation(fireDir));
            // Auto-destroy after 2 seconds
            Destroy(muzzleVfx, 3f);
            Destroy(shieldHitVfx, 1.5f);

            
        }
        
        // Create projectile
        GameObject projectileGO = new GameObject("EnemyProjectile");
        projectileGO.transform.position = firePos;
        projectileGO.transform.rotation = Quaternion.LookRotation(fireDir);
        
        // Add collider
        SphereCollider collider = projectileGO.AddComponent<SphereCollider>();
        collider.radius = projectileSize * 0.5f;
        collider.isTrigger = true;
        
        // Add rigidbody
        Rigidbody projRb = projectileGO.AddComponent<Rigidbody>();
        projRb.useGravity = false;
        projRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        projRb.linearVelocity = fireDir * projectileSpeed;
        
        // Add renderer (simple sphere) - but hide it, let VFX show instead
        MeshFilter mf = projectileGO.AddComponent<MeshFilter>();
        mf.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        MeshRenderer mr = projectileGO.AddComponent<MeshRenderer>();
        mr.material = GetProjectileMaterial();
        mr.enabled = false; // Hide sphere, VFX will cover it
        
        // Scale to size
        projectileGO.transform.localScale = Vector3.one * projectileSize;
        
        // Add trail renderer for glowing tail effect
        TrailRenderer trail = projectileGO.AddComponent<TrailRenderer>();
        trail.time = 0.4f;
        trail.startWidth = projectileSize * 1.5f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Standard"));
        trail.material.SetColor("_Color", new Color(0.8f, 0.2f, 1f, 0.8f)); // Purple glow
        trail.material.SetColor("_EmissionColor", new Color(0.8f, 0.2f, 1f, 1f) * 2f);
        
        // Attach VFX prefab to projectile if assigned
        if (projectileVfxPrefab != null)
        {
            GameObject vfxInstance = Instantiate(projectileVfxPrefab, projectileGO.transform);
            vfxInstance.transform.localPosition = Vector3.zero;
            vfxInstance.transform.localRotation = Quaternion.identity;
        }
        
        // Add damage component
        EnemyProjectile projScript = projectileGO.AddComponent<EnemyProjectile>();
        projScript.Initialize(projectileDamage, projectileLifetime);
    }
    
    private Material GetProjectileMaterial()
    {
        // Try to find a URP material or create a shiny glowing one
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
            urpLit = Shader.Find("Standard");
        
        Material mat = new Material(urpLit);
        mat.color = new Color(0.8f, 0.2f, 1f, 1f); // Vibrant purple
        mat.SetFloat("_Metallic", 1f); // Full metallic for shiny mirror-like surface
        mat.SetFloat("_Smoothness", 1f); // Full smoothness for polish
        // Add emission for glow
        mat.SetColor("_EmissionColor", new Color(0.8f, 0.2f, 1f, 1f) * 2f); // Purple emission
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        return mat;
    }
    
    private Material CreateProjectileChargeMaterial()
    {
        // Shiny glowing material for charged projectile - looks energetic and powerful
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
            urpLit = Shader.Find("Standard");
        
        Material mat = new Material(urpLit);
        mat.color = new Color(1f, 0.4f, 1f, 1f); // Bright vibrant purple
        mat.SetFloat("_Metallic", 1f); // Full metallic for shiny appearance
        mat.SetFloat("_Smoothness", 1f); // Full smoothness for polish
        // Add emission for bright glow effect
        mat.SetColor("_EmissionColor", new Color(1f, 0.4f, 1f, 1f) * 3f); // Bright purple emission
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        return mat;
    }
    
    private void UpdateTailSway()
    {
        if (!hasTails || tailBones == null || tailBones.Length == 0)
            return;
        
        for (int i = 0; i < tailBones.Length; i++)
        {
            if (tailBones[i] == null) continue;
            
            float phase = Time.time * tailSwayFrequency + i * tailPhaseOffset;
            float yawOffset = Mathf.Sin(phase) * tailSwayAmplitude;
            float pitchOffset = Mathf.Cos(phase * 0.7f) * (tailSwayAmplitude * 0.5f);
            
            tailBones[i].localRotation = tailBaseRotations[i] * 
                Quaternion.Euler(pitchOffset, yawOffset, 0f);
        }
    }
    
    private void AutoFindEyePupil()
    {
        if (eyePupil != null)
            return;
        
        eyePupil = FindChildByNameHints(transform, pupilNameHints);
        
        if (eyePupil == null)
        {
            // Try to find "Flyer" child as fallback
            Transform flyer = transform.Find("Flyer");
            if (flyer != null)
                eyePupil = flyer;
            else
                eyePupil = transform;
            
            Debug.LogWarning($"[{gameObject.name}] Could not auto-find eyePupil. Using {eyePupil.name} as fire point.", gameObject);
        }
    }
    
    private void AutoFindTailBones()
    {
        if (tailBones != null && tailBones.Length > 0)
        {
            hasTails = true;
            CacheTailBaseRotations();
            return;
        }
        
        // Search all children for tail bones (handles both flat and nested hierarchies)
        Transform[] allChildren = GetComponentsInChildren<Transform>();
        System.Collections.Generic.List<(int number, Transform bone)> foundTails = 
            new System.Collections.Generic.List<(int, Transform)>();
        
        foreach (Transform child in allChildren)
        {
            if (child == transform) continue; // Skip self
            
            if (child.name.StartsWith(tailPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                int number = ParseTrailingNumber(child.name);
                foundTails.Add((number, child));
            }
        }
        
        if (foundTails.Count > 0)
        {
            // Sort by number (works for FlyerTail1, FlyerTail2, ... FlyerTail5)
            foundTails.Sort((a, b) => a.number.CompareTo(b.number));
            
            tailBones = new Transform[foundTails.Count];
            for (int i = 0; i < foundTails.Count; i++)
                tailBones[i] = foundTails[i].bone;
            
            hasTails = true;
            CacheTailBaseRotations();
            
            if (tailBones.Length > 0)
                Debug.Log($"[{gameObject.name}] Found {tailBones.Length} tail bones: {string.Join(", ", System.Array.ConvertAll(tailBones, t => t.name))}", gameObject);
        }
        else
        {
            hasTails = false;
            Debug.LogWarning($"[{gameObject.name}] No tail bones found with prefix '{tailPrefix}'.", gameObject);
        }
    }
    
    private void CacheTailBaseRotations()
    {
        if (tailBones == null || tailBones.Length == 0)
            return;
        
        tailBaseRotations = new Quaternion[tailBones.Length];
        for (int i = 0; i < tailBones.Length; i++)
        {
            if (tailBones[i] != null)
                tailBaseRotations[i] = tailBones[i].localRotation;
        }
    }
    
    private Transform FindChildByNameHints(Transform root, string[] hints)
    {
        if (hints == null || hints.Length == 0)
            return null;
        
        // First pass: exact match (case-insensitive)
        foreach (string hint in hints)
        {
            Transform found = FindChildExact(root, hint);
            if (found != null)
                return found;
        }
        
        // Second pass: partial match
        foreach (string hint in hints)
        {
            Transform found = FindChildContains(root, hint);
            if (found != null)
                return found;
        }
        
        return null;
    }
    
    private Transform FindChildExact(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }
        return null;
    }
    
    private Transform FindChildContains(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
        }
        return null;
    }
    
    private int ParseTrailingNumber(string name)
    {
        // Extract trailing number: "FlyerTail5" -> 5, "FlyerTail" -> 0
        int num = 0;
        int len = name.Length - 1;
        
        while (len >= 0 && char.IsDigit(name[len]))
        {
            len--;
        }
        
        if (len < name.Length - 1)
        {
            string numPart = name.Substring(len + 1);
            if (int.TryParse(numPart, out num))
                return num;
        }
        
        return 0;
    }
    
    protected override void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector3.zero;
        // Optional: play death animation, ragdoll, etc.
        Destroy(gameObject, 0.5f);
    }

    private Vector3 GetFlatDirToPlayer()
    {
        if (playerTransform == null) return transform.forward;

        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return transform.forward;
        return dir.normalized;
    }
}
