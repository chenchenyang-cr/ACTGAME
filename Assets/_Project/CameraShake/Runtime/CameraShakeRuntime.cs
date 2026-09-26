using System.Collections.Generic;
using UnityEngine;

namespace CombatCamera
{
    public static class CameraShakeRuntime
    {
        private struct ContinuousSource
        {
            public CameraShakeSettings Settings;
            public Vector2 WaveCycles;
            public float SampleTime;
            public float NormalizedTime;
            public float IntensityScale;
            public Vector3 WorldForceDirection;
            public float DirectionalIntensityScale;
        }

        private sealed class TraumaLayer
        {
            public CameraShakeSettings Settings;
            public float Trauma;
            public float DecayPerSecond;
            public Vector2 WaveCycles;
            public float TimeSincePulse;
            public float PulseDuration;
            public bool UseUnscaledTime;
            public System.Func<float> TimeScale;
        }

        private sealed class DirectionalImpulse
        {
            public CameraShakeSettings Settings;
            public Vector3 WorldDirection;
            public float Duration;
            public float Elapsed;
            public float IntensityScale;
            public bool UseUnscaledTime;
            public System.Func<float> TimeScale;
        }

        private sealed class TimedContinuousSource
        {
            public float Duration;
            public float Elapsed;
            public System.Func<float> TimeScale;
        }

        private static readonly Dictionary<int, ContinuousSource> ContinuousSources =
            new Dictionary<int, ContinuousSource>();
        private static readonly Dictionary<CameraShakeChannel, TraumaLayer> TraumaLayers =
            new Dictionary<CameraShakeChannel, TraumaLayer>();
        private static readonly Dictionary<int, DirectionalImpulse> DirectionalImpulses =
            new Dictionary<int, DirectionalImpulse>();
        private static readonly Dictionary<int, TimedContinuousSource> TimedContinuousSources =
            new Dictionary<int, TimedContinuousSource>();
        private static readonly Dictionary<CameraShakeChannel, CameraShakeSample> ChannelMix =
            new Dictionary<CameraShakeChannel, CameraShakeSample>();
        private static readonly List<CameraShakeChannel> ExpiredTraumaChannels =
            new List<CameraShakeChannel>();
        private static readonly List<int> ExpiredImpulseHandles = new List<int>();
        private static readonly List<int> ExpiredContinuousHandles = new List<int>();

        private static int nextHandle = 1;
        private static CameraShakeRunner runner;

        public static int Add(CameraShakeSettings settings, float intensityScale = 1f)
        {
            if (settings == null)
                return 0;

            int handle = nextHandle++;
            ContinuousSources.Add(handle, new ContinuousSource
            {
                Settings = settings,
                SampleTime = 0f,
                NormalizedTime = 0f,
                IntensityScale = Mathf.Max(0f, intensityScale),
                WorldForceDirection = Vector3.zero,
                DirectionalIntensityScale = 0f
            });
            return handle;
        }

        public static void Update(int handle, CameraShakeSettings settings, float sampleTime,
            float normalizedTime, float intensityScale = 1f,
            Vector3 worldForceDirection = default,
            float directionalIntensityScale = 0f, bool resetPhase = false)
        {
            if (handle == 0 || settings == null ||
                !ContinuousSources.TryGetValue(handle, out ContinuousSource previous))
                return;

            sampleTime = Mathf.Max(0f, sampleTime);
            normalizedTime = Mathf.Clamp01(normalizedTime);
            Vector2 frequencies = settings.EvaluateFrequencies(normalizedTime);
            // Only elapsed clock time advances the wave. Timeline progress selects Hz.
            Vector2 cycles = resetPhase || sampleTime < previous.SampleTime
                ? sampleTime * frequencies
                : previous.WaveCycles + (sampleTime - previous.SampleTime) * frequencies;
            ContinuousSources[handle] = new ContinuousSource
            {
                Settings = settings,
                WaveCycles = cycles,
                SampleTime = Mathf.Max(0f, sampleTime),
                NormalizedTime = Mathf.Clamp01(normalizedTime),
                IntensityScale = Mathf.Max(0f, intensityScale),
                WorldForceDirection = worldForceDirection.sqrMagnitude > 0.000001f
                    ? worldForceDirection.normalized
                    : Vector3.zero,
                DirectionalIntensityScale = Mathf.Max(0f,
                    directionalIntensityScale)
            };
            RefreshEditorCamera();
        }

        public static void Remove(int handle)
        {
            TimedContinuousSources.Remove(handle);
            if (handle != 0 && ContinuousSources.Remove(handle))
                RefreshEditorCamera();
        }

        public static void ContinueTimed(int handle, float duration, float elapsed,
            System.Func<float> timeScale)
        {
            if (handle == 0 || !ContinuousSources.ContainsKey(handle))
                return;

            EnsureRunner();
            TimedContinuousSources[handle] = new TimedContinuousSource
            {
                Duration = Mathf.Max(0.01f, duration),
                Elapsed = Mathf.Max(0f, elapsed),
                TimeScale = timeScale
            };
        }

        public static void ClearContinuousSources()
        {
            ContinuousSources.Clear();
            TimedContinuousSources.Clear();
            RefreshEditorCamera();
        }

        private static void RefreshEditorCamera()
        {
#if UNITY_EDITOR
            // Repainting alone does not run Cinemachine's edit-mode LateUpdate.
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        public static void Pulse(CameraShakeSettings settings, float duration,
            float intensityScale = 1f, bool useUnscaledTime = true,
            Vector3 worldForceDirection = default, System.Func<float> timeScale = null)
        {
            if (settings == null || intensityScale <= 0f)
                return;

            EnsureRunner();
            duration = Mathf.Max(0.01f, duration);
            AddTrauma(settings, duration, intensityScale, useUnscaledTime, timeScale);

            if (!settings.EnableDirectionalImpulse)
                return;

            int handle = nextHandle++;
            DirectionalImpulses.Add(handle, new DirectionalImpulse
            {
                Settings = settings,
                WorldDirection = worldForceDirection.normalized,
                Duration = duration,
                Elapsed = 0f,
                IntensityScale = Mathf.Max(0f, intensityScale),
                UseUnscaledTime = useUnscaledTime,
                TimeScale = timeScale
            });
        }

        public static CameraShakeSample EvaluateCurrent(Quaternion? cameraOrientation = null)
        {
            ChannelMix.Clear();

            foreach (ContinuousSource source in ContinuousSources.Values)
            {
                if (source.Settings == null)
                    continue;
                CameraShakeSample sample = source.Settings.EvaluateWithPhase(source.WaveCycles,
                    source.NormalizedTime, source.IntensityScale);
                CameraShakeSample directional = source.Settings.EvaluateDirectional(
                    source.SampleTime, source.WorldForceDirection,
                    source.DirectionalIntensityScale,
                    cameraOrientation);
                AddToChannel(source.Settings.Channel, sample + directional);
            }

            foreach (KeyValuePair<CameraShakeChannel, TraumaLayer> pair in TraumaLayers)
            {
                TraumaLayer layer = pair.Value;
                if (layer.Settings == null || layer.Trauma <= 0f)
                    continue;

                float exponent = Mathf.Max(1f, layer.Settings.TraumaExponent);
                float intensity = Mathf.Pow(Mathf.Clamp01(layer.Trauma), exponent);
                float normalizedTime = Mathf.Clamp01(layer.TimeSincePulse /
                                                      Mathf.Max(0.01f,
                                                          layer.PulseDuration));
                AddToChannel(pair.Key, layer.Settings.EvaluateWithPhase(layer.WaveCycles,
                    normalizedTime, intensity));
            }

            CameraShakeSample result = default;
            foreach (CameraShakeSample channelSample in ChannelMix.Values)
                result += channelSample;

            foreach (DirectionalImpulse impulse in DirectionalImpulses.Values)
            {
                if (impulse.Settings == null)
                    continue;

                result += impulse.Settings.EvaluateDirectional(impulse.Elapsed,
                    impulse.WorldDirection, impulse.IntensityScale, cameraOrientation);
            }

            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ContinuousSources.Clear();
            TraumaLayers.Clear();
            DirectionalImpulses.Clear();
            TimedContinuousSources.Clear();
            ChannelMix.Clear();
            ExpiredTraumaChannels.Clear();
            ExpiredImpulseHandles.Clear();
            ExpiredContinuousHandles.Clear();
            nextHandle = 1;
            runner = null;
        }

        private static void AddTrauma(CameraShakeSettings settings, float duration,
            float intensityScale, bool useUnscaledTime, System.Func<float> timeScale)
        {
            float amount = Mathf.Clamp01(settings.TraumaPerPulse * intensityScale);
            if (amount <= 0f)
                return;

            float decayPerSecond = amount / duration;
            if (!TraumaLayers.TryGetValue(settings.Channel, out TraumaLayer layer))
            {
                TraumaLayers.Add(settings.Channel, new TraumaLayer
                {
                    Settings = settings,
                    Trauma = amount,
                    DecayPerSecond = decayPerSecond,
                    WaveCycles = Vector2.zero,
                    TimeSincePulse = 0f,
                    PulseDuration = duration,
                    UseUnscaledTime = useUnscaledTime,
                    TimeScale = timeScale
                });
                return;
            }

            layer.Settings = settings;
            layer.Trauma = Mathf.Clamp01(layer.Trauma + amount);
            layer.DecayPerSecond = layer.DecayPerSecond > 0f
                ? Mathf.Min(layer.DecayPerSecond, decayPerSecond)
                : decayPerSecond;
            layer.WaveCycles = Vector2.zero;
            layer.TimeSincePulse = 0f;
            layer.PulseDuration = Mathf.Max(layer.PulseDuration, duration);
            layer.UseUnscaledTime = useUnscaledTime;
            layer.TimeScale = timeScale;
        }

        private static void AddToChannel(CameraShakeChannel channel,
            CameraShakeSample sample)
        {
            if (ChannelMix.TryGetValue(channel, out CameraShakeSample current))
                ChannelMix[channel] = current + sample;
            else
                ChannelMix.Add(channel, sample);
        }

        private static void Advance(float scaledDeltaTime, float unscaledDeltaTime)
        {
            ExpiredContinuousHandles.Clear();
            foreach (KeyValuePair<int, TimedContinuousSource> pair in TimedContinuousSources)
            {
                TimedContinuousSource timed = pair.Value;
                if (!ContinuousSources.TryGetValue(pair.Key, out ContinuousSource source) ||
                    source.Settings == null)
                {
                    ExpiredContinuousHandles.Add(pair.Key);
                    continue;
                }

                float timeScale = timed.TimeScale != null
                    ? Mathf.Max(0f, timed.TimeScale()) : 1f;
                timed.Elapsed += unscaledDeltaTime * timeScale;
                if (timed.Elapsed >= timed.Duration)
                {
                    ExpiredContinuousHandles.Add(pair.Key);
                    continue;
                }

                Update(pair.Key, source.Settings, timed.Elapsed,
                    timed.Elapsed / timed.Duration, source.IntensityScale,
                    source.WorldForceDirection, source.DirectionalIntensityScale);
            }
            for (int i = 0; i < ExpiredContinuousHandles.Count; i++)
                Remove(ExpiredContinuousHandles[i]);

            ExpiredTraumaChannels.Clear();
            foreach (KeyValuePair<CameraShakeChannel, TraumaLayer> pair in TraumaLayers)
            {
                TraumaLayer layer = pair.Value;
                float deltaTime = layer.UseUnscaledTime
                    ? unscaledDeltaTime
                    : scaledDeltaTime;
                if (layer.TimeScale != null)
                    deltaTime *= Mathf.Max(0f, layer.TimeScale());
                float progress = Mathf.Clamp01(layer.TimeSincePulse / Mathf.Max(0.01f, layer.PulseDuration));
                layer.WaveCycles += deltaTime * layer.Settings.EvaluateFrequencies(progress);
                layer.TimeSincePulse += deltaTime;
                layer.Trauma = Mathf.Max(0f,
                    layer.Trauma - layer.DecayPerSecond * deltaTime);
                if (layer.Trauma <= 0f)
                    ExpiredTraumaChannels.Add(pair.Key);
            }
            for (int i = 0; i < ExpiredTraumaChannels.Count; i++)
                TraumaLayers.Remove(ExpiredTraumaChannels[i]);

            ExpiredImpulseHandles.Clear();
            foreach (KeyValuePair<int, DirectionalImpulse> pair in DirectionalImpulses)
            {
                DirectionalImpulse impulse = pair.Value;
                float deltaTime = impulse.UseUnscaledTime
                    ? unscaledDeltaTime
                    : scaledDeltaTime;
                if (impulse.TimeScale != null)
                    deltaTime *= Mathf.Max(0f, impulse.TimeScale());
                impulse.Elapsed += deltaTime;
                if (impulse.Settings == null || impulse.Elapsed >= impulse.Duration)
                    ExpiredImpulseHandles.Add(pair.Key);
            }
            for (int i = 0; i < ExpiredImpulseHandles.Count; i++)
                DirectionalImpulses.Remove(ExpiredImpulseHandles[i]);
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

        // Cinemachine normally evaluates virtual cameras during LateUpdate.  Advance
        // pulse time after that evaluation so a short hit pulse is guaranteed to be
        // sampled at least once before it can expire (notably after a slow frame).
        [DefaultExecutionOrder(10000)]
        private sealed class CameraShakeRunner : MonoBehaviour
        {
            private void LateUpdate()
            {
                Advance(Time.deltaTime, Time.unscaledDeltaTime);
            }
        }
    }
}
