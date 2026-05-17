using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MCController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpVelocity = 14f;
    [SerializeField] private float maxFallSpeed = 24f;

    [Header("Jump Feel")]
    [SerializeField] private float coyoteTime = 0.08f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float fallGravityMultiplier = 1.8f;
    [SerializeField] private float lowJumpGravityMultiplier = 2.2f;

    [Header("Ground Check")]
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -1.05f);
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.85f, 0.12f);
    [SerializeField] private LayerMask movementGroundMask;
    [SerializeField] private LayerMask safeGroundMask;

    [Header("Edge Stability")]
    [SerializeField] private bool preventIdleEdgeSlip = true;
    [SerializeField] private float idleEdgeSlipInputThreshold = 0.01f;
    [SerializeField] private float idleEdgeSlipMaxVerticalSpeed = 0.05f;

    private Rigidbody2D body;
    private RigidbodyConstraints2D movementConstraints;
    private float inputX;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool jumpHeld;
    private bool inputLocked;
    private Collider2D currentGround;
    private bool edgeSlipLockActive;

    public bool IsGrounded { get; private set; }
    public bool IsOnSafeGround { get; private set; }
    public int FacingDirection { get; private set; } = 1;

    public Vector2 Velocity
    {
        get { return body != null ? body.velocity : Vector2.zero; }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.freezeRotation = true;
        movementConstraints = body.constraints;
        EnsureLayerMasks();
    }

    private void Reset()
    {
        EnsureLayerMasks();
    }

    private void OnDisable()
    {
        if (body != null && edgeSlipLockActive)
        {
            body.constraints = movementConstraints;
            edgeSlipLockActive = false;
        }
    }

    private void Update()
    {
        if (inputLocked)
        {
            inputX = 0f;
            jumpHeld = false;
            return;
        }

        inputX = Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetKey(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (inputX > 0.01f)
        {
            FacingDirection = 1;
        }
        else if (inputX < -0.01f)
        {
            FacingDirection = -1;
        }
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
        UpdateTimers();
        ApplyMovement();
        ApplyJump();
        ApplyExtraGravity();
        ApplyIdleEdgeSlipLock();
    }

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        if (locked)
        {
            inputX = 0f;
            jumpBufferCounter = 0f;
        }
    }

    public void TeleportTo(Vector3 position, int facingDirection)
    {
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

    public Collider2D GetCurrentGround()
    {
        return currentGround;
    }

    private void ApplyMovement()
    {
        Vector2 velocity = body.velocity;
        velocity.x = inputX * moveSpeed;
        body.velocity = velocity;
    }

    private void ApplyJump()
    {
        if (jumpBufferCounter <= 0f || coyoteCounter <= 0f)
        {
            return;
        }

        Vector2 velocity = body.velocity;
        velocity.y = jumpVelocity;
        body.velocity = velocity;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        IsGrounded = false;
        IsOnSafeGround = false;
    }

    private void ApplyExtraGravity()
    {
        Vector2 velocity = body.velocity;
        if (velocity.y < 0f)
        {
            velocity += Physics2D.gravity * ((fallGravityMultiplier - 1f) * Time.fixedDeltaTime);
        }
        else if (velocity.y > 0f && !jumpHeld)
        {
            velocity += Physics2D.gravity * ((lowJumpGravityMultiplier - 1f) * Time.fixedDeltaTime);
        }

        if (velocity.y < -maxFallSpeed)
        {
            velocity.y = -maxFallSpeed;
        }

        body.velocity = velocity;
    }

    private void ApplyIdleEdgeSlipLock()
    {
        bool shouldLock = ShouldLockIdleEdgeSlip();
        if (edgeSlipLockActive == shouldLock)
        {
            return;
        }

        edgeSlipLockActive = shouldLock;
        body.constraints = shouldLock
            ? movementConstraints | RigidbodyConstraints2D.FreezePositionX
            : movementConstraints;
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

        return Mathf.Abs(body.velocity.y) <= idleEdgeSlipMaxVerticalSpeed;
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

    private void UpdateGroundedState()
    {
        Vector2 center = (Vector2)transform.position + groundCheckOffset;
        currentGround = Physics2D.OverlapBox(center, groundCheckSize, 0f, movementGroundMask);
        IsGrounded = currentGround != null;
        IsOnSafeGround = IsGrounded && IsColliderOnMask(currentGround, safeGroundMask);
    }

    private void SetFacingDirection(int facingDirection)
    {
        if (facingDirection == 0)
        {
            return;
        }

        FacingDirection = facingDirection > 0 ? 1 : -1;
    }

    private static bool IsColliderOnMask(Collider2D collider2D, LayerMask mask)
    {
        return collider2D != null && (mask.value & (1 << collider2D.gameObject.layer)) != 0;
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
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsOnSafeGround ? Color.green : Color.yellow;
        Vector2 center = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireCube(center, groundCheckSize);
    }
}
