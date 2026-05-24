using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AirZone : MonoBehaviour
{
    private static readonly List<AirZone> ActiveZones = new List<AirZone>();

    [Header("Editor")]
    [SerializeField] private Color gizmoColor = new Color(0.88f, 1f, 1f, 0.9f);

    private Collider2D airCollider;

    public Color GizmoColor { get { return gizmoColor; } }

    public static AirZone GetZoneAtPoint(Vector2 worldPoint)
    {
        for (int i = ActiveZones.Count - 1; i >= 0; i--)
        {
            AirZone airZone = ActiveZones[i];
            if (airZone == null)
            {
                ActiveZones.RemoveAt(i);
                continue;
            }

            if (airZone.ContainsPoint(worldPoint))
            {
                return airZone;
            }
        }

        return null;
    }

    public static bool HasAirAtPoint(Vector2 worldPoint)
    {
        return GetZoneAtPoint(worldPoint) != null;
    }

    private void Awake()
    {
        CacheCollider();
        EnsureTriggerCollider();
    }

    private void OnEnable()
    {
        if (!ActiveZones.Contains(this))
        {
            ActiveZones.Add(this);
        }

        CacheCollider();
        EnsureTriggerCollider();
    }

    private void OnDisable()
    {
        ActiveZones.Remove(this);
    }

    private void Reset()
    {
        CacheCollider();
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        CacheCollider();
        EnsureTriggerCollider();
    }

    private void EnsureTriggerCollider()
    {
        CacheCollider();
        if (airCollider != null)
        {
            airCollider.isTrigger = true;
        }
    }

    private void CacheCollider()
    {
        if (airCollider == null)
        {
            airCollider = GetComponent<Collider2D>();
        }
    }

    private bool ContainsPoint(Vector2 worldPoint)
    {
        CacheCollider();
        return airCollider != null && airCollider.OverlapPoint(worldPoint);
    }

    public bool TryGetGizmoBounds(out Bounds bounds)
    {
        CacheCollider();
        if (airCollider == null)
        {
            bounds = default;
            return false;
        }

        bounds = airCollider.bounds;
        return true;
    }
}
