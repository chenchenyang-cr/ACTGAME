using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CombatCamera
{
    public enum CameraShakeWaveform
    {
        [InspectorName("随机抖动")]
        Perlin = 0,
        [InspectorName("规律往复")]
        Sine = 1
    }

    public enum CameraShakeChannel
    {
        [InspectorName("受击反馈")] Impact,
        [InspectorName("移动反馈")] Movement,
        [InspectorName("环境反馈")] Environment,
        [InspectorName("镜头演出")] Cinematic
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
        [Header("普通震动整体强度")]
        [InspectorName("普通强度变化")]
        [Tooltip("控制普通位移、转动和视野变化的整体强度；不影响定向震动。横轴是震动进度，纵轴是强度倍率。")]
        public AnimationCurve AmplitudeDecayCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("震动叠加")]
        [InspectorName("震动分组")]
        public CameraShakeChannel Channel = CameraShakeChannel.Impact;
        [InspectorName("单次命中强度")]
        [Tooltip("每次确认命中增加的普通震动强度。")]
        [Range(0f, 1f)] public float TraumaPerPulse = 0.65f;
        [InspectorName("轻微震动抑制")]
        [Tooltip("数值越大，较弱的命中震动越不明显；强命中仍可达到完整强度。")]
        [Min(1f)] public float TraumaExponent = 2f;

        [Header("镜头位移抖动")]
        [InspectorName("启用位移抖动")]
        public bool EnablePosition = true;
        [InspectorName("位移轨迹")]
        public CameraShakeWaveform PositionWaveform = CameraShakeWaveform.Perlin;
        [InspectorName("位移起步位置")]
        [Tooltip("规律往复时各轴的起始相位，单位度。0 从中心起步，90 从最大偏移起步。")]
        public Vector3 PositionPhase;
        [InspectorName("位移范围")]
        [Tooltip("镜头局部坐标中各轴的最大位移范围，单位米。")]
        public Vector3 PositionAmplitude = new Vector3(0.08f, 0.05f, 0.03f);
        [InspectorName("位移抖动快慢")]
        [Tooltip("每秒往复次数；数值越大，位移抖动越快。")]
        [Min(0f)] public float PositionFrequency = 24f;
        [InspectorName("位移快慢变化")]
        [Tooltip("横轴是震动进度，纵轴是位移抖动快慢的倍率；不乘动画播放速度。")]
        public AnimationCurve PositionFrequencyCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [InspectorName("位移幅度变化")]
        [Tooltip("横轴是震动进度，纵轴是位移范围的倍率。")]
        public AnimationCurve PositionCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [InspectorName("位移随机形态")]
        [Tooltip("随机抖动时改变位移轨迹的形态；相同数值会产生相同结果。")]
        public int PositionSeed = 137;

        [Header("镜头转动抖动")]
        [InspectorName("启用转动抖动")]
        public bool EnableRotation = true;
        [InspectorName("转动轨迹")]
        public CameraShakeWaveform RotationWaveform = CameraShakeWaveform.Perlin;
        [InspectorName("转动起步角度")]
        [Tooltip("规律往复时各轴的起始相位，单位度。0 从中心起步，90 从最大转角起步。")]
        public Vector3 RotationPhase;
        [InspectorName("转动范围")]
        [Tooltip("镜头局部坐标中各轴的最大转角，单位度。")]
        public Vector3 RotationAmplitude = new Vector3(1.2f, 0.8f, 0.5f);
        [InspectorName("转动抖动快慢")]
        [Tooltip("每秒往复次数；数值越大，转动抖动越快。")]
        [Min(0f)] public float RotationFrequency = 20f;
        [InspectorName("转动快慢变化")]
        [Tooltip("横轴是震动进度，纵轴是转动抖动快慢的倍率；不乘动画播放速度。")]
        public AnimationCurve RotationFrequencyCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [InspectorName("转动幅度变化")]
        [Tooltip("横轴是震动进度，纵轴是转动范围的倍率。")]
        public AnimationCurve RotationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [InspectorName("转动随机形态")]
        [Tooltip("随机抖动时改变转动轨迹的形态；相同数值会产生相同结果。")]
        public int RotationSeed = 251;

        [Header("视野伸缩")]
        [InspectorName("启用视野伸缩")]
        public bool EnableFov;
        [InspectorName("视野变化幅度")]
        [Tooltip("正值扩大视野，负值缩小视野，单位度。")]
        public float FovAmplitude = 2f;
        [InspectorName("视野变化过程")]
        [Tooltip("横轴是震动进度，纵轴是视野变化幅度的倍率。首尾设为 0 可回到原视野。")]
        public AnimationCurve FovCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0f));

        [Header("定向往返震动（可与普通震动叠加）")]
        [InspectorName("启用定向震动")]
        public bool EnableDirectionalImpulse;
        [InspectorName("方向模式")]
        public DirectionalShakeDirectionMode DirectionalDirectionMode;
        [InspectorName("手动方向坐标系")]
        public DirectionalShakeSpace DirectionalManualSpace;
        [InspectorName("自动位移范围 (米)"), Min(0f)]
        [Tooltip("自动模式中沿攻击方向从中心到折返点的距离。0 表示不产生定向位移。")]
        [FormerlySerializedAs("DirectionalInitialPosition")]
        [FormerlySerializedAs("DirectionalPositionAmplitude")]
        public float DirectionalPositionRange = 0.35f;
        [InspectorName("各轴位移范围 (米)")]
        [Tooltip("手动模式的位移方向和各轴范围：X 左右、Y 上下、Z 前后。全为 0 时不移动。")]
        public Vector3 DirectionalManualPositionRange = new Vector3(0.35f, 0f, 0f);
        [InspectorName("位移每秒往返次数"), Min(0f)]
        [Tooltip("位移每秒完成多少轮往返；0 时不产生定向位移。")]
        public float DirectionalPositionFrequency = 18f;
        [InspectorName("位移幅度衰减"), Range(0f, 1f)]
        [Tooltip("每轮往返后位移范围缩小的比例。0 表示不衰减；数值越大，回中越快。")]
        public float DirectionalPositionRangeDecay = 0.35f;
        [InspectorName("旋转方向模式")]
        public DirectionalRotationMode DirectionalRotationMode;
        [InspectorName("旋转往返范围 (度)"), Min(0f)]
        [Tooltip("自动模式中从中心到折返点的角度。0 表示不产生定向旋转。")]
        [FormerlySerializedAs("DirectionalInitialAutoRotation")]
        [FormerlySerializedAs("DirectionalAutoRotationAmplitude")]
        public float DirectionalAutoRotationRange = 1f;
        [InspectorName("旋转每秒往返次数"), Min(0f)]
        [Tooltip("旋转每秒完成多少轮往返；0 时不产生定向旋转。")]
        public float DirectionalRotationFrequency = 18f;
        [InspectorName("旋转幅度衰减"), Range(0f, 1f)]
        [Tooltip("每轮往返后旋转范围缩小的比例。0 表示不衰减；数值越大，回中越快。")]
        public float DirectionalRotationRangeDecay = 0.35f;
        [InspectorName("各轴旋转范围 (度)")]
        [Tooltip("手动模式的转动方向和各轴范围：X 俯仰、Y 左右转动、Z 画面倾斜。全为 0 时不旋转。")]
        [FormerlySerializedAs("DirectionalInitialRotation")]
        [FormerlySerializedAs("DirectionalRotationAmplitude")]
        public Vector3 DirectionalRotationRange = Vector3.zero;
        [InspectorName("靠中心加速"), Range(1f, 4f)]
        [Tooltip("同时控制位移和旋转在单轮往返中的速度分布。1 为正弦运动；越大则从中心出发越快，靠近折返点越慢。往返次数和最大范围不变。")]
        public float DirectionalCenterSpeed = 1f;

        private static float Oscillate(float time, float range, float frequency,
            float centerSpeed, float rangeDecay)
        {
            if (Mathf.Abs(range) <= 0.000001f || frequency <= 0f)
                return 0f;
            double cycles = Math.Max(0d, (double)time * frequency);
            double retention = 1d - Mathf.Clamp01(rangeDecay);
            double envelope = retention <= 0d ? Math.Max(0d, 1d - cycles) : Math.Pow(retention, cycles);
            double sine = Math.Sin(2d * Math.PI * cycles);
            if (centerSpeed <= 1f)
                return range * (float)(envelope * sine);
            double speedShape = Math.Min(4d, centerSpeed);
            double wave = Math.Sign(sine) * (1d - Math.Pow(1d - Math.Abs(sine), speedShape));
            return range * (float)(envelope * wave);
        }

        public CameraShakeSample EvaluateDirectional(float sampleTime,
            Vector3 worldForceDirection, float intensityScale, Quaternion? cameraOrientation = null)
        {
            if (!EnableDirectionalImpulse || intensityScale <= 0f)
                return default;
            bool manual = DirectionalDirectionMode == DirectionalShakeDirectionMode.Manual;
            Vector3 direction = manual ? DirectionalManualPositionRange : worldForceDirection;
            float position = Oscillate(sampleTime, 1f, DirectionalPositionFrequency,
                DirectionalCenterSpeed, DirectionalPositionRangeDecay) * intensityScale;
            Vector3 offset = direction.sqrMagnitude > 0.000001f
                ? (manual ? direction : direction.normalized * DirectionalPositionRange) * position
                : Vector3.zero;
            Vector3 rotation = DirectionalRotationRange *
                Oscillate(sampleTime, 1f, DirectionalRotationFrequency,
                    DirectionalCenterSpeed, DirectionalRotationRangeDecay) * intensityScale;
            if (DirectionalRotationMode == DirectionalRotationMode.FollowAttack)
                rotation = AttackRotationAxis(worldForceDirection, cameraOrientation ?? Quaternion.identity) *
                    (Oscillate(sampleTime, DirectionalAutoRotationRange,
                        DirectionalRotationFrequency, DirectionalCenterSpeed,
                        DirectionalRotationRangeDecay) * intensityScale);
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
            bool hasDirectionalRotation = DirectionalRotationFrequency > 0f &&
                                          (DirectionalRotationMode == DirectionalRotationMode.FollowAttack
                                              ? Mathf.Abs(DirectionalAutoRotationRange) > 0.0001f
                                              : DirectionalRotationRange.sqrMagnitude > 0.0000001f);
            bool hasDirectionalPosition = DirectionalPositionFrequency > 0f &&
                                          (DirectionalDirectionMode == DirectionalShakeDirectionMode.Manual
                                              ? DirectionalManualPositionRange.sqrMagnitude > 0.0000001f
                                              : Mathf.Abs(DirectionalPositionRange) > 0.0001f);
            bool hasDirectionalImpulse = EnableDirectionalImpulse &&
                                         (hasDirectionalPosition || hasDirectionalRotation);
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
