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
    public bool IsGrounded => playerMovement != null
        ? playerMovement.IsGrounded
        : characterController != null && characterController.isGrounded;

    public event Action JumpRequested;
    public event Action DodgeRequested;
    public event Action<int> LightAttackRequested;
    public event Action<AbilityScriptableObject> AbilityRequested;
    public event Action HitStateEntered;

    private Vector2 lastMoveInput;
    private bool lastHasMoveInput;
    private PlayerCombatStanceAnimator combatStanceAnimator;

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
            combatAdapter.AbilityRequested += PlayAbility;
        }
    }

    private void OnDisable()
    {
        if (combatAdapter != null)
        {
            combatAdapter.AbilityRequested -= PlayAbility;
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
        bool canLeaveCombatStance = CurrentState == IdleState ||
                                    CurrentState == LocomotionState;
        combatStanceAnimator?.Tick(canLeaveCombatStance);
    }

    private void ProcessBufferedCommand(PlayerInputBuffer inputBuffer)
    {
        if (CurrentState == null ||
            inputBuffer == null ||
            !inputBuffer.TryPeek(out BufferedInput input))
        {
            return;
        }

        PlayerState handlingState = CurrentState;
        if (handlingState.TryHandleCommand(input.Command) &&
            handlingState.ShouldConsumeHandledCommand(input.Command))
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
        if (!IsGrounded)
        {
            ChangeState(AirborneState);
            return;
        }

        ChangeState(lastHasMoveInput ? LocomotionState : IdleState);
    }

    public void OpenComboWindow()
    {
        if (CurrentState == AttackState)
        {
            AttackState.OpenComboWindow();
        }
    }

    public void CloseComboWindow()
    {
        if (CurrentState == AttackState)
        {
            AttackState.CloseComboWindow();
        }
    }

    public void CompleteAttack()
    {
        if (CurrentState == AttackState)
        {
            ReturnToControllableState();
            CrossFadeToIdle();
        }
    }

    public void CompleteDodge()
    {
        if (CurrentState == DodgeState)
        {
            ReturnToControllableState();
        }
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

    internal void RaiseDodgeRequested()
    {
        DodgeRequested?.Invoke();
    }

    internal void RaiseLightAttackRequested(int comboIndex)
    {
        LightAttackRequested?.Invoke(comboIndex);
    }

    internal void RaiseAbilityRequested(AbilityScriptableObject ability)
    {
        AbilityRequested?.Invoke(ability);
    }

    private void PlayAbility(AbilityScriptableObject ability)
    {
        combatStanceAnimator?.NotifyCombatActivity();

        if (ability == null || ability.Clip == null || animator == null)
        {
            return;
        }

        if (abilityAnimatorLayer < 0 || abilityAnimatorLayer >= animator.layerCount)
        {
            Debug.LogError($"Animator layer {abilityAnimatorLayer} does not exist.", this);
            return;
        }

        string statePath = $"{animator.GetLayerName(abilityAnimatorLayer)}.{ability.Clip.name}";
        int stateHash = Animator.StringToHash(statePath);
        if (!animator.HasState(abilityAnimatorLayer, stateHash))
        {
            Debug.LogError($"Animator does not contain ability state '{statePath}'.", this);
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, 0.05f, abilityAnimatorLayer, 0f);
    }

    private void CrossFadeToIdle()
    {
        if (animator == null ||
            abilityAnimatorLayer < 0 ||
            abilityAnimatorLayer >= animator.layerCount)
        {
            return;
        }

        string idleStatePath = $"{animator.GetLayerName(abilityAnimatorLayer)}.Idle";
        int idleStateHash = Animator.StringToHash(idleStatePath);
        if (!animator.HasState(abilityAnimatorLayer, idleStateHash))
        {
            Debug.LogError($"Animator does not contain idle state '{idleStatePath}'.", this);
            return;
        }

        animator.CrossFadeInFixedTime(idleStateHash, 0.1f, abilityAnimatorLayer, 0f);
    }

    internal void RaiseHitStateEntered()
    {
        HitStateEntered?.Invoke();
    }
}
