using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }
    public static bool HasInstance
    {
        get { return Instance != null; }
    }

    [SerializeField] private Room startingRoom;
    [SerializeField] private MCController player;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private CamParent cameraRig;
    [SerializeField] private float transitionInputLock = 0.1f;
    [SerializeField] private bool useCoordinateRoomTransitions = true;
    [SerializeField] private bool buildConfiguredExitTriggers;
    [SerializeField] private float boundaryTransitionEpsilon = 0.02f;
    [SerializeField] private float transitionInset = 0.05f;
    [SerializeField] private bool validateRoomParenting = true;
    [SerializeField] private bool drawDebugGizmos = true;

    private readonly List<Room> rooms = new List<Room>();
    private bool transitionInProgress;

    public Room ActiveRoom { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                string.Format("Duplicate RoomManager '{0}' was destroyed. Active singleton is '{1}'.", name, Instance.name),
                this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CacheSceneReferences();
        CacheRooms();
        if (buildConfiguredExitTriggers)
        {
            BuildAllRoomTriggers();
        }

        SetActiveRoom(startingRoom != null ? startingRoom : FindPlayerRoom());
        ValidateRoomParenting();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (useCoordinateRoomTransitions && !transitionInProgress)
        {
            CheckCoordinateRoomTransition();
        }
    }

    public void Transition(Room sourceRoom, RoomExit exit, MCController transitionPlayer)
    {
        if (exit == null || exit.targetRoom == null)
        {
            Debug.LogWarning("Room transition failed: exit target room is missing.", sourceRoom);
            return;
        }

        MCController targetPlayer = transitionPlayer != null ? transitionPlayer : player;
        if (targetPlayer == null)
        {
            Debug.LogWarning("Room transition failed: player is missing.", this);
            return;
        }

        RoomSpawnPoint spawnPoint = exit.targetRoom.FindSpawnPoint(exit.targetSpawnId);
        if (spawnPoint == null)
        {
            Debug.LogWarning(
                string.Format("Room transition failed: spawn '{0}' not found in {1}.", exit.targetSpawnId, exit.targetRoom.RoomId),
                exit.targetRoom);
            return;
        }

        StartCoroutine(TransitionRoutine(exit.targetRoom, spawnPoint, targetPlayer));
    }

    public void SetActiveRoom(Room room)
    {
        if (room == null)
        {
            return;
        }

        EnsureRoomCache();
        for (int i = 0; i < rooms.Count; i++)
        {
            rooms[i].gameObject.SetActive(rooms[i] == room);
        }

        ActiveRoom = room;

        if (cameraRig != null)
        {
            cameraRig.SetCurrentRoom(room);
            cameraRig.HardCutToTarget();
        }
    }

    private IEnumerator TransitionRoutine(Room targetRoom, RoomSpawnPoint spawnPoint, MCController targetPlayer)
    {
        transitionInProgress = true;
        targetPlayer.SetInputLocked(true);
        SetActiveRoom(targetRoom);
        targetPlayer.TeleportTo(spawnPoint.transform.position, spawnPoint.FacingDirection);

        if (cameraRig != null)
        {
            cameraRig.HardCutToTarget();
        }

        if (transitionInputLock > 0f)
        {
            yield return new WaitForSeconds(transitionInputLock);
        }

        targetPlayer.SetInputLocked(false);
        transitionInProgress = false;
    }

    private IEnumerator CoordinateTransitionRoutine(Room targetRoom, Vector3 targetPosition, int facingDirection)
    {
        transitionInProgress = true;

        if (player != null)
        {
            player.SetInputLocked(true);
        }

        SetActiveRoom(targetRoom);

        if (player != null)
        {
            player.TeleportTo(targetPosition, facingDirection);
        }

        if (cameraRig != null)
        {
            cameraRig.HardCutToTarget();
        }

        if (transitionInputLock > 0f)
        {
            yield return new WaitForSeconds(transitionInputLock);
        }

        if (player != null)
        {
            player.SetInputLocked(false);
        }

        transitionInProgress = false;
    }

    private void CacheSceneReferences()
    {
        if (player == null)
        {
            player = FindObjectOfType<MCController>();
        }

        if (playerCollider == null && player != null)
        {
            playerCollider = player.GetComponent<Collider2D>();
        }

        if (cameraRig == null)
        {
            cameraRig = FindObjectOfType<CamParent>();
        }

        if (cameraRig != null && player != null)
        {
            cameraRig.SetTarget(player.transform);
        }
    }

    private void CacheRooms()
    {
        rooms.Clear();
        rooms.AddRange(FindObjectsOfType<Room>(true));
    }

    private void EnsureRoomCache()
    {
        if (rooms.Count == 0)
        {
            CacheRooms();
        }
    }

    private void BuildAllRoomTriggers()
    {
        EnsureRoomCache();
        for (int i = 0; i < rooms.Count; i++)
        {
            rooms[i].BuildRuntimeExitTriggers();
        }
    }

    private Room FindPlayerRoom()
    {
        if (player == null)
        {
            return null;
        }

        EnsureRoomCache();
        Vector2 playerPosition = player.transform.position;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].WorldRect.Contains(playerPosition))
            {
                return rooms[i];
            }
        }

        return rooms.Count > 0 ? rooms[0] : null;
    }

    private void CheckCoordinateRoomTransition()
    {
        if (ActiveRoom == null || player == null || playerCollider == null)
        {
            return;
        }

        Bounds playerBounds = playerCollider.bounds;
        Rect activeRect = ActiveRoom.WorldRect;
        RoomExitSide side;

        if (playerBounds.max.x > activeRect.xMax + boundaryTransitionEpsilon)
        {
            side = RoomExitSide.Right;
        }
        else if (playerBounds.min.x < activeRect.xMin - boundaryTransitionEpsilon)
        {
            side = RoomExitSide.Left;
        }
        else if (playerBounds.max.y > activeRect.yMax + boundaryTransitionEpsilon)
        {
            side = RoomExitSide.Up;
        }
        else if (playerBounds.min.y < activeRect.yMin - boundaryTransitionEpsilon)
        {
            side = RoomExitSide.Down;
        }
        else
        {
            return;
        }

        Room targetRoom = FindAdjacentRoom(ActiveRoom, side, playerBounds);
        if (targetRoom == null)
        {
            return;
        }

        Vector3 targetPosition = GetCoordinateTransitionPosition(side, targetRoom.WorldRect, playerBounds, player.transform.position);
        int facingDirection = GetFacingDirectionForExitSide(side, player.FacingDirection);
        StartCoroutine(CoordinateTransitionRoutine(targetRoom, targetPosition, facingDirection));
    }

    private int GetFacingDirectionForExitSide(RoomExitSide side, int fallbackDirection)
    {
        if (side == RoomExitSide.Left)
        {
            return GameDirection.Left;
        }

        if (side == RoomExitSide.Right)
        {
            return GameDirection.Right;
        }

        if (side == RoomExitSide.Up)
        {
            return GameDirection.Up;
        }

        if (side == RoomExitSide.Down)
        {
            return GameDirection.Down;
        }

        return GameDirection.NormalizeOrDefault(fallbackDirection);
    }

    private Room FindAdjacentRoom(Room sourceRoom, RoomExitSide side, Bounds playerBounds)
    {
        EnsureRoomCache();

        Room bestRoom = null;
        float bestOverlap = 0f;
        Rect sourceRect = sourceRoom.WorldRect;

        for (int i = 0; i < rooms.Count; i++)
        {
            Room candidate = rooms[i];
            if (candidate == null || candidate == sourceRoom)
            {
                continue;
            }

            Rect candidateRect = candidate.WorldRect;
            if (!EdgesTouch(sourceRect, candidateRect, side))
            {
                continue;
            }

            float overlap = GetPlayerOverlapOnSharedEdge(candidateRect, side, playerBounds);
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                bestRoom = candidate;
            }
        }

        return bestRoom;
    }

    private bool EdgesTouch(Rect source, Rect candidate, RoomExitSide side)
    {
        switch (side)
        {
            case RoomExitSide.Left:
                return Mathf.Abs(candidate.xMax - source.xMin) <= boundaryTransitionEpsilon;
            case RoomExitSide.Right:
                return Mathf.Abs(candidate.xMin - source.xMax) <= boundaryTransitionEpsilon;
            case RoomExitSide.Up:
                return Mathf.Abs(candidate.yMin - source.yMax) <= boundaryTransitionEpsilon;
            case RoomExitSide.Down:
                return Mathf.Abs(candidate.yMax - source.yMin) <= boundaryTransitionEpsilon;
            default:
                return false;
        }
    }

    private float GetPlayerOverlapOnSharedEdge(Rect candidate, RoomExitSide side, Bounds playerBounds)
    {
        if (side == RoomExitSide.Left || side == RoomExitSide.Right)
        {
            float min = Mathf.Max(candidate.yMin, playerBounds.min.y);
            float max = Mathf.Min(candidate.yMax, playerBounds.max.y);
            return Mathf.Max(0f, max - min);
        }

        float horizontalMin = Mathf.Max(candidate.xMin, playerBounds.min.x);
        float horizontalMax = Mathf.Min(candidate.xMax, playerBounds.max.x);
        return Mathf.Max(0f, horizontalMax - horizontalMin);
    }

    private Vector3 GetCoordinateTransitionPosition(RoomExitSide side, Rect targetRect, Bounds playerBounds, Vector3 playerPosition)
    {
        Vector3 targetPosition = playerPosition;
        Vector3 colliderOffset = playerBounds.center - playerPosition;
        Vector3 extents = playerBounds.extents;

        if (side == RoomExitSide.Left || side == RoomExitSide.Right)
        {
            float desiredCenterX = side == RoomExitSide.Right
                ? targetRect.xMin + extents.x + transitionInset
                : targetRect.xMax - extents.x - transitionInset;
            float desiredCenterY = Mathf.Clamp(playerBounds.center.y, targetRect.yMin + extents.y + transitionInset, targetRect.yMax - extents.y - transitionInset);
            targetPosition.x = desiredCenterX - colliderOffset.x;
            targetPosition.y = desiredCenterY - colliderOffset.y;
            return targetPosition;
        }

        float desiredCenterYVertical = side == RoomExitSide.Up
            ? targetRect.yMin + extents.y + transitionInset
            : targetRect.yMax - extents.y - transitionInset;
        float desiredCenterXVertical = Mathf.Clamp(playerBounds.center.x, targetRect.xMin + extents.x + transitionInset, targetRect.xMax - extents.x - transitionInset);
        targetPosition.x = desiredCenterXVertical - colliderOffset.x;
        targetPosition.y = desiredCenterYVertical - colliderOffset.y;
        return targetPosition;
    }

    private void ValidateRoomParenting()
    {
        if (!validateRoomParenting)
        {
            return;
        }

        Component[] components = FindObjectsOfType<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            GameObject go = component.gameObject;
            if (!RequiresRoomParent(go) || go.GetComponentInParent<Room>(true) != null)
            {
                continue;
            }

            Debug.LogWarning(
                string.Format("Scene object '{0}' has gameplay components but is not under a Room parent.", go.name),
                go);
        }
    }

    private bool RequiresRoomParent(GameObject go)
    {
        if (go.GetComponent<Room>() != null ||
            go.GetComponent<RoomManager>() != null ||
            go.GetComponentInParent<MCController>() != null ||
            go.GetComponentInParent<PlayerRespawn>() != null ||
            go.GetComponent<CamParent>() != null ||
            go.GetComponent<Camera>() != null)
        {
            return false;
        }

        return go.GetComponent<Collider2D>() != null ||
               go.GetComponent<Rigidbody2D>() != null ||
               go.GetComponent<SpriteRenderer>() != null;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        Room room = ActiveRoom != null ? ActiveRoom : startingRoom;
        if (room == null)
        {
            return;
        }

        Rect rect = room.WorldRect;
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(rect.center, new Vector3(rect.width, rect.height, 0f));

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(
            new Vector3(rect.center.x, rect.yMax + 1f, 0f),
            string.Format("Active Room: {0}", room.RoomId));
#endif
    }
}
