using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Parry Opportunity Window")]
    public sealed class AbilityEventObj_ParryOpportunityWindow : AbilityEventObj_GameplayWindow
    {
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.ParryOpportunity;
    }
}
