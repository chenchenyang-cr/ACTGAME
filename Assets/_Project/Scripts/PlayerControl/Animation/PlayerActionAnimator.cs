using CombatEditor;
using UnityEngine;

/// <summary>
/// Owns action-animation playback and tracking so gameplay states do not need
/// to know Animator layers, hashes, state paths, or transition timing details.
/// </summary>
public sealed class PlayerActionAnimator
{
    private readonly Animator animator;
    private readonly PlayerAnimationProfile profile;
    private readonly int dodgeXHash;
    private readonly int dodgeYHash;
    private readonly int combatWeightHash;
    private readonly Object logContext;

    private bool isDodgeAnimationPlaying;
    private bool hasEnteredDodgeAnimation;
    private int dodgeAnimationStateHash;
    private int dodgeAnimationRequestFrame;

    public PlayerActionAnimator(
        Animator animator,
        PlayerAnimationProfile profile,
        Object logContext)
    {
        this.animator = animator;
        this.profile = profile;
        this.logContext = logContext;

        dodgeXHash = Animator.StringToHash(profile.DodgeXParameter);
        dodgeYHash = Animator.StringToHash(profile.DodgeYParameter);
        combatWeightHash = Animator.StringToHash(profile.CombatWeightParameter);
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
        animator.SetFloat(dodgeXHash, direction.x);
        animator.SetFloat(dodgeYHash, direction.y);

        string stateName = IsCombatAnimationActive()
            ? profile.DodgeCombatStateName
            : profile.DodgeNormalStateName;
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
        TryCrossFade(profile.IdleStateName, profile.IdleReturnBlendDuration, out _);
    }

    public void PlayLocomotionLoop()
    {
        string stateName = IsCombatAnimationActive()
            ? profile.CombatLocomotionLoopStateName
            : profile.NormalLocomotionLoopStateName;
        TryCrossFade(stateName, profile.LocomotionReturnBlendDuration, out _);
    }

    private bool TryGetDodgeAnimationState(out AnimatorStateInfo stateInfo)
    {
        if (animator == null || !ValidateAnimatorLayer())
        {
            stateInfo = default;
            return false;
        }

        if (animator.IsInTransition(profile.AnimatorLayer))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(profile.AnimatorLayer);
            if (nextState.fullPathHash == dodgeAnimationStateHash)
            {
                hasEnteredDodgeAnimation = true;
                stateInfo = nextState;
                return true;
            }
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(profile.AnimatorLayer);
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
            profile.AnimatorLayer,
            relativeStatePath,
            profile.ActionBlendDuration,
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
            profile.AnimatorLayer,
            relativeStatePath,
            duration,
            out stateHash,
            normalizedTime,
            logContext);
    }

    private bool ValidateAnimatorLayer()
    {
        if (animator != null &&
            profile.AnimatorLayer >= 0 &&
            profile.AnimatorLayer < animator.layerCount)
        {
            return true;
        }

        Debug.LogError($"Animator layer {profile.AnimatorLayer} does not exist.", logContext);
        return false;
    }
}
