using UnityEngine;

public class RoomSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId = "default";
    [SerializeField] private int facingDirection = 1;
    [SerializeField] private bool drawGizmos = true;

    public string SpawnId
    {
        get { return spawnId; }
    }

    public int FacingDirection
    {
        get { return facingDirection >= 0 ? 1 : -1; }
    }

    private void OnValidate()
    {
        facingDirection = facingDirection >= 0 ? 1 : -1;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * FacingDirection);

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.magenta;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.35f, spawnId);
#endif
    }
}
