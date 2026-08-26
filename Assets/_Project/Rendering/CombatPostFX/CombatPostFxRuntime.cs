using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CombatPostFX
{
    public static class CombatPostFxRuntime
    {
        private struct Source
        {
            public CombatPostFxSettings settings;
            public float weight;
        }

        private static readonly Dictionary<int, Source> Sources = new Dictionary<int, Source>();
        private static int _nextHandle = 1;
        private static CombatPostFxRunner _runner;

        public static CombatPostFxSettings Current { get; private set; }

        public static int Add(CombatPostFxSettings settings, float weight = 0f)
        {
            int handle = _nextHandle++;
            Sources[handle] = new Source { settings = settings, weight = Mathf.Max(0f, weight) };
            Rebuild();
            return handle;
        }

        public static void Update(int handle, CombatPostFxSettings settings, float weight)
        {
            if (!Sources.ContainsKey(handle))
                return;

            Sources[handle] = new Source { settings = settings, weight = Mathf.Max(0f, weight) };
            Rebuild();
        }

        public static void Remove(int handle)
        {
            if (Sources.Remove(handle))
                Rebuild();
        }

        public static void Pulse(CombatPostFxSettings settings, float duration = 0.16f,
            AnimationCurve envelope = null)
        {
            EnsureRunner().StartCoroutine(RunPulse(settings, Mathf.Max(0.01f, duration),
                envelope ?? AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)));
        }

        public static void Pulse(CombatPostFxCollection collection, float duration = 0.16f,
            Vector2? center = null)
        {
            if (collection == null)
                return;
            EnsureRunner().StartCoroutine(RunCollection(collection, Mathf.Max(0.01f, duration),
                center ?? new Vector2(0.5f, 0.5f)));
        }

        public static Vector2 WorldToViewport(Vector3 worldPosition, Camera camera = null)
        {
            camera = camera != null ? camera : Camera.main;
            if (camera == null)
                return new Vector2(0.5f, 0.5f);

            Vector3 point = camera.WorldToViewportPoint(worldPosition);
            return point.z > 0f
                ? new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y))
                : new Vector2(0.5f, 0.5f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Sources.Clear();
            Current = default;
            _nextHandle = 1;
            _runner = null;
        }

        private static IEnumerator RunPulse(CombatPostFxSettings settings, float duration,
            AnimationCurve envelope)
        {
            int handle = Add(settings);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Update(handle, settings, envelope.Evaluate(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            Remove(handle);
        }

        private static IEnumerator RunCollection(CombatPostFxCollection collection, float duration,
            Vector2 center)
        {
            int handle = Add(CombatPostFxSettings.Default);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                CombatPostFxSettings frame = collection.Evaluate(Mathf.Clamp01(elapsed / duration), center);
                Update(handle, frame, 1f);
                yield return null;
            }
            Remove(handle);
        }

        private static CombatPostFxRunner EnsureRunner()
        {
            if (_runner != null)
                return _runner;

            var go = new GameObject("[Combat Post FX]") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<CombatPostFxRunner>();
            return _runner;
        }

        private static void Rebuild()
        {
            CombatPostFxSettings result = CombatPostFxSettings.Default;
            Vector2 weightedCenter = Vector2.zero;
            float centerWeight = 0f;

            foreach (Source source in Sources.Values)
            {
                float weight = source.weight;
                CombatPostFxSettings value = source.settings;
                float strength = value.radialBlur * weight;
                if (strength > result.radialBlur)
                {
                    result.radialBlur = strength;
                    result.radialBlurDistance = value.radialBlurDistance;
                }

                strength = value.chromaticAberration * weight;
                if (strength > result.chromaticAberration)
                {
                    result.chromaticAberration = strength;
                    result.chromaticSpread = value.chromaticSpread;
                }

                strength = value.vignette * weight;
                if (strength > result.vignette)
                {
                    result.vignette = strength;
                    result.vignetteInner = value.vignetteInner;
                    result.vignetteOuter = value.vignetteOuter;
                    result.vignetteColor = value.vignetteColor;
                }

                strength = value.flash * weight;
                if (strength > result.flash)
                {
                    result.flash = strength;
                    result.flashColor = value.flashColor;
                }

                result.desaturation = Mathf.Max(result.desaturation, value.desaturation * weight);
                strength = value.tintStrength * weight;
                if (strength > result.tintStrength)
                {
                    result.tintStrength = strength;
                    result.tintColor = value.tintColor;
                }

                strength = value.glitch * weight;
                if (strength > result.glitch)
                {
                    result.glitch = strength;
                    result.glitchSpeed = value.glitchSpeed;
                    result.glitchDensity = value.glitchDensity;
                    result.glitchDisplacement = value.glitchDisplacement;
                    result.glitchChannelSplit = value.glitchChannelSplit;
                }

                strength = value.speedLines * weight;
                if (strength > result.speedLines)
                {
                    result.speedLines = strength;
                    result.speedLineDensity = value.speedLineDensity;
                    result.speedLineSharpness = value.speedLineSharpness;
                    result.speedLineInnerRadius = value.speedLineInnerRadius;
                    result.speedLineOuterRadius = value.speedLineOuterRadius;
                    result.speedLineRotationSpeed = value.speedLineRotationSpeed;
                    result.speedLineColor = value.speedLineColor;
                }

                strength = value.filmGrain * weight;
                if (strength > result.filmGrain)
                {
                    result.filmGrain = strength;
                    result.filmGrainScale = value.filmGrainScale;
                    result.filmGrainSpeed = value.filmGrainSpeed;
                }

                weightedCenter += value.center * weight;
                centerWeight += weight;
            }
            result.center = centerWeight > 0.0001f
                ? weightedCenter / centerWeight
                : new Vector2(0.5f, 0.5f);
            Current = result;
        }

        private sealed class CombatPostFxRunner : MonoBehaviour { }
    }
}
