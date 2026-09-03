using UnityEngine;

namespace CombatEditor
{
    public static class CombatTimeline
    {
        public const float FramesPerSecond = 60f;

        public static int ToFrame(float normalizedTime, AnimationClip clip)
        {
            float clipLength = clip != null ? clip.length : 0f;
            return Mathf.FloorToInt(Mathf.Max(0f, normalizedTime) * clipLength *
                                    FramesPerSecond + 0.0001f);
        }
    }

    public enum CombatTeam
    {
        Neutral,
        Player,
        Enemy
    }

    public enum CombatHitMode
    {
        Single,
        Repeated
    }

    public enum CombatHitReactionPolicy
    {
        None,
        FirstHitOnly,
        EveryHit,
        PoiseBreakOnly
    }

    public readonly struct CombatHitRequest
    {
        public CombatHitRequest(CombatController attacker, AbilityScriptableObject ability,
            AbilityEventObj_CreateHitBox sourceEvent, HitBox hitBox, Component targetCollider,
            Vector3 hitPoint, Vector3 attackDirection, int hitSequenceIndex, float damage,
            float poiseDamage, CombatHitReactionPolicy hitReaction, float staggerDuration)
        {
            Attacker = attacker;
            Ability = ability;
            SourceEvent = sourceEvent;
            HitBox = hitBox;
            TargetCollider = targetCollider;
            HitPoint = hitPoint;
            AttackDirection = attackDirection;
            HitSequenceIndex = hitSequenceIndex;
            Damage = Mathf.Max(0f, damage);
            PoiseDamage = Mathf.Max(0f, poiseDamage);
            HitReaction = hitReaction;
            StaggerDuration = Mathf.Max(0f, staggerDuration);
        }

        public CombatController Attacker { get; }
        public AbilityScriptableObject Ability { get; }
        public AbilityEventObj_CreateHitBox SourceEvent { get; }
        public HitBox HitBox { get; }
        public Component TargetCollider { get; }
        public Vector3 HitPoint { get; }
        public Vector3 AttackDirection { get; }
        public int HitSequenceIndex { get; }
        public float Damage { get; }
        public float PoiseDamage { get; }
        public CombatHitReactionPolicy HitReaction { get; }
        public float StaggerDuration { get; }
    }

    public interface ICombatTeamProvider
    {
        CombatTeam Team { get; }
    }

    public interface ICombatDamageReceiver : ICombatTeamProvider
    {
        bool TryReceiveHit(in CombatHitRequest request, out CombatHitResolution resolution);
    }
}
