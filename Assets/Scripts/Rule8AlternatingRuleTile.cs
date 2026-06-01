using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Tiles/Rule 8 Alternating Rule Tile")]
public class Rule8AlternatingRuleTile : RuleTile
{
    [SerializeField] private Sprite rule8SpriteA;
    [SerializeField] private Sprite rule8SpriteB;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        if (rule8SpriteA == null || rule8SpriteB == null || tileData.sprite != rule8SpriteA)
        {
            return;
        }

        tileData.sprite = (position.x & 1) == 0 ? rule8SpriteA : rule8SpriteB;
    }
}
