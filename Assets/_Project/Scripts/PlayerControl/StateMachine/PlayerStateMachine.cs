using System;
using CombatEditor;
using UnityEngine;

public static class PlayerAnimatorTransition
{
    public static bool TryCrossFade(
        Animator animator,
        int layer,
        string relativeStatePath,
        float duration,
        out int stateHash,
        float normalizedTime = 0f,
        UnityEngine.Object logContext = null)
    {
        stateHash = 0;
        if (!ValidateLayer(animator, layer, logContext))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(relativeStatePath))
        {
            Debug.LogError("Animator state path cannot be empty.", logContext ?? animator);
            return false;
        }

        string statePath = $"{animator.GetLayerName(layer)}.{relativeStatePath}";
        stateHash = Animator.StringToHash(statePath);
        return TryCrossFade(
            animator,
            layer,
            stateHash,
            duration,
            normalizedTime,
            logContext,
            statePath);
    }

    public static bool TryCrossFade(
        Animator animator,
        int layer,
        int stateHash,
        float duration,
        float normalizedTime = 0f,
        UnityEngine.Object logContext = null,
        string stateLabel = null)
    {
        if (!ValidateLayer(animator, layer, logContext))
        {
            return false;
        }

        if (!animator.HasState(layer, stateHash))
        {
            string label = string.IsNullOrWhiteSpace(stateLabel)
                ? stateHash.ToString()
                : stateLabel;
            Debug.LogError($"Animator does not contain state '{label}'.", logContext ?? animator);
            return false;
        }

        animator.CrossFadeInFixedTime(
            stateHash,
            Mathf.Max(0f, duration),
            layer,
            Mathf.Max(0f, normalizedTime));
        return true;
    }

    private static bool ValidateLayer(
        Animator animator,
        int layer,
        UnityEngine.Object logContext)
    {
        if (animator != null && layer >= 0 && layer < animator.layerCount)
        {
            return true;
        }

        Debug.LogError($"Animator layer {layer} does not exist.", logContext ?? animator);
        return false;
    }
}

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
    public event Action HitStateEntered;

    private static readonly int DodgeXHash = Animator.StringToHash("DodgeX");
    private static readonly int DodgeYHash = Animator.StringToHash("DodgeY");
    private int combatWeightHash;
    private Vector2 lastMoveInput;
    private bool lastHasMoveInput;
    private PlayerCombatStanceAnimator combatStanceAnimator;
    private bool isDodgeAnimationPlaying;
    private bool hasEnteredDodgeAnimation;
    private int dodgeAnimationStateHash;
    private int dodgeAnimationRequestFrame;

    internal Vector2 LatestMoveInput => lastMoveInput;
    internal bool HasLatestMoveInput => lastHasMoveInput;

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

        combatWeightHash = Animator.StringToHash(combatWeightParameter);

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
        if (!IsGrounded)
        {
            ChangeState(AirborneState);
            return;
        }

        ChangeState(lastHasMoveInput ? LocomotionState : IdleState);
    }

    public void CompleteAttack()
    {
        if (CurrentState == AttackState)
        {
            ReturnToControllableState();
            CrossFadeToIdle();
        }
    }

    public void CompleteCurrentAction()
    {
        if (CurrentState == AttackState)
        {
            CompleteAttack();
        }
        else if (CurrentState == DodgeState)
        {
            CompleteDodge(lastMoveInput, lastHasMoveInput);
        }
    }

    public void CompleteDodge(Vector2 moveInput, bool hasMoveInput)
    {
        if (CurrentState != DodgeState)
        {
            return;
        }

        if (!IsGrounded)
        {
            ChangeState(AirborneState);
            return;
        }

        Vector2 currentMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
        bool returnToLocomotion = hasMoveInput || currentMoveInput.sqrMagnitude > 0.0001f;
        if (!returnToLocomotion)
        {
            ChangeState(IdleState);
            CrossFadeToIdle();
            return;
        }

        string locomotionLoopState = IsCombatAnimationActive()
            ? combatLocomotionLoopStateName
            : normalLocomotionLoopStateName;
        ChangeState(LocomotionState);
        playerMovement.PrepareLocomotionAnimation(currentMoveInput);
        TryCrossFadeAnimation(locomotionLoopState, out _);
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

    internal bool PlayDodgeAnimation(Vector2 localDirection)
    {
        if (!ValidateAnimatorLayer())
        {
            return false;
        }

        Vector2 direction = PlayerMovement.QuantizeEightWayDirection(localDirection);
        animator.SetFloat(DodgeXHash, direction.x);
        animator.SetFloat(DodgeYHash, direction.y);

        bool useCombatDodge = IsCombatAnimationActive();
        string stateName = useCombatDodge ? dodgeCombatStateName : dodgeNormalStateName;
        if (!TryCrossFadeAnimation(stateName, out int stateHash))
        {
            return false;
        }

        dodgeAnimationStateHash = stateHash;
        dodgeAnimationRequestFrame = Time.frameCount;
        hasEnteredDodgeAnimation = false;
        isDodgeAnimationPlaying = true;
        return true;
    }

    internal void BeginDodgeAbility()
    {
        combatAdapter?.BeginDodgeAbility();
    }

    internal void UpdateDodgeAbilityWindows()
    {
        if (TryGetDodgeAnimationState(out AnimatorStateInfo stateInfo))
        {
            combatAdapter?.UpdateDodgeAbility(stateInfo.normalizedTime);
        }
    }

    internal void EndDodgeAbility()
    {
        combatAdapter?.EndDodgeAbility();
    }

    internal bool CanDodgeInterruptWithMovement()
    {
        return combatAdapter != null && combatAdapter.CanInterruptWithMovement();
    }

    internal bool IsDodgeAnimationComplete()
    {
        if (!isDodgeAnimationPlaying)
        {
            return true;
        }

        if (Time.frameCount <= dodgeAnimationRequestFrame)
        {
            return false;
        }

        if (TryGetDodgeAnimationState(out AnimatorStateInfo stateInfo))
        {
            return stateInfo.normalizedTime >= 1f;
        }

        return hasEnteredDodgeAnimation;
    }

    private bool TryGetDodgeAnimationState(out AnimatorStateInfo stateInfo)
    {
        if (animator.IsInTransition(abilityAnimatorLayer))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(abilityAnimatorLayer);
            if (nextState.fullPathHash == dodgeAnimationStateHash)
            {
                hasEnteredDodgeAnimation = true;
                stateInfo = nextState;
                return true;
            }
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(abilityAnimatorLayer);
        if (currentState.fullPathHash == dodgeAnimationStateHash)
        {
            hasEnteredDodgeAnimation = true;
            stateInfo = currentState;
            return true;
        }

        stateInfo = default;
        return false;
    }

    internal void StopTrackingDodgeAnimation()
    {
        isDodgeAnimationPlaying = false;
        hasEnteredDodgeAnimation = false;
    }

    private void PlayAbility(AbilityScriptableObject ability)
    {
        combatStanceAnimator?.NotifyCombatActivity();

        if (ability == null || ability.Clip == null || animator == null)
        {
            return;
        }

        TryCrossFadeAnimation(ability.Clip.name, out _);
    }

    private void CrossFadeToIdle()
    {
        TryCrossFadeAnimation(idleStateName, out _);
    }

    private bool IsCombatAnimationActive()
    {
        return animator != null && animator.GetFloat(combatWeightHash) >= 0.5f;
    }

    private bool TryCrossFadeAnimation(
        string relativeStatePath,
        out int stateHash,
        float normalizedTime = 0f)
    {
        return PlayerAnimatorTransition.TryCrossFade(
            animator,
            abilityAnimatorLayer,
            relativeStatePath,
            animationBlendDuration,
            out stateHash,
            normalizedTime,
            this);
    }

    private bool ValidateAnimatorLayer()
    {
        if (animator != null &&
            abilityAnimatorLayer >= 0 &&
            abilityAnimatorLayer < animator.layerCount)
        {
            return true;
        }

        Debug.LogError($"Animator layer {abilityAnimatorLayer} does not exist.", this);
        return false;
    }

    internal void RaiseHitStateEntered()
    {
        HitStateEntered?.Invoke();
    }
}
