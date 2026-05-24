using System.Collections.Generic;
using UnityEngine;

public enum EnemyAIState
{
    Patrol,
    WindupToAttack,
    AttackCooldown,
    WindupToPatrol
}

[RequireComponent(typeof(EnemyController))]
public class EnemyAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform targetOverride;
    [SerializeField] private float targetSearchInterval = 0.5f;

    [Header("Vision")]
    [SerializeField] private int facingDirection = GameDirection.Left;
    [SerializeField] private float viewDistance = 8f;
    [SerializeField, Range(1f, 180f)] private float viewHalfAngle = 45f;

    [Header("Patrol")]
    [SerializeField] private float patrolPointReachDistance = 0.15f;
    [SerializeField] private bool loopPatrol;

    [Header("State Timing")]
    [SerializeField] private float stateChangeWindup = 0.2f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private EnemyAIState currentState = EnemyAIState.Patrol;

    private EnemyController enemy;
    private Transform target;
    private int patrolPointIndex;
    private int patrolStep = 1;
    private float targetSearchTimer;
    private float stateTimer;
    private Vector2 spawnPatrolPoint;
    private bool hasSpawnPatrolPoint;

    protected EnemyController Enemy
    {
        get
        {
            CacheEnemy();
            return enemy;
        }
    }

    protected virtual void Awake()
    {
        CacheEnemy();
        CaptureSpawnPatrolPointIfNeeded();
        NormalizeValues();
    }

    protected virtual void OnEnable()
    {
        CaptureSpawnPatrolPointIfNeeded();
        currentState = EnemyAIState.Patrol;
        stateTimer = 0f;
        targetSearchTimer = 0f;
        patrolPointIndex = ClampPatrolPointIndex(patrolPointIndex);
    }

    protected virtual void OnValidate()
    {
        NormalizeValues();
    }

    protected virtual void Update()
    {
        CacheEnemy();
        if (!CanRunAI())
        {
            StopEnemy();
            return;
        }

        UpdateTarget(Time.deltaTime);

        float deltaTime = GetScaledDeltaTime();
        if (deltaTime <= 0f)
        {
            return;
        }

        switch (currentState)
        {
            case EnemyAIState.Patrol:
                UpdatePatrolState();
                break;
            case EnemyAIState.WindupToAttack:
                UpdateAttackWindup(deltaTime);
                break;
            case EnemyAIState.AttackCooldown:
                UpdateAttackCooldownState(deltaTime);
                break;
            case EnemyAIState.WindupToPatrol:
                UpdatePatrolWindup(deltaTime);
                break;
        }
    }

    protected virtual void FixedUpdate()
    {
        CacheEnemy();
        if (!CanRunAI())
        {
            StopEnemy();
            return;
        }

        if (currentState == EnemyAIState.Patrol)
        {
            Patrol();
            return;
        }

        StopEnemy();
    }

    private void UpdatePatrolState()
    {
        if (CanSeeTarget(out _))
        {
            ChangeState(EnemyAIState.WindupToAttack);
        }
    }

    private void UpdateAttackWindup(float deltaTime)
    {
        if (!CanSeeTarget(out Vector2 attackDirection))
        {
            ChangeState(EnemyAIState.WindupToPatrol);
            return;
        }

        stateTimer -= deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        Attack(attackDirection);
        ChangeState(EnemyAIState.AttackCooldown);
    }

    private void UpdateAttackCooldownState(float deltaTime)
    {
        stateTimer -= deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        ChangeState(CanSeeTarget(out _) ? EnemyAIState.WindupToAttack : EnemyAIState.WindupToPatrol);
    }

    private void UpdatePatrolWindup(float deltaTime)
    {
        if (CanSeeTarget(out _))
        {
            ChangeState(EnemyAIState.WindupToAttack);
            return;
        }

        stateTimer -= deltaTime;
        if (stateTimer <= 0f)
        {
            ChangeState(EnemyAIState.Patrol);
        }
    }

    private void Patrol()
    {
        int patrolPointCount = GetPatrolPointCount();
        if (patrolPointCount <= 1)
        {
            StopEnemy();
            return;
        }

        patrolPointIndex = ClampPatrolPointIndex(patrolPointIndex);
        Vector2 currentPosition = transform.position;
        Vector2 targetPoint = GetPatrolPoint(patrolPointIndex);
        Vector2 toPoint = targetPoint - currentPosition;

        if (HasReachedPatrolPoint(toPoint))
        {
            AdvancePatrolPoint(patrolPointCount);
            patrolPointIndex = ClampPatrolPointIndex(patrolPointIndex);
            targetPoint = GetPatrolPoint(patrolPointIndex);
            toPoint = targetPoint - currentPosition;
        }

        Vector2 moveInput = GetPatrolMoveInput(toPoint);
        UpdateFacingFromMove(moveInput);
        enemy.Move(moveInput);
    }

    protected virtual void Attack(Vector2 attackDirection)
    {
    }

    private bool HasReachedPatrolPoint(Vector2 toPoint)
    {
        float reachDistance = patrolPointReachDistance;
        if (enemy.MovementKind == EnemyMovementKind.Crawling)
        {
            return Mathf.Abs(toPoint.x) <= reachDistance;
        }

        return toPoint.sqrMagnitude <= reachDistance * reachDistance;
    }

    private Vector2 GetPatrolMoveInput(Vector2 toPoint)
    {
        if (enemy.MovementKind == EnemyMovementKind.Crawling)
        {
            if (Mathf.Abs(toPoint.x) <= 0.001f)
            {
                return Vector2.zero;
            }

            return new Vector2(Mathf.Sign(toPoint.x), 0f);
        }

        if (toPoint.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        return toPoint.normalized;
    }

    private void AdvancePatrolPoint(int patrolPointCount)
    {
        if (patrolPointCount <= 1)
        {
            return;
        }

        if (loopPatrol)
        {
            patrolPointIndex = (patrolPointIndex + 1) % patrolPointCount;
            return;
        }

        if (patrolPointIndex >= patrolPointCount - 1)
        {
            patrolStep = -1;
        }
        else if (patrolPointIndex <= 0)
        {
            patrolStep = 1;
        }

        patrolPointIndex = Mathf.Clamp(patrolPointIndex + patrolStep, 0, patrolPointCount - 1);
    }

    private bool CanSeeTarget(out Vector2 directionToTarget)
    {
        directionToTarget = Vector2.zero;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector2 toTarget = target.position - transform.position;
        float sqrDistance = toTarget.sqrMagnitude;
        if (sqrDistance > viewDistance * viewDistance)
        {
            return false;
        }

        if (sqrDistance <= 0.0001f)
        {
            directionToTarget = (Vector2)GameDirection.ToVector3(facingDirection);
            return true;
        }

        directionToTarget = toTarget.normalized;
        Vector2 forward = GameDirection.ToVector3(facingDirection);
        float minDot = Mathf.Cos(viewHalfAngle * Mathf.Deg2Rad);
        return Vector2.Dot(forward.normalized, directionToTarget) >= minDot;
    }

    private void ChangeState(EnemyAIState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        currentState = nextState;
        stateTimer = nextState == EnemyAIState.AttackCooldown ? attackCooldown : stateChangeWindup;
        StopEnemy();
    }

    private void UpdateTarget(float deltaTime)
    {
        if (targetOverride != null)
        {
            target = targetOverride;
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

        targetSearchTimer = targetSearchInterval;
        MCController player = FindObjectOfType<MCController>();
        if (player != null)
        {
            target = player.transform;
            return;
        }

        PlayerRespawn respawn = FindObjectOfType<PlayerRespawn>();
        target = respawn != null ? respawn.transform : null;
    }

    private void UpdateFacingFromMove(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        facingDirection = Utils.GetCardinalDirectionFromVector(moveInput);
    }

    protected bool CanRunAI()
    {
        return enemy != null && enemy.IsAlive && !enemy.IsBoss;
    }

    protected float GetScaledDeltaTime()
    {
        if (enemy != null && enemy.IsFrozen)
        {
            return 0f;
        }

        return Time.deltaTime * GameTime.WorldScale;
    }

    private int ClampPatrolPointIndex(int index)
    {
        int patrolPointCount = GetPatrolPointCount();
        if (patrolPointCount <= 0)
        {
            return 0;
        }

        return Mathf.Clamp(index, 0, patrolPointCount - 1);
    }

    private int GetPatrolPointCount()
    {
        IReadOnlyList<Vector2> patrolPoints = enemy != null ? enemy.PatrolPoints : null;
        int configuredPointCount = patrolPoints != null ? patrolPoints.Count : 0;
        return configuredPointCount + 1;
    }

    private Vector2 GetPatrolPoint(int index)
    {
        if (index <= 0 || enemy == null)
        {
            return spawnPatrolPoint;
        }

        IReadOnlyList<Vector2> patrolPoints = enemy.PatrolPoints;
        if (patrolPoints == null || patrolPoints.Count == 0)
        {
            return spawnPatrolPoint;
        }

        return patrolPoints[Mathf.Clamp(index - 1, 0, patrolPoints.Count - 1)];
    }

    private void CaptureSpawnPatrolPointIfNeeded()
    {
        if (hasSpawnPatrolPoint)
        {
            return;
        }

        spawnPatrolPoint = transform.position;
        hasSpawnPatrolPoint = true;
    }

    protected void StopEnemy()
    {
        if (enemy != null)
        {
            enemy.StopMoving();
        }
    }

    protected void CacheEnemy()
    {
        if (enemy == null)
        {
            enemy = GetComponent<EnemyController>();
        }
    }

    private void NormalizeValues()
    {
        targetSearchInterval = Mathf.Max(0.01f, targetSearchInterval);
        facingDirection = GameDirection.NormalizeOrDefault(facingDirection, GameDirection.Left);
        viewDistance = Mathf.Max(0f, viewDistance);
        viewHalfAngle = Mathf.Clamp(viewHalfAngle, 1f, 180f);
        patrolPointReachDistance = Mathf.Max(0.01f, patrolPointReachDistance);
        stateChangeWindup = Mathf.Max(0f, stateChangeWindup);
        attackCooldown = Mathf.Max(0f, attackCooldown);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        NormalizeValues();

        Gizmos.color = Color.white;
        Vector3 origin = transform.position;
        Vector3 forward = GameDirection.ToVector3(facingDirection);
        Gizmos.DrawLine(origin, origin + forward * viewDistance);

        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Vector3 leftEdge = Quaternion.Euler(0f, 0f, viewHalfAngle) * forward;
        Vector3 rightEdge = Quaternion.Euler(0f, 0f, -viewHalfAngle) * forward;
        Gizmos.DrawLine(origin, origin + leftEdge * viewDistance);
        Gizmos.DrawLine(origin, origin + rightEdge * viewDistance);
    }
}
