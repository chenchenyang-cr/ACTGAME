using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CombatCamera
{
    public static class CameraShakeRuntime
    {
        private struct Source
        {
            public CameraShakeSettings Settings;
            public float SampleTime;
            public float NormalizedTime;
            public float IntensityScale;
        }

        private static readonly Dictionary<int, Source> Sources = new Dictionary<int, Source>();
        private static int nextHandle = 1;
        private static CameraShakeRunner runner;

        public static int Add(CameraShakeSettings settings, float intensityScale = 1f)
        {
            if (settings == null)
                return 0;

            int handle = nextHandle++;
            Sources.Add(handle, new Source
            {
                Settings = settings,
                SampleTime = 0f,
                NormalizedTime = 0f,
                IntensityScale = Mathf.Max(0f, intensityScale)
            });
            return handle;
        }

        public static void Update(int handle, CameraShakeSettings settings, float sampleTime,
            float normalizedTime, float intensityScale = 1f)
        {
            if (handle == 0 || settings == null || !Sources.ContainsKey(handle))
                return;

            Sources[handle] = new Source
            {
                Settings = settings,
                SampleTime = Mathf.Max(0f, sampleTime),
                NormalizedTime = Mathf.Clamp01(normalizedTime),
                IntensityScale = Mathf.Max(0f, intensityScale)
            };
        }

        public static void Remove(int handle)
        {
            if (handle != 0)
                Sources.Remove(handle);
        }

        public static void Pulse(CameraShakeSettings settings, float duration,
            float intensityScale = 1f, bool useUnscaledTime = true)
        {
            if (settings == null)
                return;

            EnsureRunner().StartCoroutine(RunPulse(settings, Mathf.Max(0.01f, duration),
                Mathf.Max(0f, intensityScale), useUnscaledTime));
        }

        public static CameraShakeSample EvaluateCurrent()
        {
            CameraShakeSample result = default;
            foreach (Source source in Sources.Values)
            {
                if (source.Settings == null)
                    continue;
                result += source.Settings.Evaluate(source.SampleTime, source.NormalizedTime,
                    source.IntensityScale);
            }
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Sources.Clear();
            nextHandle = 1;
            runner = null;
        }

        private static IEnumerator RunPulse(CameraShakeSettings settings, float duration,
            float intensityScale, bool useUnscaledTime)
        {
            int handle = Add(settings, intensityScale);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                Update(handle, settings, elapsed, elapsed / duration, intensityScale);
                yield return null;
            }
            Remove(handle);
        }

        private static CameraShakeRunner EnsureRunner()
        {
            if (runner != null)
                return runner;

            var gameObject = new GameObject("[Camera Shake Runtime]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(gameObject);
            runner = gameObject.AddComponent<CameraShakeRunner>();
            return runner;
        }

        private sealed class CameraShakeRunner : MonoBehaviour { }
    }
}
