using System.Collections;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [SerializeField] private Room startingRoom;
    [SerializeField] private MCController player;
    [SerializeField] private CamParent cameraRig;
    [SerializeField] private float transitionInputLock = 0.1f;
    [SerializeField] private bool validateRoomParenting = true;
    [SerializeField] private bool drawDebugGizmos = true;

    public Room ActiveRoom { get; private set; }

    private void Awake()
    {
        Instance = this;
        CacheSceneReferences();
        BuildAllRoomTriggers();
        SetActiveRoom(startingRoom != null ? startingRoom : FindPlayerRoom());
        ValidateRoomParenting();
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

        Room[] rooms = FindObjectsOfType<Room>(true);
        for (int i = 0; i < rooms.Length; i++)
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
    }

    private void CacheSceneReferences()
    {
        if (player == null)
        {
            player = FindObjectOfType<MCController>();
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

    private void BuildAllRoomTriggers()
    {
        Room[] rooms = FindObjectsOfType<Room>(true);
        for (int i = 0; i < rooms.Length; i++)
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

        Room[] rooms = FindObjectsOfType<Room>(true);
        Vector2 playerPosition = player.transform.position;
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i].WorldRect.Contains(playerPosition))
            {
                return rooms[i];
            }
        }

        return rooms.Length > 0 ? rooms[0] : null;
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
            go.GetComponent<MCController>() != null ||
            go.GetComponent<PlayerRespawn>() != null ||
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
