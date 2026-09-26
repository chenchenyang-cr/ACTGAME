using CombatCamera;
using UnityEngine;

namespace CombatEditor
{
    public enum CameraShakeTriggerMode
    {
        [InspectorName("轨道开始触发")]
        Direct,
        [InspectorName("确认命中触发")]
        OnConfirmedHit
    }

    [AbilityEvent]
    [CreateAssetMenu(menuName = "动作事件/摄像机震动")]
    public sealed class AbilityEventObj_CameraShake : AbilityEventObj
    {
        [Header("震动触发")]
        public CameraShakeTriggerMode TriggerMode = CameraShakeTriggerMode.Direct;

        [Header("震动效果")]
        public CameraShakeSettings Settings = new CameraShakeSettings();
        [Tooltip("开启后，震动在所属角色顿帧时按顿帧动画速度暂停或减速；顿帧结束后继续。")]
        public bool AffectedByHitStop;
        [Range(0f, 1f)] public float HitStopWeight = 1f;

        [Header("命中后触发")]
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

        [Header("编辑器预览")]
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
        private double lastShakeTime;
        private float shakeSampleTime;
        private AbilityEventObj_CameraShake Config =>
            (AbilityEventObj_CameraShake)_EventObj;
        private bool UseHitStop => Config.AffectedByHitStop && Config.HitStopWeight > 0f;

        public AbilityEventEffect_CameraShake(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            Release();

            if (Config.TriggerMode == CameraShakeTriggerMode.Direct)
            {
                shakeStartTime = Time.unscaledTimeAsDouble;
                lastShakeTime = shakeStartTime;
                shakeSampleTime = 0f;
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
                    AffectedByHitStop = Config.AffectedByHitStop,
                    HitStopWeight = Config.HitStopWeight,
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
            float sampleTime = AdvanceShakeClock();
            if (UseHitStop)
                localTime = Mathf.Clamp01(sampleTime / GetShakeDuration());
            CameraShakeRuntime.Update(shakeHandle, Config.Settings, sampleTime,
                localTime, worldForceDirection: _combatController != null && _combatController._animator != null
                    ? _combatController._animator.transform.forward : Vector3.forward,
                directionalIntensityScale: 1f);
        }

        private float GetShakeDuration() => Mathf.Max(0.01f,
            (eve.GetEventEndTime() - eve.GetEventStartTime()) * AnimObj.Clip.length);

        private float GetHitStopSpeedScale()
        {
            float hitStopSpeed = _combatController != null &&
                                 _combatController._animSpeedExecutor != null
                ? _combatController._animSpeedExecutor.CurrentHitStopSpeedScale : 1f;
            return Mathf.Lerp(1f, hitStopSpeed, Mathf.Clamp01(Config.HitStopWeight));
        }

        private float AdvanceShakeClock()
        {
            double now = Time.unscaledTimeAsDouble;
            if (UseHitStop)
                shakeSampleTime += (float)System.Math.Max(0d, now - lastShakeTime) *
                                   GetHitStopSpeedScale();
            lastShakeTime = now;
            return UseHitStop ? shakeSampleTime : (float)(now - shakeStartTime);
        }

        public override void EndEffect()
        {
            if (shakeHandle != 0 && UseHitStop)
            {
                float elapsed = AdvanceShakeClock();
                float duration = GetShakeDuration();
                if (elapsed < duration)
                {
                    CombatController owner = _combatController;
                    float weight = Mathf.Clamp01(Config.HitStopWeight);
                    CameraShakeRuntime.ContinueTimed(shakeHandle, duration, elapsed,
                        () => owner != null && owner._animSpeedExecutor != null
                            ? Mathf.Lerp(1f, owner._animSpeedExecutor.CurrentHitStopSpeedScale, weight)
                            : 1f);
                    shakeHandle = 0;
                }
            }
            CameraShakeRuntime.Remove(shakeHandle);
            shakeHandle = 0;
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
    // Shared by timeline shakes and hit-box previews. A stationary playhead alone
    // does not stop the wave clock; tracks can opt into hit-stop scaling.
    internal struct CameraShakePreviewClock
    {
        private bool initialized;
        private double lastTime;
        private float lastTimelineTime;
        private float elapsed;
        private bool wasPlaying;

        public float Sample(float timelineTime, bool isPlaying, double now, out bool resetPhase,
            float timeScale = 1f)
        {
            resetPhase = !initialized || timelineTime < lastTimelineTime ||
                         (!isPlaying && timelineTime != lastTimelineTime);
            if (resetPhase)
                elapsed = isPlaying ? 0f : timelineTime;
            else if (isPlaying && wasPlaying)
                elapsed += (float)System.Math.Max(0d, now - lastTime) *
                           Mathf.Max(0f, timeScale);

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
            float duration = Mathf.Max(0.01f,
                (EndTimePercentage - StartTimePercentage) * AnimLength);
            float elapsed = (currentTimePercentage - StartTimePercentage) * AnimLength;
            bool continueAfterRange = Config.AffectedByHitStop && Config.HitStopWeight > 0f &&
                                      (CombatGlobalEditorValue.IsPlaying || CombatGlobalEditorValue.IsLooping) &&
                                      shakeHandle != 0;
            if (elapsed < 0f || (elapsed >= duration && !continueAfterRange))
            {
                Release();
                return;
            }

            float localTime = Mathf.InverseLerp(StartTimePercentage, EndTimePercentage,
                currentTimePercentage);
            UpdateShake(elapsed, localTime, 1f, 1f,
                Config.AffectedByHitStop && Config.HitStopWeight > 0f ? duration : 0f);
        }

        private void PreviewConfirmedHit(float currentTimePercentage)
        {
            float hitTime = Mathf.Lerp(StartTimePercentage, EndTimePercentage,
                Config.PreviewHitTime);
            float elapsed = (currentTimePercentage - hitTime) * AnimLength;
            float duration = Mathf.Max(.01f, Config.HitShakeDuration);
            bool continueAfterRange = Config.AffectedByHitStop && Config.HitStopWeight > 0f &&
                                      (CombatGlobalEditorValue.IsPlaying || CombatGlobalEditorValue.IsLooping) &&
                                      shakeHandle != 0;
            if (elapsed < 0f || (elapsed >= duration && !continueAfterRange))
            {
                Release();
                return;
            }

            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            UpdateShake(elapsed, normalizedTime, Config.PreviewHitIntensityScale,
                Config.PreviewHitIntensityScale,
                Config.AffectedByHitStop && Config.HitStopWeight > 0f ? duration : 0f);
        }

        private void UpdateShake(float sampleTime, float normalizedTime, float intensityScale,
            float directionalIntensity = 1f, float hitStopDuration = 0f)
        {
            bool isPlaying = CombatGlobalEditorValue.IsPlaying || CombatGlobalEditorValue.IsLooping;
            float waveTime = waveClock.Sample(sampleTime, isPlaying,
                UnityEditor.EditorApplication.timeSinceStartup, out bool resetPhase,
                Config.AffectedByHitStop && _combatController != null &&
                _combatController._animSpeedExecutor != null
                    ? Mathf.Lerp(1f, _combatController._animSpeedExecutor.CurrentHitStopSpeedScale,
                        Mathf.Clamp01(Config.HitStopWeight)) : 1f);
            if (hitStopDuration > 0f)
            {
                if (waveTime >= hitStopDuration)
                {
                    Release();
                    return;
                }
                normalizedTime = Mathf.Clamp01(waveTime / hitStopDuration);
            }
            if (shakeHandle == 0)
                shakeHandle = CameraShakeRuntime.Add(Config.Settings, intensityScale);
            CameraShakeRuntime.Update(shakeHandle, Config.Settings, waveTime, normalizedTime,
                intensityScale, _combatController != null && _combatController._animator != null
                    ? _combatController._animator.transform.forward : Vector3.forward,
                directionalIntensity, resetPhase);
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
