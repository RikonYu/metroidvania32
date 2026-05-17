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

    [Header("Safe Ground")]
    [SerializeField] private float safeRecordInterval = 0.15f;
    [SerializeField] private float maxSafeVerticalSpeed = 0.05f;

    [Header("Death")]
    [SerializeField] private float respawnInputLock = 0.15f;
    [SerializeField] private float invulnerabilityDuration = 0.5f;
    [SerializeField] private LayerMask hazardMask;
    [SerializeField] private LayerMask enemyMask;

    private Room lastSafeRoom;
    private Vector3 lastSafePosition;
    private int lastSafeFacing = GameDirection.Right;
    private float nextSafeRecordTime;
    private bool hasSafePosition;
    private bool isRespawning;
    private float invulnerableUntil;

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

    private void Awake()
    {
        CacheReferences();
        EnsureLayerMasks();
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
    }

    public void SetCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint != null)
        {
            currentCheckpoint = checkpoint;
        }
    }

    public void DieFromHazard()
    {
        if (isRespawning || IsInvulnerable)
        {
            return;
        }

        if (!hasSafePosition)
        {
            RecordSafeGround(true);
        }

        StartCoroutine(RespawnRoutine(lastSafeRoom, lastSafePosition, GameDirection.NormalizeOrDefault(lastSafeFacing)));
    }

    public void DieFromEnemy()
    {
        if (isRespawning || IsInvulnerable)
        {
            return;
        }

        if (currentCheckpoint != null)
        {
            StartCoroutine(RespawnRoutine(currentCheckpoint.Room, currentCheckpoint.transform.position, currentCheckpoint.FacingDirection));
            return;
        }

        DieFromHazard();
    }

    private IEnumerator RespawnRoutine(Room targetRoom, Vector3 targetPosition, int facingDirection)
    {
        isRespawning = true;
        player.SetInputLocked(true);
        player.ClearVelocity();

        if (targetRoom != null && roomManager != null)
        {
            roomManager.SetActiveRoom(targetRoom);
        }

        player.TeleportTo(targetPosition, GameDirection.NormalizeOrDefault(facingDirection));

        if (cameraRig != null)
        {
            cameraRig.HardCutToTarget();
        }

        invulnerableUntil = Time.time + invulnerabilityDuration;

        if (respawnInputLock > 0f)
        {
            yield return new WaitForSeconds(respawnInputLock);
        }

        player.SetInputLocked(false);
        isRespawning = false;
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

        if (IsLayerInMask(other.gameObject.layer, hazardMask))
        {
            DieFromHazard();
            return;
        }

        if (IsLayerInMask(other.gameObject.layer, enemyMask))
        {
            DieFromEnemy();
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

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
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
