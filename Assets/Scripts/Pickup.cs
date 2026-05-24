using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum PickupType
{
    MaxHp,
    MaxStamina,
    HealthBottleCapacity
}

[RequireComponent(typeof(Collider2D))]
public class Pickup : MonoBehaviour
{
    [SerializeField] private PickupType pickupType = PickupType.MaxHp;

    private bool collected;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
        {
            TryCollect(collision.collider);
        }
    }

    private void TryCollect(Collider2D other)
    {
        if (collected || other == null)
        {
            return;
        }

        MCController player = other.GetComponentInParent<MCController>();
        if (player == null)
        {
            return;
        }

        if (!ApplyPickup(player))
        {
            return;
        }

        collected = true;
        gameObject.SetActive(false);
    }

    private bool ApplyPickup(MCController player)
    {
        switch (pickupType)
        {
            case PickupType.MaxHp:
                player.IncreaseMaxHp(Consts.PickupMaxHpIncrease);
                return true;
            case PickupType.MaxStamina:
                player.IncreaseMaxStamina(Consts.PickupMaxStaminaIncrease);
                return true;
            case PickupType.HealthBottleCapacity:
                if (GameController.Instance == null)
                {
                    return false;
                }

                GameController.Instance.IncreaseHealthBottleCapacity(1);
                return true;
            default:
                return false;
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider == null)
        {
            return;
        }

        DrawDottedBounds(pickupCollider.bounds, GetEditorColor());
    }

    private static void DrawDottedBounds(Bounds bounds, Color color)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3 bottomLeft = new Vector3(min.x, min.y, 0f);
        Vector3 bottomRight = new Vector3(max.x, min.y, 0f);
        Vector3 topRight = new Vector3(max.x, max.y, 0f);
        Vector3 topLeft = new Vector3(min.x, max.y, 0f);

        Handles.color = color;
        Handles.DrawDottedLine(bottomLeft, bottomRight, 4f);
        Handles.DrawDottedLine(bottomRight, topRight, 4f);
        Handles.DrawDottedLine(topRight, topLeft, 4f);
        Handles.DrawDottedLine(topLeft, bottomLeft, 4f);
    }

    private Color GetEditorColor()
    {
        switch (pickupType)
        {
            case PickupType.MaxHp:
                return Color.red;
            case PickupType.MaxStamina:
                return Color.green;
            case PickupType.HealthBottleCapacity:
                return Color.yellow;
            default:
                return Color.white;
        }
    }
#endif
}
