using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Target Assist Window")]
    public sealed class AbilityEventObj_TargetAssistWindow : AbilityEventObj_GameplayWindow
    {
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.TargetAssist;
    }
}
