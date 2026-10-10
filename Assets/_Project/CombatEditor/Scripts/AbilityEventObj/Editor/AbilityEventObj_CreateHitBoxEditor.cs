#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_CreateHitBox))]
    public sealed class AbilityEventObj_CreateHitBoxEditor : AbilityEventObj_CreateObjWithHandleEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUI.EndDisabledGroup();
            DrawPropertiesExcluding(serializedObject, "m_Script", "HitProfile");
            CombatHitProfileEditorUtility.DrawReference(serializedObject);
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            DrawTransformEditButtons();

            var source = (AbilityEventObj_CreateHitBox)target;
            if (source.ObjData == null || source.ObjData.TargetObj == null)
                EditorGUILayout.HelpBox("请选择包含 HitBox 组件的碰撞盒 Prefab。", MessageType.Warning);
            else if (source.ObjData.TargetObj.GetComponent<HitBox>() == null)
                EditorGUILayout.HelpBox("当前 Prefab 缺少 HitBox 组件，运行时不会结算伤害。", MessageType.Error);

            var profile = source.HitProfile;
            if (profile != null && profile.EnableHitCameraShake && profile.PreviewHitCameraShake)
            {
                var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(AssetDatabase.GetAssetPath(source));
                var entry = ability?.events.Find(e => e != null && e.Obj == source);
                if (entry != null && !entry.Previewable)
                    EditorGUILayout.HelpBox("请打开碰撞盒轨道的预览开关，以查看配置中的命中震动预览。", MessageType.Info);
            }
            if (changed)
            {
                SceneView.RepaintAll();
                if (CombatEditorUtility.EditorExist())
                    CombatEditorUtility.GetCurrentEditor().RequirePreviewReload();
            }
        }
    }
}
#endif