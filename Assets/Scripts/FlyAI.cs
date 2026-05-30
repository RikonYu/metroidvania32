using UnityEngine;

public enum FlyAIState
{
    ChooseMoveTarget,
    Move,
    Wait,
    RecoverVertical
}

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Rigidbody2D))]
public class FlyAI : EnemyAI
{
    private const float PositionEpsilon = 0.0001f;

    [Header("Target")]
    [SerializeField] private Transform flyTargetOverride;
    [SerializeField] private float flyTargetSearchInterval = 0.5f;

    [Header("Vision")]
    [SerializeField] private int flyFacingDirection = GameDirection.Left;
    [SerializeField] private float flyViewDistance = 8f;
    [SerializeField, Range(1f, 180f)] private float flyViewHalfAngle = 45f;
    [SerializeField] private LayerMask visionBlockMask;

    [Header("Patrol Area")]
    [SerializeField] private Vector2 patrolSize = new Vector2(6f, 4f);
    [SerializeField] private float flyPatrolPointReachDistance = 0.15f;

    [Header("Movement")]
    [SerializeField] private float flyMoveSpeed = 2.5f;
    [SerializeField] private float waitAfterCheck = 0.35f;

    [Header("Frozen")]
    [SerializeField] private float frozenFallSpeed = 5f;
    [SerializeField] private float frozenRiseSpeed = 3f;
    [SerializeField] private float recoverVerticalSpeed = 3f;
    [SerializeField] private LayerMask frozenStopMask;
    [SerializeField] private LayerMask frozenDestroyMask;

    [Header("Debug")]
    [SerializeField] private FlyAIState flyState = FlyAIState.ChooseMoveTarget;

    private Rigidbody2D body;
    private Transform flyTarget;
    private Vector2 patrolCenter;
    private Vector2 moveTarget;
    private float flyTargetSearchTimer;
    private float waitTimer;
    private bool capturedPatrolCenter;
    private bool wasFrozen;
    private bool freezeStartedUnderwater;
    private bool frozenBlockedByTerrain;

    protected override void Awake()
    {
        base.Awake();
        CacheBody();
        CapturePatrolCenter();
        NormalizeFlyValues();
        BeginChooseMoveTarget();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheBody();
        CapturePatrolCenter();
        flyTargetSearchTimer = 0f;
        wasFrozen = false;
        freezeStartedUnderwater = false;
        frozenBlockedByTerrain = false;
        BeginChooseMoveTarget();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        NormalizeFlyValues();
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
        TrackFrozenTransition();
        if (Enemy.IsFrozen)
        {
            return;
        }

        float deltaTime = Time.deltaTime * GameTime.WorldScale;
        if (deltaTime <= 0f)
        {
            return;
        }

        switch (flyState)
        {
            case FlyAIState.ChooseMoveTarget:
                UpdateChooseMoveTarget();
                break;
            case FlyAIState.Move:
                UpdateMove();
                break;
            case FlyAIState.Wait:
                UpdateWait(deltaTime);
                break;
            case FlyAIState.RecoverVertical:
                UpdateRecoverVertical();
                break;
        }
    }

    protected override void FixedUpdate()
    {
        CacheEnemy();
        CacheBody();
        if (!CanRunAI() || body == null)
        {
            StopEnemy();
            return;
        }

        if (Enemy.IsFrozen)
        {
            ApplyFrozenMovement();
            return;
        }

        switch (flyState)
        {
            case FlyAIState.Move:
                MoveTowards(moveTarget, flyMoveSpeed);
                break;
            case FlyAIState.RecoverVertical:
                MoveRecoverVertical();
                break;
            default:
                StopEnemy();
                break;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleFrozenCollision(collision != null ? collision.collider : null);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleFrozenCollision(collision != null ? collision.collider : null);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleFrozenTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleFrozenTrigger(other);
    }

    private void UpdateChooseMoveTarget()
    {
        if (IsTargetInsidePatrolRect())
        {
            TryShootVisibleTarget();
            BeginWait();
            return;
        }

        moveTarget = GetRandomPointInPatrolRect();
        flyState = FlyAIState.Move;
    }

    private void UpdateMove()
    {
        if (IsTargetInsidePatrolRect())
        {
            TryShootVisibleTarget();
            BeginWait();
            return;
        }

        if (HasReached(GetPosition(), moveTarget, flyPatrolPointReachDistance))
        {
            TryShootVisibleTarget();
            BeginWait();
        }
    }

    private void UpdateWait(float deltaTime)
    {
        waitTimer -= deltaTime;
        if (waitTimer > 0f)
        {
            return;
        }

        flyState = FlyAIState.ChooseMoveTarget;
    }

    private void UpdateRecoverVertical()
    {
        Vector2 position = GetPosition();
        if (!IsXInsidePatrolRect(position.x))
        {
            Enemy.Die();
            return;
        }

        float targetY = Mathf.Clamp(position.y, PatrolMin.y, PatrolMax.y);
        if (Mathf.Abs(position.y - targetY) <= flyPatrolPointReachDistance)
        {
            BeginChooseMoveTarget();
        }
    }

    private void MoveTowards(Vector2 targetPoint, float speed)
    {
        Vector2 toTarget = targetPoint - GetPosition();
        Vector2 direction = Utils.NormalizeOrZero(toTarget);
        SetVelocity(direction * speed);
        UpdateFacingFromVelocity(direction);
    }

    private void MoveRecoverVertical()
    {
        Vector2 position = GetPosition();
        float targetY = Mathf.Clamp(position.y, PatrolMin.y, PatrolMax.y);
        float diff = targetY - position.y;
        if (Mathf.Abs(diff) <= flyPatrolPointReachDistance)
        {
            StopEnemy();
            return;
        }

        Vector2 direction = diff > 0f ? Vector2.up : Vector2.down;
        SetVelocity(direction * recoverVerticalSpeed);
    }

    private void TryShootVisibleTarget()
    {
        if (!CanSeeTarget(out Vector2 targetPosition))
        {
            return;
        }

        Vector2 toTarget = targetPosition - GetPosition();
        Vector2 direction = Utils.NormalizeOrFallback(toTarget, GameDirection.ToVector3(flyFacingDirection));
        Enemy.FireBullet(direction);
    }

    private bool CanSeeTarget(out Vector2 targetPosition)
    {
        targetPosition = Vector2.zero;
        if (flyTarget == null || !flyTarget.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector2 origin = GetPosition();
        targetPosition = flyTarget.position;
        Vector2 toTarget = targetPosition - origin;
        float sqrDistance = toTarget.sqrMagnitude;
        if (sqrDistance > flyViewDistance * flyViewDistance)
        {
            return false;
        }

        if (sqrDistance > PositionEpsilon)
        {
            Vector2 directionToTarget = toTarget.normalized;
            Vector2 forward = GameDirection.ToVector3(flyFacingDirection);
            float minDot = Mathf.Cos(flyViewHalfAngle * Mathf.Deg2Rad);
            if (Vector2.Dot(forward.normalized, directionToTarget) < minDot)
            {
                return false;
            }
        }

        return !IsVisionBlocked(origin, targetPosition);
    }

    private bool IsVisionBlocked(Vector2 origin, Vector2 targetPosition)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(origin, targetPosition, visionBlockMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<Bubble>() != null || hit.GetComponentInParent<IceObstacle>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void ApplyFrozenMovement()
    {
        if (body == null || frozenBlockedByTerrain)
        {
            StopEnemy();
            return;
        }

        bool underwater = WaterZone.GetZoneAtPoint(GetPosition()) != null;
        if (underwater && !freezeStartedUnderwater)
        {
            Enemy.Die();
            return;
        }

        float verticalSpeed = underwater ? frozenRiseSpeed : -frozenFallSpeed;
        body.gravityScale = 0f;
        body.velocity = new Vector2(0f, verticalSpeed * GameTime.WorldScale);
    }

    private void TrackFrozenTransition()
    {
        bool isFrozen = Enemy.IsFrozen;
        if (isFrozen && !wasFrozen)
        {
            freezeStartedUnderwater = WaterZone.GetZoneAtPoint(GetPosition()) != null;
            frozenBlockedByTerrain = false;
            wasFrozen = true;
            return;
        }

        if (!isFrozen && wasFrozen)
        {
            wasFrozen = false;
            freezeStartedUnderwater = false;
            frozenBlockedByTerrain = false;
            BeginRecoverVerticalAfterFreeze();
        }
    }

    private void BeginRecoverVerticalAfterFreeze()
    {
        Vector2 position = GetPosition();
        if (!IsXInsidePatrolRect(position.x))
        {
            Enemy.Die();
            return;
        }

        if (IsYInsidePatrolRect(position.y))
        {
            BeginChooseMoveTarget();
            return;
        }

        flyState = FlyAIState.RecoverVertical;
        StopEnemy();
    }

    private void HandleFrozenCollision(Collider2D other)
    {
        if (!Enemy.IsFrozen || other == null)
        {
            return;
        }

        if (Utils.IsLayerInMask(other.gameObject.layer, frozenDestroyMask))
        {
            Enemy.Die();
            return;
        }

        if (Utils.IsLayerInMask(other.gameObject.layer, frozenStopMask))
        {
            frozenBlockedByTerrain = true;
            StopEnemy();
        }
    }

    private void HandleFrozenTrigger(Collider2D other)
    {
        if (!Enemy.IsFrozen || other == null)
        {
            return;
        }

        if (Utils.IsLayerInMask(other.gameObject.layer, frozenDestroyMask))
        {
            Enemy.Die();
            return;
        }

        if (!freezeStartedUnderwater && Utils.GetWaterZone(other) != null && WaterZone.GetZoneAtPoint(GetPosition()) != null)
        {
            Enemy.Die();
        }
    }

    private void BeginChooseMoveTarget()
    {
        flyState = FlyAIState.ChooseMoveTarget;
        StopEnemy();
    }

    private void BeginWait()
    {
        flyState = FlyAIState.Wait;
        waitTimer = waitAfterCheck;
        StopEnemy();
    }

    private void UpdateTarget(float deltaTime)
    {
        if (flyTargetOverride != null)
        {
            flyTarget = flyTargetOverride;
            return;
        }

        if (flyTarget != null && flyTarget.gameObject.activeInHierarchy)
        {
            return;
        }

        flyTargetSearchTimer -= deltaTime;
        if (flyTargetSearchTimer > 0f)
        {
            return;
        }

        flyTargetSearchTimer = flyTargetSearchInterval;
        MCController player = FindObjectOfType<MCController>();
        if (player != null)
        {
            flyTarget = player.transform;
            return;
        }

        PlayerRespawn respawn = FindObjectOfType<PlayerRespawn>();
        flyTarget = respawn != null ? respawn.transform : null;
    }

    private bool IsTargetInsidePatrolRect()
    {
        return flyTarget != null && IsInsidePatrolRect(flyTarget.position);
    }

    private bool IsInsidePatrolRect(Vector2 position)
    {
        return IsXInsidePatrolRect(position.x) && IsYInsidePatrolRect(position.y);
    }

    private bool IsXInsidePatrolRect(float x)
    {
        return x >= PatrolMin.x && x <= PatrolMax.x;
    }

    private bool IsYInsidePatrolRect(float y)
    {
        return y >= PatrolMin.y && y <= PatrolMax.y;
    }

    private Vector2 GetRandomPointInPatrolRect()
    {
        return new Vector2(
            Random.Range(PatrolMin.x, PatrolMax.x),
            Random.Range(PatrolMin.y, PatrolMax.y));
    }

    private bool HasReached(Vector2 position, Vector2 targetPoint, float reachDistance)
    {
        return (position - targetPoint).sqrMagnitude <= reachDistance * reachDistance;
    }

    private void SetVelocity(Vector2 velocity)
    {
        if (body == null)
        {
            return;
        }

        body.gravityScale = 0f;
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

    private void UpdateFacingFromVelocity(Vector2 velocity)
    {
        if (velocity.sqrMagnitude <= PositionEpsilon)
        {
            return;
        }

        flyFacingDirection = Utils.GetCardinalDirectionFromVector(velocity);
    }

    private Vector2 GetPosition()
    {
        return body != null ? body.position : (Vector2)transform.position;
    }

    private Vector2 PatrolHalfSize
    {
        get { return new Vector2(Mathf.Max(0.01f, patrolSize.x) * 0.5f, Mathf.Max(0.01f, patrolSize.y) * 0.5f); }
    }

    private Vector2 PatrolMin
    {
        get { return patrolCenter - PatrolHalfSize; }
    }

    private Vector2 PatrolMax
    {
        get { return patrolCenter + PatrolHalfSize; }
    }

    private void CacheBody()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void CapturePatrolCenter()
    {
        if (capturedPatrolCenter)
        {
            return;
        }

        patrolCenter = transform.position;
        capturedPatrolCenter = true;
    }

    private void NormalizeFlyValues()
    {
        flyTargetSearchInterval = Mathf.Max(0.01f, flyTargetSearchInterval);
        flyFacingDirection = GameDirection.NormalizeOrDefault(flyFacingDirection, GameDirection.Left);
        flyViewDistance = Mathf.Max(0f, flyViewDistance);
        flyViewHalfAngle = Mathf.Clamp(flyViewHalfAngle, 1f, 180f);
        patrolSize.x = Mathf.Max(0.01f, patrolSize.x);
        patrolSize.y = Mathf.Max(0.01f, patrolSize.y);
        flyPatrolPointReachDistance = Mathf.Max(0.01f, flyPatrolPointReachDistance);
        flyMoveSpeed = Mathf.Max(0f, flyMoveSpeed);
        waitAfterCheck = Mathf.Max(0f, waitAfterCheck);
        frozenFallSpeed = Mathf.Max(0f, frozenFallSpeed);
        frozenRiseSpeed = Mathf.Max(0f, frozenRiseSpeed);
        recoverVerticalSpeed = Mathf.Max(0f, recoverVerticalSpeed);

        if (visionBlockMask == 0)
        {
            visionBlockMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Obstacle, GameLayers.Platform, GameLayers.Hazard);
        }

        if (frozenStopMask == 0)
        {
            frozenStopMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Obstacle, GameLayers.Platform);
        }

        if (frozenDestroyMask == 0)
        {
            frozenDestroyMask = LayerMask.GetMask(GameLayers.Hazard);
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        NormalizeFlyValues();

        Vector3 origin = transform.position;
        Vector3 forward = GameDirection.ToVector3(flyFacingDirection);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(origin, origin + forward * flyViewDistance);

        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Vector3 leftEdge = Quaternion.Euler(0f, 0f, flyViewHalfAngle) * forward;
        Vector3 rightEdge = Quaternion.Euler(0f, 0f, -flyViewHalfAngle) * forward;
        Gizmos.DrawLine(origin, origin + leftEdge * flyViewDistance);
        Gizmos.DrawLine(origin, origin + rightEdge * flyViewDistance);
    }

    private void OnDrawGizmos()
    {
        NormalizeFlyValues();
        Vector2 center = Application.isPlaying || capturedPatrolCenter ? patrolCenter : (Vector2)transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, patrolSize);
    }
}
