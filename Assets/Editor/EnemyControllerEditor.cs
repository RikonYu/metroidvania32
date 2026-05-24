using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyController))]
[CanEditMultipleObjects]
public class EnemyControllerEditor : Editor
{
    private const float HandleSizeScale = 0.12f;

    private static bool editPatrolPoints;
    private static int selectedPointIndex = -1;

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
            "Patrol Points: Enable scene editing, then Shift-click to add a snapped point. Shift+Ctrl-click adds a free point. Drag point handles to move them. Click a point label, then press Delete or Backspace to remove it. No point GameObjects are created.",
            MessageType.Info);

        editPatrolPoints = GUILayout.Toggle(editPatrolPoints, "Edit Patrol Points In Scene", "Button");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Point At Enemy"))
        {
            AddPatrolPoint(((EnemyController)target).transform.position);
        }

        if (GUILayout.Button("Clear Patrol Points"))
        {
            ClearPatrolPoints();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void OnSceneGUI()
    {
        EnemyController enemy = (EnemyController)target;
        DrawPatrolPath(enemy);

        if (!editPatrolPoints)
        {
            return;
        }

        DrawEditablePatrolPoints(enemy);
        HandleSceneInput(enemy);
    }

    private void DrawEditablePatrolPoints(EnemyController enemy)
    {
        for (int i = 0; i < enemy.PatrolPoints.Count; i++)
        {
            Vector3 point = enemy.PatrolPoints[i];
            float handleSize = HandleUtility.GetHandleSize(point) * HandleSizeScale;

            Handles.color = selectedPointIndex == i ? Color.yellow : Color.cyan;
            if (Handles.Button(point + Vector3.up * handleSize * 1.5f, Quaternion.identity, handleSize * 0.5f, handleSize * 0.7f, Handles.DotHandleCap))
            {
                selectedPointIndex = i;
                SceneView.RepaintAll();
            }

            EditorGUI.BeginChangeCheck();
            Vector3 movedPoint = Handles.FreeMoveHandle(point, Quaternion.identity, handleSize, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(enemy, "Move Enemy Patrol Point");
                selectedPointIndex = i;
                enemy.SetPatrolPoint(i, new Vector2(movedPoint.x, movedPoint.y));
                EditorUtility.SetDirty(enemy);
            }

            Handles.Label(point + Vector3.up * handleSize * 2f, string.Format("P{0}", i + 1));
        }

        Handles.color = Color.cyan;
        Handles.Label(enemy.transform.position + Vector3.up * HandleUtility.GetHandleSize(enemy.transform.position) * 0.2f, "Shift-click: snapped point / Shift+Ctrl-click: free point");
    }

    private void HandleSceneInput(EnemyController enemy)
    {
        Event current = Event.current;
        if (current == null || current.alt)
        {
            return;
        }

        if (current.type == EventType.MouseDown && current.button == 0 && current.shift)
        {
            Vector3 worldPosition = Utils.GetMouseWorldPosition(current.mousePosition);
            if (!current.control)
            {
                worldPosition = SnapToPreviousPatrolPoint(enemy, worldPosition);
            }

            AddPatrolPoint(worldPosition);
            current.Use();
            return;
        }

        if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace))
        {
            DeleteSelectedPatrolPoint(enemy);
            current.Use();
        }
    }

    private void DrawPatrolPath(EnemyController enemy)
    {
        int pointCount = enemy.PatrolPoints.Count;
        if (pointCount <= 0)
        {
            return;
        }

        Handles.color = Color.cyan;
        Vector3 previous = enemy.transform.position;
        DrawPathPoint(previous, 0, true);

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 point = enemy.PatrolPoints[i];
            Handles.DrawLine(previous, point);
            DrawPathPoint(point, i + 1, false);
            previous = point;
        }
    }

    private static void DrawPathPoint(Vector3 point, int index, bool isInitialPoint)
    {
        float handleSize = HandleUtility.GetHandleSize(point) * HandleSizeScale;
        Handles.DrawWireDisc(point, Vector3.forward, handleSize);
        string label = isInitialPoint ? "Start" : string.Format("P{0}", index);
        Handles.Label(point + Vector3.up * handleSize, label);
    }

    private void AddPatrolPoint(Vector3 worldPosition)
    {
        EnemyController enemy = (EnemyController)target;
        Undo.RecordObject(enemy, "Add Enemy Patrol Point");
        selectedPointIndex = enemy.PatrolPoints.Count;
        enemy.AddPatrolPoint(new Vector2(worldPosition.x, worldPosition.y));
        EditorUtility.SetDirty(enemy);
        SceneView.RepaintAll();
    }

    private Vector3 SnapToPreviousPatrolPoint(EnemyController enemy, Vector3 worldPosition)
    {
        Vector3 previousPoint = GetPreviousPatrolPoint(enemy);
        float xDistance = Mathf.Abs(worldPosition.x - previousPoint.x);
        float yDistance = Mathf.Abs(worldPosition.y - previousPoint.y);

        if (xDistance <= yDistance)
        {
            worldPosition.x = previousPoint.x;
        }
        else
        {
            worldPosition.y = previousPoint.y;
        }

        return worldPosition;
    }

    private Vector3 GetPreviousPatrolPoint(EnemyController enemy)
    {
        if (enemy.PatrolPoints.Count <= 0)
        {
            return enemy.transform.position;
        }

        return enemy.PatrolPoints[enemy.PatrolPoints.Count - 1];
    }

    private void DeleteSelectedPatrolPoint(EnemyController enemy)
    {
        if (selectedPointIndex < 0 || selectedPointIndex >= enemy.PatrolPoints.Count)
        {
            return;
        }

        Undo.RecordObject(enemy, "Delete Enemy Patrol Point");
        enemy.RemovePatrolPointAt(selectedPointIndex);
        selectedPointIndex = Mathf.Clamp(selectedPointIndex - 1, -1, enemy.PatrolPoints.Count - 1);
        EditorUtility.SetDirty(enemy);
        SceneView.RepaintAll();
    }

    private void ClearPatrolPoints()
    {
        EnemyController enemy = (EnemyController)target;
        Undo.RecordObject(enemy, "Clear Enemy Patrol Points");
        enemy.ClearPatrolPoints();
        selectedPointIndex = -1;
        EditorUtility.SetDirty(enemy);
        SceneView.RepaintAll();
    }
}
