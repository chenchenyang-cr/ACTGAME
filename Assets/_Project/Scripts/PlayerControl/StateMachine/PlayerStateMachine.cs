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
    [Min(0)]
    private int abilityAnimatorLayer;
    [Header("Animation Transitions")]
    [SerializeField]
    [Min(0f)]
    private float animationBlendDuration = 0.12f;
    [SerializeField, Min(0f)]
    private float locomotionReturnBlendDuration = 0.2f;
    [SerializeField, Min(0f)]
    private float idleReturnBlendDuration = 0.15f;
    [SerializeField]
    private string idleStateName = "Idle";
    [SerializeField]
    private string normalLocomotionLoopStateName = "NormalLocomotion.Loop";
    [SerializeField]
    private string combatLocomotionLoopStateName = "CombatLocomotion.Loop";
    [Header("Dodge Animation")]
    [SerializeField]
    private string dodgeNormalStateName = "DodgeNormal";
    [SerializeField]
    private string dodgeCombatStateName = "DodgeCombat";
    [SerializeField, Min(0f)]
    private float dodgeRootMotionMultiplier = 1.2f;
    [Header("Combat Stance")]
    [SerializeField]
    [Min(0f)]
    private float combatStanceTimeout = 4f;
    [SerializeField]
    private string combatWeightParameter = "CombatWeight";
    [SerializeField]
    private string combatExitLayerName = "Combat Upper Body";
    [SerializeField]
    private string combatExitStateName = "Idle_Combat_To_Idle";
    [SerializeField]
    [Min(0f)]
    private float combatExitBlendDuration = 0.1f;

    public PlayerState CurrentState { get; private set; }
    public IdleState IdleState { get; private set; }
    public LocomotionState LocomotionState { get; private set; }
    public AirborneState AirborneState { get; private set; }
    public AttackState AttackState { get; private set; }
    public DodgeState DodgeState { get; private set; }
    public HitState HitState { get; private set; }

    public PlayerMovement Movement => playerMovement;
    public PlayerCombatAdapter Combat => combatAdapter;
    public PlayerActionAnimator ActionAnimator { get; private set; }
    public bool IsGrounded => playerMovement != null
        ? playerMovement.IsGrounded
        : characterController != null && characterController.isGrounded;

    public event Action JumpRequested;
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

        ActionAnimator = new PlayerActionAnimator(
            animator,
            abilityAnimatorLayer,
            animationBlendDuration,
            locomotionReturnBlendDuration,
            idleReturnBlendDuration,
            idleStateName,
            normalLocomotionLoopStateName,
            combatLocomotionLoopStateName,
            dodgeNormalStateName,
            dodgeCombatStateName,
            combatWeightParameter,
            this);

        combatStanceAnimator = new PlayerCombatStanceAnimator(
            animator,
            combatStanceTimeout,
            combatWeightParameter,
            combatExitLayerName,
            combatExitStateName,
            combatExitBlendDuration);

        IdleState = new IdleState(this);
        LocomotionState = new LocomotionState(this);
        AirborneState = new AirborneState(this);
        AttackState = new AttackState(this);
        DodgeState = new DodgeState(this);
        HitState = new HitState(this);

        ChangeState(IdleState);
    }

    private void OnEnable()
    {
        if (combatAdapter != null)
        {
            combatAdapter.AbilityRequested += OnAbilityRequested;
        }
    }

    private void OnDisable()
    {
        if (combatAdapter != null)
        {
            combatAdapter.AbilityRequested -= OnAbilityRequested;
        }
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

        CurrentState?.Exit();
        CurrentState = nextState;
        CurrentState.Enter();
    }

    public void ReturnToControllableState()
    {
        ReturnToControllableState(lastMoveInput, lastHasMoveInput);
    }

    internal bool ReturnToControllableState(Vector2 moveInput, bool hasMoveInput)
    {
        if (!IsGrounded)
        {
            ChangeState(AirborneState);
            return true;
        }

        Vector2 currentMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
        bool returnToLocomotion = hasMoveInput ||
                                  currentMoveInput.sqrMagnitude > 0.0001f;
        if (!returnToLocomotion)
        {
            ChangeState(IdleState);
            ActionAnimator?.PlayIdle();
            return true;
        }

        playerMovement.PrepareLocomotionAnimation(currentMoveInput);
        ChangeState(LocomotionState);
        ActionAnimator?.PlayLocomotion();
        return true;
    }

    public void CompleteCurrentAction()
    {
        CurrentState?.TryCompleteAction();
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

    internal void RaiseJumpRequested()
    {
        JumpRequested?.Invoke();
    }

    private void OnAbilityRequested(AbilityScriptableObject ability)
    {
        combatStanceAnimator?.NotifyCombatActivity();
        ActionAnimator?.PlayAbility(ability);
    }

    internal void RaiseHitStateEntered()
    {
        HitStateEntered?.Invoke();
    }
}
