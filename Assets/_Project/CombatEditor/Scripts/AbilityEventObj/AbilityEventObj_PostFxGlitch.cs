using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Post FX/Glitch")]
    public sealed class AbilityEventObj_PostFxGlitch : AbilityEventObj_PostFxTrack
    {
        [Range(1f, 60f)] public float Speed = 28f;
        [Range(10f, 400f)] public float RowDensity = 150f;
        [Range(0f, 0.2f)] public float Displacement = 0.055f;
        [Range(0f, 0.05f)] public float ChannelSplit = 0.006f;

        protected override void Configure(ref CombatPostFxSettings settings, float intensity)
        {
            settings.glitch = intensity;
            settings.glitchSpeed = Speed;
            settings.glitchDensity = RowDensity;
            settings.glitchDisplacement = Displacement;
            settings.glitchChannelSplit = ChannelSplit;
        }
    }
}
