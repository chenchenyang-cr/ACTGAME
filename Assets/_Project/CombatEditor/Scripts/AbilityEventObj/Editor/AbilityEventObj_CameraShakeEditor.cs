#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_CameraShake))]
    public sealed class AbilityEventObj_CameraShakeEditor : Editor
    {
        private SerializedProperty triggerMode;
        private SerializedProperty settings;
        private SerializedProperty hitShakeDuration;
        private SerializedProperty useUnscaledTime;
        private SerializedProperty hitBoxFilter;
        private SerializedProperty specificHitBox;
        private SerializedProperty triggerPolicy;
        private SerializedProperty acceptedResults;
        private SerializedProperty maximumTriggerCount;
        private SerializedProperty triggerCooldown;
        private SerializedProperty previewHitTime;
        private SerializedProperty previewHitIntensityScale;

        private SerializedProperty enablePosition;
        private SerializedProperty positionAmplitude;
        private SerializedProperty positionFrequency;
        private SerializedProperty positionCurve;
        private SerializedProperty positionSeed;
        private SerializedProperty enableRotation;
        private SerializedProperty rotationAmplitude;
        private SerializedProperty rotationFrequency;
        private SerializedProperty rotationCurve;
        private SerializedProperty rotationSeed;
        private SerializedProperty enableFov;
        private SerializedProperty fovAmplitude;
        private SerializedProperty fovCurve;

        private void OnEnable()
        {
            triggerMode = serializedObject.FindProperty("TriggerMode");
            settings = serializedObject.FindProperty("Settings");
            hitShakeDuration = serializedObject.FindProperty("HitShakeDuration");
            useUnscaledTime = serializedObject.FindProperty("UseUnscaledTime");
            hitBoxFilter = serializedObject.FindProperty("HitBoxFilter");
            specificHitBox = serializedObject.FindProperty("SpecificHitBox");
            triggerPolicy = serializedObject.FindProperty("TriggerPolicy");
            acceptedResults = serializedObject.FindProperty("AcceptedResults");
            maximumTriggerCount = serializedObject.FindProperty("MaximumTriggerCount");
            triggerCooldown = serializedObject.FindProperty("TriggerCooldown");
            previewHitTime = serializedObject.FindProperty("PreviewHitTime");
            previewHitIntensityScale = serializedObject.FindProperty("PreviewHitIntensityScale");

            enablePosition = settings.FindPropertyRelative("EnablePosition");
            positionAmplitude = settings.FindPropertyRelative("PositionAmplitude");
            positionFrequency = settings.FindPropertyRelative("PositionFrequency");
            positionCurve = settings.FindPropertyRelative("PositionCurve");
            positionSeed = settings.FindPropertyRelative("PositionSeed");
            enableRotation = settings.FindPropertyRelative("EnableRotation");
            rotationAmplitude = settings.FindPropertyRelative("RotationAmplitude");
            rotationFrequency = settings.FindPropertyRelative("RotationFrequency");
            rotationCurve = settings.FindPropertyRelative("RotationCurve");
            rotationSeed = settings.FindPropertyRelative("RotationSeed");
            enableFov = settings.FindPropertyRelative("EnableFov");
            fovAmplitude = settings.FindPropertyRelative("FovAmplitude");
            fovCurve = settings.FindPropertyRelative("FovCurve");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(
                EditorGUIUtility.currentViewWidth * 0.32f, 68f, 96f);

            EditorGUILayout.PropertyField(triggerMode, new GUIContent("Trigger"));
            EditorGUILayout.Space();
            DrawShakeSettings();

            var mode = (CameraShakeTriggerMode)triggerMode.enumValueIndex;
            if (mode == CameraShakeTriggerMode.OnConfirmedHit)
                DrawConfirmedHitSettings();

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            EditorGUIUtility.labelWidth = previousLabelWidth;

            var config = (AbilityEventObj_CameraShake)target;
            if (config.TriggerMode == CameraShakeTriggerMode.OnConfirmedHit)
            {
                EditorGUILayout.HelpBox(
                    "On Confirmed Hit：轨道区间是命中监听窗口；命中后按 Hit Shake Duration 播放。Preview Hit Time 用于编辑器模拟命中。",
                    MessageType.Info);
                if (config.HitBoxFilter == CameraShakeHitBoxFilter.SpecificHitBox &&
                    config.SpecificHitBox == null)
                {
                    EditorGUILayout.HelpBox("请选择要监听的 CreateHitBox 事件。",
                        MessageType.Warning);
                }
            }

            if (changed && CombatEditorUtility.EditorExist())
                CombatEditorUtility.GetCurrentEditor().RequirePreviewReload();
        }

        private void DrawShakeSettings()
        {
            DrawChannelHeader("Position", enablePosition);
            using (new EditorGUI.DisabledScope(!enablePosition.boolValue))
            {
                EditorGUILayout.PropertyField(positionAmplitude,
                    new GUIContent("Amplitude", "Local camera-space position amplitude."));
                EditorGUILayout.PropertyField(positionFrequency,
                    new GUIContent("Frequency", "Perlin noise frequency in Hz."));
                EditorGUILayout.PropertyField(positionCurve,
                    new GUIContent("Curve", "Position strength over normalized event time."));
                EditorGUILayout.PropertyField(positionSeed,
                    new GUIContent("Seed", "Changes the deterministic Position noise pattern."));
            }

            EditorGUILayout.Space(3f);
            DrawChannelHeader("Rotation", enableRotation);
            using (new EditorGUI.DisabledScope(!enableRotation.boolValue))
            {
                EditorGUILayout.PropertyField(rotationAmplitude,
                    new GUIContent("Amplitude", "Local rotation amplitude in degrees."));
                EditorGUILayout.PropertyField(rotationFrequency,
                    new GUIContent("Frequency", "Perlin noise frequency in Hz."));
                EditorGUILayout.PropertyField(rotationCurve,
                    new GUIContent("Curve", "Rotation strength over normalized event time."));
                EditorGUILayout.PropertyField(rotationSeed,
                    new GUIContent("Seed", "Changes the deterministic Rotation noise pattern."));
            }

            EditorGUILayout.Space(3f);
            DrawChannelHeader("FOV Punch", enableFov);
            using (new EditorGUI.DisabledScope(!enableFov.boolValue))
            {
                EditorGUILayout.PropertyField(fovAmplitude,
                    new GUIContent("Amplitude", "Positive widens the view; negative zooms in."));
                EditorGUILayout.PropertyField(fovCurve,
                    new GUIContent("Curve", "FOV offset and recovery over normalized event time."));
            }
        }

        private static void DrawChannelHeader(string title, SerializedProperty enabled)
        {
            EditorGUILayout.BeginHorizontal();
            enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfirmedHitSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Confirmed Hit Trigger", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hitShakeDuration, new GUIContent("Duration"));
            EditorGUILayout.PropertyField(useUnscaledTime, new GUIContent("Unscaled"));
            EditorGUILayout.PropertyField(hitBoxFilter, new GUIContent("HitBox"));

            var filter = (CameraShakeHitBoxFilter)hitBoxFilter.enumValueIndex;
            if (filter == CameraShakeHitBoxFilter.SpecificHitBox)
                EditorGUILayout.PropertyField(specificHitBox, new GUIContent("Specific"));

            EditorGUILayout.PropertyField(triggerPolicy, new GUIContent("Policy"));
            EditorGUILayout.PropertyField(acceptedResults, new GUIContent("Results"));
            EditorGUILayout.PropertyField(maximumTriggerCount, new GUIContent("Max Count"));

            var policy = (CameraShakeHitTriggerPolicy)triggerPolicy.enumValueIndex;
            if (policy == CameraShakeHitTriggerPolicy.EveryHitWithCooldown)
                EditorGUILayout.PropertyField(triggerCooldown, new GUIContent("Cooldown"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(previewHitTime, new GUIContent("Hit Time"));
            EditorGUILayout.PropertyField(previewHitIntensityScale,
                new GUIContent("Hit Scale"));
        }
    }
}
#endif
