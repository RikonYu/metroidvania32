using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Tiles/Rule 8 Alternating Rule Tile")]
public class Rule8AlternatingRuleTile : RuleTile
{
    [SerializeField] private Sprite rule8SpriteA;
    [SerializeField] private Sprite rule8SpriteB;

    private static bool isRefreshingCrossRoomNeighbors;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        if (rule8SpriteA == null || rule8SpriteB == null || tileData.sprite != rule8SpriteA)
        {
            return;
        }

        tileData.sprite = (position.x & 1) == 0 ? rule8SpriteA : rule8SpriteB;
    }

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        base.RefreshTile(position, tilemap);

        if (isRefreshingCrossRoomNeighbors)
        {
            return;
        }

        Tilemap sourceTilemap = tilemap.GetComponent<Tilemap>();
        if (sourceTilemap == null)
        {
            return;
        }

        isRefreshingCrossRoomNeighbors = true;
        try
        {
            foreach (Vector3Int offset in neighborPositions)
            {
                Vector3Int neighborCell = GetOffsetPositionReverse(position, offset);
                if (!RoomTilemapLookup.TryGetCrossRoomCell(sourceTilemap, neighborCell, out Tilemap targetTilemap, out Vector3Int targetCell))
                {
                    continue;
                }

                TileBase targetTile = targetTilemap.GetTile(targetCell);
                if (targetTile is RuleTile || targetTile is RuleOverrideTile)
                {
                    targetTilemap.RefreshTile(targetCell);
                }
            }
        }
        finally
        {
            isRefreshingCrossRoomNeighbors = false;
        }
    }

    public override bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, ref Matrix4x4 transform)
    {
        if (RuleMatchesAcrossRooms(rule, position, tilemap, 0))
        {
            transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 0f), Vector3.one);
            return true;
        }

        if (rule.m_RuleTransform == TilingRuleOutput.Transform.Rotated)
        {
            for (int angle = m_RotationAngle; angle < 360; angle += m_RotationAngle)
            {
                if (RuleMatchesAcrossRooms(rule, position, tilemap, angle))
                {
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, -angle), Vector3.one);
                    return true;
                }
            }
        }
        else if (rule.m_RuleTransform == TilingRuleOutput.Transform.MirrorXY)
        {
            if (RuleMatchesAcrossRooms(rule, position, tilemap, true, true))
            {
                transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1f, -1f, 1f));
                return true;
            }

            if (RuleMatchesAcrossRooms(rule, position, tilemap, true, false))
            {
                transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1f, 1f, 1f));
                return true;
            }

            if (RuleMatchesAcrossRooms(rule, position, tilemap, false, true))
            {
                transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1f, -1f, 1f));
                return true;
            }
        }
        else if (rule.m_RuleTransform == TilingRuleOutput.Transform.MirrorX)
        {
            if (RuleMatchesAcrossRooms(rule, position, tilemap, true, false))
            {
                transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1f, 1f, 1f));
                return true;
            }
        }
        else if (rule.m_RuleTransform == TilingRuleOutput.Transform.MirrorY)
        {
            if (RuleMatchesAcrossRooms(rule, position, tilemap, false, true))
            {
                transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1f, -1f, 1f));
                return true;
            }
        }

        return false;
    }

    private bool RuleMatchesAcrossRooms(TilingRule rule, Vector3Int position, ITilemap tilemap, int angle)
    {
        int minCount = Mathf.Min(rule.m_Neighbors.Count, rule.m_NeighborPositions.Count);
        for (int i = 0; i < minCount; i++)
        {
            int neighbor = rule.m_Neighbors[i];
            Vector3Int positionOffset = GetRotatedPosition(rule.m_NeighborPositions[i], angle);
            TileBase other = GetTileAcrossRooms(tilemap, GetOffsetPosition(position, positionOffset));
            if (!RuleMatch(neighbor, other))
            {
                return false;
            }
        }

        return true;
    }

    private bool RuleMatchesAcrossRooms(TilingRule rule, Vector3Int position, ITilemap tilemap, bool mirrorX, bool mirrorY)
    {
        int minCount = Mathf.Min(rule.m_Neighbors.Count, rule.m_NeighborPositions.Count);
        for (int i = 0; i < minCount; i++)
        {
            int neighbor = rule.m_Neighbors[i];
            Vector3Int positionOffset = GetMirroredPosition(rule.m_NeighborPositions[i], mirrorX, mirrorY);
            TileBase other = GetTileAcrossRooms(tilemap, GetOffsetPosition(position, positionOffset));
            if (!RuleMatch(neighbor, other))
            {
                return false;
            }
        }

        return true;
    }

    private static TileBase GetTileAcrossRooms(ITilemap tilemap, Vector3Int cellPosition)
    {
        TileBase localTile = tilemap.GetTile(cellPosition);
        if (localTile != null)
        {
            return localTile;
        }

        Tilemap sourceTilemap = tilemap.GetComponent<Tilemap>();
        if (RoomTilemapLookup.TryGetCrossRoomTile(sourceTilemap, cellPosition, out TileBase crossRoomTile))
        {
            return crossRoomTile;
        }

        return null;
    }
}

public static class RoomTilemapLookup
{
    private struct GroundTilemapEntry
    {
        public Tilemap Tilemap;
        public Rect WorldRect;
    }

    private static readonly System.Collections.Generic.List<GroundTilemapEntry> entries = new System.Collections.Generic.List<GroundTilemapEntry>();
    private static bool cacheDirty = true;

    public static void Invalidate()
    {
        cacheDirty = true;
    }

    public static bool TryGetCrossRoomTile(Tilemap sourceTilemap, Vector3Int sourceCell, out TileBase tile)
    {
        tile = null;
        if (!TryGetCrossRoomCell(sourceTilemap, sourceCell, out Tilemap targetTilemap, out Vector3Int targetCell))
        {
            return false;
        }

        tile = targetTilemap.GetTile(targetCell);
        return tile != null;
    }

    public static bool TryGetCrossRoomCell(Tilemap sourceTilemap, Vector3Int sourceCell, out Tilemap targetTilemap, out Vector3Int targetCell)
    {
        targetTilemap = null;
        targetCell = default;

        if (sourceTilemap == null)
        {
            return false;
        }

        EnsureCache();

        Vector3 worldCenter = sourceTilemap.GetCellCenterWorld(sourceCell);
        Vector2 worldPoint = new Vector2(worldCenter.x, worldCenter.y);
        for (int i = 0; i < entries.Count; i++)
        {
            GroundTilemapEntry entry = entries[i];
            if (entry.Tilemap == null || entry.Tilemap == sourceTilemap || !entry.WorldRect.Contains(worldPoint))
            {
                continue;
            }

            targetTilemap = entry.Tilemap;
            targetCell = targetTilemap.WorldToCell(worldCenter);
            return true;
        }

        return false;
    }

    private static void EnsureCache()
    {
        if (!cacheDirty)
        {
            return;
        }

        RebuildCache();
    }

    private static void RebuildCache()
    {
        entries.Clear();

        Room[] rooms = Resources.FindObjectsOfTypeAll<Room>();
        for (int i = 0; i < rooms.Length; i++)
        {
            Room room = rooms[i];
            if (!IsSceneRoom(room))
            {
                continue;
            }

            Tilemap groundTilemap = room.GroundTilemap;
            if (groundTilemap == null)
            {
                continue;
            }

            entries.Add(new GroundTilemapEntry
            {
                Tilemap = groundTilemap,
                WorldRect = room.WorldRect
            });
        }

        cacheDirty = false;
    }

    private static bool IsSceneRoom(Room room)
    {
        if (room == null || !room.gameObject.scene.IsValid())
        {
            return false;
        }

#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.IsPersistent(room) || IsPrefabStageScene(room.gameObject.scene))
        {
            return false;
        }
#endif

        return true;
    }

#if UNITY_EDITOR
    private static bool IsPrefabStageScene(UnityEngine.SceneManagement.Scene scene)
    {
        UnityEditor.SceneManagement.PrefabStage prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        return prefabStage != null && prefabStage.scene == scene;
    }
#endif
}
