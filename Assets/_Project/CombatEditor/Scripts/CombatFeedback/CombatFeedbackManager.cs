using System.Collections.Generic;
using CombatCamera;
using UnityEngine;

namespace CombatEditor
{
    public enum CameraShakeHitBoxFilter
    {
        [InspectorName("全部碰撞盒")]
        AnyHitBoxInAbility,
        [InspectorName("指定碰撞盒")]
        SpecificHitBox
    }

    public enum CameraShakeHitTriggerPolicy
    {
        [InspectorName("仅首次命中")]
        FirstHitOnly,
        [InspectorName("每个目标一次")]
        OncePerTarget,
        [InspectorName("每次确认命中")]
        EveryConfirmedHit,
        [InspectorName("按间隔重复")]
        EveryHitWithCooldown
    }

    public sealed class CameraShakeHitBinding
    {
        public CombatController Owner;
        public AbilityScriptableObject Ability;
        public CameraShakeHitBoxFilter HitBoxFilter;
        public AbilityEventObj_HitSource SpecificHitBox;
        public CameraShakeHitTriggerPolicy TriggerPolicy;
        public CombatHitResultMask ResultMask;
        public int MaximumTriggerCount;
        public float Cooldown;
        public float Duration;
        public bool UseUnscaledTime;
        public bool StartAfterHitStop;
        public bool AffectedByHitStop;
        public float HitStopWeight;
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

        public static void TriggerHitShake(CameraShakeSettings settings, float duration,
            float intensityScale, bool useUnscaledTime, Vector3 attackDirection,
            CombatController attacker, GameObject target, bool waitForHitStop,
            System.Func<float> timeScale = null)
        {
            Transform targetRoot = target != null ? target.transform.root : null;
            CombatController targetController = target != null
                ? target.GetComponentInParent<CombatController>() : null;
            if (targetController == null && targetRoot != null)
                targetController = targetRoot.GetComponentInChildren<CombatController>(true);

            int targetId = targetRoot != null ? targetRoot.GetInstanceID()
                : attacker != null ? attacker.GetInstanceID() : 0;
            System.Func<bool> isHitStopActive = waitForHitStop
                ? () => IsHitStopActive(attacker) || IsHitStopActive(targetController)
                : null;
            CameraShakeRuntime.PulseAfterHitStop(targetId, settings, duration,
                intensityScale, useUnscaledTime, attackDirection, isHitStopActive,
                timeScale);
        }

        private static bool IsHitStopActive(CombatController controller)
        {
            return controller != null && controller._animSpeedExecutor != null &&
                   controller._animSpeedExecutor.IsHitStopActive;
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
            if (hitEvent.ResultType == CombatHitResultType.Parried ||
                hitEvent.ResultType == CombatHitResultType.PerfectDodged) return;
            // Inline HitBox shake is authoritative for this confirmed hit.
            // Legacy OnConfirmedHit tracks remain available when it is disabled.
            if (hitEvent.SourceHitBoxEvent != null && hitEvent.SourceHitBoxEvent.HitProfile != null &&
                hitEvent.SourceHitBoxEvent.HitProfile.EnableHitCameraShake)
                return;

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

                TriggerHitShake(binding.Settings, binding.Duration,
                    hitEvent.CameraShakeScale, binding.UseUnscaledTime,
                    hitEvent.AttackDirection, hitEvent.Attacker, hitEvent.Target,
                    binding.StartAfterHitStop && hitEvent.SourceHitBoxEvent != null && hitEvent.SourceHitBoxEvent.HitProfile != null &&
                    hitEvent.SourceHitBoxEvent.HitProfile.EnableHitAnimationSpeed &&
                    hitEvent.SourceHitBoxEvent.HitProfile.HitStopFrames > 0,
                    binding.AffectedByHitStop && binding.HitStopWeight > 0f
                        ? () => binding.Owner != null && binding.Owner._animSpeedExecutor != null
                            ? Mathf.Lerp(1f, binding.Owner._animSpeedExecutor.CurrentHitStopSpeedScale,
                                Mathf.Clamp01(binding.HitStopWeight)) : 1f
                        : (System.Func<float>)null);
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
