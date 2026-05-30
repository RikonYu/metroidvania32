using UnityEngine;

[DisallowMultipleComponent]
public class BubbleBump : MonoBehaviour
{
    [Header("Bubble")]
    [SerializeField] private Bubble bubblePrefab;
    [SerializeField] private Vector2 initialVelocity = new Vector2(0f, 4f);

    [Header("Timing")]
    [SerializeField] private float interval = 1f;
    [SerializeField] private float initialDelay;
    [SerializeField] private bool spawnImmediatelyOnEnable;

    [Header("Gizmos")]
    [SerializeField] private bool drawBubbleColliderGizmo = true;
    [SerializeField] private Color bubbleColliderGizmoColor = new Color(0.55f, 0.9f, 1f, 0.85f);

    private float spawnTimer;

    private void Awake()
    {
        DisableOwnColliders();
    }

    private void OnEnable()
    {
        spawnTimer = spawnImmediatelyOnEnable ? 0f : GetInitialSpawnDelay();
    }

    private void Reset()
    {
        DisableOwnColliders();
    }

    private void OnValidate()
    {
        interval = Mathf.Max(0.01f, interval);
        initialDelay = Mathf.Max(0f, initialDelay);
        DisableOwnColliders();
    }

    private void Update()
    {
        if (bubblePrefab == null)
        {
            return;
        }

        spawnTimer -= GameTime.DeltaTime;
        while (spawnTimer <= 0f)
        {
            SpawnBubble();
            spawnTimer += interval;
        }
    }

    private void SpawnBubble()
    {
        Bubble bubble = Instantiate(bubblePrefab, transform.position, Quaternion.identity, GetSpawnParent());
        bubble.GiveSpeed(initialVelocity);
    }

    private float GetInitialSpawnDelay()
    {
        return initialDelay > 0f ? initialDelay : interval;
    }

    private Transform GetSpawnParent()
    {
        Room room = GetComponentInParent<Room>();
        if (room != null)
        {
            return room.transform;
        }

        return transform.parent;
    }

    private void DisableOwnColliders()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawBubbleColliderGizmo)
        {
            return;
        }

        CircleCollider2D bubbleCollider = bubblePrefab != null ? bubblePrefab.GetComponent<CircleCollider2D>() : null;
        if (bubbleCollider == null)
        {
            return;
        }

        Gizmos.color = bubbleColliderGizmoColor;
        DrawCircleColliderPreview(bubbleCollider);
    }

    private void DrawCircleColliderPreview(CircleCollider2D circle)
    {
        Vector3 scale = GetPrefabScale();
        Vector3 center = GetPreviewCenter(circle.offset, scale);
        Vector3 diameter = new Vector3(
            Mathf.Abs(scale.x) * circle.radius * 2f,
            Mathf.Abs(scale.y) * circle.radius * 2f,
            1f);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.identity, diameter);
        Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
        Gizmos.matrix = previousMatrix;
    }

    private Vector3 GetPreviewCenter(Vector2 localOffset, Vector3 prefabScale)
    {
        return transform.position + new Vector3(localOffset.x * prefabScale.x, localOffset.y * prefabScale.y, 0f);
    }

    private Vector3 GetPrefabScale()
    {
        return bubblePrefab != null ? bubblePrefab.transform.lossyScale : Vector3.one;
    }
}
