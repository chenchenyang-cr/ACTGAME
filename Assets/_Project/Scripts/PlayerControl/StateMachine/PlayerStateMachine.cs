using System;
using CombatEditor;
using UnityEngine;

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

    public PlayerState CurrentState { get; private set; }
    public IdleState IdleState { get; private set; }
    public LocomotionState LocomotionState { get; private set; }
    public AttackState AttackState { get; private set; }
    public DodgeState DodgeState { get; private set; }
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
               nextState == HitState;
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

    internal bool BeginAttackAbility(AbilityScriptableObject ability)
    {
        if (ability == null || combatAdapter == null)
        {
            return false;
        }

        combatAdapter.BeginAbility(ability);
        combatStanceAnimator?.NotifyCombatActivity();
        ActionAnimator?.PlayAbility(ability);
        return true;
    }

    public void EnterHitState()
    {
        ChangeState(HitState);
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
