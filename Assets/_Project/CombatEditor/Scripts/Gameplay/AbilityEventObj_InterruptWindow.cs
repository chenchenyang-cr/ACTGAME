using System;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Interrupt Window")]
    public sealed class AbilityEventObj_InterruptWindow : AbilityEventObj_GameplayWindow
    {
        [Tooltip("Comma separated game-defined command IDs, for example Dodge,HeavyAttack.")]
        public string AllowedCommandIds = "Dodge";
        [Tooltip("Allow movement input to cancel the current ability while this window is active.")]
        public bool AllowMovement;
        [Min(0)] public int MinimumPriority;
        public bool AllowHitReaction = true;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Interrupt;

        public bool Allows(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId) || string.IsNullOrWhiteSpace(AllowedCommandIds))
            {
                return false;
            }

            string[] ids = AllowedCommandIds.Split(',');
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i].Trim(), commandId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
