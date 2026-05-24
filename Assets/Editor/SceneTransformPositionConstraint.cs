using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneTransformPositionConstraint
{
    private const float GridSize = 0.5f;
    private const float DefaultZ = 0f;
    private const float CameraZ = -10f;
    private const float PositionEpsilon = 0.000001f;

    private static bool snapAllQueued;
    private static bool snapSelectedQueued;
    private static bool isSnapping;

    static SceneTransformPositionConstraint()
    {
        SceneView.duringSceneGui += OnSceneGui;
        EditorApplication.hierarchyChanged += QueueSnapAll;
        Undo.postprocessModifications += OnPostprocessModifications;
        QueueSnapAll();
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Utils.IsInPrefabMode())
        {
            return;
        }

        Event current = Event.current;
        if (current != null && current.type == EventType.MouseUp && current.button == 0)
        {
            QueueSnapAll();
        }
    }

    private static UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
    {
        if (isSnapping || EditorApplication.isPlayingOrWillChangePlaymode || Utils.IsInPrefabMode())
        {
            return modifications;
        }

        for (int i = 0; i < modifications.Length; i++)
        {
            PropertyModification modification = modifications[i].currentValue;
            if (modification != null
                && modification.target is Transform
                && !string.IsNullOrEmpty(modification.propertyPath)
                && modification.propertyPath.StartsWith("m_LocalPosition"))
            {
                QueueSnapSelected();
                break;
            }
        }

        return modifications;
    }

    private static void QueueSnapAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || snapAllQueued || Utils.IsInPrefabMode())
        {
            return;
        }

        snapAllQueued = true;
        EditorApplication.delayCall += SnapAllSceneTransforms;
    }

    private static void QueueSnapSelected()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || snapSelectedQueued || Utils.IsInPrefabMode())
        {
            return;
        }

        snapSelectedQueued = true;
        EditorApplication.delayCall += SnapSelectedTransforms;
    }

    private static void SnapAllSceneTransforms()
    {
        snapAllQueued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode || Utils.IsInPrefabMode())
        {
            return;
        }

        SnapTransforms(Resources.FindObjectsOfTypeAll<Transform>(), "Constrain Scene Object Positions");
    }

    private static void SnapSelectedTransforms()
    {
        snapSelectedQueued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode || Utils.IsInPrefabMode())
        {
            return;
        }

        SnapTransforms(Selection.transforms, "Constrain Scene Object Position");
    }

    private static void SnapTransforms(IEnumerable<Transform> transforms, string undoName)
    {
        if (transforms == null)
        {
            return;
        }

        List<Transform> targets = CollectTargets(transforms);
        if (targets.Count == 0)
        {
            return;
        }

        isSnapping = true;
        HashSet<Scene> dirtyScenes = new HashSet<Scene>();
        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                SnapTransform(targets[i], undoName, dirtyScenes);
            }
        }
        finally
        {
            isSnapping = false;
        }

        foreach (Scene scene in dirtyScenes)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static List<Transform> CollectTargets(IEnumerable<Transform> transforms)
    {
        HashSet<Transform> seen = new HashSet<Transform>();
        List<Transform> targets = new List<Transform>();
        foreach (Transform transform in transforms)
        {
            Transform target = GetConstrainedTarget(transform);
            if (target == null || !seen.Add(target) || ShouldSkip(target))
            {
                continue;
            }

            targets.Add(target);
        }

        targets.Sort((a, b) => GetDepth(a).CompareTo(GetDepth(b)));
        return targets;
    }

    private static Transform GetConstrainedTarget(Transform source)
    {
        if (source == null)
        {
            return null;
        }

        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(source.gameObject);
        if (prefabRoot != null && prefabRoot.transform != source)
        {
            return null;
        }

        return source;
    }

    private static void SnapTransform(Transform target, string undoName, HashSet<Scene> dirtyScenes)
    {
        Vector3 before = target.position;
        Vector3 after = GetConstrainedPosition(target, before);
        if ((after - before).sqrMagnitude < PositionEpsilon)
        {
            return;
        }

        Undo.RecordObject(target, undoName);
        target.position = after;
        EditorUtility.SetDirty(target);
        dirtyScenes.Add(target.gameObject.scene);
    }

    private static Vector3 GetConstrainedPosition(Transform target, Vector3 position)
    {
        position.x = SnapToGrid(position.x);
        position.y = SnapToGrid(position.y);
        position.z = IsCameraTransform(target) ? CameraZ : DefaultZ;
        return position;
    }

    private static float SnapToGrid(float value)
    {
        return Mathf.Round(value / GridSize) * GridSize;
    }

    private static bool ShouldSkip(Transform target)
    {
        return !Utils.IsSceneTransform(target)
            || target.GetComponentInParent<MCController>(true) != null;
    }

    private static bool IsCameraTransform(Transform target)
    {
        return target.GetComponent<Camera>() != null
            || target.GetComponentInParent<CamParent>(true) != null;
    }

    private static int GetDepth(Transform target)
    {
        int depth = 0;
        while (target.parent != null)
        {
            depth++;
            target = target.parent;
        }

        return depth;
    }
}
