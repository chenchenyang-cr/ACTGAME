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
        Object logContext = null)
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
        Object logContext = null,
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
        Object logContext)
    {
        if (animator != null && layer >= 0 && layer < animator.layerCount)
        {
            return true;
        }

        Debug.LogError($"Animator layer {layer} does not exist.", logContext ?? animator);
        return false;
    }
}
