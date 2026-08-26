using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Post FX/Color")]
    public sealed class AbilityEventObj_PostFxColor : AbilityEventObj_PostFxTrack
    {
        [Range(0f, 1f)] public float Desaturation = 1f;
        [Range(0f, 1f)] public float TintStrength;
        public Color Tint = Color.white;

        protected override void Configure(ref CombatPostFxSettings settings, float intensity)
        {
            settings.desaturation = intensity * Desaturation;
            settings.tintStrength = intensity * TintStrength;
            settings.tintColor = Tint;
        }
    }
}
