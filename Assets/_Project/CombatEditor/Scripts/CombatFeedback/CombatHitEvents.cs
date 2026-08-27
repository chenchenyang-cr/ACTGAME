using System;
using UnityEngine;

namespace CombatEditor
{
    public enum CombatHitResultType
    {
        Normal = 0,
        Critical = 1,
        Blocked = 2,
        Parried = 3,
        Immune = 4
    }

    [Flags]
    public enum CombatHitResultMask
    {
        None = 0,
        Normal = 1 << 0,
        Critical = 1 << 1,
        Blocked = 1 << 2,
        Parried = 1 << 3,
        Immune = 1 << 4,
        All = Normal | Critical | Blocked | Parried | Immune
    }

    public readonly struct CombatHitConfirmedEvent
    {
        public CombatHitConfirmedEvent(CombatController attacker,
            AbilityScriptableObject ability, AbilityEventObj_CreateHitBox sourceHitBoxEvent,
            HitBox hitBox, Component targetCollider, GameObject target, Vector3 hitPoint,
            Vector3 attackDirection, HitBoxHitContext hitContext,
            CombatHitResolution resolution)
        {
            Attacker = attacker;
            Ability = ability;
            SourceHitBoxEvent = sourceHitBoxEvent;
            HitBox = hitBox;
            TargetCollider = targetCollider;
            Target = target;
            HitPoint = hitPoint;
            AttackDirection = attackDirection;
            HitSequenceIndex = hitContext.HitSequenceIndex;
            CameraShakeScale = hitContext.CameraShakeScale *
                               Mathf.Max(0f, resolution.CameraShakeScale);
            HitStopScale = hitContext.HitStopScale *
                           Mathf.Max(0f, resolution.HitStopScale);
            ResultType = resolution.ResultType;
            Damage = resolution.Damage;
            PoiseDamage = resolution.PoiseDamage;
            TargetKilled = resolution.TargetKilled;
        }

        public CombatController Attacker { get; }
        public AbilityScriptableObject Ability { get; }
        public AbilityEventObj_CreateHitBox SourceHitBoxEvent { get; }
        public HitBox HitBox { get; }
        public Component TargetCollider { get; }
        public GameObject Target { get; }
        public Vector3 HitPoint { get; }
        public Vector3 AttackDirection { get; }
        public int HitSequenceIndex { get; }
        public float CameraShakeScale { get; }
        public float HitStopScale { get; }
        public CombatHitResultType ResultType { get; }
        public float Damage { get; }
        public float PoiseDamage { get; }
        public bool TargetKilled { get; }

        public CombatHitResultMask ResultMask => (CombatHitResultMask)(1 << (int)ResultType);
    }

    public static class CombatHitEventBus
    {
        public static event Action<CombatHitConfirmedEvent> HitConfirmed;

        public static void Publish(CombatHitConfirmedEvent hitEvent)
        {
            HitConfirmed?.Invoke(hitEvent);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            HitConfirmed = null;
        }
    }
}
