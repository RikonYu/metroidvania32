using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class RoomGroundTileBoundsEnforcer
{
    private const string PruneUndoName = "Prune Ground Tiles Outside Room";

    [MenuItem("Tools/Rooms/Prune Ground Tiles Outside Rooms")]
    public static void PruneAllGroundTilesFromMenu()
    {
        PruneAllGroundTiles();
    }

    private static void PruneAllGroundTiles()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Room[] rooms = Resources.FindObjectsOfTypeAll<Room>();
        HashSet<Tilemap> seenTilemaps = new HashSet<Tilemap>();
        HashSet<Scene> dirtyScenes = new HashSet<Scene>();

        for (int i = 0; i < rooms.Length; i++)
        {
            PruneRoomGroundTiles(rooms[i], seenTilemaps, dirtyScenes);
        }

        foreach (Scene scene in dirtyScenes)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static void PruneRoomGroundTiles(Room room, HashSet<Tilemap> seenTilemaps, HashSet<Scene> dirtyScenes)
    {
        if (!Utils.IsSceneRoom(room, false))
        {
            return;
        }

        Tilemap tilemap = room.GroundTilemap;
        if (tilemap == null || !seenTilemaps.Add(tilemap))
        {
            return;
        }

        BoundsInt bounds = tilemap.cellBounds;
        if (bounds.size.x <= 0 || bounds.size.y <= 0)
        {
            return;
        }

        bool changed = false;
        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cellPosition) || IsCellInsideRoom(tilemap, cellPosition, room))
            {
                continue;
            }

            if (!changed)
            {
                Undo.RegisterCompleteObjectUndo(tilemap, PruneUndoName);
                changed = true;
            }

            tilemap.SetTile(cellPosition, null);
        }

        if (!changed)
        {
            return;
        }

        tilemap.CompressBounds();
        EditorUtility.SetDirty(tilemap);
        dirtyScenes.Add(tilemap.gameObject.scene);
    }

    private static bool IsCellInsideRoom(Tilemap tilemap, Vector3Int cellPosition, Room room)
    {
        Vector3 worldCenter = tilemap.GetCellCenterWorld(cellPosition);
        Rect roomRect = room.WorldRect;
        return roomRect.Contains(new Vector2(worldCenter.x, worldCenter.y));
    }
}
