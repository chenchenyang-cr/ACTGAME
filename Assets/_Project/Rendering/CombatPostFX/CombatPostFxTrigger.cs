using UnityEngine;

namespace CombatPostFX
{
    public sealed class CombatPostFxTrigger : MonoBehaviour
    {
        [SerializeField] private CombatPostFxProfile profile;
        [SerializeField] private bool focusOnThisTransform = true;

        public void Play()
        {
            if (profile == null)
                return;

            if (focusOnThisTransform)
                profile.PlayAt(transform.position);
            else
                profile.Play();
        }

        public void PlayImpact()
        {
            CombatPostFxSettings settings = CombatPostFxSettings.Impact;
            if (focusOnThisTransform)
                settings.center = CombatPostFxRuntime.WorldToViewport(transform.position);
            CombatPostFxRuntime.Pulse(settings);
        }

        public void PlayFinisher()
        {
            CombatPostFxSettings settings = CombatPostFxSettings.Finisher;
            if (focusOnThisTransform)
                settings.center = CombatPostFxRuntime.WorldToViewport(transform.position);
            CombatPostFxRuntime.Pulse(settings, 0.45f);
        }
    }
}
