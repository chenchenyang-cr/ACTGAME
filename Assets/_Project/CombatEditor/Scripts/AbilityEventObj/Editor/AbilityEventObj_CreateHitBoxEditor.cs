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
