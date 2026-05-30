using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MCController))]
public class PlayerRespawn : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MCController player;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private CamParent cameraRig;
    [SerializeField] private Checkpoint currentCheckpoint;
    [SerializeField] private CampController currentCamp;

    [Header("Safe Ground")]
    [SerializeField] private float safeRecordInterval = 0.15f;
    [SerializeField] private float maxSafeVerticalSpeed = 0.05f;

    [Header("Death")]
    [SerializeField] private float respawnInputLock = 0.15f;
    [SerializeField] private float invulnerabilityDuration = 0.5f;
    [SerializeField] private float hitInvulnerabilityDuration = 0.75f;
    [SerializeField] private float hitFlashInterval = 0.08f;
    [SerializeField] private LayerMask hazardMask;
    [SerializeField] private LayerMask enemyMask;

    [Header("Elemental Status")]
    [SerializeField] private bool isBurning;
    [SerializeField] private bool isFrozen;
    [SerializeField] private bool isPoisoned;

    private Room lastSafeRoom;
    private Vector3 lastSafePosition;
    private int lastSafeFacing = GameDirection.Right;
    private float nextSafeRecordTime;
    private bool hasSafePosition;
    private bool isRespawning;
    private float invulnerableUntil;
    private Coroutine flashRoutine;
    private Coroutine knockbackRoutine;
    private SpriteRenderer[] spriteRenderers;

    public bool IsInvulnerable
    {
        get { return Time.time < invulnerableUntil; }
    }

    public bool HasSafePosition
    {
        get { return hasSafePosition; }
    }

    public Vector3 LastSafePosition
    {
        get { return lastSafePosition; }
    }

    public Checkpoint CurrentCheckpoint
    {
        get { return currentCheckpoint; }
    }

    public bool IsBurning
    {
        get { return player != null ? player.IsBurning : isBurning; }
    }

    public bool IsFrozen
    {
        get { return player != null ? player.IsFrozen : isFrozen; }
    }

    public bool IsPoisoned
    {
        get { return player != null ? player.IsPoisoned : isPoisoned; }
    }

    private void Awake()
    {
        CacheReferences();
        EnsureLayerMasks();
        CacheSpriteRenderers();
        RecordSafeGround(true);
    }

    private void Reset()
    {
        player = GetComponent<MCController>();
        EnsureLayerMasks();
    }

    private void Update()
    {
        RecordSafeGround(false);
        SyncElementalStatusesFromPlayer();
    }

    public void SetCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint != null)
        {
            currentCheckpoint = checkpoint;
            currentCamp = null;
        }
    }

    public void SetCamp(CampController camp)
    {
        if (camp == null)
        {
            return;
        }

        currentCamp = camp;
        currentCheckpoint = null;
    }

    public void DieFromHazard(bool ignoreInvulnerability = false)
    {
        if (isRespawning || (!ignoreInvulnerability && IsInvulnerable))
        {
            return;
        }

        if (!hasSafePosition)
        {
            RecordSafeGround(true);
        }

        StartCoroutine(RespawnRoutine(lastSafeRoom, lastSafePosition, GameDirection.NormalizeOrDefault(lastSafeFacing), false));
    }

    public void DieFromEnemy(bool ignoreInvulnerability = false)
    {
        if (isRespawning || (!ignoreInvulnerability && IsInvulnerable))
        {
            return;
        }

        if (currentCamp != null)
        {
            Utils.RestoreHealthBottles();
            StartCoroutine(RespawnRoutine(currentCamp.Room, currentCamp.RespawnPosition, currentCamp.FacingDirection, true));
            return;
        }

        if (currentCheckpoint != null)
        {
            StartCoroutine(RespawnRoutine(currentCheckpoint.Room, currentCheckpoint.transform.position, currentCheckpoint.FacingDirection, false));
            return;
        }

        DieFromHazard(ignoreInvulnerability);
    }

    public bool TakeDamageFromEnemy(int damage)
    {
        if (isRespawning || IsInvulnerable || player == null)
        {
            return false;
        }

        bool died = player.TakeDamage(damage);
        if (!died)
        {
            StartInvulnerability(hitInvulnerabilityDuration);
        }

        return true;
    }

    public void TakeEnemyMeleeHit(EnemyController enemy, Vector2 knockbackDirection)
    {
        if (enemy == null || IsInvulnerable || isRespawning)
        {
            return;
        }

        if (player != null && player.IsDashActive)
        {
            return;
        }

        bool died = player != null && player.TakeDamage(enemy.ContactDamage);
        if (died)
        {
            return;
        }

        StartInvulnerability(hitInvulnerabilityDuration);

        float distance = enemy.ContactKnockbackDistance;
        if (distance > 0f)
        {
            StartKnockback(knockbackDirection, distance, enemy.KnockbackDuration);
        }
    }

    public void ApplyBurning(int sourceDamage)
    {
        if (!isRespawning)
        {
            isBurning = true;
            if (player != null)
            {
                player.ApplyBurning(sourceDamage);
            }
        }
    }

    public void ApplyFrozen()
    {
        if (!isRespawning)
        {
            isFrozen = true;
            if (player != null)
            {
                player.ApplyFrozen();
            }
        }
    }

    public void ClearFrozen()
    {
        isFrozen = false;
        if (player != null)
        {
            player.ClearFrozen();
        }
    }

    public void ApplyPoisoned()
    {
        if (!isRespawning)
        {
            isPoisoned = true;
        }
    }

    private IEnumerator RespawnRoutine(Room targetRoom, Vector3 targetPosition, int facingDirection, bool reviveEnemies)
    {
        isRespawning = true;
        player.SetInputLocked(true);
        player.ClearVelocity();
        player.RestoreHpToFull();
        player.ResetStamina();
        ClearElementalStatuses();

        if (targetRoom != null && roomManager != null)
        {
            roomManager.SetActiveRoom(targetRoom);
        }

        player.TeleportTo(targetPosition, GameDirection.NormalizeOrDefault(facingDirection));

        if (reviveEnemies)
        {
            EnemyController.RespawnNonBossEnemies();
            EnemySpawner.ResetUnfinishedSpawnersForCampRespawn();
        }

        if (cameraRig != null)
        {
            cameraRig.HardCutToTarget();
        }

        StartInvulnerability(invulnerabilityDuration);

        if (respawnInputLock > 0f)
        {
            yield return new WaitForSeconds(respawnInputLock);
        }

        player.SetInputLocked(false);
        isRespawning = false;
    }

    private void ClearElementalStatuses()
    {
        isBurning = false;
        isFrozen = false;
        isPoisoned = false;
        if (player != null)
        {
            player.ClearElementalStatuses();
        }
    }

    private void SyncElementalStatusesFromPlayer()
    {
        if (player == null)
        {
            return;
        }

        isBurning = player.IsBurning;
        isFrozen = player.IsFrozen;
        isPoisoned = player.IsPoisoned;
    }

    private void RecordSafeGround(bool force)
    {
        if (player == null || roomManager == null)
        {
            return;
        }

        if (!force && Time.time < nextSafeRecordTime)
        {
            return;
        }

        if (!force)
        {
            if (!player.IsOnSafeGround)
            {
                return;
            }

            if (Mathf.Abs(player.Velocity.y) > maxSafeVerticalSpeed)
            {
                return;
            }
        }

        lastSafeRoom = roomManager.ActiveRoom;
        lastSafePosition = transform.position;
        lastSafeFacing = player.FacingDirection;
        hasSafePosition = lastSafeRoom != null;
        nextSafeRecordTime = Time.time + safeRecordInterval;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleDeathCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Utils.IsPoisonousWater(other, transform.position))
        {
            DieFromHazard();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleDeathCollider(collision.collider);
    }

    private void HandleDeathCollider(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (Utils.IsLayerInMask(other.gameObject.layer, hazardMask) || Utils.IsPoisonousWater(other, transform.position))
        {
            DieFromHazard();
            return;
        }

        if (Utils.IsLayerInMask(other.gameObject.layer, enemyMask))
        {
            EnemyController enemy = other.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                Vector2 knockbackDirection = transform.position - enemy.transform.position;
                TakeEnemyMeleeHit(enemy, knockbackDirection);
            }
        }
    }

    private void CacheReferences()
    {
        if (player == null)
        {
            player = GetComponent<MCController>();
        }

        if (roomManager == null)
        {
            roomManager = RoomManager.Instance != null ? RoomManager.Instance : FindObjectOfType<RoomManager>();
        }

        if (cameraRig == null)
        {
            cameraRig = FindObjectOfType<CamParent>();
        }
    }

    private void CacheSpriteRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void EnsureLayerMasks()
    {
        if (hazardMask == 0)
        {
            hazardMask = LayerMask.GetMask(GameLayers.Hazard);
        }

        if (enemyMask == 0)
        {
            enemyMask = LayerMask.GetMask(GameLayers.Enemy);
        }
    }

    private void StartInvulnerability(float duration)
    {
        invulnerableUntil = Time.time + Mathf.Max(0f, duration);
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine(duration));
    }

    private void StartKnockback(Vector2 rawDirection, float distance, float duration)
    {
        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
        }

        knockbackRoutine = StartCoroutine(KnockbackRoutine(rawDirection, distance, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 rawDirection, float distance, float duration)
    {
        Vector2 direction = rawDirection.sqrMagnitude > 0.0001f ? rawDirection.normalized : Vector2.right;
        float safeDuration = Mathf.Max(0.01f, duration);
        player.SetInputLocked(true);
        player.SetVelocity(direction * (distance / safeDuration));
        yield return new WaitForSeconds(safeDuration);
        player.ClearVelocity();
        player.SetInputLocked(false);
        knockbackRoutine = null;
    }

    private IEnumerator FlashRoutine(float duration)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            CacheSpriteRenderers();
        }

        float endTime = Time.time + Mathf.Max(0f, duration);
        bool visible = true;
        while (Time.time < endTime)
        {
            visible = !visible;
            SetSpriteRenderersVisible(visible);
            yield return new WaitForSeconds(Mathf.Max(0.01f, hitFlashInterval));
        }

        SetSpriteRenderersVisible(true);
        flashRoutine = null;
    }

    private void SetSpriteRenderersVisible(bool visible)
    {
        if (spriteRenderers == null)
        {
            return;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].enabled = visible;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!hasSafePosition)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(lastSafePosition, 0.35f);
    }
}
