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

    private Rigidbody2D body;

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

    private void Awake()
    {
        CacheRigidbody();
        ConfigurePhysics();
        ApplyLayer();
        ApplyVelocity();
    }

    private void OnEnable()
    {
        ApplyLayer();
        ApplyVelocity();
    }

    private void FixedUpdate()
    {
        ApplyVelocity();
    }

    private void OnValidate()
    {
        direction = NormalizeDirection(direction);
        speed = Mathf.Max(0f, speed);
        ApplyLayer();
    }

    public void Configure(BulletSource bulletSource, Vector2 bulletDirection, float bulletSpeed)
    {
        source = bulletSource;
        direction = NormalizeDirection(bulletDirection);
        speed = Mathf.Max(0f, bulletSpeed);
        ApplyLayer();
        ApplyVelocity();
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

        Destroy(gameObject);
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

    private void ApplyVelocity()
    {
        CacheRigidbody();
        if (body == null)
        {
            return;
        }

        body.velocity = NormalizeDirection(direction) * speed;
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
