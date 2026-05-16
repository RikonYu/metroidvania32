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
    [SerializeField] private List<RoomExit> exits = new List<RoomExit>();
    [SerializeField] private float exitTriggerThickness = 0.5f;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.7f, 1f, 1f);
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool warnOnOverlap = true;

    private readonly List<RoomExitTrigger> runtimeExitTriggers = new List<RoomExitTrigger>();

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

    public IReadOnlyList<RoomExit> Exits
    {
        get { return exits; }
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
            WarnAboutInvalidExits();
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

    public Rect GetExitRect(RoomExit exit)
    {
        return GetExitRect(exit, exitTriggerThickness);
    }

    public Rect GetExitRect(RoomExit exit, float thickness)
    {
        Rect roomRect = WorldRect;
        float safeLength = Mathf.Max(0.1f, exit.length);
        float safeThickness = Mathf.Max(0.05f, thickness);

        switch (exit.side)
        {
            case RoomExitSide.Left:
                return new Rect(roomRect.xMin - safeThickness * 0.5f, roomRect.yMin + exit.offset, safeThickness, safeLength);
            case RoomExitSide.Right:
                return new Rect(roomRect.xMax - safeThickness * 0.5f, roomRect.yMin + exit.offset, safeThickness, safeLength);
            case RoomExitSide.Up:
                return new Rect(roomRect.xMin + exit.offset, roomRect.yMax - safeThickness * 0.5f, safeLength, safeThickness);
            case RoomExitSide.Down:
                return new Rect(roomRect.xMin + exit.offset, roomRect.yMin - safeThickness * 0.5f, safeLength, safeThickness);
            default:
                return new Rect(roomRect.xMin, roomRect.yMin, safeThickness, safeLength);
        }
    }

    public void BuildRuntimeExitTriggers()
    {
        ClearRuntimeExitTriggers();

        for (int i = 0; i < exits.Count; i++)
        {
            RoomExit exit = exits[i];
            GameObject triggerObject = new GameObject(string.Format("ExitTrigger_{0}", exit.GetDisplayId()));
            triggerObject.transform.SetParent(transform, false);

            Rect exitRect = GetExitRect(exit);
            triggerObject.transform.position = new Vector3(exitRect.center.x, exitRect.center.y, transform.position.z);
            int triggerLayer = LayerMask.NameToLayer(GameLayers.Trigger);
            if (triggerLayer >= 0)
            {
                triggerObject.layer = triggerLayer;
            }

            BoxCollider2D collider2D = triggerObject.AddComponent<BoxCollider2D>();
            collider2D.isTrigger = true;
            collider2D.size = new Vector2(exitRect.width, exitRect.height);

            RoomExitTrigger trigger = triggerObject.AddComponent<RoomExitTrigger>();
            trigger.Configure(this, exit);
            runtimeExitTriggers.Add(trigger);
        }
    }

    public RoomSpawnPoint FindSpawnPoint(string spawnId)
    {
        RoomSpawnPoint[] spawnPoints = GetComponentsInChildren<RoomSpawnPoint>(true);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i].SpawnId == spawnId)
            {
                return spawnPoints[i];
            }
        }

        return spawnPoints.Length > 0 ? spawnPoints[0] : null;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(string newRoomId, Vector2Int newSizeUnits, List<RoomExit> newExits)
    {
        roomId = newRoomId;
        sizeUnits = newSizeUnits;
        exits = newExits != null ? newExits : new List<RoomExit>();
        OnValidate();
    }
#endif

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
        exitTriggerThickness = Mathf.Max(0.05f, exitTriggerThickness);

        for (int i = 0; i < exits.Count; i++)
        {
            exits[i].index = Mathf.Max(0, exits[i].index);
            exits[i].length = Mathf.Max(0.1f, exits[i].length);
        }
    }

    private void ClearRuntimeExitTriggers()
    {
        for (int i = runtimeExitTriggers.Count - 1; i >= 0; i--)
        {
            RoomExitTrigger trigger = runtimeExitTriggers[i];
            if (trigger == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(trigger.gameObject);
            }
            else
            {
                DestroyImmediate(trigger.gameObject);
            }
        }

        runtimeExitTriggers.Clear();
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

    private void WarnAboutInvalidExits()
    {
        for (int i = 0; i < exits.Count; i++)
        {
            RoomExit exit = exits[i];
            if (exit.targetRoom == null)
            {
                Debug.LogWarning(string.Format("Room exit '{0}' on {1} has no target room.", exit.GetDisplayId(), RoomId), this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(exit.targetSpawnId))
            {
                Debug.LogWarning(string.Format("Room exit '{0}' on {1} has no target spawn id.", exit.GetDisplayId(), RoomId), this);
                continue;
            }

            if (exit.targetRoom.FindSpawnPoint(exit.targetSpawnId) == null)
            {
                Debug.LogWarning(
                    string.Format("Room exit '{0}' on {1} targets missing spawn '{2}' in {3}.", exit.GetDisplayId(), RoomId, exit.targetSpawnId, exit.targetRoom.RoomId),
                    this);
            }
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

        DrawExitGizmos();
    }

    private void DrawExitGizmos()
    {
        for (int i = 0; i < exits.Count; i++)
        {
            RoomExit exit = exits[i];
            Rect exitRect = GetExitRect(exit);
            Vector3 center = new Vector3(exitRect.center.x, exitRect.center.y, transform.position.z);
            Vector3 size = new Vector3(exitRect.width, exitRect.height, 0f);

            Gizmos.color = GetExitColor(exit.side);
            Gizmos.DrawWireCube(center, size);

            string targetName = exit.targetRoom != null ? exit.targetRoom.RoomId : "missing target";
            Handles.color = Gizmos.color;
            Handles.Label(center, string.Format("{0} -> {1}:{2}", exit.GetDisplayId(), targetName, exit.targetSpawnId));
        }
    }

    private static Color GetExitColor(RoomExitSide side)
    {
        switch (side)
        {
            case RoomExitSide.Left:
                return Color.blue;
            case RoomExitSide.Right:
                return Color.red;
            case RoomExitSide.Up:
                return Color.green;
            case RoomExitSide.Down:
                return Color.yellow;
            default:
                return Color.white;
        }
    }
#endif
}
