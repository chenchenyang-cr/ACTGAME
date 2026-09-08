using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Parry Window")]
    public sealed class AbilityEventObj_ParryWindow : AbilityEventObj_GameplayWindow
    {
        [Range(0f, 360f)] public float FacingAngle = 120f;
        [Min(0f)] public float AttackerStaggerDuration = 0.8f;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.Parry;

        // Shared by runtime and tests; do not wrap time when a clip loops.
        public bool Contains(float time, Vector2 range, Vector3 forward, Vector3 toAttacker)
        {
            if (!IsActive || time < range.x || time >= range.y) return false;
            forward.y = 0f;
            toAttacker.y = 0f;
            return toAttacker.sqrMagnitude < 0.0001f ||
                   Vector3.Angle(forward, toAttacker) <= FacingAngle * 0.5f;
        }
    }
}
