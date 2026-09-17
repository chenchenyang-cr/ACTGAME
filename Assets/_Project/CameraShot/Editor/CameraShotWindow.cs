using Cinemachine;
using CombatEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CombatCamera.Editor
{
    public sealed class CameraShotWindow : EditorWindow
    {
        [SerializeField] private CameraShotProfile profile;
        [SerializeField] private AbilityEventObj_CameraShot actionEvent;
        [SerializeField] private Transform actor, target;
        [SerializeField] private Camera source;
        [SerializeField] private bool timeline = true, handles = true;
        [SerializeField] private bool rotateCamera;
        [SerializeField] private float progress, duration = .5f, elapsed;
        private readonly CameraShotRuntime.PreviewSession session = new();
        private readonly CameraShotReturnPreview returnPreview = new();
        private Camera previewCamera;
        private RenderTexture texture;
        private UnityEditor.Editor profileEditor;
        private CameraShotPose pose;
        private Vector3 initialPosition, initialActorPosition;
        private Quaternion initialRotation = Quaternion.identity;
        private float initialFov = 55;
        private bool initialized, playing;
        private double previousTime, renderTime;
        private Vector2 scroll;
        private string renderError;
        private float PreviewAspect => source != null ? Mathf.Clamp(source.aspect, .2f, 5f) : 16f / 9f;

        [System.Serializable]
        private sealed class WindowSettings
        {
            public string actorId, targetId, sourceId, eventId;
            public bool timeline = true, handles = true, rotateCamera;
            public float duration = .5f, elapsed;
        }
        private static string SettingsPrefix => "UnityLearning.CameraShot." + Application.dataPath + ".";
        private string SettingsKey => SettingsPrefix + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile));
        private static string ObjectId(Object value) => value != null &&
            (value.hideFlags & HideFlags.DontSave) == 0
                ? GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() : "";
        private static T Resolve<T>(string id) where T : Object =>
            !string.IsNullOrEmpty(id) && GlobalObjectId.TryParse(id, out var parsed)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as T : null;

        private void SaveSettings()
        {
            if (profile == null || !AssetDatabase.Contains(profile) || Application.isPlaying) return;
            EditorPrefs.SetString(SettingsPrefix + "LastProfile", ObjectId(profile));
            EditorPrefs.SetString(SettingsKey, JsonUtility.ToJson(new WindowSettings {
                actorId = ObjectId(actor), targetId = ObjectId(target), sourceId = ObjectId(source), eventId = ObjectId(actionEvent),
                timeline = timeline, handles = handles, rotateCamera = rotateCamera,
                duration = duration, elapsed = elapsed
            }));
        }
        private void RestoreSettings()
        {
            if (profile == null || !EditorPrefs.HasKey(SettingsKey)) return;
            var saved = JsonUtility.FromJson<WindowSettings>(EditorPrefs.GetString(SettingsKey));
            actor = Resolve<Transform>(saved.actorId); target = Resolve<Transform>(saved.targetId);
            source = Resolve<Camera>(saved.sourceId); actionEvent = Resolve<AbilityEventObj_CameraShot>(saved.eventId);
            timeline = saved.timeline; handles = saved.handles; rotateCamera = saved.rotateCamera;
            duration = Mathf.Max(.001f, saved.duration); elapsed = saved.elapsed;
            progress = Mathf.Clamp01(elapsed / duration);
        }
        private void OnLostFocus() { SaveSettings(); CameraShotProfileEditor.FlushPendingSaves(); }

        [MenuItem("Tools/Camera/Camera Shot Editor")]
        public static void Open() => GetWindow<CameraShotWindow>("CameraShot");
        public static void Open(CameraShotProfile p, AbilityEventObj_CameraShot e = null)
        {
            var window = GetWindow<CameraShotWindow>("CameraShot");
            bool changed = window.profile != p || (e != null && window.actionEvent != e);
            if (changed)
            {
                window.SaveSettings(); CameraShotProfileEditor.FlushPendingSaves();
                window.profile = p;
                window.actor = window.target = null; window.source = null;
                window.actionEvent = null;
                window.RestoreSettings();
                if (e != null) window.actionEvent = e;
                if (window.actor == null) window.BindTimeline();
                window.ResetBasis();
            }
            window.Show();
        }
        private void OnEnable()
        {
            if (profile == null) profile = Resolve<CameraShotProfile>(EditorPrefs.GetString(SettingsPrefix + "LastProfile", ""));
            RestoreSettings();
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            SceneView.duringSceneGui += DrawHandles;
            Undo.undoRedoPerformed += UndoRedo;
            previousTime = EditorApplication.timeSinceStartup;
        }
        private void OnDisable()
        {
            SaveSettings(); CameraShotProfileEditor.FlushPendingSaves();
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            SceneView.duringSceneGui -= DrawHandles;
            Undo.undoRedoPerformed -= UndoRedo;
            Cleanup();
            if (profileEditor != null) DestroyImmediate(profileEditor);
        }
        private void PlayModeChanged(PlayModeStateChange state) { playing = false; Cleanup(); ResetBasis(); }
        private void Cleanup()
        {
            returnPreview.Dispose();
            if (previewCamera != null) DestroyImmediate(previewCamera.gameObject);
            if (texture != null) { texture.Release(); DestroyImmediate(texture); }
        }
        private void ResetBasis()
        {
            returnPreview.Dispose();
            initialized = false; session.Reset(); Repaint(); SceneView.RepaintAll();
        }
        private void UndoRedo()
        {
            CameraShotProfileEditor.QueueSave(profile);
            if (actionEvent != null && profile != actionEvent.Profile) profile = actionEvent.Profile;
            ResetBasis();
        }
        private AbilityEventPreview_CameraShot TimelinePreview
        {
            get
            {
                var p = AbilityEventPreview_CameraShot.Find(actionEvent);
                return p != null && (actionEvent == null || p._EventObj == actionEvent) ? p : null;
            }
        }
        private void BindTimeline()
        {
            var p = TimelinePreview;
            if (p == null || p._combatController == null) return;
            actor = p._combatController.transform;
            var e = (AbilityEventObj_CameraShot)p._EventObj;
            if (target == null) target = e.ResolveTarget(p._combatController);
            if (profile == null) profile = e.Profile;
        }
        private bool Evaluate()
        {
            // Animation preview objects are recreated after play mode / script reload.
            // Rebind here too: Scene GUI can run before the next editor update.
            if (actor == null) BindTimeline();
            if (profile == null || actor == null) return false;
            if (source == null) source = Camera.main;
            if (profile.Composition == CameraShotComposition.GameCameraRelative && source == null) return false;
            if (!initialized)
            {
                if (source == null) source = Camera.main;
                var reference = source != null ? source : SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
                initialRotation = reference != null ? reference.transform.rotation : actor.rotation;
                initialPosition = reference != null ? reference.transform.position : actor.position - actor.forward * 5 + Vector3.up * 2;
                initialFov = reference != null ? reference.fieldOfView : 55;
                initialActorPosition = actor.position;
                initialized = true;
            }
            Quaternion referenceRotation = profile.FollowRotation && source != null ? source.transform.rotation : initialRotation;
            var action = timeline ? TimelinePreview : null;
            if (action != null && action.HasStartPose && profile.Composition != CameraShotComposition.GameCameraRelative)
            {
                var actionTarget = ((AbilityEventObj_CameraShot)action._EventObj).ResolveTarget(action._combatController);
                session.CaptureStart(profile, actor, target, action.StartPosition, action.StartForward,
                    target != null ? (target == actionTarget ? action.StartTarget ?? target.position : target.position) : null, initialRotation);
            }
            var basis = SourcePose();
            pose = session.Evaluate(profile, actor, target,
                profile.Composition == CameraShotComposition.GameCameraRelative ? basis.Rotation : referenceRotation,
                basis.FieldOfView, progress, basis.Position);
            return true;
        }
        private CameraShotPose SourcePose()
        {
            if (source != null)
            {
                var brain = source.GetComponent<CinemachineBrain>();
                var active = brain != null ? brain.ActiveVirtualCamera : null;
                var extension = active != null && active.VirtualCameraGameObject != null
                    ? active.VirtualCameraGameObject.GetComponent<CinemachineCameraShotExtension>() : null;
                if (CameraShotRuntime.ActiveCount > 0 && extension != null && extension.isActiveAndEnabled && extension.HasBasePose)
                    return extension.BasePose;
                return new CameraShotPose { Position = source.transform.position,
                    Rotation = source.transform.rotation, FieldOfView = source.fieldOfView };
            }
            return new CameraShotPose { Position = initialPosition + actor.position - initialActorPosition,
                Rotation = initialRotation, FieldOfView = initialFov };
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - previousTime); previousTime = now;
            if (Application.isPlaying) return;
            if (timeline && TimelinePreview != null)
            {
                var p = TimelinePreview;
                if (actor != p._combatController?.transform) { BindTimeline(); ResetBasis(); }
                if (p.eve != null && p.AnimObj != null && p.AnimObj.Clip != null)
                {
                    duration = Mathf.Max(.001f, (p.eve.GetEventEndTime() - p.eve.GetEventStartTime()) * p.AnimObj.Clip.length);
                    elapsed = (CombatGlobalEditorValue.Percentage - p.eve.GetEventStartTime()) * p.AnimObj.Clip.length;
                    progress = Mathf.Clamp01(elapsed / duration);
                }
            }
            else if (playing)
            {
                elapsed += dt;
                if (elapsed > duration) { elapsed = 0; session.Reset(); }
                progress = Mathf.Clamp01(elapsed / Mathf.Max(.001f, duration));
            }
            if (now - renderTime < 1.0 / 30.0) return;
            renderTime = now;
            if (!Evaluate()) return;
            Render(); Repaint();
        }
        private float Weight() => profile != null &&
            !(timeline && TimelinePreview != null && (elapsed < 0f || elapsed >= duration))
                ? profile.EvaluateWeight(progress) : 0f;
        private void Render()
        {
            if (previewCamera == null)
            {
                var go = new GameObject("CameraShot authoring preview") { hideFlags = HideFlags.HideAndDontSave };
                previewCamera = go.AddComponent<Camera>(); previewCamera.enabled = false;
            }
            int height = Mathf.RoundToInt(960 / PreviewAspect);
            if (texture == null || texture.height != height)
            {
                if (texture != null) { texture.Release(); DestroyImmediate(texture); }
                texture = new RenderTexture(960, height, 24) { hideFlags = HideFlags.HideAndDontSave };
                texture.Create();
            }
            if (source != null) previewCamera.CopyFrom(source);
            previewCamera.enabled = false;
            previewCamera.targetTexture = texture; previewCamera.aspect = PreviewAspect;
            float weight = Weight();
            var state = CameraState.Default;
            var basis = SourcePose();
            Vector3 basePosition = basis.Position;
            Quaternion baseRotation = basis.Rotation;
            float baseFov = basis.FieldOfView;
            state.RawPosition = Vector3.Lerp(basePosition, pose.Position, weight);
            state.RawOrientation = Quaternion.Slerp(baseRotation, pose.Rotation, weight);
            var lens = state.Lens; lens.FieldOfView = Mathf.Lerp(baseFov, pose.FieldOfView, weight); state.Lens = lens;
            if (profile.TryGetReturnBlend(progress, out float returnStart, out float returnBlend))
            {
                var brain = source != null ? source.GetComponent<CinemachineBrain>() : null;
                var rig = brain != null ? brain.ActiveVirtualCamera as CinemachineFreeLook : null;
                var startPose = session.Evaluate(profile, actor, target, basis.Rotation, basis.FieldOfView, returnStart, basis.Position);
                var from = CameraShotReturnPreview.Blend(basis, startPose, profile.EvaluateWeight(returnStart));
                if (returnPreview.Evaluate(rig, from, out var destination))
                {
                    var returning = CameraShotReturnPreview.Blend(from, destination, returnBlend);
                    state.RawPosition = returning.Position; state.RawOrientation = returning.Rotation;
                    lens.FieldOfView = returning.FieldOfView; state.Lens = lens;
                }
                // Keep gizmos at the selected frame after evaluating the exit start.
                session.Evaluate(profile, actor, target, basis.Rotation, basis.FieldOfView, progress, basis.Position);
            }
            if (timeline)
            {
                // Reuse the game's shake composition when the action timeline has active shake sources.
                var extension = Object.FindObjectOfType<CinemachineCameraShakeExtension>();
                if (extension != null && extension.enabled) extension.ApplyShake(ref state);
            }
            previewCamera.transform.SetPositionAndRotation(state.CorrectedPosition, state.CorrectedOrientation);
            previewCamera.fieldOfView = state.Lens.FieldOfView;
            try
            {
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                {
                    var data = previewCamera.GetUniversalAdditionalCameraData();
                    var original = source != null ? source.GetComponent<UniversalAdditionalCameraData>() : null;
                    if (original != null) EditorUtility.CopySerialized(original, data);
                    data.renderType = CameraRenderType.Base;
                    RenderPipeline.SubmitRenderRequest(previewCamera,
                        new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                }
                else previewCamera.Render();
                renderError = null;
            }
            catch (System.Exception e) { renderError = e.Message; }
        }
        private void OnGUI()
        {
            if (Application.isPlaying) { EditorGUILayout.HelpBox("请退出 Play 模式后编辑镜头。", MessageType.Info); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginChangeCheck();
            var selectedProfile = (CameraShotProfile)EditorGUILayout.ObjectField("镜头配置", profile, typeof(CameraShotProfile), false);
            if (selectedProfile != profile)
            {
                SaveSettings(); CameraShotProfileEditor.FlushPendingSaves();
                profile = selectedProfile;
                if (actionEvent != null)
                {
                    Undo.RecordObject(actionEvent, "Change Camera Shot profile");
                    actionEvent.Profile = profile;
                    EditorUtility.SetDirty(actionEvent);
                    AssetDatabase.SaveAssetIfDirty(actionEvent);
                }
            }
            actor = (Transform)EditorGUILayout.ObjectField("玩家 / 预览角色", actor, typeof(Transform), true);
            target = (Transform)EditorGUILayout.ObjectField("敌人 / 目标", target, typeof(Transform), true);
            source = (Camera)EditorGUILayout.ObjectField("原游戏相机（融合起点）", source, typeof(Camera), true);
            if (EditorGUI.EndChangeCheck()) ResetBasis();
            timeline = EditorGUILayout.Toggle("跟随动作时间轴", timeline);
            if (timeline && TimelinePreview == null)
                EditorGUILayout.HelpBox("打开含 CameraShot 的动作预览，或关闭时间轴联动并手动指定角色。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            handles = EditorGUILayout.Toggle("Scene 拖拽手柄", handles);
            if (handles && profile != null && profile.Composition != CameraShotComposition.ActorAnchored)
                rotateCamera = GUILayout.Toolbar(rotateCamera ? 1 : 0, new[] { "移动目标相机", "旋转目标相机" }) == 1;
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            using (new EditorGUI.DisabledScope(profile == null || actor == null))
                if (GUILayout.Button("在 Scene 中定位目标相机手柄")) FrameTargetHandles();
            if (actor == null)
                EditorGUILayout.HelpBox("请指定场景中的玩家，或打开动作预览以绑定角色。", MessageType.Warning);
            if (!timeline || TimelinePreview == null)
            {
                progress = EditorGUILayout.Slider("轨道预览进度", progress, 0, 1);
                elapsed = progress * duration;
                if (GUILayout.Button(playing ? "暂停镜头" : "循环播放镜头")) playing = !playing;
            }
            else EditorGUILayout.LabelField("镜头进度", progress.ToString("P0"));
            if (profile != null) EditorGUILayout.LabelField("当前镜头权重", Weight().ToString("P0"));
            if (profile != null && Weight() < .001f)
                EditorGUILayout.HelpBox("当前权重为 0，画面显示游戏相机。将时间轴拖到曲线高处查看目标构图；Scene 手柄仍可编辑。", MessageType.Info);
            Rect rect = GUILayoutUtility.GetAspectRect(PreviewAspect, GUILayout.MinHeight(140));
            if (texture != null && profile != null && actor != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            else GUI.Box(rect, "指定镜头配置和玩家后显示实时画面");
            if (renderError != null) EditorGUILayout.HelpBox(renderError, MessageType.Error);
            using (new EditorGUI.DisabledScope(profile == null || actor == null))
            {
                using (new EditorGUI.DisabledScope(source == null))
                    if (GUILayout.Button("采用游戏相机构图")) AlignToGameCamera();
                if (profile != null && profile.Composition != CameraShotComposition.ActorStartPose &&
                    GUILayout.Button("转换为玩家固定构图")) BakeCurrentTarget();
            }
            EditorGUILayout.HelpBox("配置修改自动保存，支持 Undo。采用游戏相机构图或转换构图会重置位移路径。", MessageType.None);
            if (source == null) EditorGUILayout.HelpBox("请指定原游戏相机；相机相对模式不会使用 Scene 视图作为替代。", MessageType.Warning);
            if (profile != null)
            {
                UnityEditor.Editor.CreateCachedEditor(profile, typeof(CameraShotProfileEditor), ref profileEditor);
                EditorGUI.BeginChangeCheck();
                // Draw fields here without recursively opening another window.
                ((CameraShotProfileEditor)profileEditor).DrawFields();
                if (EditorGUI.EndChangeCheck()) { session.Reset(); SceneView.RepaintAll(); }
            }
            if (EditorGUI.EndChangeCheck()) SaveSettings();
            EditorGUILayout.EndScrollView();
        }
        private void AlignToGameCamera()
        {
            if (!Evaluate() || source == null) return;
            StoreTarget(SourcePose());
        }
        private void BakeCurrentTarget()
        {
            if (Evaluate()) StoreTarget(pose);
        }
        private void StoreTarget(CameraShotPose targetPose)
        {
            var action = timeline ? TimelinePreview : null;
            Vector3 forward = action != null && action.HasStartPose ? action.StartForward : actor.forward;
            forward.y = 0;
            Quaternion frame = Quaternion.LookRotation(forward.sqrMagnitude > .0001f ? forward : Vector3.forward, Vector3.up);
            Undo.RecordObject(profile, "Store actor-relative target camera");
            profile.Composition = CameraShotComposition.ActorStartPose;
            profile.TargetPosition = Quaternion.Inverse(frame) * (targetPose.Position - actor.position);
            profile.TargetRotation = (Quaternion.Inverse(frame) * targetPose.Rotation).eulerAngles;
            profile.FieldOfView = targetPose.FieldOfView;
            profile.PositionTravel = Vector3.zero;
            profile.OverrideFieldOfView = true;
            elapsed = progress = 0; playing = false;
            EditorUtility.SetDirty(profile); CameraShotProfileEditor.QueueSave(profile); ResetBasis();
        }
        private void FrameTargetHandles()
        {
            if (!Evaluate()) return;
            handles = true;
            var view = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView : GetWindow<SceneView>();
            float motion = profile.MotionCurve != null ? profile.MotionCurve.Evaluate(progress) : progress;
            Vector3 offset = profile.Composition == CameraShotComposition.ActorStartPose ? profile.TargetPosition :
                profile.Composition == CameraShotComposition.GameCameraRelative ? profile.CameraPositionOffset : profile.PositionOffset;
            Vector3 position = session.PositionAnchor + session.Frame * (offset + profile.PositionTravel * motion);
            view.Frame(new Bounds(position, Vector3.one * 2f), false);
            view.Focus(); view.Repaint(); Repaint();
        }
        private void DrawHandles(SceneView view)
        {
            if (!handles || Application.isPlaying || !Evaluate()) return;
            var previousDepth = Handles.zTest;
            // Other action-editor handles share this callback. Do not inherit their
            // coordinate matrix or let scenery obscure the editable camera gizmo.
            using (new Handles.DrawingScope(Color.white, Matrix4x4.identity))
            {
                Handles.zTest = CompareFunction.Always;
                try
                {
                    var current = Event.current;
                    if (current.type == EventType.KeyDown && !current.alt && !current.control && !current.command && GUIUtility.hotControl == 0)
                    {
                        if (current.keyCode == KeyCode.W || current.keyCode == KeyCode.E)
                        {
                            rotateCamera = current.keyCode == KeyCode.E;
                            SaveSettings();
                            current.Use(); Repaint(); view.Repaint();
                        }
                    }
                    DrawCameraHandles(view);
                }
                finally { Handles.zTest = previousDepth; }
            }
        }
        private void DrawCameraHandles(SceneView view)
        {
            if (!handles || Application.isPlaying || !Evaluate()) return;
            float motion = profile.MotionCurve != null ? profile.MotionCurve.Evaluate(progress) : progress;
            bool fixedPose = profile.Composition == CameraShotComposition.ActorStartPose;
            bool relative = profile.Composition != CameraShotComposition.ActorAnchored;
            Vector3 offset = fixedPose ? profile.TargetPosition : relative ? profile.CameraPositionOffset : profile.PositionOffset;
            Vector3 rawPosition = session.PositionAnchor + session.Frame * (offset + profile.PositionTravel * motion);
            Vector3 look = session.LookAt;
            Handles.color = Color.cyan;
            if (relative)
            {
                Handles.Label(rawPosition, "融合目标相机");
                Handles.DrawDottedLine(session.PositionAnchor, rawPosition, 4);
                Handles.Label(session.PositionAnchor, fixedPose ? "玩家 / 触发朝向基准" : "原游戏相机");
                EditorGUI.BeginChangeCheck();
                Vector3 moved = rotateCamera ? rawPosition : Handles.PositionHandle(rawPosition, session.Frame);
                Quaternion rotated = rotateCamera ? Handles.RotationHandle(pose.Rotation, rawPosition) : pose.Rotation;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(profile, "Move target camera relative to game camera");
                    Vector3 localPosition = session.ToPositionOffset(moved) - profile.PositionTravel * motion;
                    if (fixedPose) profile.TargetPosition = localPosition;
                    else profile.CameraPositionOffset = localPosition;
                    if (rotateCamera)
                    {
                        Vector3 angles = (Quaternion.Inverse(session.Frame) * rotated).eulerAngles;
                        Vector3 localAngles = new Vector3(Mathf.DeltaAngle(0, angles.x),
                            Mathf.DeltaAngle(0, angles.y), Mathf.DeltaAngle(0, angles.z));
                        if (fixedPose) profile.TargetRotation = localAngles;
                        else profile.CameraRotationOffset = localAngles;
                    }
                    EditorUtility.SetDirty(profile); CameraShotProfileEditor.QueueSave(profile); Repaint();
                }
            }
            else
            {
            Handles.DrawLine(rawPosition, look);
            Handles.Label(rawPosition, "CameraShot 相机"); Handles.Label(look, "注视点");
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(rawPosition, session.Frame);
            Vector3 aim = Handles.PositionHandle(look, session.Frame);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Move relative CameraShot handles");
                profile.PositionOffset = session.ToPositionOffset(moved) - profile.PositionTravel * motion;
                profile.LookAtOffset = session.ToLookOffset(aim);
                EditorUtility.SetDirty(profile); CameraShotProfileEditor.QueueSave(profile); Repaint();
            }
            }
            var oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(pose.Position, pose.Rotation, Vector3.one);
            Handles.DrawWireCube(new Vector3(0, 0, .15f), new Vector3(.25f, .15f, .3f));
            float halfHeight = Mathf.Tan(pose.FieldOfView * .5f * Mathf.Deg2Rad);
            Vector3[] corners = {
                new Vector3(-halfHeight * PreviewAspect, -halfHeight, 1),
                new Vector3(halfHeight * PreviewAspect, -halfHeight, 1),
                new Vector3(halfHeight * PreviewAspect, halfHeight, 1),
                new Vector3(-halfHeight * PreviewAspect, halfHeight, 1)
            };
            for (int i = 0; i < 4; i++)
            {
                Handles.DrawLine(Vector3.zero, corners[i]);
                Handles.DrawLine(corners[i], corners[(i + 1) % 4]);
            }
            Handles.matrix = oldMatrix;
            Vector3 start = session.PositionAnchor + session.Frame * offset;
            Handles.DrawDottedLine(start, start + session.Frame * profile.PositionTravel, 4);
        }
    }

    [CustomEditor(typeof(CameraShotProfile))]
    public sealed class CameraShotProfileEditor : UnityEditor.Editor
    {
        private static readonly System.Collections.Generic.HashSet<CameraShotProfile> pendingSaves = new();
        private static double saveAfter;
        internal static void QueueSave(CameraShotProfile profile)
        {
            if (profile == null || !AssetDatabase.Contains(profile)) return;
            pendingSaves.Add(profile);
            saveAfter = EditorApplication.timeSinceStartup + .35;
            EditorApplication.update -= SaveWhenIdle;
            EditorApplication.update += SaveWhenIdle;
        }
        private static void SaveWhenIdle()
        {
            if (GUIUtility.hotControl == 0 && !EditorApplication.isCompiling && EditorApplication.timeSinceStartup >= saveAfter) FlushPendingSaves();
        }
        internal static void FlushPendingSaves()
        {
            EditorApplication.update -= SaveWhenIdle;
            foreach (var profile in pendingSaves)
                if (profile != null) AssetDatabase.SaveAssetIfDirty(profile);
            pendingSaves.Clear();
        }
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("打开 CameraShot 可视化编辑器")) CameraShotWindow.Open((CameraShotProfile)target);
            DrawFields();
        }
        public void DrawFields()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Composition"), new GUIContent("构图模式"));
            bool relative = serializedObject.FindProperty("Composition").enumValueIndex == 1;
            bool fixedPose = serializedObject.FindProperty("Composition").enumValueIndex == 2;
            if (fixedPose)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetPosition"), new GUIContent("目标位置（玩家触发坐标系）"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetRotation"), new GUIContent("目标角度（玩家触发坐标系）"));
            }
            else if (relative)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("CameraPositionOffset"), new GUIContent("相机位置偏移"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("CameraRotationOffset"), new GUIContent("相机角度偏移"));
            }
            else
                foreach (string name in new[] { "PositionAnchor", "LookAtAnchor", "ReferenceFrame", "PositionOffset", "LookAtOffset", "FollowPosition", "FollowRotation", "Roll" })
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            EditorGUILayout.CurveField(serializedObject.FindProperty("WeightCurve"), Color.cyan,
                new Rect(0, 0, 1, 1), new GUIContent("进入 / 退出权重"), GUILayout.Height(50));
            EditorGUILayout.HelpBox("横轴是轨道进度，纵轴是镜头权重。0 为游戏镜头，1 为目标构图；起点和终点设为 0 可平顺进入、退出。拉长轨道会同比拉长整个镜头过程。", MessageType.None);
            foreach (string name in new[] { "PositionTravel", "MotionCurve", "OverrideFieldOfView", "FieldOfView", "Priority", "LockCameraInput", "AvoidObstacles", "ObstacleLayers", "CollisionRadius" })
            {
                if (fixedPose && name == "OverrideFieldOfView") continue;
                if (name == "FieldOfView" && !fixedPose && !serializedObject.FindProperty("OverrideFieldOfView").boolValue) continue;
                if ((name == "ObstacleLayers" || name == "CollisionRadius") && !serializedObject.FindProperty("AvoidObstacles").boolValue) continue;
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            }
            if (serializedObject.ApplyModifiedProperties()) QueueSave((CameraShotProfile)target);
        }
    }
    [CustomEditor(typeof(AbilityEventObj_CameraShot))]
    public sealed class CameraShotEventEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var e = (AbilityEventObj_CameraShot)target;
            if (GUILayout.Button("编辑镜头 / 实时预览")) CameraShotWindow.Open(e.Profile, e);
        }
    }
}
