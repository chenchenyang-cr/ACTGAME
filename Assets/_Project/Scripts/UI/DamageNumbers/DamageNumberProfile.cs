using UnityEngine;
using TMPro;

namespace UnityLearning.UI
{
    public enum DamageNumberAnchor
    {
        [InspectorName("命中点")] HitPoint,
        [InspectorName("敌人位置")] TargetPosition
    }

    public enum DamageNumberShakeDirection
    {
        [InspectorName("水平")] Horizontal,
        [InspectorName("垂直")] Vertical,
        [InspectorName("自定义角度")] Angle,
        [InspectorName("二维自由")] Free
    }

    public enum DamageNumberWaveform
    {
        [InspectorName("正弦")] Sine,
        [InspectorName("柏林噪声")] Perlin
    }

    public readonly struct DamageNumberSample
    {
        public readonly Vector2 Offset;
        public readonly float Scale;
        public readonly float Alpha;
        public readonly float Glow;

        public DamageNumberSample(Vector2 offset, float scale, float alpha, float glow)
        {
            Offset = offset;
            Scale = scale;
            Alpha = alpha;
            Glow = glow;
        }
    }

    [CreateAssetMenu(menuName = "战斗/伤害跳字预设", fileName = "DamageNumberProfile")]
    public sealed class DamageNumberProfile : ScriptableObject
    {
        [Header("生成位置")]
        [InspectorName("世界锚点")] public DamageNumberAnchor Anchor = DamageNumberAnchor.HitPoint;
        [InspectorName("世界位置偏移")] public Vector3 WorldOffset = new Vector3(0f, .25f, 0f);
        [InspectorName("跟随目标")]
        [Tooltip("跟随目标位移；关闭时停留在生成时的世界位置。目标销毁后保留最后位置。")]
        public bool FollowTarget;
        [InspectorName("UI 初始偏移")] public Vector2 InitialOffset = new Vector2(0f, 15f);
        [InspectorName("UI 随机偏移范围")] public Vector2 RandomOffset = new Vector2(16f, 8f);

        [Header("生成后位移（Canvas 参考像素）")]
        [InspectorName("位移幅度")] public Vector2 Movement = new Vector2(20f, 85f);
        [InspectorName("X 位移曲线")] public AnimationCurve MovementX = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [InspectorName("Y 位移曲线")] public AnimationCurve MovementY = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("大小变化")]
        [InspectorName("缩放开始延迟（秒）")][Min(0f)] public float ScaleDelay;
        [InspectorName("缩放持续时间（秒）")][Min(0f)]
        [Tooltip("独立于跳字总时长。0 沿用总持续时间；结束后保持缩放曲线末值。")]
        public float ScaleDuration;
        [InspectorName("初始缩放")][Min(0f)] public float InitialScale = 1f;
        [InspectorName("缩放曲线")]
        [Tooltip("横轴为缩放阶段的进度 0～1，纵轴乘初始缩放。")]
        public AnimationCurve Scale = new AnimationCurve(new Keyframe(0f, .7f),
            new Keyframe(.12f, 1.25f), new Keyframe(.3f, 1f), new Keyframe(1f, .85f));

        [Header("持续与淡出（秒）")]
        [InspectorName("总持续时间")][Min(.01f)] public float Duration = .8f;
        [InspectorName("淡出持续时间")][Min(0f)] public float FadeDuration = .25f;
        [InspectorName("淡出曲线")]
        [Tooltip("横轴为淡出阶段进度，纵轴为透明度。淡出位于总持续时间的最后一段。")]
        public AnimationCurve Fade = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [InspectorName("使用非缩放时间")]
        [Tooltip("开启后不受全局慢动作影响。角色动画顿帧不暂停跳字。")]
        public bool UseUnscaledTime = true;

        [Header("数字自身抖动")]
        [InspectorName("启用抖动")] public bool EnableShake = true;
        [InspectorName("抖动开始延迟（秒）")][Min(0f)] public float ShakeDelay;
        [InspectorName("抖动持续时间（秒）")][Min(0f)]
        [Tooltip("独立于跳字总时长。0 沿用总持续时间；到期立即停止抖动。")]
        public float ShakeDuration;
        [InspectorName("抖动幅度")][Min(0f)] public float ShakeAmplitude = 4f;
        [InspectorName("正弦频率 / 噪声采样速度")][Min(0f)] public float ShakeFrequency = 14f;
        [InspectorName("抖动方向")] public DamageNumberShakeDirection ShakeDirection = DamageNumberShakeDirection.Horizontal;
        [InspectorName("自定义方向角度（度）")] public float ShakeAngle;
        [InspectorName("抖动波形")] public DamageNumberWaveform Waveform = DamageNumberWaveform.Sine;
        [InspectorName("抖动幅度曲线")]
        [Tooltip("横轴为抖动阶段进度 0～1。每个数字具有独立相位和噪声种子。")]
        public AnimationCurve ShakeEnvelope = new AnimationCurve(new Keyframe(0f, 0f),
            new Keyframe(.08f, 1f), new Keyframe(.4f, .35f), new Keyframe(1f, 0f));

        [Header("文字外观")]
        [InspectorName("字体")] public TMP_FontAsset Font;
        [InspectorName("基础字号")][Min(1f)] public float FontSize = 42f;
        [InspectorName("字体粗细")][Range(-.4f, .4f)]
        [Tooltip("相对于字体原材质的字面厚度：负数变细，正数变粗，0 保持原字重。")]
        public float FontWeight;
        [InspectorName("普通命中颜色")] public Color Color = UnityEngine.Color.white;
        [InspectorName("暴击颜色")] public Color CriticalColor = new Color(1f, .75f, .15f);
        [InspectorName("暴击缩放倍率")][Min(0f)] public float CriticalScale = 1.3f;
        [InspectorName("描边颜色")] public Color OutlineColor = new Color(.1f, .05f, .02f, 1f);
        [InspectorName("描边宽度")][Range(0f, .4f)] public float OutlineWidth = .15f;
        [InspectorName("小数位数")][Range(0, 3)]
        [Tooltip("0 位时向上取整，避免正伤害显示为 0；其他位数按指定精度显示。")]
        public int DecimalPlaces;

        [Header("字体发光")]
        [InspectorName("启用字体发光")] public bool EnableGlow;
        [InspectorName("发光颜色")][ColorUsage(true, true)]
        public Color GlowColor = new Color(1f, .75f, .25f, 1f);
        [InspectorName("发光强度")][Min(0f)] public float GlowIntensity = 1.5f;
        [InspectorName("外沿发光范围")][Range(0f, 1f)] public float GlowOuter = .3f;
        [InspectorName("发光衰减曲线")]
        [Tooltip("横轴为跳字生命周期 0～1，纵轴乘发光强度。0 恢复普通文字，1 保持设定强度。")]
        public AnimationCurve GlowDecay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        public float Lifetime => Mathf.Max(.01f, Duration);

        // Used by both the runtime and the Inspector preview.
        public DamageNumberSample Evaluate(float elapsed, int seed)
        {
            float time = Mathf.Clamp(elapsed, 0f, Lifetime);
            float progress = time / Lifetime;
            Vector2 offset = new Vector2(Movement.x * Curve(MovementX, progress, progress),
                Movement.y * Curve(MovementY, progress, progress));
            offset += EvaluateShake(time, seed);
            float fadeDuration = Mathf.Clamp(FadeDuration, 0f, Lifetime);
            float alpha = time >= Lifetime ? 0f : fadeDuration > 0f && time >= Lifetime - fadeDuration
                ? Mathf.Clamp01(Curve(Fade, (time - Lifetime + fadeDuration) / fadeDuration, 0f)) : 1f;
            return new DamageNumberSample(offset,
                Mathf.Max(0f, InitialScale * Curve(Scale, PhaseProgress(time, ScaleDelay, ScaleDuration), 1f)), alpha,
                EnableGlow ? Mathf.Max(0f, GlowIntensity) * Mathf.Max(0f, Curve(GlowDecay, progress, 1f)) : 0f);
        }

        private float PhaseProgress(float time, float delay, float duration)
            => Mathf.Clamp01((time - Mathf.Max(0f, delay)) / (duration > 0f ? duration : Lifetime));

        private Vector2 EvaluateShake(float elapsed, int seed)
        {
            if (!EnableShake || ShakeAmplitude <= 0f || ShakeFrequency <= 0f) return Vector2.zero;
            float localTime = elapsed - Mathf.Max(0f, ShakeDelay);
            float duration = ShakeDuration > 0f ? ShakeDuration : Lifetime;
            if (localTime < 0f || localTime >= duration) return Vector2.zero;
            float progress = localTime / duration;
            float amplitude = Mathf.Max(0f, ShakeAmplitude) * Mathf.Max(0f, Curve(ShakeEnvelope, progress, 1f));
            float x = Wave(localTime * ShakeFrequency, seed);
            if (ShakeDirection == DamageNumberShakeDirection.Free)
                return new Vector2(x, Wave(localTime * ShakeFrequency, unchecked(seed + 719))) * amplitude;
            float angle = ShakeDirection == DamageNumberShakeDirection.Horizontal ? 0f :
                ShakeDirection == DamageNumberShakeDirection.Vertical ? 90f : ShakeAngle;
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (x * amplitude);
        }

        private float Wave(float cycles, int seed)
        {
            uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
            float phase = (hash & 0xffffu) / 65535f;
            if (Waveform == DamageNumberWaveform.Sine)
                return Mathf.Sin(2f * Mathf.PI * (cycles + phase));
            return Mathf.PerlinNoise(phase * 71f + cycles, ((hash >> 16) & 0xffffu) / 997f + 17f) * 2f - 1f;
        }

        private static float Curve(AnimationCurve curve, float progress, float fallback)
            => curve != null && curve.length > 0 ? curve.Evaluate(progress) : fallback;
    }
}
