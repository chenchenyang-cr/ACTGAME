using CombatEditor;
using UnityEngine;

/// <summary>
/// Owns action-animation playback and tracking so gameplay states do not need
/// to know Animator layers, hashes, state paths, or transition timing details.
/// </summary>
public sealed class PlayerActionAnimator
{
    private static readonly int DodgeXHash = Animator.StringToHash("DodgeX");
    private static readonly int DodgeYHash = Animator.StringToHash("DodgeY");

    private readonly Animator animator;
    private readonly int animatorLayer;
    private readonly float actionBlendDuration;
    private readonly float locomotionReturnBlendDuration;
    private readonly float idleReturnBlendDuration;
    private readonly string idleStateName;
    private readonly string normalLocomotionLoopStateName;
    private readonly string combatLocomotionLoopStateName;
    private readonly string dodgeNormalStateName;
    private readonly string dodgeCombatStateName;
    private readonly int combatWeightHash;
    private readonly Object logContext;

    private bool isDodgeAnimationPlaying;
    private bool hasEnteredDodgeAnimation;
    private int dodgeAnimationStateHash;
    private int dodgeAnimationRequestFrame;

    public PlayerActionAnimator(
        Animator animator,
        int animatorLayer,
        float actionBlendDuration,
        float locomotionReturnBlendDuration,
        float idleReturnBlendDuration,
        string idleStateName,
        string normalLocomotionLoopStateName,
        string combatLocomotionLoopStateName,
        string dodgeNormalStateName,
        string dodgeCombatStateName,
        string combatWeightParameter,
        Object logContext)
    {
        this.animator = animator;
        this.animatorLayer = animatorLayer;
        this.actionBlendDuration = actionBlendDuration;
        this.locomotionReturnBlendDuration = locomotionReturnBlendDuration;
        this.idleReturnBlendDuration = idleReturnBlendDuration;
        this.idleStateName = idleStateName;
        this.normalLocomotionLoopStateName = normalLocomotionLoopStateName;
        this.combatLocomotionLoopStateName = combatLocomotionLoopStateName;
        this.dodgeNormalStateName = dodgeNormalStateName;
        this.dodgeCombatStateName = dodgeCombatStateName;
        combatWeightHash = Animator.StringToHash(combatWeightParameter);
        this.logContext = logContext;
    }

    public void PlayAbility(AbilityScriptableObject ability)
    {
        if (ability == null || ability.Clip == null || animator == null)
        {
            return;
        }

        TryCrossFade(ability.Clip.name, out _);
    }

    public bool PlayDodge(Vector2 localDirection)
    {
        if (!ValidateAnimatorLayer())
        {
            return false;
        }

        Vector2 direction = PlayerMovement.QuantizeEightWayDirection(localDirection);
        animator.SetFloat(DodgeXHash, direction.x);
        animator.SetFloat(DodgeYHash, direction.y);

        string stateName = IsCombatAnimationActive()
            ? dodgeCombatStateName
            : dodgeNormalStateName;
        if (!TryCrossFade(stateName, out int stateHash))
        {
            return false;
        }

        dodgeAnimationStateHash = stateHash;
        dodgeAnimationRequestFrame = Time.frameCount;
        hasEnteredDodgeAnimation = false;
        isDodgeAnimationPlaying = true;
        return true;
    }

    public bool TryGetDodgeNormalizedTime(out float normalizedTime)
    {
        if (TryGetDodgeAnimationState(out AnimatorStateInfo stateInfo))
        {
            normalizedTime = stateInfo.normalizedTime;
            return true;
        }

        normalizedTime = 0f;
        return false;
    }

    public bool IsDodgeComplete()
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

    public void StopTrackingDodge()
    {
        isDodgeAnimationPlaying = false;
        hasEnteredDodgeAnimation = false;
    }

    public void PlayIdle()
    {
        TryCrossFade(idleStateName, idleReturnBlendDuration, out _);
    }

    public void PlayLocomotionLoop()
    {
        string stateName = IsCombatAnimationActive()
            ? combatLocomotionLoopStateName
            : normalLocomotionLoopStateName;
        TryCrossFade(stateName, locomotionReturnBlendDuration, out _);
    }

    private bool TryGetDodgeAnimationState(out AnimatorStateInfo stateInfo)
    {
        if (animator == null || !ValidateAnimatorLayer())
        {
            stateInfo = default;
            return false;
        }

        if (animator.IsInTransition(animatorLayer))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(animatorLayer);
            if (nextState.fullPathHash == dodgeAnimationStateHash)
            {
                hasEnteredDodgeAnimation = true;
                stateInfo = nextState;
                return true;
            }
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        if (currentState.fullPathHash == dodgeAnimationStateHash)
        {
            hasEnteredDodgeAnimation = true;
            stateInfo = currentState;
            return true;
        }

        stateInfo = default;
        return false;
    }

    private bool IsCombatAnimationActive()
    {
        return animator != null && animator.GetFloat(combatWeightHash) >= 0.5f;
    }

    private bool TryCrossFade(
        string relativeStatePath,
        out int stateHash,
        float normalizedTime = 0f)
    {
        return PlayerAnimatorTransition.TryCrossFade(
            animator,
            animatorLayer,
            relativeStatePath,
            actionBlendDuration,
            out stateHash,
            normalizedTime,
            logContext);
    }

    private bool TryCrossFade(
        string relativeStatePath,
        float duration,
        out int stateHash,
        float normalizedTime = 0f)
    {
        return PlayerAnimatorTransition.TryCrossFade(
            animator,
            animatorLayer,
            relativeStatePath,
            duration,
            out stateHash,
            normalizedTime,
            logContext);
    }

    private bool ValidateAnimatorLayer()
    {
        if (animator != null &&
            animatorLayer >= 0 &&
            animatorLayer < animator.layerCount)
        {
            return true;
        }

        Debug.LogError($"Animator layer {animatorLayer} does not exist.", logContext);
        return false;
    }
}
