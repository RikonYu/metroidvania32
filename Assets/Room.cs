using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class Room : MonoBehaviour
{
    public const float BaseWidth = 32f;
    public const float BaseHeight = 16f;

    [SerializeField] private string roomId = "";
    [SerializeField] private Vector2Int sizeUnits = Vector2Int.one;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.7f, 1f, 1f);
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool warnOnOverlap = true;

    public string RoomId
    {
        get { return string.IsNullOrWhiteSpace(roomId) ? gameObject.name : roomId; }
    }

    public Vector2Int SizeUnits
    {
        get { return sizeUnits; }
    }

    public Vector2 SizeWorld
    {
        get { return new Vector2(sizeUnits.x * BaseWidth, sizeUnits.y * BaseHeight); }
    }

    public Vector2Int GridPosition
    {
        get
        {
            Vector3 position = transform.position;
            return new Vector2Int(
                Mathf.RoundToInt(position.x / BaseWidth),
                Mathf.RoundToInt(position.y / BaseHeight));
        }
    }

    public Rect WorldRect
    {
        get
        {
            Vector3 position = transform.position;
            Vector2 size = SizeWorld;
            return new Rect(position.x, position.y, size.x, size.y);
        }
    }

    public Bounds WorldBounds
    {
        get
        {
            Rect rect = WorldRect;
            return new Bounds(rect.center, new Vector3(rect.width, rect.height, 0f));
        }
    }

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            roomId = gameObject.name;
        }

        SnapToGrid();
    }

    private void OnValidate()
    {
        SanitizeSize();
        SnapToGrid();

#if UNITY_EDITOR
        if (!Application.isPlaying && warnOnOverlap)
        {
            WarnAboutOverlaps();
        }
#endif
    }

    public void SnapToGrid()
    {
        Vector3 position = transform.position;
        position.x = Mathf.Round(position.x / BaseWidth) * BaseWidth;
        position.y = Mathf.Round(position.y / BaseHeight) * BaseHeight;
        transform.position = position;
    }

    public bool Overlaps(Room other)
    {
        if (other == null || other == this)
        {
            return false;
        }

        return RectsOverlapByArea(WorldRect, other.WorldRect);
    }

    public List<Room> GetOverlappingRooms()
    {
        List<Room> overlappingRooms = new List<Room>();
        Room[] rooms = FindObjectsOfType<Room>();

        for (int i = 0; i < rooms.Length; i++)
        {
            Room other = rooms[i];
            if (Overlaps(other))
            {
                overlappingRooms.Add(other);
            }
        }

        return overlappingRooms;
    }

    private void SanitizeSize()
    {
        sizeUnits.x = Mathf.Max(1, sizeUnits.x);
        sizeUnits.y = Mathf.Max(1, sizeUnits.y);
    }

    private static bool RectsOverlapByArea(Rect a, Rect b)
    {
        const float epsilon = 0.0001f;
        float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return overlapX > epsilon && overlapY > epsilon;
    }

#if UNITY_EDITOR
    private void WarnAboutOverlaps()
    {
        List<Room> overlaps = GetOverlappingRooms();
        for (int i = 0; i < overlaps.Count; i++)
        {
            Room other = overlaps[i];
            Debug.LogWarning(
                string.Format("Room overlap detected: {0} overlaps {1}. Touching edges is valid; overlapping area is not.", RoomId, other.RoomId),
                this);
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        Rect rect = WorldRect;
        Vector3 center = new Vector3(rect.center.x, rect.center.y, transform.position.z);
        Vector3 size = new Vector3(rect.width, rect.height, 0f);

        bool hasOverlap = GetOverlappingRooms().Count > 0;
        Color outlineColor = hasOverlap ? Color.red : gizmoColor;
        Gizmos.color = outlineColor;
        Gizmos.DrawWireCube(center, size);

        Handles.color = outlineColor;
        Handles.Label(
            new Vector3(rect.xMin, rect.yMax, transform.position.z),
            string.Format("{0} ({1},{2}) {3}x{4}", RoomId, GridPosition.x, GridPosition.y, sizeUnits.x, sizeUnits.y));
    }
#endif
}
