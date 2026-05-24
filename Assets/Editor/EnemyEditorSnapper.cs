using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyEditorSnapper
{
    private static bool snapQueued;

    static EnemyEditorSnapper()
    {
        EditorApplication.hierarchyChanged += QueueSnapSelectedEnemies;
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

        SnapSelectedEnemies("Snap Enemy To Ground");
    }

    private static void QueueSnapSelectedEnemies()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || snapQueued || Utils.IsInPrefabMode())
        {
            return;
        }

        snapQueued = true;
        EditorApplication.delayCall += SnapSelectedEnemiesAfterHierarchyChange;
    }

    private static void SnapSelectedEnemiesAfterHierarchyChange()
    {
        snapQueued = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode || Utils.IsInPrefabMode())
        {
            return;
        }

        SnapSelectedEnemies("Snap New Enemy To Ground");
    }

    private static void SnapSelectedEnemies(string undoName)
    {
        HashSet<EnemyController> enemies = new HashSet<EnemyController>();
        Transform[] selectedTransforms = Selection.transforms;
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            EnemyController enemy = selectedTransforms[i].GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.MovementKind != EnemyMovementKind.Flying)
            {
                enemies.Add(enemy);
            }
        }

        foreach (EnemyController enemy in enemies)
        {
            Utils.SnapToGround(enemy, undoName);
        }
    }
}
