using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterZone : MonoBehaviour
{
    [Header("Player Swim")]
    [SerializeField] private float playerHorizontalSwimSpeed = 5f;
    [SerializeField] private float playerVerticalSwimSpeed = 4.5f;
    [SerializeField] private float playerSwimAcceleration = 20f;
    [SerializeField] private float playerSwimDeceleration = 16f;
    [SerializeField] private float waterExitBoost = 7f;

    [Header("Bullet Drag")]
    [SerializeField] private float bulletDrag = 2f;

    [Header("Enemies")]
    [SerializeField] private bool killNonUnderwaterEnemies = true;

    [Header("Editor")]
    [SerializeField] private Color gizmoColor = new Color(0f, 0.45f, 1f, 0.25f);

    public float PlayerHorizontalSwimSpeed { get { return playerHorizontalSwimSpeed; } }
    public float PlayerVerticalSwimSpeed { get { return playerVerticalSwimSpeed; } }
    public float PlayerSwimAcceleration { get { return playerSwimAcceleration; } }
    public float PlayerSwimDeceleration { get { return playerSwimDeceleration; } }
    public float WaterExitBoost { get { return waterExitBoost; } }
    public float BulletDrag { get { return bulletDrag; } }
    public bool KillNonUnderwaterEnemies { get { return killNonUnderwaterEnemies; } }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        playerHorizontalSwimSpeed = Mathf.Max(0f, playerHorizontalSwimSpeed);
        playerVerticalSwimSpeed = Mathf.Max(0f, playerVerticalSwimSpeed);
        playerSwimAcceleration = Mathf.Max(0f, playerSwimAcceleration);
        playerSwimDeceleration = Mathf.Max(0f, playerSwimDeceleration);
        waterExitBoost = Mathf.Max(0f, waterExitBoost);
        bulletDrag = Mathf.Max(0f, bulletDrag);
        EnsureTriggerCollider();
    }

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }

    private void OnDrawGizmos()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D == null)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(collider2D.bounds.center, collider2D.bounds.size);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.85f);
        Gizmos.DrawWireCube(collider2D.bounds.center, collider2D.bounds.size);
    }
}
