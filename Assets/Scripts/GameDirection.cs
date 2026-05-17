using UnityEngine;

public static class GameDirection
{
    public const int Down = 2;
    public const int Left = 4;
    public const int Right = 6;
    public const int Up = 8;

    public static bool IsValid(int direction)
    {
        return direction == Down || direction == Left || direction == Right || direction == Up;
    }

    public static int NormalizeOrDefault(int direction, int fallback = Right)
    {
        if (IsValid(direction))
        {
            return direction;
        }

        if (!IsValid(fallback))
        {
            fallback = Right;
        }

        if (direction < 0)
        {
            return Left;
        }

        if (direction > 0)
        {
            return Right;
        }

        return fallback;
    }

    public static Vector3 ToVector3(int direction)
    {
        switch (NormalizeOrDefault(direction))
        {
            case Down:
                return Vector3.down;
            case Left:
                return Vector3.left;
            case Right:
                return Vector3.right;
            case Up:
                return Vector3.up;
            default:
                return Vector3.right;
        }
    }

    public static RoomExitSide NormalizeRoomExitSide(RoomExitSide side)
    {
        switch ((int)side)
        {
            case Down:
                return RoomExitSide.Down;
            case Left:
                return RoomExitSide.Left;
            case Right:
                return RoomExitSide.Right;
            case Up:
                return RoomExitSide.Up;
            case 0:
                return RoomExitSide.Left;
            case 1:
                return RoomExitSide.Right;
            case 3:
                return RoomExitSide.Down;
            default:
                return RoomExitSide.Right;
        }
    }
}
