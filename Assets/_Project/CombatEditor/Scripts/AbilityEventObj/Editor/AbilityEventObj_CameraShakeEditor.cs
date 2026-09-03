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

        private SerializedProperty channel;
        private SerializedProperty traumaPerPulse;
        private SerializedProperty traumaExponent;
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
        private SerializedProperty enableDirectionalImpulse;
        private SerializedProperty directionalPositionAmplitude;
        private SerializedProperty directionalImpulseCurve;

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

            channel = settings.FindPropertyRelative("Channel");
            traumaPerPulse = settings.FindPropertyRelative("TraumaPerPulse");
            traumaExponent = settings.FindPropertyRelative("TraumaExponent");
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
            enableDirectionalImpulse =
                settings.FindPropertyRelative("EnableDirectionalImpulse");
            directionalPositionAmplitude =
                settings.FindPropertyRelative("DirectionalPositionAmplitude");
            directionalImpulseCurve =
                settings.FindPropertyRelative("DirectionalImpulseCurve");
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

                DrawHitWindowValidation(config);
            }

            if (changed && CombatEditorUtility.EditorExist())
                CombatEditorUtility.GetCurrentEditor().RequirePreviewReload();
        }

        private void DrawShakeSettings()
        {
            EditorGUILayout.LabelField("Mixing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(channel, new GUIContent("Channel"));
            EditorGUILayout.PropertyField(traumaPerPulse,
                new GUIContent("Trauma / Pulse"));
            EditorGUILayout.PropertyField(traumaExponent,
                new GUIContent("Trauma Exponent"));

            EditorGUILayout.Space(3f);
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

            EditorGUILayout.Space(3f);
            DrawChannelHeader("Directional Impulse", enableDirectionalImpulse);
            using (new EditorGUI.DisabledScope(!enableDirectionalImpulse.boolValue))
            {
                EditorGUILayout.PropertyField(directionalPositionAmplitude,
                    new GUIContent("Amplitude",
                        "World-space displacement along the hit force. Negative values recoil against the force."));
                EditorGUILayout.PropertyField(directionalImpulseCurve,
                    new GUIContent("Curve"));
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

        private static void DrawHitWindowValidation(AbilityEventObj_CameraShake config)
        {
            string path = AssetDatabase.GetAssetPath(config);
            AbilityScriptableObject ability =
                AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(path);
            if (ability == null) return;

            AbilityEvent cameraEvent = null;
            bool hasHitBox = false;
            bool overlaps = false;
            for (int i = 0; i < ability.events.Count; i++)
            {
                AbilityEvent entry = ability.events[i];
                if (entry == null) continue;
                if (entry.Obj == config) cameraEvent = entry;
            }

            if (cameraEvent == null) return;
            for (int i = 0; i < ability.events.Count; i++)
            {
                AbilityEvent entry = ability.events[i];
                if (entry?.Obj is not AbilityEventObj_CreateHitBox hitBox) continue;
                if (config.HitBoxFilter == CameraShakeHitBoxFilter.SpecificHitBox &&
                    hitBox != config.SpecificHitBox) continue;

                hasHitBox = true;
                if (cameraEvent.GetEventStartTime() < entry.GetEventEndTime() &&
                    cameraEvent.GetEventEndTime() > entry.GetEventStartTime())
                    overlaps = true;
            }

            if (!hasHitBox)
            {
                EditorGUILayout.HelpBox("当前 Ability 中没有可监听的 CreateHitBox 事件。",
                    MessageType.Warning);
            }
            else if (!overlaps)
            {
                EditorGUILayout.HelpBox("摄像机震动监听区间没有覆盖目标 HitBox 区间，命中时不会触发震动。",
                    MessageType.Warning);
            }
        }
    }
}
#endif
