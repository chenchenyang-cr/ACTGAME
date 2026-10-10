using System.Collections.Generic;
using Cinemachine;
using CombatEditor;
using UnityEditor;
using UnityEngine;

namespace CombatCamera.Editor
{
    public sealed partial class CameraShotWindow
    {
        private readonly CameraShotPreviewRig simulation = new();
        private bool simulationReady, editComposition, navigating;
        private int simulationVersion, navigationUndo;
        private CinemachineFreeLook simulationSource;
        private int sourceVersion;
        private float simulationStart, simulationDuration, flySpeed = 3f;
        private readonly HashSet<KeyCode> heldKeys = new();
        private CameraShotPose navigationPose;
        private CameraShotTiming ClipTiming => actionEvent != null ? actionEvent.Timing : default;
        private CombatEditor.CombatEditor ActionEditor => CombatEditorUtility.EditorExist() ? CombatEditorUtility.GetCurrentEditor() : null;

        private CinemachineFreeLook ResolveRig()
        {
            var active = source != null ? source.GetComponent<CinemachineBrain>()?.ActiveVirtualCamera as CinemachineFreeLook : null;
            if (active != null) return active;
            CinemachineFreeLook best = null;
            foreach (var candidate in FindObjectsOfType<CinemachineFreeLook>())
                if ((candidate.hideFlags & HideFlags.DontSave) == 0 && candidate.isActiveAndEnabled &&
                    (best == null || candidate.Priority > best.Priority)) best = candidate;
            return best;
        }

        private void UpdateSimulation()
        {
            var preview = timeline ? TimelinePreview : null;
            var editor = ActionEditor;
            bool hasAction = preview != null && preview.AnimObj != null && preview.AnimObj.Clip != null &&
                editor != null && editor._previewer != null && editor._previewer._combatController != null &&
                editor._previewer._combatController.transform == actor;
            float clipLength = hasAction ? preview.AnimObj.Clip.length : duration;
            float start = hasAction ? preview.eve.GetEventStartTime() * clipLength : 0;
            float time = hasAction ? CombatGlobalEditorValue.Percentage * clipLength : elapsed;
            int version = EditorUtility.GetDirtyCount(profile) ^ ClipTiming.GetHashCode();
            var currentRig = SourceRig;
            int currentSourceVersion = currentRig != null ? EditorUtility.GetDirtyCount(currentRig) : 0;
            if (simulationSource != currentRig || sourceVersion != currentSourceVersion)
            { simulationReady = false; simulationSource = currentRig; sourceVersion = currentSourceVersion; }
            if (!simulationReady)
            {
                simulation.Create(SourceRig, actor); simulationReady = true;
                simulationVersion = version; simulationStart = start; simulationDuration = duration;
            }
            if (simulationVersion != version || simulationStart != start || simulationDuration != duration)
            {
                simulation.Reset(); simulationVersion = version; simulationStart = start; simulationDuration = duration;
            }
            System.Action<float> sample = hasAction
                ? seconds => editor._previewer.UpdateAnimationInEditMode(Mathf.Clamp01(seconds / clipLength)) : null;
            simulation.Evaluate(profile, ClipTiming, actor, time, start, duration, SourcePose(), sample);
        }

        private void DrawPlaybackControls()
        {
            EditorGUI.BeginChangeCheck();
            int mode = GUILayout.Toolbar(timeline ? 0 : 1, new[] { "动作与镜头", "仅镜头（冻结角色）" });
            if (EditorGUI.EndChangeCheck())
            {
                ActionEditor?.OnPausePlay(); playing = false; timeline = mode == 0; elapsed = progress = 0;
                ResetBasis();
            }
            editComposition = GUILayout.Toolbar(editComposition ? 0 : 1, new[] { "编辑目标构图", "预览最终效果" }) == 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                bool actionPlaying = timeline && ActionEditor != null && (ActionEditor.IsPlaying || ActionEditor.IsLooping);
                if (GUILayout.Button(actionPlaying || playing ? "暂停" : "播放 / 循环"))
                {
                    if (actionPlaying || playing) { ActionEditor?.OnPausePlay(); playing = false; }
                    else
                    {
                        editComposition = false;
                        if (timeline && ActionEditor != null) ActionEditor.OnStartLoop();
                        else { playing = true; elapsed = 0; }
                        ResetBasis();
                    }
                }
                if (GUILayout.Button("回到镜头开始")) SeekShot(0);
            }
            if (SourceRig == null)
                EditorGUILayout.HelpBox("未找到基础 FreeLook。请在预览绑定中指定，以预览跟随、缓冲和镜头接续。", MessageType.Warning);
            EditorGUILayout.LabelField("预览内：右键观察 + WASD / QE 移动；中键平移；滚轮推拉；Alt + 左键环绕；Shift 加速。", EditorStyles.wordWrappedMiniLabel);
            flySpeed = EditorGUILayout.Slider("摆机位速度", flySpeed, .1f, 20f);
        }

        private void SeekShot(float value)
        {
            ActionEditor?.OnPausePlay(); playing = false;
            progress = Mathf.Clamp01(value); elapsed = progress * duration;
            var preview = TimelinePreview;
            if (timeline && preview != null && preview.eve != null && ActionEditor != null)
            {
                float time = Mathf.Lerp(preview.eve.GetEventStartTime(), preview.eve.GetEventEndTime(), progress);
                ActionEditor.OnPreviewAnimationAtPercentage(time);
                ActionEditor.OnSetPointerOnTrack(Mathf.RoundToInt(time * preview.AnimObj.Clip.length * CombatTimeline.FramesPerSecond));
                ActionEditor.Repaint();
            }
            simulation.Reset(); Repaint();
        }

        private void AdoptSceneView()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null || !Evaluate()) return;
            if (view.orthographic) { ShowNotification(new GUIContent("请先将 Scene 切换为透视视图")); return; }
            ActionEditor?.OnPausePlay(); playing = false; editComposition = true;
            WritePose(new CameraShotPose { Position = view.camera.transform.position,
                Rotation = view.camera.transform.rotation, FieldOfView = view.camera.fieldOfView }, true);
        }

        private void WritePose(CameraShotPose desired, bool undo)
        {
            if (profile == null || actor == null) return;
            if (undo) Undo.RecordObject(profile, "Edit camera shot composition");
            Vector3 localPosition = session.ToPositionOffset(desired.Position);
            Vector3 localRotation = (Quaternion.Inverse(session.Frame) * desired.Rotation).eulerAngles;
            bool constrained = profile.Composition == CameraShotComposition.ActorStartPose && (profile.LookAtPlayer || profile.LimitToGameCameraOrbit);
            if (profile.UseKeyframes)
            {
                int index = profile.Keys.FindIndex(k => Mathf.Abs(k.Time - progress) < .0001f);
                var key = new CameraShotKey { Time = progress, Position = localPosition,
                    Rotation = constrained ? profile.SampleMotion(progress).Rotation.eulerAngles : localRotation,
                    FieldOfView = desired.FieldOfView };
                if (index < 0) profile.Keys.Add(key); else profile.Keys[index] = key;
                profile.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
            }
            else
            {
                float travel = profile.MotionCurve != null ? profile.MotionCurve.Evaluate(progress) : progress;
                localPosition -= profile.PositionTravel * travel;
                if (profile.Composition == CameraShotComposition.ActorStartPose)
                {
                    profile.TargetPosition = localPosition;
                    if (!constrained) profile.TargetRotation = localRotation;
                }
                else { profile.CameraPositionOffset = localPosition; profile.CameraRotationOffset = localRotation; }
                profile.FieldOfView = desired.FieldOfView;
            }
            profile.OverrideFieldOfView = true;
            EditorUtility.SetDirty(profile); CameraShotProfileEditor.QueueSave(profile);
            simulation.Reset(); Repaint(); SceneView.RepaintAll();
        }

        private void DrawKeyframes()
        {
            if (profile == null || actor == null) return;
            if (!profile.UseKeyframes)
            {
                if (GUILayout.Button("将当前运镜复制为起止关键帧"))
                {
                    var a = profile.SampleMotion(0); var b = profile.SampleMotion(1);
                    Undo.RecordObject(profile, "Create camera shot keys");
                    profile.Keys = new List<CameraShotKey> {
                        new CameraShotKey { Time = 0, Position = a.Position, Rotation = a.Rotation.eulerAngles, FieldOfView = a.FieldOfView },
                        new CameraShotKey { Time = 1, Position = b.Position, Rotation = b.Rotation.eulerAngles, FieldOfView = b.FieldOfView }
                    };
                    profile.UseKeyframes = true; EditorUtility.SetDirty(profile); CameraShotProfileEditor.QueueSave(profile); simulation.Reset();
                }
                return;
            }
            EditorGUILayout.LabelField("运镜关键帧（点击定位；摆机位自动记录当前帧）", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float fov = EditorGUILayout.Slider("当前帧 FOV", profile.SampleMotion(progress).FieldOfView, 1, 179);
            if (EditorGUI.EndChangeCheck() && Evaluate())
            { var desired = pose; desired.FieldOfView = fov; WritePose(desired, true); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加 / 更新当前关键帧") && Evaluate()) WritePose(pose, true);
                if (GUILayout.Button("删除当前关键帧"))
                {
                    Undo.RecordObject(profile, "Remove camera shot key");
                    profile.Keys.RemoveAll(k => Mathf.Abs(k.Time - progress) < .0001f);
                    EditorUtility.SetDirty(profile); CameraShotProfileEditor.QueueSave(profile); simulation.Reset();
                }
            }
            for (int i = 0; i < profile.Keys.Count; i++)
            {
                var key = profile.Keys[i];
                if (GUILayout.Button($"关键帧 {i + 1}   {key.Time * duration:0.000} 秒 / {key.Time:P0}"))
                { SeekShot(key.Time); editComposition = true; }
            }
        }

        private void BeginNavigation()
        {
            if (navigating || !Evaluate()) return;
            ActionEditor?.OnPausePlay(); playing = false; editComposition = true;
            navigationPose = pose; navigating = true; heldKeys.Clear();
            Undo.IncrementCurrentGroup(); navigationUndo = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(profile, "Move camera shot viewpoint");
        }

        private void EndNavigation()
        {
            if (navigating) Undo.CollapseUndoOperations(navigationUndo);
            navigating = false; heldKeys.Clear();
        }

        private void HandleViewport(Rect rect)
        {
            if (profile == null || actor == null) return;
            var e = Event.current;
            int id = GUIUtility.GetControlID("CameraShotViewport".GetHashCode(), FocusType.Keyboard, rect);
            bool inside = rect.Contains(e.mousePosition);
            if (e.type == EventType.MouseDown && inside && (e.button == 1 || e.button == 2 || (e.button == 0 && e.alt)))
            { BeginNavigation(); GUIUtility.hotControl = id; GUIUtility.keyboardControl = id; e.Use(); }
            if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
            { GUIUtility.hotControl = 0; EndNavigation(); e.Use(); }
            if (e.type == EventType.KeyDown && navigating)
            {
                if (e.keyCode == KeyCode.Escape) { GUIUtility.hotControl = 0; EndNavigation(); }
                else heldKeys.Add(e.keyCode);
                e.Use();
            }
            if (e.type == EventType.KeyUp && navigating) { heldKeys.Remove(e.keyCode); e.Use(); }
            if (e.type == EventType.ScrollWheel && inside)
            {
                BeginNavigation(); navigationPose.Position += navigationPose.Rotation * Vector3.forward * (-e.delta.y * flySpeed * .08f);
                WritePose(navigationPose, false); EndNavigation(); e.Use();
            }
            if (e.type == EventType.MouseDrag && navigating && GUIUtility.hotControl == id)
            {
                bool constrained = profile.Composition == CameraShotComposition.ActorStartPose && (profile.LookAtPlayer || profile.LimitToGameCameraOrbit);
                if ((e.button == 0 && e.alt) || (e.button == 1 && constrained))
                {
                    Vector3 focus = actor.position + Vector3.up * 1.2f;
                    var rotation = Quaternion.AngleAxis(e.delta.x * .25f, Vector3.up) * Quaternion.AngleAxis(-e.delta.y * .25f, navigationPose.Rotation * Vector3.right);
                    navigationPose.Position = focus + rotation * (navigationPose.Position - focus);
                    navigationPose.Rotation = rotation * navigationPose.Rotation;
                }
                else if (e.button == 1)
                    navigationPose.Rotation = Quaternion.AngleAxis(e.delta.x * .2f, Vector3.up) * navigationPose.Rotation * Quaternion.Euler(-e.delta.y * .2f, 0, 0);
                else if (e.button == 2)
                    navigationPose.Position += navigationPose.Rotation * new Vector3(-e.delta.x, e.delta.y, 0) * flySpeed * .003f;
                WritePose(navigationPose, false); e.Use();
            }
            if (profile.Composition == CameraShotComposition.ActorStartPose && (profile.LookAtPlayer || profile.LimitToGameCameraOrbit))
                GUI.Label(new Rect(rect.x + 5, rect.y + 5, rect.width - 10, 35), "当前有构图约束：右键围绕角色转动；自由朝向需关闭自动看向 / 轨道限制。", EditorStyles.whiteMiniLabel);
        }

        private void Navigate(float dt)
        {
            if (!navigating || profile == null) return;
            Vector3 direction = new Vector3((heldKeys.Contains(KeyCode.D) ? 1 : 0) - (heldKeys.Contains(KeyCode.A) ? 1 : 0),
                (heldKeys.Contains(KeyCode.E) ? 1 : 0) - (heldKeys.Contains(KeyCode.Q) ? 1 : 0),
                (heldKeys.Contains(KeyCode.W) ? 1 : 0) - (heldKeys.Contains(KeyCode.S) ? 1 : 0));
            if (direction == Vector3.zero) return;
            float speed = flySpeed * (heldKeys.Contains(KeyCode.LeftShift) || heldKeys.Contains(KeyCode.RightShift) ? 3 : 1);
            navigationPose.Position += navigationPose.Rotation * direction.normalized * speed * Mathf.Min(dt, .05f);
            WritePose(navigationPose, false);
        }
    }
}
