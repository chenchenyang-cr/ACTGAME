using UnityEngine;

namespace CombatCamera
{
    [CreateAssetMenu(fileName = "CameraShakeProfile",
        menuName = "战斗/摄像机震动预设")]
    public sealed class CameraShakeProfile : ScriptableObject
    {
        [Min(0.01f)] public float Duration = 0.2f;
        public bool UseUnscaledTime = true;
        public bool StartAfterHitStop;
        public bool AffectedByHitStop;
        [Range(0f, 1f)] public float HitStopWeight = 1f;
        public CameraShakeSettings Settings = new CameraShakeSettings();

        public bool HasVisibleOutput => Settings != null && Settings.HasVisibleOutput();
    }
}
