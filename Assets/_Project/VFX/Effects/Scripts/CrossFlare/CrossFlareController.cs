using UnityEngine;

/// <summary>Single view-aligned particle. Shape, reveal and fade live in CrossFlare.shadergraph.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(ParticleSystem))]
public sealed class CrossFlareController : MonoBehaviour
{
    [Header("形状（世界单位）")]
    [Min(0.01f)] public float horizontalLength = 18f;
    [Min(0.01f)] public float verticalLength = 9f;
    [Min(0.001f)] public float horizontalWidth = 0.026f;
    [Min(0.001f)] public float verticalWidth = 0.032f;
    [Header("边缘柔化")]
    [Min(1f)] public float glowWidth = 16f;
    [Range(0.05f, 1f)] public float endFade = 0.45f;
    [Header("颜色与亮度")]
    [ColorUsage(false, true)] public Color coreColor = new Color(1.8f, 2.25f, 2.8f, 1);
    [ColorUsage(false, true)] public Color glowColor = new Color(0.42f, 0.68f, 1.45f, 1);
    [Min(0)] public float brightness = 1f;
    [Tooltip("横轴：单次播放进度 0～1；纵轴：亮度倍率，可大于 1。与整体亮度及原有淡出相乘。")]
    public AnimationCurve brightnessCurve = new AnimationCurve(
        new Keyframe(0, 0), new Keyframe(0.12f, 0.85f),
        new Keyframe(0.26f, 1.25f), new Keyframe(0.4f, 1f),
        new Keyframe(0.75f, 0.35f), new Keyframe(1, 0));
    [Header("时间（秒）")]
    [Min(0.01f)] public float expandTime = 0.16f;
    [Min(0)] public float holdTime = 0.06f;
    [Min(0.01f)] public float fadeTime = 0.48f;
    public bool playOnEnable = true;
    public bool loop;
    [Min(0)] public float loopInterval = 0.35f;

    ParticleSystem particles;
    ParticleSystemRenderer particleRenderer;
    MaterialPropertyBlock properties;
    Texture2D curveTexture;
    readonly Color[] curveSamples = new Color[CurveSamples];
    public const int CurveSamples = 512;
    public float TotalTime => Mathf.Max(0.01f, expandTime) + Mathf.Max(0, holdTime) + Mathf.Max(0.01f, fadeTime);
    public float CycleTime => TotalTime + Mathf.Max(0, loopInterval);

    public float PeakTime
    {
        get
        {
            float best = -1, peak = 0;
            float fadeStart = (Mathf.Max(0.01f, expandTime) + Mathf.Max(0, holdTime)) / TotalTime;
            for (int i = 0; i <= 512; i++)
            {
                float age = i / 512f;
                float curve = brightnessCurve != null && brightnessCurve.length > 0 ? brightnessCurve.Evaluate(age) : 1;
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(fadeStart, 1, age));
                float appear = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / 0.025f));
                float value = Mathf.Max(0, curve) * fade * appear;
                if (value > best) { best = value; peak = age; }
            }
            return peak * TotalTime;
        }
    }

    void Cache()
    {
        if (!particles) particles = GetComponent<ParticleSystem>();
        if (!particleRenderer) particleRenderer = GetComponent<ParticleSystemRenderer>();
        if (properties == null) properties = new MaterialPropertyBlock();
    }
    void OnEnable() { if (Application.isPlaying && playOnEnable) Play(); }
    void OnDisable() { Stop(); ReleaseCurve(); }
    void OnDestroy() { ReleaseCurve(); }
    void ReleaseCurve()
    {
        if (!curveTexture) return;
        if (Application.isPlaying) Destroy(curveTexture); else DestroyImmediate(curveTexture);
        curveTexture = null;
    }
    void BakeBrightnessCurve()
    {
        if (!curveTexture)
            curveTexture = new Texture2D(CurveSamples, 1, TextureFormat.RFloat, false, true)
            {
                name = "CrossFlare Instance Brightness", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
        for (int i = 0; i < CurveSamples; i++)
        {
            float value = brightnessCurve != null && brightnessCurve.length > 0
                ? brightnessCurve.Evaluate(i / (float)(CurveSamples - 1)) : 1;
            curveSamples[i] = new Color(float.IsFinite(value) ? Mathf.Max(0, value) : 0, 0, 0, 1);
        }
        curveTexture.SetPixels(curveSamples); curveTexture.Apply(false, false);
    }

    public void ApplySettings()
    {
        Cache();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.duration = CycleTime;
        main.startLifetime = TotalTime;
        main.loop = loop;
        main.startSize3D = true;
        main.startSizeX = Mathf.Max(0.01f, horizontalLength);
        main.startSizeY = Mathf.Max(0.01f, verticalLength);
        main.startSizeZ = 1;
        RefreshAppearance();
    }

    public void RefreshAppearance()
    {
        Cache();
        BakeBrightnessCurve();
        particleRenderer.GetPropertyBlock(properties);
        properties.SetFloat("_HorizontalLength", Mathf.Max(0.01f, horizontalLength));
        properties.SetFloat("_VerticalLength", Mathf.Max(0.01f, verticalLength));
        properties.SetFloat("_HorizontalWidth", Mathf.Max(0.001f, horizontalWidth));
        properties.SetFloat("_VerticalWidth", Mathf.Max(0.001f, verticalWidth));
        properties.SetFloat("_GlowWidth", Mathf.Max(1, glowWidth));
        properties.SetFloat("_EndFade", Mathf.Clamp(endFade, 0.05f, 1f));
        properties.SetTexture("_BrightnessCurve", curveTexture);
        properties.SetFloat("_Brightness", Mathf.Max(0, brightness));
        properties.SetFloat("_ExpandFraction", Mathf.Max(0.01f, expandTime) / TotalTime);
        properties.SetFloat("_FadeStart", (Mathf.Max(0.01f, expandTime) + Mathf.Max(0, holdTime)) / TotalTime);
        properties.SetColor("_CoreColor", coreColor);
        properties.SetColor("_GlowColor", glowColor);
        particleRenderer.SetPropertyBlock(properties);
    }

    [ContextMenu("播放")]
    public void Play() { ApplySettings(); particles.Play(true); }
    [ContextMenu("停止")]
    public void Stop() { Cache(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }

    // Also used by the custom Inspector and deterministic render verification.
    public void PreviewAt(float seconds)
    {
        Cache();
        particles.Simulate(Mathf.Max(0, seconds), true, true, false);
        particles.Pause(true);
    }
}
