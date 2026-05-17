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
    [Header("Core")]
    [SerializeField] private EnemyMovementKind movementKind = EnemyMovementKind.Crawling;
    [SerializeField] private int maxHp = 3;
    [SerializeField] private bool destroyOnDeath = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Attack")]
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private Vector2 attackDirection = Vector2.left;
    [SerializeField] private float bulletSpeed = 10f;
    [SerializeField] private float attackCooldown = 1f;

    private Rigidbody2D body;
    private float initialGravityScale = 1f;
    private float nextAttackTime;
    private int currentHp;
    private bool dead;

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

    public bool IsAlive
    {
        get { return !dead; }
    }

    public bool CanAttack
    {
        get { return IsAlive && Time.time >= nextAttackTime; }
    }

    private void Awake()
    {
        CacheRigidbody();
        currentHp = maxHp;
        ApplyLayer();
        ApplyMovementKindPhysics();
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        bulletSpeed = Mathf.Max(0f, bulletSpeed);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        attackDirection = NormalizeDirection(attackDirection);
        ApplyLayer();
    }

    public void Move(Vector2 moveInput)
    {
        if (!IsAlive)
        {
            return;
        }

        CacheRigidbody();
        Vector2 normalizedInput = NormalizeMovementInput(moveInput);
        if (movementKind == EnemyMovementKind.Flying)
        {
            body.velocity = normalizedInput * moveSpeed;
            return;
        }

        body.velocity = new Vector2(normalizedInput.x * moveSpeed, body.velocity.y);
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
        nextAttackTime = Time.time + attackCooldown;
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
        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
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

    private void ApplyMovementKindPhysics()
    {
        if (body == null)
        {
            return;
        }

        body.gravityScale = movementKind == EnemyMovementKind.Flying ? 0f : initialGravityScale;
    }

    private void ApplyLayer()
    {
        int enemyLayer = LayerMask.NameToLayer(GameLayers.Enemy);
        if (enemyLayer >= 0)
        {
            gameObject.layer = enemyLayer;
        }
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
}
