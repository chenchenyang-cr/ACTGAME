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

    public enum DirectionalShakeDirectionMode
    {
        [InspectorName("自动（攻击方向）")] Automatic,
        [InspectorName("手动指定")] Manual
    }

    public enum DirectionalShakeSpace
    {
        [InspectorName("镜头局部坐标")] CameraLocal,
        [InspectorName("世界坐标")] World
    }

    public enum DirectionalRotationMode
    {
        [InspectorName("手动旋转轴")] Manual,
        [InspectorName("自动跟随攻击方向")] FollowAttack
    }

    [Serializable]
    public sealed class CameraShakeSettings
    {
        [Header("Amplitude Envelope")]
        [InspectorName("振幅衰减曲线")]
        [Tooltip("X：震动生命周期 0~1；Y：Position / Rotation / FOV 的振幅倍率，与各分项曲线相乘。定向震荡使用自己的强度衰减曲线。")]
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
        [Tooltip("X：轨道进度 0~1；Y：频率倍率。实际频率不乘动画播放速度。")]
        public AnimationCurve PositionFrequencyCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public AnimationCurve PositionCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        public int PositionSeed = 137;

        [Header("Rotation (Degrees)")]
        public bool EnableRotation = true;
        public CameraShakeWaveform RotationWaveform = CameraShakeWaveform.Perlin;
        [Tooltip("Sine initial phase per axis in degrees.")]
        public Vector3 RotationPhase;
        public Vector3 RotationAmplitude = new Vector3(1.2f, 0.8f, 0.5f);
        [Min(0f)] public float RotationFrequency = 20f;
        [Tooltip("X：轨道进度 0~1；Y：频率倍率。实际频率不乘动画播放速度。")]
        public AnimationCurve RotationFrequencyCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
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

        [Header("定向震荡模式（可与 Position / Rotation 叠加）")]
        [InspectorName("启用定向震荡")]
        public bool EnableDirectionalImpulse;
        [InspectorName("方向模式")]
        public DirectionalShakeDirectionMode DirectionalDirectionMode;
        [InspectorName("手动方向坐标系")]
        public DirectionalShakeSpace DirectionalManualSpace;
        [InspectorName("手动方向")]
        [Tooltip("仅手动模式使用。镜头坐标 X 向右、Y 向上、Z 向前。自动归一化；零向量不产生定向位移。")]
        public Vector3 DirectionalManualDirection = Vector3.right;
        [InspectorName("位置强度")]
        [Tooltip("从中心开始沿指定方向正弦往复。绝对值控制位移幅度，负值反转起始运动方向。不受 Trauma 和总振幅衰减曲线影响。")]
        public float DirectionalPositionAmplitude = -0.35f;
        [InspectorName("旋转方向模式")]
        public DirectionalRotationMode DirectionalRotationMode;
        [InspectorName("自动旋转强度 (度)")]
        [Tooltip("自动模式使用。正值先朝攻击方向倾转，负值反向；0 关闭自动旋转。正前后攻击采用镜头俯仰轴。")]
        public float DirectionalAutoRotationAmplitude = 1f;
        [InspectorName("手动旋转强度 (度)")]
        [Tooltip("镜头局部 X：俯仰，Y：左右转动，Z：画面倾斜。各轴独立设置幅度，负值反转方向。与定向位置共用正弦相位、强度曲线和频率；设为 0 可关闭旋转。")]
        public Vector3 DirectionalRotationAmplitude = Vector3.zero;
        [InspectorName("强度衰减曲线")]
        [Tooltip("X：生命周期 0~1；Y：非负强度倍率。往复由正弦波产生，不需要负值关键帧。末端设为 0 可平滑归位。")]
        public AnimationCurve DirectionalIntensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [InspectorName("频率值 (Hz)"), Min(0f)]
        [Tooltip("每秒往复次数。从 0 相位开始；频率值为 0 时不产生震荡。")]
        public float DirectionalFrequency = 18f;
        [InspectorName("频率衰减曲线")]
        [Tooltip("X：生命周期 0~1；Y：频率倍率。频率变化时保持相位连续。")]
        public AnimationCurve DirectionalFrequencyCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [InspectorName("平滑归位时长 (秒)"), Min(0.01f)]
        [Tooltip("震荡持续时间结束后，停止往复，从结束时的偏移平滑回到中心。这段时间额外计算。")]
        public float DirectionalReturnDuration = 0.1f;

        public static float DirectionalReturnWeight(float progress)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        }

        public float EvaluateDirectionalFrequency(float progress)
        {
            return Mathf.Max(0f, DirectionalFrequency) *
                   FrequencyMultiplier(DirectionalFrequencyCurve, Mathf.Clamp01(progress));
        }

        // Integrate for deterministic timeline seeking instead of time * current Hz.
        public float EvaluateDirectionalCycles(float sampleTime, float progress)
        {
            progress = Mathf.Clamp01(progress);
            const int steps = 64;
            float sum = 0f;
            for (int i = 0; i < steps; i++)
                sum += EvaluateDirectionalFrequency(progress * (i + 0.5f) / steps);
            return Mathf.Max(0f, sampleTime) * sum / steps;
        }

        public CameraShakeSample EvaluateDirectional(float cycles, float progress,
            Vector3 worldForceDirection, float intensityScale, Quaternion? cameraOrientation = null)
        {
            if (!EnableDirectionalImpulse || intensityScale <= 0f)
                return default;
            bool manual = DirectionalDirectionMode == DirectionalShakeDirectionMode.Manual;
            Vector3 direction = manual ? DirectionalManualDirection : worldForceDirection;
            progress = Mathf.Clamp01(progress);
            float envelope = DirectionalIntensityCurve != null && DirectionalIntensityCurve.length > 0
                ? Mathf.Max(0f, DirectionalIntensityCurve.Evaluate(progress))
                : 1f - progress;
            // Start at the center and oscillate along the selected direction.
            float wave = envelope * Mathf.Sin(2f * Mathf.PI * cycles) * intensityScale;
            Vector3 offset = direction.sqrMagnitude > 0.000001f
                ? direction.normalized * (DirectionalPositionAmplitude * wave) : Vector3.zero;
            Vector3 rotation = DirectionalRotationAmplitude * wave;
            if (DirectionalRotationMode == DirectionalRotationMode.FollowAttack)
                rotation = AttackRotationAxis(worldForceDirection, cameraOrientation ?? Quaternion.identity) *
                    (DirectionalAutoRotationAmplitude * wave);
            return manual && DirectionalManualSpace == DirectionalShakeSpace.CameraLocal
                ? new CameraShakeSample(offset, rotation, 0f)
                : new CameraShakeSample(Vector3.zero, rotation, 0f, offset);
        }

        public static Vector3 AttackRotationAxis(Vector3 worldAttackDirection, Quaternion cameraOrientation)
        {
            if (worldAttackDirection.sqrMagnitude <= 0.000001f) return Vector3.zero;
            Vector3 localDirection = Quaternion.Inverse(cameraOrientation) * worldAttackDirection.normalized;
            Vector3 axis = Vector3.Cross(Vector3.forward, localDirection);
            // Near the viewing axis, the projected attack has no stable turn direction.
            return axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;
        }

        public bool HasVisibleOutput()
        {
            bool hasPosition = EnablePosition && PositionFrequency > 0f &&
                               PositionAmplitude.sqrMagnitude > 0.0000001f;
            bool hasRotation = EnableRotation && RotationFrequency > 0f &&
                               RotationAmplitude.sqrMagnitude > 0.0000001f;
            bool hasFov = EnableFov && Mathf.Abs(FovAmplitude) > 0.0001f;
            bool hasDirectionalImpulse = EnableDirectionalImpulse &&
                                         ((DirectionalRotationMode == DirectionalRotationMode.FollowAttack
                                             ? Mathf.Abs(DirectionalAutoRotationAmplitude) > 0.0001f
                                             : DirectionalRotationAmplitude.sqrMagnitude > 0.0000001f) ||
                                         (Mathf.Abs(DirectionalPositionAmplitude) > 0.0001f &&
                                         (DirectionalDirectionMode != DirectionalShakeDirectionMode.Manual ||
                                          DirectionalManualDirection.sqrMagnitude > 0.000001f)));
            return hasPosition || hasRotation || hasFov || hasDirectionalImpulse;
        }

        public CameraShakeSample Evaluate(float sampleTime, float normalizedTime,
            float intensityScale = 1f)
        {
            return EvaluateWithPhase(sampleTime * EvaluateFrequencies(normalizedTime),
                normalizedTime, intensityScale);
        }

        public Vector2 EvaluateFrequencies(float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            return new Vector2(
                Mathf.Max(0f, PositionFrequency) * FrequencyMultiplier(PositionFrequencyCurve, normalizedTime),
                Mathf.Max(0f, RotationFrequency) * FrequencyMultiplier(RotationFrequencyCurve, normalizedTime));
        }

        private static float FrequencyMultiplier(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0 ? Mathf.Max(0f, curve.Evaluate(progress)) : 1f;
        }

        // Accumulated cycles keep phase continuous when the frequency curve changes.
        public CameraShakeSample EvaluateWithPhase(Vector2 cycles, float normalizedTime,
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
                position.x = SampleWave(PositionWaveform, cycles.x, PositionFrequency, PositionSeed + 11, PositionPhase.x) * PositionAmplitude.x;
                position.y = SampleWave(PositionWaveform, cycles.x, PositionFrequency, PositionSeed + 29, PositionPhase.y) * PositionAmplitude.y;
                position.z = SampleWave(PositionWaveform, cycles.x, PositionFrequency, PositionSeed + 47, PositionPhase.z) * PositionAmplitude.z;
            }

            Vector3 rotation = Vector3.zero;
            if (EnableRotation)
            {
                rotation.x = SampleWave(RotationWaveform, cycles.y, RotationFrequency, RotationSeed + 11, RotationPhase.x) * RotationAmplitude.x;
                rotation.y = SampleWave(RotationWaveform, cycles.y, RotationFrequency, RotationSeed + 29, RotationPhase.y) * RotationAmplitude.y;
                rotation.z = SampleWave(RotationWaveform, cycles.y, RotationFrequency, RotationSeed + 47, RotationPhase.z) * RotationAmplitude.z;
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

        private static float SampleWave(CameraShakeWaveform waveform, float cycles,
            float frequency, int seed, float phase)
        {
            if (frequency <= 0f)
                return 0f;

            if (waveform == CameraShakeWaveform.Sine)
                return Mathf.Sin(2f * Mathf.PI * cycles + phase * Mathf.Deg2Rad);

            uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
            float xOffset = (hash & 0xffffu) * (1f / 997f) + 0.123f;
            float yOffset = ((hash >> 16) & 0xffffu) * (1f / 991f) + 17.731f;
            return Mathf.PerlinNoise(xOffset + cycles, yOffset) * 2f - 1f;
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
