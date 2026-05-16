using System;
using UnityEngine;

public enum RoomExitSide
{
    Left,
    Right,
    Up,
    Down
}

[Serializable]
public class RoomExit
{
    public string exitId = "";
    public RoomExitSide side;
    public int index;
    public float offset;
    public float length = 2f;
    public Room targetRoom;
    public string targetSpawnId = "";

    public string GetDisplayId()
    {
        if (!string.IsNullOrWhiteSpace(exitId))
        {
            return exitId;
        }

        return string.Format("{0}_{1}", side, index);
    }
}
