using CombatEditor;
using UnityEngine;

namespace UnityLearning.EnemySystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyMotor), typeof(EnemyBrain))]
    [RequireComponent(typeof(EnemyCombatAdapter), typeof(EnemyStateMachine))]
    [RequireComponent(typeof(EnemyDamageReceiver))]
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
        [SerializeField] private EncounterCombatDirector combatDirector;
        [SerializeField] private CharacterHitVisualShake hitVisualShake;

        public EnemyConfig Config => config;
        public Transform Target => target;
        public EnemyBrain Brain => brain;
        public EnemyMotor Motor => motor;
        public EnemyCombatAdapter Combat => combatAdapter;
        public EncounterCombatDirector CombatDirector => combatDirector;
        public float ConfrontationArrivalTolerance => combatDirector != null
            ? combatDirector.SlotArrivalTolerance
            : config.ArrivalTolerance;
        public EnemyStateMachine StateMachine => stateMachine;
        public float PendingStaggerDuration { get; private set; }
        public EnemyLifeState LifeState { get; private set; } = EnemyLifeState.Alive;

        private void OnEnable()
        {
            if (Application.isPlaying)
                ResolveCombatDirector();
        }

        private void Awake()
        {
            if (brain == null) brain = GetComponent<EnemyBrain>();
            if (motor == null) motor = GetComponent<EnemyMotor>();
            if (combatAdapter == null) combatAdapter = GetComponent<EnemyCombatAdapter>();
            if (stateMachine == null) stateMachine = GetComponent<EnemyStateMachine>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            ConfigureHitVisualShake();
            ResolveCombatDirector();
            ConfigureRootMotion();
            if (target == null && combatDirector != null)
                target = combatDirector.Target;
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
            // 对峙时朝向由 EnemyMotor.FaceTarget 独占；动画只提供位移，避免横移动画
            // 的根旋转在 LateUpdate 把角色扭向移动方向，导致 MoveX 被误读成 MoveY。
            applier.SetRootRotationProcessor(IgnoreAnimationRootRotation);

            CombatController combatController = GetComponent<CombatController>();
            if (combatController != null)
                combatController.AllowMotionTranslation = true;
        }

        private void ConfigureHitVisualShake()
        {
            if (animator == null)
                return;

            if (hitVisualShake == null)
                hitVisualShake = GetComponent<CharacterHitVisualShake>();
            if (hitVisualShake == null)
                hitVisualShake = gameObject.AddComponent<CharacterHitVisualShake>();

            hitVisualShake.Initialize(animator.transform);
        }

        private static Quaternion IgnoreAnimationRootRotation(Quaternion animationDeltaRotation)
        {
            return Quaternion.identity;
        }

        private void Update()
        {
            stateMachine?.Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            combatDirector?.Unregister(this);
            motor?.Stop();
            combatAdapter?.InterruptAttack();
        }

        private void ResolveCombatDirector()
        {
            if (combatDirector == null)
                combatDirector = FindObjectOfType<EncounterCombatDirector>();
            combatDirector?.Register(this);
        }

        public bool TryAcquireAttackToken()
        {
            return combatDirector == null || combatDirector.TryAcquireAttackToken(this);
        }

        public void ReleaseAttackToken()
        {
            combatDirector?.ReleaseAttackToken(this);
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
            stateMachine.ChangeState(stateMachine.StaggerState, true);
        }

        public void PlayHitVisualShake()
        {
            if (config == null || !config.EnableHitVisualShake || hitVisualShake == null)
                return;

            hitVisualShake.Play(
                config.HitShakeDuration,
                config.HitShakeFrequency,
                config.HitShakeAmplitude,
                config.HitShakeDecayCurve);
        }

        public void PlayHitRecoil(Transform attacker, Vector3 attackDirection)
        {
            if (config == null || !config.EnableHitRecoil || motor == null)
                return;

            Vector3 recoilDirection = attacker != null
                ? transform.position - attacker.position
                : attackDirection;
            recoilDirection.y = 0f;
            if (recoilDirection.sqrMagnitude <= 0.0001f)
            {
                recoilDirection = attackDirection;
                recoilDirection.y = 0f;
            }

            motor.PlayHitRecoil(
                recoilDirection,
                config.HitRecoilDuration,
                config.HitRecoilSpeed,
                config.HitRecoilDecayCurve);
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

        public void PlayIdle()
        {
            if (IsAnimatorInState(config.IdleState) ||
                IsAnimatorInState(config.LocomotionStopState))
                return;
            if (animator != null &&
                (IsAnimatorInState(config.LocomotionState) ||
                 IsAnimatorInState(config.LocomotionStartState)))
            {
                // 移动状态之间统一由 IsMoving 驱动，保留 Animator 中配置的融合过渡。
                return;
            }
            PlayAnimation(config.IdleState);
        }
        public void PlayAlert() => PlayAnimation(config.AlertState);
        public void PlayLocomotion()
        {
            if (IsAnimatorInState(config.LocomotionState) ||
                IsAnimatorInState(config.LocomotionStartState))
                return;
            if (IsAnimatorInState(config.IdleState) ||
                IsAnimatorInState(config.LocomotionStopState))
            {
                // Idle/Stop 会根据 IsMoving 自动融合到 Start，不再用 CrossFade 抢状态。
                return;
            }
            PlayAnimation(config.LocomotionStartState);
        }
        public void PlayStagger()
        {
            if (animator == null || string.IsNullOrWhiteSpace(config.StaggerState))
                return;

            animator.CrossFadeInFixedTime(
                config.StaggerState,
                config.AnimationBlendDuration,
                0,
                0f);
        }
        public void PlayDeath() => PlayAnimation(config.DeathState);

        public bool IsLocomotionSettledInIdle()
        {
            if (animator == null || config == null)
                return true;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            int idleHash = Animator.StringToHash(config.IdleState);
            // 必须完全进入 Idle 后才开始行为停顿计时；Stop→Idle 正在融合时仍不算完成。
            return !animator.IsInTransition(0) &&
                   (current.shortNameHash == idleHash || current.IsName(config.IdleState));
        }

        private bool IsAnimatorInState(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
                return false;
            int shortNameHash = Animator.StringToHash(stateName);
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == shortNameHash || current.IsName(stateName))
                return true;
            if (!animator.IsInTransition(0))
                return false;

            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            return next.shortNameHash == shortNameHash || next.IsName(stateName);
        }
    }
}
