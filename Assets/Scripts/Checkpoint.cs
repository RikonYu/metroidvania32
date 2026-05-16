using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] private Room room;
    [SerializeField] private int facingDirection = 1;
    [SerializeField] private bool activateOnPlayerTouch = true;
    [SerializeField] private bool drawGizmos = true;

    public Room Room
    {
        get
        {
            if (room == null)
            {
                room = GetComponentInParent<Room>();
            }

            return room;
        }
    }

    public int FacingDirection
    {
        get { return facingDirection >= 0 ? 1 : -1; }
    }

    private void OnValidate()
    {
        facingDirection = facingDirection >= 0 ? 1 : -1;
        if (room == null)
        {
            room = GetComponentInParent<Room>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!activateOnPlayerTouch)
        {
            return;
        }

        PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn != null)
        {
            respawn.SetCheckpoint(this);
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 2f, 0f));
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * FacingDirection);
    }
}
