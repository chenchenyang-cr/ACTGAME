using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Post FX/Film Grain")]
    public sealed class AbilityEventObj_PostFxFilmGrain : AbilityEventObj_PostFxTrack
    {
        [Range(0.25f, 8f)] public float Scale = 1f;
        [Range(0f, 60f)] public float Speed = 24f;

        protected override void Configure(ref CombatPostFxSettings settings, float intensity)
        {
            settings.filmGrain = intensity;
            settings.filmGrainScale = Scale;
            settings.filmGrainSpeed = Speed;
        }
    }
}
