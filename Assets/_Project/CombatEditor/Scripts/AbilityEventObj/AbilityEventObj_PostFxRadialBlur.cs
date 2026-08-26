using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Post FX/Radial Blur")]
    public sealed class AbilityEventObj_PostFxRadialBlur : AbilityEventObj_PostFxTrack
    {
        [Range(0.1f, 3f)] public float SampleDistance = 1f;
        public Vector2 FocusPoint = new Vector2(0.5f, 0.5f);

        protected override void Configure(ref CombatPostFxSettings settings, float intensity)
        {
            settings.radialBlur = intensity;
            settings.radialBlurDistance = SampleDistance;
            settings.center = FocusPoint;
        }
    }
}
