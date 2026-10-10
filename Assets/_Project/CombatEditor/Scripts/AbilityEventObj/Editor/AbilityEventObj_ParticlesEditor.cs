using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_Particles))]
    public class AbilityEventObj_ParticlesEditor : AbilityEventObj_CreateObjWithHandleEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUI.EndDisabledGroup();

            DrawPropertiesExcluding(serializedObject, "m_Script", "EnableCollision", "Collision", "HitProfile");
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("EnableCollision"));
            if (serializedObject.FindProperty("EnableCollision").boolValue)
            {
                DrawCollisionSettings();
                CombatHitProfileEditorUtility.DrawReference(serializedObject);
                var profile = serializedObject.FindProperty("HitProfile").objectReferenceValue as CombatHitProfile;
                if (profile != null)
                    EditorGUILayout.LabelField("碰撞体延迟", $"{Mathf.Max(0, profile.ParticleCollisionDelayFrames)} 帧（在受击配置 SO 中修改）");
            }

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            DrawTransformEditButtons();
            DrawCollisionPreviewButton();

            if (changed && CombatEditorUtility.EditorExist())
                CombatEditorUtility.GetCurrentEditor().HardResetPreviewToCurrentFrame();
        }

        private void DrawCollisionPreviewButton()
        {
            var source = (AbilityEventObj_Particles)target;
            if (!source.EnableCollision) return;
            var editor = CombatEditorUtility.GetCurrentEditor();
            var ability = editor != null ? editor.SelectedAbilityObj : null;
            var entry = ability != null ? ability.events.Find(e => e != null && e.Obj == source) : null;
            bool canPreview = !EditorApplication.isPlaying && source.IsActive && entry != null &&
                ability.Clip != null && source.ObjData != null && source.ObjData.TargetObj != null;
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                if (GUILayout.Button("预览碰撞体"))
                {
                    editor.OnStopPlayAnimation();
                    if (!entry.Previewable)
                    {
                        Undo.RecordObject(ability, "Preview Particle Collider");
                        entry.Previewable = true;
                        EditorUtility.SetDirty(ability);
                    }
                    int frame = CombatTimeline.ToFrame(entry.GetEventStartTime(), ability.Clip) + source.CollisionDelayFrames;
                    editor.OnSetPointerOnTrack(frame);
                    editor.HardResetPreviewToCurrentFrame();
                    editor.Repaint();
                    SceneView.lastActiveSceneView?.Focus();
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.HelpBox(canPreview
                ? "点击预览碰撞体会打开轨道预览并跳到碰撞体延迟结束帧。在 Scene 视图中拖动绿色轮廓可调整尺寸；Pos / Rot 调整时也会显示碰撞体。"
                : "请在动作编辑器中选中此粒子轨道、启用轨道，并指定粒子 Prefab 后预览碰撞体。", MessageType.Info);
        }

        private void DrawCollisionSettings()
        {
            var collision = serializedObject.FindProperty("Collision");
            EditorGUILayout.PropertyField(collision.FindPropertyRelative("Shape"), new GUIContent("形状"));
            EditorGUILayout.PropertyField(collision.FindPropertyRelative("Offset"), new GUIContent("中心偏移"));
            EditorGUILayout.PropertyField(collision.FindPropertyRelative("Rotation"), new GUIContent("局部旋转"));
            var shape = (ParticleColliderShape)collision.FindPropertyRelative("Shape").enumValueIndex;
            if (shape == ParticleColliderShape.Box)
                EditorGUILayout.PropertyField(collision.FindPropertyRelative("BoxSize"), new GUIContent("尺寸"));
            else
            {
                EditorGUILayout.PropertyField(collision.FindPropertyRelative("Radius"), new GUIContent("半径"));
                if (shape == ParticleColliderShape.Capsule)
                {
                    EditorGUILayout.PropertyField(collision.FindPropertyRelative("Height"), new GUIContent("高度"));
                    var direction = collision.FindPropertyRelative("CapsuleDirection");
                    direction.intValue = EditorGUILayout.Popup("轴向",
                        direction.intValue, new[] { "X", "Y", "Z" });
                }
            }
        }
    }
}
