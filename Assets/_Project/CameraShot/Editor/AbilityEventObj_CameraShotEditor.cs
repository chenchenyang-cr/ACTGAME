using CombatCamera;
using CombatCamera.Editor;
using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_CameraShot))]
    public sealed class AbilityEventObj_CameraShotEditor : UnityEditor.Editor
    {
        private readonly CameraShotNodeView node = new CameraShotNodeView();
        private SerializedObject profileObject;
        private void OnDisable() => node.Dispose();

        public override void OnInspectorGUI()
        {
            float oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 105;
            var config = (AbilityEventObj_CameraShot)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playOnPerfectDodge"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Profile"), new GUIContent("机位配置"));
            if (config.Profile == null && GUILayout.Button("创建机位配置")) {
                var profile = ScriptableObject.CreateInstance<CameraShotProfile>();
                profile.name = config.name + "_机位";
                string path = AssetDatabase.GetAssetPath(config);
                if (!string.IsNullOrEmpty(path)) {
                    Undo.RegisterCreatedObjectUndo(profile, "Create CameraShot profile");
                    AssetDatabase.AddObjectToAsset(profile, path);
                    serializedObject.FindProperty("Profile").objectReferenceValue = profile;
                }
                else Object.DestroyImmediate(profile);
            }
            var keepPosition = serializedObject.FindProperty("keepFinalCameraPosition");
            keepPosition.boolValue = !EditorGUILayout.Toggle(new GUIContent("回到原镜头位置",
                "开启：按转出曲线返回原游戏机位。关闭：结束或中断时从当前机位交回相机操作，不执行回程。"), !keepPosition.boolValue);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("IgnoreHitStop"), new GUIContent("不受顿帧影响",
                "开启：转入、停留和转出按实际时间推进，不受角色或全局顿帧影响。关闭：随动作时间推进。动作中断仍会结束运镜。"));
            CameraShotNodeView.DrawTiming(serializedObject.FindProperty("Timing"), serializedObject.FindProperty("HoldSeconds"), !keepPosition.boolValue);
            EditorGUILayout.LabelField("节点总时长", $"{config.Duration:F3} 秒");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("PreviewCamera"), new GUIContent("同步游戏视图"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Channel"), new GUIContent("相机分组"));
            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed) AssetDatabase.SaveAssetIfDirty(config);
            if (config.Profile != null) {
                if (profileObject == null || profileObject.targetObject != config.Profile) profileObject = new SerializedObject(config.Profile);
                changed |= node.DrawProfile(profileObject);
            }
            var editor = CombatEditorUtility.EditorExist() ? CombatEditorUtility.GetCurrentEditor() : null;
            var actor = editor != null && editor.SelectedController != null ? editor.SelectedController.transform : null;
            var extension = CinemachineCameraShotExtension.FindActive(config.Channel);
            var preview = AbilityEventPreview_CameraShot.Find(config);
            node.DrawPreview(config.Profile, actor, extension != null ? extension.Rig : null,
                preview != null && preview.HasOutput ? (CameraShotPose?)preview.Output : null,
                preview != null ? preview.SelectedCandidate : -1);
            if (editor != null && editor.SelectedAbilityObj != null && editor.SelectedAbilityObj.Clip != null) {
                var entry = editor.SelectedAbilityObj.events.Find(e => e != null && e.Obj == config);
                if (entry != null) {
                    if (!entry.Previewable)
                        EditorGUILayout.HelpBox("轨道预览已关闭：节点内仍可预览镜头；启用轨道预览后才能同步游戏视图。", MessageType.Info);
                    using (new EditorGUILayout.HorizontalScope()) {
                        if (GUILayout.Button(editor.IsPlaying || editor.IsLooping ? "暂停动作" : "播放动作")) {
                            node.PreviewCandidate = -1;
                            if (editor.IsPlaying || editor.IsLooping) editor.OnPausePlay(); else editor.OnStartLoop();
                        }
                        if (GUILayout.Button("回到片段开始")) {
                            editor.OnPausePlay();
                            editor.OnPreviewAnimationAtPercentage(entry.GetEventStartTime());
                            editor.OnSetPointerOnTrack(Mathf.RoundToInt(entry.GetEventStartTime() * editor.SelectedAbilityObj.Clip.length * CombatTimeline.FramesPerSecond));
                            editor.Repaint();
                        }
                    }
                }
            }
            if (changed && editor != null) { editor.RequirePreviewReload(); editor.Repaint(); }
            EditorGUIUtility.labelWidth = oldWidth;
        }
    }
}
