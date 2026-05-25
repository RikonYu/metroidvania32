using UnityEngine;

public enum WaterChargeAIState
{
    WanderPause,
    WanderMove,
    AlertPause,
    Charge,
    BlockedPause,
    RecoverToPatrol
}

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Rigidbody2D))]
public class WaterChargeAI : EnemyAI
{
    private const float PositionEpsilon = 0.0001f;

    [Header("Target")]
    [SerializeField] private Transform waterTargetOverride;
    [SerializeField] private float waterTargetSearchInterval = 0.5f;

    [Header("Vision")]
    [SerializeField] private int waterFacingDirection = GameDirection.Left;
    [SerializeField] private float waterViewDistance = 8f;
    [SerializeField, Range(1f, 180f)] private float waterViewHalfAngle = 45f;
    [SerializeField] private LayerMask visionBlockMask;

    [Header("Patrol Area")]
    [SerializeField] private Vector2 patrolCenter;
    [SerializeField] private float patrolRadius = 4f;
    [SerializeField] private bool useInitialPositionAsPatrolCenter = true;
    [SerializeField] private float waterPatrolPointReachDistance = 0.15f;
    [SerializeField, HideInInspector] private Vector2 patrolSize;
    [SerializeField, HideInInspector] private bool migratedPatrolRadius;

    [Header("Wander")]
    [SerializeField] private float wanderSpeed = 2f;
    [SerializeField] private float wanderPauseMin = 0.4f;
    [SerializeField] private float wanderPauseMax = 1.2f;

    [Header("Charge")]
    [SerializeField] private float alertPauseDuration = 0.25f;
    [SerializeField] private float chargeSpeed = 10f;
    [SerializeField] private float maxChargeDistance = 8f;
    [SerializeField] private float blockedPauseDuration = 0.25f;
    [SerializeField] private LayerMask terrainStopMask;

    [Header("Recover")]
    [SerializeField] private float recoverSpeed = 3f;
    [SerializeField] private float recoverReachDistance = 0.12f;

    [Header("Debug")]
    [SerializeField] private WaterChargeAIState waterState = WaterChargeAIState.WanderPause;

    private Rigidbody2D body;
    private Transform target;
    private float targetSearchTimer;
    private float stateTimer;
    private Vector2 wanderTarget;
    private Vector2 alertTargetPosition;
    private Vector2 chargeDirection;
    private Vector2 chargeStartPosition;
    private bool terrainCollisionQueued;
    private bool capturedInitialPatrolCenter;
    private Collider2D[] diveColliders;
    private bool[] diveColliderEnabledStates;
    private bool diveCollisionsDisabled;

    protected override void Awake()
    {
        base.Awake();
        CacheBody();
        CaptureInitialPatrolCenter();
        NormalizeWaterChargeValues();
        BeginWanderPause();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RestoreDiveCollisions();
        CacheBody();
        CaptureInitialPatrolCenter();
        targetSearchTimer = 0f;
        terrainCollisionQueued = false;
        BeginWanderPause();
    }

    private void OnDisable()
    {
        RestoreDiveCollisions();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        NormalizeWaterChargeValues();
    }

    protected override void Update()
    {
        CacheEnemy();
        CacheBody();
        if (!CanRunAI())
        {
            StopEnemy();
            return;
        }

        UpdateTarget(Time.deltaTime);

        float deltaTime = GetScaledDeltaTime();
        if (deltaTime <= 0f)
        {
            StopEnemy();
            return;
        }

        switch (waterState)
        {
            case WaterChargeAIState.WanderPause:
                UpdateWanderPause(deltaTime);
                break;
            case WaterChargeAIState.WanderMove:
                UpdateWanderMove();
                break;
            case WaterChargeAIState.AlertPause:
                UpdateAlertPause(deltaTime);
                break;
            case WaterChargeAIState.Charge:
                UpdateCharge();
                break;
            case WaterChargeAIState.BlockedPause:
                UpdateBlockedPause(deltaTime);
                break;
            case WaterChargeAIState.RecoverToPatrol:
                UpdateRecoverToPatrol();
                break;
        }
    }

    protected override void FixedUpdate()
    {
        CacheEnemy();
        CacheBody();
        if (!CanRunAI() || body == null || Enemy.IsFrozen)
        {
            StopEnemy();
            return;
        }

        switch (waterState)
        {
            case WaterChargeAIState.WanderMove:
                MoveTowards(wanderTarget, wanderSpeed);
                break;
            case WaterChargeAIState.Charge:
                MoveCharge();
                break;
            case WaterChargeAIState.RecoverToPatrol:
                MoveTowards(GetClosestPointInPatrolRange(body.position), recoverSpeed);
                break;
            default:
                StopEnemy();
                break;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleTerrainCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleTerrainCollision(collision);
    }

    private void UpdateWanderPause(float deltaTime)
    {
        if (!IsInsidePatrolRange(GetPosition()))
        {
            BeginRecoverToPatrol();
            return;
        }

        if (TryBeginAlert())
        {
            return;
        }

        stateTimer -= deltaTime;
        if (stateTimer <= 0f)
        {
            ChooseWanderTarget();
            waterState = WaterChargeAIState.WanderMove;
        }
    }

    private void UpdateWanderMove()
    {
        Vector2 position = GetPosition();
        if (!IsInsidePatrolRange(position))
        {
            BeginRecoverToPatrol();
            return;
        }

        if (TryBeginAlert())
        {
            return;
        }

        if (HasReached(position, wanderTarget, waterPatrolPointReachDistance))
        {
            BeginWanderPause();
        }
    }

    private void UpdateAlertPause(float deltaTime)
    {
        stateTimer -= deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        Vector2 toTarget = alertTargetPosition - GetPosition();
        chargeDirection = Utils.NormalizeOrFallback(toTarget, (Vector2)GameDirection.ToVector3(waterFacingDirection));
        chargeStartPosition = GetPosition();
        terrainCollisionQueued = false;
        waterState = WaterChargeAIState.Charge;
    }

    private void UpdateCharge()
    {
        Vector2 position = GetPosition();
        if (!IsInsidePatrolRange(position))
        {
            BeginRecoverToPatrol();
            return;
        }

        if (terrainCollisionQueued)
        {
            terrainCollisionQueued = false;
            BeginBlockedPause();
            return;
        }

        if ((position - chargeStartPosition).sqrMagnitude >= maxChargeDistance * maxChargeDistance)
        {
            BeginBlockedPause();
        }
    }

    private void UpdateBlockedPause(float deltaTime)
    {
        stateTimer -= deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        if (!IsInsidePatrolRange(GetPosition()))
        {
            BeginRecoverToPatrol();
            return;
        }

        BeginWanderPause();
    }

    private void UpdateRecoverToPatrol()
    {
        Vector2 position = GetPosition();
        Vector2 closest = GetClosestPointInPatrolRange(position);
        if (HasReached(position, closest, recoverReachDistance))
        {
            BeginWanderPause();
        }
    }

    private bool TryBeginAlert()
    {
        if (!CanSeeTarget(out Vector2 targetPosition))
        {
            return false;
        }

        alertTargetPosition = targetPosition;
        waterState = WaterChargeAIState.AlertPause;
        stateTimer = alertPauseDuration;
        StopEnemy();
        return true;
    }

    private void BeginWanderPause()
    {
        RestoreDiveCollisions();
        waterState = WaterChargeAIState.WanderPause;
        stateTimer = Random.Range(wanderPauseMin, wanderPauseMax);
        StopEnemy();
    }

    private void BeginBlockedPause()
    {
        RestoreDiveCollisions();
        waterState = WaterChargeAIState.BlockedPause;
        stateTimer = blockedPauseDuration;
        StopEnemy();
    }

    private void BeginRecoverToPatrol()
    {
        waterState = WaterChargeAIState.RecoverToPatrol;
        DisableDiveCollisions();
        StopEnemy();
    }

    private void ChooseWanderTarget()
    {
        wanderTarget = PatrolCenter + Random.insideUnitCircle * PatrolRadius;
    }

    private void MoveTowards(Vector2 targetPoint, float speed)
    {
        Vector2 toTarget = targetPoint - GetPosition();
        Vector2 direction = Utils.NormalizeOrZero(toTarget);
        SetVelocity(direction * speed);
        UpdateFacingFromVelocity(direction);
    }

    private void MoveCharge()
    {
        if (!ReflectChargeDirectionIfLeavingWater())
        {
            return;
        }

        SetVelocity(chargeDirection * chargeSpeed);
    }

    private bool ReflectChargeDirectionIfLeavingWater()
    {
        Vector2 position = GetPosition();
        if (WaterZone.GetZoneAtPoint(position) == null)
        {
            BeginRecoverToPatrol();
            return false;
        }

        Vector2 nextPosition = position + chargeDirection * chargeSpeed * Time.fixedDeltaTime;
        if (WaterZone.GetZoneAtPoint(nextPosition) != null)
        {
            return true;
        }

        Vector2 xOnly = new Vector2(nextPosition.x, position.y);
        Vector2 yOnly = new Vector2(position.x, nextPosition.y);
        bool hitVerticalSurface = WaterZone.GetZoneAtPoint(xOnly) == null;
        bool hitHorizontalSurface = WaterZone.GetZoneAtPoint(yOnly) == null;

        if (hitVerticalSurface)
        {
            chargeDirection.x = -chargeDirection.x;
        }

        if (hitHorizontalSurface)
        {
            chargeDirection.y = -chargeDirection.y;
        }

        if (!hitVerticalSurface && !hitHorizontalSurface)
        {
            chargeDirection = -chargeDirection;
        }

        chargeDirection = Utils.NormalizeOrFallback(chargeDirection, Vector2.down);
        return true;
    }

    private void SetVelocity(Vector2 velocity)
    {
        if (body == null)
        {
            return;
        }

        body.velocity = velocity * GameTime.WorldScale;
    }

    private new void StopEnemy()
    {
        base.StopEnemy();
        if (body != null)
        {
            body.velocity = Vector2.zero;
        }
    }

    private bool CanSeeTarget(out Vector2 targetPosition)
    {
        targetPosition = Vector2.zero;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector2 origin = GetPosition();
        Vector2 targetPoint = target.position;
        if (WaterZone.GetZoneAtPoint(origin) == null || WaterZone.GetZoneAtPoint(targetPoint) == null)
        {
            return false;
        }

        Vector2 toTarget = targetPoint - origin;
        float sqrDistance = toTarget.sqrMagnitude;
        if (sqrDistance > waterViewDistance * waterViewDistance)
        {
            return false;
        }

        if (sqrDistance > PositionEpsilon)
        {
            Vector2 directionToTarget = toTarget.normalized;
            Vector2 forward = GameDirection.ToVector3(waterFacingDirection);
            float minDot = Mathf.Cos(waterViewHalfAngle * Mathf.Deg2Rad);
            if (Vector2.Dot(forward.normalized, directionToTarget) < minDot)
            {
                return false;
            }
        }

        if (Physics2D.Linecast(origin, targetPoint, visionBlockMask).collider != null)
        {
            return false;
        }

        targetPosition = targetPoint;
        return true;
    }

    private void UpdateTarget(float deltaTime)
    {
        if (waterTargetOverride != null)
        {
            target = waterTargetOverride;
            return;
        }

        if (target != null && target.gameObject.activeInHierarchy)
        {
            return;
        }

        targetSearchTimer -= deltaTime;
        if (targetSearchTimer > 0f)
        {
            return;
        }

        targetSearchTimer = waterTargetSearchInterval;
        MCController player = FindObjectOfType<MCController>();
        if (player != null)
        {
            target = player.transform;
            return;
        }

        PlayerRespawn respawn = FindObjectOfType<PlayerRespawn>();
        target = respawn != null ? respawn.transform : null;
    }

    private void HandleTerrainCollision(Collision2D collision)
    {
        if (collision == null || waterState != WaterChargeAIState.Charge)
        {
            return;
        }

        Collider2D collider2D = collision.collider;
        if (collider2D != null && Utils.IsLayerInMask(collider2D.gameObject.layer, terrainStopMask))
        {
            terrainCollisionQueued = true;
        }
    }

    private void UpdateFacingFromVelocity(Vector2 velocity)
    {
        if (velocity.sqrMagnitude <= PositionEpsilon)
        {
            return;
        }

        waterFacingDirection = Utils.GetCardinalDirectionFromVector(velocity);
    }

    private Vector2 GetPosition()
    {
        return body != null ? body.position : (Vector2)transform.position;
    }

    private bool IsInsidePatrolRange(Vector2 position)
    {
        return (position - PatrolCenter).sqrMagnitude <= PatrolRadius * PatrolRadius;
    }

    private Vector2 GetClosestPointInPatrolRange(Vector2 position)
    {
        Vector2 center = PatrolCenter;
        Vector2 toPosition = position - center;
        float radius = PatrolRadius;
        if (toPosition.sqrMagnitude <= radius * radius)
        {
            return position;
        }

        return center + Utils.NormalizeOrFallback(toPosition, Vector2.right) * radius;
    }

    private bool HasReached(Vector2 position, Vector2 targetPoint, float reachDistance)
    {
        return (position - targetPoint).sqrMagnitude <= reachDistance * reachDistance;
    }

    private float PatrolRadius
    {
        get { return Mathf.Max(0.01f, patrolRadius); }
    }

    private Vector2 PatrolCenter
    {
        get
        {
            if (!Application.isPlaying && useInitialPositionAsPatrolCenter)
            {
                return transform.position;
            }

            return patrolCenter;
        }
    }

    private void CacheBody()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void DisableDiveCollisions()
    {
        if (diveCollisionsDisabled)
        {
            return;
        }

        diveColliders = GetComponentsInChildren<Collider2D>(true);
        if (diveColliderEnabledStates == null || diveColliderEnabledStates.Length != diveColliders.Length)
        {
            diveColliderEnabledStates = new bool[diveColliders.Length];
        }

        for (int i = 0; i < diveColliders.Length; i++)
        {
            Collider2D collider2D = diveColliders[i];
            if (collider2D == null)
            {
                continue;
            }

            diveColliderEnabledStates[i] = collider2D.enabled;
            collider2D.enabled = false;
        }

        diveCollisionsDisabled = true;
    }

    private void RestoreDiveCollisions()
    {
        if (!diveCollisionsDisabled || diveColliders == null || diveColliderEnabledStates == null)
        {
            diveCollisionsDisabled = false;
            return;
        }

        int count = Mathf.Min(diveColliders.Length, diveColliderEnabledStates.Length);
        for (int i = 0; i < count; i++)
        {
            Collider2D collider2D = diveColliders[i];
            if (collider2D != null)
            {
                collider2D.enabled = diveColliderEnabledStates[i];
            }
        }

        diveCollisionsDisabled = false;
    }

    private void CaptureInitialPatrolCenter()
    {
        if (capturedInitialPatrolCenter || !useInitialPositionAsPatrolCenter)
        {
            return;
        }

        patrolCenter = transform.position;
        capturedInitialPatrolCenter = true;
    }

    private void NormalizeWaterChargeValues()
    {
        MigrateLegacyPatrolSize();
        waterTargetSearchInterval = Mathf.Max(0.01f, waterTargetSearchInterval);
        waterFacingDirection = GameDirection.NormalizeOrDefault(waterFacingDirection, GameDirection.Left);
        waterViewDistance = Mathf.Max(0f, waterViewDistance);
        waterViewHalfAngle = Mathf.Clamp(waterViewHalfAngle, 1f, 180f);
        patrolRadius = PatrolRadius;
        waterPatrolPointReachDistance = Mathf.Max(0.01f, waterPatrolPointReachDistance);
        wanderSpeed = Mathf.Max(0f, wanderSpeed);
        wanderPauseMin = Mathf.Max(0f, wanderPauseMin);
        wanderPauseMax = Mathf.Max(wanderPauseMin, wanderPauseMax);
        alertPauseDuration = Mathf.Max(0f, alertPauseDuration);
        chargeSpeed = Mathf.Max(0f, chargeSpeed);
        maxChargeDistance = Mathf.Max(0.01f, maxChargeDistance);
        blockedPauseDuration = Mathf.Max(0f, blockedPauseDuration);
        recoverSpeed = Mathf.Max(0f, recoverSpeed);
        recoverReachDistance = Mathf.Max(0.01f, recoverReachDistance);

        if (visionBlockMask == 0)
        {
            visionBlockMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Obstacle, GameLayers.Platform);
        }

        if (terrainStopMask == 0)
        {
            terrainStopMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Obstacle, GameLayers.Platform);
        }
    }

    private void MigrateLegacyPatrolSize()
    {
        if (migratedPatrolRadius)
        {
            return;
        }

        if (patrolSize.x > 0f || patrolSize.y > 0f)
        {
            patrolRadius = Mathf.Max(patrolSize.x, patrolSize.y) * 0.5f;
            patrolSize = Vector2.zero;
        }

        migratedPatrolRadius = true;
    }

    protected override void OnDrawGizmosSelected()
    {
        NormalizeWaterChargeValues();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(wanderTarget, 0.15f);

        Vector3 origin = transform.position;
        Vector3 forward = GameDirection.ToVector3(waterFacingDirection);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(origin, origin + forward * waterViewDistance);

        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Vector3 leftEdge = Quaternion.Euler(0f, 0f, waterViewHalfAngle) * forward;
        Vector3 rightEdge = Quaternion.Euler(0f, 0f, -waterViewHalfAngle) * forward;
        Gizmos.DrawLine(origin, origin + leftEdge * waterViewDistance);
        Gizmos.DrawLine(origin, origin + rightEdge * waterViewDistance);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        DrawPatrolRangeGizmo();
    }

    private void DrawPatrolRangeGizmo()
    {
        const int SegmentCount = 64;
        float radius = PatrolRadius;
        Vector2 patrolCenterPoint = PatrolCenter;
        Vector3 center = new Vector3(patrolCenterPoint.x, patrolCenterPoint.y, transform.position.z);
        Vector3 previous = center + Vector3.right * radius;
        for (int i = 1; i <= SegmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / SegmentCount;
            Vector3 next = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
