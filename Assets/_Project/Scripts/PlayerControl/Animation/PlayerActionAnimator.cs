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
    private bool dodgeContinuesToFastRun;
    private bool dodgeUsesCombatStance;

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

    private int trackedActionHash;
    private int trackedActionRequestFrame;
    private bool trackedActionEntered;
    private bool trackedActionPlaying;

    public bool PlayTrackedAction(string stateName, float blendDuration)
    {
        trackedActionEntered = false;
        trackedActionRequestFrame = Time.frameCount;
        trackedActionPlaying = TryCrossFade(stateName, blendDuration, out trackedActionHash);
        return trackedActionPlaying;
    }

    public bool TryGetTrackedActionTime(out float normalizedTime)
    {
        normalizedTime = 0f;
        if (!trackedActionPlaying || !ValidateAnimatorLayer()) return false;
        // On a same-state restart, Animator may still report the previous attempt's time
        // until it evaluates the new transition. Never reuse that old parry window.
        if (Time.frameCount <= trackedActionRequestFrame) return false;
        AnimatorStateInfo state;
        if (animator.IsInTransition(profile.AnimatorLayer))
        {
            state = animator.GetNextAnimatorStateInfo(profile.AnimatorLayer);
            if (state.fullPathHash == trackedActionHash)
            {
                trackedActionEntered = true;
                normalizedTime = state.normalizedTime;
                return true;
            }
        }
        state = animator.GetCurrentAnimatorStateInfo(profile.AnimatorLayer);
        if (state.fullPathHash != trackedActionHash) return false;
        trackedActionEntered = true;
        normalizedTime = state.normalizedTime;
        return true;
    }

    public bool IsTrackedActionComplete()
    {
        if (!trackedActionPlaying) return true;
        if (Time.frameCount <= trackedActionRequestFrame) return false;
        if (TryGetTrackedActionTime(out float time)) return time >= 1f;
        // Recover if another animator transition steals the action.
        return trackedActionEntered || Time.frameCount > trackedActionRequestFrame + 2;
    }

    public void StopTrackingAction() => trackedActionPlaying = false;

    public void PlayAbility(AbilityScriptableObject ability)
    {
        if (ability == null || ability.Clip == null || animator == null)
        {
            return;
        }

        TryCrossFade(ability.Clip.name, out _);
    }

    public bool PlayDodge(Vector2 localDirection, bool continueToFastRun)
    {
        if (!ValidateAnimatorLayer())
        {
            return false;
        }

        Vector2 direction = PlayerMovement.QuantizeEightWayDirection(localDirection);
        animator.SetFloat(dodgeXHash, direction.x);
        animator.SetFloat(dodgeYHash, direction.y);

        dodgeUsesCombatStance = IsCombatAnimationActive();
        string stateName = GetDodgeStateName(continueToFastRun);
        int requestedHash = Animator.StringToHash($"{animator.GetLayerName(profile.AnimatorLayer)}.{stateName}");
        if (continueToFastRun && !animator.HasState(profile.AnimatorLayer, requestedHash))
        {
            continueToFastRun = false;
            stateName = GetDodgeStateName(false);
        }
        dodgeContinuesToFastRun = continueToFastRun;
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

    private string GetDodgeStateName(bool continueToFastRun)
    {
        if (continueToFastRun)
            return dodgeUsesCombatStance ? profile.DodgeToFastRunCombatStateName : profile.DodgeToFastRunNormalStateName;
        return dodgeUsesCombatStance ? profile.DodgeCombatStateName : profile.DodgeNormalStateName;
    }

    public bool TryGetDodgeElapsedTime(out float seconds)
    {
        if (TryGetDodgeAnimationState(out AnimatorStateInfo state))
        {
            seconds = state.normalizedTime * state.length;
            return true;
        }
        seconds = 0f;
        return false;
    }

    public void SetDodgeContinuation(bool continueToFastRun)
    {
        if (continueToFastRun == dodgeContinuesToFastRun ||
            !TryGetDodgeElapsedTime(out float seconds)) return;
        int hash = Animator.StringToHash($"{animator.GetLayerName(profile.AnimatorLayer)}.{GetDodgeStateName(continueToFastRun)}");
        if (!animator.HasState(profile.AnimatorLayer, hash)) return;
        // Both authored clips contain the dodge prefix. Change the selected
        // ending at the same source time, without blending or replaying that prefix.
        animator.PlayInFixedTime(hash, profile.AnimatorLayer, seconds);
        dodgeAnimationStateHash = hash;
        dodgeAnimationRequestFrame = Time.frameCount;
        hasEnteredDodgeAnimation = false;
        dodgeContinuesToFastRun = continueToFastRun;
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
        PlayLocomotionLoop(profile.LocomotionReturnBlendDuration);
    }

    private void PlayLocomotionLoop(float duration)
    {
        string stateName = IsCombatAnimationActive()
            ? profile.CombatLocomotionLoopStateName
            : profile.NormalLocomotionLoopStateName;
        TryCrossFade(stateName, duration, out _);
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
