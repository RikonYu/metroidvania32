using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class IceObstacle : MonoBehaviour
{
    private const float SurfaceEpsilon = 0.0001f;
    private const float SwirlHoldTime = 0.05f;

    private static IceObstacle activePlayerGeneratedIceObstacle;

    [Header("Source")]
    [SerializeField] private bool isFromPlayer;

    [Header("Float")]
    [SerializeField] private float riseAcceleration = 8f;
    [SerializeField] private float maxRiseSpeed = 4f;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 5f;

    private Rigidbody2D body;
    private Collider2D obstacleCollider;
    private Vector2 worldVelocity;
    private float riseSpeed;
    private float lifetimeRemaining;
    private float verticalSwirlTimer;
    private float horizontalSwirlTimer;

    public bool IsFromPlayer
    {
        get { return isFromPlayer; }
    }

    public Vector2 WorldVelocity
    {
        get { return worldVelocity; }
    }

    public static IceObstacle ActivePlayerGeneratedIceObstacle
    {
        get { return activePlayerGeneratedIceObstacle; }
    }

    private void Awake()
    {
        CacheComponents();
        ConfigurePhysics();
        ResetLifetime();
    }

    private void OnEnable()
    {
        CacheComponents();
        ConfigurePhysics();
        ResetLifetime();
        worldVelocity = Vector2.zero;
        riseSpeed = 0f;
        verticalSwirlTimer = 0f;
        horizontalSwirlTimer = 0f;
    }

    private void Update()
    {
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        lifetimeRemaining -= GameTime.DeltaTime;
        if (lifetimeRemaining <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (activePlayerGeneratedIceObstacle == this)
        {
            activePlayerGeneratedIceObstacle = null;
        }
    }

    private void FixedUpdate()
    {
        TickSwirlTimers();
        UpdateVelocity();
        ApplyVelocity();
    }

    private void Reset()
    {
        CacheComponents();
        ConfigurePhysics();
        GameLayers.ApplyTo(gameObject, GameLayers.Obstacle);
    }

    private void OnValidate()
    {
        riseAcceleration = Mathf.Max(0f, riseAcceleration);
        maxRiseSpeed = Mathf.Max(0f, maxRiseSpeed);
        lifetime = Mathf.Max(0f, lifetime);
        CacheComponents();
        ConfigurePhysics();
        GameLayers.ApplyToAfterValidation(gameObject, GameLayers.Obstacle);
    }

    public void ApplySwirlVelocity(Vector2 velocity, bool affectsVertical)
    {
        worldVelocity = velocity;
        if (affectsVertical)
        {
            verticalSwirlTimer = SwirlHoldTime;
        }
        else
        {
            horizontalSwirlTimer = SwirlHoldTime;
        }
    }

    public void RegisterAsPlayerGenerated()
    {
        isFromPlayer = true;
        if (activePlayerGeneratedIceObstacle != null && activePlayerGeneratedIceObstacle != this)
        {
            Destroy(activePlayerGeneratedIceObstacle.gameObject);
        }

        activePlayerGeneratedIceObstacle = this;
    }

    private void UpdateVelocity()
    {
        bool horizontalControlledBySwirl = horizontalSwirlTimer > 0f;
        bool verticalControlledBySwirl = verticalSwirlTimer > 0f;

        if (!horizontalControlledBySwirl)
        {
            worldVelocity.x = 0f;
        }

        if (verticalControlledBySwirl)
        {
            return;
        }

        UpdateFloatVelocity();
    }

    private void UpdateFloatVelocity()
    {
        if (!TryGetWaterSurfaceTarget(out float targetTransformY, out float surfaceY))
        {
            riseSpeed = 0f;
            worldVelocity.y = 0f;
            return;
        }

        Bounds bounds = obstacleCollider.bounds;
        if (bounds.center.y >= surfaceY - SurfaceEpsilon)
        {
            StopAtSurface(targetTransformY);
            return;
        }

        riseSpeed = Mathf.Min(maxRiseSpeed, riseSpeed + riseAcceleration * GameTime.FixedDeltaTime);
        worldVelocity.y = riseSpeed;
    }

    private void ApplyVelocity()
    {
        if (body == null)
        {
            return;
        }

        Vector2 delta = worldVelocity * GameTime.FixedDeltaTime;
        if (delta.sqrMagnitude <= 0f)
        {
            body.velocity = Vector2.zero;
            return;
        }

        Vector2 nextPosition = body.position + delta;
        if (verticalSwirlTimer <= 0f && worldVelocity.y > 0f && TryGetWaterSurfaceTarget(out float targetTransformY, out _))
        {
            nextPosition.y = Mathf.Min(nextPosition.y, targetTransformY);
        }

        body.MovePosition(nextPosition);
        body.velocity = worldVelocity * GameTime.WorldScale;
    }

    private void StopAtSurface(float targetTransformY)
    {
        riseSpeed = 0f;
        worldVelocity.y = 0f;

        if (Mathf.Abs(transform.position.y - targetTransformY) <= SurfaceEpsilon)
        {
            return;
        }

        Vector2 position = body != null ? body.position : (Vector2)transform.position;
        position.y = targetTransformY;
        if (body != null)
        {
            body.position = position;
            body.velocity = Vector2.zero;
        }
        else
        {
            transform.position = new Vector3(transform.position.x, targetTransformY, transform.position.z);
        }
    }

    private bool TryGetWaterSurfaceTarget(out float targetTransformY, out float surfaceY)
    {
        targetTransformY = transform.position.y;
        surfaceY = 0f;

        if (obstacleCollider == null)
        {
            return false;
        }

        Bounds obstacleBounds = obstacleCollider.bounds;
        WaterZone waterZone = WaterZone.GetZoneAtPoint(obstacleBounds.center);
        if (waterZone == null || !waterZone.TryGetGizmoBounds(out Bounds waterBounds))
        {
            return false;
        }

        surfaceY = waterBounds.max.y;
        targetTransformY = transform.position.y + surfaceY - obstacleBounds.center.y;
        return true;
    }

    private void TickSwirlTimers()
    {
        verticalSwirlTimer = Mathf.Max(0f, verticalSwirlTimer - Time.fixedDeltaTime);
        horizontalSwirlTimer = Mathf.Max(0f, horizontalSwirlTimer - Time.fixedDeltaTime);
    }

    private void CacheComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (obstacleCollider == null)
        {
            obstacleCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigurePhysics()
    {
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        if (obstacleCollider != null)
        {
            obstacleCollider.isTrigger = false;
        }
    }

    private void ResetLifetime()
    {
        lifetimeRemaining = lifetime;
    }
}
