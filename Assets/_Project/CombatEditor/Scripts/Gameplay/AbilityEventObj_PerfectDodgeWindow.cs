using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Perfect Dodge Window")]
    public sealed class AbilityEventObj_PerfectDodgeWindow : AbilityEventObj_GameplayWindow
    {
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.PerfectDodge;
    }
}
