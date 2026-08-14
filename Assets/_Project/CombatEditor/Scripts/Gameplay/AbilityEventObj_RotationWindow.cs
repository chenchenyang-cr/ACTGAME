using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Rotation Window")]
    public sealed class AbilityEventObj_RotationWindow : AbilityEventObj_GameplayWindow
    {
        public CombatRotationPolicy Policy = CombatRotationPolicy.Animation;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Rotation;
    }
}
