using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RoomEditorSnapper
{
    private static bool hierarchySnapQueued;

    static RoomEditorSnapper()
    {
        SceneView.duringSceneGui += OnSceneGui;
        EditorApplication.hierarchyChanged += QueueHierarchySnap;
        QueueHierarchySnap();
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Event current = Event.current;
        if (current == null || current.type != EventType.MouseUp || current.button != 0)
        {
            return;
        }

        SnapSelectedRooms("Snap Room After Drag");
    }

    private static void QueueHierarchySnap()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || hierarchySnapQueued)
        {
            return;
        }

        hierarchySnapQueued = true;
        EditorApplication.delayCall += SnapRoomsAfterHierarchyChange;
    }

    private static void SnapRoomsAfterHierarchyChange()
    {
        hierarchySnapQueued = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Room[] rooms = Resources.FindObjectsOfTypeAll<Room>();
        for (int i = 0; i < rooms.Length; i++)
        {
            SnapRoomIfSceneInstance(rooms[i], "Snap New Room To Grid");
        }
    }

    private static void SnapSelectedRooms(string undoName)
    {
        HashSet<Room> rooms = new HashSet<Room>();
        Transform[] selectedTransforms = Selection.transforms;
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            Room room = selectedTransforms[i].GetComponentInParent<Room>();
            if (room != null)
            {
                rooms.Add(room);
            }
        }

        foreach (Room room in rooms)
        {
            SnapRoomIfSceneInstance(room, undoName);
        }
    }

    private static void SnapRoomIfSceneInstance(Room room, string undoName)
    {
        if (room == null || EditorUtility.IsPersistent(room))
        {
            return;
        }

        Scene scene = room.gameObject.scene;
        if (!scene.IsValid())
        {
            return;
        }

        Vector3 before = room.transform.position;
        Vector3 after = GetSnappedPosition(before);
        if ((after - before).sqrMagnitude < 0.000001f)
        {
            return;
        }

        Undo.RecordObject(room.transform, undoName);
        room.SnapToGrid();
        EditorUtility.SetDirty(room.transform);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static Vector3 GetSnappedPosition(Vector3 position)
    {
        position.x = Mathf.Round(position.x / Room.BaseWidth) * Room.BaseWidth;
        position.y = Mathf.Round(position.y / Room.BaseHeight) * Room.BaseHeight;
        return position;
    }
}
