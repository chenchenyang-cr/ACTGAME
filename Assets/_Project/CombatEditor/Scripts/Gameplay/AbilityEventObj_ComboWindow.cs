using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Combo Window")]
    public sealed class AbilityEventObj_ComboWindow : AbilityEventObj_GameplayWindow
    {
        [Tooltip("Game-defined command ID, for example LightAttack. It deliberately does not reference an input enum.")]
        public string CommandId = "LightAttack";
        public AbilityScriptableObject NextAbility;
        [Tooltip("When enabled, a matching buffered command starts Next Ability and is consumed after success.")]
        public bool ConsumeBufferedInput = true;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Combo;
    }
}
