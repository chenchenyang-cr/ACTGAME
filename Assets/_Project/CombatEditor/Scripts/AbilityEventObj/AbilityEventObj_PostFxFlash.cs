using CombatPostFX;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Post FX/Flash")]
    public sealed class AbilityEventObj_PostFxFlash : AbilityEventObj_PostFxTrack
    {
        public Color Color = Color.white;

        protected override void Configure(ref CombatPostFxSettings settings, float intensity)
        {
            settings.flash = intensity;
            settings.flashColor = Color;
        }
    }
}
