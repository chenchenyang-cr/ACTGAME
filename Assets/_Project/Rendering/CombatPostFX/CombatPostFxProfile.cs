using UnityEngine;

namespace CombatPostFX
{
    [CreateAssetMenu(fileName = "CombatPostFxProfile", menuName = "Combat/Post FX Playback Profile")]
    public sealed class CombatPostFxProfile : ScriptableObject
    {
        public CombatPostFxSettings settings = CombatPostFxSettings.Impact;
        [Min(0.01f)] public float duration = 0.16f;
        public AnimationCurve envelope = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        public void Play()
        {
            CombatPostFxRuntime.Pulse(settings, duration, envelope);
        }

        public void PlayAt(Vector3 worldPosition)
        {
            CombatPostFxSettings positioned = settings;
            positioned.center = CombatPostFxRuntime.WorldToViewport(worldPosition);
            CombatPostFxRuntime.Pulse(positioned, duration, envelope);
        }
    }
}
