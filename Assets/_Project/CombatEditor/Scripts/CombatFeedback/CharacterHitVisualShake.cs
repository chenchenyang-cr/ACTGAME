using UnityEngine;

namespace CombatEditor
{
    [DisallowMultipleComponent]
    public sealed class CharacterHitVisualShake : MonoBehaviour
    {
        private Transform shakePivot;
        private Vector3 pivotBaseLocalPosition;
        private AnimationCurve decayCurve;
        private float duration;
        private float frequency;
        private float amplitude;
        private float startTime;
        private Vector3 impulse;
        private Vector3 restartCorrection;
        private bool playing;

        public void Initialize(Transform visualRoot)
        {
            if (visualRoot == null || visualRoot == shakePivot)
                return;

            if (visualRoot.parent != null &&
                visualRoot.parent.name == "HitShakePivot")
            {
                shakePivot = visualRoot.parent;
            }
            else
            {
                Transform originalParent = visualRoot.parent;
                int siblingIndex = visualRoot.GetSiblingIndex();
                var pivotObject = new GameObject("HitShakePivot");
                shakePivot = pivotObject.transform;
                shakePivot.SetParent(originalParent, false);
                shakePivot.SetSiblingIndex(siblingIndex);
                visualRoot.SetParent(shakePivot, true);
            }

            pivotBaseLocalPosition = shakePivot.localPosition;
            StopAndRestore();
        }

        public void Play(float shakeDuration, float shakeFrequency,
            float shakeAmplitude, AnimationCurve shakeDecayCurve, Vector3 worldDirection)
        {
            if (shakePivot == null || shakeDuration <= 0f || shakeAmplitude <= 0f)
            {
                StopAndRestore();
                return;
            }

            duration = shakeDuration;
            frequency = Mathf.Max(0f, shakeFrequency);
            amplitude = shakeAmplitude;
            decayCurve = shakeDecayCurve;
            // Keep grounded hits horizontal; the animation owns vertical reactions.
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
                worldDirection = Vector3.forward;
            Vector3 worldImpulse = worldDirection.normalized * amplitude;
            impulse = shakePivot.parent != null
                ? shakePivot.parent.InverseTransformVector(worldImpulse)
                : worldImpulse;
            // A new hit keeps the current displacement instead of jumping to a new phase.
            Vector3 initialOffset = playing
                ? shakePivot.localPosition - pivotBaseLocalPosition
                : impulse;
            restartCorrection = initialOffset - impulse;
            startTime = Time.unscaledTime;
            playing = true;
            shakePivot.localPosition = pivotBaseLocalPosition + initialOffset;
        }

        private void LateUpdate()
        {
            if (!playing || shakePivot == null)
                return;

            float elapsed = Time.unscaledTime - startTime;
            if (elapsed >= duration)
            {
                StopAndRestore();
                return;
            }
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float decay = decayCurve != null && decayCurve.length > 0
                ? Mathf.Clamp01(decayCurve.Evaluate(normalizedTime))
                : 1f;
            // Keep the return stroke visible: use one authored decay envelope.
            float oscillation = Mathf.Cos(elapsed * frequency * 2f * Mathf.PI);
            float startDecay = decayCurve != null && decayCurve.length > 0
                ? Mathf.Clamp01(decayCurve.Evaluate(0f)) : 1f;
            // Preserve the configured envelope, but always begin at full impact.
            float shapedDecay = startDecay > 0.0001f
                ? Mathf.Clamp01(decay / startDecay) : 1f - normalizedTime;
            // A custom curve may end above zero; smoothly settle in the final 20%.
            float envelope = Mathf.Min(shapedDecay,
                1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, normalizedTime)));
            shakePivot.localPosition = pivotBaseLocalPosition +
                impulse * (oscillation * envelope) +
                restartCorrection * (envelope * Mathf.Exp(-8f * normalizedTime));
        }

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnDestroy()
        {
            StopAndRestore();
        }

        private void StopAndRestore()
        {
            playing = false;
            restartCorrection = Vector3.zero;
            if (shakePivot != null)
                shakePivot.localPosition = pivotBaseLocalPosition;
        }
    }
}
