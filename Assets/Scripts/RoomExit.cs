using System;
using UnityEngine;

public enum RoomExitSide
{
    Down = GameDirection.Down,
    Left = GameDirection.Left,
    Right = GameDirection.Right,
    Up = GameDirection.Up
}

[Serializable]
public class RoomExit
{
    public string exitId = "";
    public RoomExitSide side = RoomExitSide.Right;
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
