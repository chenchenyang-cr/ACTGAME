using System;
using UnityEngine;

namespace CombatPostFX
{
    [Serializable]
    public struct CombatPostFxSettings
    {
        [Header("Radial Blur")]
        [Range(0f, 1f)] public float radialBlur;
        [Range(0.1f, 3f)] public float radialBlurDistance;
        [Header("Chromatic Aberration")]
        [Range(0f, 1f)] public float chromaticAberration;
        [Range(0.1f, 3f)] public float chromaticSpread;
        [Header("Vignette")]
        [Range(0f, 1f)] public float vignette;
        [Range(0f, 1f)] public float vignetteInner;
        [Range(0f, 1.5f)] public float vignetteOuter;
        public Color vignetteColor;
        [Header("Flash and Color")]
        [Range(0f, 1f)] public float flash;
        public Color flashColor;
        [Range(0f, 1f)] public float desaturation;
        [Range(0f, 1f)] public float tintStrength;
        public Color tintColor;
        [Header("Glitch")]
        [Range(0f, 1f)] public float glitch;
        [Range(1f, 60f)] public float glitchSpeed;
        [Range(10f, 400f)] public float glitchDensity;
        [Range(0f, 0.2f)] public float glitchDisplacement;
        [Range(0f, 0.05f)] public float glitchChannelSplit;
        [Header("Speed Lines")]
        [Range(0f, 1f)] public float speedLines;
        [Range(4f, 100f)] public float speedLineDensity;
        [Range(1f, 40f)] public float speedLineSharpness;
        [Range(0f, 1f)] public float speedLineInnerRadius;
        [Range(0f, 1.5f)] public float speedLineOuterRadius;
        [Range(-10f, 10f)] public float speedLineRotationSpeed;
        public Color speedLineColor;
        [Header("Film Grain")]
        [Range(0f, 1f)] public float filmGrain;
        [Range(0.25f, 8f)] public float filmGrainScale;
        [Range(0f, 60f)] public float filmGrainSpeed;
        [Header("Focus")]
        public Vector2 center;

        public bool IsVisible => radialBlur > 0.0001f || chromaticAberration > 0.0001f ||
                                 vignette > 0.0001f || flash > 0.0001f ||
                                 desaturation > 0.0001f || tintStrength > 0.0001f ||
                                 glitch > 0.0001f || speedLines > 0.0001f || filmGrain > 0.0001f;

        public static CombatPostFxSettings Default
        {
            get
            {
                CombatPostFxSettings value = default;
                value.radialBlurDistance = 1f;
                value.chromaticSpread = 1f;
                value.vignetteInner = 0.22f;
                value.vignetteOuter = 0.82f;
                value.vignetteColor = Color.black;
                value.flashColor = Color.white;
                value.tintColor = Color.white;
                value.glitchSpeed = 28f;
                value.glitchDensity = 150f;
                value.glitchDisplacement = 0.055f;
                value.glitchChannelSplit = 0.006f;
                value.speedLineDensity = 34f;
                value.speedLineSharpness = 18f;
                value.speedLineInnerRadius = 0.12f;
                value.speedLineOuterRadius = 0.72f;
                value.speedLineColor = Color.white;
                value.filmGrainScale = 1f;
                value.filmGrainSpeed = 24f;
                value.center = new Vector2(0.5f, 0.5f);
                return value;
            }
        }

        public static CombatPostFxSettings Impact
        {
            get
            {
                CombatPostFxSettings value = Default;
                value.radialBlur = 0.55f;
                value.chromaticAberration = 0.35f;
                value.vignette = 0.18f;
                value.flash = 0.22f;
                value.flashColor = new Color(1f, 0.9f, 0.72f, 1f);
                value.desaturation = 0.08f;
                value.speedLines = 0.32f;
                value.filmGrain = 0.06f;
                return value;
            }
        }

        public static CombatPostFxSettings Finisher
        {
            get
            {
                CombatPostFxSettings value = Default;
                value.radialBlur = 0.3f;
                value.chromaticAberration = 0.55f;
                value.vignette = 0.6f;
                value.flash = 0.38f;
                value.desaturation = 0.65f;
                value.glitch = 0.18f;
                value.speedLines = 0.5f;
                value.filmGrain = 0.12f;
                return value;
            }
        }
    }
}
