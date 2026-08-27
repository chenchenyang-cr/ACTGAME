using System.Collections.Generic;
using CombatCamera;
using UnityEngine;

namespace CombatEditor
{
    public enum CameraShakeHitBoxFilter
    {
        AnyHitBoxInAbility,
        SpecificHitBox
    }

    public enum CameraShakeHitTriggerPolicy
    {
        FirstHitOnly,
        OncePerTarget,
        EveryConfirmedHit,
        EveryHitWithCooldown
    }

    public sealed class CameraShakeHitBinding
    {
        public CombatController Owner;
        public AbilityScriptableObject Ability;
        public CameraShakeHitBoxFilter HitBoxFilter;
        public AbilityEventObj_CreateHitBox SpecificHitBox;
        public CameraShakeHitTriggerPolicy TriggerPolicy;
        public CombatHitResultMask ResultMask;
        public int MaximumTriggerCount;
        public float Cooldown;
        public float Duration;
        public bool UseUnscaledTime;
        public CameraShakeSettings Settings;

        internal int TriggerCount;
        internal float NextTriggerTime;
        internal readonly HashSet<int> TriggeredTargets = new HashSet<int>();
    }

    public static class CombatFeedbackManager
    {
        private static readonly Dictionary<int, CameraShakeHitBinding> CameraShakeBindings =
            new Dictionary<int, CameraShakeHitBinding>();
        private static int nextHandle = 1;
        private static bool subscribed;

        public static int RegisterCameraShake(CameraShakeHitBinding binding)
        {
            if (binding == null || binding.Settings == null)
                return 0;

            EnsureSubscribed();
            int handle = nextHandle++;
            CameraShakeBindings.Add(handle, binding);
            return handle;
        }

        public static void UnregisterCameraShake(int handle)
        {
            if (handle != 0)
                CameraShakeBindings.Remove(handle);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (subscribed)
                CombatHitEventBus.HitConfirmed -= OnHitConfirmed;
            CameraShakeBindings.Clear();
            nextHandle = 1;
            subscribed = false;
        }

        private static void EnsureSubscribed()
        {
            if (subscribed)
                return;
            CombatHitEventBus.HitConfirmed += OnHitConfirmed;
            subscribed = true;
        }

        private static void OnHitConfirmed(CombatHitConfirmedEvent hitEvent)
        {
            foreach (CameraShakeHitBinding binding in CameraShakeBindings.Values)
            {
                if (!Matches(binding, hitEvent))
                    continue;

                int targetId = hitEvent.Target != null ? hitEvent.Target.GetInstanceID() : 0;
                if (!CanTrigger(binding, targetId))
                    continue;

                binding.TriggerCount++;
                if (targetId != 0)
                    binding.TriggeredTargets.Add(targetId);
                binding.NextTriggerTime = Time.unscaledTime + Mathf.Max(0f, binding.Cooldown);

                CameraShakeRuntime.Pulse(binding.Settings, binding.Duration,
                    hitEvent.CameraShakeScale, binding.UseUnscaledTime);
            }
        }

        private static bool Matches(CameraShakeHitBinding binding,
            CombatHitConfirmedEvent hitEvent)
        {
            if (binding.Owner != null && binding.Owner != hitEvent.Attacker)
                return false;
            if (binding.Ability != null && binding.Ability != hitEvent.Ability)
                return false;
            if ((binding.ResultMask & hitEvent.ResultMask) == 0)
                return false;
            return binding.HitBoxFilter != CameraShakeHitBoxFilter.SpecificHitBox ||
                   binding.SpecificHitBox == hitEvent.SourceHitBoxEvent;
        }

        private static bool CanTrigger(CameraShakeHitBinding binding, int targetId)
        {
            if (binding.MaximumTriggerCount > 0 &&
                binding.TriggerCount >= binding.MaximumTriggerCount)
                return false;

            switch (binding.TriggerPolicy)
            {
                case CameraShakeHitTriggerPolicy.FirstHitOnly:
                    return binding.TriggerCount == 0;
                case CameraShakeHitTriggerPolicy.OncePerTarget:
                    return targetId == 0 || !binding.TriggeredTargets.Contains(targetId);
                case CameraShakeHitTriggerPolicy.EveryHitWithCooldown:
                    return Time.unscaledTime >= binding.NextTriggerTime;
                default:
                    return true;
            }
        }
    }
}
