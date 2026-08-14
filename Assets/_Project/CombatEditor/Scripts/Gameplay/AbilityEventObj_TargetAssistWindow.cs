using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Gameplay/Target Assist Window")]
    public sealed class AbilityEventObj_TargetAssistWindow : AbilityEventObj_GameplayWindow
    {
        [Min(0)] public float MaxAcquireDistance = 4f;
        [Min(0)] public float StopDistance = 1.2f;
        [Min(0)] public float MoveSpeed = 8f;
        [Min(0)] public float RotationSpeed = 720f;
        public bool FaceTarget = true;
        public bool HorizontalOnly = true;
        public override CombatGameplayWindowType WindowType => CombatGameplayWindowType.TargetAssist;
    }
}
