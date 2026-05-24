using System.Collections.Generic;
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
    [SerializeField] private float jumpGroundIgnoreTime = 0.08f;
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
    [SerializeField] private float minBulletSpeed = 12f;
    [SerializeField] private float maxBulletSpeed = 26f;
    [SerializeField] private float maxChargeTime = 1.2f;
    [SerializeField] private float fullChargeGroundMoveSpeedMultiplier = 0.45f;
    [SerializeField] private float aerialBulletTimeScale = 0.35f;
    [SerializeField] private BulletElement bulletElement = BulletElement.None;

    [Header("Healing")]
    [SerializeField] private KeyCode useHealthBottleKey = KeyCode.R;
    [SerializeField] private int healthBottleHealAmount = 1;

    [Header("HP")]
    [SerializeField] private int maxHp = 5;
    [SerializeField] private int currentHp = 5;

    [Header("Stamina")]
    [SerializeField] private Transform breathPoint;
    [SerializeField] private Vector2 breathPointFallbackOffset = new Vector2(0f, 0.75f);
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float staminaDrainPerSecond = 1f;
    [SerializeField] private float staminaRecoveryPerSecond = 2f;
    [SerializeField] private float currentStamina = 5f;

    [Header("Elemental Status")]
    [SerializeField] private bool isBurning;
    [SerializeField] private bool isFrozen;
    [SerializeField] private bool isPoisoned;

    private Rigidbody2D body;
    private Animator[] animators;
    private RigidbodyConstraints2D movementConstraints;
    private ContactFilter2D movementContactFilter;
    private readonly ContactPoint2D[] supportContacts = new ContactPoint2D[MaxSupportContacts];
    private readonly List<WaterZone> waterZones = new List<WaterZone>();
    private readonly List<Swirl> activeSwirls = new List<Swirl>();
    private Vector2 currentGroundNormal = Vector2.up;
    private float inputX;
    private float inputY;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float groundIgnoreCounter;
    private bool jumpHeld;
    private bool inputLocked;
    private Collider2D currentGround;
    private bool edgeSlipLockActive;
    private float defaultGravityScale;
    private bool isDashing;
    private bool isDashRecoiling;
    private bool hasUsedDoubleJump;
    private int dashDirection = 1;
    private float dashTimeRemaining;
    private float dashRecoilTimeRemaining;
    private float dashCooldownRemaining;
    private bool isChargingAttack;
    private bool isAttackFullyCharged;
    private bool bulletTimeStartedForCharge;
    private float attackChargeTime;
    private float fullChargeAutoFireCounter;
    private WaterZone currentWaterZone;
    private WaterZone breathWaterZone;
    private PlayerRespawn playerRespawn;
    private float burningTimeRemaining;
    private float burningDamagePerSecond;
    private float burningDamageAccumulator;
    private float freezeTimeRemaining;

    public bool IsGrounded { get; private set; }
    public bool IsOnSafeGround { get; private set; }
    public int FacingDirection { get; private set; } = GameDirection.Right;
    public bool IsDashing { get { return isDashing; } }
    public bool IsDashActive { get { return isDashing || isDashRecoiling; } }
    public bool IsUnderwater { get { return currentWaterZone != null; } }
    public bool IsBreathPointUnderwater { get { return breathWaterZone != null; } }
    public bool IsInSwirl
    {
        get
        {
            PruneInactiveSwirls();
            return activeSwirls.Count > 0;
        }
    }

    public bool IsBurning { get { return isBurning; } }
    public bool IsFrozen { get { return isFrozen; } }
    public bool IsPoisoned { get { return isPoisoned; } }
    public float CurrentStamina { get { return currentStamina; } }
    public float MaxStamina { get { return maxStamina; } }
    public int CurrentHp { get { return currentHp; } }
    public int MaxHp { get { return maxHp; } }
    public bool IsAlive { get { return currentHp > 0; } }

    public Vector2 Velocity
    {
        get { return body != null ? body.velocity : Vector2.zero; }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.freezeRotation = true;
        CacheAnimators();
        movementConstraints = body.constraints;
        defaultGravityScale = body.gravityScale;
        CachePlayerRespawn();
        RestoreHpToFull();
        ResetStamina();
        EnsureLayerMasks();
        UpdateMovementContactFilter();
    }

    private void Reset()
    {
        if (breathPoint == null)
        {
            Transform foundBreathPoint = transform.Find("BreathPoint");
            if (foundBreathPoint != null)
            {
                breathPoint = foundBreathPoint;
            }
        }

        EnsureLayerMasks();
    }

    private void OnValidate()
    {
        groundContactMinNormalY = Mathf.Clamp01(groundContactMinNormalY);
        jumpGroundIgnoreTime = Mathf.Max(0f, jumpGroundIgnoreTime);
        idleEdgeSlipInputThreshold = Mathf.Max(0f, idleEdgeSlipInputThreshold);
        dashDistance = Mathf.Max(0f, dashDistance);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        dashCooldown = Mathf.Max(0f, dashCooldown);
        dashRecoilDistance = Mathf.Max(0f, dashRecoilDistance);
        dashRecoilDuration = Mathf.Max(0.01f, dashRecoilDuration);
        bulletSpawnOffset = Mathf.Max(0f, bulletSpawnOffset);
        healthBottleHealAmount = Mathf.Max(0, healthBottleHealAmount);
        maxHp = Mathf.Max(1, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        minBulletSpeed = Mathf.Max(0f, minBulletSpeed);
        maxBulletSpeed = Mathf.Max(minBulletSpeed, maxBulletSpeed);
        maxChargeTime = Mathf.Max(0.01f, maxChargeTime);
        fullChargeGroundMoveSpeedMultiplier = Mathf.Clamp01(fullChargeGroundMoveSpeedMultiplier);
        aerialBulletTimeScale = Mathf.Clamp(aerialBulletTimeScale, Consts.MinWorldScale, 1f);
        maxStamina = Mathf.Max(0.01f, maxStamina);
        staminaDrainPerSecond = Mathf.Max(0f, staminaDrainPerSecond);
        staminaRecoveryPerSecond = Mathf.Max(0f, staminaRecoveryPerSecond);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        EnsureLayerMasks();
        UpdateMovementContactFilter();
    }

    private void OnDisable()
    {
        CancelAttackCharge();
        isDashing = false;
        isDashRecoiling = false;
        waterZones.Clear();
        activeSwirls.Clear();
        currentWaterZone = null;
        breathWaterZone = null;

        if (body != null)
        {
            RestoreGravity();
            SetEdgeSlipLock(false);
        }
    }

    private void Update()
    {
        UpdateElementalStatuses(Time.deltaTime);
        UpdateStamina();

        if (isFrozen)
        {
            inputX = 0f;
            inputY = 0f;
            jumpHeld = false;
            if (bulletTimeStartedForCharge && fullChargeAutoFireCounter > 0f)
            {
                GameTime.SetSlow(this, aerialBulletTimeScale, fullChargeAutoFireCounter);
            }

            return;
        }

        if (inputLocked)
        {
            inputX = 0f;
            inputY = 0f;
            jumpHeld = false;
            CancelAttackCharge();
            return;
        }

        UpdateDashCooldown(Time.deltaTime);

        inputX = Input.GetAxisRaw("Horizontal");
        inputY = Input.GetAxisRaw("Vertical");
        jumpHeld = Input.GetKey(KeyCode.Space);

        if (!IsUnderwater && Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (Input.GetKeyDown(dashKey))
        {
            TryStartDash();
        }

        if (Input.GetKeyDown(useHealthBottleKey))
        {
            TryUseHealthBottle();
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
        bool wasGroundedOnSlope = IsGrounded && SlopeMovement.IsSlopeNormal(currentGroundNormal);
        UpdateGroundedState();

        if (isFrozen)
        {
            ApplyFrozenPhysics();
            return;
        }

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

        if (ApplySwirlMovement())
        {
            return;
        }

        if (IsUnderwater)
        {
            ApplyUnderwaterMovement();
            return;
        }

        ApplyWorldGravityScale();
        UpdateTimers();
        ApplyMovement();
        bool jumped = ApplyJump();
        if (!jumped)
        {
            PreventSlopeExitLaunch(wasGroundedOnSlope);
        }

        ApplyExtraGravity();
        ApplyIdleEdgeSlipLock();
        ApplyMovingPlatformMotion();
    }

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        if (locked)
        {
            inputX = 0f;
            inputY = 0f;
            jumpBufferCounter = 0f;
            groundIgnoreCounter = 0f;
            CancelAttackCharge();
            StopDash();
        }
    }

    public void TeleportTo(Vector3 position, int facingDirection)
    {
        ClearWaterState();
        activeSwirls.Clear();
        hasUsedDoubleJump = false;
        groundIgnoreCounter = 0f;
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

    public void EnterSwirl(Swirl swirl)
    {
        if (swirl != null && !activeSwirls.Contains(swirl))
        {
            activeSwirls.Add(swirl);
        }
    }

    public void ExitSwirl(Swirl swirl)
    {
        if (swirl != null)
        {
            activeSwirls.Remove(swirl);
        }

        if (!IsInSwirl && !isDashing && !isDashRecoiling && !isFrozen)
        {
            RestoreGravity();
        }
    }

    public bool TakeDamage(int amount)
    {
        if (amount <= 0 || !IsAlive)
        {
            return false;
        }

        currentHp = Mathf.Max(0, currentHp - amount);
        if (currentHp <= 0)
        {
            DieFromDamage();
            return true;
        }

        return false;
    }

    public bool Heal(int amount)
    {
        if (amount <= 0 || !IsAlive || currentHp >= maxHp)
        {
            return false;
        }

        currentHp = Mathf.Min(maxHp, currentHp + amount);
        return true;
    }

    public void RestoreHpToFull()
    {
        currentHp = maxHp;
    }

    public void IncreaseMaxHp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        maxHp += amount;
        RestoreHpToFull();
    }

    public void ResetStamina()
    {
        currentStamina = maxStamina;
    }

    public void IncreaseMaxStamina(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        maxStamina += amount;
        ResetStamina();
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

    public void ApplyFrozen()
    {
        if (!IsAlive)
        {
            return;
        }

        isFrozen = true;
        freezeTimeRemaining = Consts.FreezeDuration;
        inputX = 0f;
        inputY = 0f;
        jumpHeld = false;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        StopDash();
        ApplyAnimatorSpeed();
    }

    public void ApplyPoisoned()
    {
        if (IsAlive)
        {
            isPoisoned = true;
        }
    }

    public void ClearElementalStatuses()
    {
        isBurning = false;
        isFrozen = false;
        isPoisoned = false;
        burningTimeRemaining = 0f;
        burningDamagePerSecond = 0f;
        burningDamageAccumulator = 0f;
        freezeTimeRemaining = 0f;
        ApplyAnimatorSpeed();
    }

    public Collider2D GetCurrentGround()
    {
        return currentGround;
    }

    public bool TryUseHealthBottle()
    {
        if (GameController.Instance == null || !GameController.Instance.TryUseHealthBottle())
        {
            return false;
        }

        Heal(healthBottleHealAmount);
        return true;
    }

    private void ApplyMovement()
    {
        Vector2 velocity = body.velocity;
        float currentMoveSpeed = moveSpeed;
        if (IsGrounded && isAttackFullyCharged)
        {
            currentMoveSpeed *= fullChargeGroundMoveSpeedMultiplier;
        }

        float horizontalSpeed = inputX * currentMoveSpeed * GameTime.WorldScale;
        if (IsGrounded && SlopeMovement.IsSlopeNormal(currentGroundNormal))
        {
            velocity = SlopeMovement.GetSurfaceVelocityForHorizontalSpeed(horizontalSpeed, currentGroundNormal);
        }
        else
        {
            velocity.x = horizontalSpeed;
        }

        body.velocity = velocity;
    }

    private bool ApplyJump()
    {
        if (jumpBufferCounter <= 0f)
        {
            return false;
        }

        if (coyoteCounter <= 0f)
        {
            return TryApplyDoubleJump();
        }

        Vector2 velocity = body.velocity;
        velocity.y = jumpVelocity * GameTime.WorldScale;
        body.velocity = velocity;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        groundIgnoreCounter = jumpGroundIgnoreTime;
        hasUsedDoubleJump = false;
        IsGrounded = false;
        IsOnSafeGround = false;
        currentGroundNormal = Vector2.up;
        return true;
    }

    private bool TryApplyDoubleJump()
    {
        if (IsInSwirl)
        {
            jumpBufferCounter = 0f;
            return false;
        }

        if (!CanDoubleJump())
        {
            return false;
        }

        Vector2 velocity = body.velocity;
        velocity.y = jumpVelocity * GameTime.WorldScale;
        body.velocity = velocity;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        groundIgnoreCounter = jumpGroundIgnoreTime;
        hasUsedDoubleJump = true;
        IsGrounded = false;
        IsOnSafeGround = false;
        currentGroundNormal = Vector2.up;
        return true;
    }

    private bool CanDoubleJump()
    {
        return GameController.Instance != null
            && GameController.Instance.CanDoubleJump
            && !hasUsedDoubleJump
            && !IsUnderwater
            && !IsInSwirl
            && !inputLocked
            && !isFrozen
            && !isDashing
            && !isDashRecoiling;
    }

    private void ApplyUnderwaterMovement()
    {
        if (currentWaterZone == null)
        {
            return;
        }

        body.gravityScale = 0f;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        SetEdgeSlipLock(false);

        Vector2 input = new Vector2(inputX, inputY);
        Vector2 targetVelocity = new Vector2(
            input.x * currentWaterZone.PlayerHorizontalSwimSpeed,
            input.y * currentWaterZone.PlayerVerticalSwimSpeed);
        targetVelocity *= GameTime.WorldScale;

        bool hasSwimInput = input.sqrMagnitude > 0.0001f;
        float rate = hasSwimInput
            ? currentWaterZone.PlayerSwimAcceleration
            : currentWaterZone.PlayerSwimDeceleration;
        body.velocity = Vector2.MoveTowards(body.velocity, targetVelocity, rate * GameTime.FixedDeltaTime);
    }

    private bool ApplySwirlMovement()
    {
        PruneInactiveSwirls();
        if (activeSwirls.Count == 0)
        {
            return false;
        }

        Swirl swirl = activeSwirls[activeSwirls.Count - 1];
        if (swirl == null)
        {
            return false;
        }

        body.gravityScale = 0f;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        SetEdgeSlipLock(false);

        Vector2 velocity = body.velocity;
        float forcedSpeed = swirl.Speed * GameTime.WorldScale;
        float controlSpeed = moveSpeed * GameTime.WorldScale;
        switch (swirl.ForceDirection)
        {
            case GameDirection.Down:
                velocity.y = -forcedSpeed;
                velocity.x = inputX * controlSpeed;
                break;
            case GameDirection.Left:
                velocity.x = -forcedSpeed;
                velocity.y = inputY * controlSpeed;
                break;
            case GameDirection.Right:
                velocity.x = forcedSpeed;
                velocity.y = inputY * controlSpeed;
                break;
            case GameDirection.Up:
            default:
                velocity.y = forcedSpeed;
                velocity.x = inputX * controlSpeed;
                break;
        }

        body.velocity = velocity;
        return true;
    }

    private void ApplyFrozenPhysics()
    {
        if (body == null)
        {
            return;
        }

        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        SetEdgeSlipLock(false);
        body.gravityScale = IsUnderwater
            ? defaultGravityScale * Consts.FrozenWaterBuoyancyGravityScale * GameTime.WorldScale
            : defaultGravityScale * GameTime.WorldScale;
        body.velocity = new Vector2(0f, body.velocity.y);
    }

    private void UpdateElementalStatuses(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        UpdateBurning(deltaTime);
        UpdateFrozen(deltaTime);
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

    private void UpdateFrozen(float deltaTime)
    {
        if (!isFrozen)
        {
            return;
        }

        freezeTimeRemaining -= deltaTime;
        if (freezeTimeRemaining > 0f)
        {
            return;
        }

        isFrozen = false;
        freezeTimeRemaining = 0f;
        RestoreGravity();
        ApplyAnimatorSpeed();
    }

    private void UpdateDashCooldown(float deltaTime)
    {
        if (dashCooldownRemaining > 0f)
        {
            dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - deltaTime);
        }
    }

    private void UpdateStamina()
    {
        breathWaterZone = WaterZone.GetZoneAtPoint(GetBreathPointPosition());
        float deltaTime = GameTime.DeltaTime;
        if (breathWaterZone != null)
        {
            currentStamina = Mathf.MoveTowards(currentStamina, 0f, staminaDrainPerSecond * deltaTime);
            if (currentStamina <= 0f)
            {
                DieFromDrowning();
            }

            return;
        }

        currentStamina = Mathf.MoveTowards(currentStamina, maxStamina, staminaRecoveryPerSecond * deltaTime);
    }

    private Vector2 GetBreathPointPosition()
    {
        if (breathPoint != null)
        {
            return breathPoint.position;
        }

        return (Vector2)transform.position + breathPointFallbackOffset;
    }

    private void DieFromDrowning()
    {
        CachePlayerRespawn();
        if (playerRespawn != null)
        {
            playerRespawn.DieFromEnemy(true);
        }
    }

    private void DieFromDamage()
    {
        CachePlayerRespawn();
        if (playerRespawn != null)
        {
            playerRespawn.DieFromEnemy(true);
        }
    }

    private void ApplyExtraGravity()
    {
        if (IsGrounded)
        {
            return;
        }

        Vector2 velocity = body.velocity;
        if (velocity.y < 0f)
        {
            velocity += Physics2D.gravity * ((fallGravityMultiplier - 1f) * GameTime.FixedDeltaTime);
        }
        else if (velocity.y > 0f && !jumpHeld)
        {
            velocity += Physics2D.gravity * ((lowJumpGravityMultiplier - 1f) * GameTime.FixedDeltaTime);
        }

        float currentMaxFallSpeed = maxFallSpeed * GameTime.WorldScale;
        if (velocity.y < -currentMaxFallSpeed)
        {
            velocity.y = -currentMaxFallSpeed;
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

        if (GetCurrentMovingPlatform() != null)
        {
            return false;
        }

        return true;
    }

    private void ApplyMovingPlatformMotion()
    {
        if (body == null || !IsGrounded)
        {
            return;
        }

        MovingPlatform movingPlatform = GetCurrentMovingPlatform();
        if (movingPlatform == null || movingPlatform.CurrentDelta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        body.position += movingPlatform.CurrentDelta;
    }

    private MovingPlatform GetCurrentMovingPlatform()
    {
        return currentGround != null ? currentGround.GetComponentInParent<MovingPlatform>() : null;
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
            fullChargeAutoFireCounter = GetFullChargeHoldDuration();
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
        fullChargeAutoFireCounter = 0f;
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
            bool bulletIsPiercing = fullChargeShot && bulletElement != BulletElement.Fire;
            bullet.Configure(BulletSource.Player, shotDirection, bulletSpeed, !fullChargeShot, bulletIsPiercing, bulletElement, fullChargeShot);
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
        if (bulletTimeStartedForCharge || IsGrounded || fullChargeAutoFireCounter <= 0f)
        {
            return;
        }

        bulletTimeStartedForCharge = true;
        GameTime.SetSlow(this, aerialBulletTimeScale, fullChargeAutoFireCounter);
    }

    private float GetFullChargeHoldDuration()
    {
        return Mathf.Max(0f, currentStamina);
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
        if (body == null || inputLocked || isFrozen || isDashing || isDashRecoiling || dashCooldownRemaining > 0f)
        {
            return;
        }

        dashDirection = FacingDirection == GameDirection.Left ? -1 : 1;
        dashTimeRemaining = dashDuration;
        dashCooldownRemaining = dashCooldown;
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
        dashTimeRemaining -= GameTime.FixedDeltaTime;
        body.velocity = new Vector2(GetDashSpeed() * dashDirection, 0f);

        if (dashTimeRemaining <= 0f)
        {
            StopDash();
        }
    }

    private void ApplyDashRecoil()
    {
        dashRecoilTimeRemaining -= GameTime.FixedDeltaTime;
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
        return (dashDistance / Mathf.Max(0.01f, dashDuration)) * GameTime.WorldScale;
    }

    private float GetDashRecoilSpeed()
    {
        return (dashRecoilDistance / Mathf.Max(0.01f, dashRecoilDuration)) * GameTime.WorldScale;
    }

    private void SetDashGravity()
    {
        body.gravityScale = 0f;
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

        float animatorSpeed = isFrozen ? 0f : GameTime.WorldScale;
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].speed = animatorSpeed;
            }
        }
    }

    private void CachePlayerRespawn()
    {
        if (playerRespawn == null)
        {
            playerRespawn = GetComponent<PlayerRespawn>();
        }
    }

    private void RestoreGravity()
    {
        if (IsUnderwater)
        {
            body.gravityScale = 0f;
            return;
        }

        ApplyWorldGravityScale();
    }

    private void ApplyWorldGravityScale()
    {
        body.gravityScale = defaultGravityScale * GameTime.WorldScale;
    }

    private void UpdateGroundedState()
    {
        if (ShouldIgnoreGroundAfterJump())
        {
            currentGround = null;
            currentGroundNormal = Vector2.up;
            IsGrounded = false;
            IsOnSafeGround = false;
            groundIgnoreCounter = Mathf.Max(0f, groundIgnoreCounter - Time.fixedDeltaTime);
            return;
        }

        groundIgnoreCounter = 0f;

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
            currentGroundNormal = support.Normal;
        }
        else
        {
            Vector2 center = (Vector2)transform.position + groundCheckOffset;
            currentGround = Physics2D.OverlapBox(center, groundCheckSize, 0f, movementGroundMask);
            currentGroundNormal = Vector2.up;
        }

        IsGrounded = currentGround != null;
        IsOnSafeGround = IsGrounded && Utils.IsColliderOnMask(currentGround, safeGroundMask);
        if (IsGrounded && !IsUnderwater)
        {
            hasUsedDoubleJump = false;
        }
    }

    private bool ShouldIgnoreGroundAfterJump()
    {
        return groundIgnoreCounter > 0f && body != null && body.velocity.y > 0f;
    }

    private void PreventSlopeExitLaunch(bool wasGroundedOnSlope)
    {
        if (!wasGroundedOnSlope || IsGrounded || body == null || body.velocity.y <= 0f)
        {
            return;
        }

        body.velocity = new Vector2(body.velocity.x, 0f);
    }

    private void SetFacingDirection(int facingDirection)
    {
        FacingDirection = GameDirection.NormalizeOrDefault(facingDirection, FacingDirection);
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
            dashStopMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Obstacle, GameLayers.PierceObstacle, GameLayers.Platform, GameLayers.Enemy);
        }

        IncludePierceObstacleWithObstacleDashMask();
    }

    private void IncludePierceObstacleWithObstacleDashMask()
    {
        int obstacleLayer = LayerMask.NameToLayer(GameLayers.Obstacle);
        int pierceObstacleLayer = LayerMask.NameToLayer(GameLayers.PierceObstacle);
        if (obstacleLayer < 0 || pierceObstacleLayer < 0 || !Utils.IsLayerInMask(obstacleLayer, dashStopMask))
        {
            return;
        }

        dashStopMask.value |= 1 << pierceObstacleLayer;
    }

    private void UpdateMovementContactFilter()
    {
        movementContactFilter.useTriggers = false;
        movementContactFilter.SetLayerMask(movementGroundMask);
    }

    private void PruneInactiveSwirls()
    {
        for (int i = activeSwirls.Count - 1; i >= 0; i--)
        {
            Swirl swirl = activeSwirls[i];
            if (swirl == null || !swirl.isActiveAndEnabled || !swirl.IsActive)
            {
                activeSwirls.RemoveAt(i);
            }
        }
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
        EnterWater(Utils.GetWaterZone(other));
        HandleDashTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        EnterWater(Utils.GetWaterZone(other));
        HandleDashTrigger(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        ExitWater(Utils.GetWaterZone(other));
    }

    private void HandleDashCollision(Collision2D collision)
    {
        if (!isDashing || collision == null || collision.collider == null)
        {
            return;
        }

        bool hitEnemy = Utils.IsEnemyCollider(collision.collider);
        if (!hitEnemy && !Utils.IsLayerInMask(collision.collider.gameObject.layer, dashStopMask))
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

        if (Utils.IsEnemyCollider(other))
        {
            StopDashWithRecoil();
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
        body.gravityScale = 0f;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        SetEdgeSlipLock(false);
    }

    private void ClearWaterState()
    {
        waterZones.Clear();
        currentWaterZone = null;
        breathWaterZone = null;
        if (body != null)
        {
            RestoreGravity();
        }
    }

    private void ExitWater(WaterZone waterZone)
    {
        if (waterZone == null)
        {
            return;
        }

        bool wasCurrent = currentWaterZone == waterZone;
        waterZones.Remove(waterZone);
        currentWaterZone = waterZones.Count > 0 ? waterZones[waterZones.Count - 1] : null;

        if (currentWaterZone != null)
        {
            return;
        }

        RestoreGravity();
        if (wasCurrent && (inputY > 0.01f || body.velocity.y > 0.01f))
        {
            body.velocity = new Vector2(body.velocity.x, Mathf.Max(body.velocity.y, waterZone.WaterExitBoost * GameTime.WorldScale));
        }
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

public struct GroundSupport
{
    public readonly Collider2D Collider;
    public readonly Vector2 Normal;

    public GroundSupport(Collider2D collider, Vector2 normal)
    {
        Collider = collider;
        Normal = normal;
    }
}

public static class SlopeMovement
{
    private const float SlopeNormalXEpsilon = 0.01f;
    private const float MinTangentX = 0.001f;

    public static bool IsSlopeNormal(Vector2 normal)
    {
        normal = SanitizeGroundNormal(normal);
        return normal.y > 0f && Mathf.Abs(normal.x) > SlopeNormalXEpsilon;
    }

    public static Vector2 GetSurfaceVelocityForHorizontalSpeed(float horizontalSpeed, Vector2 normal)
    {
        normal = SanitizeGroundNormal(normal);
        Vector2 tangent = new Vector2(normal.y, -normal.x);
        if (Mathf.Abs(tangent.x) < MinTangentX)
        {
            return new Vector2(horizontalSpeed, 0f);
        }

        return tangent * (horizontalSpeed / tangent.x);
    }

    public static bool TryFindSupport(
        Rigidbody2D body,
        ContactFilter2D filter,
        ContactPoint2D[] contacts,
        float minNormalY,
        float maxSupportPointY,
        LayerMask groundMask,
        out GroundSupport support)
    {
        support = new GroundSupport(null, Vector2.up);
        if (body == null || contacts == null)
        {
            return false;
        }

        int contactCount = body.GetContacts(filter, contacts);
        float bestNormalY = -1f;
        Collider2D bestCollider = null;
        Vector2 bestNormal = Vector2.up;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = contacts[i];
            if (contact.point.y > maxSupportPointY)
            {
                continue;
            }

            Vector2 normal = SanitizeGroundNormal(contact.normal);
            if (normal.y < minNormalY)
            {
                continue;
            }

            Collider2D ground = GetGroundColliderFromContact(contact, groundMask);
            if (ground == null || normal.y <= bestNormalY)
            {
                continue;
            }

            bestCollider = ground;
            bestNormal = normal;
            bestNormalY = normal.y;
        }

        if (bestCollider == null)
        {
            return false;
        }

        support = new GroundSupport(bestCollider, bestNormal);
        return true;
    }

    private static Vector2 SanitizeGroundNormal(Vector2 normal)
    {
        if (normal.sqrMagnitude <= 0.0001f)
        {
            return Vector2.up;
        }

        normal.Normalize();
        return normal.y < 0f ? -normal : normal;
    }

    private static Collider2D GetGroundColliderFromContact(ContactPoint2D contact, LayerMask groundMask)
    {
        if (Utils.IsColliderOnMask(contact.collider, groundMask))
        {
            return contact.collider;
        }

        if (Utils.IsColliderOnMask(contact.otherCollider, groundMask))
        {
            return contact.otherCollider;
        }

        return null;
    }

}
