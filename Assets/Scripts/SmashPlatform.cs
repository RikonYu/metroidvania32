using UnityEngine;

public class SmashPlatform : MovingPlatform
{
    [Header("Smash")]
    [SerializeField] private float outboundSpeed = 12f;
    [SerializeField] private float returnSpeed = 3f;
    [SerializeField] private Vector2 defaultPathOffset = Vector2.down * 4f;

    protected override void Reset()
    {
        base.Reset();
        EnsureSinglePathPoint();
    }

    protected override void Awake()
    {
        EnsureSinglePathPoint();
        base.Awake();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        outboundSpeed = Mathf.Max(0f, outboundSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
        EnsureSinglePathPoint();
    }

    protected override float GetSegmentMoveSpeed(Vector2 currentPosition, Vector2 targetPosition)
    {
        return IsMovingTowardInitialPosition ? returnSpeed : outboundSpeed;
    }

    private void EnsureSinglePathPoint()
    {
        includeInitialPosition = true;
        pathMode = MovingPlatformPathMode.PingPong;

        if (pathPoints == null)
        {
            pathPoints = new System.Collections.Generic.List<Vector2>();
        }

        if (pathPoints.Count == 0)
        {
            pathPoints.Add((Vector2)transform.position + defaultPathOffset);
        }

        while (pathPoints.Count > 1)
        {
            pathPoints.RemoveAt(pathPoints.Count - 1);
        }
    }
}
