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
                    "命中确认后，攻击者和被击中者会同时使用该曲线变速。横轴是效果时间（0~1），纵轴直接作为速度倍率；连续命中会刷新效果，不会重复叠乘。",
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
                    "特效会在确认命中后生成于 HitPoint。默认 Attack Direction 适合血液和火花；位置与旋转偏移按最终特效朝向计算。",
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
