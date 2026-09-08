using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerAnimationProfile",
    menuName = "Player/Animation Profile")]
public sealed class PlayerAnimationProfile : ScriptableObject
{
    [Header("Action Layer")]
    [SerializeField, Min(0)] private int animatorLayer;

    [Header("Transition Durations")]
    [SerializeField, Min(0f)] private float actionBlendDuration = 0.12f;
    [SerializeField, Min(0f)] private float locomotionReturnBlendDuration = 0.2f;
    [SerializeField, Min(0f)] private float idleReturnBlendDuration = 0.15f;

    [Header("State Paths")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string normalLocomotionLoopStateName = "NormalLocomotion.Loop";
    [SerializeField] private string combatLocomotionLoopStateName = "CombatLocomotion.Loop";
    [SerializeField] private string dodgeNormalStateName = "DodgeNormal";
    [SerializeField] private string dodgeCombatStateName = "DodgeCombat";
    [SerializeField] private string dodgeToFastRunNormalStateName = "DodgeToFastRunNormal";
    [SerializeField] private string dodgeToFastRunCombatStateName = "DodgeToFastRunCombat";

    [Header("Parry")]
    public string ParryEndStateName = "Player_Block_End";
    public string ParryLeftStateName = "Player_Parry_L";
    public string ParryRightStateName = "Player_Parry_R";
    [Min(0f)] public float ParryBlendDuration = 0.12f;
    [Tooltip("Blend duration when transitioning into the parry end animation.")]
    [Min(0f)] public float ParryEndBlendDuration = 0.18f;
    [Tooltip("Normalized startup time at which blending into the end animation begins.")]
    [Range(0f, 1f)] public float ParryStartEndTransitionTime = 0.75f;
    [Header("Hit Reaction")]
    public string HitStateName = "Player_Hit_F";

    [Header("Dodge To Fast Run")]
    [Tooltip("Blend duration for the short dodge-to-sprint recovery, independent of the full action blend.")]
    [SerializeField, Min(0f)] private float dodgeToFastRunBlendDuration = 0.08f;
    [Tooltip("Latest entry time in seconds into the authored dodge-to-run clip, preserving its running recovery even after a late movement input.")]
    [SerializeField, Min(0f)] private float dodgeToFastRunLatestStartTime = 0.8f;

    [Header("Animator Parameters")]
    [SerializeField] private string dodgeXParameter = "DodgeX";
    [SerializeField] private string dodgeYParameter = "DodgeY";
    [SerializeField] private string combatWeightParameter = "CombatWeight";

    [Header("Combat Stance")]
    [SerializeField, Min(0f)] private float combatStanceTimeout = 4f;
    [SerializeField] private string combatExitLayerName = "Combat Upper Body";
    [SerializeField] private string combatExitStateName = "Idle_Combat_To_Idle";
    [SerializeField, Min(0f)] private float combatExitBlendDuration = 0.1f;

    public int AnimatorLayer => animatorLayer;
    public float ActionBlendDuration => actionBlendDuration;
    public float LocomotionReturnBlendDuration => locomotionReturnBlendDuration;
    public float IdleReturnBlendDuration => idleReturnBlendDuration;
    public string IdleStateName => idleStateName;
    public string NormalLocomotionLoopStateName => normalLocomotionLoopStateName;
    public string CombatLocomotionLoopStateName => combatLocomotionLoopStateName;
    public string DodgeNormalStateName => dodgeNormalStateName;
    public string DodgeCombatStateName => dodgeCombatStateName;
    public string DodgeToFastRunNormalStateName => dodgeToFastRunNormalStateName;
    public string DodgeToFastRunCombatStateName => dodgeToFastRunCombatStateName;
    public float DodgeToFastRunLatestStartTime => dodgeToFastRunLatestStartTime;
    public float DodgeToFastRunBlendDuration => dodgeToFastRunBlendDuration;
    public string DodgeXParameter => dodgeXParameter;
    public string DodgeYParameter => dodgeYParameter;
    public string CombatWeightParameter => combatWeightParameter;
    public float CombatStanceTimeout => combatStanceTimeout;
    public string CombatExitLayerName => combatExitLayerName;
    public string CombatExitStateName => combatExitStateName;
    public float CombatExitBlendDuration => combatExitBlendDuration;
}
