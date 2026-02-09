## Eyeball Shooter Enemy Setup

### Scripts Created
1. **EyeballShooterEnemy.cs** - Main shooter enemy with auto-detection, tail animation, and shooting
2. **EnemyProjectile.cs** - Projectile damage/collision system

### Prefab Setup

Create a new enemy prefab with this hierarchy:

```
ShootEnemy (root)
├── Flyer (main visual)
├── FlyerTail1
├── FlyerTail2
├── FlyerTail3
├── FlyerTail4
├── FlyerTail5
└── [eyeball/pupil object somewhere in hierarchy]
```

### Required Components on Root

1. **Collider** (Box/Capsule) - for enemy body collision detection
2. **Rigidbody** - will be created automatically by script if missing
   - Set to NOT use gravity (script does this)
   - Freeze rotation X/Z if desired (script sets freezeRotation=true)
3. **EyeballShooterEnemy** script

### Inspector Assignment (Optional)

These are **optional** - the script auto-detects them:

- **eyePupil**: Drag the eyeball/pupil child transform here
  - If not assigned, auto-searches for "EyePupil", "Pupil", "Iris", "Eyeball", etc.
  - Falls back to "Flyer" or root transform if not found

- **tailBones**: Drag the tail chain (FlyerTail1 through FlyerTail5)
  - If not assigned, auto-finds all children starting with "FlyerTail"
  - Automatically sorted by trailing number

### Key Parameters

**Movement**
- `followSpeed` (6f) - how fast it chases the player
- `maxSpeed` (8f) - speed cap
- `turnSpeed` (5f) - aiming smoothness
- `shootRadius` (15f) - distance at which it starts shooting
- `preferredDistance` (10f) - tries to maintain this distance

**Shooting**
- `fireCooldown` (1.5f) - delay between shots
- `projectileSpeed` (20f) - projectile velocity
- `projectileSize` (0.3f) - projectile scale
- `projectileDamage` (10) - damage per hit
- `projectileLifetime` (5f) - how long projectile lasts

**Tail Animation**
- `tailSwayAmplitude` (15f) - max rotation degrees
- `tailSwayFrequency` (2f) - wave speed
- `tailPhaseOffset` (0.3f) - per-bone phase difference

**VFX**
- `enableMuzzleFlash` (true) - spawn particle burst on shoot
- `muzzleFlashColor` - orange (1, 0.8, 0.3)

### How It Works

1. **Auto-Detection**: On Awake, finds eyePupil and tail bones by name hints
2. **Detection**: Checks player distance against detectionRadius/loseRadius
3. **States**:
   - **Idle**: Wait for player, hover with tail sway
   - **Follow**: Move toward player, aiming as it goes
   - **AimShoot**: Stop, aim precisely, fire
4. **Shooting**: Uses eyePupil.position and eyePupil.forward
   - Creates projectile sphere at runtime (no prefab needed)
   - Projectile auto-destroys on player hit or after lifetime
5. **Tail Sway**: Per-bone sin wave with phase offset = natural swimming motion

### Debugging

- If pupil not found: yellow warning in console, falls back to root
- If no tails found: silently disables tail animation
- Check console for what was auto-detected

### Performance Notes

- Projectiles are simple spheres (no fancy mesh)
- Muzzle flash is a quick ParticleSystem that auto-destructs
- Rigidbody movement uses Lerp (smooth, not physics forces)
- All code-generated, no asset dependencies
