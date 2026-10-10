using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(CombatHitProfile))]
    public sealed class CombatHitProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                string label = property.name == "SuppressDamageNumber" ? "禁用跳字" :
                    property.name == "DamageNumberOverride" ? "跳字预设覆盖" : property.displayName;
                using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                    EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }
            serializedObject.ApplyModifiedProperties();
            bool changed = EditorGUI.EndChangeCheck();
            var profile = (CombatHitProfile)target;
            if (profile.HitMode == CombatHitMode.Repeated)
                EditorGUILayout.HelpBox($"首次接触立即命中，之后每 {Mathf.Max(1, profile.RepeatIntervalFrames)} 个动作帧再次命中同一目标。命中次数由各轨道独立记录，时间线按 60 FPS 计算。", MessageType.Info);
            if (profile.EnableHitCameraShake && profile.HitCameraShakeProfile == null)
                EditorGUILayout.HelpBox("尚未指定震动预设，当前使用配置内保留的旧版震动参数。", MessageType.Warning);
            if (profile.EnableHitVfx && profile.HitVfxPrefab == null)
                EditorGUILayout.HelpBox("命中特效已启用，请指定命中特效 Prefab。", MessageType.Warning);
            if (changed)
            {
                SceneView.RepaintAll();
                if (CombatEditorUtility.EditorExist())
                    CombatEditorUtility.GetCurrentEditor().RequirePreviewReload();
            }
        }
    }

    internal static class CombatHitProfileEditorUtility
    {
        private const string Folder = "Assets/_Project/CombatEditor/ScriptableObjects/HitProfiles";

        internal static void DrawReference(SerializedObject source)
        {
            var property = source.FindProperty("HitProfile");
            EditorGUILayout.PropertyField(property, new GUIContent("受击配置"));
            var profile = property.objectReferenceValue as CombatHitProfile;
            if (profile != null)
            {
                if (GUILayout.Button("编辑受击配置"))
                {
                    Selection.activeObject = profile;
                    EditorGUIUtility.PingObject(profile);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("选择或新建受击配置后，碰撞体才能结算伤害和命中反馈。", MessageType.Info);
                if (GUILayout.Button("新建受击配置"))
                {
                    EnsureFolder(Folder);
                    string path = EditorUtility.SaveFilePanelInProject("新建受击配置", "HitProfile", "asset",
                        "保存此轨道使用的受击配置。", Folder);
                    if (string.IsNullOrEmpty(path)) return;
                    var created = ScriptableObject.CreateInstance<CombatHitProfile>();
                    AssetDatabase.CreateAsset(created, path);
                    Undo.RegisterCreatedObjectUndo(created, "Create Hit Profile");
                    property.objectReferenceValue = created;
                    source.ApplyModifiedProperties();
                    EditorUtility.SetDirty(source.targetObject);
                    AssetDatabase.SaveAssetIfDirty(source.targetObject);
                    EditorGUIUtility.PingObject(created);
                }
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder)
                .Replace(System.IO.Path.DirectorySeparatorChar, '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
