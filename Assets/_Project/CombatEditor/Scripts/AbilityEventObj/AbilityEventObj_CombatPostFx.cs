using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    // Legacy aggregate event kept only so existing serialized abilities can be migrated.
    public sealed class AbilityEventObj_CombatPostFx : AbilityEventObj
    {
        [Tooltip("Reusable gameplay collection containing independently timed post FX tracks.")]
        public CombatPostFxCollection collection;
        [Tooltip("Viewport-space focus point. (0.5, 0.5) is screen center.")]
        public Vector2 focusPoint = new Vector2(0.5f, 0.5f);

        public override EventTimeType GetEventTimeType()
        {
            return EventTimeType.EventRange;
        }

        public override AbilityEventEffect Initialize()
        {
            return new AbilityEventEffect_CombatPostFx(this);
        }

#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview()
        {
            return new AbilityEventPreview_CombatPostFx(this);
        }

        public override bool PreviewExist()
        {
            return true;
        }
#endif
    }

    public sealed class AbilityEventEffect_CombatPostFx : AbilityEventEffect
    {
        private int _handle;
        private AbilityEventObj_CombatPostFx EventObj => (AbilityEventObj_CombatPostFx)_EventObj;

        public AbilityEventEffect_CombatPostFx(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            if (_handle != 0)
                CombatPostFxRuntime.Remove(_handle);
            if (EventObj.collection != null)
                _handle = CombatPostFxRuntime.Add(CombatPostFxSettings.Default);
        }

        public override void EffectRunning(float currentTimePercentage)
        {
            base.EffectRunning(currentTimePercentage);
            if (_handle == 0)
                return;

            float localTime = Mathf.InverseLerp(eve.GetEventStartTime(), eve.GetEventEndTime(),
                currentTimePercentage);
            CombatPostFxSettings frame = EventObj.collection.Evaluate(localTime, EventObj.focusPoint);
            CombatPostFxRuntime.Update(_handle, frame, 1f);
        }

        public override void EndEffect()
        {
            if (_handle != 0)
            {
                CombatPostFxRuntime.Remove(_handle);
                _handle = 0;
            }
            base.EndEffect();
        }
    }

#if UNITY_EDITOR
    public sealed class AbilityEventPreview_CombatPostFx : AbilityEventPreview
    {
        private int _handle;
        private AbilityEventObj_CombatPostFx EventObj => (AbilityEventObj_CombatPostFx)_EventObj;

        public AbilityEventPreview_CombatPostFx(AbilityEventObj obj) : base(obj) { }

        public override void PreviewUpdateFrame(float currentTimePercentage)
        {
            bool inRange = PreviewInRange(currentTimePercentage);
            if (!inRange)
            {
                Release();
                return;
            }

            if (EventObj.collection == null)
            {
                Release();
                return;
            }

            if (_handle == 0)
                _handle = CombatPostFxRuntime.Add(CombatPostFxSettings.Default);

            float localTime = Mathf.InverseLerp(StartTimePercentage, EndTimePercentage,
                currentTimePercentage);
            CombatPostFxSettings frame = EventObj.collection.Evaluate(localTime, EventObj.focusPoint);
            CombatPostFxRuntime.Update(_handle, frame, 1f);
        }

        public override void BackToStart()
        {
            Release();
        }

        public override void DestroyPreview()
        {
            Release();
        }

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
