using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyController))]
[CanEditMultipleObjects]
public class EnemyControllerEditor : Editor
{
    private static bool editPatrolPoints;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Death/Respawn: Boss death is permanent. Normal enemies revive only when MC saves at a camp or respawns from a camp. Hazard respawn does not revive enemies.",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            "Room State: Enemies should stay under a Room parent. When MC leaves the room, the room disables its children, so enemies become inactive with that room.",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            "Patrol Points: Enable scene editing, then left-click in the Scene view to add patrol points. No point GameObjects are created.",
            MessageType.Info);

        editPatrolPoints = GUILayout.Toggle(editPatrolPoints, "Edit Patrol Points In Scene", "Button");

        if (GUILayout.Button("Clear Patrol Points"))
        {
            for (int i = 0; i < targets.Length; i++)
            {
                EnemyController enemy = (EnemyController)targets[i];
                Undo.RecordObject(enemy, "Clear Enemy Patrol Points");
                enemy.ClearPatrolPoints();
                EditorUtility.SetDirty(enemy);
            }
        }
    }

    private void OnSceneGUI()
    {
        EnemyController enemy = (EnemyController)target;
        DrawPatrolPoints(enemy);

        if (!editPatrolPoints)
        {
            return;
        }

        Handles.color = Color.cyan;
        Handles.Label(enemy.transform.position + Vector3.up * 1.2f, "Left-click to add patrol point");

        Event current = Event.current;
        if (current == null || current.type != EventType.MouseDown || current.button != 0 || current.alt)
        {
            return;
        }

        Vector3 worldPosition = GetMouseWorldPosition(current.mousePosition);
        Undo.RecordObject(enemy, "Add Enemy Patrol Point");
        enemy.AddPatrolPoint(new Vector2(worldPosition.x, worldPosition.y));
        EditorUtility.SetDirty(enemy);
        current.Use();
    }

    private static void DrawPatrolPoints(EnemyController enemy)
    {
        IReadOnlyList<Vector2> points = enemy.PatrolPoints;
        Handles.color = Color.cyan;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 point = points[i];
            float handleSize = HandleUtility.GetHandleSize(point) * 0.1f;

            EditorGUI.BeginChangeCheck();
            Vector3 movedPoint = Handles.FreeMoveHandle(point, Quaternion.identity, handleSize, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(enemy, "Move Enemy Patrol Point");
                enemy.SetPatrolPoint(i, new Vector2(movedPoint.x, movedPoint.y));
                EditorUtility.SetDirty(enemy);
            }

            Handles.Label(point + Vector3.up * handleSize, string.Format("P{0}", i + 1));
            if (i > 0)
            {
                Handles.DrawLine(points[i - 1], point);
            }
        }
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
}
