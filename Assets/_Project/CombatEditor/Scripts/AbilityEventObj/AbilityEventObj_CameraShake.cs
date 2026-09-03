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
        private AbilityEventObj_CameraShake Config =>
            (AbilityEventObj_CameraShake)_EventObj;

        public AbilityEventEffect_CameraShake(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            Release();

            if (Config.TriggerMode == CameraShakeTriggerMode.Direct)
            {
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
            float duration = GetEventDurationSeconds();
            CameraShakeRuntime.Update(shakeHandle, Config.Settings, localTime * duration,
                localTime);
        }

        public override void EndEffect()
        {
            Release();
            base.EndEffect();
        }

        private float GetEventDurationSeconds()
        {
            float clipLength = AnimObj != null && AnimObj.Clip != null
                ? AnimObj.Clip.length
                : 1f;
            return Mathf.Max(0.01f,
                (eve.GetEventEndTime() - eve.GetEventStartTime()) * clipLength);
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
    public sealed class AbilityEventPreview_CameraShake : AbilityEventPreview
    {
        private int shakeHandle;
        private AbilityEventObj_CameraShake Config =>
            (AbilityEventObj_CameraShake)_EventObj;

        public AbilityEventPreview_CameraShake(AbilityEventObj obj) : base(obj) { }

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
            if (!PreviewInRange(currentTimePercentage))
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
            if (shakeHandle == 0)
                shakeHandle = CameraShakeRuntime.Add(Config.Settings, intensityScale);
            CameraShakeRuntime.Update(shakeHandle, Config.Settings, sampleTime, normalizedTime,
                intensityScale);
        }

        private void Release()
        {
            CameraShakeRuntime.Remove(shakeHandle);
            shakeHandle = 0;
        }
    }
#endif
}
