using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Bubble : MonoBehaviour
{
    private const float ContactHoldTime = 0.05f;
    private const int MaxCastHits = 8;

    [Header("Movement")]
    [SerializeField] private float riseSpeed = 3f;
    [SerializeField, Range(0f, 1f)] private float standingRiseSpeedMultiplier = 0.5f;
    [SerializeField] private float playerPushVelocityMultiplier = 1f;
    [SerializeField] private float minPlayerPushSpeed = 0.05f;
    [SerializeField] private float givenSpeedDeceleration = 6f;

    [Header("Lifetime")]
    [SerializeField] private float lifespan = 6f;

    [Header("Setup")]
    [SerializeField] private bool applyPlatformLayer = true;
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private float collisionSkin = 0.01f;

    private Rigidbody2D body;
    private Collider2D bubbleCollider;
    private ContactFilter2D collisionFilter;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[MaxCastHits];
    private Vector2 worldVelocity;
    private Vector2 currentDelta;
    private Vector2 swirlVelocity;
    private Vector2 givenVelocity;
    private float standingTimer;
    private float pushTimer;
    private float swirlTimer;
    private float pushedVelocityX;
    private float lifetimeRemaining;
    private bool isDestroying;

    public Vector2 WorldVelocity
    {
        get { return worldVelocity; }
    }

    public Vector2 CurrentDelta
    {
        get { return currentDelta; }
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
        ResetRuntimeMotion();
        isDestroying = false;
    }

    private void Reset()
    {
        CacheComponents();
        ConfigurePhysics();
    }

    private void OnValidate()
    {
        riseSpeed = Mathf.Max(0f, riseSpeed);
        standingRiseSpeedMultiplier = Mathf.Clamp01(standingRiseSpeedMultiplier);
        playerPushVelocityMultiplier = Mathf.Max(0f, playerPushVelocityMultiplier);
        minPlayerPushSpeed = Mathf.Max(0f, minPlayerPushSpeed);
        givenSpeedDeceleration = Mathf.Max(0f, givenSpeedDeceleration);
        lifespan = Mathf.Max(0f, lifespan);
        collisionSkin = Mathf.Max(0f, collisionSkin);
        EnsureCollisionMask();
        CacheComponents();
        ConfigurePhysics();
    }

    private void Update()
    {
        if (isDestroying)
        {
            return;
        }

        if (lifespan <= 0f)
        {
            DestroyBubble();
            return;
        }

        lifetimeRemaining -= GameTime.DeltaTime;
        if (lifetimeRemaining <= 0f)
        {
            DestroyBubble();
        }
    }

    private void FixedUpdate()
    {
        if (isDestroying)
        {
            return;
        }

        TickTimers();
        UpdateVelocity();
        ApplyVelocity();
    }

    public void NotifyPlayerStanding(MCController player)
    {
        if (player == null)
        {
            return;
        }

        standingTimer = ContactHoldTime;
    }

    public void ApplySwirlVelocity(Vector2 velocity)
    {
        swirlVelocity = velocity;
        swirlTimer = ContactHoldTime;
    }

    public void GiveSpeed(Vector2 velocity)
    {
        givenVelocity = velocity;
    }

    public void DestroyBubble()
    {
        if (isDestroying)
        {
            return;
        }

        isDestroying = true;
        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    public void ApplyBurning(int sourceDamage)
    {
        DestroyBubble();
    }

    public void ApplyFrozen()
    {
        DestroyBubble();
    }

    public void ApplyPoisoned()
    {
        DestroyBubble();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleCollision(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleCollider(other);
    }

    private void HandleCollision(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        Collider2D other = collision.collider;
        if (HandleCollider(other))
        {
            return;
        }

        MCController player = other != null ? other.GetComponentInParent<MCController>() : null;
        if (player == null)
        {
            return;
        }

        if (player.IsDashing)
        {
            DestroyBubble();
            return;
        }

        if (IsStandingContact(player, collision))
        {
            NotifyPlayerStanding(player);
            return;
        }

        TryApplyPlayerPush(player);
    }

    private bool HandleCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (Utils.IsLayer(other.gameObject.layer, GameLayers.Hazard)
            || Utils.IsEnemyCollider(other)
            || other.GetComponentInParent<Bullet>() != null)
        {
            DestroyBubble();
            return true;
        }

        MCController player = other.GetComponentInParent<MCController>();
        if (player != null && player.IsDashing)
        {
            DestroyBubble();
            return true;
        }

        return false;
    }

    private bool IsStandingContact(MCController player, Collision2D collision)
    {
        Collider2D ground = player != null ? player.GetCurrentGround() : null;
        if (ground != null && ground.GetComponentInParent<Bubble>() == this)
        {
            return true;
        }

        if (bubbleCollider == null || collision == null || collision.collider == null)
        {
            return false;
        }

        Bounds playerBounds = collision.collider.bounds;
        Bounds bubbleBounds = bubbleCollider.bounds;
        return playerBounds.min.y >= bubbleBounds.center.y;
    }

    private void TryApplyPlayerPush(MCController player)
    {
        if (player == null)
        {
            return;
        }

        NotifyPlayerPush(player, player.BubblePushVelocity);
    }

    public void NotifyPlayerPush(float horizontalVelocity)
    {
        if (Mathf.Abs(horizontalVelocity) < minPlayerPushSpeed)
        {
            return;
        }

        pushedVelocityX = horizontalVelocity * playerPushVelocityMultiplier;
        pushTimer = ContactHoldTime;
    }

    private void NotifyPlayerPush(MCController player, float horizontalVelocity)
    {
        if (player == null || !IsPlayerPushingTowardBubble(player.transform.position, horizontalVelocity))
        {
            return;
        }

        NotifyPlayerPush(horizontalVelocity);
    }

    private bool IsPlayerPushingTowardBubble(Vector3 playerPosition, float horizontalVelocity)
    {
        float directionToBubble = Mathf.Sign(transform.position.x - playerPosition.x);
        if (Mathf.Approximately(directionToBubble, 0f))
        {
            return false;
        }

        return Mathf.Sign(horizontalVelocity) == directionToBubble;
    }

    private void TickTimers()
    {
        standingTimer = Mathf.Max(0f, standingTimer - Time.fixedDeltaTime);
        pushTimer = Mathf.Max(0f, pushTimer - Time.fixedDeltaTime);
        swirlTimer = Mathf.Max(0f, swirlTimer - Time.fixedDeltaTime);
        DecayGivenVelocity();
    }

    private void UpdateVelocity()
    {
        float verticalSpeed = riseSpeed;
        if (standingTimer > 0f)
        {
            verticalSpeed *= standingRiseSpeedMultiplier;
        }

        Vector2 baseVelocity = new Vector2(pushTimer > 0f ? pushedVelocityX : 0f, verticalSpeed);
        worldVelocity = baseVelocity + givenVelocity + (swirlTimer > 0f ? swirlVelocity : Vector2.zero);
    }

    private void DecayGivenVelocity()
    {
        if (givenSpeedDeceleration <= 0f || givenVelocity.sqrMagnitude <= 0f)
        {
            return;
        }

        float delta = givenSpeedDeceleration * GameTime.FixedDeltaTime;
        givenVelocity.x = Mathf.MoveTowards(givenVelocity.x, 0f, delta);
        givenVelocity.y = Mathf.MoveTowards(givenVelocity.y, 0f, delta);
    }

    private void ApplyVelocity()
    {
        currentDelta = Vector2.zero;
        if (body == null)
        {
            return;
        }

        Vector2 requestedDelta = worldVelocity * GameTime.FixedDeltaTime;
        currentDelta = GetCollisionConstrainedDelta(requestedDelta);
        if (isDestroying)
        {
            return;
        }

        body.MovePosition(body.position + currentDelta);
        body.velocity = worldVelocity * GameTime.WorldScale;
    }

    private Vector2 GetCollisionConstrainedDelta(Vector2 requestedDelta)
    {
        Vector2 constrainedDelta = Vector2.zero;
        constrainedDelta.x = GetAxisConstrainedDelta(new Vector2(requestedDelta.x, 0f)).x;
        if (isDestroying)
        {
            return Vector2.zero;
        }

        if (Mathf.Abs(constrainedDelta.x) > 0f)
        {
            body.position += new Vector2(constrainedDelta.x, 0f);
        }

        constrainedDelta.y = GetAxisConstrainedDelta(new Vector2(0f, requestedDelta.y)).y;

        if (Mathf.Abs(constrainedDelta.x) > 0f)
        {
            body.position -= new Vector2(constrainedDelta.x, 0f);
        }

        return constrainedDelta;
    }

    private Vector2 GetAxisConstrainedDelta(Vector2 axisDelta)
    {
        if (body == null || bubbleCollider == null || axisDelta.sqrMagnitude <= 0f)
        {
            return axisDelta;
        }

        EnsureCollisionMask();
        UpdateCollisionFilter();

        float distance = axisDelta.magnitude;
        int hitCount = body.Cast(axisDelta.normalized, collisionFilter, castHits, distance + collisionSkin);
        float allowedDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (Utils.IsLayer(hit.collider.gameObject.layer, GameLayers.Hazard))
            {
                DestroyBubble();
                return Vector2.zero;
            }

            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - collisionSkin));
        }

        return axisDelta.normalized * allowedDistance;
    }

    private void CacheComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (bubbleCollider == null)
        {
            bubbleCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigurePhysics()
    {
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.useFullKinematicContacts = true;
        }

        if (bubbleCollider != null)
        {
            bubbleCollider.isTrigger = false;
        }

        if (applyPlatformLayer)
        {
            GameLayers.ApplyToAfterValidation(gameObject, GameLayers.Platform);
        }

        EnsureCollisionMask();
        UpdateCollisionFilter();
    }

    private void EnsureCollisionMask()
    {
        if (collisionMask == 0)
        {
            collisionMask = LayerMask.GetMask(
                GameLayers.Ground,
                GameLayers.Obstacle,
                GameLayers.Platform,
                GameLayers.Hazard);
        }
    }

    private void UpdateCollisionFilter()
    {
        collisionFilter.useTriggers = true;
        collisionFilter.SetLayerMask(collisionMask);
    }

    private void ResetLifetime()
    {
        lifetimeRemaining = lifespan;
    }

    private void ResetRuntimeMotion()
    {
        worldVelocity = Vector2.zero;
        currentDelta = Vector2.zero;
        swirlVelocity = Vector2.zero;
        givenVelocity = Vector2.zero;
        standingTimer = 0f;
        pushTimer = 0f;
        swirlTimer = 0f;
        pushedVelocityX = 0f;
    }
}
