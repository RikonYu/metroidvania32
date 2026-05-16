using System.Collections;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [SerializeField] private Room startingRoom;
    [SerializeField] private MCController player;
    [SerializeField] private CamParent cameraRig;
    [SerializeField] private float transitionInputLock = 0.1f;

    public Room ActiveRoom { get; private set; }

    private void Awake()
    {
        Instance = this;
        CacheSceneReferences();
        BuildAllRoomTriggers();
        SetActiveRoom(startingRoom != null ? startingRoom : FindPlayerRoom());
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
}
