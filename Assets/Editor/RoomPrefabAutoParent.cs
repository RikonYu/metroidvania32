using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RoomPrefabAutoParent
{
    private static bool autoParentQueued;

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

        Collider2D[] colliders = Resources.FindObjectsOfTypeAll<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Transform target = GetAutoParentTarget(colliders[i]);
            if (target == null)
            {
                continue;
            }

            Room room = Utils.FindContainingRoom(colliders[i].bounds.center);
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
        Scene scene = target.gameObject.scene;
        Undo.SetTransformParent(target, room.transform, "Auto Parent To Room");
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(scene);
    }

}
