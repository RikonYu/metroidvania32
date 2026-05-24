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
        Vector3 worldPosition = Utils.GetMouseWorldPosition(mousePosition);
        return Utils.FindContainingRoom(worldPosition, false);
    }
}
