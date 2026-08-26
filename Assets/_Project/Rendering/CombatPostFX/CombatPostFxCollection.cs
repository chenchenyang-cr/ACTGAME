using System;
using UnityEngine;

namespace CombatPostFX
{
    [Serializable]
    public class CombatPostFxTrack
    {
        public bool enabled;
        [Tooltip("Normalized range inside the collection playback interval.")]
        public Vector2 timeRange = new Vector2(0f, 1f);
        [Min(0f)] public float intensity = 1f;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        public float Evaluate(float collectionTime)
        {
            if (!enabled || collectionTime < timeRange.x || collectionTime > timeRange.y)
                return 0f;
            float duration = timeRange.y - timeRange.x;
            float localTime = duration > 0.0001f
                ? Mathf.Clamp01((collectionTime - timeRange.x) / duration)
                : 0f;
            float curveValue = curve != null ? curve.Evaluate(localTime) : 1f;
            return Mathf.Max(0f, curveValue * intensity);
        }
    }

    [Serializable] public sealed class RadialBlurTrack : CombatPostFxTrack
    { [Range(0.1f, 3f)] public float sampleDistance = 1f; }

    [Serializable] public sealed class ChromaticAberrationTrack : CombatPostFxTrack
    { [Range(0.1f, 3f)] public float spread = 1f; }

    [Serializable]
    public sealed class VignetteTrack : CombatPostFxTrack
    {
        [Range(0f, 1f)] public float innerRadius = 0.22f;
        [Range(0f, 1.5f)] public float outerRadius = 0.82f;
        public Color color = Color.black;
    }

    [Serializable] public sealed class FlashTrack : CombatPostFxTrack
    { public Color color = Color.white; }

    [Serializable]
    public sealed class ColorTrack : CombatPostFxTrack
    {
        [Range(0f, 1f)] public float desaturation = 1f;
        [Range(0f, 1f)] public float tintStrength;
        public Color tint = Color.white;
    }

    [Serializable]
    public sealed class GlitchTrack : CombatPostFxTrack
    {
        [Range(1f, 60f)] public float speed = 28f;
        [Range(10f, 400f)] public float rowDensity = 150f;
        [Range(0f, 0.2f)] public float displacement = 0.055f;
        [Range(0f, 0.05f)] public float channelSplit = 0.006f;
    }

    [Serializable]
    public sealed class SpeedLinesTrack : CombatPostFxTrack
    {
        [Range(4f, 100f)] public float density = 34f;
        [Range(1f, 40f)] public float sharpness = 18f;
        [Range(0f, 1f)] public float innerRadius = 0.12f;
        [Range(0f, 1.5f)] public float outerRadius = 0.72f;
        [Range(-10f, 10f)] public float rotationSpeed;
        public Color color = Color.white;
    }

    [Serializable]
    public sealed class FilmGrainTrack : CombatPostFxTrack
    {
        [Range(0.25f, 8f)] public float scale = 1f;
        [Range(0f, 60f)] public float speed = 24f;
    }

    // Legacy aggregate data retained only for one-time migration of prototype assets.
    public sealed class CombatPostFxCollection : ScriptableObject
    {
        public RadialBlurTrack radialBlur = new RadialBlurTrack { enabled = true, intensity = 0.45f };
        public ChromaticAberrationTrack chromaticAberration = new ChromaticAberrationTrack
            { enabled = true, intensity = 0.25f };
        public VignetteTrack vignette = new VignetteTrack();
        public FlashTrack flash = new FlashTrack { enabled = true, intensity = 0.18f };
        public ColorTrack color = new ColorTrack();
        public GlitchTrack glitch = new GlitchTrack();
        public SpeedLinesTrack speedLines = new SpeedLinesTrack();
        public FilmGrainTrack filmGrain = new FilmGrainTrack();

        public void ApplyImpactPreset()
        {
            radialBlur = new RadialBlurTrack { enabled = true, intensity = 0.45f, sampleDistance = 1f };
            chromaticAberration = new ChromaticAberrationTrack
                { enabled = true, intensity = 0.25f, spread = 1f };
            vignette = new VignetteTrack { enabled = true, intensity = 0.12f };
            flash = new FlashTrack { enabled = true, intensity = 0.18f,
                color = new Color(1f, 0.9f, 0.72f, 1f) };
            color = new ColorTrack();
            glitch = new GlitchTrack();
            speedLines = new SpeedLinesTrack { enabled = true, intensity = 0.22f };
            filmGrain = new FilmGrainTrack();
        }

        public void ApplyFinisherPreset()
        {
            radialBlur = new RadialBlurTrack { enabled = true, intensity = 0.3f, sampleDistance = 1.2f };
            chromaticAberration = new ChromaticAberrationTrack
                { enabled = true, intensity = 0.5f, spread = 1.2f };
            vignette = new VignetteTrack { enabled = true, intensity = 0.55f };
            flash = new FlashTrack { enabled = true, intensity = 0.38f };
            color = new ColorTrack { enabled = true, intensity = 0.65f, desaturation = 1f };
            glitch = new GlitchTrack { enabled = true, intensity = 0.16f };
            speedLines = new SpeedLinesTrack { enabled = true, intensity = 0.5f,
                rotationSpeed = 0.25f };
            filmGrain = new FilmGrainTrack { enabled = true, intensity = 0.12f };
        }

        public CombatPostFxSettings Evaluate(float normalizedTime, Vector2 center)
        {
            CombatPostFxSettings result = CombatPostFxSettings.Default;
            result.center = center;
            result.radialBlur = radialBlur.Evaluate(normalizedTime);
            result.radialBlurDistance = radialBlur.sampleDistance;
            result.chromaticAberration = chromaticAberration.Evaluate(normalizedTime);
            result.chromaticSpread = chromaticAberration.spread;
            result.vignette = vignette.Evaluate(normalizedTime);
            result.vignetteInner = vignette.innerRadius;
            result.vignetteOuter = Mathf.Max(vignette.innerRadius + 0.001f, vignette.outerRadius);
            result.vignetteColor = vignette.color;
            result.flash = flash.Evaluate(normalizedTime);
            result.flashColor = flash.color;

            float colorWeight = color.Evaluate(normalizedTime);
            result.desaturation = colorWeight * color.desaturation;
            result.tintStrength = colorWeight * color.tintStrength;
            result.tintColor = color.tint;
            result.glitch = glitch.Evaluate(normalizedTime);
            result.glitchSpeed = glitch.speed;
            result.glitchDensity = glitch.rowDensity;
            result.glitchDisplacement = glitch.displacement;
            result.glitchChannelSplit = glitch.channelSplit;
            result.speedLines = speedLines.Evaluate(normalizedTime);
            result.speedLineDensity = speedLines.density;
            result.speedLineSharpness = speedLines.sharpness;
            result.speedLineInnerRadius = speedLines.innerRadius;
            result.speedLineOuterRadius = Mathf.Max(speedLines.innerRadius + 0.001f, speedLines.outerRadius);
            result.speedLineRotationSpeed = speedLines.rotationSpeed;
            result.speedLineColor = speedLines.color;
            result.filmGrain = filmGrain.Evaluate(normalizedTime);
            result.filmGrainScale = filmGrain.scale;
            result.filmGrainSpeed = filmGrain.speed;
            return result;
        }

        private void OnValidate()
        {
            ValidateTrack(radialBlur);
            ValidateTrack(chromaticAberration);
            ValidateTrack(vignette);
            ValidateTrack(flash);
            ValidateTrack(color);
            ValidateTrack(glitch);
            ValidateTrack(speedLines);
            ValidateTrack(filmGrain);
        }

        private static void ValidateTrack(CombatPostFxTrack track)
        {
            if (track == null)
                return;
            float start = Mathf.Clamp01(Mathf.Min(track.timeRange.x, track.timeRange.y));
            float end = Mathf.Clamp01(Mathf.Max(track.timeRange.x, track.timeRange.y));
            track.timeRange = new Vector2(start, end);
        }
    }
}
