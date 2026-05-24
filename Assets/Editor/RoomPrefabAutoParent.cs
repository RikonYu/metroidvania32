using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RoomPrefabAutoParent
{
    private const float RestorePositionEpsilon = 0.0001f;

    private static bool autoParentQueued;
    private static bool restoreQueued;
    private static readonly Dictionary<int, PendingWorldRestore> pendingWorldRestores = new Dictionary<int, PendingWorldRestore>();

    static RoomPrefabAutoParent()
    {
        EditorApplication.hierarchyChanged += QueueAutoParent;
    }

    private static void QueueAutoParent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || autoParentQueued || Utils.IsInPrefabMode())
        {
            return;
        }

        autoParentQueued = true;
        EditorApplication.delayCall += AutoParentColliderObjects;
    }

    private static void AutoParentColliderObjects()
    {
        autoParentQueued = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode || Utils.IsInPrefabMode())
        {
            return;
        }

        Dictionary<Transform, Bounds> targetBounds = new Dictionary<Transform, Bounds>();
        Collider2D[] colliders = Resources.FindObjectsOfTypeAll<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Transform target = GetAutoParentTarget(colliders[i]);
            if (target == null)
            {
                continue;
            }

            AddTargetBounds(targetBounds, target, colliders[i].bounds);
        }

        foreach (KeyValuePair<Transform, Bounds> targetBound in targetBounds)
        {
            Transform target = targetBound.Key;
            if (target == null || target.GetComponentInParent<Room>(true) != null)
            {
                continue;
            }

            Room room = FindRoomForTarget(target, targetBound.Value);
            if (room != null)
            {
                ParentToRoom(target, room);
            }
        }
    }

    private static Transform GetAutoParentTarget(Collider2D collider2D)
    {
        if (collider2D == null || EditorUtility.IsPersistent(collider2D))
        {
            return null;
        }

        Transform target = GetPrefabInstanceRoot(collider2D);
        if (!Utils.IsSceneTransform(target) || target.GetComponentInParent<Room>(true) != null || ShouldSkip(target))
        {
            return null;
        }

        return target;
    }

    private static Transform GetPrefabInstanceRoot(Collider2D collider2D)
    {
        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(collider2D.gameObject);
        return prefabRoot != null ? prefabRoot.transform : null;
    }

    private static void AddTargetBounds(Dictionary<Transform, Bounds> targetBounds, Transform target, Bounds bounds)
    {
        if (targetBounds.TryGetValue(target, out Bounds combinedBounds))
        {
            combinedBounds.Encapsulate(bounds);
            targetBounds[target] = combinedBounds;
            return;
        }

        targetBounds.Add(target, bounds);
    }

    private static Room FindRoomForTarget(Transform target, Bounds bounds)
    {
        Room room = Utils.FindContainingRoom(target.position);
        return room != null ? room : FindRoomIntersectingBounds(bounds);
    }

    private static Room FindRoomIntersectingBounds(Bounds bounds)
    {
        Rect boundsRect = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        Room[] rooms = Resources.FindObjectsOfTypeAll<Room>();
        Room bestRoom = null;
        float bestOverlapArea = 0f;

        for (int i = 0; i < rooms.Length; i++)
        {
            Room room = rooms[i];
            if (!Utils.IsSceneRoom(room))
            {
                continue;
            }

            float overlapArea = GetOverlapArea(boundsRect, room.WorldRect);
            if (overlapArea > bestOverlapArea)
            {
                bestOverlapArea = overlapArea;
                bestRoom = room;
            }
        }

        return bestRoom;
    }

    private static float GetOverlapArea(Rect a, Rect b)
    {
        float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return overlapX > 0f && overlapY > 0f ? overlapX * overlapY : 0f;
    }

    private static bool ShouldSkip(Transform target)
    {
        return target.GetComponent<Room>() != null
            || target.GetComponentInParent<RoomManager>(true) != null
            || target.GetComponentInParent<GameController>(true) != null
            || target.GetComponentInParent<MCController>(true) != null
            || target.GetComponentInParent<PlayerRespawn>(true) != null
            || target.GetComponentInParent<CamParent>(true) != null
            || target.GetComponentInParent<Camera>(true) != null;
    }

    private static void ParentToRoom(Transform target, Room room)
    {
        const string undoName = "Auto Parent To Room";
        Scene scene = target.gameObject.scene;
        Vector3 worldPosition = target.position;
        Quaternion worldRotation = target.rotation;
        Vector3 preParentLocalPosition = target.localPosition;

        Undo.SetTransformParent(target, room.transform, undoName);
        Undo.RecordObject(target, undoName);
        RestoreWorldTransform(target, worldPosition, worldRotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(scene);
        QueueWorldRestoreGuard(target, room.transform, worldPosition, worldRotation, preParentLocalPosition, scene);
    }

    private static void RestoreWorldTransform(Transform target, Vector3 worldPosition, Quaternion worldRotation)
    {
        target.SetPositionAndRotation(worldPosition, worldRotation);
        if (target.parent != null)
        {
            target.localPosition = target.parent.InverseTransformPoint(worldPosition);
            target.localRotation = Quaternion.Inverse(target.parent.rotation) * worldRotation;
        }
    }

    private static void QueueWorldRestoreGuard(
        Transform target,
        Transform expectedParent,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Vector3 preParentLocalPosition,
        Scene scene)
    {
        pendingWorldRestores[target.GetInstanceID()] = new PendingWorldRestore(
            target,
            expectedParent,
            worldPosition,
            worldRotation,
            preParentLocalPosition,
            scene);

        if (restoreQueued)
        {
            return;
        }

        restoreQueued = true;
        EditorApplication.delayCall += RestorePendingWorldTransforms;
    }

    private static void RestorePendingWorldTransforms()
    {
        restoreQueued = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode || Utils.IsInPrefabMode())
        {
            pendingWorldRestores.Clear();
            return;
        }

        foreach (PendingWorldRestore pendingRestore in pendingWorldRestores.Values)
        {
            RestorePendingWorldTransform(pendingRestore);
        }

        pendingWorldRestores.Clear();
    }

    private static void RestorePendingWorldTransform(PendingWorldRestore pendingRestore)
    {
        Transform target = pendingRestore.Target;
        if (target == null || target.parent != pendingRestore.ExpectedParent)
        {
            return;
        }

        float worldDelta = (target.position - pendingRestore.WorldPosition).sqrMagnitude;
        if (worldDelta <= RestorePositionEpsilon * RestorePositionEpsilon)
        {
            return;
        }

        float preservedLocalDelta = (target.localPosition - pendingRestore.PreParentLocalPosition).sqrMagnitude;
        if (preservedLocalDelta > RestorePositionEpsilon * RestorePositionEpsilon)
        {
            return;
        }

        Undo.RecordObject(target, "Preserve Auto-Parented World Position");
        RestoreWorldTransform(target, pendingRestore.WorldPosition, pendingRestore.WorldRotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);

        if (pendingRestore.Scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(pendingRestore.Scene);
        }
    }

    private struct PendingWorldRestore
    {
        public PendingWorldRestore(
            Transform target,
            Transform expectedParent,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 preParentLocalPosition,
            Scene scene)
        {
            Target = target;
            ExpectedParent = expectedParent;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            PreParentLocalPosition = preParentLocalPosition;
            Scene = scene;
        }

        public Transform Target { get; private set; }
        public Transform ExpectedParent { get; private set; }
        public Vector3 WorldPosition { get; private set; }
        public Quaternion WorldRotation { get; private set; }
        public Vector3 PreParentLocalPosition { get; private set; }
        public Scene Scene { get; private set; }
    }
}
