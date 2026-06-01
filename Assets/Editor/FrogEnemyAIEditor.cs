using UnityEditor;

[CustomEditor(typeof(FrogEnemyAI))]
[CanEditMultipleObjects]
public class FrogEnemyAIEditor : Editor
{
    private static readonly string[] DirectionLabels =
    {
        "Left (4)",
        "Right (6)"
    };

    private static readonly int[] DirectionValues =
    {
        GameDirection.Left,
        GameDirection.Right
    };

    private SerializedProperty scriptProperty;
    private SerializedProperty frogTargetOverrideProperty;
    private SerializedProperty frogTargetSearchIntervalProperty;
    private SerializedProperty frogFacingDirectionProperty;
    private SerializedProperty frogViewDistanceProperty;
    private SerializedProperty frogViewHalfAngleProperty;
    private SerializedProperty visionBlockMaskProperty;
    private SerializedProperty patrolCenterProperty;
    private SerializedProperty patrolWidthProperty;
    private SerializedProperty useInitialPositionAsPatrolCenterProperty;
    private SerializedProperty maxJumpHeightProperty;
    private SerializedProperty jumpWindupProperty;
    private SerializedProperty landingPauseProperty;
    private SerializedProperty landingReachDistanceProperty;
    private SerializedProperty minimumAirTimeProperty;
    private SerializedProperty landingSupportMaskProperty;
    private SerializedProperty landingProbeUpOffsetProperty;
    private SerializedProperty landingProbeDownDistanceProperty;
    private SerializedProperty landingMinNormalYProperty;
    private SerializedProperty frogStateProperty;

    private void OnEnable()
    {
        scriptProperty = serializedObject.FindProperty("m_Script");
        frogTargetOverrideProperty = serializedObject.FindProperty("frogTargetOverride");
        frogTargetSearchIntervalProperty = serializedObject.FindProperty("frogTargetSearchInterval");
        frogFacingDirectionProperty = serializedObject.FindProperty("frogFacingDirection");
        frogViewDistanceProperty = serializedObject.FindProperty("frogViewDistance");
        frogViewHalfAngleProperty = serializedObject.FindProperty("frogViewHalfAngle");
        visionBlockMaskProperty = serializedObject.FindProperty("visionBlockMask");
        patrolCenterProperty = serializedObject.FindProperty("patrolCenter");
        patrolWidthProperty = serializedObject.FindProperty("patrolWidth");
        useInitialPositionAsPatrolCenterProperty = serializedObject.FindProperty("useInitialPositionAsPatrolCenter");
        maxJumpHeightProperty = serializedObject.FindProperty("maxJumpHeight");
        jumpWindupProperty = serializedObject.FindProperty("jumpWindup");
        landingPauseProperty = serializedObject.FindProperty("landingPause");
        landingReachDistanceProperty = serializedObject.FindProperty("landingReachDistance");
        minimumAirTimeProperty = serializedObject.FindProperty("minimumAirTime");
        landingSupportMaskProperty = serializedObject.FindProperty("landingSupportMask");
        landingProbeUpOffsetProperty = serializedObject.FindProperty("landingProbeUpOffset");
        landingProbeDownDistanceProperty = serializedObject.FindProperty("landingProbeDownDistance");
        landingMinNormalYProperty = serializedObject.FindProperty("landingMinNormalY");
        frogStateProperty = serializedObject.FindProperty("frogState");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(scriptProperty);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(frogTargetOverrideProperty);
        EditorGUILayout.PropertyField(frogTargetSearchIntervalProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Vision", EditorStyles.boldLabel);
        DrawFacingDirection();
        EditorGUILayout.PropertyField(frogViewDistanceProperty);
        EditorGUILayout.PropertyField(frogViewHalfAngleProperty);
        EditorGUILayout.PropertyField(visionBlockMaskProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Patrol Range", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(patrolCenterProperty);
        EditorGUILayout.PropertyField(patrolWidthProperty);
        EditorGUILayout.PropertyField(useInitialPositionAsPatrolCenterProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Jump", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maxJumpHeightProperty);
        EditorGUILayout.PropertyField(jumpWindupProperty);
        EditorGUILayout.PropertyField(landingPauseProperty);
        EditorGUILayout.PropertyField(landingReachDistanceProperty);
        EditorGUILayout.PropertyField(minimumAirTimeProperty);
        EditorGUILayout.PropertyField(landingSupportMaskProperty);
        EditorGUILayout.PropertyField(landingProbeUpOffsetProperty);
        EditorGUILayout.PropertyField(landingProbeDownDistanceProperty);
        EditorGUILayout.PropertyField(landingMinNormalYProperty);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(frogStateProperty);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFacingDirection()
    {
        EditorGUI.showMixedValue = frogFacingDirectionProperty.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int nextDirection = EditorGUILayout.IntPopup(
            "Facing Direction",
            frogFacingDirectionProperty.intValue == GameDirection.Right ? GameDirection.Right : GameDirection.Left,
            DirectionLabels,
            DirectionValues);

        if (EditorGUI.EndChangeCheck())
        {
            frogFacingDirectionProperty.intValue = nextDirection;
        }

        EditorGUI.showMixedValue = false;
    }
}
