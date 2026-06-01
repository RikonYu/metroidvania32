using UnityEngine;

public enum FrogEnemyAIState
{
    Wait,
    Windup,
    Jumping
}

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Rigidbody2D))]
public class FrogEnemyAI : EnemyAI
{
    private const float PositionEpsilon = 0.0001f;
    private const int JumpArcSegments = 16;

    [Header("Target")]
    [SerializeField] private Transform frogTargetOverride;
    [SerializeField] private float frogTargetSearchInterval = 0.5f;

    [Header("Vision")]
    [SerializeField] private int frogFacingDirection = GameDirection.Left;
    [SerializeField] private float frogViewDistance = 8f;
    [SerializeField, Range(1f, 180f)] private float frogViewHalfAngle = 45f;
    [SerializeField] private LayerMask visionBlockMask;

    [Header("Patrol Range")]
    [SerializeField] private Vector2 patrolCenter;
    [SerializeField] private float patrolWidth = 12f;
    [SerializeField] private bool useInitialPositionAsPatrolCenter = true;

    [Header("Jump")]
    [SerializeField] private float maxJumpHeight = 3f;
    [SerializeField] private float jumpWindup = 0.2f;
    [SerializeField] private float landingPause = 0.35f;
    [SerializeField] private float landingReachDistance = 0.15f;
    [SerializeField] private float minimumAirTime = 0.08f;
    [SerializeField] private LayerMask landingSupportMask;
    [SerializeField] private float landingProbeUpOffset = 0.25f;
    [SerializeField] private float landingProbeDownDistance = 0.25f;
    [SerializeField, Range(0f, 1f)] private float landingMinNormalY = 0.35f;

    [Header("Debug")]
    [SerializeField] private FrogEnemyAIState frogState = FrogEnemyAIState.Wait;

    private Rigidbody2D body;
    private Transform frogTarget;
    private float frogTargetSearchTimer;
    private float stateTimer;
    private Vector2 pendingJumpTarget;
    private Vector2 activeJumpTarget;
    private float jumpElapsed;
    private bool capturedInitialPatrolCenter;
    private bool landingCollisionQueued;

    protected override void Awake()
    {
        base.Awake();
        CacheBody();
        CaptureInitialPatrolCenter();
        NormalizeFrogValues();
        BeginWait(landingPause);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheBody();
        CaptureInitialPatrolCenter();
        frogTargetSearchTimer = 0f;
        landingCollisionQueued = false;
        BeginWait(landingPause);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        NormalizeFrogValues();
    }

    protected override void Update()
    {
        CacheEnemy();
        CacheBody();
        if (!CanRunAI())
        {
            StopFrog();
            return;
        }

        UpdateTarget(Time.deltaTime);
        if (Enemy.IsFrozen)
        {
            return;
        }

        float deltaTime = GetScaledDeltaTime();
        if (deltaTime <= 0f)
        {
            return;
        }

        switch (frogState)
        {
            case FrogEnemyAIState.Wait:
                UpdateWait(deltaTime);
                break;
            case FrogEnemyAIState.Windup:
                UpdateWindup(deltaTime);
                break;
            case FrogEnemyAIState.Jumping:
                UpdateJumping(deltaTime);
                break;
        }
    }

    protected override void FixedUpdate()
    {
        CacheEnemy();
        CacheBody();
        if (!CanRunAI() || body == null)
        {
            StopFrog();
            return;
        }

        if (Enemy.IsFrozen)
        {
            return;
        }

        if (frogState != FrogEnemyAIState.Jumping)
        {
            StopFrog();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleJumpCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleJumpCollision(collision);
    }

    private void UpdateWait(float deltaTime)
    {
        stateTimer -= deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        if (TryGetAttackLanding(out Vector2 attackLanding))
        {
            BeginWindup(attackLanding);
            return;
        }

        if (TryGetPatrolLanding(out Vector2 patrolLanding))
        {
            BeginWindup(patrolLanding);
            return;
        }

        FlipFacing();
        BeginWait(landingPause);
    }

    private void UpdateWindup(float deltaTime)
    {
        stateTimer -= deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        if (!HasLandingSupport(pendingJumpTarget))
        {
            BeginWait(landingPause);
            return;
        }

        LaunchJump(pendingJumpTarget);
    }

    private void UpdateJumping(float deltaTime)
    {
        jumpElapsed += deltaTime;
        if (jumpElapsed < minimumAirTime)
        {
            return;
        }

        if ((landingCollisionQueued && IsCloseToActiveLandingX()) || HasReachedJumpTarget())
        {
            FinishJump();
        }
    }

    private bool TryGetAttackLanding(out Vector2 landingPoint)
    {
        landingPoint = Vector2.zero;
        if (!CanSeeTarget(out Vector2 targetPosition))
        {
            return false;
        }

        Vector2 position = GetPosition();
        float horizontalDistance = Mathf.Abs(targetPosition.x - position.x);
        if (horizontalDistance > HopDistance + landingReachDistance)
        {
            return false;
        }

        landingPoint = new Vector2(targetPosition.x, position.y);
        return HasLandingSupport(landingPoint);
    }

    private bool TryGetPatrolLanding(out Vector2 landingPoint)
    {
        Vector2 position = GetPosition();
        float direction = FacingSign;
        if (TryGetPatrolLandingInDirection(position, direction, false, out landingPoint))
        {
            return true;
        }

        FlipFacing();
        direction = FacingSign;
        if (TryGetPatrolLandingInDirection(position, direction, false, out landingPoint))
        {
            return true;
        }

        if (TryGetPatrolLandingInDirection(position, direction, true, out landingPoint))
        {
            return true;
        }

        FlipFacing();
        direction = FacingSign;
        return TryGetPatrolLandingInDirection(position, direction, true, out landingPoint);
    }

    private bool TryGetPatrolLandingInDirection(Vector2 position, float direction, bool clampToRange, out Vector2 landingPoint)
    {
        float targetX = position.x + direction * HopDistance;
        if (clampToRange)
        {
            targetX = Mathf.Clamp(targetX, PatrolMinX, PatrolMaxX);
        }

        landingPoint = new Vector2(targetX, position.y);
        if (!IsInsidePatrolRange(targetX) || Mathf.Abs(targetX - position.x) <= landingReachDistance)
        {
            return false;
        }

        return HasLandingSupport(landingPoint);
    }

    private void BeginWindup(Vector2 jumpTarget)
    {
        pendingJumpTarget = jumpTarget;
        stateTimer = jumpWindup;
        frogState = FrogEnemyAIState.Windup;
        UpdateFacingFromTarget(jumpTarget);
        StopFrog();
    }

    private void LaunchJump(Vector2 jumpTarget)
    {
        activeJumpTarget = jumpTarget;
        landingCollisionQueued = false;
        jumpElapsed = 0f;
        frogState = FrogEnemyAIState.Jumping;
        UpdateFacingFromTarget(jumpTarget);

        if (body == null)
        {
            return;
        }

        Vector2 position = GetPosition();
        float distance = Mathf.Abs(jumpTarget.x - position.x);
        float gravity = GetEffectiveGravity();
        float height = Mathf.Max(0.01f, maxJumpHeight * Mathf.Clamp01(distance / HopDistance));
        float verticalVelocity = Mathf.Sqrt(2f * gravity * height);
        float flightTime = Mathf.Max(0.01f, 2f * verticalVelocity / gravity);
        float horizontalVelocity = (jumpTarget.x - position.x) / flightTime;
        body.velocity = new Vector2(horizontalVelocity, verticalVelocity);
    }

    private void FinishJump()
    {
        if (body != null)
        {
            body.position = activeJumpTarget;
            body.velocity = Vector2.zero;
        }
        else
        {
            transform.position = new Vector3(activeJumpTarget.x, activeJumpTarget.y, transform.position.z);
        }

        landingCollisionQueued = false;
        BeginWait(landingPause);
    }

    private void BeginWait(float duration)
    {
        frogState = FrogEnemyAIState.Wait;
        stateTimer = Mathf.Max(0f, duration);
        StopFrog();
    }

    private bool CanSeeTarget(out Vector2 targetPosition)
    {
        targetPosition = Vector2.zero;
        if (frogTarget == null || !frogTarget.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector2 origin = GetPosition();
        targetPosition = frogTarget.position;
        Vector2 toTarget = targetPosition - origin;
        float sqrDistance = toTarget.sqrMagnitude;
        if (sqrDistance > frogViewDistance * frogViewDistance)
        {
            return false;
        }

        if (sqrDistance > PositionEpsilon)
        {
            Vector2 directionToTarget = toTarget.normalized;
            Vector2 forward = GameDirection.ToVector3(frogFacingDirection);
            float minDot = Mathf.Cos(frogViewHalfAngle * Mathf.Deg2Rad);
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

            return true;
        }

        return false;
    }

    private bool HasLandingSupport(Vector2 landingPoint)
    {
        float footY = landingPoint.y + GetBottomOffset();
        Vector2 origin = new Vector2(landingPoint.x, footY + landingProbeUpOffset);
        float distance = landingProbeUpOffset + landingProbeDownDistance;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, distance, landingSupportMask);
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.normal.y < landingMinNormalY)
            {
                continue;
            }

            if (Mathf.Abs(hit.point.y - footY) <= landingProbeDownDistance)
            {
                return true;
            }
        }

        return false;
    }

    private float GetBottomOffset()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        bool found = false;
        float minY = 0f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider2D = colliders[i];
            if (collider2D == null || collider2D.isTrigger)
            {
                continue;
            }

            float y = collider2D.bounds.min.y;
            if (!found || y < minY)
            {
                minY = y;
                found = true;
            }
        }

        return found ? minY - transform.position.y : -0.5f;
    }

    private bool HasReachedJumpTarget()
    {
        if (body == null || body.velocity.y > 0f)
        {
            return false;
        }

        Vector2 position = GetPosition();
        return Mathf.Abs(position.x - activeJumpTarget.x) <= landingReachDistance
            && position.y <= activeJumpTarget.y + landingReachDistance
            && HasLandingSupport(activeJumpTarget);
    }

    private bool IsCloseToActiveLandingX()
    {
        return Mathf.Abs(GetPosition().x - activeJumpTarget.x) <= landingReachDistance;
    }

    private void HandleJumpCollision(Collision2D collision)
    {
        if (frogState != FrogEnemyAIState.Jumping || collision == null || !Utils.IsLayerInMask(collision.gameObject.layer, landingSupportMask))
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.normal.y >= landingMinNormalY)
            {
                landingCollisionQueued = true;
                return;
            }
        }
    }

    private void UpdateTarget(float deltaTime)
    {
        if (frogTargetOverride != null)
        {
            frogTarget = frogTargetOverride;
            return;
        }

        if (frogTarget != null && frogTarget.gameObject.activeInHierarchy)
        {
            return;
        }

        frogTargetSearchTimer -= deltaTime;
        if (frogTargetSearchTimer > 0f)
        {
            return;
        }

        frogTargetSearchTimer = frogTargetSearchInterval;
        MCController player = FindObjectOfType<MCController>();
        if (player != null)
        {
            frogTarget = player.transform;
            return;
        }

        PlayerRespawn respawn = FindObjectOfType<PlayerRespawn>();
        frogTarget = respawn != null ? respawn.transform : null;
    }

    private void StopFrog()
    {
        if (body != null)
        {
            body.velocity = Vector2.zero;
            return;
        }

        StopEnemy();
    }

    private void UpdateFacingFromTarget(Vector2 targetPosition)
    {
        float deltaX = targetPosition.x - GetPosition().x;
        if (Mathf.Abs(deltaX) <= PositionEpsilon)
        {
            return;
        }

        frogFacingDirection = deltaX < 0f ? GameDirection.Left : GameDirection.Right;
    }

    private void FlipFacing()
    {
        frogFacingDirection = frogFacingDirection == GameDirection.Left ? GameDirection.Right : GameDirection.Left;
    }

    private bool IsInsidePatrolRange(float x)
    {
        return x >= PatrolMinX && x <= PatrolMaxX;
    }

    private float GetEffectiveGravity()
    {
        if (body == null)
        {
            return Mathf.Abs(Physics2D.gravity.y);
        }

        float gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
        return Mathf.Max(0.01f, gravity);
    }

    private Vector2 GetPosition()
    {
        return body != null ? body.position : (Vector2)transform.position;
    }

    private float HopDistance
    {
        get { return Mathf.Max(0.01f, frogViewDistance); }
    }

    private float FacingSign
    {
        get { return frogFacingDirection == GameDirection.Right ? 1f : -1f; }
    }

    private float PatrolMinX
    {
        get { return patrolCenter.x - patrolWidth * 0.5f; }
    }

    private float PatrolMaxX
    {
        get { return patrolCenter.x + patrolWidth * 0.5f; }
    }

    private void CacheBody()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
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

    private void NormalizeFrogValues()
    {
        frogTargetSearchInterval = Mathf.Max(0.01f, frogTargetSearchInterval);
        frogFacingDirection = frogFacingDirection == GameDirection.Right ? GameDirection.Right : GameDirection.Left;
        frogViewDistance = Mathf.Max(0.01f, frogViewDistance);
        frogViewHalfAngle = Mathf.Clamp(frogViewHalfAngle, 1f, 180f);
        patrolWidth = Mathf.Max(0.01f, patrolWidth);
        maxJumpHeight = Mathf.Max(0.01f, maxJumpHeight);
        jumpWindup = Mathf.Max(0f, jumpWindup);
        landingPause = Mathf.Max(0f, landingPause);
        landingReachDistance = Mathf.Max(0.01f, landingReachDistance);
        minimumAirTime = Mathf.Max(0f, minimumAirTime);
        landingProbeUpOffset = Mathf.Max(0.01f, landingProbeUpOffset);
        landingProbeDownDistance = Mathf.Max(0.01f, landingProbeDownDistance);

        if (visionBlockMask == 0)
        {
            visionBlockMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Obstacle, GameLayers.Platform, GameLayers.Hazard);
        }

        if (landingSupportMask == 0)
        {
            landingSupportMask = LayerMask.GetMask(GameLayers.Ground, GameLayers.Obstacle);
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        NormalizeFrogValues();

        Vector3 origin = transform.position;
        Vector3 forward = GameDirection.ToVector3(frogFacingDirection);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(origin, origin + forward * frogViewDistance);

        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Vector3 leftEdge = Quaternion.Euler(0f, 0f, frogViewHalfAngle) * forward;
        Vector3 rightEdge = Quaternion.Euler(0f, 0f, -frogViewHalfAngle) * forward;
        Gizmos.DrawLine(origin, origin + leftEdge * frogViewDistance);
        Gizmos.DrawLine(origin, origin + rightEdge * frogViewDistance);

        Gizmos.color = Color.yellow;
        DrawJumpArc((Vector2)origin, new Vector2(origin.x + FacingSign * HopDistance, origin.y));
    }

    private void OnDrawGizmos()
    {
        NormalizeFrogValues();
        Vector2 center = Application.isPlaying || capturedInitialPatrolCenter ? patrolCenter : (Vector2)transform.position;
        Vector3 left = new Vector3(center.x - patrolWidth * 0.5f, center.y, transform.position.z);
        Vector3 right = new Vector3(center.x + patrolWidth * 0.5f, center.y, transform.position.z);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left, 0.15f);
        Gizmos.DrawWireSphere(right, 0.15f);
    }

    private void DrawJumpArc(Vector2 start, Vector2 end)
    {
        float distance = Mathf.Abs(end.x - start.x);
        float height = maxJumpHeight * Mathf.Clamp01(distance / HopDistance);
        Vector3 previous = start;
        for (int i = 1; i <= JumpArcSegments; i++)
        {
            float t = i / (float)JumpArcSegments;
            float x = Mathf.Lerp(start.x, end.x, t);
            float y = Mathf.Lerp(start.y, end.y, t) + 4f * height * t * (1f - t);
            Vector3 next = new Vector3(x, y, transform.position.z);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
