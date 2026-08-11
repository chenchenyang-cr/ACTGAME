using UnityEngine;

namespace CombatEditor
{
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
        bool TryHandleHit(HitBox hitBox, Component other, Vector3 hitPoint, HitBoxHitContext hitContext);
    }
}
