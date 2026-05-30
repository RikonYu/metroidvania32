using System.Collections.Generic;
using UnityEngine;

public enum MovingPlatformPathMode
{
    Loop,
    PingPong,
    Once
}

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    private const int MaxPathCastHits = 16;
    private const float PathCollisionOpposingDot = -0.25f;
    private const float PathCollisionSkin = 0.01f;

    [Header("Path")]
    [SerializeField] protected bool includeInitialPosition = true;
    [SerializeField] protected List<Vector2> pathPoints = new List<Vector2>();
    [SerializeField] protected MovingPlatformPathMode pathMode = MovingPlatformPathMode.PingPong;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float waitAtPoints = 0f;

    [Header("Start")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool startMovingOnAwake;

    [Header("Setup")]
    [SerializeField] private bool applyPlatformLayer = true;

    private Rigidbody2D body;
    private Collider2D[] pathCollisionColliders;
    private ContactFilter2D pathTerrainContactFilter;
    private readonly RaycastHit2D[] pathCastHits = new RaycastHit2D[MaxPathCastHits];
    private Vector2 initialPosition;
    private Vector2 currentVelocity;
    private Vector2 currentDelta;
    private int targetPointIndex = 1;
    private int pathDirection = 1;
    private float waitCounter;
    private bool moving;
    private bool pathBlockedByTerrain;
    private int pathBlockedTargetPointIndex = -1;

    public bool IsMoving
    {
        get { return moving; }
    }

    public Vector2 CurrentVelocity
    {
        get { return currentVelocity; }
    }

    public Vector2 CurrentDelta
    {
        get { return currentDelta; }
    }

    public bool IncludeInitialPosition
    {
        get { return includeInitialPosition; }
    }

    public MovingPlatformPathMode PathMode
    {
        get { return pathMode; }
    }

    public IReadOnlyList<Vector2> PathPoints
    {
        get { return pathPoints; }
    }

    protected virtual void Reset()
    {
        CacheComponents();
        ConfigurePlatform();
    }

    protected virtual void Awake()
    {
        CacheComponents();
        ConfigurePlatform();
        initialPosition = body != null ? body.position : (Vector2)transform.position;
        targetPointIndex = GetInitialTargetIndex();
        moving = ShouldAutoStart();
    }

    protected virtual void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        waitAtPoints = Mathf.Max(0f, waitAtPoints);
        CacheComponents();
        ConfigurePlatform();
    }

    protected virtual void FixedUpdate()
    {
        if (!moving && CanStartMoving())
        {
            moving = true;
        }

        StepMovement();
    }

    public void StartMoving()
    {
        ClearPathTerrainBlock();
        moving = true;
    }

    public void StopMoving()
    {
        moving = false;
        currentVelocity = Vector2.zero;
        currentDelta = Vector2.zero;
        ClearPathTerrainBlock();
    }

    public void AddPathPoint(Vector2 worldPoint)
    {
        pathPoints.Add(worldPoint);
    }

    public void SetPathPoint(int index, Vector2 worldPoint)
    {
        if (index < 0 || index >= pathPoints.Count)
        {
            return;
        }

        pathPoints[index] = worldPoint;
    }

    public void RemovePathPointAt(int index)
    {
        if (index < 0 || index >= pathPoints.Count)
        {
            return;
        }

        pathPoints.RemoveAt(index);
    }

    public void ClearPathPoints()
    {
        pathPoints.Clear();
    }

    protected virtual bool CanStartMoving()
    {
        return false;
    }

    protected virtual bool ShouldAutoStart()
    {
        return autoStart || startMovingOnAwake;
    }

    protected virtual bool CanKeepMoving()
    {
        return true;
    }

    protected virtual float GetSegmentMoveSpeed(Vector2 currentPosition, Vector2 targetPosition)
    {
        return moveSpeed;
    }

    protected bool IsMovingTowardInitialPosition
    {
        get { return includeInitialPosition && targetPointIndex == 0; }
    }

    private void StepMovement()
    {
        currentVelocity = Vector2.zero;
        currentDelta = Vector2.zero;
        if (!moving || !CanKeepMoving() || body == null || GetPathPointCount() < 2)
        {
            return;
        }

        if (waitCounter > 0f)
        {
            waitCounter = Mathf.Max(0f, waitCounter - GameTime.FixedDeltaTime);
            return;
        }

        if (pathBlockedByTerrain)
        {
            if (pathBlockedTargetPointIndex == targetPointIndex)
            {
                ClearPathTerrainBlock();
                AdvanceTargetPoint();
                return;
            }

            ClearPathTerrainBlock();
        }

        Vector2 currentPosition = body.position;
        Vector2 targetPosition = GetPathPoint(targetPointIndex);
        float segmentMoveSpeed = Mathf.Max(0f, GetSegmentMoveSpeed(currentPosition, targetPosition));
        if (segmentMoveSpeed <= 0f)
        {
            return;
        }

        Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, segmentMoveSpeed * GameTime.FixedDeltaTime);
        Vector2 requestedDelta = nextPosition - currentPosition;
        if (TryGetPathTerrainBlockedDelta(requestedDelta, out Vector2 blockedDelta))
        {
            currentDelta = blockedDelta;
            currentVelocity = currentDelta / Time.fixedDeltaTime;
            body.MovePosition(currentPosition + blockedDelta);
            AdvanceTargetPoint();
            return;
        }

        currentDelta = requestedDelta;
        currentVelocity = currentDelta / Time.fixedDeltaTime;
        body.MovePosition(nextPosition);

        if ((targetPosition - nextPosition).sqrMagnitude <= 0.0001f)
        {
            AdvanceTargetPoint();
        }
    }

    private void AdvanceTargetPoint()
    {
        int pointCount = GetPathPointCount();
        if (pointCount < 2)
        {
            StopMoving();
            return;
        }

        waitCounter = waitAtPoints;
        if (pathMode == MovingPlatformPathMode.Loop)
        {
            targetPointIndex = (targetPointIndex + 1) % pointCount;
            return;
        }

        if (pathMode == MovingPlatformPathMode.Once)
        {
            if (targetPointIndex >= pointCount - 1)
            {
                StopMoving();
                return;
            }

            targetPointIndex++;
            return;
        }

        if (targetPointIndex >= pointCount - 1)
        {
            pathDirection = -1;
        }
        else if (targetPointIndex <= 0)
        {
            pathDirection = 1;
        }

        targetPointIndex = Mathf.Clamp(targetPointIndex + pathDirection, 0, pointCount - 1);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TrackPathTerrainCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TrackPathTerrainCollision(collision);
    }

    private int GetInitialTargetIndex()
    {
        int pointCount = GetPathPointCount();
        if (pointCount <= 1)
        {
            return 0;
        }

        return includeInitialPosition ? 1 : Mathf.Clamp(GetClosestPathPointIndex(transform.position), 0, pointCount - 1);
    }

    private int GetClosestPathPointIndex(Vector2 position)
    {
        int pointCount = GetPathPointCount();
        int closestIndex = 0;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < pointCount; i++)
        {
            float distance = (GetPathPoint(i) - position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private int GetPathPointCount()
    {
        return pathPoints.Count + (includeInitialPosition ? 1 : 0);
    }

    private Vector2 GetPathPoint(int index)
    {
        if (includeInitialPosition)
        {
            if (index <= 0)
            {
                return Application.isPlaying ? initialPosition : (Vector2)transform.position;
            }

            return pathPoints[Mathf.Clamp(index - 1, 0, pathPoints.Count - 1)];
        }

        return pathPoints[Mathf.Clamp(index, 0, pathPoints.Count - 1)];
    }

    private bool TryGetPathTerrainBlockedDelta(Vector2 requestedDelta, out Vector2 blockedDelta)
    {
        blockedDelta = requestedDelta;
        if (requestedDelta.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        CachePathCollisionColliders();
        UpdatePathTerrainContactFilter();

        Vector2 direction = requestedDelta.normalized;
        float distance = requestedDelta.magnitude;
        bool blocked = false;
        float allowedDistance = distance;
        for (int colliderIndex = 0; colliderIndex < pathCollisionColliders.Length; colliderIndex++)
        {
            Collider2D collider2D = pathCollisionColliders[colliderIndex];
            if (collider2D == null || !collider2D.enabled)
            {
                continue;
            }

            int hitCount = collider2D.Cast(direction, pathTerrainContactFilter, pathCastHits, distance + PathCollisionSkin);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit2D hit = pathCastHits[hitIndex];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || !IsBlockingPathNormal(direction, hit.normal))
                {
                    continue;
                }

                blocked = true;
                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - PathCollisionSkin));
            }
        }

        blockedDelta = direction * allowedDistance;
        return blocked;
    }

    private void TrackPathTerrainCollision(Collision2D collision)
    {
        if (!moving || waitCounter > 0f || collision == null || body == null || pathBlockedByTerrain || GetPathPointCount() < 2)
        {
            return;
        }

        Collider2D terrainCollider = GetExternalTerrainCollider(collision);
        if (terrainCollider == null)
        {
            return;
        }

        Vector2 toTarget = GetPathPoint(targetPointIndex) - body.position;
        Vector2 pathDirectionToTarget = Utils.NormalizeOrZero(toTarget);
        if (pathDirectionToTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (IsBlockingPathNormal(pathDirectionToTarget, contact.normal))
            {
                pathBlockedByTerrain = true;
                pathBlockedTargetPointIndex = targetPointIndex;
                return;
            }
        }
    }

    private Collider2D GetExternalTerrainCollider(Collision2D collision)
    {
        if (IsExternalTerrainCollider(collision.collider))
        {
            return collision.collider;
        }

        return IsExternalTerrainCollider(collision.otherCollider) ? collision.otherCollider : null;
    }

    private bool IsExternalTerrainCollider(Collider2D collider2D)
    {
        return Utils.IsTerrain(collider2D) && !collider2D.transform.IsChildOf(transform);
    }

    private bool IsBlockingPathNormal(Vector2 pathDirectionToTarget, Vector2 normal)
    {
        return Vector2.Dot(pathDirectionToTarget, normal) <= PathCollisionOpposingDot;
    }

    private void ClearPathTerrainBlock()
    {
        pathBlockedByTerrain = false;
        pathBlockedTargetPointIndex = -1;
    }

    private void CacheComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        CachePathCollisionColliders();
        UpdatePathTerrainContactFilter();
    }

    private void CachePathCollisionColliders()
    {
        if (pathCollisionColliders == null || pathCollisionColliders.Length == 0)
        {
            pathCollisionColliders = GetComponentsInChildren<Collider2D>();
        }
    }

    private void UpdatePathTerrainContactFilter()
    {
        pathTerrainContactFilter.useTriggers = false;
        pathTerrainContactFilter.SetLayerMask(LayerMask.GetMask(GameLayers.Ground, GameLayers.Obstacle, GameLayers.Platform));
    }

    private void ConfigurePlatform()
    {
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.freezeRotation = true;
        }

        if (applyPlatformLayer)
        {
            GameLayers.ApplyToAfterValidation(gameObject, GameLayers.Platform);
        }
    }

    private void OnDrawGizmosSelected()
    {
        int pointCount = GetPathPointCount();
        if (pointCount <= 0)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Vector2 previous = GetPathPoint(0);
        Gizmos.DrawWireSphere(previous, 0.2f);
        for (int i = 1; i < pointCount; i++)
        {
            Vector2 point = GetPathPoint(i);
            Gizmos.DrawWireSphere(point, 0.2f);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        if (pathMode == MovingPlatformPathMode.Loop && pointCount > 2)
        {
            Gizmos.DrawLine(GetPathPoint(pointCount - 1), GetPathPoint(0));
        }
    }
}
