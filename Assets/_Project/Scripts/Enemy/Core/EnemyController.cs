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
        public Vector2 PendingHitDirection { get; private set; } = Vector2.up;
        public EnemyLifeState LifeState { get; private set; } = EnemyLifeState.Alive;
        private bool pendingImmediateStagger;

        private void OnEnable()
        {
            GlobalHitStopRuntime.Started += OnGlobalHitStopStarted;
            if (Application.isPlaying)
                ResolveCombatDirector();
        }

        private void OnGlobalHitStopStarted()
        {
            stateMachine?.StaggerState?.OnGlobalHitStopStarted();
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
            float visualSpeed = GlobalHitStopRuntime.CurrentVisualSpeed;
            if (visualSpeed <= 0.0001f) return;
            stateMachine?.Tick(Time.deltaTime * visualSpeed);
        }

        private void OnDisable()
        {
            GlobalHitStopRuntime.Started -= OnGlobalHitStopStarted;
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

        public void NotifyStagger(float duration = -1f, Transform attacker = null)
        {
            if (LifeState == EnemyLifeState.Dead || stateMachine == null)
                return;

            pendingImmediateStagger = false;
            PendingStaggerDuration = duration >= 0f
                ? duration
                : config.DefaultStaggerDuration;
            Transform hitSource = attacker != null ? attacker : target;
            PendingHitDirection = CalculateHitDirection(transform.rotation,
                hitSource != null ? hitSource.position - transform.position : Vector3.zero);
            stateMachine.StaggerState.ClearParryWait();
            stateMachine.ChangeState(stateMachine.StaggerState, true);
        }

        public void NotifyParried(float duration = -1f, bool waitForHitStop = false)
        {
            if (LifeState == EnemyLifeState.Dead || stateMachine == null)
                return;

            PendingStaggerDuration = duration >= 0f
                ? duration
                : config.DefaultStaggerDuration;
            PendingHitDirection = Vector2.up;
            pendingImmediateStagger = true;
            if (waitForHitStop)
                stateMachine.StaggerState.WaitForParryHitStop();
            else
                stateMachine.StaggerState.ClearParryWait();
            stateMachine.ChangeState(stateMachine.StaggerState, true);
        }

        internal static Vector2 CalculateHitDirection(Quaternion facing, Vector3 towardAttacker)
        {
            // Hit_F means the player is in front of the enemy.
            Vector3 incoming = towardAttacker;
            incoming.y = 0f;
            if (incoming.sqrMagnitude < 0.0001f)
                return Vector2.up;

            Vector3 local = Quaternion.Inverse(facing) * incoming;
            return new Vector2(local.x, local.z).normalized;
        }

        public void PlayHitVisualShake(in CombatHitRequest request)
        {
            if (config == null || !config.EnableHitVisualShake || hitVisualShake == null)
                return;

            Vector3 direction = request.AttackDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = request.Attacker != null
                    ? transform.position - request.Attacker.transform.position
                    : -transform.forward;
            hitVisualShake.Play(
                config.HitShakeDuration,
                config.HitShakeFrequency,
                config.HitShakeAmplitude,
                config.HitShakeDecayCurve, direction,
                config.HitShakeAffectedByHitStop, GetComponent<CombatController>());
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
                config.HitRecoilDecayCurve,
                config.HitRecoilAffectedByHitStop, GetComponent<CombatController>());
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
            if (IsAnimatorInState(config.IdleState))
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
            if (animator == null)
                return;
            if (IsAnimatorInState(config.LocomotionState) ||
                IsAnimatorInState(config.LocomotionStartState))
                return;
            if (IsAnimatorInState(config.IdleState))
            {
                // Idle 会根据 IsMoving 自动融合到 Start，不再用 CrossFade 抢状态。
                return;
            }
            // 与 Idle→Start 过渡的 0.1 偏移一致，跳过原动画开头的骨盆下压。
            animator.CrossFadeInFixedTime(
                config.LocomotionStartState,
                config.AnimationBlendDuration,
                0,
                0.27f);
        }
        public void PlayStagger()
        {
            if (animator == null || string.IsNullOrWhiteSpace(config.StaggerState))
                return;

            animator.SetFloat(config.HitXParameter, PendingHitDirection.x);
            animator.SetFloat(config.HitYParameter, PendingHitDirection.y);
            if (pendingImmediateStagger || IsAnimatorInState(config.StaggerState))
            {
                // A parry must show the front hit pose immediately; repeated hits also restart it.
                animator.Play(config.StaggerState, 0, 0f);
                if (pendingImmediateStagger)
                    animator.Update(0f);
                return;
            }
            animator.CrossFadeInFixedTime(
                config.StaggerState,
                config.AnimationBlendDuration,
                0,
                0f);
        }
        public void PlayHitRecovery()
        {
            if (animator == null || config == null ||
                string.IsNullOrWhiteSpace(config.IdleState))
                return;

            animator.CrossFadeInFixedTime(
                config.IdleState,
                config.HitRecoveryBlendDuration,
                0);
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
