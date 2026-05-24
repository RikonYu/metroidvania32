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
[RequireComponent(typeof(Collider2D))]
public class MovingPlatform : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private bool includeInitialPosition = true;
    [SerializeField] private List<Vector2> pathPoints = new List<Vector2>();
    [SerializeField] private MovingPlatformPathMode pathMode = MovingPlatformPathMode.PingPong;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float waitAtPoints = 0f;

    [Header("Start")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool startMovingOnAwake;

    [Header("Setup")]
    [SerializeField] private bool applyPlatformLayer = true;

    private Rigidbody2D body;
    private Vector2 initialPosition;
    private Vector2 currentVelocity;
    private Vector2 currentDelta;
    private int targetPointIndex = 1;
    private int pathDirection = 1;
    private float waitCounter;
    private bool moving;

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

    private void FixedUpdate()
    {
        if (!moving && CanStartMoving())
        {
            moving = true;
        }

        StepMovement();
    }

    public void StartMoving()
    {
        moving = true;
    }

    public void StopMoving()
    {
        moving = false;
        currentVelocity = Vector2.zero;
        currentDelta = Vector2.zero;
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

    private void StepMovement()
    {
        currentVelocity = Vector2.zero;
        currentDelta = Vector2.zero;
        if (!moving || !CanKeepMoving() || body == null || GetPathPointCount() < 2 || moveSpeed <= 0f)
        {
            return;
        }

        if (waitCounter > 0f)
        {
            waitCounter = Mathf.Max(0f, waitCounter - GameTime.FixedDeltaTime);
            return;
        }

        Vector2 currentPosition = body.position;
        Vector2 targetPosition = GetPathPoint(targetPointIndex);
        Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, moveSpeed * GameTime.FixedDeltaTime);
        currentDelta = nextPosition - currentPosition;
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

    private void CacheComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

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
