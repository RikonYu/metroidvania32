using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class RoomTilePaintTargetSelector
{
    static RoomTilePaintTargetSelector()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Event current = Event.current;
        if (current == null || !ShouldUpdatePaintTarget(current))
        {
            return;
        }

        if (GridPaintingState.gridBrush == null)
        {
            return;
        }

        Room room = FindRoomAtMouse(current.mousePosition);
        if (room == null)
        {
            return;
        }

        Tilemap groundTilemap = room.GroundTilemap;
        if (groundTilemap == null)
        {
            return;
        }

        GameObject target = groundTilemap.gameObject;
        if (GridPaintingState.scenePaintTarget != target)
        {
            GridPaintingState.scenePaintTarget = target;
        }
    }

    private static bool ShouldUpdatePaintTarget(Event current)
    {
        return current.type == EventType.MouseMove
            || current.type == EventType.MouseDown
            || current.type == EventType.MouseDrag
            || current.type == EventType.Repaint;
    }

    private static Room FindRoomAtMouse(Vector2 mousePosition)
    {
        Vector3 worldPosition = GetMouseWorldPosition(mousePosition);
        Vector2 worldPoint = new Vector2(worldPosition.x, worldPosition.y);
        Room[] rooms = Resources.FindObjectsOfTypeAll<Room>();

        Room bestRoom = null;
        float bestArea = float.PositiveInfinity;
        for (int i = 0; i < rooms.Length; i++)
        {
            Room room = rooms[i];
            if (!IsSceneRoom(room))
            {
                continue;
            }

            Rect rect = room.WorldRect;
            if (!rect.Contains(worldPoint))
            {
                continue;
            }

            float area = rect.width * rect.height;
            if (area < bestArea)
            {
                bestArea = area;
                bestRoom = room;
            }
        }

        return bestRoom;
    }

    private static Vector3 GetMouseWorldPosition(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        if (Mathf.Abs(ray.direction.z) < 0.0001f)
        {
            return ray.origin;
        }

        float distance = -ray.origin.z / ray.direction.z;
        return ray.GetPoint(distance);
    }

    private static bool IsSceneRoom(Room room)
    {
        if (room == null || EditorUtility.IsPersistent(room))
        {
            return false;
        }

        Scene scene = room.gameObject.scene;
        return scene.IsValid();
    }
}
