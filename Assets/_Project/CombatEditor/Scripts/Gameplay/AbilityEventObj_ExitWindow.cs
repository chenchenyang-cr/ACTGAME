using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Exit Window")]
    public sealed class AbilityEventObj_ExitWindow : AbilityEventObj_GameplayWindow
    {
        [Tooltip("Allows the game-specific controller to leave its attack state during this range.")]
        public bool AllowControllerExit = true;
        [Tooltip("Immediately asks the game-specific controller to exit when this window starts.")]
        public bool ExitOnWindowEnter = true;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Exit;
    }
}
