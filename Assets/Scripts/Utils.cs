using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public static class Utils
{
    private const float VectorEpsilon = 0.0001f;
    private const float RectOverlapEpsilon = 0.0001f;

    public static Vector2 NormalizeOrZero(Vector2 vector)
    {
        return vector.sqrMagnitude <= VectorEpsilon ? Vector2.zero : vector.normalized;
    }

    public static Vector2 NormalizeOrFallback(Vector2 vector, Vector2 fallback)
    {
        if (vector.sqrMagnitude > VectorEpsilon)
        {
            return vector.normalized;
        }

        return fallback.sqrMagnitude > VectorEpsilon ? fallback.normalized : Vector2.zero;
    }

    public static int GetCardinalDirectionFromVector(Vector2 vector)
    {
        if (Mathf.Abs(vector.x) >= Mathf.Abs(vector.y))
        {
            return vector.x < 0f ? GameDirection.Left : GameDirection.Right;
        }

        return vector.y < 0f ? GameDirection.Down : GameDirection.Up;
    }

    public static bool IsLayer(int layer, string layerName)
    {
        return layer == LayerMask.NameToLayer(layerName);
    }

    public static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    public static bool IsColliderOnMask(Collider2D collider2D, LayerMask mask)
    {
        return collider2D != null && IsLayerInMask(collider2D.gameObject.layer, mask);
    }

    public static bool IsTerrainLayer(int layer)
    {
        return IsLayer(layer, GameLayers.Ground)
            || IsLayer(layer, GameLayers.Obstacle)
            || IsLayer(layer, GameLayers.Platform);
    }

    public static bool IsTerrain(Collider2D other)
    {
        return other != null && IsTerrainLayer(other.gameObject.layer);
    }

    public static WaterZone GetWaterZone(Collider2D other)
    {
        return other != null ? other.GetComponentInParent<WaterZone>() : null;
    }

    public static EnemyController GetEnemyTarget(Collider2D other)
    {
        return other != null ? other.GetComponentInParent<EnemyController>() : null;
    }

    public static PlayerRespawn GetPlayerTarget(Collider2D other)
    {
        return other != null ? other.GetComponentInParent<PlayerRespawn>() : null;
    }

    public static bool IsEnemyCollider(Collider2D collider2D)
    {
        return collider2D != null
            && (IsLayer(collider2D.gameObject.layer, GameLayers.Enemy)
                || GetEnemyTarget(collider2D) != null);
    }

    public static bool IsPoisonousWater(Collider2D other)
    {
        WaterZone waterZone = GetWaterZone(other);
        return waterZone != null && waterZone.IsPoisonous;
    }

    public static bool IsSceneInstance(GameObject gameObject)
    {
        return gameObject != null && gameObject.scene.IsValid();
    }

    public static bool RectsOverlapByArea(Rect a, Rect b)
    {
        float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return overlapX > RectOverlapEpsilon && overlapY > RectOverlapEpsilon;
    }

    public static void RestoreHealthBottles()
    {
        if (GameController.Instance != null)
        {
            GameController.Instance.RestoreHealthBottlesToFull();
        }
    }

#if UNITY_EDITOR
    private const float GroundSnapSearchDistance = 64f;
    private const float GroundSnapRayStartOffset = 0.1f;
    private const float GroundSnapEpsilon = 0.0001f;

    public static bool SnapToGround(Component target, string undoName)
    {
        if (!IsSceneComponent(target))
        {
            return false;
        }

        Collider2D targetCollider = target.GetComponent<Collider2D>();
        if (!TryGetSnappedPosition(target.transform, targetCollider, out Vector3 snappedPosition))
        {
            return false;
        }

        Transform targetTransform = target.transform;
        if ((snappedPosition - targetTransform.position).sqrMagnitude < GroundSnapEpsilon * GroundSnapEpsilon)
        {
            return false;
        }

        Scene scene = target.gameObject.scene;
        Undo.RecordObject(targetTransform, undoName);
        targetTransform.position = snappedPosition;
        EditorUtility.SetDirty(targetTransform);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    public static bool TryGetSnappedPosition(Transform target, Collider2D targetCollider, out Vector3 snappedPosition)
    {
        snappedPosition = target != null ? target.position : Vector3.zero;

        int groundLayer = LayerMask.NameToLayer(GameLayers.Ground);
        if (target == null || targetCollider == null || groundLayer < 0 || !IsSceneTransform(target))
        {
            return false;
        }

        Bounds targetBounds = targetCollider.bounds;
        if (TryFindGroundYByRaycast(target, targetBounds, groundLayer, out float groundY)
            || TryFindGroundYByBounds(target, targetBounds, groundLayer, out groundY))
        {
            snappedPosition.y += groundY - targetBounds.min.y;
            return true;
        }

        return false;
    }

    public static Room FindContainingRoom(Vector3 worldPosition, bool excludePrefabStage = true)
    {
        Room[] rooms = Resources.FindObjectsOfTypeAll<Room>();
        Room bestRoom = null;
        float bestArea = float.PositiveInfinity;
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);

        for (int i = 0; i < rooms.Length; i++)
        {
            Room room = rooms[i];
            if (!IsSceneRoom(room, excludePrefabStage))
            {
                continue;
            }

            Rect rect = room.WorldRect;
            if (!rect.Contains(point))
            {
                continue;
            }

            float area = rect.width * rect.height;
            if (area < bestArea)
            {
                bestArea = area;
                bestRoom = room;
            }
        }

        return bestRoom;
    }

    public static Vector3 GetMouseWorldPosition(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        if (Mathf.Abs(ray.direction.z) < VectorEpsilon)
        {
            return ray.origin;
        }

        float distance = -ray.origin.z / ray.direction.z;
        return ray.GetPoint(distance);
    }

    public static bool IsSceneRoom(Room room, bool excludePrefabStage = true)
    {
        return room != null
            && !EditorUtility.IsPersistent(room)
            && room.gameObject.scene.IsValid()
            && (!excludePrefabStage || !IsPrefabStageScene(room.gameObject.scene));
    }

    public static bool IsSceneTransform(Transform target, bool excludePrefabStage = true)
    {
        return target != null
            && !EditorUtility.IsPersistent(target)
            && target.gameObject.scene.IsValid()
            && (!excludePrefabStage || !IsPrefabStageScene(target.gameObject.scene));
    }

    public static bool IsInPrefabMode()
    {
        return PrefabStageUtility.GetCurrentPrefabStage() != null;
    }

    public static bool IsPrefabStageScene(Scene scene)
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        return prefabStage != null && prefabStage.scene == scene;
    }

    private static bool TryFindGroundYByRaycast(Transform target, Bounds targetBounds, int groundLayer, out float groundY)
    {
        groundY = 0f;

        Vector2 origin = new Vector2(targetBounds.center.x, targetBounds.max.y + GroundSnapRayStartOffset);
        float distance = targetBounds.size.y + GroundSnapSearchDistance + GroundSnapRayStartOffset;
        int mask = 1 << groundLayer;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, distance, mask);

        bool found = false;
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null
                || hit.normal.y <= 0f
                || hit.point.y > targetBounds.max.y + GroundSnapEpsilon
                || !IsSceneGround(hit.collider, target.gameObject.scene, groundLayer)
                || hit.collider.transform.IsChildOf(target))
            {
                continue;
            }

            if (!found || hit.point.y > bestY)
            {
                bestY = hit.point.y;
                found = true;
            }
        }

        groundY = bestY;
        return found;
    }

    private static bool TryFindGroundYByBounds(Transform target, Bounds targetBounds, int groundLayer, out float groundY)
    {
        groundY = 0f;

        Collider2D[] colliders = Resources.FindObjectsOfTypeAll<Collider2D>();
        bool found = false;
        float bestY = float.NegativeInfinity;
        float targetX = targetBounds.center.x;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D ground = colliders[i];
            if (!IsSceneGround(ground, target.gameObject.scene, groundLayer)
                || ground.transform.IsChildOf(target))
            {
                continue;
            }

            Bounds groundBounds = ground.bounds;
            if (groundBounds.size.sqrMagnitude <= 0f
                || targetX < groundBounds.min.x
                || targetX > groundBounds.max.x
                || groundBounds.max.y > targetBounds.max.y + GroundSnapEpsilon)
            {
                continue;
            }

            if (!found || groundBounds.max.y > bestY)
            {
                bestY = groundBounds.max.y;
                found = true;
            }
        }

        groundY = bestY;
        return found;
    }

    private static bool IsSceneComponent(Component component)
    {
        return component != null
            && !EditorUtility.IsPersistent(component)
            && IsSceneTransform(component.transform);
    }

    private static bool IsSceneGround(Collider2D collider2D, Scene scene, int groundLayer)
    {
        return collider2D != null
            && collider2D.gameObject.layer == groundLayer
            && !EditorUtility.IsPersistent(collider2D)
            && collider2D.gameObject.scene == scene
            && collider2D.gameObject.scene.IsValid()
            && !IsPrefabStageScene(collider2D.gameObject.scene);
    }
#endif
}
