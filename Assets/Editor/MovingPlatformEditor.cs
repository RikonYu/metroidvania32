using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MovingPlatform), true)]
[CanEditMultipleObjects]
public class MovingPlatformEditor : Editor
{
    private const float HandleSizeScale = 0.12f;

    private static bool editPathPoints;
    private static int selectedPointIndex = -1;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        editPathPoints = GUILayout.Toggle(editPathPoints, "Edit Path Points In Scene", "Button");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Point At Platform"))
        {
            AddPathPoint(((MovingPlatform)target).transform.position);
        }

        if (GUILayout.Button("Clear Path Points"))
        {
            ClearPathPoints();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Scene editing: Shift-click to add a snapped path point. Shift+Ctrl-click adds a free path point. Drag point handles to move them. Click a point label, then press Delete or Backspace to remove it.",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        MovingPlatform platform = (MovingPlatform)target;
        DrawPath(platform);

        if (!editPathPoints)
        {
            return;
        }

        ReserveSceneInput();
        DrawEditablePathPoints(platform);
        HandleSceneInput(platform);
    }

    private void ReserveSceneInput()
    {
        Event current = Event.current;
        if (current == null || current.alt)
        {
            return;
        }

        if (current.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }

    private void DrawEditablePathPoints(MovingPlatform platform)
    {
        for (int i = 0; i < platform.PathPoints.Count; i++)
        {
            Vector3 point = platform.PathPoints[i];
            float handleSize = HandleUtility.GetHandleSize(point) * HandleSizeScale;

            Handles.color = selectedPointIndex == i ? Color.yellow : Color.magenta;
            if (Handles.Button(point + Vector3.up * handleSize * 1.5f, Quaternion.identity, handleSize * 0.5f, handleSize * 0.7f, Handles.DotHandleCap))
            {
                selectedPointIndex = i;
                SceneView.RepaintAll();
            }

            EditorGUI.BeginChangeCheck();
            Vector3 movedPoint = Handles.FreeMoveHandle(point, Quaternion.identity, handleSize, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(platform, "Move Moving Platform Path Point");
                selectedPointIndex = i;
                platform.SetPathPoint(i, new Vector2(movedPoint.x, movedPoint.y));
                EditorUtility.SetDirty(platform);
            }

            Handles.Label(point + Vector3.up * handleSize * 2f, string.Format("P{0}", i + 1));
        }

        Handles.color = Color.magenta;
        Handles.Label(platform.transform.position + Vector3.up * HandleUtility.GetHandleSize(platform.transform.position) * 0.2f, "Shift-click: snapped point / Shift+Ctrl-click: free point");
    }

    private void HandleSceneInput(MovingPlatform platform)
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
                worldPosition = SnapToPreviousPathPoint(platform, worldPosition);
            }

            AddPathPoint(worldPosition);
            current.Use();
            return;
        }

        if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace))
        {
            DeleteSelectedPathPoint(platform);
            current.Use();
        }
    }

    private void DrawPath(MovingPlatform platform)
    {
        int pointCount = GetPointCount(platform);
        if (pointCount <= 0)
        {
            return;
        }

        Handles.color = Color.magenta;
        Vector3 previous = GetPoint(platform, 0);
        DrawPathPoint(platform, previous, 0, platform.IncludeInitialPosition);

        for (int i = 1; i < pointCount; i++)
        {
            Vector3 point = GetPoint(platform, i);
            Handles.DrawLine(previous, point);
            DrawPathPoint(platform, point, i, false);
            previous = point;
        }

        if (platform.PathMode == MovingPlatformPathMode.Loop && pointCount > 2)
        {
            Handles.DrawLine(GetPoint(platform, pointCount - 1), GetPoint(platform, 0));
        }
    }

    private void DrawPathPoint(MovingPlatform platform, Vector3 point, int index, bool isInitialPoint)
    {
        float handleSize = HandleUtility.GetHandleSize(point) * HandleSizeScale;
        Handles.DrawWireDisc(point, Vector3.forward, handleSize);
        string label = isInitialPoint ? "Start" : string.Format("P{0}", GetPathPointIndex(platform, index) + 1);
        Handles.Label(point + Vector3.up * handleSize, label);
    }

    private int GetPointCount(MovingPlatform platform)
    {
        return platform.PathPoints.Count + (platform.IncludeInitialPosition ? 1 : 0);
    }

    private Vector3 GetPoint(MovingPlatform platform, int index)
    {
        if (platform.IncludeInitialPosition)
        {
            if (index == 0)
            {
                return platform.transform.position;
            }

            return platform.PathPoints[index - 1];
        }

        return platform.PathPoints[index];
    }

    private int GetPathPointIndex(MovingPlatform platform, int drawnPointIndex)
    {
        return platform.IncludeInitialPosition ? drawnPointIndex - 1 : drawnPointIndex;
    }

    private void AddPathPoint(Vector3 worldPosition)
    {
        MovingPlatform platform = (MovingPlatform)target;
        Undo.RecordObject(platform, "Add Moving Platform Path Point");
        int newIndex = platform.PathPoints.Count;
        platform.AddPathPoint(new Vector2(worldPosition.x, worldPosition.y));
        selectedPointIndex = newIndex;
        EditorUtility.SetDirty(platform);
        SceneView.RepaintAll();
    }

    private Vector3 SnapToPreviousPathPoint(MovingPlatform platform, Vector3 worldPosition)
    {
        Vector3 previousPoint = GetPreviousPathPoint(platform);
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

    private Vector3 GetPreviousPathPoint(MovingPlatform platform)
    {
        if (platform.PathPoints.Count <= 0)
        {
            return platform.transform.position;
        }

        return platform.PathPoints[platform.PathPoints.Count - 1];
    }

    private void DeleteSelectedPathPoint(MovingPlatform platform)
    {
        if (selectedPointIndex < 0 || selectedPointIndex >= platform.PathPoints.Count)
        {
            return;
        }

        Undo.RecordObject(platform, "Delete Moving Platform Path Point");
        platform.RemovePathPointAt(selectedPointIndex);
        selectedPointIndex = Mathf.Clamp(selectedPointIndex - 1, -1, platform.PathPoints.Count - 1);
        EditorUtility.SetDirty(platform);
        SceneView.RepaintAll();
    }

    private void ClearPathPoints()
    {
        MovingPlatform platform = (MovingPlatform)target;
        Undo.RecordObject(platform, "Clear Moving Platform Path Points");
        platform.ClearPathPoints();
        selectedPointIndex = -1;
        EditorUtility.SetDirty(platform);
        SceneView.RepaintAll();
    }
}
