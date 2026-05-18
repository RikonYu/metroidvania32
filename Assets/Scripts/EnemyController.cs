using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum EnemyMovementKind
{
    Crawling,
    Flying
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private EnemyMovementKind movementKind = EnemyMovementKind.Crawling;
    [SerializeField] private bool isBoss;
    [SerializeField] private bool isUnderwaterEnemy;
    [SerializeField] private int maxHp = 3;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private List<Vector2> patrolPoints = new List<Vector2>();

    [Header("Attack")]
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private Vector2 attackDirection = Vector2.left;
    [SerializeField] private float bulletSpeed = 10f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Melee Contact")]
    [SerializeField] private float normalEnemyKnockbackDistance = 3f;
    [SerializeField] private float knockbackDuration = 0.15f;

    private Rigidbody2D body;
    private Vector3 spawnPosition;
    private float initialGravityScale = 1f;
    private float nextAttackTime;
    private int currentHp;
    private bool dead;
    private bool initialized;

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
        get { return IsAlive && Time.time >= nextAttackTime; }
    }

    public float ContactKnockbackDistance
    {
        get { return isBoss ? 0f : normalEnemyKnockbackDistance; }
    }

    public float KnockbackDuration
    {
        get { return knockbackDuration; }
    }

    public IReadOnlyList<Vector2> PatrolPoints
    {
        get { return patrolPoints; }
    }

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void FixedUpdate()
    {
        ApplyMovementKindPhysics();
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        bulletSpeed = Mathf.Max(0f, bulletSpeed);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        normalEnemyKnockbackDistance = Mathf.Max(0f, normalEnemyKnockbackDistance);
        knockbackDuration = Mathf.Max(0.01f, knockbackDuration);
        attackDirection = NormalizeDirection(attackDirection);
        ApplyLayerAfterValidation();
    }

    public void Move(Vector2 moveInput)
    {
        if (!IsAlive)
        {
            return;
        }

        CacheRigidbody();
        Vector2 normalizedInput = NormalizeMovementInput(moveInput);
        float scaledMoveSpeed = moveSpeed * GameTime.WorldScale;
        if (movementKind == EnemyMovementKind.Flying)
        {
            body.velocity = normalizedInput * scaledMoveSpeed;
            return;
        }

        body.velocity = new Vector2(normalizedInput.x * scaledMoveSpeed, body.velocity.y);
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

        Bullet bullet = SpawnBullet();
        bullet.Configure(BulletSource.Enemy, NormalizeDirection(direction), bulletSpeed);
        nextAttackTime = Time.time + attackCooldown / Mathf.Max(0.01f, GameTime.WorldScale);
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
            if (enemy == null || enemy.isBoss || !IsSceneInstance(enemy))
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

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        CacheRigidbody();
        spawnPosition = transform.position;
        currentHp = maxHp;
        initialized = true;
        ApplyLayer();
        ApplyMovementKindPhysics();
    }

    private void ApplyMovementKindPhysics()
    {
        if (body == null)
        {
            return;
        }

        body.gravityScale = movementKind == EnemyMovementKind.Flying ? 0f : initialGravityScale * GameTime.WorldScale;
    }

    private void ApplyLayer()
    {
        GameLayers.ApplyTo(gameObject, GameLayers.Enemy);
    }

    private void ApplyLayerAfterValidation()
    {
        GameLayers.ApplyToAfterValidation(gameObject, GameLayers.Enemy);
    }

    private static Vector2 NormalizeMovementInput(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        return moveInput.normalized;
    }

    private static Vector2 NormalizeDirection(Vector2 rawDirection)
    {
        if (rawDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector2.left;
        }

        return rawDirection.normalized;
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

        WaterZone waterZone = other != null ? other.GetComponentInParent<WaterZone>() : null;
        if (waterZone != null && waterZone.KillNonUnderwaterEnemies)
        {
            Die();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleWaterContact(other);
        HandlePlayerContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleWaterContact(other);
        HandlePlayerContact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandlePlayerContact(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandlePlayerContact(collision.collider);
    }

    private static bool IsSceneInstance(EnemyController enemy)
    {
        Scene scene = enemy.gameObject.scene;
        return scene.IsValid();
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
