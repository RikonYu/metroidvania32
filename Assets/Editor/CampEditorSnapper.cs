using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CampEditorSnapper
{
    private static bool snapQueued;

    static CampEditorSnapper()
    {
        EditorApplication.hierarchyChanged += QueueSnapSelectedCamps;
        SceneView.duringSceneGui += OnSceneGui;
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Utils.IsInPrefabMode())
        {
            return;
        }

        Event current = Event.current;
        if (current == null || current.type != EventType.MouseUp || current.button != 0)
        {
            return;
        }

        SnapSelectedCamps("Snap Camp To Ground");
    }

    private static void QueueSnapSelectedCamps()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || snapQueued || Utils.IsInPrefabMode())
        {
            return;
        }

        snapQueued = true;
        EditorApplication.delayCall += SnapSelectedCampsAfterHierarchyChange;
    }

    private static void SnapSelectedCampsAfterHierarchyChange()
    {
        snapQueued = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode || Utils.IsInPrefabMode())
        {
            return;
        }

        SnapSelectedCamps("Snap New Camp To Ground");
    }

    private static void SnapSelectedCamps(string undoName)
    {
        HashSet<CampController> camps = new HashSet<CampController>();
        Transform[] selectedTransforms = Selection.transforms;
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            CampController camp = selectedTransforms[i].GetComponentInParent<CampController>();
            if (camp != null)
            {
                camps.Add(camp);
            }
        }

        foreach (CampController camp in camps)
        {
            Utils.SnapToGround(camp, undoName);
        }
    }
}
