using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CampController : MonoBehaviour
{
    [SerializeField] private Checkpoint checkpoint;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private bool reviveEnemiesOnSave = true;

    public Room Room
    {
        get
        {
            if (checkpoint != null)
            {
                return checkpoint.Room;
            }

            return GetComponentInParent<Room>();
        }
    }

    public Vector3 RespawnPosition
    {
        get { return checkpoint != null ? checkpoint.transform.position : transform.position; }
    }

    public int FacingDirection
    {
        get { return checkpoint != null ? checkpoint.FacingDirection : GameDirection.Right; }
    }

    private void Awake()
    {
        CacheReferences();
        EnsureTriggerCollider();
    }

    private void Reset()
    {
        CacheReferences();
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        CacheReferences();
        EnsureTriggerCollider();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!Input.GetKeyDown(interactKey))
        {
            return;
        }

        PlayerRespawn playerRespawn = other.GetComponentInParent<PlayerRespawn>();
        if (playerRespawn != null)
        {
            Save(playerRespawn);
        }
    }

    public void Save(PlayerRespawn playerRespawn)
    {
        if (playerRespawn == null)
        {
            return;
        }

        playerRespawn.SetCamp(this);
        if (reviveEnemiesOnSave)
        {
            EnemyController.RespawnNonBossEnemies();
        }
    }

    private void CacheReferences()
    {
        if (checkpoint == null)
        {
            checkpoint = GetComponent<Checkpoint>();
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }
}
