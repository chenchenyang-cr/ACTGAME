using CombatCamera;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Camera Shot")]
    public sealed class AbilityEventObj_CameraShot : AbilityEventObj, IPerfectDodgeFeedbackTrack
    {
        [SerializeField, InspectorName("仅完美闪避成功时播放")]
        private bool playOnPerfectDodge;
        public bool IsPerfectDodgeFeedback => playOnPerfectDodge;
        public CameraShotProfile Profile;
        public int Channel;
        public CameraShotTiming Timing = CameraShotTiming.Default;
        [Min(0f)] public float HoldSeconds = .15f;
        // Store the opt-out so existing serialized nodes continue returning by default.
        [SerializeField] private bool keepFinalCameraPosition;
        public bool ReturnToOriginalCamera { get => !keepFinalCameraPosition; set => keepFinalCameraPosition = !value; }
        public float Duration => Mathf.Max(0, Timing.EnterSeconds) + Mathf.Max(0, HoldSeconds) +
            (ReturnToOriginalCamera ? Mathf.Max(0, Timing.ExitSeconds) : 0);
        public bool PreviewCamera = true;
        public bool IgnoreHitStop;
        public override EventTimeType GetEventTimeType() => EventTimeType.EventTime;
        public override AbilityEventEffect Initialize() => new AbilityEventEffect_CameraShot(this);
#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview() => new AbilityEventPreview_CameraShot(this);
        public override bool PreviewExist() => true;
#endif
    }

    public sealed class AbilityEventEffect_CameraShot : AbilityEventEffect
    {
        private int handle;
        private float previousTime = -1;
        private AbilityEventObj_CameraShot Config => (AbilityEventObj_CameraShot)_EventObj;
        public AbilityEventEffect_CameraShot(AbilityEventObj obj) : base(obj) { }
        public override void StartEffect()
        {
            base.StartEffect();
            CameraShotRuntime.Release(handle, true);
            if (_combatController == null || eve == null) return;
            handle = CameraShotRuntime.Begin(Config.Profile, _combatController.transform, Config.Duration,
                Config.Channel, _combatController, Config.Timing, Config.ReturnToOriginalCamera, Config.IgnoreHitStop);
            CameraShotRuntime.SetProgress(handle, 0);
            previousTime = -1;
        }
        public override void EffectRunning(float time)
        {
            if (AnimObj == null || AnimObj.Clip == null || eve == null) return;
            if (previousTime >= 0 && time < previousTime - .0001f) {
                CameraShotRuntime.Release(handle);
                handle = CameraShotRuntime.Begin(Config.Profile, _combatController.transform, Config.Duration,
                    Config.Channel, _combatController, Config.Timing, Config.ReturnToOriginalCamera, Config.IgnoreHitStop);
            }
            previousTime = time;
            if (handle == 0) return;
            // The camera pipeline advances independent shots even when actor updates pause.
            if (Config.IgnoreHitStop) return;
            float elapsed = Mathf.Max(0, time - eve.EventTime) * AnimObj.Clip.length;
            if (elapsed >= Config.Duration) {
                CameraShotRuntime.Complete(handle); handle = 0;
                // Point events remain latched until the action changes or loops back.
                // Clearing IsRunning here would trigger this same node again next frame.
            }
            else CameraShotRuntime.SetProgress(handle, Config.Duration > 0 ? elapsed / Config.Duration : 1);
        }
        public override void EndEffect()
        {
            CameraShotRuntime.Release(handle); handle = 0; base.EndEffect();
        }
    }

#if UNITY_EDITOR
    public sealed class AbilityEventPreview_CameraShot : AbilityEventPreview
    {
        private static readonly System.Collections.Generic.List<AbilityEventPreview_CameraShot> previews = new System.Collections.Generic.List<AbilityEventPreview_CameraShot>();
        public static AbilityEventPreview_CameraShot Find(AbilityEventObj_CameraShot obj) => previews.FindLast(p => p._EventObj == obj);
        private readonly CombatCamera.Editor.CameraShotPreviewRig simulation = new CombatCamera.Editor.CameraShotPreviewRig();
        private int handle;
        private int profileRevision;
        private int eventRevision;
        public System.Action<float> SampleActor;
        private AbilityEventObj_CameraShot Config => (AbilityEventObj_CameraShot)_EventObj;
        public CameraShotPose Output { get; private set; }
        public int SelectedCandidate => simulation.SelectedCandidate;
        public bool HasOutput { get; private set; }
        public AbilityEventPreview_CameraShot(AbilityEventObj obj) : base(obj) { }
        public override void InitPreview() { base.InitPreview(); if (!previews.Contains(this)) previews.Add(this); }
        public override void PreviewRunning(float time) { base.PreviewRunning(time); PreviewUpdateFrame(time); }
        // Evaluate samples the actor from the start when needed, and resets itself on backward seeks.
        public override bool NeedStartFrameValue() => false;
        public override void PreviewUpdateFrame(float time)
        {
            if (Application.isPlaying || eve == null || Config.Profile == null ||
                _combatController == null || AnimObj == null || AnimObj.Clip == null) { Release(); return; }
            var extension = CinemachineCameraShotExtension.FindActive(Config.Channel);
            var rig = extension != null ? extension.Rig : null;
            if (rig == null) { Release(); return; }
            int revision = UnityEditor.EditorUtility.GetDirtyCount(Config.Profile);
            int configRevision = UnityEditor.EditorUtility.GetDirtyCount(Config);
            if (revision != profileRevision || configRevision != eventRevision) {
                simulation.Dispose(); Release(); profileRevision = revision; eventRevision = configRevision;
            }
            System.Action<float> sample = SampleActor != null ? seconds => SampleActor(Mathf.Clamp01(seconds / AnimObj.Clip.length)) : null;
            float start = StartTimePercentage * AnimObj.Clip.length;
            float duration = Config.Duration;
            Output = simulation.Evaluate(Config.Profile, Config.Timing, rig, _combatController.transform,
                time * AnimObj.Clip.length, start, duration, sample, Config.ReturnToOriginalCamera);
            HasOutput = true;
            if (!eve.Previewable || !Config.PreviewCamera || !PreviewInRange(time)) { Release(false); return; }
            if (handle == 0) handle = CameraShotRuntime.Begin(Config.Profile, _combatController.transform,
                duration, Config.Channel, timing: Config.Timing, returnToOriginalCamera: Config.ReturnToOriginalCamera);
            CameraShotRuntime.SetPreview(handle, duration > 0 ? (time - StartTimePercentage) * AnimObj.Clip.length / duration : 1);
            CameraShotRuntime.SetPreviewOutput(handle, simulation.Axes, Output, simulation.SelectedCandidate);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
        public override void BackToStart() { simulation.Dispose(); Release(); }
        public override bool PreviewInRange(float time)
        {
            if (AnimObj == null || AnimObj.Clip == null) return false;
            float elapsed = (time - StartTimePercentage) * AnimObj.Clip.length;
            return elapsed >= 0 && (!Config.ReturnToOriginalCamera || elapsed < Config.Duration);
        }
        public override void DestroyPreview() { simulation.Dispose(); Release(); previews.Remove(this); }
        private void Release(bool clearOutput = true)
        {
            CameraShotRuntime.Release(handle, true); handle = 0;
            if (clearOutput) HasOutput = false;
        }
    }
#endif
}
