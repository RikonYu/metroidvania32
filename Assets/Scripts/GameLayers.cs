using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class GameLayers
{
    public const string Player = "player";
    public const string Ground = "ground";
    public const string Obstacle = "obstacle";
    public const string Platform = "platform";
    public const string Trigger = "trigger";
    public const string Enemy = "enemy";
    public const string Hazard = "hazard";
    public const string Water = "Water";
    public const string PlayerBullet = "PlayerBullet";
    public const string EnemyBullet = "EnemyBullet";

    public static void ApplyTo(GameObject target, string layerName)
    {
        if (target == null)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0 && target.layer != layer)
        {
            target.layer = layer;
        }
    }

    public static void ApplyToAfterValidation(GameObject target, string layerName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () => ApplyTo(target, layerName);
            return;
        }
#endif

        ApplyTo(target, layerName);
    }
}
