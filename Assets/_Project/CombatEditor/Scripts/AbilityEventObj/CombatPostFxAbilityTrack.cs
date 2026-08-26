using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    public abstract class AbilityEventObj_PostFxTrack : AbilityEventObj
    {
        [Min(0f)] public float Intensity = 1f;
        [MyAnimationCurve]
        public AnimationCurve IntensityCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        public override EventTimeType GetEventTimeType() => EventTimeType.EventRange;
        public override AbilityEventEffect Initialize() => new AbilityEventEffect_PostFxTrack(this);

        internal CombatPostFxSettings Evaluate(float normalizedTrackTime)
        {
            CombatPostFxSettings settings = CombatPostFxSettings.Default;
            float curveValue = IntensityCurve != null
                ? IntensityCurve.Evaluate(Mathf.Clamp01(normalizedTrackTime))
                : 1f;
            Configure(ref settings, Mathf.Max(0f, Intensity * curveValue));
            return settings;
        }

        protected abstract void Configure(ref CombatPostFxSettings settings, float intensity);

#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview() => new AbilityEventPreview_PostFxTrack(this);
        public override bool PreviewExist() => true;
#endif
    }

    public sealed class AbilityEventEffect_PostFxTrack : AbilityEventEffect
    {
        private int _handle;
        private AbilityEventObj_PostFxTrack Track => (AbilityEventObj_PostFxTrack)_EventObj;

        public AbilityEventEffect_PostFxTrack(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            Release();
            _handle = CombatPostFxRuntime.Add(CombatPostFxSettings.Default);
        }

        public override void EffectRunning(float currentTimePercentage)
        {
            base.EffectRunning(currentTimePercentage);
            if (_handle == 0)
                return;
            float localTime = Mathf.InverseLerp(eve.GetEventStartTime(), eve.GetEventEndTime(),
                currentTimePercentage);
            CombatPostFxRuntime.Update(_handle, Track.Evaluate(localTime), 1f);
        }

        public override void EndEffect()
        {
            Release();
            base.EndEffect();
        }

        private void Release()
        {
            if (_handle == 0)
                return;
            CombatPostFxRuntime.Remove(_handle);
            _handle = 0;
        }
    }

#if UNITY_EDITOR
    public sealed class AbilityEventPreview_PostFxTrack : AbilityEventPreview
    {
        private int _handle;
        private AbilityEventObj_PostFxTrack Track => (AbilityEventObj_PostFxTrack)_EventObj;

        public AbilityEventPreview_PostFxTrack(AbilityEventObj obj) : base(obj) { }

        public override void PreviewUpdateFrame(float currentTimePercentage)
        {
            if (!PreviewInRange(currentTimePercentage))
            {
                Release();
                return;
            }
            if (_handle == 0)
                _handle = CombatPostFxRuntime.Add(CombatPostFxSettings.Default);
            float localTime = Mathf.InverseLerp(StartTimePercentage, EndTimePercentage,
                currentTimePercentage);
            CombatPostFxRuntime.Update(_handle, Track.Evaluate(localTime), 1f);
        }

        public override void BackToStart() => Release();
        public override void DestroyPreview() => Release();

        private void Release()
        {
            if (_handle == 0)
                return;
            CombatPostFxRuntime.Remove(_handle);
            _handle = 0;
        }
    }
#endif
}
