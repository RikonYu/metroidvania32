using UnityEditor;

[CustomEditor(typeof(WaterChargeAI), true)]
[CanEditMultipleObjects]
public class WaterChargeAIEditor : Editor
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
    private SerializedProperty waterTargetOverrideProperty;
    private SerializedProperty waterTargetSearchIntervalProperty;
    private SerializedProperty waterFacingDirectionProperty;
    private SerializedProperty waterViewDistanceProperty;
    private SerializedProperty waterViewHalfAngleProperty;
    private SerializedProperty visionBlockMaskProperty;
    private SerializedProperty patrolCenterProperty;
    private SerializedProperty patrolRadiusProperty;
    private SerializedProperty useInitialPositionAsPatrolCenterProperty;
    private SerializedProperty waterPatrolPointReachDistanceProperty;
    private SerializedProperty wanderSpeedProperty;
    private SerializedProperty wanderPauseMinProperty;
    private SerializedProperty wanderPauseMaxProperty;
    private SerializedProperty alertPauseDurationProperty;
    private SerializedProperty chargeSpeedProperty;
    private SerializedProperty maxChargeDistanceProperty;
    private SerializedProperty blockedPauseDurationProperty;
    private SerializedProperty terrainStopMaskProperty;
    private SerializedProperty recoverSpeedProperty;
    private SerializedProperty recoverReachDistanceProperty;
    private SerializedProperty waterStateProperty;

    private void OnEnable()
    {
        scriptProperty = serializedObject.FindProperty("m_Script");
        waterTargetOverrideProperty = serializedObject.FindProperty("waterTargetOverride");
        waterTargetSearchIntervalProperty = serializedObject.FindProperty("waterTargetSearchInterval");
        waterFacingDirectionProperty = serializedObject.FindProperty("waterFacingDirection");
        waterViewDistanceProperty = serializedObject.FindProperty("waterViewDistance");
        waterViewHalfAngleProperty = serializedObject.FindProperty("waterViewHalfAngle");
        visionBlockMaskProperty = serializedObject.FindProperty("visionBlockMask");
        patrolCenterProperty = serializedObject.FindProperty("patrolCenter");
        patrolRadiusProperty = serializedObject.FindProperty("patrolRadius");
        useInitialPositionAsPatrolCenterProperty = serializedObject.FindProperty("useInitialPositionAsPatrolCenter");
        waterPatrolPointReachDistanceProperty = serializedObject.FindProperty("waterPatrolPointReachDistance");
        wanderSpeedProperty = serializedObject.FindProperty("wanderSpeed");
        wanderPauseMinProperty = serializedObject.FindProperty("wanderPauseMin");
        wanderPauseMaxProperty = serializedObject.FindProperty("wanderPauseMax");
        alertPauseDurationProperty = serializedObject.FindProperty("alertPauseDuration");
        chargeSpeedProperty = serializedObject.FindProperty("chargeSpeed");
        maxChargeDistanceProperty = serializedObject.FindProperty("maxChargeDistance");
        blockedPauseDurationProperty = serializedObject.FindProperty("blockedPauseDuration");
        terrainStopMaskProperty = serializedObject.FindProperty("terrainStopMask");
        recoverSpeedProperty = serializedObject.FindProperty("recoverSpeed");
        recoverReachDistanceProperty = serializedObject.FindProperty("recoverReachDistance");
        waterStateProperty = serializedObject.FindProperty("waterState");
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
        EditorGUILayout.PropertyField(waterTargetOverrideProperty);
        EditorGUILayout.PropertyField(waterTargetSearchIntervalProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Vision", EditorStyles.boldLabel);
        DrawFacingDirection();
        EditorGUILayout.PropertyField(waterViewDistanceProperty);
        EditorGUILayout.PropertyField(waterViewHalfAngleProperty);
        EditorGUILayout.PropertyField(visionBlockMaskProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Patrol Area", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(patrolCenterProperty);
        EditorGUILayout.PropertyField(patrolRadiusProperty);
        EditorGUILayout.PropertyField(useInitialPositionAsPatrolCenterProperty);
        EditorGUILayout.PropertyField(waterPatrolPointReachDistanceProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Wander", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(wanderSpeedProperty);
        EditorGUILayout.PropertyField(wanderPauseMinProperty);
        EditorGUILayout.PropertyField(wanderPauseMaxProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Charge", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(alertPauseDurationProperty);
        EditorGUILayout.PropertyField(chargeSpeedProperty);
        EditorGUILayout.PropertyField(maxChargeDistanceProperty);
        EditorGUILayout.PropertyField(blockedPauseDurationProperty);
        EditorGUILayout.PropertyField(terrainStopMaskProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recover", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(recoverSpeedProperty);
        EditorGUILayout.PropertyField(recoverReachDistanceProperty);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(waterStateProperty);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFacingDirection()
    {
        EditorGUI.showMixedValue = waterFacingDirectionProperty.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int nextDirection = EditorGUILayout.IntPopup(
            "Facing Direction",
            GameDirection.NormalizeOrDefault(waterFacingDirectionProperty.intValue, GameDirection.Left),
            DirectionLabels,
            DirectionValues);

        if (EditorGUI.EndChangeCheck())
        {
            waterFacingDirectionProperty.intValue = nextDirection;
        }

        EditorGUI.showMixedValue = false;
    }
}
