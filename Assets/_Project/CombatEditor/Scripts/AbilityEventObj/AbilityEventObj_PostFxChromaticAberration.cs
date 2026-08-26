using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Post FX/Chromatic Aberration")]
    public sealed class AbilityEventObj_PostFxChromaticAberration : AbilityEventObj_PostFxTrack
    {
        [Range(0.1f, 3f)] public float Spread = 1f;
        public Vector2 FocusPoint = new Vector2(0.5f, 0.5f);

        protected override void Configure(ref CombatPostFxSettings settings, float intensity)
        {
            settings.chromaticAberration = intensity;
            settings.chromaticSpread = Spread;
            settings.center = FocusPoint;
        }
    }
}
