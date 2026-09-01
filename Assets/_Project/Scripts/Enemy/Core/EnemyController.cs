using CombatEditor;
using UnityEngine;

namespace UnityLearning.EnemySystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyMotor), typeof(EnemyBrain))]
    [RequireComponent(typeof(EnemyCombatAdapter), typeof(EnemyStateMachine))]
    public sealed class EnemyController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EnemyConfig config;
        [SerializeField] private Transform target;
        [SerializeField] private bool findPlayerTargetOnAwake = true;

        [Header("Dependencies")]
        [SerializeField] private EnemyBrain brain;
        [SerializeField] private EnemyMotor motor;
        [SerializeField] private EnemyCombatAdapter combatAdapter;
        [SerializeField] private EnemyStateMachine stateMachine;
        [SerializeField] private Animator animator;

        public EnemyConfig Config => config;
        public Transform Target => target;
        public EnemyBrain Brain => brain;
        public EnemyMotor Motor => motor;
        public EnemyCombatAdapter Combat => combatAdapter;
        public EnemyStateMachine StateMachine => stateMachine;
        public float PendingStaggerDuration { get; private set; }
        public EnemyLifeState LifeState { get; private set; } = EnemyLifeState.Alive;

        private void Awake()
        {
            if (brain == null) brain = GetComponent<EnemyBrain>();
            if (motor == null) motor = GetComponent<EnemyMotor>();
            if (combatAdapter == null) combatAdapter = GetComponent<EnemyCombatAdapter>();
            if (stateMachine == null) stateMachine = GetComponent<EnemyStateMachine>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            ConfigureRootMotion();
            if (target == null && findPlayerTargetOnAwake) FindPlayerTarget();

            if (config == null)
            {
                Debug.LogError("EnemyConfig is not configured.", this);
                enabled = false;
                return;
            }

            brain?.Initialize(config);
            motor?.Configure(config);
            stateMachine?.Initialize(this);
        }

        private void ConfigureRootMotion()
        {
            if (animator == null)
                return;

            animator.applyRootMotion = true;

            RootMotionReceiver receiver = animator.GetComponent<RootMotionReceiver>();
            if (receiver == null)
                receiver = animator.gameObject.AddComponent<RootMotionReceiver>();

            RootMotionParentApplier applier = GetComponent<RootMotionParentApplier>();
            if (applier == null)
                applier = gameObject.AddComponent<RootMotionParentApplier>();
            applier.SetSourceAnimator(animator);

            CombatController combatController = GetComponent<CombatController>();
            if (combatController != null)
                combatController.AllowMotionTranslation = true;
        }

        private void Update()
        {
            stateMachine?.Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            motor?.Stop();
            combatAdapter?.InterruptAttack();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public bool FindPlayerTarget()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            target = player != null ? player.transform : null;
            return target != null;
        }

        public float DistanceToTarget()
        {
            if (target == null) return float.PositiveInfinity;
            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        public bool IsTargetWithin(float distance)
        {
            return target != null && DistanceToTarget() <= distance;
        }

        public void ActivateCombat()
        {
            if (LifeState == EnemyLifeState.Dead || stateMachine == null)
                return;
            stateMachine.ChangeState(stateMachine.AlertState);
        }

        public void NotifyStagger(float duration = -1f)
        {
            if (LifeState == EnemyLifeState.Dead || stateMachine == null)
                return;

            PendingStaggerDuration = duration >= 0f
                ? duration
                : config.DefaultStaggerDuration;
            stateMachine.ChangeState(stateMachine.StaggerState);
        }

        public void NotifyDied()
        {
            if (LifeState == EnemyLifeState.Dead || stateMachine == null)
                return;

            LifeState = EnemyLifeState.Dead;
            stateMachine.ChangeState(stateMachine.DeadState);
        }

        public void PlayAnimation(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
                return;
            animator.CrossFadeInFixedTime(
                stateName,
                config.AnimationBlendDuration,
                0);
        }

        public void PlayIdle() => PlayAnimation(config.IdleState);
        public void PlayAlert() => PlayAnimation(config.AlertState);
        public void PlayLocomotion() => PlayAnimation(config.LocomotionState);
        public void PlayStagger() => PlayAnimation(config.StaggerState);
        public void PlayDeath() => PlayAnimation(config.DeathState);
    }
}
