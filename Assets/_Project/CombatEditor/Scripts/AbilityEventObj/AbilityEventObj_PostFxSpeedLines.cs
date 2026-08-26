using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Post FX/Speed Lines")]
    public sealed class AbilityEventObj_PostFxSpeedLines : AbilityEventObj_PostFxTrack
    {
        [Range(4f, 100f)] public float Density = 34f;
        [Range(1f, 40f)] public float Sharpness = 18f;
        [Range(0f, 1f)] public float InnerRadius = 0.12f;
        [Range(0f, 1.5f)] public float OuterRadius = 0.72f;
        [Range(-10f, 10f)] public float RotationSpeed;
        public Color Color = Color.white;
        public Vector2 FocusPoint = new Vector2(0.5f, 0.5f);

        protected override void Configure(ref CombatPostFxSettings settings, float intensity)
        {
            settings.speedLines = intensity;
            settings.speedLineDensity = Density;
            settings.speedLineSharpness = Sharpness;
            settings.speedLineInnerRadius = InnerRadius;
            settings.speedLineOuterRadius = Mathf.Max(InnerRadius + 0.001f, OuterRadius);
            settings.speedLineRotationSpeed = RotationSpeed;
            settings.speedLineColor = Color;
            settings.center = FocusPoint;
        }
    }
}
