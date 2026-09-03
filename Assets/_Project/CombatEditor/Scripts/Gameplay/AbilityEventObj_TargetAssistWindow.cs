using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Target Assist Window")]
    public sealed class AbilityEventObj_TargetAssistWindow : AbilityEventObj_GameplayWindow
    {
        [Min(0f)]
        [Tooltip("Maximum horizontal distance in which an enemy can be selected for direction assist.")]
        public float AcquireRadius = 4f;

        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.TargetAssist;

#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview()
        {
            return new AbilityEventPreview_TargetAssist(this);
        }

        public override bool PreviewExist() => true;
#endif
    }
}
