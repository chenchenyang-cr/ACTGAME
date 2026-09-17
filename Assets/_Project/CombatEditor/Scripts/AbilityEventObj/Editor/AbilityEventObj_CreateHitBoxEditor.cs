#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_CreateHitBox))]
    public sealed class AbilityEventObj_CreateHitBoxEditor :
        AbilityEventObj_CreateObjWithHandleEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            bool previewDataChanged = EditorGUI.EndChangeCheck();

            var config = (AbilityEventObj_CreateHitBox)target;
            if (config.EnableHitCameraShake && config.PreviewHitCameraShake)
            {
                var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(
                    AssetDatabase.GetAssetPath(config));
                var previewEvent = ability?.events.Find(entry => entry != null && entry.Obj == config);
                if (previewEvent != null && !previewEvent.Previewable)
                    EditorGUILayout.HelpBox(
                        "命中震动预览已勾选，但 Hitbox 轨道的预览开关仍关闭。请打开该轨道的预览图标，再在 Game 视图查看震动。",
                        MessageType.Info);
            }
            if (config.HitMode == CombatHitMode.Repeated)
            {
                EditorGUILayout.HelpBox(
                    $"首次接触立即命中，之后每 {Mathf.Max(1, config.RepeatIntervalFrames)} 个动作帧可再次命中同一目标。动作时间线固定按 60 FPS 计算。",
                    MessageType.Info);
            }

            if (config.ObjData == null || config.ObjData.TargetObj == null)
            {
                EditorGUILayout.HelpBox("请选择包含 HitBox 组件的碰撞盒 Prefab。",
                    MessageType.Warning);
            }
            else if (config.ObjData.TargetObj.GetComponent<HitBox>() == null)
            {
                EditorGUILayout.HelpBox("当前 Prefab 缺少 HitBox 组件，运行时不会结算伤害。",
                    MessageType.Error);
            }

            if (!config.EnableHitCameraShake)
            {
                EditorGUILayout.HelpBox(
                    "命中震动未启用。碰撞仍会正常结算伤害。",
                    MessageType.Info);
            }

            if (config.EnableHitCameraShake && config.HitCameraShakeProfile == null)
            {
                EditorGUILayout.HelpBox(
                    "请选择 Camera Shake Profile。当前暂时使用旧版内嵌参数作为兼容回退。",
                    MessageType.Warning);
            }

            if (config.EnableHitAnimationSpeed)
            {
                EditorGUILayout.HelpBox(
                    $"命中确认后，攻击者立即顿帧，受击者延迟 1 个游戏帧开始顿帧。各自暂停 {Mathf.Max(0, config.HitStopFrames)} 帧（按 60 FPS 计时）后恢复，延迟不占暂停时长。0 帧表示不顿帧；连续命中会刷新持续时间。",
                    MessageType.Info);
            }

            if (config.EnableHitVfx && config.HitVfxPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    "命中特效已启用，但尚未指定 Hit VFX Prefab。",
                    MessageType.Warning);
            }
            else if (config.EnableHitVfx)
            {
                EditorGUILayout.HelpBox(
                    config.HitVfxPositionMode == CombatHitVfxPositionMode.TargetPosition
                        ? "生成位置 = 对手受击组件所在对象的位置 + 固定偏移 + 随机偏移。偏移使用世界坐标；随机幅度为各轴的正负上限，每次命中独立取值。角色原点在脚底时，可将固定偏移 Y 设为胸口高度。"
                        : "特效会在确认命中后生成于 HitPoint，固定位置偏移按特效最终朝向计算；随机位置偏移在此模式下不生效。",
                    MessageType.Info);
            }

            if (previewDataChanged)
            {
                SceneView.RepaintAll();
                if (CombatEditorUtility.EditorExist())
                    CombatEditorUtility.GetCurrentEditor().RequirePreviewReload();
            }
        }
    }
}
#endif
