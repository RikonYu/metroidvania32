using UnityEngine;

[RequireComponent(typeof(EnemyController))]
public class StaticEnemyAI : EnemyAI
{
    [Header("Static Fire")]
    [SerializeField] private int fireDirection = GameDirection.Left;
    [SerializeField] private float fireInterval = 1f;
    [SerializeField] private bool fireImmediatelyOnEnable = true;

    private float fireTimer;

    public int FacingDirection
    {
        get { return GameDirection.NormalizeOrDefault(fireDirection, GameDirection.Left); }
    }

    public float FireInterval
    {
        get { return fireInterval; }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        NormalizeStaticValues();
        fireTimer = fireImmediatelyOnEnable ? 0f : fireInterval;
        StopEnemy();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        NormalizeStaticValues();
    }

    protected override void Update()
    {
        CacheEnemy();
        if (!CanRunAI())
        {
            StopEnemy();
            return;
        }

        StopEnemy();
        float deltaTime = GetScaledDeltaTime();
        if (deltaTime <= 0f)
        {
            return;
        }

        fireTimer -= deltaTime;
        if (fireTimer > 0f)
        {
            return;
        }

        Fire();
        fireTimer = fireInterval;
    }

    protected override void FixedUpdate()
    {
        CacheEnemy();
        StopEnemy();
    }

    protected override void OnDrawGizmosSelected()
    {
        NormalizeStaticValues();

        Vector3 origin = transform.position;
        Vector3 forward = GameDirection.ToVector3(fireDirection);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + forward * 1.5f);
        Gizmos.DrawWireSphere(origin + forward * 1.5f, 0.12f);
    }

    private void Fire()
    {
        EnemyController enemy = Enemy;
        if (enemy == null)
        {
            return;
        }

        enemy.FireBullet((Vector2)GameDirection.ToVector3(FacingDirection));
    }

    private void NormalizeStaticValues()
    {
        fireDirection = GameDirection.NormalizeOrDefault(fireDirection, GameDirection.Left);
        fireInterval = Mathf.Max(0.01f, fireInterval);
    }
}
