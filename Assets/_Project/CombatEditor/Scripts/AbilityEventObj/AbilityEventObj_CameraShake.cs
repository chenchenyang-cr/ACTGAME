using CombatCamera;
using UnityEngine;

namespace CombatEditor
{
    public enum CameraShakeTriggerMode
    {
        Direct,
        OnConfirmedHit
    }

    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Camera Shake")]
    public sealed class AbilityEventObj_CameraShake : AbilityEventObj
    {
        [Header("Trigger")]
        public CameraShakeTriggerMode TriggerMode = CameraShakeTriggerMode.Direct;

        [Header("Shake")]
        public CameraShakeSettings Settings = new CameraShakeSettings();

        [Header("Confirmed Hit Trigger")]
        [Min(0.01f)] public float HitShakeDuration = 0.16f;
        public bool UseUnscaledTime = true;
        public CameraShakeHitBoxFilter HitBoxFilter =
            CameraShakeHitBoxFilter.AnyHitBoxInAbility;
        public AbilityEventObj_CreateHitBox SpecificHitBox;
        public CameraShakeHitTriggerPolicy TriggerPolicy =
            CameraShakeHitTriggerPolicy.FirstHitOnly;
        public CombatHitResultMask AcceptedResults = CombatHitResultMask.Normal |
                                                         CombatHitResultMask.Critical;
        [Min(0)] public int MaximumTriggerCount = 1;
        [Min(0f)] public float TriggerCooldown;

        [Header("Editor Preview")]
        [Range(0f, 1f)] public float PreviewHitTime = 0.5f;
        [Min(0f)] public float PreviewHitIntensityScale = 1f;

        public override EventTimeType GetEventTimeType() => EventTimeType.EventRange;

        public override AbilityEventEffect Initialize()
        {
            return new AbilityEventEffect_CameraShake(this);
        }

#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview()
        {
            return new AbilityEventPreview_CameraShake(this);
        }

        public override bool PreviewExist() => true;
#endif
    }

    public sealed class AbilityEventEffect_CameraShake : AbilityEventEffect
    {
        private int shakeHandle;
        private int hitBindingHandle;
        private double shakeStartTime;
        private AbilityEventObj_CameraShake Config =>
            (AbilityEventObj_CameraShake)_EventObj;

        public AbilityEventEffect_CameraShake(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            Release();

            if (Config.TriggerMode == CameraShakeTriggerMode.Direct)
            {
                shakeStartTime = Time.unscaledTimeAsDouble;
                shakeHandle = CameraShakeRuntime.Add(Config.Settings);
                return;
            }

            hitBindingHandle = CombatFeedbackManager.RegisterCameraShake(
                new CameraShakeHitBinding
                {
                    Owner = _combatController,
                    Ability = AnimObj,
                    HitBoxFilter = Config.HitBoxFilter,
                    SpecificHitBox = Config.SpecificHitBox,
                    TriggerPolicy = Config.TriggerPolicy,
                    ResultMask = Config.AcceptedResults,
                    MaximumTriggerCount = Config.MaximumTriggerCount,
                    Cooldown = Config.TriggerCooldown,
                    Duration = Config.HitShakeDuration,
                    UseUnscaledTime = Config.UseUnscaledTime,
                    Settings = Config.Settings
                });
        }

        public override void EffectRunning(float currentTimePercentage)
        {
            base.EffectRunning(currentTimePercentage);
            if (Config.TriggerMode != CameraShakeTriggerMode.Direct || shakeHandle == 0)
                return;

            float localTime = Mathf.InverseLerp(eve.GetEventStartTime(),
                eve.GetEventEndTime(), currentTimePercentage);
            // The timeline controls the envelope, but frequency is measured in real seconds.
            float sampleTime = (float)(Time.unscaledTimeAsDouble - shakeStartTime);
            CameraShakeRuntime.Update(shakeHandle, Config.Settings, sampleTime,
                localTime);
        }

        public override void EndEffect()
        {
            Release();
            base.EndEffect();
        }

        private void Release()
        {
            CameraShakeRuntime.Remove(shakeHandle);
            CombatFeedbackManager.UnregisterCameraShake(hitBindingHandle);
            shakeHandle = 0;
            hitBindingHandle = 0;
        }
    }

#if UNITY_EDITOR
    // Shared by timeline shakes and hit-box previews. A stationary playhead during
    // playback (hit stop / zero speed) must not stop the wave clock.
    internal struct CameraShakePreviewClock
    {
        private bool initialized;
        private double lastTime;
        private float lastTimelineTime;
        private float elapsed;
        private bool wasPlaying;

        public float Sample(float timelineTime, bool isPlaying, double now, out bool resetPhase)
        {
            resetPhase = !initialized || timelineTime < lastTimelineTime ||
                         (!isPlaying && timelineTime != lastTimelineTime);
            if (resetPhase)
                elapsed = isPlaying ? 0f : timelineTime;
            else if (isPlaying && wasPlaying)
                elapsed += (float)System.Math.Max(0d, now - lastTime);

            initialized = true;
            lastTime = now;
            lastTimelineTime = timelineTime;
            wasPlaying = isPlaying;
            return elapsed;
        }
    }

    public sealed class AbilityEventPreview_CameraShake : AbilityEventPreview
    {
        private int shakeHandle;
        private CameraShakePreviewClock waveClock;
        private AbilityEventObj_CameraShake Config =>
            (AbilityEventObj_CameraShake)_EventObj;

        public AbilityEventPreview_CameraShake(AbilityEventObj obj) : base(obj) { }

        public override void PreviewRunning(float currentTimePercentage)
        {
            int previousFrame = LastFrame;
            base.PreviewRunning(currentTimePercentage);
            // Settings and preview toggles can change without moving the playhead.
            if (LastFrame == previousFrame)
                PreviewUpdateFrame(currentTimePercentage);
        }

        public override void PreviewUpdateFrame(float currentTimePercentage)
        {
            if (eve == null || !eve.Previewable)
            {
                Release();
                return;
            }

            if (Config.TriggerMode == CameraShakeTriggerMode.Direct)
                PreviewDirect(currentTimePercentage);
            else
                PreviewConfirmedHit(currentTimePercentage);
        }

        public override void BackToStart() => Release();
        public override void DestroyPreview() => Release();

        private void PreviewDirect(float currentTimePercentage)
        {
            if (currentTimePercentage < StartTimePercentage || currentTimePercentage >= EndTimePercentage)
            {
                Release();
                return;
            }

            float localTime = Mathf.InverseLerp(StartTimePercentage, EndTimePercentage,
                currentTimePercentage);
            float duration = Mathf.Max(0.01f,
                (EndTimePercentage - StartTimePercentage) * AnimLength);
            UpdateShake(localTime * duration, localTime, 1f);
        }

        private void PreviewConfirmedHit(float currentTimePercentage)
        {
            float hitTime = Mathf.Lerp(StartTimePercentage, EndTimePercentage,
                Config.PreviewHitTime);
            float elapsed = (currentTimePercentage - hitTime) * AnimLength;
            if (elapsed < 0f || elapsed > Config.HitShakeDuration)
            {
                Release();
                return;
            }

            float normalizedTime = elapsed / Mathf.Max(0.01f, Config.HitShakeDuration);
            UpdateShake(elapsed, normalizedTime, Config.PreviewHitIntensityScale);
        }

        private void UpdateShake(float sampleTime, float normalizedTime, float intensityScale)
        {
            bool isPlaying = CombatGlobalEditorValue.IsPlaying || CombatGlobalEditorValue.IsLooping;
            if (shakeHandle == 0)
                shakeHandle = CameraShakeRuntime.Add(Config.Settings, intensityScale);
            float waveTime = waveClock.Sample(sampleTime, isPlaying,
                UnityEditor.EditorApplication.timeSinceStartup, out bool resetPhase);
            CameraShakeRuntime.Update(shakeHandle, Config.Settings, waveTime, normalizedTime,
                intensityScale, resetPhase: resetPhase);
        }

        private void Release()
        {
            CameraShakeRuntime.Remove(shakeHandle);
            shakeHandle = 0;
            waveClock = default;
        }
    }
#endif
}
