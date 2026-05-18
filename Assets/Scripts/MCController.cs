using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MCController : MonoBehaviour
{
    private const int MaxSupportContacts = 8;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpVelocity = 14f;
    [SerializeField] private float maxFallSpeed = 24f;

    [Header("Jump Feel")]
    [SerializeField] private float coyoteTime = 0.08f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float fallGravityMultiplier = 1.8f;
    [SerializeField] private float lowJumpGravityMultiplier = 2.2f;

    [Header("Ground Check")]
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -1.05f);
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.85f, 0.12f);
    [SerializeField] private float groundContactMinNormalY = 0.2f;
    [SerializeField] private LayerMask movementGroundMask;
    [SerializeField] private LayerMask safeGroundMask;

    [Header("Edge Stability")]
    [SerializeField] private bool preventIdleEdgeSlip = true;
    [SerializeField] private float idleEdgeSlipInputThreshold = 0.01f;

    [Header("Dash")]
    [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;
    [SerializeField] private float dashDistance = 4f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private float dashCooldown = 0.2f;
    [SerializeField] private float dashRecoilDistance = 1f;
    [SerializeField] private float dashRecoilDuration = 0.08f;
    [SerializeField] private LayerMask dashStopMask;

    [Header("Weapon")]
    [SerializeField] private GameObject Bullet;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private float bulletSpawnOffset = 0.75f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float minBulletSpeed = 12f;
    [SerializeField] private float maxBulletSpeed = 26f;
    [SerializeField] private float maxChargeTime = 1.2f;
    [SerializeField] private float fullChargeGroundMoveSpeedMultiplier = 0.45f;
    [SerializeField] private float fullChargeAutoFireDelay = 0.6f;
    [SerializeField] private float aerialBulletTimeScale = 0.35f;

    private Rigidbody2D body;
    private RigidbodyConstraints2D movementConstraints;
    private ContactFilter2D movementContactFilter;
    private readonly ContactPoint2D[] supportContacts = new ContactPoint2D[MaxSupportContacts];
    private float inputX;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool jumpHeld;
    private bool inputLocked;
    private Collider2D currentGround;
    private bool edgeSlipLockActive;
    private float defaultGravityScale;
    private bool isDashing;
    private bool isDashRecoiling;
    private int dashDirection = 1;
    private float dashTimeRemaining;
    private float dashRecoilTimeRemaining;
    private float nextDashTime;
    private bool isChargingAttack;
    private bool isAttackFullyCharged;
    private bool bulletTimeStartedForCharge;
    private float attackChargeTime;
    private float fullChargeAutoFireCounter;

    public bool IsGrounded { get; private set; }
    public bool IsOnSafeGround { get; private set; }
    public int FacingDirection { get; private set; } = GameDirection.Right;
    public bool IsDashing { get { return isDashing; } }
    public bool IsDashActive { get { return isDashing || isDashRecoiling; } }

    public Vector2 Velocity
    {
        get { return body != null ? body.velocity : Vector2.zero; }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.freezeRotation = true;
        movementConstraints = body.constraints;
        defaultGravityScale = body.gravityScale;
        EnsureLayerMasks();
        UpdateMovementContactFilter();
    }

    private void Reset()
    {
        EnsureLayerMasks();
    }

    private void OnValidate()
    {
        groundContactMinNormalY = Mathf.Clamp01(groundContactMinNormalY);
        idleEdgeSlipInputThreshold = Mathf.Max(0f, idleEdgeSlipInputThreshold);
        dashDistance = Mathf.Max(0f, dashDistance);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        dashCooldown = Mathf.Max(0f, dashCooldown);
        dashRecoilDistance = Mathf.Max(0f, dashRecoilDistance);
        dashRecoilDuration = Mathf.Max(0.01f, dashRecoilDuration);
        bulletSpawnOffset = Mathf.Max(0f, bulletSpawnOffset);
        attackDamage = Mathf.Max(1, attackDamage);
        minBulletSpeed = Mathf.Max(0f, minBulletSpeed);
        maxBulletSpeed = Mathf.Max(minBulletSpeed, maxBulletSpeed);
        maxChargeTime = Mathf.Max(0.01f, maxChargeTime);
        fullChargeGroundMoveSpeedMultiplier = Mathf.Clamp01(fullChargeGroundMoveSpeedMultiplier);
        fullChargeAutoFireDelay = Mathf.Max(0f, fullChargeAutoFireDelay);
        aerialBulletTimeScale = Mathf.Clamp(aerialBulletTimeScale, 0.01f, 1f);
        EnsureLayerMasks();
        UpdateMovementContactFilter();
    }

    private void OnDisable()
    {
        CancelAttackCharge();
        isDashing = false;
        isDashRecoiling = false;

        if (body != null)
        {
            RestoreGravity();
            SetEdgeSlipLock(false);
        }
    }

    private void Update()
    {
        if (inputLocked)
        {
            inputX = 0f;
            jumpHeld = false;
            CancelAttackCharge();
            return;
        }

        inputX = Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetKey(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (Input.GetKeyDown(dashKey))
        {
            TryStartDash();
        }

        UpdateAttackCharge();

        if (inputX > 0.01f)
        {
            FacingDirection = GameDirection.Right;
        }
        else if (inputX < -0.01f)
        {
            FacingDirection = GameDirection.Left;
        }
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();

        if (isDashing)
        {
            ApplyDash();
            return;
        }

        if (isDashRecoiling)
        {
            ApplyDashRecoil();
            return;
        }

        UpdateTimers();
        ApplyMovement();
        ApplyJump();
        ApplyExtraGravity();
        ApplyIdleEdgeSlipLock();
    }

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        if (locked)
        {
            inputX = 0f;
            jumpBufferCounter = 0f;
            CancelAttackCharge();
            StopDash();
        }
    }

    public void TeleportTo(Vector3 position, int facingDirection)
    {
        transform.position = position;
        SetFacingDirection(facingDirection);
        ClearVelocity();
        UpdateGroundedState();
    }

    public void ClearVelocity()
    {
        if (body != null)
        {
            body.velocity = Vector2.zero;
        }
    }

    public void SetVelocity(Vector2 velocity)
    {
        if (body != null)
        {
            body.velocity = velocity;
        }
    }

    public Collider2D GetCurrentGround()
    {
        return currentGround;
    }

    private void ApplyMovement()
    {
        Vector2 velocity = body.velocity;
        float currentMoveSpeed = moveSpeed;
        if (IsGrounded && isAttackFullyCharged)
        {
            currentMoveSpeed *= fullChargeGroundMoveSpeedMultiplier;
        }

        velocity.x = inputX * currentMoveSpeed;
        body.velocity = velocity;
    }

    private void ApplyJump()
    {
        if (jumpBufferCounter <= 0f || coyoteCounter <= 0f)
        {
            return;
        }

        Vector2 velocity = body.velocity;
        velocity.y = jumpVelocity;
        body.velocity = velocity;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        IsGrounded = false;
        IsOnSafeGround = false;
    }

    private void ApplyExtraGravity()
    {
        Vector2 velocity = body.velocity;
        if (velocity.y < 0f)
        {
            velocity += Physics2D.gravity * ((fallGravityMultiplier - 1f) * Time.fixedDeltaTime);
        }
        else if (velocity.y > 0f && !jumpHeld)
        {
            velocity += Physics2D.gravity * ((lowJumpGravityMultiplier - 1f) * Time.fixedDeltaTime);
        }

        if (velocity.y < -maxFallSpeed)
        {
            velocity.y = -maxFallSpeed;
        }

        body.velocity = velocity;
    }

    private void ApplyIdleEdgeSlipLock()
    {
        bool shouldLock = ShouldLockIdleEdgeSlip();
        SetEdgeSlipLock(shouldLock);
    }

    private bool ShouldLockIdleEdgeSlip()
    {
        if (!preventIdleEdgeSlip || !IsGrounded)
        {
            return false;
        }

        if (Mathf.Abs(inputX) > idleEdgeSlipInputThreshold)
        {
            return false;
        }

        return true;
    }

    private void UpdateTimers()
    {
        if (IsGrounded)
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.fixedDeltaTime;
        }

        jumpBufferCounter -= Time.fixedDeltaTime;
    }

    private void UpdateAttackCharge()
    {
        if (Input.GetMouseButtonDown(0))
        {
            BeginAttackCharge();
        }

        if (!isChargingAttack)
        {
            return;
        }

        attackChargeTime = Mathf.Min(maxChargeTime, attackChargeTime + Time.deltaTime);
        if (!isAttackFullyCharged && attackChargeTime >= maxChargeTime)
        {
            isAttackFullyCharged = true;
            fullChargeAutoFireCounter = fullChargeAutoFireDelay;
        }

        if (isAttackFullyCharged)
        {
            TryStartAerialBulletTime();
            fullChargeAutoFireCounter -= Time.deltaTime;
            if (fullChargeAutoFireCounter <= 0f)
            {
                FireChargedBullet();
                return;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            FireChargedBullet();
        }
    }

    private void BeginAttackCharge()
    {
        isChargingAttack = true;
        isAttackFullyCharged = false;
        bulletTimeStartedForCharge = false;
        attackChargeTime = 0f;
        fullChargeAutoFireCounter = fullChargeAutoFireDelay;
        GameTime.ClearSlow(this);
    }

    private void FireChargedBullet()
    {
        if (!isChargingAttack)
        {
            return;
        }

        float chargeRatio = GetAttackChargeRatio();
        bool fullChargeShot = isAttackFullyCharged;
        Vector3 aimOrigin = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;
        Vector2 shotDirection = GetMouseAimDirection(aimOrigin);
        Vector3 spawnPosition = bulletSpawnPoint != null
            ? bulletSpawnPoint.position
            : transform.position + (Vector3)(shotDirection * bulletSpawnOffset);
        global::Bullet bullet = SpawnPlayerBullet(spawnPosition);
        if (bullet != null)
        {
            float bulletSpeed = Mathf.Lerp(minBulletSpeed, maxBulletSpeed, chargeRatio);
            bullet.Configure(BulletSource.Player, shotDirection, bulletSpeed, attackDamage, !fullChargeShot, fullChargeShot);
        }

        FinishAttackCharge();
    }

    private void FinishAttackCharge()
    {
        isChargingAttack = false;
        isAttackFullyCharged = false;
        bulletTimeStartedForCharge = false;
        attackChargeTime = 0f;
        fullChargeAutoFireCounter = 0f;
        GameTime.ClearSlow(this);
    }

    private void CancelAttackCharge()
    {
        if (!isChargingAttack && !bulletTimeStartedForCharge)
        {
            return;
        }

        FinishAttackCharge();
    }

    private float GetAttackChargeRatio()
    {
        return Mathf.Clamp01(attackChargeTime / Mathf.Max(0.01f, maxChargeTime));
    }

    private void TryStartAerialBulletTime()
    {
        if (bulletTimeStartedForCharge || IsGrounded || fullChargeAutoFireDelay <= 0f)
        {
            return;
        }

        bulletTimeStartedForCharge = true;
        GameTime.SetSlow(this, aerialBulletTimeScale, fullChargeAutoFireDelay);
    }

    private Vector2 GetMouseAimDirection(Vector3 origin)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 rawDirection = (Vector2)(mouseWorld - origin);
            if (rawDirection.sqrMagnitude > 0.0001f)
            {
                return rawDirection.normalized;
            }
        }

        return FacingDirection == GameDirection.Left ? Vector2.left : Vector2.right;
    }

    private global::Bullet SpawnPlayerBullet(Vector3 spawnPosition)
    {
        if (Bullet != null)
        {
            GameObject bulletObject = Instantiate(Bullet, spawnPosition, Quaternion.identity);
            global::Bullet bullet = bulletObject.GetComponent<global::Bullet>();
            if (bullet != null)
            {
                return bullet;
            }

            EnsureBulletPhysics(bulletObject);
            return bulletObject.AddComponent<global::Bullet>();
        }

        GameObject fallbackBullet = new GameObject("PlayerBullet");
        fallbackBullet.transform.position = spawnPosition;
        EnsureBulletPhysics(fallbackBullet);
        return fallbackBullet.AddComponent<global::Bullet>();
    }

    private static void EnsureBulletPhysics(GameObject bulletObject)
    {
        if (bulletObject.GetComponent<Rigidbody2D>() == null)
        {
            bulletObject.AddComponent<Rigidbody2D>();
        }

        if (bulletObject.GetComponent<Collider2D>() == null)
        {
            CircleCollider2D collider2D = bulletObject.AddComponent<CircleCollider2D>();
            collider2D.isTrigger = true;
        }
    }

    private void TryStartDash()
    {
        if (body == null || inputLocked || isDashing || isDashRecoiling || Time.time < nextDashTime)
        {
            return;
        }

        dashDirection = FacingDirection == GameDirection.Left ? -1 : 1;
        dashTimeRemaining = dashDuration;
        nextDashTime = Time.time + dashCooldown;
        isDashing = true;
        isDashRecoiling = false;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        SetEdgeSlipLock(false);
        SetDashGravity();
        body.velocity = new Vector2(GetDashSpeed() * dashDirection, 0f);
    }

    private void ApplyDash()
    {
        dashTimeRemaining -= Time.fixedDeltaTime;
        body.velocity = new Vector2(GetDashSpeed() * dashDirection, 0f);

        if (dashTimeRemaining <= 0f)
        {
            StopDash();
        }
    }

    private void ApplyDashRecoil()
    {
        dashRecoilTimeRemaining -= Time.fixedDeltaTime;
        body.velocity = new Vector2(GetDashRecoilSpeed() * -dashDirection, 0f);

        if (dashRecoilTimeRemaining <= 0f)
        {
            StopDashRecoil();
        }
    }

    private void StopDash()
    {
        if (!isDashing && !isDashRecoiling)
        {
            return;
        }

        isDashing = false;
        isDashRecoiling = false;
        RestoreGravity();

        if (body != null)
        {
            body.velocity = new Vector2(0f, body.velocity.y);
        }
    }

    private void StopDashWithRecoil()
    {
        if (!isDashing || body == null)
        {
            return;
        }

        isDashing = false;
        isDashRecoiling = dashRecoilDistance > 0f;
        dashRecoilTimeRemaining = dashRecoilDuration;

        if (!isDashRecoiling)
        {
            RestoreGravity();
            body.velocity = new Vector2(0f, body.velocity.y);
            return;
        }

        SetDashGravity();
        body.velocity = new Vector2(GetDashRecoilSpeed() * -dashDirection, 0f);
    }

    private void StopDashRecoil()
    {
        if (!isDashRecoiling)
        {
            return;
        }

        isDashRecoiling = false;
        RestoreGravity();
        body.velocity = new Vector2(0f, body.velocity.y);
    }

    private float GetDashSpeed()
    {
        return dashDistance / Mathf.Max(0.01f, dashDuration);
    }

    private float GetDashRecoilSpeed()
    {
        return dashRecoilDistance / Mathf.Max(0.01f, dashRecoilDuration);
    }

    private void SetDashGravity()
    {
        body.gravityScale = 0f;
    }

    private void RestoreGravity()
    {
        body.gravityScale = defaultGravityScale;
    }

    private void UpdateGroundedState()
    {
        Vector2 center = (Vector2)transform.position + groundCheckOffset;
        currentGround = Physics2D.OverlapBox(center, groundCheckSize, 0f, movementGroundMask);
        if (currentGround == null)
        {
            currentGround = FindSupportContactGround();
        }

        IsGrounded = currentGround != null;
        IsOnSafeGround = IsGrounded && IsColliderOnMask(currentGround, safeGroundMask);
    }

    private Collider2D FindSupportContactGround()
    {
        if (body == null)
        {
            return null;
        }

        int contactCount = body.GetContacts(movementContactFilter, supportContacts);
        float playerCenterY = transform.position.y;
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = supportContacts[i];
            if (contact.point.y > playerCenterY)
            {
                continue;
            }

            if (Mathf.Abs(contact.normal.y) < groundContactMinNormalY)
            {
                continue;
            }

            Collider2D ground = GetGroundColliderFromContact(contact);
            if (ground != null)
            {
                return ground;
            }
        }

        return null;
    }

    private void SetFacingDirection(int facingDirection)
    {
        FacingDirection = GameDirection.NormalizeOrDefault(facingDirection, FacingDirection);
    }

    private static bool IsColliderOnMask(Collider2D collider2D, LayerMask mask)
    {
        return collider2D != null && (mask.value & (1 << collider2D.gameObject.layer)) != 0;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void EnsureLayerMasks()
    {
        if (movementGroundMask == 0)
        {
            movementGroundMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Platform);
        }

        if (safeGroundMask == 0)
        {
            safeGroundMask = LayerMask.GetMask(GameLayers.Ground);
        }

        if (dashStopMask == 0)
        {
            dashStopMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Obstacle, GameLayers.Platform, GameLayers.Enemy);
        }
    }

    private void UpdateMovementContactFilter()
    {
        movementContactFilter.useTriggers = false;
        movementContactFilter.SetLayerMask(movementGroundMask);
    }

    private Collider2D GetGroundColliderFromContact(ContactPoint2D contact)
    {
        if (IsColliderOnMask(contact.collider, movementGroundMask))
        {
            return contact.collider;
        }

        if (IsColliderOnMask(contact.otherCollider, movementGroundMask))
        {
            return contact.otherCollider;
        }

        return null;
    }

    private void SetEdgeSlipLock(bool active)
    {
        if (body == null || edgeSlipLockActive == active)
        {
            return;
        }

        edgeSlipLockActive = active;
        body.constraints = active
            ? movementConstraints | RigidbodyConstraints2D.FreezePositionX
            : movementConstraints;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleDashCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleDashCollision(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleDashTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleDashTrigger(other);
    }

    private void HandleDashCollision(Collision2D collision)
    {
        if (!isDashing || collision == null || collision.collider == null)
        {
            return;
        }

        bool hitEnemy = IsEnemyCollider(collision.collider);
        if (!hitEnemy && !IsLayerInMask(collision.collider.gameObject.layer, dashStopMask))
        {
            return;
        }

        if (hitEnemy || HasDashBlockingNormal(collision))
        {
            StopDashWithRecoil();
        }
    }

    private void HandleDashTrigger(Collider2D other)
    {
        if (!isDashing || other == null)
        {
            return;
        }

        if (IsEnemyCollider(other))
        {
            StopDashWithRecoil();
        }
    }

    private static bool IsEnemyCollider(Collider2D collider2D)
    {
        return collider2D != null
            && (collider2D.gameObject.layer == LayerMask.NameToLayer(GameLayers.Enemy)
                || collider2D.GetComponentInParent<EnemyController>() != null);
    }

    private bool HasDashBlockingNormal(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.normal.x * dashDirection < -0.25f)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsOnSafeGround ? Color.green : Color.yellow;
        Vector2 center = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireCube(center, groundCheckSize);
    }
}
