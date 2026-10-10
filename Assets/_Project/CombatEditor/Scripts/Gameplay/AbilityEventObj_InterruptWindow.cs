using System.Collections.Generic;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Interrupt Window")]
    public sealed class AbilityEventObj_InterruptWindow : AbilityEventObj_GameplayWindow
    {
        [Tooltip("Commands accepted during this window. Drag to arrange them in the editor.")]
        public List<PlayerActionCommand> AllowedCommands = new List<PlayerActionCommand> { PlayerActionCommand.Dodge };
        [Tooltip("Allow movement input to cancel the current ability while this window is active.")]
        public bool AllowMovement;
        [Min(0)] public int MinimumPriority;
        public bool AllowHitReaction = true;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Interrupt;

        public bool Allows(PlayerActionCommand command)
        {
            if (AllowedCommands == null) return false;
            for (int i = 0; i < AllowedCommands.Count; i++)
            {
                if (AllowedCommands[i] == command) return true;
            }
            return false;
        }
    }
}
