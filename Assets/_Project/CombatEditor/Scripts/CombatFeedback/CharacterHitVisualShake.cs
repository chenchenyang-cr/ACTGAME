using UnityEngine;

namespace CombatEditor
{
    [DisallowMultipleComponent]
    public sealed class CharacterHitVisualShake : MonoBehaviour
    {
        private const float NoiseSeedSpacing = 17.137f;

        private Transform shakePivot;
        private Vector3 pivotBaseLocalPosition;
        private AnimationCurve decayCurve;
        private float duration;
        private float frequency;
        private float amplitude;
        private float elapsed;
        private float noiseSeed;
        private int playSequence;
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
            float shakeAmplitude, AnimationCurve shakeDecayCurve)
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
            elapsed = 0f;
            playSequence++;
            noiseSeed = Mathf.Abs(GetInstanceID() * 0.001f) +
                        playSequence * NoiseSeedSpacing;
            playing = true;
        }

        private void LateUpdate()
        {
            if (!playing || shakePivot == null)
                return;

            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float decay = decayCurve != null && decayCurve.length > 0
                ? Mathf.Max(0f, decayCurve.Evaluate(normalizedTime))
                : 1f - normalizedTime;
            float phase = elapsed * frequency;
            Vector3 noise = new Vector3(
                SampleNoise(phase, noiseSeed),
                SampleNoise(phase, noiseSeed + NoiseSeedSpacing),
                SampleNoise(phase, noiseSeed + NoiseSeedSpacing * 2f));
            noise = Vector3.ClampMagnitude(noise, 1f);
            shakePivot.localPosition = pivotBaseLocalPosition +
                                       noise * (amplitude * decay);

            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= duration)
                StopAndRestore();
        }

        private static float SampleNoise(float phase, float seed)
        {
            return Mathf.PerlinNoise(phase, seed) * 2f - 1f;
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
            elapsed = 0f;
            if (shakePivot != null)
                shakePivot.localPosition = pivotBaseLocalPosition;
        }
    }
}
