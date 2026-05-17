using UnityEngine;

public class RoomSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId = "default";
    [SerializeField] private int facingDirection = GameDirection.Right;
    [SerializeField] private bool drawGizmos = true;

    public string SpawnId
    {
        get { return spawnId; }
    }

    public int FacingDirection
    {
        get { return GameDirection.NormalizeOrDefault(facingDirection); }
    }

    private void Awake()
    {
        NormalizeFacingDirection();
    }

    private void OnValidate()
    {
        NormalizeFacingDirection();
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + GameDirection.ToVector3(FacingDirection));

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.magenta;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.35f, spawnId);
#endif
    }

    private void NormalizeFacingDirection()
    {
        facingDirection = GameDirection.NormalizeOrDefault(facingDirection);
    }
}
