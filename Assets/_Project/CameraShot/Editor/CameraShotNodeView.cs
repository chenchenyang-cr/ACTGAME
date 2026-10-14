using System;
using Cinemachine;
using UnityEditor;
using UnityEngine;

namespace CombatCamera.Editor
{
    // Drawn inside the action editor's selected node, or an asset Inspector.
    public sealed class CameraShotNodeView : IDisposable
    {
        public int PreviewCandidate = -1;
        private readonly CameraShotOrbitSolver probe = new CameraShotOrbitSolver();
        private Camera renderCamera;
        private RenderTexture texture;
        private bool advanced;

        public bool DrawProfile(SerializedObject serialized)
        {
            serialized.Update();
            var candidates = serialized.FindProperty("Candidates");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUILayout.LabelField("CameraShot · 轨道机位", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{candidates.arraySize} 个候选机位 · 开始时选择最近机位", EditorStyles.miniLabel);
                EditorGUILayout.HelpBox("水平角度：0° 后方、90° 右侧、180° 前方、270° 左侧。镜头距离和高度范围采用原 FreeLook 轨道。", MessageType.None);
                for (int i = 0; i < candidates.arraySize; i++) {
                    var candidate = candidates.GetArrayElementAtIndex(i);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                        using (new EditorGUILayout.HorizontalScope()) {
                            EditorGUILayout.LabelField($"机位 {i + 1}", EditorStyles.boldLabel, GUILayout.Width(50));
                            EditorGUILayout.PropertyField(candidate.FindPropertyRelative("Name"), GUIContent.none);
                            if (GUILayout.Button("×", GUILayout.Width(22))) {
                                candidates.DeleteArrayElementAtIndex(i);
                                if (PreviewCandidate >= candidates.arraySize) PreviewCandidate = -1;
                                break;
                            }
                        }
                        var direction = candidate.FindPropertyRelative("RelativePosition");
                        float angle = CameraShotCandidate.AngleFromDirection(direction.vector2Value);
                        EditorGUI.BeginChangeCheck();
                        float editedAngle = EditorGUILayout.Slider(new GUIContent("水平角度（°）"), angle, 0f, 360f);
                        if (EditorGUI.EndChangeCheck())
                            direction.vector2Value = CameraShotCandidate.DirectionFromAngle(editedAngle);
                        EditorGUILayout.PropertyField(candidate.FindPropertyRelative("Height"), new GUIContent("镜头高度（米）"));
                        bool viewing = PreviewCandidate == i;
                        if (GUILayout.Toggle(viewing, viewing ? "正在查看此机位" : "查看此机位", "Button")) PreviewCandidate = i;
                    }
                }
                if (GUILayout.Button("＋ 添加候选机位")) {
                    int index = candidates.arraySize;
                    candidates.InsertArrayElementAtIndex(index);
                    var created = candidates.GetArrayElementAtIndex(index);
                    created.FindPropertyRelative("Name").stringValue = $"机位 {index + 1}";
                    created.FindPropertyRelative("RelativePosition").vector2Value = new Vector2(index % 2 == 0 ? 1 : -1, -3);
                    created.FindPropertyRelative("Height").floatValue = 1.55f;
                }
                if (candidates.arraySize == 0) EditorGUILayout.HelpBox("请添加至少一个机位。空节点不会接管相机。", MessageType.Warning);
                if (GUILayout.Toggle(PreviewCandidate < 0, "预览实际选择与过渡", "Button")) PreviewCandidate = -1;
                advanced = EditorGUILayout.Foldout(advanced, "接管设置", true);
                if (advanced) {
                    EditorGUILayout.PropertyField(serialized.FindProperty("Priority"), new GUIContent("优先级"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("LockCameraInput"), new GUIContent("锁定相机输入"));
                }
            }
            bool changed = serialized.ApplyModifiedProperties();
            if (changed) AssetDatabase.SaveAssetIfDirty(serialized.targetObject);
            return changed;
        }

        public static void DrawTiming(SerializedProperty timing, SerializedProperty holdSeconds = null, bool returnToOriginalCamera = true)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUILayout.LabelField("转入", EditorStyles.boldLabel);
                DrawTransition(timing.FindPropertyRelative("EnterSeconds"), timing.FindPropertyRelative("EnterCurve"));
                if (holdSeconds != null) {
                    EditorGUILayout.LabelField("保持机位", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(holdSeconds, new GUIContent("时长（秒）"));
                    holdSeconds.floatValue = Mathf.Max(0, holdSeconds.floatValue);
                }
                if (returnToOriginalCamera) {
                    EditorGUILayout.LabelField("转出", EditorStyles.boldLabel);
                    DrawTransition(timing.FindPropertyRelative("ExitSeconds"), timing.FindPropertyRelative("ExitCurve"));
                }
                EditorGUILayout.LabelField("曲线 X：过渡进度 · Y：完成程度（0 → 1）", EditorStyles.wordWrappedMiniLabel);
            }
        }
        private static void DrawTransition(SerializedProperty seconds, SerializedProperty curve)
        {
            EditorGUILayout.PropertyField(seconds, new GUIContent("时长（秒）"));
            seconds.floatValue = Mathf.Max(0, seconds.floatValue);
            EditorGUILayout.CurveField(curve, Color.cyan, new Rect(0, 0, 1, 1), new GUIContent("动画曲线"), GUILayout.Height(42));
        }

        public void DrawPreview(CameraShotProfile profile, Transform actor, CinemachineFreeLook rig,
            CameraShotPose? timelinePose = null, int selectedCandidate = -1)
        {
            if (profile == null || actor == null || rig == null || rig.Follow == null) {
                EditorGUILayout.HelpBox("在动作编辑器选择角色，并绑定带 CameraShot 扩展的基础 FreeLook 后预览。", MessageType.Info);
                return;
            }
            CameraShotPose pose;
            if (PreviewCandidate >= 0 && PreviewCandidate < profile.Candidates.Count) {
                var candidate = profile.Candidates[PreviewCandidate];
                if (!probe.TryCandidate(rig, actor, candidate, out _, out pose)) return;
                EditorGUILayout.LabelField($"查看机位：{candidate.Name}", EditorStyles.miniLabel);
            }
            else {
                pose = timelinePose ?? CameraShotPose.FromState(rig.State);
                string name = selectedCandidate >= 0 && selectedCandidate < profile.Candidates.Count
                    ? profile.Candidates[selectedCandidate].Name : "等待片段开始";
                EditorGUILayout.LabelField($"实际选择：{name}", EditorStyles.miniLabel);
            }
            var brain = CinemachineCore.Instance.FindPotentialTargetBrain(rig);
            var sourceCamera = brain != null ? brain.OutputCamera : Camera.main;
            if (sourceCamera == null) return;
            var rect = GUILayoutUtility.GetAspectRect(sourceCamera.aspect > 0 ? sourceCamera.aspect : 16f / 9f, GUILayout.MinHeight(100));
            if (Event.current.type != EventType.Repaint) return;
            if (renderCamera == null) {
                var go = new GameObject("CameraShot node preview") { hideFlags = HideFlags.HideAndDontSave };
                renderCamera = go.AddComponent<Camera>(); renderCamera.enabled = false;
                // Inherit the renderer and post-processing, without the scene's UI stack.
                var sourceData = sourceCamera.GetComponent("UniversalAdditionalCameraData");
                if (sourceData != null) {
                    var data = go.AddComponent(sourceData.GetType());
                    EditorUtility.CopySerialized(sourceData, data);
                    var renderType = sourceData.GetType().GetProperty("renderType");
                    if (renderType != null) renderType.SetValue(data, Enum.Parse(renderType.PropertyType, "Base"));
                    var stack = sourceData.GetType().GetProperty("cameraStack")?.GetValue(data) as System.Collections.IList;
                    stack?.Clear();
                }
            }
            int width = Mathf.Clamp(Mathf.RoundToInt(rect.width * EditorGUIUtility.pixelsPerPoint), 160, 640);
            int height = Mathf.Max(90, Mathf.RoundToInt(width / sourceCamera.aspect));
            if (texture == null || texture.width != width || texture.height != height) {
                ReleaseTexture(); texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf) { hideFlags = HideFlags.HideAndDontSave };
                texture.Create();
            }
            renderCamera.CopyFrom(sourceCamera); renderCamera.enabled = false;
            renderCamera.targetTexture = texture;
            renderCamera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            renderCamera.Render(); GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
        }
        private void ReleaseTexture() { if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null; } }
        public void Dispose() { probe.Dispose(); ReleaseTexture(); if (renderCamera != null) UnityEngine.Object.DestroyImmediate(renderCamera.gameObject); renderCamera = null; }
    }

    [CustomEditor(typeof(CameraShotProfile))]
    public sealed class CameraShotProfileEditor : UnityEditor.Editor
    {
        private readonly CameraShotNodeView node = new CameraShotNodeView();
        private void OnDisable() => node.Dispose();
        public override void OnInspectorGUI() => node.DrawProfile(serializedObject);
    }
}
