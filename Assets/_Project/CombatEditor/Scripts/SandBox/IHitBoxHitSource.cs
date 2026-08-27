using UnityEngine;

namespace CombatEditor
{
    public readonly struct CombatHitResolution
    {
        public CombatHitResolution(bool isAccepted,
            CombatHitResultType resultType = CombatHitResultType.Normal,
            float damage = 0f, float poiseDamage = 0f, bool targetKilled = false,
            float cameraShakeScale = 1f, float hitStopScale = 1f)
        {
            IsAccepted = isAccepted;
            ResultType = resultType;
            Damage = damage;
            PoiseDamage = poiseDamage;
            TargetKilled = targetKilled;
            CameraShakeScale = cameraShakeScale;
            HitStopScale = hitStopScale;
        }

        public bool IsAccepted { get; }
        public CombatHitResultType ResultType { get; }
        public float Damage { get; }
        public float PoiseDamage { get; }
        public bool TargetKilled { get; }
        public float CameraShakeScale { get; }
        public float HitStopScale { get; }

        public static CombatHitResolution Normal(float damage = 0f,
            float poiseDamage = 0f, bool targetKilled = false)
        {
            return new CombatHitResolution(true, CombatHitResultType.Normal,
                damage, poiseDamage, targetKilled);
        }

        public static CombatHitResolution Rejected => new CombatHitResolution(false);
    }

    public readonly struct HitBoxHitContext
    {
        public HitBoxHitContext(int hitSequenceIndex, float cameraShakeScale, float hitStopScale)
        {
            HitSequenceIndex = hitSequenceIndex;
            CameraShakeScale = cameraShakeScale;
            HitStopScale = hitStopScale;
        }

        public int HitSequenceIndex { get; }
        public float CameraShakeScale { get; }
        public float HitStopScale { get; }
    }

    public interface IHitBoxHitSource
    {
        bool TryHandleHit(HitBox hitBox, Component other, Vector3 hitPoint,
            HitBoxHitContext hitContext, out CombatHitResolution resolution);
    }
}
