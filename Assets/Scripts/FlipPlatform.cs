using UnityEngine;

public enum FlipPlatformRotationStep
{
    Rotate90 = 90,
    Rotate180 = 180
}

public enum FlipPlatformRotationState
{
    Idle,
    Clockwise,
    CounterClockwise
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class FlipPlatform : MonoBehaviour
{
    [SerializeField] private FlipPlatformRotationStep rotationStep = FlipPlatformRotationStep.Rotate90;
    [SerializeField] private float rotationDuration = 0.2f;
    [SerializeField] private float returnDelay = 3f;
    [SerializeField] private FlipPlatformRotationState rotationState = FlipPlatformRotationState.Idle;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;
    private int pendingReturnRotations;
    private float returnTimer;
    private float rotationStartAngle;
    private float rotationTargetAngle;
    private float rotationElapsed;
    private float rotationTotalTime;
    private float counterClockwiseSourceAngle;

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        MatchCollidersToSpriteSizes();
    }

    private void FixedUpdate()
    {
        if (UpdateRotation())
        {
            return;
        }

        UpdateReturnTimer();
    }

    private void OnValidate()
    {
        rotationDuration = Mathf.Max(0f, rotationDuration);
        returnDelay = Mathf.Max(0f, returnDelay);
        CacheComponents();
    }

    public bool RotateFromArrow(Bullet bullet)
    {
        if (!IsValidArrow(bullet))
        {
            return false;
        }

        if (rotationState == FlipPlatformRotationState.Clockwise)
        {
            return true;
        }

        if (rotationState == FlipPlatformRotationState.CounterClockwise)
        {
            BeginRotation(counterClockwiseSourceAngle, FlipPlatformRotationState.Clockwise);
            return true;
        }

        BeginClockwiseRotation();
        pendingReturnRotations++;
        returnTimer = returnDelay;
        return true;
    }

    private void BeginClockwiseRotation()
    {
        BeginRotation(NormalizeAngle(CurrentRotation - RotationDegrees), FlipPlatformRotationState.Clockwise);
    }

    private void BeginCounterClockwiseRotation()
    {
        counterClockwiseSourceAngle = CurrentRotation;
        BeginRotation(NormalizeAngle(counterClockwiseSourceAngle + RotationDegrees), FlipPlatformRotationState.CounterClockwise);
    }

    private void BeginRotation(float targetAngle, FlipPlatformRotationState nextState)
    {
        rotationStartAngle = CurrentRotation;
        rotationTargetAngle = NormalizeAngle(targetAngle);
        rotationElapsed = 0f;
        rotationTotalTime = GetRotationTime(rotationStartAngle, rotationTargetAngle);
        rotationState = nextState;

        if (rotationTotalTime <= 0f)
        {
            SetRotationAngle(rotationTargetAngle);
            FinishRotation();
        }
    }

    private bool UpdateRotation()
    {
        if (rotationState == FlipPlatformRotationState.Idle)
        {
            return false;
        }

        rotationElapsed += GameTime.FixedDeltaTime;
        float t = rotationTotalTime <= 0f ? 1f : Mathf.Clamp01(rotationElapsed / rotationTotalTime);
        SetRotationAngle(Mathf.LerpAngle(rotationStartAngle, rotationTargetAngle, t));
        if (t >= 1f)
        {
            FinishRotation();
        }

        return true;
    }

    private void FinishRotation()
    {
        FlipPlatformRotationState finishedState = rotationState;
        SetRotationAngle(rotationTargetAngle);
        rotationState = FlipPlatformRotationState.Idle;
        rotationElapsed = 0f;
        rotationTotalTime = 0f;

        if (finishedState == FlipPlatformRotationState.CounterClockwise)
        {
            pendingReturnRotations = Mathf.Max(0, pendingReturnRotations - 1);
        }

        returnTimer = pendingReturnRotations > 0 ? returnDelay : 0f;
    }

    private void SetRotationAngle(float angle)
    {
        float nextRotation = NormalizeAngle(angle);
        if (body != null)
        {
            body.SetRotation(nextRotation);
            return;
        }

        Vector3 eulerAngles = transform.eulerAngles;
        eulerAngles.z = nextRotation;
        transform.eulerAngles = eulerAngles;
    }

    private float GetRotationTime(float startAngle, float targetAngle)
    {
        if (rotationDuration <= 0f)
        {
            return 0f;
        }

        float angleDistance = Mathf.Abs(Mathf.DeltaAngle(startAngle, targetAngle));
        if (angleDistance <= 0.001f)
        {
            return 0f;
        }

        return rotationDuration * angleDistance / RotationDegrees;
    }

    private void UpdateReturnTimer()
    {
        if (pendingReturnRotations <= 0)
        {
            returnTimer = 0f;
            return;
        }

        if (returnDelay <= 0f)
        {
            BeginCounterClockwiseRotation();
            return;
        }

        returnTimer -= GameTime.FixedDeltaTime;
        if (returnTimer > 0f)
        {
            return;
        }

        BeginCounterClockwiseRotation();
    }

    private static bool IsValidArrow(Bullet bullet)
    {
        return bullet != null && bullet.Source == BulletSource.Player;
    }

    private float CurrentRotation
    {
        get { return body != null ? body.rotation : transform.eulerAngles.z; }
    }

    private float RotationDegrees
    {
        get { return (float)rotationStep; }
    }

    private void MatchCollidersToSpriteSizes()
    {
        CacheComponents();
        MatchColliderToOwnSpriteSize(transform);
    }

    private void MatchColliderToOwnSpriteSize(Transform target)
    {
        if (target == null)
        {
            return;
        }

        SpriteRenderer targetSpriteRenderer = target.GetComponent<SpriteRenderer>();
        BoxCollider2D targetBoxCollider = target.GetComponent<BoxCollider2D>();
        if (targetSpriteRenderer != null && targetBoxCollider != null)
        {
            targetBoxCollider.size = targetSpriteRenderer.size;
        }

        for (int i = 0; i < target.childCount; i++)
        {
            MatchColliderToOwnSpriteSize(target.GetChild(i));
        }
    }

    private void CacheComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f)
        {
            angle += 360f;
        }

        return angle;
    }
}
