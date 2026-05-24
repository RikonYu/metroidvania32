using System.Collections.Generic;
using UnityEngine;

public enum BulletSource
{
    Player,
    Enemy
}

public enum BulletElement
{
    None,
    Fire,
    Ice,
    Poison
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    [SerializeField] private BulletSource source = BulletSource.Player;
    [SerializeField] private Vector2 direction = Vector2.right;
    [SerializeField] private float speed = 16f;
    [SerializeField] private float maxRange;
    [SerializeField] private int damage = 1;
    [SerializeField] private bool isHyperbolic;
    [SerializeField] private float hyperbolicGravityScale = 1f;
    [SerializeField] private bool isPiercing;
    [SerializeField] private bool isCharged;
    [SerializeField] private BulletElement elemental = BulletElement.None;
    [SerializeField] private float explosionRadius = 2f;

    private Rigidbody2D body;
    private Vector2 worldVelocity;
    private Vector2 rangeOrigin;
    private readonly List<WaterZone> waterZones = new List<WaterZone>();
    private WaterZone currentWaterZone;

    public BulletSource Source
    {
        get { return source; }
    }

    public Vector2 Direction
    {
        get { return Utils.NormalizeOrFallback(direction, Vector2.right); }
    }

    public float Speed
    {
        get { return speed; }
    }

    public float MaxRange
    {
        get { return maxRange; }
    }

    public int Damage
    {
        get { return damage; }
    }

    public bool IsHyperbolic
    {
        get { return isHyperbolic; }
    }

    public bool IsPiercing
    {
        get { return isPiercing; }
    }

    public bool IsCharged
    {
        get { return isCharged; }
    }

    public BulletElement Elemental
    {
        get { return elemental; }
    }

    public float ExplosionRadius
    {
        get { return explosionRadius; }
    }

    public Vector2 WorldVelocity
    {
        get { return worldVelocity; }
    }

    protected virtual void Awake()
    {
        CacheRigidbody();
        ConfigurePhysics();
        ApplyLayer();
        ResetRangeOrigin();
        ResetVelocity();
        FaceVelocity();
    }

    protected virtual void OnEnable()
    {
        ConfigurePhysics();
        ApplyLayer();
        ResetRangeOrigin();
        ResetVelocity();
        FaceVelocity();
    }

    protected virtual void FixedUpdate()
    {
        if (DestroyIfPastMaxRange())
        {
            return;
        }

        StepVelocity();
        FaceVelocity();
    }

    protected virtual void OnDisable()
    {
        waterZones.Clear();
        currentWaterZone = null;
    }

    protected virtual void OnValidate()
    {
        direction = Utils.NormalizeOrFallback(direction, Vector2.right);
        speed = Mathf.Max(0f, speed);
        maxRange = Mathf.Max(0f, maxRange);
        damage = Mathf.Max(1, damage);
        hyperbolicGravityScale = Mathf.Max(0f, hyperbolicGravityScale);
        explosionRadius = Mathf.Max(0f, explosionRadius);
        ApplyChargedElementRules();
        ConfigurePhysics();
        ApplyLayerAfterValidation();
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection)
    {
        Configure(bulletSource, bulletDirection, speed, isHyperbolic, isPiercing);
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed)
    {
        Configure(bulletSource, bulletDirection, bulletSpeed, isHyperbolic, isPiercing);
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed, bool bulletIsHyperbolic)
    {
        Configure(bulletSource, bulletDirection, bulletSpeed, bulletIsHyperbolic, isPiercing);
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed, bool bulletIsHyperbolic, bool bulletIsPiercing)
    {
        Configure(bulletSource, bulletDirection, bulletSpeed, bulletIsHyperbolic, bulletIsPiercing, elemental, isCharged);
    }

    public virtual void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed, bool bulletIsHyperbolic, bool bulletIsPiercing, BulletElement bulletElement, bool bulletIsCharged)
    {
        source = bulletSource;
        direction = Utils.NormalizeOrFallback(bulletDirection, Vector2.right);
        speed = Mathf.Max(0f, bulletSpeed);
        isHyperbolic = bulletIsHyperbolic;
        isPiercing = bulletIsPiercing;
        elemental = bulletElement;
        isCharged = bulletIsCharged;
        ApplyChargedElementRules();
        ConfigurePhysics();
        ApplyLayer();
        ResetRangeOrigin();
        ResetVelocity();
        FaceVelocity();
    }

    protected void SetElemental(BulletElement bulletElement)
    {
        elemental = bulletElement;
        ApplyChargedElementRules();
    }

    public void SetWorldVelocity(Vector2 velocity)
    {
        CacheRigidbody();
        worldVelocity = velocity;
        speed = velocity.magnitude;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            direction = velocity.normalized;
        }

        ApplyBodyVelocity();
        FaceVelocity();
    }

    public void AddWorldVelocity(Vector2 velocity)
    {
        SetWorldVelocity(worldVelocity + velocity);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnterWater(Utils.GetWaterZone(other));
        HandleHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        EnterWater(Utils.GetWaterZone(other));
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        ExitWater(Utils.GetWaterZone(other));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (DestroyIfPastMaxRange())
        {
            return;
        }

        if (TryUnlockArrowLock(other))
        {
            Destroy(gameObject);
            return;
        }

        if (!ShouldDestroyOnLayer(other.gameObject.layer))
        {
            return;
        }

        if (ShouldIgnoreDashingPlayer(other))
        {
            return;
        }

        if (ShouldExplodeOnHit(other))
        {
            Explode(other);
            Destroy(gameObject);
            return;
        }

        ApplyHitEffect(other);
        if (ShouldPierceTarget(other))
        {
            IgnoreCollisionWith(other);
            return;
        }

        Destroy(gameObject);
    }

    private void IgnoreCollisionWith(Collider2D other)
    {
        Collider2D bulletCollider = GetComponent<Collider2D>();
        if (bulletCollider != null && other != null)
        {
            Physics2D.IgnoreCollision(bulletCollider, other, true);
        }
    }

    private void CacheRigidbody()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void ConfigurePhysics()
    {
        ApplyChargedElementRules();
        if (body == null)
        {
            return;
        }

        body.gravityScale = 0f;
        body.freezeRotation = true;
        ConfigureLayerCollisions();
    }

    protected void ApplyChargedElementRules()
    {
        if (isCharged)
        {
            isPiercing = elemental != BulletElement.Fire;
        }
    }

    private void ResetVelocity()
    {
        CacheRigidbody();
        if (body == null)
        {
            return;
        }

        worldVelocity = Utils.NormalizeOrFallback(direction, Vector2.right) * speed;
        ApplyBodyVelocity();
    }

    private void ResetRangeOrigin()
    {
        CacheRigidbody();
        rangeOrigin = body != null ? body.position : (Vector2)transform.position;
    }

    private void StepVelocity()
    {
        CacheRigidbody();
        if (body == null)
        {
            return;
        }

        RefreshWaterZone();

        if (isHyperbolic)
        {
            worldVelocity += Physics2D.gravity * (hyperbolicGravityScale * GameTime.FixedDeltaTime);
        }

        ApplyWaterDrag();
        ApplyBodyVelocity();
    }

    private void ApplyWaterDrag()
    {
        if (currentWaterZone == null || worldVelocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        worldVelocity -= worldVelocity * Mathf.Clamp01(currentWaterZone.BulletDrag * GameTime.FixedDeltaTime);
    }

    private void ApplyBodyVelocity()
    {
        body.velocity = worldVelocity * GameTime.WorldScale;
    }

    private bool DestroyIfPastMaxRange()
    {
        if (maxRange <= 0f)
        {
            return false;
        }

        Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
        if ((currentPosition - rangeOrigin).sqrMagnitude <= maxRange * maxRange)
        {
            return false;
        }

        Destroy(gameObject);
        return true;
    }

    private void RefreshWaterZone()
    {
        Vector2 position = body != null ? body.position : (Vector2)transform.position;
        WaterZone positionWaterZone = WaterZone.GetZoneAtPoint(position);
        if (positionWaterZone != null)
        {
            EnterWater(positionWaterZone);
            return;
        }

        for (int i = waterZones.Count - 1; i >= 0; i--)
        {
            if (waterZones[i] == null || !waterZones[i].isActiveAndEnabled)
            {
                waterZones.RemoveAt(i);
            }
        }

        currentWaterZone = waterZones.Count > 0 ? waterZones[waterZones.Count - 1] : null;
    }

    private void FaceVelocity()
    {
        CacheRigidbody();
        if (body == null || body.velocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 velocity = body.velocity;
        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void ApplyLayer()
    {
        GameLayers.ApplyTo(gameObject, GetBulletLayerName(source));
    }

    private void ApplyLayerAfterValidation()
    {
        GameLayers.ApplyToAfterValidation(gameObject, GetBulletLayerName(source));
    }

    private bool ShouldDestroyOnLayer(int layer)
    {
        if (source == BulletSource.Player)
        {
            return Utils.IsTerrainLayer(layer) || Utils.IsLayer(layer, GameLayers.Enemy);
        }

        return Utils.IsTerrainLayer(layer) || Utils.IsLayer(layer, GameLayers.Player);
    }

    private bool ShouldPierceTarget(Collider2D other)
    {
        return isPiercing && IsEnemyTarget(other);
    }

    private void EnterWater(WaterZone waterZone)
    {
        if (waterZone == null)
        {
            return;
        }

        if (!waterZones.Contains(waterZone))
        {
            waterZones.Add(waterZone);
        }

        currentWaterZone = waterZone;
    }

    private void ExitWater(WaterZone waterZone)
    {
        if (waterZone == null)
        {
            return;
        }

        waterZones.Remove(waterZone);
        currentWaterZone = waterZones.Count > 0 ? waterZones[waterZones.Count - 1] : null;
    }

    private bool ShouldIgnoreDashingPlayer(Collider2D other)
    {
        if (source != BulletSource.Enemy)
        {
            return false;
        }

        MCController player = other.GetComponentInParent<MCController>();
        if (player == null || !player.IsDashing)
        {
            return false;
        }

        IgnoreCollisionWith(other);
        return true;
    }

    private void ApplyHitEffect(Collider2D other)
    {
        if (source == BulletSource.Player)
        {
            EnemyController enemy = Utils.GetEnemyTarget(other);
            if (enemy != null)
            {
                ApplyHitEffect(enemy);
            }

            return;
        }

        PlayerRespawn playerRespawn = Utils.GetPlayerTarget(other);
        if (playerRespawn != null)
        {
            ApplyHitEffect(playerRespawn);
        }
    }

    private void ApplyHitEffect(EnemyController enemy)
    {
        ApplyHitEffect(enemy, true);
    }

    private void ApplyHitEffect(EnemyController enemy, bool applyElementalStatus)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.TakeDamage(damage);
        if (!applyElementalStatus)
        {
            return;
        }

        switch (elemental)
        {
            case BulletElement.Fire:
                enemy.ApplyBurning(damage);
                break;
            case BulletElement.Ice:
                enemy.ApplyFrozenOrSlowed();
                break;
            case BulletElement.Poison:
                enemy.ApplyPoisoned();
                break;
        }
    }

    private void ApplyHitEffect(PlayerRespawn playerRespawn)
    {
        ApplyHitEffect(playerRespawn, true);
    }

    private void ApplyHitEffect(PlayerRespawn playerRespawn, bool applyElementalStatus)
    {
        if (playerRespawn == null)
        {
            return;
        }

        if (!playerRespawn.TakeDamageFromEnemy(damage))
        {
            return;
        }

        if (applyElementalStatus)
        {
            switch (elemental)
            {
                case BulletElement.Fire:
                    playerRespawn.ApplyBurning(damage);
                    break;
                case BulletElement.Ice:
                    playerRespawn.ApplyFrozen();
                    break;
                case BulletElement.Poison:
                    playerRespawn.ApplyPoisoned();
                    break;
            }
        }
    }

    private bool ShouldExplodeOnHit(Collider2D other)
    {
        return isCharged && elemental == BulletElement.Fire && other != null && (IsEnemyTarget(other) || Utils.IsTerrain(other));
    }

    protected virtual void Explode(Collider2D firstHit)
    {
        Vector2 center = GetExplosionCenter(firstHit);
        ApplyExplosionToFireLocks(center);

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius, GetEnemyTargetMask());
        if (source == BulletSource.Player)
        {
            ApplyExplosionToEnemies(hits);
            return;
        }

        ApplyExplosionToPlayers(hits);
    }

    private bool TryUnlockArrowLock(Collider2D other)
    {
        ArrowLock arrowLock = other != null ? other.GetComponentInParent<ArrowLock>() : null;
        if (arrowLock == null)
        {
            return false;
        }

        arrowLock.UnlockFromArrow(this);
        return arrowLock.IsUnlocked;
    }

    private void ApplyExplosionToFireLocks(Vector2 center)
    {
        if (source != BulletSource.Player || !isCharged || elemental != BulletElement.Fire)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius);
        HashSet<FireLock> affectedLocks = new HashSet<FireLock>();
        for (int i = 0; i < hits.Length; i++)
        {
            FireLock fireLock = hits[i] != null ? hits[i].GetComponentInParent<FireLock>() : null;
            if (fireLock != null && affectedLocks.Add(fireLock))
            {
                fireLock.UnlockFromChargedFireExplosion(this);
            }
        }
    }

    private void ApplyExplosionToEnemies(Collider2D[] hits)
    {
        HashSet<EnemyController> affectedEnemies = new HashSet<EnemyController>();
        for (int i = 0; i < hits.Length; i++)
        {
            EnemyController enemy = Utils.GetEnemyTarget(hits[i]);
            if (enemy != null && affectedEnemies.Add(enemy))
            {
                ApplyHitEffect(enemy, false);
            }
        }
    }

    private void ApplyExplosionToPlayers(Collider2D[] hits)
    {
        HashSet<PlayerRespawn> affectedPlayers = new HashSet<PlayerRespawn>();
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerRespawn playerRespawn = Utils.GetPlayerTarget(hits[i]);
            if (playerRespawn != null && affectedPlayers.Add(playerRespawn))
            {
                ApplyHitEffect(playerRespawn, false);
            }
        }
    }

    protected Vector2 GetExplosionCenter(Collider2D firstHit)
    {
        Vector2 fallback = body != null ? body.position : (Vector2)transform.position;
        if (firstHit == null)
        {
            return fallback;
        }

        return firstHit.ClosestPoint(fallback);
    }

    private int GetEnemyTargetMask()
    {
        return source == BulletSource.Player ? LayerMask.GetMask(GameLayers.Enemy) : LayerMask.GetMask(GameLayers.Player);
    }

    private bool IsEnemyTarget(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (source == BulletSource.Player)
        {
            return Utils.IsLayer(other.gameObject.layer, GameLayers.Enemy) || Utils.GetEnemyTarget(other) != null;
        }

        return Utils.IsLayer(other.gameObject.layer, GameLayers.Player) || Utils.GetPlayerTarget(other) != null;
    }

    private static void ConfigureLayerCollisions()
    {
        ConfigureBulletLayer(GameLayers.PlayerBullet, GameLayers.Ground, GameLayers.Obstacle, GameLayers.Platform, GameLayers.Trigger, GameLayers.Enemy, GameLayers.Water);
        ConfigureBulletLayer(GameLayers.EnemyBullet, GameLayers.Ground, GameLayers.Obstacle, GameLayers.Platform, GameLayers.Player, GameLayers.Water);
    }

    private static void ConfigureBulletLayer(string bulletLayerName, params string[] targetLayerNames)
    {
        int bulletLayer = LayerMask.NameToLayer(bulletLayerName);
        if (bulletLayer < 0)
        {
            return;
        }

        for (int layer = 0; layer < 32; layer++)
        {
            Physics2D.IgnoreLayerCollision(bulletLayer, layer, true);
        }

        for (int i = 0; i < targetLayerNames.Length; i++)
        {
            EnableLayerCollision(bulletLayer, targetLayerNames[i]);
        }
    }

    private static void EnableLayerCollision(int bulletLayer, string targetLayerName)
    {
        int targetLayer = LayerMask.NameToLayer(targetLayerName);
        if (targetLayer >= 0)
        {
            Physics2D.IgnoreLayerCollision(bulletLayer, targetLayer, false);
        }
    }

    private static string GetBulletLayerName(BulletSource bulletSource)
    {
        return bulletSource == BulletSource.Player ? GameLayers.PlayerBullet : GameLayers.EnemyBullet;
    }
}
