using UnityEngine;

/// <summary>
/// Drives the temporary post-attack combat stance without coupling locomotion
/// or the generic CombatEditor runtime to project-specific Animator states.
/// </summary>
public sealed class PlayerCombatStanceAnimator
{
    private readonly Animator animator;
    private readonly float timeout;
    private readonly float exitBlendDuration;
    private readonly int combatWeightHash;
    private readonly int exitStateHash;
    private readonly int exitStateShortHash;
    private readonly int exitLayer;
    private readonly bool hasCombatWeightParameter;

    private float lastCombatActivityTime;
    private float exitAnimationStartTime;
    private int exitAnimationStartFrame;
    private bool combatStanceActive;
    private bool exitAnimationPlaying;

    public PlayerCombatStanceAnimator(
        Animator animator,
        float timeout,
        string combatWeightParameter,
        string exitLayerName,
        string exitStateName,
        float exitBlendDuration)
    {
        this.animator = animator;
        this.timeout = Mathf.Max(0f, timeout);
        this.exitBlendDuration = Mathf.Max(0f, exitBlendDuration);

        if (animator == null)
        {
            return;
        }

        combatWeightHash = Animator.StringToHash(combatWeightParameter);
        hasCombatWeightParameter = HasFloatParameter(combatWeightHash);
        exitLayer = animator.GetLayerIndex(exitLayerName);
        exitStateShortHash = Animator.StringToHash(exitStateName);

        if (exitLayer >= 0)
        {
            exitStateHash = Animator.StringToHash($"{exitLayerName}.{exitStateName}");
            animator.SetLayerWeight(exitLayer, 0f);
        }
    }

    public void NotifyCombatActivity()
    {
        lastCombatActivityTime = Time.time;
        combatStanceActive = true;
        exitAnimationPlaying = false;

        if (animator == null)
        {
            return;
        }

        if (hasCombatWeightParameter)
        {
            animator.SetFloat(combatWeightHash, 1f);
        }

        if (exitLayer >= 0)
        {
            animator.SetLayerWeight(exitLayer, 0f);
        }
    }

    public void Tick(bool canLeaveCombatStance)
    {
        if (animator == null)
        {
            return;
        }

        if (combatStanceActive &&
            canLeaveCombatStance &&
            Time.time - lastCombatActivityTime >= timeout)
        {
            BeginExit();
        }

        UpdateExitAnimation();
    }

    private void BeginExit()
    {
        combatStanceActive = false;

        if (hasCombatWeightParameter)
        {
            animator.SetFloat(combatWeightHash, 0f);
        }

        if (exitLayer < 0 || !animator.HasState(exitLayer, exitStateHash))
        {
            return;
        }

        animator.SetLayerWeight(exitLayer, exitBlendDuration > 0f ? 0f : 1f);
        if (!PlayerAnimatorTransition.TryCrossFade(
                animator,
                exitLayer,
                exitStateHash,
                exitBlendDuration))
        {
            return;
        }

        exitAnimationStartTime = Time.time;
        exitAnimationStartFrame = Time.frameCount;
        exitAnimationPlaying = true;
    }

    private void UpdateExitAnimation()
    {
        if (!exitAnimationPlaying || exitLayer < 0)
        {
            return;
        }

        if (exitBlendDuration > 0f)
        {
            float layerWeight = Mathf.Clamp01(
                (Time.time - exitAnimationStartTime) / exitBlendDuration);
            animator.SetLayerWeight(exitLayer, layerWeight);
        }

        // CrossFade takes effect during Animator evaluation later in the frame.
        // Waiting one frame prevents reading the zero-weight state's old loop time.
        if (Time.frameCount == exitAnimationStartFrame)
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.IsInTransition(exitLayer)
            ? animator.GetNextAnimatorStateInfo(exitLayer)
            : animator.GetCurrentAnimatorStateInfo(exitLayer);

        if (stateInfo.shortNameHash != exitStateShortHash ||
            stateInfo.normalizedTime < 1f)
        {
            return;
        }

        animator.SetLayerWeight(exitLayer, 0f);
        exitAnimationPlaying = false;
    }

    private bool HasFloatParameter(int parameterHash)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == parameterHash &&
                parameters[i].type == AnimatorControllerParameterType.Float)
            {
                return true;
            }
        }

        return false;
    }
}
