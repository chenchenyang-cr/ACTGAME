using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Post FX/Vignette")]
    public sealed class AbilityEventObj_PostFxVignette : AbilityEventObj_PostFxTrack
    {
        [Range(0f, 1f)] public float InnerRadius = 0.22f;
        [Range(0f, 1.5f)] public float OuterRadius = 0.82f;
        public Color Color = Color.black;
        public Vector2 FocusPoint = new Vector2(0.5f, 0.5f);

        protected override void Configure(ref CombatPostFxSettings settings, float intensity)
        {
            settings.vignette = intensity;
            settings.vignetteInner = InnerRadius;
            settings.vignetteOuter = Mathf.Max(InnerRadius + 0.001f, OuterRadius);
            settings.vignetteColor = Color;
            settings.center = FocusPoint;
        }
    }
}
