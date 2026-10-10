#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CombatCamera;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_CameraShake))]
    public sealed class AbilityEventObj_CameraShakeEditor : Editor
    {
        private SerializedProperty triggerMode;
        private SerializedProperty settings;
        private SerializedProperty affectedByHitStop;
        private SerializedProperty hitStopWeight;
        private SerializedProperty hitShakeDuration;
        private SerializedProperty useUnscaledTime;
        private SerializedProperty startAfterHitStop;
        private SerializedProperty hitBoxFilter;
        private SerializedProperty specificHitBox;
        private SerializedProperty triggerPolicy;
        private SerializedProperty acceptedResults;
        private SerializedProperty maximumTriggerCount;
        private SerializedProperty triggerCooldown;
        private SerializedProperty previewHitTime;
        private SerializedProperty previewHitIntensityScale;

        private SerializedProperty amplitudeDecayCurve;
        private SerializedProperty channel;
        private SerializedProperty traumaPerPulse;
        private SerializedProperty traumaExponent;
        private SerializedProperty enablePosition;
        private SerializedProperty positionWaveform;
        private SerializedProperty positionPhase;
        private SerializedProperty positionAmplitude;
        private SerializedProperty positionFrequency;
        private SerializedProperty positionFrequencyCurve;
        private SerializedProperty positionCurve;
        private SerializedProperty positionSeed;
        private SerializedProperty enableRotation;
        private SerializedProperty rotationWaveform;
        private SerializedProperty rotationPhase;
        private SerializedProperty rotationAmplitude;
        private SerializedProperty rotationFrequency;
        private SerializedProperty rotationFrequencyCurve;
        private SerializedProperty rotationCurve;
        private SerializedProperty rotationSeed;
        private SerializedProperty enableFov;
        private SerializedProperty fovAmplitude;
        private SerializedProperty fovCurve;
        private SerializedProperty enableDirectionalImpulse;
        private SerializedProperty directionalPositionRange;
        private SerializedProperty directionalPositionFrequency;
        private SerializedProperty directionalDirectionMode;
        private SerializedProperty directionalManualSpace;

        private void OnEnable()
        {
            triggerMode = serializedObject.FindProperty("TriggerMode");
            settings = serializedObject.FindProperty("Settings");
            affectedByHitStop = serializedObject.FindProperty("AffectedByHitStop");
            hitStopWeight = serializedObject.FindProperty("HitStopWeight");
            hitShakeDuration = serializedObject.FindProperty("HitShakeDuration");
            useUnscaledTime = serializedObject.FindProperty("UseUnscaledTime");
            startAfterHitStop = serializedObject.FindProperty("StartAfterHitStop");
            hitBoxFilter = serializedObject.FindProperty("HitBoxFilter");
            specificHitBox = serializedObject.FindProperty("SpecificHitBox");
            triggerPolicy = serializedObject.FindProperty("TriggerPolicy");
            acceptedResults = serializedObject.FindProperty("AcceptedResults");
            maximumTriggerCount = serializedObject.FindProperty("MaximumTriggerCount");
            triggerCooldown = serializedObject.FindProperty("TriggerCooldown");
            previewHitTime = serializedObject.FindProperty("PreviewHitTime");
            previewHitIntensityScale = serializedObject.FindProperty("PreviewHitIntensityScale");

            amplitudeDecayCurve = settings.FindPropertyRelative("AmplitudeDecayCurve");
            channel = settings.FindPropertyRelative("Channel");
            traumaPerPulse = settings.FindPropertyRelative("TraumaPerPulse");
            traumaExponent = settings.FindPropertyRelative("TraumaExponent");
            enablePosition = settings.FindPropertyRelative("EnablePosition");
            positionWaveform = settings.FindPropertyRelative("PositionWaveform");
            positionPhase = settings.FindPropertyRelative("PositionPhase");
            positionAmplitude = settings.FindPropertyRelative("PositionAmplitude");
            positionFrequency = settings.FindPropertyRelative("PositionFrequency");
            positionFrequencyCurve = settings.FindPropertyRelative("PositionFrequencyCurve");
            positionCurve = settings.FindPropertyRelative("PositionCurve");
            positionSeed = settings.FindPropertyRelative("PositionSeed");
            enableRotation = settings.FindPropertyRelative("EnableRotation");
            rotationWaveform = settings.FindPropertyRelative("RotationWaveform");
            rotationPhase = settings.FindPropertyRelative("RotationPhase");
            rotationAmplitude = settings.FindPropertyRelative("RotationAmplitude");
            rotationFrequency = settings.FindPropertyRelative("RotationFrequency");
            rotationFrequencyCurve = settings.FindPropertyRelative("RotationFrequencyCurve");
            rotationCurve = settings.FindPropertyRelative("RotationCurve");
            rotationSeed = settings.FindPropertyRelative("RotationSeed");
            enableFov = settings.FindPropertyRelative("EnableFov");
            fovAmplitude = settings.FindPropertyRelative("FovAmplitude");
            fovCurve = settings.FindPropertyRelative("FovCurve");
            enableDirectionalImpulse =
                settings.FindPropertyRelative("EnableDirectionalImpulse");
            directionalPositionRange = settings.FindPropertyRelative("DirectionalPositionRange");
            directionalPositionFrequency = settings.FindPropertyRelative("DirectionalPositionFrequency");
            directionalDirectionMode = settings.FindPropertyRelative("DirectionalDirectionMode");
            directionalManualSpace = settings.FindPropertyRelative("DirectionalManualSpace");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(
                EditorGUIUtility.currentViewWidth * 0.32f, 68f, 96f);

            EditorGUILayout.PropertyField(triggerMode, new GUIContent("触发方式"));
            EditorGUILayout.Space();
            DrawShakeSettings();
            EditorGUILayout.PropertyField(affectedByHitStop,
                new GUIContent("受顿帧影响", triggerMode.enumValueIndex ==
                    (int)CameraShakeTriggerMode.OnConfirmedHit
                    ? "震动开始播放后若遇到后续顿帧，此项控制减速程度；首次命中是否等待顿帧由“顿帧后开始震动”控制。"
                    : "直接触发的震动在顿帧时按所属角色的顿帧动画速度暂停或减速，结束后继续。"));
            if (affectedByHitStop.boolValue)
                EditorGUILayout.PropertyField(hitStopWeight,
                    new GUIContent("顿帧影响权重", "控制震动开始播放后遇到后续顿帧时的速度：0 原速，1 完全按顿帧动画速度播放。"));

            var mode = (CameraShakeTriggerMode)triggerMode.enumValueIndex;
            if (mode == CameraShakeTriggerMode.OnConfirmedHit)
                DrawConfirmedHitSettings();

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            EditorGUIUtility.labelWidth = previousLabelWidth;

            var config = (AbilityEventObj_CameraShake)target;
            var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(
                AssetDatabase.GetAssetPath(config));
            var previewEvent = ability?.events.Find(entry => entry != null && entry.Obj == config);
            if (previewEvent != null && !previewEvent.Previewable)
                EditorGUILayout.HelpBox(
                    "此轨道的预览开关已关闭。请先打开动作编辑器中该轨道的预览图标，再在游戏视图播放或拖动时间线查看震动。",
                    MessageType.Info);
            if (config.TriggerMode == CameraShakeTriggerMode.OnConfirmedHit)
            {
                EditorGUILayout.HelpBox(
                    "轨道区间是命中监听窗口；开启“顿帧后开始震动”时，确认命中后等待双方顿帧结束，同一目标等待期间的新命中会替换待播震动；没有顿帧时立即播放。“模拟位置”用于编辑器预览。",
                    MessageType.Info);
                if (config.HitBoxFilter == CameraShakeHitBoxFilter.SpecificHitBox &&
                    config.SpecificHitBox == null)
                {
                    EditorGUILayout.HelpBox("请选择要监听的碰撞盒轨道。",
                        MessageType.Warning);
                }

                DrawHitWindowValidation(config);
            }
            else
            {
                EditorGUILayout.HelpBox("普通震动按轨道区间播放；定向震动在轨道起点从中心开始恒速往返，轨道结束时直接回中。默认按实际秒数计算；开启受顿帧影响后会随顿帧暂停或减速。",
                    MessageType.Info);
            }

            if (changed && CombatEditorUtility.EditorExist())
                CombatEditorUtility.GetCurrentEditor().RequirePreviewReload();
        }

        private void DrawShakeSettings()
        {
            EditorGUILayout.PropertyField(amplitudeDecayCurve,
                new GUIContent("普通强度变化", "只控制普通位移、转动和视野变化；定向震动不受此曲线影响。"));
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("震动叠加", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(channel, new GUIContent("震动分组", "震动按组合成；每次命中独立播放，不覆盖同组旧震动。"));
            EditorGUILayout.PropertyField(traumaPerPulse,
                new GUIContent("命中强度", "每次命中的独立普通震动倍率；1 为完整强度。仅命中触发有效，不影响定向震动。"));
            EditorGUILayout.PropertyField(traumaExponent,
                new GUIContent("轻震抑制", "数值越大，较弱的命中震动越不明显。"));

            EditorGUILayout.Space(3f);
            DrawChannelHeader("镜头位移抖动", enablePosition);
            using (new EditorGUI.DisabledScope(!enablePosition.boolValue))
            {
                EditorGUILayout.PropertyField(positionWaveform,
                    new GUIContent("位移轨迹", "随机抖动或规律往复。"));
                EditorGUILayout.PropertyField(positionAmplitude,
                    new GUIContent("位移范围", "镜头局部坐标中各轴的最大位移范围，单位米。"));
                EditorGUILayout.PropertyField(positionFrequency,
                    new GUIContent("抖动快慢", "数值越大，位移抖动越快。"));
                EditorGUILayout.PropertyField(positionFrequencyCurve,
                    new GUIContent("快慢变化", "横轴是震动进度，纵轴是抖动快慢的倍率。"));
                EditorGUILayout.PropertyField(positionCurve,
                    new GUIContent("幅度变化", "横轴是震动进度，纵轴是位移范围的倍率。"));
                if (positionWaveform.intValue == (int)CameraShakeWaveform.Sine)
                    EditorGUILayout.PropertyField(positionPhase,
                        new GUIContent("起步位置", "各轴的起始相位。0 从中心起步，90 从最大偏移起步，单位度。"));
                else
                    EditorGUILayout.PropertyField(positionSeed,
                        new GUIContent("随机形态", "改变随机位移轨迹；相同数值产生相同结果。"));
            }

            EditorGUILayout.Space(3f);
            DrawChannelHeader("镜头转动抖动", enableRotation);
            using (new EditorGUI.DisabledScope(!enableRotation.boolValue))
            {
                EditorGUILayout.PropertyField(rotationWaveform,
                    new GUIContent("转动轨迹", "随机抖动或规律往复。"));
                EditorGUILayout.PropertyField(rotationAmplitude,
                    new GUIContent("转动范围", "镜头局部坐标中各轴的最大转角，单位度。"));
                EditorGUILayout.PropertyField(rotationFrequency,
                    new GUIContent("抖动快慢", "数值越大，转动抖动越快。"));
                EditorGUILayout.PropertyField(rotationFrequencyCurve,
                    new GUIContent("快慢变化", "横轴是震动进度，纵轴是抖动快慢的倍率。"));
                EditorGUILayout.PropertyField(rotationCurve,
                    new GUIContent("幅度变化", "横轴是震动进度，纵轴是转动范围的倍率。"));
                if (rotationWaveform.intValue == (int)CameraShakeWaveform.Sine)
                    EditorGUILayout.PropertyField(rotationPhase,
                        new GUIContent("起步角度", "各轴的起始相位。0 从中心起步，90 从最大转角起步，单位度。"));
                else
                    EditorGUILayout.PropertyField(rotationSeed,
                        new GUIContent("随机形态", "改变随机转动轨迹；相同数值产生相同结果。"));
            }

            EditorGUILayout.Space(3f);
            DrawChannelHeader("视野伸缩", enableFov);
            using (new EditorGUI.DisabledScope(!enableFov.boolValue))
            {
                EditorGUILayout.PropertyField(fovAmplitude,
                    new GUIContent("变化幅度", "正值扩大视野，负值缩小视野，单位度。"));
                EditorGUILayout.PropertyField(fovCurve,
                    new GUIContent("变化过程", "控制视野扩大或缩小以及恢复的过程。"));
            }

            EditorGUILayout.Space(3f);
            DrawChannelHeader("定向往返震动（可叠加）", enableDirectionalImpulse);
            using (new EditorGUI.DisabledScope(!enableDirectionalImpulse.boolValue))
            {
                EditorGUILayout.PropertyField(directionalDirectionMode, new GUIContent("方向模式"));
                if (directionalDirectionMode.intValue == (int)DirectionalShakeDirectionMode.Manual)
                {
                    EditorGUILayout.PropertyField(directionalManualSpace, new GUIContent("方向坐标系"));
                }
                bool manualPosition = directionalDirectionMode.intValue == (int)DirectionalShakeDirectionMode.Manual;
                EditorGUILayout.PropertyField(manualPosition
                        ? settings.FindPropertyRelative("DirectionalManualPositionRange")
                        : directionalPositionRange,
                    new GUIContent("位移范围", manualPosition
                        ? "三轴数值共同决定往返方向和范围，单位米；全为 0 时不移动。"
                        : "沿攻击方向从中心到折返点的距离，单位米；0 时不移动。"));
                EditorGUILayout.PropertyField(directionalPositionFrequency,
                    new GUIContent("位移每秒往返次数", "每秒完成多少轮位移往返；0 时不移动。"));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("DirectionalPositionRangeDecay"),
                    new GUIContent("位移幅度衰减", "每轮往返后位移范围缩小的比例。0 不衰减；数值越大，回中越快。"));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("DirectionalPositionDecayCurve"),
                    new GUIContent("位移衰减强度曲线", "横轴是整段震动进度，纵轴是位移幅度衰减的倍率；1 保持原效果，后段高于 1 会逐渐增强衰减。"));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("DirectionalTrajectoryStraightness"),
                    new GUIContent("轨迹直线度", "1 为圆滑的正弦曲线；越大越接近直线运动，并在折返点直接折返。往返次数和范围不变。"));
                EditorGUILayout.HelpBox("位移按设定周期往返，幅度随时间衰减；轨道结束时直接回中。", MessageType.Info);
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
            EditorGUILayout.LabelField("命中后触发", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hitShakeDuration,
                new GUIContent("震动时长", "命中后普通震动和定向震动持续多久，单位秒；结束时定向震动直接回中。"));
            EditorGUILayout.PropertyField(useUnscaledTime,
                new GUIContent("真实计时", "开启后不受游戏暂停或慢动作影响。"));
            EditorGUILayout.PropertyField(startAfterHitStop,
                new GUIContent("顿帧后开始震动", "开启时等待攻击者和受击者的顿帧都结束；没有顿帧时立即播放。等待期间同一目标仅保留最新一次命中震动。"));
            EditorGUILayout.PropertyField(hitBoxFilter,
                new GUIContent("监听范围", "监听本动作的全部碰撞盒或指定碰撞盒。"));

            var filter = (CameraShakeHitBoxFilter)hitBoxFilter.enumValueIndex;
            if (filter == CameraShakeHitBoxFilter.SpecificHitBox)
                EditorGUILayout.PropertyField(specificHitBox, new GUIContent("指定碰撞盒"));

            EditorGUILayout.PropertyField(triggerPolicy, new GUIContent("重复规则"));
            EditorGUILayout.PropertyField(acceptedResults, new GUIContent("触发结果"));
            EditorGUILayout.PropertyField(maximumTriggerCount,
                new GUIContent("触发上限", "0 表示不限制次数。"));

            var policy = (CameraShakeHitTriggerPolicy)triggerPolicy.enumValueIndex;
            if (policy == CameraShakeHitTriggerPolicy.EveryHitWithCooldown)
                EditorGUILayout.PropertyField(triggerCooldown,
                    new GUIContent("触发间隔", "连续命中至少间隔的秒数。"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("命中预览", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(previewHitTime,
                new GUIContent("模拟位置", "在监听区间中模拟命中的时间位置。"));
            EditorGUILayout.PropertyField(previewHitIntensityScale,
                new GUIContent("预览强度", "模拟命中时的震动强度倍率。"));
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
                if (entry?.Obj is not AbilityEventObj_HitSource hitBox || !hitBox.HasHitBox) continue;
                if (config.HitBoxFilter == CameraShakeHitBoxFilter.SpecificHitBox &&
                    hitBox != config.SpecificHitBox) continue;

                hasHitBox = true;
                if (cameraEvent.GetEventStartTime() < entry.GetEventEndTime() &&
                    cameraEvent.GetEventEndTime() > entry.GetEventStartTime())
                    overlaps = true;
            }

            if (!hasHitBox)
            {
                EditorGUILayout.HelpBox("当前动作中没有可监听的碰撞盒轨道。",
                    MessageType.Warning);
            }
            else if (!overlaps)
            {
                EditorGUILayout.HelpBox("摄像机震动监听区间没有覆盖目标碰撞盒区间，命中时不会触发震动。",
                    MessageType.Warning);
            }
        }
    }

    [CustomEditor(typeof(CameraShakeProfile))]
    [CanEditMultipleObjects]
    public sealed class CameraShakeProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Duration"),
                new GUIContent("震动时长", "命中后普通震动和定向震动持续多久；结束时定向震动直接回中。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("UseUnscaledTime"),
                new GUIContent("使用真实时间"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("StartAfterHitStop"),
                new GUIContent("顿帧后开始震动", "开启时等待攻击者和受击者的顿帧都结束；没有顿帧时立即播放。等待期间同一目标仅保留最新一次命中震动。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AffectedByHitStop"),
                new GUIContent("受顿帧影响", "震动开始播放后若遇到后续顿帧，按攻击者的顿帧动画速度暂停或减速。"));
            if (serializedObject.FindProperty("AffectedByHitStop").boolValue)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("HitStopWeight"),
                    new GUIContent("顿帧影响权重", "控制震动开始播放后遇到后续顿帧时的速度：0 原速，1 完全按顿帧动画速度播放。"));

            SerializedProperty settings = serializedObject.FindProperty("Settings");
            EditorGUILayout.PropertyField(settings, new GUIContent("震动设置"), false);
            if (settings.isExpanded)
            {
                EditorGUI.indentLevel++;
                SerializedProperty child = settings.Copy();
                SerializedProperty end = child.GetEndProperty();
                bool hasNext = child.NextVisible(true);
                while (hasNext && !SerializedProperty.EqualContents(child, end))
                {
                    bool manualPosition = settings.FindPropertyRelative("DirectionalDirectionMode").intValue ==
                                          (int)DirectionalShakeDirectionMode.Manual;
                    if ((child.name == "DirectionalPositionRange" && manualPosition) ||
                        (child.name == "DirectionalManualPositionRange" && !manualPosition) ||
                        (child.name == "DirectionalManualSpace" && !manualPosition))
                    {
                        hasNext = child.NextVisible(false);
                        continue;
                    }
                    GUIContent label = ShakeLabel(child.name);
                    if (label == null)
                        EditorGUILayout.PropertyField(child, true);
                    else
                        EditorGUILayout.PropertyField(child, label, true);
                    hasNext = child.NextVisible(false);
                }
                EditorGUI.indentLevel--;
            }
            serializedObject.ApplyModifiedProperties();
            if (targets.Length == 1)
            {
                var profile = (CameraShakeProfile)target;
                DrawDirectionalTrajectory(profile.Settings, Mathf.Max(0.01f, profile.Duration));
            }
        }

        private static void DrawDirectionalTrajectory(CameraShakeSettings settings, float duration)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("定向位移轨迹", EditorStyles.boldLabel);
            if (settings == null || !settings.EnableDirectionalImpulse)
            {
                EditorGUILayout.HelpBox("启用定向震动后显示位移轨迹。", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("横轴：震动时间（秒）    纵轴：沿震动方向的位移（米）",
                EditorStyles.miniLabel);

            Rect area = EditorGUILayout.GetControlRect(false, 176f);
            Rect graph = new Rect(area.x + 48f, area.y + 8f,
                Mathf.Max(1f, area.width - 58f), area.height - 32f);
            EditorGUI.DrawRect(graph, new Color(0.13f, 0.13f, 0.13f, 1f));

            Vector3 direction = settings.DirectionalDirectionMode == DirectionalShakeDirectionMode.Manual
                ? settings.DirectionalManualPositionRange : Vector3.right;
            float range = settings.DirectionalDirectionMode == DirectionalShakeDirectionMode.Manual
                ? direction.magnitude : Mathf.Abs(settings.DirectionalPositionRange);
            range = Mathf.Max(0.001f, range);
            float middle = graph.center.y;
            float halfHeight = graph.height * 0.45f;
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            Handles.DrawLine(new Vector3(graph.xMin, middle), new Vector3(graph.xMax, middle));

            int sampleCount = Mathf.Clamp(Mathf.CeilToInt(
                duration * settings.DirectionalPositionFrequency * 16f), 256, 2048);
            var points = new Vector3[sampleCount + 1];
            Vector3 unitDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.right;
            for (int i = 0; i <= sampleCount; i++)
            {
                float seconds = duration * i / sampleCount;
                // A finished shake is removed immediately, returning the camera to center.
                float displacement = 0f;
                if (i < sampleCount)
                {
                    CameraShakeSample sample = settings.EvaluateDirectional(
                        seconds, seconds / duration, Vector3.right, 1f);
                    displacement = Vector3.Dot(unitDirection,
                        sample.Position + sample.WorldPosition);
                }
                points[i] = new Vector3(
                    Mathf.Lerp(graph.xMin, graph.xMax, i / (float)sampleCount),
                    middle - Mathf.Clamp(displacement / range, -1f, 1f) * halfHeight);
            }
            Handles.color = new Color(0.3f, 0.85f, 1f, 1f);
            Handles.DrawAAPolyLine(2f, points);
            Handles.color = Color.white;

            GUI.Label(new Rect(area.x, graph.yMin - 8f, 46f, 18f), $"+{range:0.###}",
                EditorStyles.miniLabel);
            GUI.Label(new Rect(area.x, middle - 8f, 46f, 18f), "0",
                EditorStyles.miniLabel);
            GUI.Label(new Rect(area.x, graph.yMax - 10f, 46f, 18f), $"-{range:0.###}",
                EditorStyles.miniLabel);
            GUI.Label(new Rect(graph.xMin, graph.yMax + 2f, 60f, 18f), "0 秒",
                EditorStyles.miniLabel);
            GUI.Label(new Rect(graph.xMax - 80f, graph.yMax + 2f, 80f, 18f),
                $"{duration:0.###} 秒", EditorStyles.miniLabel);
        }

        private static GUIContent ShakeLabel(string field)
        {
            switch (field)
            {
                case "AmplitudeDecayCurve": return new GUIContent("普通强度变化", "只控制普通位移、转动和视野变化；定向震动不受此曲线影响。");
                case "Channel": return new GUIContent("震动分组");
                case "TraumaPerPulse": return new GUIContent("命中强度");
                case "TraumaExponent": return new GUIContent("轻震抑制");
                case "EnablePosition": return new GUIContent("启用位移抖动");
                case "PositionWaveform": return new GUIContent("位移轨迹");
                case "PositionPhase": return new GUIContent("位移起步位置");
                case "PositionAmplitude": return new GUIContent("位移范围");
                case "PositionFrequency": return new GUIContent("位移抖动快慢");
                case "PositionFrequencyCurve": return new GUIContent("位移快慢变化");
                case "PositionCurve": return new GUIContent("位移幅度变化");
                case "PositionSeed": return new GUIContent("位移随机形态");
                case "EnableRotation": return new GUIContent("启用转动抖动");
                case "RotationWaveform": return new GUIContent("转动轨迹");
                case "RotationPhase": return new GUIContent("转动起步角度");
                case "RotationAmplitude": return new GUIContent("转动范围");
                case "RotationFrequency": return new GUIContent("转动抖动快慢");
                case "RotationFrequencyCurve": return new GUIContent("转动快慢变化");
                case "RotationCurve": return new GUIContent("转动幅度变化");
                case "RotationSeed": return new GUIContent("转动随机形态");
                case "EnableFov": return new GUIContent("启用视野伸缩");
                case "FovAmplitude": return new GUIContent("视野变化幅度");
                case "FovCurve": return new GUIContent("视野变化过程");
                case "EnableDirectionalImpulse": return new GUIContent("启用定向震动");
                case "DirectionalDirectionMode": return new GUIContent("方向模式");
                case "DirectionalManualSpace": return new GUIContent("方向坐标系");
                case "DirectionalPositionRange": return new GUIContent("位移范围", "沿攻击方向从中心到折返点的距离，单位米；0 时不移动。");
                case "DirectionalManualPositionRange": return new GUIContent("位移范围", "三轴数值共同决定往返方向和范围，单位米；全为 0 时不移动。");
                case "DirectionalPositionFrequency": return new GUIContent("位移每秒往返次数", "每秒完成多少轮位移往返；0 时不移动。");
                case "DirectionalPositionRangeDecay": return new GUIContent("位移幅度衰减", "每轮往返后位移范围缩小的比例。0 不衰减；数值越大，回中越快。");
                case "DirectionalPositionDecayCurve": return new GUIContent("位移衰减强度曲线", "横轴是整段震动进度，纵轴是位移幅度衰减的倍率；1 保持原效果，后段高于 1 会逐渐增强衰减。");
                case "DirectionalTrajectoryStraightness": return new GUIContent("轨迹直线度", "1 为圆滑的正弦曲线；越大越接近直线运动，并在折返点直接折返。往返次数和范围不变。");
                default: return null;
            }
        }
    }
}
#endif
