using Cinemachine;
using CombatEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CombatCamera.Editor
{
    public sealed partial class CameraShotWindow : EditorWindow
    {
        [SerializeField] private CameraShotProfile profile;
        [SerializeField] private AbilityEventObj_CameraShot actionEvent;
        [SerializeField] private Transform actor;
        [SerializeField] private Camera source;
        [SerializeField] private bool timeline = true, handles = true;
        [SerializeField] private bool rotateCamera;
        [SerializeField] private float progress, duration = .5f, elapsed;
        private readonly CameraShotRuntime.PreviewSession session = new();
        private readonly CameraShotOrbitSolver returnPreview = new();
        private readonly CameraShotOrbitSolver targetSolver = new();
        [SerializeField] private CinemachineFreeLook gameRig;
        private CinemachineFreeLook SourceRig => gameRig != null ? gameRig : ResolveRig();
        private bool ConstrainTarget(CameraShotPose desired, out CameraShotPose constrained) => targetSolver.Evaluate(SourceRig, desired, out constrained, true);
        private Camera previewCamera;
        private RenderTexture texture;
        private UnityEditor.Editor profileEditor;
        private CameraShotPose pose;
        private Vector3 initialPosition, initialActorPosition;
        private Quaternion initialRotation = Quaternion.identity;
        private float initialFov = 55;
        private bool initialized, playing;
        private bool previewSettings;
        private double previousTime, renderTime;
        private Vector2 scroll;
        private string renderError;
        private float PreviewAspect => source != null ? Mathf.Clamp(source.aspect, .2f, 5f) : 16f / 9f;

        [System.Serializable]
        private sealed class WindowSettings
        {
            public string actorId, sourceId, eventId;
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
                actorId = ObjectId(actor), sourceId = ObjectId(source), eventId = ObjectId(actionEvent),
                timeline = timeline, handles = handles, rotateCamera = rotateCamera,
                duration = duration, elapsed = elapsed
            }));
        }
        private void RestoreSettings()
        {
            if (profile == null || !EditorPrefs.HasKey(SettingsKey)) return;
            var saved = JsonUtility.FromJson<WindowSettings>(EditorPrefs.GetString(SettingsKey));
            actor = Resolve<Transform>(saved.actorId);
            source = Resolve<Camera>(saved.sourceId); actionEvent = Resolve<AbilityEventObj_CameraShot>(saved.eventId);
            timeline = saved.timeline; handles = saved.handles; rotateCamera = saved.rotateCamera;
            duration = Mathf.Max(.001f, saved.duration); elapsed = saved.elapsed;
            progress = Mathf.Clamp01(elapsed / duration);
        }
        private void OnLostFocus() { EndNavigation(); SaveSettings(); CameraShotProfileEditor.FlushPendingSaves(); }

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
                window.actor = null; window.source = null;
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
            simulation.Dispose(); simulationReady = false; EndNavigation();
            returnPreview.Dispose(); targetSolver.Dispose();
            if (previewCamera != null) DestroyImmediate(previewCamera.gameObject);
            if (texture != null) { texture.Release(); DestroyImmediate(texture); }
        }
        private void ResetBasis()
        {
            simulation.Dispose(); simulationReady = false;
            returnPreview.Dispose(); targetSolver.Dispose();
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
            if (actionEvent == null) actionEvent = e;
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
            if (profile.Composition == CameraShotComposition.ActorStartPose && profile.LookAtPlayer) rotateCamera = false;
            if (profile.Composition == CameraShotComposition.ActorStartPose && profile.LimitToGameCameraOrbit)
            {
                rotateCamera = false;
                if (SourceRig == null || SourceRig.Follow == null)
                {
                    renderError = "限制目标机位需要游戏相机上正在使用的 FreeLook，并绑定 Follow。";
                    return false;
                }
            }
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
            var action = timeline ? TimelinePreview : null;
            if (action != null && action.HasStartPose && profile.Composition != CameraShotComposition.GameCameraRelative)
            {
                session.CaptureStart(profile, actor, action.StartPosition, action.StartForward);
            }
            UpdateSimulation();
            var basis = simulationReady ? simulation.Basis : SourcePose();
            pose = session.Evaluate(profile, actor,
                profile.Composition == CameraShotComposition.GameCameraRelative ? basis.Rotation : initialRotation,
                basis.FieldOfView, progress, basis.Position, ConstrainTarget);
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
            Navigate(dt);
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
                if (elapsed > duration + (ClipTiming.Enabled && ClipTiming.Exit > 0 ? .25f : profile != null ? profile.BlendOut + .25f : .25f))
                { elapsed = 0; session.Reset(); simulation.Reset(); }
                progress = Mathf.Clamp01(elapsed / Mathf.Max(.001f, duration));
            }
            if (now - renderTime < 1.0 / 30.0) return;
            renderTime = now;
            if (!Evaluate()) { Repaint(); return; }
            Render(); Repaint();
        }
        private float Weight() => profile != null &&
            !(timeline && TimelinePreview != null && (elapsed < 0f || elapsed >= duration))
                ? (ClipTiming.Enabled ? ClipTiming.Evaluate(progress) : profile.EvaluateWeight(progress)) : 0f;
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
            var state = CameraState.Default;
            var composed = editComposition ? pose : simulation.Output;
            state.RawPosition = composed.Position;
            state.RawOrientation = composed.Rotation;
            var lens = state.Lens; lens.FieldOfView = composed.FieldOfView; state.Lens = lens;
            if (timeline && !editComposition)
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
            previewSettings = EditorGUILayout.Foldout(previewSettings, "预览绑定", true);
            if (previewSettings || actor == null || source == null)
            {
                actor = (Transform)EditorGUILayout.ObjectField("预览角色", actor, typeof(Transform), true);
                source = (Camera)EditorGUILayout.ObjectField("游戏相机", source, typeof(Camera), true);
                gameRig = (CinemachineFreeLook)EditorGUILayout.ObjectField("基础 FreeLook", SourceRig, typeof(CinemachineFreeLook), true);
            }
            if (EditorGUI.EndChangeCheck()) ResetBasis();
            DrawPlaybackControls();
            if (timeline && TimelinePreview == null)
                EditorGUILayout.HelpBox("打开含 CameraShot 的动作预览，或关闭时间轴联动并手动指定角色。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            handles = EditorGUILayout.Toggle("Scene 拖拽手柄", handles);
            if (handles && profile != null && !(profile.Composition == CameraShotComposition.ActorStartPose && (profile.LimitToGameCameraOrbit || profile.LookAtPlayer)))
                rotateCamera = GUILayout.Toolbar(rotateCamera ? 1 : 0, new[] { "移动目标相机", "旋转目标相机" }) == 1;
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            using (new EditorGUI.DisabledScope(profile == null || actor == null))
                if (GUILayout.Button("在 Scene 中定位目标相机手柄")) FrameTargetHandles();
            if (actor == null)
                EditorGUILayout.HelpBox("请指定场景中的玩家，或打开动作预览以绑定角色。", MessageType.Warning);
            if (!timeline || TimelinePreview == null)
            {
                EditorGUI.BeginChangeCheck();
                float selected = EditorGUILayout.Slider("轨道预览进度", progress, 0, 1);
                if (EditorGUI.EndChangeCheck()) { progress = selected; elapsed = progress * duration; }
                duration = Mathf.Max(.001f, EditorGUILayout.FloatField("镜头时长（秒）", duration));
            }
            else EditorGUILayout.LabelField("镜头进度", progress.ToString("P0"));
            if (profile != null) EditorGUILayout.LabelField("当前镜头权重", Weight().ToString("P0"));
            if (!editComposition && profile != null && Weight() < .001f)
                EditorGUILayout.HelpBox("当前权重为 0，画面显示游戏相机。将时间轴拖到曲线高处查看目标构图；Scene 手柄仍可编辑。", MessageType.Info);
            Rect rect = GUILayoutUtility.GetAspectRect(PreviewAspect, GUILayout.MinHeight(140));
            if (texture != null && profile != null && actor != null && renderError == null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            else GUI.Box(rect, "指定镜头配置和玩家后显示实时画面");
            HandleViewport(rect);
            if (renderError != null) EditorGUILayout.HelpBox(renderError, MessageType.Error);
            using (new EditorGUI.DisabledScope(profile == null || actor == null))
            {
                if (GUILayout.Button("采用当前 Scene 视角")) AdoptSceneView();
                using (new EditorGUI.DisabledScope(source == null))
                    if (GUILayout.Button("采用游戏相机构图")) AlignToGameCamera();
                if (profile != null && profile.Composition != CameraShotComposition.ActorStartPose &&
                    GUILayout.Button("转换为玩家固定构图")) BakeCurrentTarget();
            }
            DrawKeyframes();
            if (actionEvent != null) CameraShotEventEditor.DrawTiming(actionEvent);
            EditorGUILayout.LabelField("自动保存 · 支持 Undo", EditorStyles.miniLabel);
            if (source == null) EditorGUILayout.HelpBox("请指定原游戏相机；相机相对模式不会使用 Scene 视图作为替代。", MessageType.Warning);
            if (profile != null)
            {
                UnityEditor.Editor.CreateCachedEditor(profile, typeof(CameraShotProfileEditor), ref profileEditor);
                EditorGUI.BeginChangeCheck();
                // Draw fields here without recursively opening another window.
                ((CameraShotProfileEditor)profileEditor).DrawFields();
                if (EditorGUI.EndChangeCheck()) { session.Reset(); simulation.Reset(); SceneView.RepaintAll(); }
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
            profile.UseKeyframes = false;
            profile.OverrideFieldOfView = true;
            elapsed = progress = 0; playing = false;
            EditorUtility.SetDirty(profile); CameraShotProfileEditor.QueueSave(profile); ResetBasis();
        }
        private void FrameTargetHandles()
        {
            if (!Evaluate()) return;
            handles = true;
            var view = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView : GetWindow<SceneView>();
            Vector3 position = session.PositionAnchor + session.Frame * profile.SampleMotion(progress).Position;
            view.Frame(new Bounds(profile.LimitToGameCameraOrbit && profile.Composition == CameraShotComposition.ActorStartPose ? pose.Position : position, Vector3.one * 2f), false);
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
            bool fixedPose = profile.Composition == CameraShotComposition.ActorStartPose;
            Vector3 offset = profile.SampleMotion(progress).Position;
            Vector3 rawPosition = session.PositionAnchor + session.Frame * offset;
            Handles.color = Color.cyan;
            {
                Handles.Label(rawPosition, "融合目标相机");
                Handles.DrawDottedLine(session.PositionAnchor, rawPosition, 4);
                Handles.Label(session.PositionAnchor, fixedPose ? "玩家 / 触发朝向基准" : "原游戏相机");
                EditorGUI.BeginChangeCheck();
                Vector3 moved = rotateCamera ? rawPosition : Handles.PositionHandle(rawPosition, session.Frame);
                Quaternion rotated = rotateCamera ? Handles.RotationHandle(pose.Rotation, rawPosition) : pose.Rotation;
                if (EditorGUI.EndChangeCheck())
                {
                    WritePose(new CameraShotPose { Position = moved, Rotation = rotated, FieldOfView = pose.FieldOfView }, true);
                }
            }
            if (fixedPose && profile.LimitToGameCameraOrbit)
            {
                Handles.color = Color.yellow;
                Handles.DrawDottedLine(rawPosition, pose.Position, 4);
                Handles.Label(pose.Position, "常态轨道目标机位");
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
        private bool advanced, motion;
        private void Field(string name, string label) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label));
        public void DrawFields()
        {
            serializedObject.Update();
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("构图", EditorStyles.boldLabel);
            Field("Composition", "构图模式");
            bool relative = serializedObject.FindProperty("Composition").intValue == (int)CameraShotComposition.GameCameraRelative;
            bool fixedPose = serializedObject.FindProperty("Composition").intValue == (int)CameraShotComposition.ActorStartPose;
            if (fixedPose)
            {
                Field("TargetPosition", "位置");
                Field("LimitToGameCameraOrbit", "限制目标机位到常态轨道");
                Field("LookAtPlayer", "始终看向玩家");
                bool lookAtPlayer = serializedObject.FindProperty("LookAtPlayer").boolValue;
                bool limited = serializedObject.FindProperty("LimitToGameCameraOrbit").boolValue;
                if (!limited && !lookAtPlayer) Field("TargetRotation", "角度");
                if (lookAtPlayer) EditorGUILayout.LabelField("自动朝向玩家上身；同时开启轨道限制时，位置受限，朝向以玩家为准。", EditorStyles.wordWrappedMiniLabel);
                else if (limited) EditorGUILayout.LabelField("角度采用常态相机构图；FOV 可调，过渡路径不受限制。", EditorStyles.wordWrappedMiniLabel);
            }
            else if (relative)
            {
                Field("CameraPositionOffset", "位置偏移");
                Field("CameraRotationOffset", "角度偏移");
            }
            if (!fixedPose) Field("OverrideFieldOfView", "调整视野");
            if (!serializedObject.FindProperty("UseKeyframes").boolValue && (fixedPose || serializedObject.FindProperty("OverrideFieldOfView").boolValue)) Field("FieldOfView", "视野 FOV");
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("节奏", EditorStyles.boldLabel);
            EditorGUILayout.CurveField(serializedObject.FindProperty("WeightCurve"), Color.cyan,
                new Rect(0, 0, 1, 1), new GUIContent("入场 / 保持"), GUILayout.Height(45));
            Field("ReturnStart", "回程起点（轨道比例）");
            EditorGUILayout.LabelField("回程前使用权重曲线，回程后平滑交还游戏镜头。", EditorStyles.wordWrappedMiniLabel);
            motion = EditorGUILayout.Foldout(motion, "运镜", true);
            if (motion)
            {
                EditorGUI.indentLevel++;
                Field("UseKeyframes", "启用运镜关键帧");
                if (serializedObject.FindProperty("UseKeyframes").boolValue) Field("Keys", "关键帧（镜头参考空间）");
                else { Field("PositionTravel", "位移"); Field("MotionCurve", "位移曲线"); }
                EditorGUI.indentLevel--;
            }
            advanced = EditorGUILayout.Foldout(advanced, "高级设置", true);
            if (advanced)
            {
                EditorGUI.indentLevel++;
                if (!relative) { Field("FollowDamping", "跟随缓冲（秒）"); Field("OrbitBlend", "围绕角色过渡"); }
                Field("Priority", "优先级"); Field("LockCameraInput", "锁定镜头输入");
                Field("BlendIn", "非时间轴入场（秒）"); Field("BlendOut", "中断退出（秒）");
                Field("UseUnscaledTime", "缓冲不受慢动作影响");
                Field("AvoidObstacles", "避障");
                if (serializedObject.FindProperty("AvoidObstacles").boolValue)
                {
                    Field("ObstacleLayers", "障碍物层"); Field("CollisionRadius", "检测半径");
                    Field("ObstacleRecovery", "避障恢复（秒）");
                }
                EditorGUI.indentLevel--;
            }
            if (serializedObject.ApplyModifiedProperties()) QueueSave((CameraShotProfile)target);
        }
    }
    [CustomEditor(typeof(AbilityEventObj_CameraShot))]
    public sealed class CameraShotEventEditor : UnityEditor.Editor
    {
        public static void DrawTiming(AbilityEventObj_CameraShot e)
        {
            EditorGUILayout.LabelField("本片段过渡", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var timing = e.Timing;
            timing.Enabled = EditorGUILayout.Toggle("使用片段内进入 / 退出", timing.Enabled);
            if (timing.Enabled)
            {
                if (!e.Timing.Enabled) { timing.Enter = .2f; timing.Exit = .25f; }
                timing.Enter = EditorGUILayout.Slider("进入占比", timing.Enter, 0, 1);
                timing.Exit = EditorGUILayout.Slider("退出占比", timing.Exit, 0, 1f - timing.Enter);
                EditorGUILayout.HelpBox("进入 / 退出随动作时间推进；正常退出在片段内完成。提前中断使用 Profile 的中断退出时长。返回持续更新的游戏镜头。", MessageType.Info);
            }
            else EditorGUILayout.HelpBox("兼容原配置：使用 Profile 的权重曲线与回程规则。启用上方选项后，本片段独立控制正常过渡，原曲线保留。", MessageType.None);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(e, "Edit camera shot clip blends"); e.Timing = timing;
                EditorUtility.SetDirty(e); AssetDatabase.SaveAssetIfDirty(e);
            }
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "Timing");
            serializedObject.ApplyModifiedProperties();
            var e = (AbilityEventObj_CameraShot)target;
            DrawTiming(e);
            if (GUILayout.Button("编辑镜头 / 实时预览")) CameraShotWindow.Open(e.Profile, e);
        }
    }
}
