using UnityEngine;

public enum BulletSource
{
    Player,
    Enemy
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    [SerializeField] private BulletSource source = BulletSource.Player;
    [SerializeField] private Vector2 direction = Vector2.right;
    [SerializeField] private float speed = 16f;
    [SerializeField] private int damage = 1;
    [SerializeField] private bool isHyperbolic;
    [SerializeField] private float hyperbolicGravityScale = 1f;
    [SerializeField] private bool isPiercing;

    private Rigidbody2D body;
    private Vector2 worldVelocity;

    public BulletSource Source
    {
        get { return source; }
    }

    public Vector2 Direction
    {
        get { return NormalizeDirection(direction); }
    }

    public float Speed
    {
        get { return speed; }
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

    private void Awake()
    {
        CacheRigidbody();
        ConfigurePhysics();
        ApplyLayer();
        ResetVelocity();
        FaceVelocity();
    }

    private void OnEnable()
    {
        ConfigurePhysics();
        ApplyLayer();
        ResetVelocity();
        FaceVelocity();
    }

    private void FixedUpdate()
    {
        StepVelocity();
        FaceVelocity();
    }

    private void OnValidate()
    {
        direction = NormalizeDirection(direction);
        speed = Mathf.Max(0f, speed);
        damage = Mathf.Max(1, damage);
        hyperbolicGravityScale = Mathf.Max(0f, hyperbolicGravityScale);
        ConfigurePhysics();
        ApplyLayer();
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed)
    {
        Configure(bulletSource, bulletDirection, bulletSpeed, damage);
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed, int bulletDamage)
    {
        Configure(bulletSource, bulletDirection, bulletSpeed, bulletDamage, isHyperbolic, isPiercing);
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed, bool bulletIsHyperbolic)
    {
        Configure(bulletSource, bulletDirection, bulletSpeed, damage, bulletIsHyperbolic, isPiercing);
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed, int bulletDamage, bool bulletIsHyperbolic)
    {
        Configure(bulletSource, bulletDirection, bulletSpeed, bulletDamage, bulletIsHyperbolic, isPiercing);
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed, int bulletDamage, bool bulletIsHyperbolic, bool bulletIsPiercing)
    {
        source = bulletSource;
        direction = NormalizeDirection(bulletDirection);
        speed = Mathf.Max(0f, bulletSpeed);
        damage = Mathf.Max(1, bulletDamage);
        isHyperbolic = bulletIsHyperbolic;
        isPiercing = bulletIsPiercing;
        ConfigurePhysics();
        ApplyLayer();
        ResetVelocity();
        FaceVelocity();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider2D other)
    {
        if (other == null || !ShouldDestroyOnLayer(other.gameObject.layer))
        {
            return;
        }

        if (ShouldIgnoreDashingPlayer(other))
        {
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
        if (body == null)
        {
            return;
        }

        body.gravityScale = 0f;
        body.freezeRotation = true;
        ConfigureLayerCollisions();
    }

    private void ResetVelocity()
    {
        CacheRigidbody();
        if (body == null)
        {
            return;
        }

        worldVelocity = NormalizeDirection(direction) * speed;
        ApplyBodyVelocity();
    }

    private void StepVelocity()
    {
        CacheRigidbody();
        if (body == null)
        {
            return;
        }

        if (isHyperbolic)
        {
            worldVelocity += Physics2D.gravity * (hyperbolicGravityScale * GameTime.FixedDeltaTime);
        }
        else
        {
            worldVelocity = NormalizeDirection(direction) * speed;
        }

        ApplyBodyVelocity();
    }

    private void ApplyBodyVelocity()
    {
        body.velocity = worldVelocity * GameTime.WorldScale;
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
        int layer = LayerMask.NameToLayer(GetBulletLayerName(source));
        if (layer >= 0 && gameObject.layer != layer)
        {
            gameObject.layer = layer;
        }
    }

    private bool ShouldDestroyOnLayer(int layer)
    {
        if (source == BulletSource.Player)
        {
            return IsLayer(layer, GameLayers.Ground) || IsLayer(layer, GameLayers.Enemy);
        }

        return IsLayer(layer, GameLayers.Ground) || IsLayer(layer, GameLayers.Player);
    }

    private bool ShouldPierceTarget(Collider2D other)
    {
        return isPiercing && other != null && IsLayer(other.gameObject.layer, GameLayers.Enemy);
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
            EnemyController enemy = other.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }

            return;
        }

        PlayerRespawn playerRespawn = other.GetComponentInParent<PlayerRespawn>();
        if (playerRespawn != null)
        {
            playerRespawn.DieFromEnemy();
        }
    }

    private static void ConfigureLayerCollisions()
    {
        ConfigureBulletLayer(GameLayers.PlayerBullet, GameLayers.Ground, GameLayers.Enemy);
        ConfigureBulletLayer(GameLayers.EnemyBullet, GameLayers.Ground, GameLayers.Player);
    }

    private static void ConfigureBulletLayer(string bulletLayerName, string targetLayerNameA, string targetLayerNameB)
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

        EnableLayerCollision(bulletLayer, targetLayerNameA);
        EnableLayerCollision(bulletLayer, targetLayerNameB);
    }

    private static void EnableLayerCollision(int bulletLayer, string targetLayerName)
    {
        int targetLayer = LayerMask.NameToLayer(targetLayerName);
        if (targetLayer >= 0)
        {
            Physics2D.IgnoreLayerCollision(bulletLayer, targetLayer, false);
        }
    }

    private static bool IsLayer(int layer, string layerName)
    {
        return layer == LayerMask.NameToLayer(layerName);
    }

    private static Vector2 NormalizeDirection(Vector2 rawDirection)
    {
        if (rawDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector2.right;
        }

        return rawDirection.normalized;
    }

    private static string GetBulletLayerName(BulletSource bulletSource)
    {
        return bulletSource == BulletSource.Player ? GameLayers.PlayerBullet : GameLayers.EnemyBullet;
    }
}
