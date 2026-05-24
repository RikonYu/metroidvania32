using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StaticEnemyAI))]
[CanEditMultipleObjects]
public class StaticEnemyAIEditor : Editor
{
    private static readonly string[] DirectionLabels =
    {
        "Down (2)",
        "Left (4)",
        "Right (6)",
        "Up (8)"
    };

    private static readonly int[] DirectionValues =
    {
        GameDirection.Down,
        GameDirection.Left,
        GameDirection.Right,
        GameDirection.Up
    };

    private SerializedProperty scriptProperty;
    private SerializedProperty fireDirectionProperty;
    private SerializedProperty firstHitDelayProperty;
    private SerializedProperty fireIntervalProperty;

    private void OnEnable()
    {
        scriptProperty = serializedObject.FindProperty("m_Script");
        fireDirectionProperty = serializedObject.FindProperty("fireDirection");
        firstHitDelayProperty = serializedObject.FindProperty("firstHitDelay");
        fireIntervalProperty = serializedObject.FindProperty("fireInterval");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(scriptProperty);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Static Fire", EditorStyles.boldLabel);
        DrawFireDirection();
        EditorGUILayout.PropertyField(firstHitDelayProperty);
        EditorGUILayout.PropertyField(fireIntervalProperty);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFireDirection()
    {
        EditorGUI.showMixedValue = fireDirectionProperty.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int nextDirection = EditorGUILayout.IntPopup(
            "Fire Direction",
            GameDirection.NormalizeOrDefault(fireDirectionProperty.intValue, GameDirection.Left),
            DirectionLabels,
            DirectionValues);

        if (EditorGUI.EndChangeCheck())
        {
            fireDirectionProperty.intValue = nextDirection;
        }

        EditorGUI.showMixedValue = false;
    }
}
