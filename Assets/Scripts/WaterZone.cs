using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterZone : MonoBehaviour
{
    private static readonly System.Collections.Generic.List<WaterZone> ActiveZones = new System.Collections.Generic.List<WaterZone>();

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

    [Header("Death")]
    [SerializeField] private bool isPoisonous;

    [Header("Editor")]
    [SerializeField] private Color gizmoColor = new Color(0f, 0.45f, 1f, 0.25f);
    [SerializeField] private Color poisonousGizmoColor = new Color(0.65f, 0f, 1f, 0.3f);

    public float PlayerHorizontalSwimSpeed { get { return playerHorizontalSwimSpeed; } }
    public float PlayerVerticalSwimSpeed { get { return playerVerticalSwimSpeed; } }
    public float PlayerSwimAcceleration { get { return playerSwimAcceleration; } }
    public float PlayerSwimDeceleration { get { return playerSwimDeceleration; } }
    public float WaterExitBoost { get { return waterExitBoost; } }
    public float BulletDrag { get { return bulletDrag; } }
    public bool KillNonUnderwaterEnemies { get { return killNonUnderwaterEnemies; } }
    public bool IsPoisonous { get { return isPoisonous; } }
    public Color GizmoColor { get { return isPoisonous ? poisonousGizmoColor : gizmoColor; } }

    private Collider2D waterCollider;

    public bool CleansePoison()
    {
        if (!isPoisonous)
        {
            return false;
        }

        isPoisonous = false;
        return true;
    }

    public static WaterZone GetZoneAtPoint(Vector2 worldPoint)
    {
        if (AirZone.HasAirAtPoint(worldPoint))
        {
            return null;
        }

        for (int i = ActiveZones.Count - 1; i >= 0; i--)
        {
            WaterZone waterZone = ActiveZones[i];
            if (waterZone == null)
            {
                ActiveZones.RemoveAt(i);
                continue;
            }

            if (waterZone.ContainsPoint(worldPoint))
            {
                return waterZone;
            }
        }

        return null;
    }

    private void Awake()
    {
        CacheCollider();
        EnsureTriggerCollider();
        ApplyWaterLayer();
    }

    private void OnEnable()
    {
        if (!ActiveZones.Contains(this))
        {
            ActiveZones.Add(this);
        }

        CacheCollider();
        EnsureTriggerCollider();
        ApplyWaterLayer();
    }

    private void OnDisable()
    {
        ActiveZones.Remove(this);
    }

    private void Reset()
    {
        CacheCollider();
        EnsureTriggerCollider();
        ApplyWaterLayer();
    }

    private void OnValidate()
    {
        playerHorizontalSwimSpeed = Mathf.Max(0f, playerHorizontalSwimSpeed);
        playerVerticalSwimSpeed = Mathf.Max(0f, playerVerticalSwimSpeed);
        playerSwimAcceleration = Mathf.Max(0f, playerSwimAcceleration);
        playerSwimDeceleration = Mathf.Max(0f, playerSwimDeceleration);
        waterExitBoost = Mathf.Max(0f, waterExitBoost);
        bulletDrag = Mathf.Max(0f, bulletDrag);
        CacheCollider();
        EnsureTriggerCollider();
        ApplyWaterLayerAfterValidation();
    }

    private void EnsureTriggerCollider()
    {
        CacheCollider();
        if (waterCollider != null)
        {
            waterCollider.isTrigger = true;
        }
    }

    private void ApplyWaterLayer()
    {
        GameLayers.ApplyTo(gameObject, GameLayers.Water);
    }

    private void ApplyWaterLayerAfterValidation()
    {
        GameLayers.ApplyToAfterValidation(gameObject, GameLayers.Water);
    }

    private void CacheCollider()
    {
        if (waterCollider == null)
        {
            waterCollider = GetComponent<Collider2D>();
        }
    }

    private bool ContainsPoint(Vector2 worldPoint)
    {
        CacheCollider();
        return waterCollider != null && waterCollider.OverlapPoint(worldPoint);
    }

    public bool TryGetGizmoBounds(out Bounds bounds)
    {
        CacheCollider();
        if (waterCollider == null)
        {
            bounds = default;
            return false;
        }

        bounds = waterCollider.bounds;
        return true;
    }
}
