using System;
using CombatEditor;
using UnityEngine;
using UnityLearning.EnemySystem;

[RequireComponent(typeof(PlayerInputReader), typeof(PlayerInputBuffer))]
[RequireComponent(typeof(CharacterController), typeof(PlayerMovement))]
public sealed class PlayerStateMachine : MonoBehaviour
{
    [SerializeField]
    private PlayerInputReader inputReader;
    [SerializeField]
    private PlayerInputBuffer inputBuffer;
    [SerializeField]
    private PlayerMovement playerMovement;
    [SerializeField]
    private CharacterController characterController;
    [SerializeField]
    private PlayerCombatAdapter combatAdapter;
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private PlayerAnimationProfile animationProfile;
    [Header("Dodge Movement")]
    [SerializeField, Min(0f)]
    private float dodgeRootMotionMultiplier = 1.2f;
    [Header("Available Actions")]
    [SerializeField] private bool locomotionOnly;

    public PlayerState CurrentState { get; private set; }
    public IdleState IdleState { get; private set; }
    public LocomotionState LocomotionState { get; private set; }
    public AttackState AttackState { get; private set; }
    public DodgeState DodgeState { get; private set; }
    public ParryState ParryState { get; private set; }
    public PlayerAnimationProfile AnimationProfile => animationProfile;
    public HitState HitState { get; private set; }

    public PlayerMovement Movement => playerMovement;
    public PlayerCombatAdapter Combat => combatAdapter;
    public PlayerActionAnimator ActionAnimator { get; private set; }
    public bool IsGrounded => playerMovement != null
        ? playerMovement.IsGrounded
        : characterController != null && characterController.isGrounded;

    public event Action HitStateEntered;

    private Vector2 lastMoveInput;
    private bool lastHasMoveInput;
    private PlayerCombatStanceAnimator combatStanceAnimator;

    internal Vector2 LatestMoveInput => lastMoveInput;
    internal bool HasLatestMoveInput => lastHasMoveInput;
    internal float DodgeRootMotionMultiplier => dodgeRootMotionMultiplier;

    private void Awake()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }
        if (inputBuffer == null)
        {
            inputBuffer = GetComponent<PlayerInputBuffer>();
        }
        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
        }
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
        if (combatAdapter == null)
        {
            combatAdapter = GetComponent<PlayerCombatAdapter>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (animationProfile == null)
        {
            Debug.LogError("Player Animation Profile is not configured.", this);
            enabled = false;
            return;
        }

        ActionAnimator = new PlayerActionAnimator(
            animator,
            animationProfile,
            this);

        combatStanceAnimator = new PlayerCombatStanceAnimator(
            animator,
            animationProfile);

        IdleState = new IdleState(this);
        LocomotionState = new LocomotionState(this);
        AttackState = new AttackState(this);
        DodgeState = new DodgeState(this);
        HitState = new HitState(this);
        ParryState = new ParryState(this);

        ChangeState(IdleState);
    }

    private void Update()
    {
        Vector2 moveInput = inputReader.State.MoveInput;
        bool hasMoveInput = inputReader.HasActiveMoveControl();

        lastMoveInput = moveInput;
        lastHasMoveInput = hasMoveInput;

        ProcessBufferedCommand(inputBuffer);
        CurrentState?.Tick(moveInput, hasMoveInput);
        bool canLeaveCombatStance = CurrentState == IdleState ||CurrentState == LocomotionState;
        combatStanceAnimator?.Tick(canLeaveCombatStance);
    }

    private void ProcessBufferedCommand(PlayerInputBuffer inputBuffer)
    {
        if (locomotionOnly)
        {
            inputBuffer?.Clear();
            return;
        }
        if (CurrentState != null && inputBuffer != null && inputBuffer.TryPeekParry(out _) &&
            CurrentState.TryHandleCommand(PlayerActionCommand.Parry))
        {
            inputBuffer.ConsumeParry();
            return;
        }
        if (CurrentState == null ||inputBuffer == null ||!inputBuffer.TryPeek(out BufferedInput input))
        {
            return;
        }

        PlayerState handlingState = CurrentState;
        if (handlingState.TryHandleCommand(input.Command) &&handlingState.ShouldConsumeHandledCommand(input.Command))
        {
            inputBuffer.TryConsumeNext(out _);
        }
    }

    public void ChangeState(PlayerState nextState)
    {
        if (locomotionOnly && nextState != IdleState && nextState != LocomotionState)
        {
            return;
        }
        if (nextState == null || nextState == CurrentState)
        {
            return;
        }

        if (ShouldInterruptCombatExitAnimation(nextState))
        {
            combatStanceAnimator?.InterruptExitAnimation();
        }

        CurrentState?.Exit();
        CurrentState = nextState;
        CurrentState.Enter();
    }

    private bool ShouldInterruptCombatExitAnimation(PlayerState nextState)
    {
        return nextState == AttackState ||
               nextState == DodgeState ||
               nextState == HitState || nextState == ParryState;
    }

    public void ReturnToControllableState()
    {
        ReturnToControllableState(lastMoveInput, lastHasMoveInput);
    }

    internal bool ReturnToControllableState(Vector2 moveInput, bool hasMoveInput)
    {
        Vector2 currentMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
        bool returnToLocomotion = hasMoveInput ||
                                  currentMoveInput.sqrMagnitude > 0.0001f;
        if (!returnToLocomotion)
        {
            ChangeState(IdleState);
            ActionAnimator?.PlayIdle();
            return true;
        }

        // Action recovery (including Dodge -> FastMovement) must land directly
        // in the locomotion loop. Cross-fade before setting IsMoving so the
        // Animator cannot briefly take the Idle -> Start transition first.
        ChangeState(LocomotionState);
        ActionAnimator?.PlayLocomotionLoop();
        playerMovement.PrepareLocomotionAnimation(currentMoveInput);
        return true;
    }

    internal bool EnterFastLocomotionLoop(Vector2 moveInput)
    {
        Vector2 currentMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
        if (currentMoveInput.sqrMagnitude <= 0.0001f)
        {
            return ReturnToControllableState(currentMoveInput, false);
        }

        // MoveSpeed must already be 2 when the Loop state is sampled for the
        // first time, so this intentionally happens before the cross-fade.
        playerMovement.BeginFastMovement();
        ChangeState(LocomotionState);
        ActionAnimator?.PlayLocomotionLoop();
        playerMovement.PrepareLocomotionAnimation(currentMoveInput);
        return true;
    }

    public void CompleteCurrentAction()
    {
        CurrentState?.TryCompleteAction();
    }

    internal bool BeginAttackAbility(AbilityScriptableObject ability, float blendDuration = -1f)
    {
        if (ability == null || combatAdapter == null)
        {
            return false;
        }

        if (ActionAnimator == null || !ActionAnimator.PlayAbility(ability, blendDuration)) return false;
        combatAdapter.BeginAbility(ability);
        combatStanceAnimator?.NotifyCombatActivity();
        return true;
    }

    public void NotifyCombatActivity() => combatStanceAnimator?.NotifyCombatActivity();

    public bool TryEnterParry()
    {
        return TryEnterParry(false);
    }

    internal bool TryRestartContextParry()
    {
        return TryEnterParry(true);
    }

    private bool TryEnterParry(bool restartCurrentContextAction)
    {
        if (locomotionOnly) return false;
        if (combatAdapter != null && combatAdapter.HasContextParryAttacks)
        {
            if (!IsGrounded || (CurrentState == ParryState && !restartCurrentContextAction)) return false;
            EnemyCombatAdapter enemy = combatAdapter.FindParryOpportunity();
            AbilityScriptableObject ability = enemy != null
                ? combatAdapter.SuccessfulParryAttack
                : combatAdapter.OrdinaryParryAttack;
            EnemyController controller = enemy != null
                ? enemy.GetComponentInParent<EnemyController>() : null;
            if (CurrentState == ParryState) ParryState.Exit();
            ParryState.ConfigureContextAction(ability, controller);
            if (CurrentState == ParryState) ParryState.Enter();
            else ChangeState(ParryState);
            if (enemy != null)
            {
                enemy.CancelActiveHitBoxes();
                GetComponent<PlayerParryFeedback>()?.PlayContextParry(enemy);
                bool hasHitStop = false;
                foreach (AbilityEvent entry in ability.events)
                    if (entry?.Obj is AbilityEventObj_GlobalHitStop stop &&
                        stop.IsActive && stop.DurationFrames > 0)
                    {
                        hasHitStop = true;
                        break;
                    }
                controller?.NotifyParried(combatAdapter.ParryStaggerDuration, hasHitStop);
            }
            return true;
        }
        if (combatAdapter == null || combatAdapter.ParryStartAbility == null ||
            combatAdapter.ParryStartAbility.Clip == null || !IsGrounded) return false;
        ChangeState(ParryState);
        return true;
    }

    private void OnEnable()
    {
        if (CurrentState != null && CurrentState == IdleState)
        {
            IdleState.Enter();
            ActionAnimator?.PlayIdle();
        }
    }

    private void OnDisable()
    {
        CurrentState?.Exit();
        inputBuffer?.Clear();
        if (IdleState != null) CurrentState = IdleState;
    }

    public void EnterHitState()
    {
        if (CurrentState == HitState) HitState.Enter();
        else ChangeState(HitState);
    }

    public void RecoverFromHit()
    {
        if (CurrentState == HitState)
        {
            ReturnToControllableState();
        }
    }

    internal void RaiseHitStateEntered()
    {
        HitStateEntered?.Invoke();
    }
}
