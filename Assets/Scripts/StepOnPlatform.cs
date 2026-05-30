using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StepOnPlatform : MovingPlatform
{
    [Header("Step On")]
    [SerializeField] private float stepDelay = 0.5f;
    [SerializeField] private float topContactSkin = 0.08f;

    private Collider2D platformCollider;
    private bool mcOnTop;
    private float stepTimer;

    protected override void Reset()
    {
        base.Reset();
        CachePlatformCollider();
    }

    protected override void Awake()
    {
        base.Awake();
        CachePlatformCollider();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        stepDelay = Mathf.Max(0f, stepDelay);
        topContactSkin = Mathf.Max(0f, topContactSkin);
        CachePlatformCollider();
    }

    protected override bool ShouldAutoStart()
    {
        return false;
    }

    protected override bool CanStartMoving()
    {
        if (!mcOnTop)
        {
            stepTimer = 0f;
            return false;
        }

        stepTimer += GameTime.FixedDeltaTime;
        return stepTimer >= stepDelay;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        UpdateMcContact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        UpdateMcContact(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!IsMcCollision(collision))
        {
            return;
        }

        mcOnTop = false;
        if (!IsMoving)
        {
            stepTimer = 0f;
        }
    }

    private void UpdateMcContact(Collision2D collision)
    {
        if (IsMoving || !IsMcCollision(collision))
        {
            return;
        }

        mcOnTop = HasTopContact(collision);
        if (!mcOnTop)
        {
            stepTimer = 0f;
        }
    }

    private bool IsMcCollision(Collision2D collision)
    {
        return collision.rigidbody != null && collision.rigidbody.GetComponentInParent<MCController>() != null;
    }

    private bool HasTopContact(Collision2D collision)
    {
        CachePlatformCollider();
        if (platformCollider == null || collision.collider == null)
        {
            return false;
        }

        Bounds platformBounds = platformCollider.bounds;
        Bounds mcBounds = collision.collider.bounds;
        if (mcBounds.min.y < platformBounds.center.y)
        {
            return false;
        }

        float topY = platformBounds.max.y - topContactSkin;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).point.y >= topY)
            {
                return true;
            }
        }

        return false;
    }

    private void CachePlatformCollider()
    {
        if (platformCollider == null)
        {
            platformCollider = GetComponent<Collider2D>();
        }
    }
}
