using UnityEngine;

namespace CombatCamera
{
    [CreateAssetMenu(fileName = "CameraShakeProfile",
        menuName = "Combat/Camera Shake Profile")]
    public sealed class CameraShakeProfile : ScriptableObject
    {
        [Min(0.01f)] public float Duration = 0.2f;
        public bool UseUnscaledTime = true;
        public CameraShakeSettings Settings = new CameraShakeSettings();

        public bool HasVisibleOutput => Settings != null && Settings.HasVisibleOutput();
    }
}
