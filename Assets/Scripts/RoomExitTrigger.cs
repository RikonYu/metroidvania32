using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RoomExitTrigger : MonoBehaviour
{
    private Room sourceRoom;
    private RoomExit exit;

    public void Configure(Room room, RoomExit roomExit)
    {
        sourceRoom = room;
        exit = roomExit;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        int playerLayer = LayerMask.NameToLayer(GameLayers.Player);
        if (playerLayer >= 0 && other.gameObject.layer != playerLayer)
        {
            return;
        }

        MCController player = other.GetComponentInParent<MCController>();
        if (player == null)
        {
            return;
        }

        RoomManager manager = RoomManager.Instance;
        if (manager != null)
        {
            manager.Transition(sourceRoom, exit, player);
        }
    }
}
