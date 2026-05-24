using System.Collections.Generic;
using UnityEngine;

public enum EnemyMovementKind
{
    Crawling,
    Flying
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyController : MonoBehaviour
{
    private const int MaxSupportContacts = 8;

    [Header("Core")]
    [SerializeField] private EnemyMovementKind movementKind = EnemyMovementKind.Crawling;
    [SerializeField] private bool isBoss;
    [SerializeField] private bool isUnderwaterEnemy;
    [SerializeField] private int maxHp = 3;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float groundContactMinNormalY = 0.2f;
    [SerializeField] private LayerMask movementGroundMask;
    [SerializeField] private List<Vector2> patrolPoints = new List<Vector2>();

    [Header("Attack")]
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private Vector2 attackDirection = Vector2.left;
    [SerializeField] private float bulletSpeed = 10f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Melee Contact")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float normalEnemyKnockbackDistance = 3f;
    [SerializeField] private float knockbackDuration = 0.15f;

    [Header("Elemental Status")]
    [SerializeField] private bool isBurning;
    [SerializeField] private bool isFrozen;
    [SerializeField] private bool isPoisoned;
    [SerializeField] private bool isIceSlowed;

    private Rigidbody2D body;
    private Animator[] animators;
    private ContactFilter2D movementContactFilter;
    private readonly ContactPoint2D[] supportContacts = new ContactPoint2D[MaxSupportContacts];
    private readonly List<WaterZone> waterZones = new List<WaterZone>();
    private Collider2D currentGround;
    private Vector2 currentGroundNormal = Vector2.up;
    private Vector3 spawnPosition;
    private float initialGravityScale = 1f;
    private float attackCooldownRemaining;
    private int currentHp;
    private bool dead;
    private bool isGrounded;
    private bool initialized;
    private WaterZone currentWaterZone;
    private float burningTimeRemaining;
    private float burningDamagePerSecond;
    private float burningDamageAccumulator;
    private float freezeTimeRemaining;

    public EnemyMovementKind MovementKind
    {
        get { return movementKind; }
    }

    public int CurrentHp
    {
        get { return currentHp; }
    }

    public int MaxHp
    {
        get { return maxHp; }
    }

    public bool IsBoss
    {
        get { return isBoss; }
    }

    public bool IsUnderwaterEnemy
    {
        get { return isUnderwaterEnemy; }
    }

    public bool IsAlive
    {
        get { return !dead; }
    }

    public bool CanAttack
    {
        get { return IsAlive && !isFrozen && attackCooldownRemaining <= 0f; }
    }

    public float ContactKnockbackDistance
    {
        get { return isBoss ? 0f : normalEnemyKnockbackDistance; }
    }

    public int ContactDamage
    {
        get { return contactDamage; }
    }

    public float KnockbackDuration
    {
        get { return knockbackDuration; }
    }

    public IReadOnlyList<Vector2> PatrolPoints
    {
        get { return patrolPoints; }
    }

    public bool IsBurning
    {
        get { return isBurning; }
    }

    public bool IsFrozen
    {
        get { return isFrozen; }
    }

    public bool IsPoisoned
    {
        get { return isPoisoned; }
    }

    public bool IsIceSlowed
    {
        get { return isIceSlowed; }
    }

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void OnDisable()
    {
        waterZones.Clear();
        currentWaterZone = null;
    }

    private void Update()
    {
        UpdateElementalStatuses(Time.deltaTime);
        UpdateAttackCooldown(Time.deltaTime);
        ApplyAnimatorSpeed();
    }

    private void FixedUpdate()
    {
        bool wasGroundedOnSlope = isGrounded && SlopeMovement.IsSlopeNormal(currentGroundNormal);
        UpdateGroundSupport();
        PreventSlopeExitLaunch(wasGroundedOnSlope);
        ApplyMovementKindPhysics();
        if (isFrozen)
        {
            ApplyFrozenVelocityLock();
        }

        ApplyMovingPlatformMotion();
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        groundContactMinNormalY = Mathf.Clamp01(groundContactMinNormalY);
        bulletSpeed = Mathf.Max(0f, bulletSpeed);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        contactDamage = Mathf.Max(0, contactDamage);
        normalEnemyKnockbackDistance = Mathf.Max(0f, normalEnemyKnockbackDistance);
        knockbackDuration = Mathf.Max(0.01f, knockbackDuration);
        attackDirection = Utils.NormalizeOrFallback(attackDirection, Vector2.left);
        EnsureMovementLayerMask();
        UpdateMovementContactFilter();
        ApplyLayerAfterValidation();
    }

    public void Move(Vector2 moveInput)
    {
        if (!IsAlive)
        {
            return;
        }

        CacheRigidbody();
        if (isFrozen)
        {
            ApplyFrozenVelocityLock();
            return;
        }

        Vector2 normalizedInput = Utils.NormalizeOrZero(moveInput);
        float scaledMoveSpeed = moveSpeed * GameTime.WorldScale * GetEffectiveTimeScale();
        if (movementKind == EnemyMovementKind.Flying)
        {
            body.velocity = normalizedInput * scaledMoveSpeed;
            return;
        }

        UpdateGroundSupport();
        float horizontalSpeed = normalizedInput.x * scaledMoveSpeed;
        if (isGrounded && SlopeMovement.IsSlopeNormal(currentGroundNormal))
        {
            body.velocity = SlopeMovement.GetSurfaceVelocityForHorizontalSpeed(horizontalSpeed, currentGroundNormal);
            return;
        }

        body.velocity = new Vector2(horizontalSpeed, body.velocity.y);
    }

    public void StopMoving()
    {
        if (body == null)
        {
            return;
        }

        if (movementKind == EnemyMovementKind.Flying)
        {
            body.velocity = Vector2.zero;
            return;
        }

        UpdateGroundSupport();
        if (isGrounded && SlopeMovement.IsSlopeNormal(currentGroundNormal))
        {
            body.velocity = SlopeMovement.GetSurfaceVelocityForHorizontalSpeed(0f, currentGroundNormal);
            return;
        }

        body.velocity = new Vector2(0f, body.velocity.y);
    }

    public bool TryAttack()
    {
        return TryAttack(attackDirection);
    }

    public bool TryAttack(Vector2 direction)
    {
        if (!CanAttack)
        {
            return false;
        }

        if (!FireBullet(direction))
        {
            return false;
        }

        attackCooldownRemaining = attackCooldown;
        return true;
    }

    public bool FireBullet(Vector2 direction)
    {
        if (!IsAlive || isFrozen)
        {
            return false;
        }

        Bullet bullet = SpawnBullet();
        bullet.Configure(BulletSource.Enemy, Utils.NormalizeOrFallback(direction, Vector2.left), bulletSpeed);
        return true;
    }

    public void TakeDamage(int damage)
    {
        if (!IsAlive || damage <= 0)
        {
            return;
        }

        currentHp = Mathf.Max(0, currentHp - damage);
        if (currentHp <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive || amount <= 0)
        {
            return;
        }

        currentHp = Mathf.Min(maxHp, currentHp + amount);
    }

    public void RestoreHpToFull()
    {
        if (IsAlive)
        {
            currentHp = maxHp;
        }
    }

    public void ApplyBurning(int sourceDamage)
    {
        if (!IsAlive || sourceDamage <= 0)
        {
            return;
        }

        isBurning = true;
        burningTimeRemaining = Consts.BurningDuration;
        burningDamagePerSecond = Mathf.Max(burningDamagePerSecond, sourceDamage * Consts.BurningDamagePerSecondRatio);
    }

    public void ApplyFrozenOrSlowed()
    {
        if (!IsAlive)
        {
            return;
        }

        if (isBoss)
        {
            isIceSlowed = true;
            freezeTimeRemaining = Consts.FreezeDuration;
            ApplyAnimatorSpeed();
            return;
        }

        isFrozen = true;
        freezeTimeRemaining = Consts.FreezeDuration;
        ApplyFrozenVelocityLock();
        ApplyAnimatorSpeed();
    }

    public void ApplyPoisoned()
    {
        if (IsAlive)
        {
            isPoisoned = true;
        }
    }

    public void Die()
    {
        if (dead)
        {
            return;
        }

        dead = true;
        StopMoving();

        if (isBoss)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void Respawn()
    {
        if (isBoss)
        {
            return;
        }

        InitializeIfNeeded();
        dead = false;
        currentHp = maxHp;
        ClearElementalStatuses();
        transform.position = spawnPosition;
        gameObject.SetActive(true);
        ApplyLayer();
        ApplyMovementKindPhysics();
        StopMoving();
    }

    public void AddPatrolPoint(Vector2 worldPoint)
    {
        patrolPoints.Add(worldPoint);
    }

    public void SetPatrolPoint(int index, Vector2 worldPoint)
    {
        if (index < 0 || index >= patrolPoints.Count)
        {
            return;
        }

        patrolPoints[index] = worldPoint;
    }

    public void RemovePatrolPointAt(int index)
    {
        if (index < 0 || index >= patrolPoints.Count)
        {
            return;
        }

        patrolPoints.RemoveAt(index);
    }

    public void ClearPatrolPoints()
    {
        patrolPoints.Clear();
    }

    public static void RespawnNonBossEnemies()
    {
        EnemyController[] enemies = Resources.FindObjectsOfTypeAll<EnemyController>();
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy == null || enemy.isBoss || !Utils.IsSceneInstance(enemy.gameObject))
            {
                continue;
            }

            enemy.Respawn();
        }
    }

    private Bullet SpawnBullet()
    {
        Vector3 spawnPosition = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;
        if (bulletPrefab != null)
        {
            return Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
        }

        GameObject bulletObject = new GameObject("EnemyBullet");
        bulletObject.transform.position = spawnPosition;
        bulletObject.AddComponent<Rigidbody2D>();
        CircleCollider2D collider2D = bulletObject.AddComponent<CircleCollider2D>();
        collider2D.isTrigger = true;
        return bulletObject.AddComponent<Bullet>();
    }

    private void CacheRigidbody()
    {
        if (body != null)
        {
            return;
        }

        body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            initialGravityScale = body.gravityScale;
            body.freezeRotation = true;
        }
    }

    private void CacheAnimators()
    {
        animators = GetComponentsInChildren<Animator>(true);
    }

    private void ApplyAnimatorSpeed()
    {
        if (animators == null || animators.Length == 0)
        {
            CacheAnimators();
        }

        if (animators == null)
        {
            return;
        }

        float animatorSpeed = IsAlive ? GameTime.WorldScale * GetEffectiveTimeScale() : 0f;
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].speed = animatorSpeed;
            }
        }
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        CacheRigidbody();
        CacheAnimators();
        EnsureMovementLayerMask();
        UpdateMovementContactFilter();
        spawnPosition = transform.position;
        currentHp = maxHp;
        ClearElementalStatuses();
        initialized = true;
        ApplyLayer();
        ApplyMovementKindPhysics();
        UpdateGroundSupport();
    }

    private void ApplyMovementKindPhysics()
    {
        if (body == null)
        {
            return;
        }

        if (isFrozen)
        {
            body.gravityScale = currentWaterZone != null
                ? initialGravityScale * Consts.FrozenWaterBuoyancyGravityScale * GameTime.WorldScale
                : initialGravityScale * GameTime.WorldScale;
            return;
        }

        float effectiveScale = GameTime.WorldScale * GetEffectiveTimeScale();
        body.gravityScale = movementKind == EnemyMovementKind.Flying ? 0f : initialGravityScale * effectiveScale;
    }

    private void UpdateGroundSupport()
    {
        if (movementKind != EnemyMovementKind.Crawling)
        {
            currentGround = null;
            isGrounded = false;
            currentGroundNormal = Vector2.up;
            return;
        }

        GroundSupport support;
        if (SlopeMovement.TryFindSupport(
            body,
            movementContactFilter,
            supportContacts,
            groundContactMinNormalY,
            transform.position.y,
            movementGroundMask,
            out support))
        {
            currentGround = support.Collider;
            isGrounded = true;
            currentGroundNormal = support.Normal;
            return;
        }

        currentGround = null;
        isGrounded = false;
        currentGroundNormal = Vector2.up;
    }

    private void ApplyMovingPlatformMotion()
    {
        if (body == null || !isGrounded)
        {
            return;
        }

        MovingPlatform movingPlatform = currentGround != null ? currentGround.GetComponentInParent<MovingPlatform>() : null;
        if (movingPlatform == null || movingPlatform.CurrentDelta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        body.position += movingPlatform.CurrentDelta;
    }

    private void PreventSlopeExitLaunch(bool wasGroundedOnSlope)
    {
        if (!wasGroundedOnSlope || isGrounded || body == null || body.velocity.y <= 0f)
        {
            return;
        }

        body.velocity = new Vector2(body.velocity.x, 0f);
    }

    private void UpdateElementalStatuses(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        UpdateBurning(deltaTime);
        UpdateFrozenOrSlowed(deltaTime);
    }

    private void UpdateBurning(float deltaTime)
    {
        if (!isBurning)
        {
            return;
        }

        burningTimeRemaining -= deltaTime;
        burningDamageAccumulator += burningDamagePerSecond * deltaTime;
        int damageToApply = Mathf.FloorToInt(burningDamageAccumulator);
        if (damageToApply > 0)
        {
            burningDamageAccumulator -= damageToApply;
            TakeDamage(damageToApply);
        }

        if (burningTimeRemaining <= 0f || !IsAlive)
        {
            isBurning = false;
            burningTimeRemaining = 0f;
            burningDamagePerSecond = 0f;
            burningDamageAccumulator = 0f;
        }
    }

    private void UpdateFrozenOrSlowed(float deltaTime)
    {
        if (!isFrozen && !isIceSlowed)
        {
            return;
        }

        freezeTimeRemaining -= deltaTime;
        if (freezeTimeRemaining > 0f)
        {
            return;
        }

        isFrozen = false;
        isIceSlowed = false;
        freezeTimeRemaining = 0f;
        ApplyAnimatorSpeed();
    }

    private void UpdateAttackCooldown(float deltaTime)
    {
        if (attackCooldownRemaining <= 0f)
        {
            return;
        }

        float scaledDeltaTime = deltaTime * GameTime.WorldScale * GetEffectiveTimeScale();
        if (scaledDeltaTime <= 0f)
        {
            return;
        }

        attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining - scaledDeltaTime);
    }

    private void ApplyFrozenVelocityLock()
    {
        if (body != null)
        {
            body.velocity = new Vector2(0f, body.velocity.y);
        }
    }

    private float GetEffectiveTimeScale()
    {
        if (isFrozen)
        {
            return 0f;
        }

        return isIceSlowed ? Consts.BossFreezeSlowScale : 1f;
    }

    private void ClearElementalStatuses()
    {
        isBurning = false;
        isFrozen = false;
        isPoisoned = false;
        isIceSlowed = false;
        burningTimeRemaining = 0f;
        burningDamagePerSecond = 0f;
        burningDamageAccumulator = 0f;
        freezeTimeRemaining = 0f;
        ApplyAnimatorSpeed();
    }

    private void ApplyLayer()
    {
        GameLayers.ApplyTo(gameObject, GameLayers.Enemy);
    }

    private void ApplyLayerAfterValidation()
    {
        GameLayers.ApplyToAfterValidation(gameObject, GameLayers.Enemy);
    }

    private void EnsureMovementLayerMask()
    {
        if (movementGroundMask == 0)
        {
            movementGroundMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Platform);
        }
    }

    private void UpdateMovementContactFilter()
    {
        movementContactFilter.useTriggers = false;
        movementContactFilter.SetLayerMask(movementGroundMask);
    }

    private void HandlePlayerContact(Collider2D other)
    {
        if (!IsAlive || other == null)
        {
            return;
        }

        PlayerRespawn playerRespawn = other.GetComponentInParent<PlayerRespawn>();
        if (playerRespawn == null)
        {
            return;
        }

        Vector2 knockbackDirection = playerRespawn.transform.position - transform.position;
        playerRespawn.TakeEnemyMeleeHit(this, knockbackDirection);
    }

    private void HandleWaterContact(Collider2D other)
    {
        if (!IsAlive || isUnderwaterEnemy)
        {
            return;
        }

        WaterZone waterZone = Utils.GetWaterZone(other);
        if (waterZone != null && waterZone.KillNonUnderwaterEnemies)
        {
            Die();
        }
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnterWater(Utils.GetWaterZone(other));
        HandleWaterContact(other);
        HandlePlayerContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        EnterWater(Utils.GetWaterZone(other));
        HandleWaterContact(other);
        HandlePlayerContact(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        ExitWater(Utils.GetWaterZone(other));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandlePlayerContact(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandlePlayerContact(collision.collider);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isBoss ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        Gizmos.color = Color.cyan;
        for (int i = 0; i < patrolPoints.Count; i++)
        {
            Vector3 point = patrolPoints[i];
            Gizmos.DrawWireSphere(point, 0.25f);

            if (i > 0)
            {
                Gizmos.DrawLine(patrolPoints[i - 1], point);
            }
        }
    }
}
