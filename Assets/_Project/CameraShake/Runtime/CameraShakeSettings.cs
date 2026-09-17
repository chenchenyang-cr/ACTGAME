using System;
using UnityEngine;

namespace CombatCamera
{
    public enum CameraShakeWaveform
    {
        Perlin = 0,
        Sine = 1
    }

    public enum CameraShakeChannel
    {
        Impact,
        Movement,
        Environment,
        Cinematic
    }

    [Serializable]
    public sealed class CameraShakeSettings
    {
        [Header("Amplitude Envelope")]
        [InspectorName("振幅衰减曲线")]
        [Tooltip("X：震动生命周期 0~1；Y：整体振幅倍率。1 为原始幅度，0 为无震动；与各分项曲线相乘。")]
        public AnimationCurve AmplitudeDecayCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Channel And Trauma")]
        public CameraShakeChannel Channel = CameraShakeChannel.Impact;
        [Range(0f, 1f)] public float TraumaPerPulse = 0.65f;
        [Min(1f)] public float TraumaExponent = 2f;

        [Header("Position (Camera Local Space)")]
        public bool EnablePosition = true;
        public CameraShakeWaveform PositionWaveform = CameraShakeWaveform.Perlin;
        [Tooltip("Sine initial phase per axis in degrees.")]
        public Vector3 PositionPhase;
        public Vector3 PositionAmplitude = new Vector3(0.08f, 0.05f, 0.03f);
        [Min(0f)] public float PositionFrequency = 24f;
        public AnimationCurve PositionCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        public int PositionSeed = 137;

        [Header("Rotation (Degrees)")]
        public bool EnableRotation = true;
        public CameraShakeWaveform RotationWaveform = CameraShakeWaveform.Perlin;
        [Tooltip("Sine initial phase per axis in degrees.")]
        public Vector3 RotationPhase;
        public Vector3 RotationAmplitude = new Vector3(1.2f, 0.8f, 0.5f);
        [Min(0f)] public float RotationFrequency = 20f;
        public AnimationCurve RotationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        public int RotationSeed = 251;

        [Header("Field Of View Punch (Degrees)")]
        public bool EnableFov;
        [Tooltip("Positive values widen the view; negative values zoom in.")]
        public float FovAmplitude = 2f;
        [Tooltip("X is normalized shake time; Y is the FOV offset multiplier. Keep the curve at 0 at both ends to return to the original FOV.")]
        public AnimationCurve FovCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0f));

        [Header("Directional Impulse (World Space)")]
        public bool EnableDirectionalImpulse;
        [Tooltip("World-space displacement along the incoming force direction. Use a negative value to recoil against the force.")]
        public float DirectionalPositionAmplitude = -0.35f;
        [Tooltip("Signed deterministic hit response: the initial positive lobe applies the recoil, and the following negative/positive lobes create an overshoot and settling rebound.")]
        public AnimationCurve DirectionalImpulseCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.12f, -0.32f),
            new Keyframe(0.32f, 0.1f),
            new Keyframe(0.58f, -0.025f),
            new Keyframe(1f, 0f));

        public bool HasVisibleOutput()
        {
            bool hasPosition = EnablePosition && PositionFrequency > 0f &&
                               PositionAmplitude.sqrMagnitude > 0.0000001f;
            bool hasRotation = EnableRotation && RotationFrequency > 0f &&
                               RotationAmplitude.sqrMagnitude > 0.0000001f;
            bool hasFov = EnableFov && Mathf.Abs(FovAmplitude) > 0.0001f;
            bool hasDirectionalImpulse = EnableDirectionalImpulse &&
                                         Mathf.Abs(DirectionalPositionAmplitude) > 0.0001f;
            return hasPosition || hasRotation || hasFov || hasDirectionalImpulse;
        }

        public CameraShakeSample Evaluate(float sampleTime, float normalizedTime,
            float intensityScale = 1f)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            intensityScale = Mathf.Max(0f, intensityScale) * EvaluateAmplitudeDecay(normalizedTime);
            if (intensityScale <= 0f)
                return default;

            float positionWeight = intensityScale * EvaluateCurve(PositionCurve, normalizedTime);
            float rotationWeight = intensityScale * EvaluateCurve(RotationCurve, normalizedTime);

            Vector3 position = Vector3.zero;
            if (EnablePosition)
            {
                position.x = SampleWave(PositionWaveform, sampleTime, PositionFrequency, PositionSeed + 11, PositionPhase.x) * PositionAmplitude.x;
                position.y = SampleWave(PositionWaveform, sampleTime, PositionFrequency, PositionSeed + 29, PositionPhase.y) * PositionAmplitude.y;
                position.z = SampleWave(PositionWaveform, sampleTime, PositionFrequency, PositionSeed + 47, PositionPhase.z) * PositionAmplitude.z;
            }

            Vector3 rotation = Vector3.zero;
            if (EnableRotation)
            {
                rotation.x = SampleWave(RotationWaveform, sampleTime, RotationFrequency, RotationSeed + 11, RotationPhase.x) * RotationAmplitude.x;
                rotation.y = SampleWave(RotationWaveform, sampleTime, RotationFrequency, RotationSeed + 29, RotationPhase.y) * RotationAmplitude.y;
                rotation.z = SampleWave(RotationWaveform, sampleTime, RotationFrequency, RotationSeed + 47, RotationPhase.z) * RotationAmplitude.z;
            }

            float fovCurveValue = FovCurve != null
                ? FovCurve.Evaluate(normalizedTime)
                : 0f;
            float fov = EnableFov
                ? FovAmplitude * fovCurveValue * intensityScale
                : 0f;
            return new CameraShakeSample(position * positionWeight,
                rotation * rotationWeight, fov);
        }

        public float EvaluateAmplitudeDecay(float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            return AmplitudeDecayCurve != null && AmplitudeDecayCurve.length > 0
                ? Mathf.Max(0f, AmplitudeDecayCurve.Evaluate(normalizedTime))
                : 1f - normalizedTime;
        }

        private static float EvaluateCurve(AnimationCurve curve, float normalizedTime)
        {
            return curve != null ? curve.Evaluate(normalizedTime) : 1f;
        }

        private static float SampleWave(CameraShakeWaveform waveform, float time,
            float frequency, int seed, float phase)
        {
            if (frequency <= 0f)
                return 0f;

            if (waveform == CameraShakeWaveform.Sine)
                return Mathf.Sin(2f * Mathf.PI * time * frequency + phase * Mathf.Deg2Rad);

            uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
            float xOffset = (hash & 0xffffu) * (1f / 997f) + 0.123f;
            float yOffset = ((hash >> 16) & 0xffffu) * (1f / 991f) + 17.731f;
            return Mathf.PerlinNoise(xOffset + time * frequency, yOffset) * 2f - 1f;
        }
    }

    public readonly struct CameraShakeSample
    {
        public CameraShakeSample(Vector3 position, Vector3 rotation, float fov,
            Vector3 worldPosition = default)
        {
            Position = position;
            Rotation = rotation;
            Fov = fov;
            WorldPosition = worldPosition;
        }

        public Vector3 Position { get; }
        public Vector3 Rotation { get; }
        public float Fov { get; }
        public Vector3 WorldPosition { get; }

        public static CameraShakeSample operator +(CameraShakeSample a, CameraShakeSample b)
        {
            return new CameraShakeSample(
                a.Position + b.Position,
                a.Rotation + b.Rotation,
                a.Fov + b.Fov,
                a.WorldPosition + b.WorldPosition);
        }
    }
}
