using System;
using CombatEditor;
using UnityEngine;

namespace UnityLearning.EnemySystem
{
    [Serializable]
    public sealed class EnemyAttackConfig
    {
        [SerializeField] private string displayName = "Light Attack";
        [SerializeField] private AbilityScriptableObject ability;
        [SerializeField, Min(0f)] private float minimumRange = 0.8f;
        [SerializeField, Min(0f)] private float maximumRange = 2.3f;
        [SerializeField, Min(0f)] private float cooldown = 1.2f;
        [SerializeField] private float priority = 1f;
        [SerializeField, Range(0f, 180f)] private float facingTolerance = 20f;
        [SerializeField, Min(0f)] private float entryTolerance = 0.35f;

        public string DisplayName => displayName;
        public AbilityScriptableObject Ability => ability;
        public float MinimumRange => minimumRange;
        public float MaximumRange => Mathf.Max(minimumRange, maximumRange);
        public float PreferredRange => (minimumRange + MaximumRange) * 0.5f;
        public float Cooldown => cooldown;
        public float Priority => priority;
        public float FacingTolerance => facingTolerance;
        public float EntryTolerance => entryTolerance;
    }

    [CreateAssetMenu(menuName = "Enemy/Enemy Config", fileName = "EnemyConfig")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [Header("Perception")]
        [SerializeField, Min(0f)] private float detectionDistance = 8f;
        [SerializeField, Min(0f)] private float loseTargetDistance = 12f;
        [SerializeField, Min(0f)] private float alertDuration = 0.5f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float rotationSpeed = 540f;
        [SerializeField, Min(0.01f)] private float arrivalTolerance = 0.3f;
        [SerializeField, Min(0f)] private float combatEnterDistance = 4f;
        [SerializeField, Min(0f)] private float chaseResumeDistance = 5f;

        [Header("Combat")]
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.25f;
        [SerializeField, Min(0f)] private float attackApproachAllowance = 2f;
        [SerializeField, Min(0f)] private float postAttackRecovery = 1f;
        [SerializeField, Min(0f)] private float defaultStaggerDuration = 0.45f;
        [SerializeField] private EnemyAttackConfig[] attacks = Array.Empty<EnemyAttackConfig>();

        [Header("Vitals")]
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(0f)] private float maximumPoise = 30f;
        [SerializeField, Min(0f)] private float poiseRecoveryDelay = 1.5f;
        [SerializeField, Min(0f)] private float poiseRecoveryPerSecond = 15f;

        [Header("Animation")]
        [SerializeField] private string idleState = "Idle";
        [SerializeField] private string alertState = "Alert";
        [SerializeField] private string locomotionState = "Locomotion";
        [SerializeField] private string locomotionStartState = "LocomotionStart";
        [SerializeField] private string locomotionStopState = "LocomotionStop";
        [SerializeField] private string staggerState = "Hit";
        [SerializeField] private string deathState = "Death";
        [SerializeField] private string moveSpeedParameter = "MoveSpeed";
        [SerializeField] private string moveXParameter = "MoveX";
        [SerializeField] private string moveYParameter = "MoveY";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private string startXParameter = "StartX";
        [SerializeField] private string startYParameter = "StartY";
        [SerializeField] private string stopXParameter = "StopX";
        [SerializeField] private string stopYParameter = "StopY";
        [SerializeField, Min(0f)] private float animationBlendDuration = 0.12f;

        public float DetectionDistance => detectionDistance;
        public float LoseTargetDistance => Mathf.Max(detectionDistance, loseTargetDistance);
        public float AlertDuration => alertDuration;
        public float RotationSpeed => rotationSpeed;
        public float ArrivalTolerance => arrivalTolerance;
        public float CombatEnterDistance => combatEnterDistance;
        public float ChaseResumeDistance => Mathf.Max(combatEnterDistance, chaseResumeDistance);
        public float DecisionInterval => decisionInterval;
        public float AttackApproachAllowance => attackApproachAllowance;
        public float PostAttackRecovery => postAttackRecovery;
        public float DefaultStaggerDuration => defaultStaggerDuration;
        public EnemyAttackConfig[] Attacks => attacks;
        public float MaximumHealth => Mathf.Max(1f, maximumHealth);
        public float MaximumPoise => Mathf.Max(0f, maximumPoise);
        public float PoiseRecoveryDelay => Mathf.Max(0f, poiseRecoveryDelay);
        public float PoiseRecoveryPerSecond => Mathf.Max(0f, poiseRecoveryPerSecond);
        public string IdleState => idleState;
        public string AlertState => alertState;
        public string LocomotionState => locomotionState;
        public string LocomotionStartState => locomotionStartState;
        public string LocomotionStopState => locomotionStopState;
        public string StaggerState => staggerState;
        public string DeathState => deathState;
        public string MoveSpeedParameter => moveSpeedParameter;
        public string MoveXParameter => moveXParameter;
        public string MoveYParameter => moveYParameter;
        public string IsMovingParameter => isMovingParameter;
        public string StartXParameter => startXParameter;
        public string StartYParameter => startYParameter;
        public string StopXParameter => stopXParameter;
        public string StopYParameter => stopYParameter;
        public float AnimationBlendDuration => animationBlendDuration;
    }
}
