using CombatCamera;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Camera Shot")]
    public sealed class AbilityEventObj_CameraShot : AbilityEventObj
    {
        public CameraShotProfile Profile;
        public int Channel;
        [Tooltip("Opt in to previewing this shot in Game view while scrubbing. Requires the shot extension on the scene camera.")]
        public bool PreviewCamera;
        public override EventTimeType GetEventTimeType() => EventTimeType.EventRange;
        public override AbilityEventEffect Initialize() => new AbilityEventEffect_CameraShot(this);
#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview() => new AbilityEventPreview_CameraShot(this);
        public override bool PreviewExist() => true;
#endif

        public Transform ResolveTarget(CombatController owner)
        {
            var context = owner != null ? owner.GetComponentInParent<CameraShotTarget>() : null;
            return context != null ? context.Target : null;
        }
    }

    public sealed class AbilityEventEffect_CameraShot : AbilityEventEffect
    {
        private int handle;
        private AbilityEventObj_CameraShot Config => (AbilityEventObj_CameraShot)_EventObj;
        public AbilityEventEffect_CameraShot(AbilityEventObj obj) : base(obj) { }
        public override void StartEffect()
        {
            base.StartEffect();
            CameraShotRuntime.Release(handle, true);
            if (_combatController == null) return;
            handle = CameraShotRuntime.Begin(Config.Profile, _combatController.transform,
                Config.ResolveTarget(_combatController), channel: Config.Channel, lifetimeOwner: _combatController);
            CameraShotRuntime.SetProgress(handle, 0f);
        }
        public override void EffectRunning(float time) => CameraShotRuntime.SetProgress(handle,
            Mathf.InverseLerp(eve.GetEventStartTime(), eve.GetEventEndTime(), time));
        public override void EndEffect()
        {
            CameraShotRuntime.Release(handle);
            handle = 0;
            base.EndEffect();
        }
    }

#if UNITY_EDITOR
    public sealed class AbilityEventPreview_CameraShot : AbilityEventPreview
    {
        private static readonly System.Collections.Generic.List<AbilityEventPreview_CameraShot> previews = new();
        public static AbilityEventPreview_CameraShot Find(AbilityEventObj_CameraShot obj) =>
            previews.FindLast(p => obj == null || p._EventObj == obj);
        public override void InitPreview()
        {
            base.InitPreview();
            if (!previews.Contains(this)) previews.Add(this);
        }
        public override void PreviewRunning(float time)
        {
            // Property edits must refresh even while the timeline stays on one frame.
            base.PreviewRunning(time);
            PreviewUpdateFrame(time);
        }
        public bool HasStartPose { get; private set; }
        public Vector3 StartPosition { get; private set; }
        public Vector3 StartForward { get; private set; }
        public Vector3? StartTarget { get; private set; }
        public override bool NeedStartFrameValue() => true;
        public override void GetStartFrameDataBeforePreview()
        {
            if (_combatController == null) return;
            StartPosition = _combatController.transform.position;
            StartForward = _combatController.transform.forward;
            var target = Config.ResolveTarget(_combatController);
            StartTarget = target != null ? target.position : (Vector3?)null;
            HasStartPose = true;
        }
        private int handle;
        private AbilityEventObj_CameraShot Config => (AbilityEventObj_CameraShot)_EventObj;
        public AbilityEventPreview_CameraShot(AbilityEventObj obj) : base(obj) { }
        public override void PreviewUpdateFrame(float time)
        {
            if (Application.isPlaying || !Config.PreviewCamera || eve == null || !eve.Previewable ||
                !PreviewInRange(time) || Config.Profile == null || _combatController == null)
            { Release(); return; }
            if (handle == 0) handle = CameraShotRuntime.Begin(Config.Profile,
                _combatController.transform, Config.ResolveTarget(_combatController), channel: Config.Channel);
            float t = Mathf.InverseLerp(StartTimePercentage, EndTimePercentage, time);
            CameraShotRuntime.SetPreview(handle, t, Config.Profile.EvaluateWeight(t));
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
        public override void BackToStart() => Release();
        public override void DestroyPreview()
        {
            Release();
            previews.Remove(this);
        }
        private void Release()
        {
            CameraShotRuntime.Release(handle, true); handle = 0;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
    }
#endif
}
