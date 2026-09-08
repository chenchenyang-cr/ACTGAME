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
    private readonly int dodgeToFastRunNormalHash;
    private readonly int dodgeToFastRunCombatHash;
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
        if (animator != null && profile.AnimatorLayer < animator.layerCount && profile.AnimatorLayer >= 0)
        {
            string layerName = animator.GetLayerName(profile.AnimatorLayer);
            dodgeToFastRunNormalHash = Animator.StringToHash($"{layerName}.{profile.DodgeToFastRunNormalStateName}");
            dodgeToFastRunCombatHash = Animator.StringToHash($"{layerName}.{profile.DodgeToFastRunCombatStateName}");
        }
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
        PlayLocomotionLoop(profile.LocomotionReturnBlendDuration);
    }

    private void PlayLocomotionLoop(float duration)
    {
        string stateName = IsCombatAnimationActive()
            ? profile.CombatLocomotionLoopStateName
            : profile.NormalLocomotionLoopStateName;
        TryCrossFade(stateName, duration, out _);
    }

    public void UpdateLocomotionRecovery()
    {
        if (!ValidateAnimatorLayer() || animator.IsInTransition(profile.AnimatorLayer))
        {
            return;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(profile.AnimatorLayer);
        if ((current.fullPathHash == dodgeToFastRunNormalHash ||
             current.fullPathHash == dodgeToFastRunCombatHash) && current.normalizedTime >= 0.9f)
        {
            // A fixed-time cross-fade can advance the incoming non-looping clip
            // past its exit time. Check the level, not only the Animator's exit
            // crossing, so late entries and long frames cannot strand recovery.
            PlayLocomotionLoop(profile.DodgeToFastRunBlendDuration);
        }
    }

    public bool TryPlayDodgeToFastRun()
    {
        if (!isDodgeAnimationPlaying || !ValidateAnimatorLayer())
        {
            return false;
        }

        string stateName = IsCombatAnimationActive()
            ? profile.DodgeToFastRunCombatStateName
            : profile.DodgeToFastRunNormalStateName;
        int stateHash = Animator.StringToHash($"{animator.GetLayerName(profile.AnimatorLayer)}.{stateName}");
        // Controllers without authored recovery states keep their direct return.
        if (!animator.HasState(profile.AnimatorLayer, stateHash))
        {
            return false;
        }

        // These clips include the initial dodge. Continue at the elapsed clip
        // time instead of replaying it; late inputs still get the recovery tail.
        float fixedTimeOffset = TryGetDodgeAnimationState(out AnimatorStateInfo dodgeState)
            ? Mathf.Clamp(dodgeState.normalizedTime * dodgeState.length,
                0f, profile.DodgeToFastRunLatestStartTime)
            : profile.DodgeToFastRunLatestStartTime;
        return PlayerAnimatorTransition.TryCrossFade(
            animator, profile.AnimatorLayer, stateHash,
            profile.DodgeToFastRunBlendDuration, fixedTimeOffset, logContext);
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
